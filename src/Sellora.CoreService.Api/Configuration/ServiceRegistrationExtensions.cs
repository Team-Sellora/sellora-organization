using System.Net.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Sellora.CoreService.Application.IdentityProvisioning;
using Sellora.CoreService.Infrastructure.IdentityProvisioning;

namespace Sellora.CoreService.Api.Configuration;

/// <summary>
/// Startup registrations that carry their own branching, kept out of
/// Program.cs so the composition root stays a flat list of registrations.
/// </summary>
public static class ServiceRegistrationExtensions
{
  /// <summary>
  /// JWT bearer authentication. Validates every incoming token against
  /// Identity Server's JWKS: signature (via keys fetched from JWKS),
  /// issuer, audience, and lifetime.
  /// </summary>
  public static IServiceCollection AddSelloraJwtAuthentication(
    this IServiceCollection services,
    IConfiguration configuration,
    IHostEnvironment environment)
  {
    var jwt = configuration.GetSection("Jwt");
    var authority = jwt["Authority"]!;
    var metadataAddress = jwt["MetadataAddress"]!;
    var issuer = jwt["Issuer"]!;
    var audiences = jwt.GetSection("Audience").Get<string[]>()
        ?? new[] { jwt["Audience"]! };

    // Development and staging talk to an Identity Server with a
    // self-signed certificate, so untrusted certificates are accepted there.
    var acceptUntrustedBackchannelCertificate =
      environment.IsDevelopment() || environment.IsStaging();

    services
      .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddJwtBearer(options =>
      {
        options.Authority = authority;
        options.MetadataAddress = metadataAddress;
        // WSO2 IS emits claims under their raw names ("roles", "companyId",
        // "sub"). Keep them unmapped so RolePolicies and the tenant/current-
        // user contexts can read "roles" directly instead of ClaimTypes.Role.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
          ValidateIssuer = true,
          ValidIssuer = issuer,
          ValidateAudience = true,
          ValidAudiences = audiences,
          ValidateLifetime = true,
          ValidateIssuerSigningKey = true,
          ClockSkew = TimeSpan.FromSeconds(30),
          RoleClaimType = "roles",
        };

        if (acceptUntrustedBackchannelCertificate)
        {
          options.BackchannelHttpHandler = new HttpClientHandler
          {
            ServerCertificateCustomValidationCallback =
              (_, _, _, sslPolicyErrors) =>
                sslPolicyErrors == SslPolicyErrors.None ||
                acceptUntrustedBackchannelCertificate,
          };
        }
      });

    return services;
  }

  /// <summary>
  /// Users are created from Sellora (POST /api/staff, shop registration) and
  /// provisioned into WSO2 IS over SCIM2. Without settings, provisioning fails
  /// clearly with 503 instead of half-working.
  /// </summary>
  public static IServiceCollection AddSelloraIdentityProvisioning(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    var identityProvisioning = configuration
      .GetSection(IdentityProvisioningOptions.SectionName)
      .Get<IdentityProvisioningOptions>() ?? new IdentityProvisioningOptions();

    if (!identityProvisioning.IsConfigured)
    {
      services.AddSingleton<IIdentityProvisioner, DisabledIdentityProvisioner>();
      return services;
    }

    HttpMessageHandler ProvisioningHandler() => new HttpClientHandler
    {
      // Only for a self-signed IS on staging; off unless explicitly set.
      ServerCertificateCustomValidationCallback = identityProvisioning.AllowUntrustedCertificate
        ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        : null
    };

    services.AddHttpClient(ScimAuthenticationHandler.TokenClientName, client =>
        client.Timeout = TimeSpan.FromSeconds(identityProvisioning.TimeoutSeconds))
      .ConfigurePrimaryHttpMessageHandler(ProvisioningHandler);

    services.AddSingleton<ScimTokenCache>();
    services.AddTransient<ScimAuthenticationHandler>();
    services.AddHttpClient(Wso2ScimIdentityProvisioner.HttpClientName, client =>
      {
        client.BaseAddress = new Uri(identityProvisioning.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(identityProvisioning.TimeoutSeconds);
      })
      .ConfigurePrimaryHttpMessageHandler(ProvisioningHandler)
      .AddHttpMessageHandler<ScimAuthenticationHandler>();

    services.AddScoped<IIdentityProvisioner, Wso2ScimIdentityProvisioner>();

    return services;
  }
}
