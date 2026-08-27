using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Tests;

public sealed class ShopUpdateEndpointTests
  : IClassFixture<TestWebAppFactory>
{
  private readonly TestWebAppFactory _factory;

  public ShopUpdateEndpointTests(TestWebAppFactory factory)
  {
    _factory = factory;

    // Forces TestWebAppFactory.ConfigureWebHost to run so Issuer and
    // Audience are loaded before TestTokenFactory.CreateToken is called.
    _factory.CreateClient();
  }

  [Fact]
  public async Task Put_ChangingCoordinatesAndCreditLimit_WritesTwoAuditEntries()
  {
    // These are the values seeded for NorthShopId in
    // HierarchyEndpointTestData.
    const decimal oldLatitude = 6.927079m;
    const decimal oldLongitude = 79.861244m;
    const decimal oldCreditLimit = 10000m;

    const decimal newLatitude = 6.934400m;
    const decimal newLongitude = 79.842800m;
    const decimal newCreditLimit = 25000m;

    var token = TestTokenFactory.CreateToken(
      _factory.Issuer,
      _factory.Audience,
      role: "AgencyOperator",
      companyId: HierarchyEndpointTestData.CompanyId.ToString(),
      sub: HierarchyEndpointTestData.AgencyOperatorSubject);

    var request = new HttpRequestMessage(
      HttpMethod.Put,
      $"/api/shops/{HierarchyEndpointTestData.NorthShopId}");

    request.Headers.Authorization =
      new AuthenticationHeaderValue("Bearer", token);

    request.Content = JsonContent.Create(new
    {
      latitude = newLatitude,
      longitude = newLongitude,
      creditLimit = newCreditLimit
    });

    var response = await _factory.CreateClient().SendAsync(request);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var auditEntries = await db.AuditEntries
      .IgnoreQueryFilters()
      .Where(entry =>
        entry.EntityType == "Shop" &&
        entry.EntityId == HierarchyEndpointTestData.NorthShopId)
      .OrderBy(entry => entry.FieldName)
      .ToListAsync();

    Assert.Equal(2, auditEntries.Count);

    var coordinatesAudit = Assert.Single(
      auditEntries,
      entry => entry.FieldName == "Coordinates");

    var creditLimitAudit = Assert.Single(
      auditEntries,
      entry => entry.FieldName == "CreditLimit");

    Assert.Equal(
      HierarchyEndpointTestData.CompanyId,
      coordinatesAudit.CompanyId);

    Assert.Equal(
      HierarchyEndpointTestData.AgencyOperatorSubject,
      coordinatesAudit.ChangedBy);

    Assert.Equal(
      HierarchyEndpointTestData.AgencyOperatorSubject,
      creditLimitAudit.ChangedBy);

    using var oldCoordinates = JsonDocument.Parse(
      coordinatesAudit.OldValue);

    using var newCoordinates = JsonDocument.Parse(
      coordinatesAudit.NewValue);

    Assert.Equal(
      oldLatitude,
      oldCoordinates.RootElement
        .GetProperty("latitude")
        .GetDecimal());

    Assert.Equal(
      oldLongitude,
      oldCoordinates.RootElement
        .GetProperty("longitude")
        .GetDecimal());

    Assert.Equal(
      newLatitude,
      newCoordinates.RootElement
        .GetProperty("latitude")
        .GetDecimal());

    Assert.Equal(
      newLongitude,
      newCoordinates.RootElement
        .GetProperty("longitude")
        .GetDecimal());

    using var oldCredit = JsonDocument.Parse(
      creditLimitAudit.OldValue);

    using var newCredit = JsonDocument.Parse(
      creditLimitAudit.NewValue);

    Assert.Equal(oldCreditLimit, oldCredit.RootElement.GetDecimal());
    Assert.Equal(newCreditLimit, newCredit.RootElement.GetDecimal());

    var updatedShop = await db.Shops
      .IgnoreQueryFilters()
      .SingleAsync(shop =>
        shop.ShopId == HierarchyEndpointTestData.NorthShopId);

    Assert.Equal(newLatitude, updatedShop.Latitude);
    Assert.Equal(newLongitude, updatedShop.Longitude);
    Assert.Equal(newCreditLimit, updatedShop.CreditLimit);
    Assert.NotNull(updatedShop.UpdatedAt);
  }
}