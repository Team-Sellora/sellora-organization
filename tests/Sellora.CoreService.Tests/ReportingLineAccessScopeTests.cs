using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Sellora.CoreService.Application.Hierarchy;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Tests;

/// <summary>
/// Reporting lines are visibility-only metadata. They must never be used to
/// calculate an Area Manager's hierarchy access scope.
/// </summary>
public sealed class ReportingLineAccessScopeTests
  : IClassFixture<TestWebAppFactory>
{
  private readonly TestWebAppFactory _factory;

  public ReportingLineAccessScopeTests(TestWebAppFactory factory)
  {
    _factory = factory;
    _factory.CreateClient();
  }

  [Fact]
  public async Task Put_ReportingLineChange_DoesNotAlterAccessScope_AndIsImmediatelyVisibleInRollUp()
  {
    var hierarchyBefore = await GetHierarchyAsync(
      Roles.AreaManager,
      HierarchyEndpointTestData.AreaManagerSubject);
    var replacementAdminId = SeedCompanyAdmin("replacement-reporting-admin");

    var updateResponse = await UpdateReportsToAsync(replacementAdminId);

    Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

    var hierarchyAfter = await GetHierarchyAsync(
      Roles.AreaManager,
      HierarchyEndpointTestData.AreaManagerSubject);

    Assert.Equal(
      ProvinceIds(hierarchyBefore),
      ProvinceIds(hierarchyAfter));
    Assert.Equal(
      AgencyIds(hierarchyBefore),
      AgencyIds(hierarchyAfter));
    Assert.Equal(
      TerritoryIds(hierarchyBefore),
      TerritoryIds(hierarchyAfter));

    var rollUp = await GetRollUpAsync();
    var northProvince = Assert.Single(rollUp, province =>
      province.ProvinceId == HierarchyEndpointTestData.NorthProvinceId);

    Assert.NotNull(northProvince.CurrentManager?.ReportsToAdmin);
    Assert.Equal(
      replacementAdminId,
      northProvince.CurrentManager.ReportsToAdmin.StaffProfileId);
  }

  private async Task<HierarchyTreeResponse> GetHierarchyAsync(
    string role,
    string subject)
  {
    var response = await SendAsync(
      HttpMethod.Get,
      "/api/hierarchy",
      role,
      subject);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    return await response.Content.ReadFromJsonAsync<HierarchyTreeResponse>()
      ?? throw new InvalidOperationException("The hierarchy response body was empty.");
  }

  private async Task<IReadOnlyList<ProvinceRollUpResponse>> GetRollUpAsync()
  {
    var response = await SendAsync(
      HttpMethod.Get,
      "/api/hierarchy/roll-up",
      Roles.CompanyAdmin,
      HierarchyEndpointTestData.CompanyAdminSubject);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    return await response.Content.ReadFromJsonAsync<IReadOnlyList<ProvinceRollUpResponse>>()
      ?? throw new InvalidOperationException("The roll-up response body was empty.");
  }

  private async Task<HttpResponseMessage> UpdateReportsToAsync(Guid reportsToAdminId)
  {
    return await SendAsync(
      HttpMethod.Put,
      $"/api/provinces/{HierarchyEndpointTestData.NorthProvinceId}/area-manager/reports-to",
      Roles.CompanyAdmin,
      HierarchyEndpointTestData.CompanyAdminSubject,
      JsonContent.Create(new { reportsToAdminId }));
  }

  private async Task<HttpResponseMessage> SendAsync(
    HttpMethod method,
    string path,
    string role,
    string subject,
    HttpContent? content = null)
  {
    var token = TestTokenFactory.CreateToken(
      _factory.Issuer,
      _factory.Audience,
      role,
      HierarchyEndpointTestData.CompanyId.ToString(),
      subject);

    var request = new HttpRequestMessage(method, path)
    {
      Content = content
    };
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    return await _factory.CreateClient().SendAsync(request);
  }

  private Guid SeedCompanyAdmin(string identitySub)
  {
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    var staffProfileId = Guid.NewGuid();

    db.StaffProfiles.Add(new StaffProfile
    {
      StaffProfileId = staffProfileId,
      CompanyId = HierarchyEndpointTestData.CompanyId,
      IdentitySub = identitySub,
      DisplayName = identitySub,
      Role = Roles.CompanyAdmin,
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    });
    db.SaveChanges();

    return staffProfileId;
  }

  private static IReadOnlyList<Guid> ProvinceIds(HierarchyTreeResponse hierarchy) =>
    hierarchy.Provinces.Select(province => province.ProvinceId).Order().ToList();

  private static IReadOnlyList<Guid> AgencyIds(HierarchyTreeResponse hierarchy) =>
    hierarchy.Provinces
      .SelectMany(province => province.Agencies)
      .Select(agency => agency.AgencyId)
      .Order()
      .ToList();

  private static IReadOnlyList<Guid> TerritoryIds(HierarchyTreeResponse hierarchy) =>
    hierarchy.Provinces
      .SelectMany(province => province.Agencies.SelectMany(agency => agency.Territories))
      .Concat(hierarchy.Provinces.SelectMany(province => province.UnassignedTerritories))
      .Select(territory => territory.TerritoryId)
      .Order()
      .ToList();
}
