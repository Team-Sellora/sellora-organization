using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Application.Identity;
using Sellora.CoreService.Application.Outbox;
using Sellora.CoreService.Domain.Tenancy;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Tests;

/// <summary>
/// An isolated in-memory SQLite database with the CoreDbContext schema, for
/// service tests that need real EF queries but not PostgreSQL constraints
/// or PostgreSQL-only translations.
/// The connection stays open for the lifetime of this object so every
/// context created from it sees the same data.
/// </summary>
public sealed class SqliteCoreDatabase : IDisposable
{
  private readonly SqliteConnection _connection;
  private readonly DbContextOptions<CoreDbContext> _options;

  public SqliteCoreDatabase()
  {
    _connection = new SqliteConnection("DataSource=:memory:");
    _connection.Open();

    _options = new DbContextOptionsBuilder<CoreDbContext>()
      .UseSqlite(_connection)
      .Options;

    using var db = CreateDbContext();
    db.Database.EnsureCreated();
  }

  /// <summary>A context scoped to one company, or unscoped when null.</summary>
  public CoreDbContext CreateDbContext(Guid? companyId = null) =>
    new(_options, new FixedTenantContext(companyId));

  public void Dispose() => _connection.Dispose();
}

internal sealed class FixedTenantContext : ITenantContext
{
  public FixedTenantContext(Guid? companyId)
  {
    CompanyId = companyId;
  }

  public Guid? CompanyId { get; }
}

internal sealed class FixedCurrentUserContext : ICurrentUserContext
{
  public FixedCurrentUserContext(string? subject, string? role = null)
  {
    Subject = subject;
    Role = role;
  }

  public string? Subject { get; }

  public string? Role { get; }
}

internal sealed class FixedCorrelationIdAccessor : ICorrelationIdAccessor
{
  public string GetCorrelationId() => "test-correlation-id";
}
