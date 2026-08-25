using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;
using Xunit;

namespace Sellora.CoreService.Tests;

/// <summary>
/// Verifies PUT /api/provinces/{id}/area-manager against the three CSP-62 ACs
/// plus the CompanyAdmin policy guard.
/// </summary>
public sealed class AssignAreaManagerEndpointTests
  : IClassFixture<TestWebAppFactory>
{
  private readonly TestWebAppFactory _factory;

  public AssignAreaManagerEndpointTests(TestWebAppFactory factory)
  {
    _factory = factory;
    _factory.CreateClient();
  }

  // AC1 — Assign to a province with no active manager
  [Fact]
  public async Task Put_ValidAreaManager_ReturnsOkAndCreatesAssignment()
  {
    var provinceId = SeedNewProvince("AC1-P");
    var managerId = SeedNewStaff(Roles.AreaManager, "ac1-manager");

    var response = await PutAsync(
      provinceId,
      managerId,
      callerRole: Roles.CompanyAdmin);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    var assignment = await db.ProvinceManagerAssignments
      .IgnoreQueryFilters()
      .SingleAsync(a =>
        a.ProvinceId == provinceId && a.EndsAt == null);

    Assert.Equal(managerId, assignment.AreaManagerId);
    Assert.Equal(HierarchyEndpointTestData.CompanyId, assignment.CompanyId);
    Assert.Equal("test-admin", assignment.CreatedBy);
  }

  // AC3 — Wrong role rejected with a message naming the actual role
  [Fact]
  public async Task Put_AgencyOperator_ReturnsBadRequestNamingRole()
  {
    var provinceId = SeedNewProvince("AC3-P");
    var operatorId = SeedNewStaff(Roles.AgencyOperator, "ac3-operator");

    var response = await PutAsync(
      provinceId,
      operatorId,
      callerRole: Roles.CompanyAdmin);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

    var body = await response.Content.ReadAsStringAsync();
    Assert.Contains(Roles.AgencyOperator, body);
    Assert.Contains(Roles.AreaManager, body);

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    var count = await db.ProvinceManagerAssignments
      .IgnoreQueryFilters()
      .CountAsync(a => a.ProvinceId == provinceId);
    Assert.Equal(0, count);
  }

  // Tenant validation — target user in another company is invisible
  [Fact]
  public async Task Put_TargetFromAnotherCompany_ReturnsBadRequest()
  {
    var provinceId = SeedNewProvince("TENANT-P");
    var crossCompanyManagerId = SeedNewStaff(
      Roles.AreaManager,
      "cross-tenant-manager",
      companyId: HierarchyEndpointTestData.OtherCompanyId);

    var response = await PutAsync(
      provinceId,
      crossCompanyManagerId,
      callerRole: Roles.CompanyAdmin);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    var count = await db.ProvinceManagerAssignments
      .IgnoreQueryFilters()
      .CountAsync(a => a.ProvinceId == provinceId);
    Assert.Equal(0, count);
  }

  // Endpoint policy — a non-admin caller is refused before validation
  [Fact]
  public async Task Put_NonAdminCaller_ReturnsForbidden()
  {
    var response = await PutAsync(
      Guid.NewGuid(),
      Guid.NewGuid(),
      callerRole: Roles.AreaManager);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // Endpoint policy — no token at all
  [Fact]
  public async Task Put_NoToken_ReturnsUnauthorized()
  {
    var client = _factory.CreateClient();
    var response = await client.PutAsJsonAsync(
      $"/api/provinces/{Guid.NewGuid()}/area-manager",
      new { areaManagerId = Guid.NewGuid() });

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  // ---------- helpers ----------

  private async Task<HttpResponseMessage> PutAsync(
    Guid provinceId,
    Guid areaManagerId,
    string callerRole)
  {
    var token = TestTokenFactory.CreateToken(
      _factory.Issuer,
      _factory.Audience,
      role: callerRole,
      companyId: HierarchyEndpointTestData.CompanyId.ToString(),
      sub: "test-admin");

    var client = _factory.CreateClient();
    var request = new HttpRequestMessage(
      HttpMethod.Put,
      $"/api/provinces/{provinceId}/area-manager");
    request.Headers.Authorization =
      new AuthenticationHeaderValue("Bearer", token);
    request.Content = JsonContent.Create(new { areaManagerId });

    return await client.SendAsync(request);
  }

  private Guid SeedNewProvince(string code)
  {
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var provinceId = Guid.NewGuid();
    db.Provinces.Add(new Province
    {
      ProvinceId = provinceId,
      CompanyId = HierarchyEndpointTestData.CompanyId,
      Code = $"{code}-{Guid.NewGuid():N}"[..8],
      Name = $"Test Province {provinceId:N}"[..20],
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    });
    db.SaveChanges();
    return provinceId;
  }

  private Guid SeedNewStaff(
    string role,
    string subject,
    Guid? companyId = null)
  {
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var staffProfileId = Guid.NewGuid();
    db.StaffProfiles.Add(new StaffProfile
    {
      StaffProfileId = staffProfileId,
      CompanyId = companyId ?? HierarchyEndpointTestData.CompanyId,
      IdentitySub = $"{subject}-{Guid.NewGuid():N}",
      Role = role,
      DisplayName = subject,
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    });
    db.SaveChanges();
    return staffProfileId;
  }
}