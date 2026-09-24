using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sellora.CoreService.Application.IdentityProvisioning;
using Sellora.CoreService.Application.Me;
using Sellora.CoreService.Application.Staff;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Tests;

/// <summary>
/// POST /api/staff and owner provisioning on POST /api/shops: one action
/// creates the login and the profile, linked by the provider's ID.
/// </summary>
public sealed class StaffProvisioningEndpointTests : IClassFixture<ProvisioningWebAppFactory>
{
  private readonly ProvisioningWebAppFactory _factory;

  public StaffProvisioningEndpointTests(ProvisioningWebAppFactory factory)
  {
    _factory = factory;
    _factory.CreateClient();
    _factory.Provisioner.Failure = null;
  }

  [Fact]
  public async Task Company_admin_adds_a_sales_rep_in_one_call()
  {
    var email = UniqueEmail();

    var response = await PostStaffAsync(Roles.CompanyAdmin, HierarchyEndpointTestData.CompanyAdminSubject,
      new { role = "SalesRep", displayName = "Nimal Perera", email, phone = "0771234567" });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

    var created = (await response.Content.ReadFromJsonAsync<CreatedStaffResponse>())!;
    Assert.Equal("SalesRep", created.Role);
    Assert.NotNull(created.TemporaryPassword);

    // The provider got the admin's company, never one from the request.
    var sent = Assert.Single(_factory.Provisioner.Created, user => user.Email == email);
    Assert.Equal(HierarchyEndpointTestData.CompanyId, sent.CompanyId);
    Assert.Equal("SalesRep", sent.Role);

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    var profile = await db.StaffProfiles.IgnoreQueryFilters().SingleAsync(p => p.StaffProfileId == created.StaffProfileId);
    Assert.Equal(created.IdentitySub, profile.IdentitySub);
    Assert.Equal(email, profile.Email);
  }

  [Fact]
  public async Task The_new_login_resolves_to_its_profile_immediately()
  {
    var response = await PostStaffAsync(Roles.CompanyAdmin, HierarchyEndpointTestData.CompanyAdminSubject,
      new { role = "SalesRep", displayName = "Kamal Silva", email = UniqueEmail() });
    var created = (await response.Content.ReadFromJsonAsync<CreatedStaffResponse>())!;

    var token = TestTokenFactory.CreateToken(
      _factory.Issuer, _factory.Audience, role: Roles.SalesRep,
      companyId: HierarchyEndpointTestData.CompanyId.ToString(), sub: created.IdentitySub);
    var request = new HttpRequestMessage(HttpMethod.Get, "/api/me/scope");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var scopeResponse = await _factory.CreateClient().SendAsync(request);

    Assert.Equal(HttpStatusCode.OK, scopeResponse.StatusCode);
    var scope = (await scopeResponse.Content.ReadFromJsonAsync<CallerScopeResponse>())!;
    Assert.Equal(created.StaffProfileId, scope.SalesRepId);
    // No territory yet: assigned later on the Sales Reps screen.
    Assert.Null(scope.TerritoryId);
  }

  [Fact]
  public async Task Agency_operator_can_add_sales_reps_only()
  {
    var rep = await PostStaffAsync(Roles.AgencyOperator, HierarchyEndpointTestData.AgencyOperatorSubject,
      new { role = "SalesRep", displayName = "Rep", email = UniqueEmail() });
    var manager = await PostStaffAsync(Roles.AgencyOperator, HierarchyEndpointTestData.AgencyOperatorSubject,
      new { role = "AreaManager", displayName = "Manager", email = UniqueEmail() });

    Assert.Equal(HttpStatusCode.Created, rep.StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, manager.StatusCode);
  }

  [Fact]
  public async Task Sales_reps_cannot_add_staff()
  {
    var response = await PostStaffAsync(Roles.SalesRep, HierarchyEndpointTestData.SalesRepSubject,
      new { role = "SalesRep", displayName = "Rep", email = UniqueEmail() });

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Theory]
  [InlineData("not-an-email")]
  [InlineData("")]
  public async Task An_invalid_email_is_rejected_before_any_login_is_created(string email)
  {
    var before = _factory.Provisioner.Created.Count;

    var response = await PostStaffAsync(Roles.CompanyAdmin, HierarchyEndpointTestData.CompanyAdminSubject,
      new { role = "SalesRep", displayName = "Rep", email });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal(before, _factory.Provisioner.Created.Count);
  }

  [Fact]
  public async Task A_duplicate_email_is_409()
  {
    var email = UniqueEmail();
    await PostStaffAsync(Roles.CompanyAdmin, HierarchyEndpointTestData.CompanyAdminSubject,
      new { role = "SalesRep", displayName = "First", email });

    var second = await PostStaffAsync(Roles.CompanyAdmin, HierarchyEndpointTestData.CompanyAdminSubject,
      new { role = "SalesRep", displayName = "Second", email = email.ToUpperInvariant() });

    Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
  }

  [Fact]
  public async Task A_login_that_already_exists_in_IS_is_409()
  {
    _factory.Provisioner.Failure = new IdentityUserAlreadyExistsException("x@test.local");

    var response = await PostStaffAsync(Roles.CompanyAdmin, HierarchyEndpointTestData.CompanyAdminSubject,
      new { role = "SalesRep", displayName = "Rep", email = UniqueEmail() });

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
  }

  [Fact]
  public async Task Identity_provider_down_is_503_and_saves_nothing()
  {
    var email = UniqueEmail();
    _factory.Provisioner.Failure = new IdentityProvisioningUnavailableException("IS is down");

    var response = await PostStaffAsync(Roles.CompanyAdmin, HierarchyEndpointTestData.CompanyAdminSubject,
      new { role = "SalesRep", displayName = "Rep", email });

    Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    Assert.False(await db.StaffProfiles.IgnoreQueryFilters().AnyAsync(p => p.Email == email));
  }

  [Fact]
  public async Task Registering_a_shop_with_an_owner_email_creates_the_owner_login()
  {
    var email = UniqueEmail();
    var token = TestTokenFactory.CreateToken(
      _factory.Issuer, _factory.Audience, role: Roles.AgencyOperator,
      companyId: HierarchyEndpointTestData.CompanyId.ToString(),
      sub: HierarchyEndpointTestData.AgencyOperatorSubject);

    var request = new HttpRequestMessage(HttpMethod.Post, "/api/shops")
    {
      Content = JsonContent.Create(new
      {
        territoryId = HierarchyEndpointTestData.NorthTerritoryId,
        name = $"Owner Email Shop {Guid.NewGuid():N}",
        ownerName = "Sunil Fernando",
        ownerEmail = email,
        address = "1 Main Street",
        latitude = 6.927079m,
        longitude = 79.861244m,
        creditLimit = 10000m
      })
    };
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var response = await _factory.CreateClient().SendAsync(request);

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    var sent = Assert.Single(_factory.Provisioner.Created, user => user.Email == email);
    Assert.Equal(Roles.ShopOwner, sent.Role);
    Assert.Equal("Sunil Fernando", sent.DisplayName);

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    var shop = await db.Shops.IgnoreQueryFilters().SingleAsync(s => s.OwnerEmail == email);
    Assert.StartsWith("is-", shop.OwnerIdentitySub);
  }

  private static string UniqueEmail() => $"staff-{Guid.NewGuid():N}@test.local";

  private Task<HttpResponseMessage> PostStaffAsync(string role, string subject, object body)
  {
    var token = TestTokenFactory.CreateToken(
      _factory.Issuer, _factory.Audience, role: role,
      companyId: HierarchyEndpointTestData.CompanyId.ToString(), sub: subject);

    var request = new HttpRequestMessage(HttpMethod.Post, "/api/staff") { Content = JsonContent.Create(body) };
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    return _factory.CreateClient().SendAsync(request);
  }
}
