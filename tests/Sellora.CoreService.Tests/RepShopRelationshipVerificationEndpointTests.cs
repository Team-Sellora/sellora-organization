using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sellora.CoreService.Application.SalesRepAssignments;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;
using System.Net.Http.Json;
using Xunit;

namespace Sellora.CoreService.Tests;

public sealed class RepShopRelationshipVerificationEndpointTests
  : IClassFixture<TestWebAppFactory>
{
  private readonly TestWebAppFactory _factory;

  public RepShopRelationshipVerificationEndpointTests(
    TestWebAppFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task Get_RepCoversShopTerritory_ReturnsValid()
  {
    var salesRepId = GetSalesRepId(
      HierarchyEndpointTestData.SalesRepSubject);

    var response = await GetAsync(
      salesRepId,
      HierarchyEndpointTestData.NorthShopId);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var result = await response.Content.ReadFromJsonAsync<VerifyRepShopRelationshipResponse>();

    Assert.NotNull(result);
    Assert.True(result.IsValid);
    Assert.Null(result.Reason);
  }

  [Fact]
  public async Task Get_RepDoesNotCoverShopTerritory_ReturnsInvalid()
  {
    var salesRepId = GetSalesRepId(
      HierarchyEndpointTestData.SalesRepSubject);

    var response = await GetAsync(
      salesRepId,
      HierarchyEndpointTestData.SouthShopId);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var result = await response.Content.ReadFromJsonAsync<VerifyRepShopRelationshipResponse>();

    Assert.NotNull(result);
    Assert.False(result.IsValid);
    Assert.Equal("repNotAssignedToShopTerritory", result.Reason);
  }

  private Guid GetSalesRepId(string subject)
  {
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    return db.StaffProfiles
      .IgnoreQueryFilters()
      .Single(profile => profile.IdentitySub == subject)
      .StaffProfileId;
  }

  private async Task<HttpResponseMessage> GetAsync(
    Guid salesRepId,
    Guid shopId)
  {
    var token = TestTokenFactory.CreateToken(
      _factory.Issuer,
      _factory.Audience,
      role: Roles.CompanyAdmin,
      companyId: HierarchyEndpointTestData.CompanyId.ToString(),
      sub: "verification-caller");

    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", token);

    return await client.GetAsync(
      $"/api/rep-shop-relationships/verify?repId={salesRepId}&shopId={shopId}");
  }
}