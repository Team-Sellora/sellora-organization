using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sellora.CoreService.Application.Identity;
using Sellora.CoreService.Application.Territories;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.Territories;

public sealed class TerritoryRegistrationService : ITerritoryRegistrationService
{
  private const string UniqueTerritoryCodeConstraint =
    "uq_territory_company_code";
  private const string UniqueTerritoryNameConstraint =
    "uq_territory_province_name";

  private readonly CoreDbContext _db;
  private readonly ICurrentUserContext _currentUser;
  private readonly ILogger<TerritoryRegistrationService> _logger;

  public TerritoryRegistrationService(
    CoreDbContext db,
    ICurrentUserContext currentUser,
    ILogger<TerritoryRegistrationService> logger)
  {
    _db = db;
    _currentUser = currentUser;
    _logger = logger;
  }

  public async Task<RegisterTerritoryResult> RegisterAsync(
    RegisterTerritoryRequest request,
    CancellationToken cancellationToken = default)
  {
    // Body-shape validation up front so downstream checks never run on
    // garbage. The controller does its own guard; this belt-and-braces
    // check protects any future caller that bypasses the controller.
    if (string.IsNullOrWhiteSpace(request.Code))
    {
      return RegisterTerritoryResult.InvalidRequest(
        "Territory code is required.");
    }

    if (string.IsNullOrWhiteSpace(request.Name))
    {
      return RegisterTerritoryResult.InvalidRequest(
        "Territory name is required.");
    }

    if (request.ProvinceId == Guid.Empty)
    {
      return RegisterTerritoryResult.InvalidRequest(
        "provinceId is required.");
    }

    // Step 1: resolve the caller. The JWT sub is the only identity we trust.
    var callerSub = _currentUser.Subject;
    if (string.IsNullOrEmpty(callerSub))
    {
      _logger.LogWarning(
        "POST /api/territories rejected: authenticated request carried " +
        "no subject claim.");
      return RegisterTerritoryResult.CallerNotAnActiveAreaManager();
    }

    // Step 2: match the sub to a staff profile with role=AreaManager and
    // status=Active. The role/status check runs here — not just at login —
    // because the persisted profile may have been deactivated after the
    // token was issued.
    var callerProfile = await _db.StaffProfiles
      .SingleOrDefaultAsync(
        s => s.IdentitySub == callerSub &&
             s.Role == Roles.AreaManager &&
             s.Status == HierarchyStatus.Active,
        cancellationToken);

    if (callerProfile is null)
    {
      _logger.LogWarning(
        "POST /api/territories rejected: JWT subject {Subject} does not " +
        "resolve to an active AreaManager staff profile in this company.",
        callerSub);
      return RegisterTerritoryResult.CallerNotAnActiveAreaManager();
    }

    // Step 3: the target province must exist in the caller's company.
    // Global tenant filter turns other-company IDs into null here.
    var province = await _db.Provinces
      .SingleOrDefaultAsync(
        p => p.ProvinceId == request.ProvinceId,
        cancellationToken);

    if (province is null)
    {
      return RegisterTerritoryResult.ProvinceNotFound(request.ProvinceId);
    }

    // Step 4: caller must be the *current* active manager of that province.
    // Reading from province_manager_assignment (not from a token claim) is
    // deliberate — assignments can change between sessions, so the check
    // must run on every request.
    var isActiveManager = await _db.ProvinceManagerAssignments
      .AnyAsync(
        a => a.ProvinceId == request.ProvinceId &&
             a.AreaManagerId == callerProfile.StaffProfileId &&
             a.EndsAt == null,
        cancellationToken);

    if (!isActiveManager)
    {
      _logger.LogWarning(
        "POST /api/territories rejected: AreaManager {StaffProfileId} " +
        "(sub {Subject}) attempted to create a territory in province " +
        "{ProvinceId} they do not currently manage.",
        callerProfile.StaffProfileId,
        callerSub,
        request.ProvinceId);
      return RegisterTerritoryResult.ProvinceNotManagedByCaller(
        request.ProvinceId);
    }

    // Step 5: build the row. CompanyId is copied from the province (already
    // tenant-filtered) — never trusted from a client field.
    var territory = new Territory
    {
      TerritoryId = Guid.NewGuid(),
      CompanyId = province.CompanyId,
      ProvinceId = province.ProvinceId,
      Code = request.Code.Trim(),
      Name = request.Name.Trim(),
      GeographicDescription = request.GeographicDescription?.Trim(),
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    };

    _db.Territories.Add(territory);

    try
    {
      await _db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException ex) when (
      IsConstraintViolation(ex, UniqueTerritoryCodeConstraint))
    {
      // The (company_id, code) unique index is the constraint CSP-68 is
      // built around. Message names the conflicting code so the caller
      // knows exactly which value to change — AC3 of US-E1-3.
      return RegisterTerritoryResult.DuplicateTerritoryCode(territory.Code);
    }
    catch (DbUpdateException ex) when (
      IsConstraintViolation(ex, UniqueTerritoryNameConstraint))
    {
      // Not CSP-68's headline case but the (province_id, name) index also
      // exists in the schema; catching it here prevents a raw 500 while
      // still returning a message that names the actual conflict rather
      // than misleading the caller about which constraint fired.
      return RegisterTerritoryResult.DuplicateTerritoryName(
        territory.Name,
        territory.ProvinceId);
    }

    _logger.LogInformation(
      "AreaManager {StaffProfileId} created territory {TerritoryId} " +
      "(code '{TerritoryCode}', name '{TerritoryName}') in province " +
      "{ProvinceId}.",
      callerProfile.StaffProfileId,
      territory.TerritoryId,
      territory.Code,
      territory.Name,
      territory.ProvinceId);

    return RegisterTerritoryResult.Success(territory);
  }

  private static bool IsConstraintViolation(
    DbUpdateException ex,
    string constraintName)
  {
    return ex.InnerException is PostgresException pg &&
           pg.SqlState == PostgresErrorCodes.UniqueViolation &&
           string.Equals(
             pg.ConstraintName,
             constraintName,
             StringComparison.Ordinal);
  }
}