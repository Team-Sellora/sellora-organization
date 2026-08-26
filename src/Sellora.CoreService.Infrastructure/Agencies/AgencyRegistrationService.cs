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
    throw new NotImplementedException();
  }
}