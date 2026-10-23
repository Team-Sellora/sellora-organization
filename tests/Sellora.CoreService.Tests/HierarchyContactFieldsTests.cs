using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Sellora.CoreService.Domain.Identity;

namespace Sellora.CoreService.Tests;

/// <summary>
/// US-E4-4: Order snapshots the agency email and shop owner email from
/// /api/hierarchy, so order events can be emailed without calling back.
/// </summary>
public sealed class HierarchyContactFieldsTests : IClassFixture<TestWebAppFactory>
{
  private readonly TestWebAppFactory _factory;

  public HierarchyContactFieldsTests(TestWebAppFactory factory)
  {
    _factory = factory;
    _factory.CreateClient();
  }

  [Fact]
  public async Task Hierarchy_includes_agency_and_shop_owner_emails()
  {
    var token = TestTokenFactory.CreateToken(
      _factory.Issuer,
      _factory.Audience,
      role: Roles.SalesRep,
      companyId: HierarchyEndpointTestData.CompanyId.ToString(),
      sub: HierarchyEndpointTestData.SalesRepSubject);

    var request = new HttpRequestMessage(HttpMethod.Get, "/api/hierarchy");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var response = await _factory.CreateClient().SendAsync(request);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var agency = json.RootElement.GetProperty("provinces")
      .EnumerateArray()
      .SelectMany(province => province.GetProperty("agencies").EnumerateArray())
      .Single(candidate => candidate.GetProperty("agencyId").GetGuid() == HierarchyEndpointTestData.NorthAgencyId);

    Assert.Equal("north@agency.test", agency.GetProperty("email").GetString());

    var shop = agency.GetProperty("territories")
      .EnumerateArray()
      .SelectMany(territory => territory.GetProperty("shops").EnumerateArray())
      .First();

    Assert.EndsWith("@shop.test", shop.GetProperty("ownerEmail").GetString());
  }
}
