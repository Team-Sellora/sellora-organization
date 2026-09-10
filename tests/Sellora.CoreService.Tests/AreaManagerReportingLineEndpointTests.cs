using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Tests;

public sealed class AreaManagerReportingLineEndpointTests
  : IClassFixture<TestWebAppFactory>
{
  private readonly TestWebAppFactory _factory;

  public AreaManagerReportingLineEndpointTests(TestWebAppFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task Put_ValidCompanyAdmin_UpdatesActiveAssignment()
  {
    var newAdminId = SeedStaff(Roles.CompanyAdmin, "replacement-hq-admin");

    var response = await PutAsync(
      HierarchyEndpointTestData.NorthProvinceId,
      newAdminId,
      Roles.CompanyAdmin);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var assignment = await db.ProvinceManagerAssignments
      .IgnoreQueryFilters()
      .SingleAsync(item =>
        item.ProvinceId == HierarchyEndpointTestData.NorthProvinceId &&
        item.EndsAt == null);

    Assert.Equal(newAdminId, assignment.ReportsToAdminId);
    Assert.Equal(HierarchyEndpointTestData.NorthProvinceId, assignment.ProvinceId);
    Assert.Null(assignment.EndsAt);
  }

  [Fact]
  public async Task Put_NonCompanyAdminTarget_ReturnsBadRequestAndChangesNothing()
  {
    var invalidTargetId = SeedStaff(Roles.AgencyOperator, "not-an-hq-admin");
    var reportsToBefore = await GetReportsToAdminIdAsync();

    var response = await PutAsync(
      HierarchyEndpointTestData.NorthProvinceId,
      invalidTargetId,
      Roles.CompanyAdmin);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Contains(Roles.CompanyAdmin, await response.Content.ReadAsStringAsync());
    Assert.Equal(reportsToBefore, await GetReportsToAdminIdAsync());
  }

  [Fact]
  public async Task Put_CrossCompanyAdmin_ReturnsBadRequestAndChangesNothing()
  {
    var otherCompanyAdminId = SeedStaff(
      Roles.CompanyAdmin,
      "other-company-admin",
      HierarchyEndpointTestData.OtherCompanyId);
    var reportsToBefore = await GetReportsToAdminIdAsync();

    var response = await PutAsync(
      HierarchyEndpointTestData.NorthProvinceId,
      otherCompanyAdminId,
      Roles.CompanyAdmin);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal(reportsToBefore, await GetReportsToAdminIdAsync());
  }

  [Fact]
  public async Task Put_NonAdminCaller_ReturnsForbidden()
  {
    var response = await PutAsync(
      HierarchyEndpointTestData.NorthProvinceId,
      HierarchyEndpointTestData.CompanyAdminId,
      Roles.AreaManager);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  private async Task<HttpResponseMessage> PutAsync(
    Guid provinceId,
    Guid reportsToAdminId,
    string callerRole)
  {
    var token = TestTokenFactory.CreateToken(
      _factory.Issuer,
      _factory.Audience,
      callerRole,
      HierarchyEndpointTestData.CompanyId.ToString(),
      HierarchyEndpointTestData.CompanyAdminSubject);

    var request = new HttpRequestMessage(
      HttpMethod.Put,
      $"/api/provinces/{provinceId}/area-manager/reports-to");

    request.Headers.Authorization =
      new AuthenticationHeaderValue("Bearer", token);
    request.Content = JsonContent.Create(new { reportsToAdminId });

    return await _factory.CreateClient().SendAsync(request);
  }

  private Guid SeedStaff(
    string role,
    string identitySub,
    Guid? companyId = null)
  {
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    var staffProfileId = Guid.NewGuid();

    db.StaffProfiles.Add(new StaffProfile
    {
      StaffProfileId = staffProfileId,
      CompanyId = companyId ?? HierarchyEndpointTestData.CompanyId,
      IdentitySub = $"{identitySub}-{Guid.NewGuid():N}",
      Role = role,
      DisplayName = identitySub,
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    });
    db.SaveChanges();

    return staffProfileId;
  }

  private async Task<Guid?> GetReportsToAdminIdAsync()
  {
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    return await db.ProvinceManagerAssignments
      .IgnoreQueryFilters()
      .Where(item =>
        item.ProvinceId == HierarchyEndpointTestData.NorthProvinceId &&
        item.EndsAt == null)
      .Select(item => item.ReportsToAdminId)
      .SingleAsync();
  }
}
