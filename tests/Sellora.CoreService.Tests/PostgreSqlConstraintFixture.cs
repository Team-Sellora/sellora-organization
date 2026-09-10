using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Domain.Tenancy;
using Sellora.CoreService.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Sellora.CoreService.Tests;

/// <summary>
/// Provides an isolated PostgreSQL 16 database containing the real EF Core
/// migrations. It is used for tests that must verify PostgreSQL constraints
/// rather than application-level validation or SQLite behaviour.
/// </summary>
public sealed class PostgreSqlConstraintFixture : IAsyncLifetime
{
  private readonly PostgreSqlContainer _database =
    new PostgreSqlBuilder("postgres:16")
      .WithDatabase("organization_constraint_tests")
      .WithUsername("sellora_test")
      .WithPassword("sellora_test_password")
      .Build();
  public string ConnectionString => _database.GetConnectionString();

  public async Task InitializeAsync()
  {
    await _database.StartAsync();

    await using var db = CreateDbContext();
    await db.Database.MigrateAsync();
  }

  public async Task DisposeAsync()
  {
    await _database.DisposeAsync();
  }

  public CoreDbContext CreateDbContext(Guid? companyId = null)
  {
    var options = new DbContextOptionsBuilder<CoreDbContext>()
      .UseNpgsql(_database.GetConnectionString())
      .Options;

    return new CoreDbContext(options, new FixedTenantContext(companyId));
  }

  private sealed class FixedTenantContext : ITenantContext
  {
    public FixedTenantContext(Guid? companyId)
    {
      CompanyId = companyId;
    }

    public Guid? CompanyId { get; }
  }
}