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

  // Reassignment ends the prior record and leaves exactly one active
  [Fact]
  public async Task Put_ReassigningToDifferentManager_EndsPriorAndLeavesOneActive()
  {
    var provinceId = SeedNewProvince("REASSIGN");
    var oldManagerId = SeedNewStaff(Roles.AreaManager, "old-mgr");
    var newManagerId = SeedNewStaff(Roles.AreaManager, "new-mgr");

    var first = await PutAsync(provinceId, oldManagerId, Roles.CompanyAdmin);
    Assert.Equal(HttpStatusCode.OK, first.StatusCode);

    var second = await PutAsync(provinceId, newManagerId, Roles.CompanyAdmin);
    Assert.Equal(HttpStatusCode.OK, second.StatusCode);

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var history = await db.ProvinceManagerAssignments
      .IgnoreQueryFilters()
      .Where(a => a.ProvinceId == provinceId)
      .OrderBy(a => a.StartsAt)
      .ToListAsync();

    // Both assignments preserved — history is append-only.
    Assert.Equal(2, history.Count);

    // The old assignment has been ended.
    Assert.Equal(oldManagerId, history[0].AreaManagerId);
    Assert.NotNull(history[0].EndsAt);

    // The new assignment is active.
    Assert.Equal(newManagerId, history[1].AreaManagerId);
    Assert.Null(history[1].EndsAt);

    // Exactly one active row remains — the schema's partial unique index
    // would have thrown otherwise, but assert it here for clarity.
    var activeCount = history.Count(a => a.EndsAt == null);
    Assert.Equal(1, activeCount);
  }

  // 409 — Target already manages another province
  [Fact]
  public async Task Put_TargetAlreadyManagesAnotherProvince_Returns409()
  {
    var provinceA = SeedNewProvince("PA");
    var provinceB = SeedNewProvince("PB");
    var manager = SeedNewStaff(Roles.AreaManager, "cross-province-mgr");

    var assignA = await PutAsync(provinceA, manager, Roles.CompanyAdmin);
    Assert.Equal(HttpStatusCode.OK, assignA.StatusCode);

    var assignB = await PutAsync(provinceB, manager, Roles.CompanyAdmin);
    Assert.Equal(HttpStatusCode.Conflict, assignB.StatusCode);

    // The error names the other province so the admin sees where the user
    // is already assigned, not just that it failed.
    var body = await assignB.Content.ReadAsStringAsync();
    Assert.Contains(provinceA.ToString(), body);

    // No assignment was written for province B.
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    var countB = await db.ProvinceManagerAssignments
      .IgnoreQueryFilters()
      .CountAsync(a => a.ProvinceId == provinceB);
    Assert.Equal(0, countB);

    // Province A's active assignment is untouched.
    var stillActiveOnA = await db.ProvinceManagerAssignments
      .IgnoreQueryFilters()
      .SingleAsync(a => a.ProvinceId == provinceA && a.EndsAt == null);
    Assert.Equal(manager, stillActiveOnA.AreaManagerId);
  }
}