using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Tenancy;

namespace Sellora.CoreService.Infrastructure.Persistence;

/// <summary>
/// Organization service database context.
///
/// Every entity implementing ITenantScoped automatically receives a global
/// company filter. This prevents a newly added tenant-owned table from being
/// accidentally left outside tenant isolation.
/// </summary>
public class CoreDbContext : DbContext
{
  private static readonly MethodInfo ApplyTenantFilterMethod =
    typeof(CoreDbContext).GetMethod(
      nameof(ApplyTenantFilter),
      BindingFlags.Instance | BindingFlags.NonPublic)
    ?? throw new InvalidOperationException(
      "The tenant-filter method could not be located.");

  private readonly ITenantContext _tenant;

  public CoreDbContext(
    DbContextOptions<CoreDbContext> options,
    ITenantContext tenant)
    : base(options)
  {
    _tenant = tenant;
  }

  public DbSet<Company> Companies => Set<Company>();
  public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
  public DbSet<Province> Provinces => Set<Province>();
  public DbSet<Agency> Agencies => Set<Agency>();
  public DbSet<Territory> Territories => Set<Territory>();
  public DbSet<Shop> Shops => Set<Shop>();

  public DbSet<ProvinceManagerAssignment>
    ProvinceManagerAssignments => Set<ProvinceManagerAssignment>();

  public DbSet<AgencyOperatorAssignment>
    AgencyOperatorAssignments => Set<AgencyOperatorAssignment>();

  public DbSet<TerritoryAgencyAssignment>
    TerritoryAgencyAssignments => Set<TerritoryAgencyAssignment>();

  public DbSet<SalesRepTerritoryAssignment>
    SalesRepTerritoryAssignments =>
      Set<SalesRepTerritoryAssignment>();

  public DbSet<OutboxMessage>
    OutboxMessages => Set<OutboxMessage>();

  public DbSet<AuditEntry>
    AuditEntries => Set<AuditEntry>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    // This will automatically load the Fluent configuration classes
    // that we create in the next step.
    modelBuilder.ApplyConfigurationsFromAssembly(
      typeof(CoreDbContext).Assembly);

    ApplyTenantFilters(modelBuilder);
  }

  private void ApplyTenantFilters(ModelBuilder modelBuilder)
  {
    var tenantEntityTypes = modelBuilder.Model
      .GetEntityTypes()
      .Where(entityType =>
        typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType));

    foreach (var entityType in tenantEntityTypes)
    {
      ApplyTenantFilterMethod
        .MakeGenericMethod(entityType.ClrType)
        .Invoke(this, new object[] { modelBuilder });
    }
  }

  private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
    where TEntity : class, ITenantScoped
  {
    modelBuilder.Entity<TEntity>()
      .HasQueryFilter(entity =>
        _tenant.CompanyId != null &&
        entity.CompanyId == _tenant.CompanyId);
  }

  public override int SaveChanges(bool acceptAllChangesOnSuccess)
  {
    RejectHierarchyHardDeletes();

    return base.SaveChanges(acceptAllChangesOnSuccess);
  }

  public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
  {
    RejectHierarchyHardDeletes();

    return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
  }

  private void RejectHierarchyHardDeletes()
  {
    var deletedEntityNames = ChangeTracker
      .Entries<ISoftDeactivatable>()
      .Where(entry => entry.State == EntityState.Deleted)
      .Select(entry => entry.Metadata.ClrType.Name)
      .Distinct()
      .OrderBy(name => name)
      .ToArray();

    if (deletedEntityNames.Length == 0)
    {
      return;
    }

    throw new InvalidOperationException(
      "Hard deletion is intentionally unsupported for hierarchy " +
      $"entities ({string.Join(", ", deletedEntityNames)}). " +
      $"Set Status to '{HierarchyStatus.Inactive}' instead.");
  }
}