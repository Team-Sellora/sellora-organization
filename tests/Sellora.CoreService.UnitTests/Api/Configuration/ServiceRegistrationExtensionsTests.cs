using System.Net.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sellora.CoreService.Api.Configuration;
using Sellora.CoreService.Application.IdentityProvisioning;
using Sellora.CoreService.Infrastructure.IdentityProvisioning;

namespace Sellora.CoreService.UnitTests.Api.Configuration;

/// <summary>
/// Startup registrations: JWT validation settings per environment and the
/// SCIM provisioning pipeline when (and only when) it is configured.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ServiceRegistrationExtensionsTests
{
  [Theory]
  [InlineData("Development")]
  [InlineData("Staging")]
  public void Jwt_InDevelopmentAndStaging_AcceptsTheSelfSignedIdentityServer(string environment)
  {
    var options = JwtOptionsFor(environment, Configuration(("Jwt:Audience:0", "aud-1"), ("Jwt:Audience:1", "aud-2")));

    var handler = options.BackchannelHttpHandler.Should().BeOfType<HttpClientHandler>().Subject;
    var validate = handler.ServerCertificateCustomValidationCallback!;

    validate(new HttpRequestMessage(), null, null, SslPolicyErrors.None).Should().BeTrue();
    validate(new HttpRequestMessage(), null, null, SslPolicyErrors.RemoteCertificateChainErrors)
      .Should().BeTrue();

    options.TokenValidationParameters.ValidAudiences.Should().Equal("aud-1", "aud-2");
  }

  [Fact]
  public void Jwt_InProduction_UsesDefaultCertificateValidation()
  {
    var options = JwtOptionsFor("Production", Configuration(("Jwt:Audience", "single-audience")));

    options.BackchannelHttpHandler.Should().BeNull();
    options.Authority.Should().Be("https://is.test/oauth2/token");
    options.MapInboundClaims.Should().BeFalse();
    options.TokenValidationParameters.ValidIssuer.Should().Be("https://is.test/oauth2/token");
    options.TokenValidationParameters.ValidAudiences.Should().Equal("single-audience");
    options.TokenValidationParameters.RoleClaimType.Should().Be("roles");
    options.TokenValidationParameters.ClockSkew.Should().Be(TimeSpan.FromSeconds(30));
  }

  [Fact]
  public void Provisioning_NotConfigured_RegistersTheDisabledProvisioner()
  {
    using var provider = ProvisioningServices(Configuration()).BuildServiceProvider();

    provider.GetRequiredService<IIdentityProvisioner>()
      .Should().BeOfType<DisabledIdentityProvisioner>();
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void Provisioning_Configured_RegistersScimClients(bool allowUntrustedCertificate)
  {
    var configuration = Configuration(
      ("IdentityProvisioning:BaseUrl", "https://is.test/"),
      ("IdentityProvisioning:ClientId", "client"),
      ("IdentityProvisioning:ClientSecret", "secret"),
      ("IdentityProvisioning:TimeoutSeconds", "7"),
      ("IdentityProvisioning:AllowUntrustedCertificate", allowUntrustedCertificate.ToString()));

    using var provider = ProvisioningServices(configuration).BuildServiceProvider();
    using var scope = provider.CreateScope();

    scope.ServiceProvider.GetRequiredService<IIdentityProvisioner>()
      .Should().BeOfType<Wso2ScimIdentityProvisioner>();

    var clients = provider.GetRequiredService<IHttpClientFactory>();

    var scimClient = clients.CreateClient(Wso2ScimIdentityProvisioner.HttpClientName);
    scimClient.BaseAddress.Should().Be(new Uri("https://is.test/"));
    scimClient.Timeout.Should().Be(TimeSpan.FromSeconds(7));

    clients.CreateClient(ScimAuthenticationHandler.TokenClientName)
      .Timeout.Should().Be(TimeSpan.FromSeconds(7));
  }

  private static JwtBearerOptions JwtOptionsFor(string environment, IConfiguration configuration)
  {
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSelloraJwtAuthentication(configuration, new TestHostEnvironment(environment));

    using var provider = services.BuildServiceProvider();

    return provider
      .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
      .Get(JwtBearerDefaults.AuthenticationScheme);
  }

  private static IServiceCollection ProvisioningServices(IConfiguration configuration)
  {
    var services = new ServiceCollection();
    services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
    services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
    services.Configure<IdentityProvisioningOptions>(
      configuration.GetSection(IdentityProvisioningOptions.SectionName));
    services.AddSelloraIdentityProvisioning(configuration);
    return services;
  }

  private static IConfiguration Configuration(params (string Key, string Value)[] values)
  {
    var settings = new Dictionary<string, string?>
    {
      ["Jwt:Authority"] = "https://is.test/oauth2/token",
      ["Jwt:MetadataAddress"] = "https://is.test/oauth2/token/.well-known/openid-configuration",
      ["Jwt:Issuer"] = "https://is.test/oauth2/token"
    };

    foreach (var (key, value) in values)
    {
      settings[key] = value;
    }

    return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
  }

  private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
  {
    public string EnvironmentName { get; set; } = environmentName;
    public string ApplicationName { get; set; } = "Sellora.CoreService.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
  }
}
