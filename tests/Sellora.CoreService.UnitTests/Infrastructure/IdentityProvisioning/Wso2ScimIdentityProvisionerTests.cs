using System.Net;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sellora.CoreService.Application.IdentityProvisioning;
using Sellora.CoreService.Infrastructure.IdentityProvisioning;

namespace Sellora.CoreService.Tests;

/// <summary>
/// The SCIM2 conversation with WSO2 IS, against a stubbed server: what is
/// sent, how each failure is reported, and that a half-created login is
/// removed. Role IDs are cached per process, so each test uses its own role.
/// </summary>
public sealed class Wso2ScimIdentityProvisionerTests
{
  private const string UserId = "scim-user-1";
  private const string RoleId = "scim-role-1";

  private static readonly Guid CompanyId = Guid.NewGuid();

  [Fact]
  public async Task CreateUser_WithTemporaryPassword_CreatesUserAndAddsRole()
  {
    var role = UniqueRole();
    var server = ScimServer(role);

    var identity = await Provisioner(server).CreateUserAsync(
      new NewIdentityUser(" Rep@Example.COM ", "Nimal Perera", " 0771234567 ", role, CompanyId),
      CancellationToken.None);

    identity.IdentityId.Should().Be(UserId);
    identity.UserName.Should().Be("rep@example.com");
    identity.TemporaryPassword.Should().NotBeNullOrWhiteSpace();

    server.Requests.Select(request => $"{request.Method} {request.PathAndQuery}")
      .Should().Equal(
        "POST /scim2/Users",
        "GET /scim2/v2/Roles?count=200",
        $"PATCH /scim2/v2/Roles/{RoleId}");

    var user = JsonNode.Parse(server.Requests[0].Body!)!;
    user["userName"]!.GetValue<string>().Should().Be("rep@example.com");
    user["password"]!.GetValue<string>().Should().Be(identity.TemporaryPassword);
    user["name"]!["givenName"]!.GetValue<string>().Should().Be("Nimal");
    user["name"]!["familyName"]!.GetValue<string>().Should().Be("Perera");
    user["emails"]![0]!["value"]!.GetValue<string>().Should().Be("rep@example.com");
    user["phoneNumbers"]![0]!["value"]!.GetValue<string>().Should().Be("0771234567");
    user["urn:scim:schemas:extension:custom:User"]!["companyId"]!.GetValue<string>()
      .Should().Be(CompanyId.ToString());

    var patch = JsonNode.Parse(server.Requests[2].Body!)!;
    patch["Operations"]![0]!["value"]![0]!["value"]!.GetValue<string>().Should().Be(UserId);
  }

  [Fact]
  public async Task CreateUser_InAskPasswordMode_LetsIsEmailTheUser_AndReusesTheCachedRole()
  {
    var role = UniqueRole();
    var server = ScimServer(role);
    var provisioner = Provisioner(server, new IdentityProvisioningOptions { InvitationMode = "AskPassword" });

    var first = await provisioner.CreateUserAsync(
      new NewIdentityUser("a@example.com", "Single", null, role, CompanyId),
      CancellationToken.None);
    await provisioner.CreateUserAsync(
      new NewIdentityUser("b@example.com", "Other", null, role, CompanyId),
      CancellationToken.None);

    first.TemporaryPassword.Should().BeNull();

    var user = JsonNode.Parse(server.Requests[0].Body!)!;
    user["password"].Should().BeNull();
    user["phoneNumbers"].Should().BeNull();
    user["urn:scim:wso2:schema"]!["askPassword"]!.GetValue<bool>().Should().BeTrue();
    user["name"]!["familyName"]!.GetValue<string>().Should().Be("-");

    server.Requests.Count(request => request.Method == HttpMethod.Get)
      .Should().Be(1, "the role ID is looked up once and then cached");
  }

  [Fact]
  public async Task CreateUser_ExistingUserName_ThrowsAlreadyExists()
  {
    var server = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict));

    var act = () => Provisioner(server).CreateUserAsync(NewUser(UniqueRole()), CancellationToken.None);

    (await act.Should().ThrowAsync<IdentityUserAlreadyExistsException>())
      .Which.UserName.Should().Be("new.user@example.com");
  }

  [Fact]
  public async Task CreateUser_ServerError_ReportsProviderUnavailable()
  {
    var server = new StubHttpHandler(_ =>
      StubHttpHandler.Json(HttpStatusCode.InternalServerError, "{\"detail\":\"boom\"}"));

    var act = () => Provisioner(server).CreateUserAsync(NewUser(UniqueRole()), CancellationToken.None);

    (await act.Should().ThrowAsync<IdentityProvisioningUnavailableException>())
      .WithMessage("*could not create the user (HTTP 500)*");
  }

  [Fact]
  public async Task CreateUser_ResponseWithoutId_ReportsProviderUnavailable()
  {
    var server = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Created));

    var act = () => Provisioner(server).CreateUserAsync(NewUser(UniqueRole()), CancellationToken.None);

    (await act.Should().ThrowAsync<IdentityProvisioningUnavailableException>())
      .WithMessage("*returned no id*");
  }

  [Fact]
  public async Task CreateUser_UnknownRole_RemovesTheNewLoginAndFails()
  {
    var role = UniqueRole();
    var server = ScimServer(knownRole: "SomeOtherRole");

    var act = () => Provisioner(server).CreateUserAsync(NewUser(role), CancellationToken.None);

    (await act.Should().ThrowAsync<IdentityProvisioningUnavailableException>())
      .WithMessage($"*Role '{role}' does not exist*");
    server.Requests.Last().Should().Match<StubHttpHandler.RecordedRequest>(request =>
      request.Method == HttpMethod.Delete &&
      request.PathAndQuery == $"/scim2/Users/{UserId}");
  }

  [Fact]
  public async Task CreateUser_RoleAssignmentRejected_RemovesTheNewLoginAndFails()
  {
    var role = UniqueRole();
    var server = ScimServer(role, patchStatus: HttpStatusCode.Forbidden);

    var act = () => Provisioner(server).CreateUserAsync(NewUser(role), CancellationToken.None);

    (await act.Should().ThrowAsync<IdentityProvisioningUnavailableException>())
      .WithMessage($"*add the user to role {role}*");
    server.Requests.Last().Method.Should().Be(HttpMethod.Delete);
  }

  [Theory]
  [InlineData(typeof(HttpRequestException))]
  [InlineData(typeof(TaskCanceledException))]
  public async Task CreateUser_ServerUnreachable_ReportsProviderUnavailable(Type failure)
  {
    var server = new StubHttpHandler(_ => throw (Exception)Activator.CreateInstance(failure)!);

    var act = () => Provisioner(server).CreateUserAsync(NewUser(UniqueRole()), CancellationToken.None);

    (await act.Should().ThrowAsync<IdentityProvisioningUnavailableException>())
      .WithMessage("*could not be reached*")
      .WithInnerException(failure);
  }

  [Fact]
  public async Task CreateUser_ProvisioningFailureFromTheAuthHandler_IsNotRewrapped()
  {
    var server = new StubHttpHandler(_ =>
      throw new IdentityProvisioningUnavailableException("credentials refused"));

    var act = () => Provisioner(server).CreateUserAsync(NewUser(UniqueRole()), CancellationToken.None);

    (await act.Should().ThrowAsync<IdentityProvisioningUnavailableException>())
      .WithMessage("credentials refused");
  }

  [Theory]
  [InlineData(HttpStatusCode.NoContent)]
  [InlineData(HttpStatusCode.NotFound)]
  [InlineData(HttpStatusCode.InternalServerError)]
  public async Task DeleteUser_NeverThrowsForAnHttpOutcome(HttpStatusCode status)
  {
    var server = new StubHttpHandler(_ => new HttpResponseMessage(status));

    var act = () => Provisioner(server).DeleteUserAsync("id with space", CancellationToken.None);

    await act.Should().NotThrowAsync();
    server.Requests.Single().PathAndQuery.Should().Be("/scim2/Users/id%20with%20space");
  }

  [Fact]
  public async Task DeleteUser_SwallowsTransportFailures()
  {
    var server = new StubHttpHandler(_ => throw new HttpRequestException("down"));

    var act = () => Provisioner(server).DeleteUserAsync(UserId, CancellationToken.None);

    await act.Should().NotThrowAsync();
  }

  [Fact]
  public async Task DisabledProvisioner_FailsClearlyOnCreate_AndIgnoresDelete()
  {
    var provisioner = new DisabledIdentityProvisioner();

    var create = () => provisioner.CreateUserAsync(NewUser("SalesRep"), CancellationToken.None);

    (await create.Should().ThrowAsync<IdentityProvisioningUnavailableException>())
      .WithMessage("*not configured*");
    await provisioner.Awaiting(p => p.DeleteUserAsync(UserId, CancellationToken.None))
      .Should().NotThrowAsync();
  }

  private static string UniqueRole() => $"TestRole-{Guid.NewGuid():N}";

  private static NewIdentityUser NewUser(string role) =>
    new("new.user@example.com", "New User", null, role, CompanyId);

  private static StubHttpHandler ScimServer(
    string knownRole,
    HttpStatusCode patchStatus = HttpStatusCode.OK) =>
    new(request => (request.Method.Method, request.RequestUri!.AbsolutePath) switch
    {
      ("POST", "/scim2/Users") =>
        StubHttpHandler.Json(HttpStatusCode.Created, $"{{\"id\":\"{UserId}\"}}"),
      ("GET", "/scim2/v2/Roles") =>
        StubHttpHandler.Json(
          HttpStatusCode.OK,
          $"{{\"Resources\":[{{\"displayName\":\"Unrelated\",\"id\":\"x\"}},{{\"displayName\":\"{knownRole}\",\"id\":\"{RoleId}\"}}]}}"),
      ("PATCH", _) => new HttpResponseMessage(patchStatus),
      ("DELETE", _) => new HttpResponseMessage(HttpStatusCode.NoContent),
      _ => new HttpResponseMessage(HttpStatusCode.BadRequest)
    });

  private static Wso2ScimIdentityProvisioner Provisioner(
    HttpMessageHandler server,
    IdentityProvisioningOptions? options = null) =>
    new(
      new StubHttpClientFactory().Add(
        Wso2ScimIdentityProvisioner.HttpClientName,
        server,
        new Uri("https://is.test/")),
      Options.Create(options ?? new IdentityProvisioningOptions()),
      NullLogger<Wso2ScimIdentityProvisioner>.Instance);
}
