using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Tenancy;

namespace Sellora.CoreService.Infrastructure.Persistence;

/// <summary>
/// The service's EF Core database context. Applies a global query filter so every
/// query against a tenant-scoped entity is automatically restricted to the current
/// company. The filter is defined here centrally — endpoints cannot bypass it.
/// </summary>
public class CoreDbContext : DbContext
{
  private readonly ITenantContext _tenant;

  public CoreDbContext(DbContextOptions<CoreDbContext> options, ITenantContext tenant)
    : base(options)
  {
    _tenant = tenant;
  }

  public DbSet<DemoRecord> DemoRecords => Set<DemoRecord>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    // The tenant filter: every query on DemoRecord is auto-scoped to the
    // current request's company. A user from Company A physically cannot
    // read Company B rows, regardless of any parameter the client sends.
    modelBuilder.Entity<DemoRecord>()
      .HasQueryFilter(r => r.CompanyId == _tenant.CompanyId);
  }
}