using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sellora.CoreService.Application.Agencies;
using Sellora.CoreService.Application.Identity;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.Agencies;

public sealed class AgencyRegistrationService : IAgencyRegistrationService
{
  private const string UniqueAgencyNameConstraint = "uq_agency_province_name";

  private readonly CoreDbContext _db;
  private readonly ICurrentUserContext _currentUser;
  private readonly ILogger<AgencyRegistrationService> _logger;

  public AgencyRegistrationService(
    CoreDbContext db,
    ICurrentUserContext currentUser,
    ILogger<AgencyRegistrationService> logger)
  {
    _db = db;
    _currentUser = currentUser;
    _logger = logger;
  }

  public async Task<RegisterAgencyResult> RegisterAsync(
    RegisterAgencyRequest request,
    CancellationToken cancellationToken = default)
  {
    // Body-shape validation first so downstream checks never run on garbage.
    // The controller does its own null/empty guard; this belt-and-braces
    // check protects any future caller that bypasses the controller.
    if (string.IsNullOrWhiteSpace(request.Name))
    {
      return RegisterAgencyResult.InvalidRequest(
        "Agency name is required.");
    }

    if (request.ProvinceId == Guid.Empty)
    {
      return RegisterAgencyResult.InvalidRequest(
        "provinceId is required.");
    }

    // Step 1: resolve the caller. The JWT sub is the only identity we trust.
    var callerSub = _currentUser.Subject;
    if (string.IsNullOrEmpty(callerSub))
    {
      // A [Authorize] endpoint should never reach here, but if it does the
      // safest response is the same 403 as the not-an-active-AM path.
      _logger.LogWarning(
        "POST /api/agencies rejected: authenticated request carried no " +
        "subject claim.");
      return RegisterAgencyResult.CallerNotAnActiveAreaManager();
    }

    // Step 2: match the sub to a staff profile with role=AreaManager and
    // status=Active. The global tenant filter scopes this to the caller's
    // own company, so a stray sub from another tenant simply misses.
    //
    // We check the role/status here even though the endpoint policy also
    // requires the AreaManager role, because the role claim in the token
    // and the persisted profile are two different truths — the profile
    // could have been deactivated after the token was issued, and the
    // story requires the check on *every* request rather than at login.
    var callerProfile = await _db.StaffProfiles
      .SingleOrDefaultAsync(
        s => s.IdentitySub == callerSub &&
             s.Role == Roles.AreaManager &&
             s.Status == HierarchyStatus.Active,
        cancellationToken);

    if (callerProfile is null)
    {
      _logger.LogWarning(
        "POST /api/agencies rejected: JWT subject {Subject} does not " +
        "resolve to an active AreaManager staff profile in this company.",
        callerSub);
      return RegisterAgencyResult.CallerNotAnActiveAreaManager();
    }

    // Step 3: the target province must exist in the caller's company.
    // Global tenant filter turns other-company IDs into null here, so this
    // one query covers both "province doesn't exist" and "province belongs
    // to another tenant" — both are honestly 404 from the caller's view.
    var province = await _db.Provinces
      .SingleOrDefaultAsync(
        p => p.ProvinceId == request.ProvinceId,
        cancellationToken);

    if (province is null)
    {
      return RegisterAgencyResult.ProvinceNotFound(request.ProvinceId);
    }

    // Step 4: the caller must be the *current* active manager of that
    // province. Reading it from province_manager_assignment (not from a
    // claim) is deliberate: assignments can change between sessions, and
    // the story requires this check on every request. The partial unique
    // index UNIQUE (province_id) WHERE ends_at IS NULL means at most one
    // manager is active per province, so an AnyAsync is definitive.
    var isActiveManager = await _db.ProvinceManagerAssignments
      .AnyAsync(
        a => a.ProvinceId == request.ProvinceId &&
             a.AreaManagerId == callerProfile.StaffProfileId &&
             a.EndsAt == null,
        cancellationToken);

    if (!isActiveManager)
    {
      // Warning, not information: this is the audit trail for someone
      // systematically probing outside their scope. Structured fields keep
      // it queryable in the Serilog sink.
      _logger.LogWarning(
        "POST /api/agencies rejected: AreaManager {StaffProfileId} " +
        "(sub {Subject}) attempted to register an agency in province " +
        "{ProvinceId} they do not currently manage.",
        callerProfile.StaffProfileId,
        callerSub,
        request.ProvinceId);
      return RegisterAgencyResult.ProvinceNotManagedByCaller(
        request.ProvinceId);
    }

    // Step 5: build the row. CompanyId is copied from the province, not the
    // request — the province was already tenant-filtered, so this closes
    // the loop that a client-supplied companyId would leave open.
    var agency = new Agency
    {
      AgencyId = Guid.NewGuid(),
      CompanyId = province.CompanyId,
      ProvinceId = province.ProvinceId,
      Name = request.Name.Trim(),
      Email = request.Email?.Trim(),
      Phone = request.Phone?.Trim(),
      Address = request.Address?.Trim(),
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    };

    _db.Agencies.Add(agency);

    try
    {
      await _db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException ex) when (IsUniqueAgencyNameViolation(ex))
    {
      // The (province_id, name) unique index is enforced at the DB level
      // by AgencyConfiguration. CSP-69 will refine the surfaced message
      // and add its dedicated test, but catching here is the difference
      // between a clean 409 and a raw 500 while that story is in flight.
      return RegisterAgencyResult.DuplicateAgencyName(
        agency.Name,
        agency.ProvinceId);
    }

    _logger.LogInformation(
      "AreaManager {StaffProfileId} registered agency {AgencyId} " +
      "('{AgencyName}') in province {ProvinceId}.",
      callerProfile.StaffProfileId,
      agency.AgencyId,
      agency.Name,
      agency.ProvinceId);

    return RegisterAgencyResult.Success(agency);
  }

  private static bool IsUniqueAgencyNameViolation(DbUpdateException ex)
  {
    return ex.InnerException is PostgresException pg &&
           pg.SqlState == PostgresErrorCodes.UniqueViolation &&
           string.Equals(
             pg.ConstraintName,
             UniqueAgencyNameConstraint,
             StringComparison.Ordinal);
  }
}