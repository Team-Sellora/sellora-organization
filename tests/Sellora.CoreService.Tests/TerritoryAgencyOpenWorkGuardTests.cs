using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sellora.CoreService.Application.TerritoryAssignments;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;
using Xunit;

namespace Sellora.CoreService.Tests;

public sealed class TerritoryAgencyOpenWorkGuardTests
  : IClassFixture<BlockingOpenWorkWebAppFactory>
{
  private readonly BlockingOpenWorkWebAppFactory _factory;

  public TerritoryAgencyOpenWorkGuardTests(
    BlockingOpenWorkWebAppFactory factory)
  {
    _factory = factory;
    _factory.CreateClient();
  }

  [Fact]
  public async Task Put_ReassignmentWithOpenWork_ReturnsConflictAndKeepsAssignment()
  {
    var replacementAgencyId = SeedNorthAgency();

    var response = await PutAsync(
      HierarchyEndpointTestData.NorthTerritoryId,
      replacementAgencyId);

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

    var body = await response.Content.ReadAsStringAsync();
    Assert.Contains("ORDER-123", body);

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var assignments = await db.TerritoryAgencyAssignments
      .IgnoreQueryFilters()
      .Where(item =>
        item.TerritoryId == HierarchyEndpointTestData.NorthTerritoryId)
      .ToListAsync();

    var activeAssignment = Assert.Single(
      assignments.Where(item => item.EndsAt == null));

    Assert.Equal(
      HierarchyEndpointTestData.NorthAgencyId,
      activeAssignment.AgencyId);

    Assert.Single(assignments);
  }

  private async Task<HttpResponseMessage> PutAsync(
    Guid territoryId,
    Guid agencyId)
  {
    var token = TestTokenFactory.CreateToken(
      _factory.Issuer,
      _factory.Audience,
      role: Roles.AreaManager,
      companyId: HierarchyEndpointTestData.CompanyId.ToString(),
      sub: HierarchyEndpointTestData.AreaManagerSubject);

    var client = _factory.CreateClient();

    var request = new HttpRequestMessage(
      HttpMethod.Put,
      $"/api/territories/{territoryId}/agency");

    request.Headers.Authorization =
      new AuthenticationHeaderValue("Bearer", token);

    request.Content = JsonContent.Create(new { agencyId });

    return await client.SendAsync(request);
  }

  private Guid SeedNorthAgency()
  {
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var agencyId = Guid.NewGuid();

    db.Agencies.Add(new Agency
    {
      AgencyId = agencyId,
      CompanyId = HierarchyEndpointTestData.CompanyId,
      ProvinceId = HierarchyEndpointTestData.NorthProvinceId,
      Name = $"Replacement Agency {agencyId:N}",
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    });

    db.SaveChanges();
    return agencyId;
  }
}

public sealed class BlockingOpenWorkWebAppFactory : TestWebAppFactory
{
  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    base.ConfigureWebHost(builder);

    builder.ConfigureTestServices(services =>
    {
      var registration = services.SingleOrDefault(
        descriptor =>
          descriptor.ServiceType == typeof(IOpenWorkChecker));

      if (registration is not null)
      {
        services.Remove(registration);
      }

      services.AddScoped<IOpenWorkChecker, BlockingOpenWorkChecker>();
    });
  }

  private sealed class BlockingOpenWorkChecker : IOpenWorkChecker
  {
    public Task<OpenWorkResult> GetOpenWorkForTerritoryAsync(
      Guid territoryId,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(
        new OpenWorkResult(
          HasOpenWork: true,
          BlockingReferences: new[] { "ORDER-123" }));
  }
}