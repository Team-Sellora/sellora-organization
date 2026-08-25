using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Sellora.CoreService.Tests;

/// <summary>
/// Boots the real API but overrides JWT validation to trust a test signing key
/// instead of fetching Identity Server's JWKS. Issuer and audience are read from
/// <c>appsettings.Testing.json</c> so tests share one source of truth with no
/// hardcoded URLs. The full auth pipeline (issuer, audience, lifetime, signature,
/// role policies) still runs; only the trusted key is swapped.
/// </summary>
public class TestWebAppFactory : WebApplicationFactory<Program>
{
    /// <summary>The expected token issuer, from test configuration.</summary>
    public string Issuer { get; private set; } = string.Empty;

    /// <summary>The expected token audience, from test configuration.</summary>
    public string Audience { get; private set; } = string.Empty;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Testing.json"), optional: false);
        });

        builder.ConfigureTestServices(services =>
        {
            var sp = services.BuildServiceProvider();
            var configuration = sp.GetRequiredService<IConfiguration>();
            Issuer = configuration["Jwt:Issuer"]!;
            Audience = configuration["Jwt:Audience"]!;

            services.Configure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.Authority = null;
                    options.MetadataAddress = null;
                    options.RequireHttpsMetadata = false;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = Issuer,
                        ValidateAudience = true,
                        ValidAudience = Audience,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = TestSigningKey.SecurityKey,
                    };
                });
        });
    }
}