using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Tests;

/// <summary>
/// Boots the real API for integration tests, overriding two things:
/// JWT validation trusts a test signing key (not IS's JWKS), and the database
/// is an isolated in-memory SQLite instance seeded with two companies' data.
/// The rest of the pipeline — auth, policies, the tenant query filter — runs
/// exactly as in production.
/// </summary>
public class TestWebAppFactory : WebApplicationFactory<Program>
{
    /// <summary>The expected token issuer, from test configuration.</summary>
    public string Issuer { get; private set; } = string.Empty;

    /// <summary>The expected token audience, from test configuration.</summary>
    public string Audience { get; private set; } = string.Empty;

    // Kept open for the lifetime of the factory so the in-memory DB survives.
    private SqliteConnection? _connection;

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

            // --- Override JWT to trust the test signing key ---
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

            // --- Replace the real DbContext with an isolated in-memory SQLite one ---
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<CoreDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<CoreDbContext>(options =>
                options.UseSqlite(_connection));

            // --- Create schema + seed two companies' data ---
            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            db.Database.EnsureCreated();
            db.DemoRecords.AddRange(
                new DemoRecord { CompanyId = "COMP-001", Name = "A-Alpha" },
                new DemoRecord { CompanyId = "COMP-001", Name = "A-Beta" },
                new DemoRecord { CompanyId = "COMP-002", Name = "B-Gamma" },
                new DemoRecord { CompanyId = "COMP-002", Name = "B-Delta" });
            db.SaveChanges();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection?.Dispose();
    }
}