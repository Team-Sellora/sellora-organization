using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Sellora.CoreService.Api.Authorization;
using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Domain.Tenancy;
using Sellora.CoreService.Api.Tenancy;
using Sellora.CoreService.Infrastructure.Persistence;
using Sellora.CoreService.Api.Middleware;
using Sellora.CoreService.Application.Hierarchy;
using Sellora.CoreService.Infrastructure.Hierarchy;
using Sellora.CoreService.Api.Identity;
using Sellora.CoreService.Application.Identity;
using Serilog;
using Sellora.CoreService.Application.ProvinceAssignments;
using Sellora.CoreService.Infrastructure.ProvinceAssignments;
using Sellora.CoreService.Application.Provinces;
using Sellora.CoreService.Infrastructure.Provinces;
using Sellora.CoreService.Application.AreaManagers;
using Sellora.CoreService.Infrastructure.AreaManagers;
using Sellora.CoreService.Application.CompanyAdmins;
using Sellora.CoreService.Infrastructure.CompanyAdmins;
using Sellora.CoreService.Application.Shops;
using Sellora.CoreService.Infrastructure.Shops;
using Sellora.CoreService.Application.Agencies;
using Sellora.CoreService.Infrastructure.Agencies;
using Sellora.CoreService.Application.Territories;
using Sellora.CoreService.Infrastructure.Territories;
using Sellora.CoreService.Application.TerritoryAssignments;
using Sellora.CoreService.Infrastructure.TerritoryAssignments;
using Sellora.CoreService.Application.SalesRepAssignments;
using Sellora.CoreService.Infrastructure.SalesRepAssignments;
using Sellora.CoreService.Application.SalesRepAssignments;
using Sellora.CoreService.Infrastructure.SalesRepAssignments;
using Microsoft.Extensions.Caching.Memory;
using Sellora.CoreService.Application.Outbox;
using Sellora.CoreService.Infrastructure.Outbox;
using Sellora.CoreService.Api.Outbox;
using Sellora.CoreService.Infrastructure.Persistence.Seeding;

var builder = WebApplication.CreateBuilder(args);

// Structured logging via Serilog, written to the console.
builder.Host.UseSerilog((context, config) =>
    config
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console());

// Configuration
var jwt = builder.Configuration.GetSection("Jwt");
var authority = jwt["Authority"]!;
var metadataAddress = jwt["MetadataAddress"]!;
var issuer = jwt["Issuer"]!;
var audience = jwt["Audience"]!;
// JWT bearer authentication
// Validates every incoming token against Identity Server's JWKS:
// signature (via keys fetched from JWKS), issuer, audience, and lifetime.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.MetadataAddress = metadataAddress;
        options.Audience = audience;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        if (builder.Environment.IsDevelopment() || builder.Environment.IsStaging())
        {
            options.BackchannelHttpHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            };
        }
    });

builder.Services.AddAuthorization(options =>
{
    options.AddSelloraRolePolicies();
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddScoped<IOpenWorkChecker, StubOpenWorkChecker>();

builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
var connectionString = builder.Configuration.GetConnectionString("Default");
if (builder.Environment.IsProduction() &&
    (string.IsNullOrWhiteSpace(connectionString) ||
     connectionString.Contains("Host=localhost", StringComparison.OrdinalIgnoreCase)))
{
    throw new InvalidOperationException(
        "A production database connection string must be configured via ConnectionStrings__Default.");
}

builder.Services.AddDbContext<CoreDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.Configure<KafkaOptions>(
  builder.Configuration.GetSection(KafkaOptions.SectionName));

builder.Services.Configure<OutboxRelayOptions>(
  builder.Configuration.GetSection(OutboxRelayOptions.SectionName));

builder.Services.AddScoped<IOutboxWriter, EntityFrameworkOutboxWriter>();

builder.Services.AddScoped<IHierarchyEventFactory, HierarchyEventFactory>();

builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<OutboxRelayService>();
}

builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();

builder.Services.AddScoped<IHierarchyReadService, HierarchyReadService>();
builder.Services.AddScoped<IHierarchyRollUpService, HierarchyRollUpService>();

builder.Services.AddScoped<IProvinceAssignmentService, ProvinceAssignmentService>();

builder.Services.AddScoped<IProvinceReadService, ProvinceReadService>();

builder.Services.AddScoped<IHierarchyDeactivationService, HierarchyDeactivationService>();

builder.Services.AddScoped<IAreaManagerReadService, AreaManagerReadService>();

builder.Services.AddScoped<ICompanyAdminReadService, CompanyAdminReadService>();

builder.Services.AddScoped<IShopRegistrationService, ShopRegistrationService>();

builder.Services.AddScoped<IShopUpdateService, ShopUpdateService>();

builder.Services.AddScoped<IShopReadService, ShopReadService>();

builder.Services.AddScoped<IAgencyRegistrationService, AgencyRegistrationService>();
builder.Services.AddScoped<IAgencyReadService, AgencyReadService>();

builder.Services.AddScoped<ITerritoryRegistrationService, TerritoryRegistrationService>();
builder.Services.AddScoped<ITerritoryReadService, TerritoryReadService>();

builder.Services.AddScoped<ITerritoryAgencyAssignmentService, TerritoryAgencyAssignmentService>();

builder.Services.AddScoped<ISalesRepTerritoryAssignmentService, SalesRepTerritoryAssignmentService>();

builder.Services.AddScoped<IRepShopRelationshipVerifier, RepShopRelationshipVerifier>();

builder.Services.AddMemoryCache();

builder.Services.AddScoped<IRepTerritoryAssignmentCache, MemoryRepTerritoryAssignmentCache>();

builder.Services.AddScoped<ISalesRepAssignmentReadService, SalesRepAssignmentReadService>();

builder.Services.AddScoped<ICorrelationIdAccessor, HttpCorrelationIdAccessor>();

builder.Host.UseSerilog((context, config) =>
    config
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} " +
                "{Properties:j}{NewLine}{Exception}"));

builder.Services.AddHealthChecks();

builder.Services.AddProblemDetails();

builder.Services.AddControllers();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins);
        }
        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();

app.UseMiddleware<CorrelationIdMiddleware>();

// Apply version-controlled migrations when the service starts.
if (!app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    await db.Database.MigrateAsync();

    // Seed predictable demo data only in the Azure staging slot.
    // The seeder checks for SELLORA-DEMO first, so repeated restarts
    // do not duplicate rows.
    if (app.Environment.IsStaging())
    {
        await DevelopmentOrganizationSeeder.SeedAsync(db);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// CORS must come BEFORE authentication so the browser's preflight OPTIONS
// (which has no Authorization header) isn't rejected by the JWT middleware.
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/whoami", (HttpContext ctx) =>
{
    var name = ctx.User.Identity?.Name ?? "(no name claim)";
    var claims = ctx.User.Claims.Select(c => new { c.Type, c.Value });
    return Results.Ok(new { name, claims });
})
.RequireAuthorization();

app.MapHealthChecks("/health");

if (!app.Environment.IsProduction())
{
    app.MapGet("/nonexistent", () => { throw new InvalidOperationException("Test failure"); });
}

app.Run();

// Exposes the implicit Program class to the test project (WebApplicationFactory).
public partial class Program { }
