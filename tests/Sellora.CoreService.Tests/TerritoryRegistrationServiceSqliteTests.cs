using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Sellora.CoreService.Application.Territories;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;
using Sellora.CoreService.Infrastructure.Territories;

namespace Sellora.CoreService.Tests;

/// <summary>
/// Territory creation scoping and validation. Duplicate code/name handling
/// relies on PostgreSQL constraint names and is covered by the PostgreSQL
/// fixture tests.
/// </summary>
public sealed class TerritoryRegistrationServiceSqliteTests : IDisposable
{
  private static readonly Guid CompanyId = HierarchyEndpointTestData.CompanyId;

  private readonly SqliteCoreDatabase _database = new();

  public TerritoryRegistrationServiceSqliteTests()
  {
    using var db = _database.CreateDbContext();
    HierarchyEndpointTestData.Seed(db);
  }

  public void Dispose() => _database.Dispose();

  [Fact]
  public async Task Register_ByProvinceManager_CreatesTrimmedActiveTerritory()
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await Service(db, HierarchyEndpointTestData.AreaManagerSubject)
      .RegisterAsync(new RegisterTerritoryRequest(
        HierarchyEndpointTestData.NorthProvinceId,
        "  N-02 ",
        " North Coast ",
        " Along the coast "));

    result.Outcome.Should().Be(RegisterTerritoryOutcome.Success);
    result.IsSuccess.Should().BeTrue();
    result.Territory!.Code.Should().Be("N-02");
    result.Territory.Name.Should().Be("North Coast");
    result.Territory.GeographicDescription.Should().Be("Along the coast");
    result.Territory.Status.Should().Be(HierarchyStatus.Active);

    await using var verify = _database.CreateDbContext(CompanyId);
    (await verify.Territories.AnyAsync(territory =>
        territory.TerritoryId == result.Territory.TerritoryId &&
        territory.CompanyId == CompanyId))
      .Should().BeTrue();
  }

  [Theory]
  [InlineData(" ", "Name", false, "Territory code is required.")]
  [InlineData("N-09", " ", false, "Territory name is required.")]
  [InlineData("N-09", "Name", true, "provinceId is required.")]
  public async Task Register_InvalidBody_IsRejected(
    string code,
    string name,
    bool emptyProvince,
    string expectedMessage)
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await Service(db, HierarchyEndpointTestData.AreaManagerSubject)
      .RegisterAsync(new RegisterTerritoryRequest(
        emptyProvince ? Guid.Empty : HierarchyEndpointTestData.NorthProvinceId,
        code,
        name,
        null));

    result.Outcome.Should().Be(RegisterTerritoryOutcome.InvalidRequest);
    result.Message.Should().Be(expectedMessage);
  }

  [Theory]
  [InlineData(null)]
  [InlineData(HierarchyEndpointTestData.SalesRepSubject)]
  public async Task Register_CallerWhoIsNotAnActiveAreaManager_IsRejected(string? subject)
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await Service(db, subject)
      .RegisterAsync(NewRequest(HierarchyEndpointTestData.NorthProvinceId));

    result.Outcome.Should().Be(RegisterTerritoryOutcome.CallerNotAnActiveAreaManager);
  }

  [Fact]
  public async Task Register_UnknownProvince_IsNotFound()
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await Service(db, HierarchyEndpointTestData.AreaManagerSubject)
      .RegisterAsync(NewRequest(HierarchyEndpointTestData.OtherProvinceId));

    result.Outcome.Should().Be(RegisterTerritoryOutcome.ProvinceNotFound);
  }

  [Fact]
  public async Task Register_InProvinceManagedBySomeoneElse_IsRejected()
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await Service(db, HierarchyEndpointTestData.AreaManagerSubject)
      .RegisterAsync(NewRequest(HierarchyEndpointTestData.SouthProvinceId));

    result.Outcome.Should().Be(RegisterTerritoryOutcome.ProvinceNotManagedByCaller);
  }

  [Fact]
  public void DuplicateOutcomes_NameTheConflictingValue()
  {
    var provinceId = HierarchyEndpointTestData.NorthProvinceId;

    RegisterTerritoryResult.DuplicateTerritoryCode("N-01").Message
      .Should().Contain("'N-01'");
    RegisterTerritoryResult.DuplicateTerritoryName("North", provinceId).Message
      .Should().Contain("'North'").And.Contain(provinceId.ToString());
  }

  private static RegisterTerritoryRequest NewRequest(Guid provinceId) =>
    new(provinceId, "N-99", "New Territory", null);

  private static TerritoryRegistrationService Service(CoreDbContext db, string? subject) =>
    new(
      db,
      new FixedCurrentUserContext(subject, Roles.AreaManager),
      NullLogger<TerritoryRegistrationService>.Instance);
}
