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

        // DEV ONLY: Identity Server uses a self-signed cert, so the metadata/JWKS
        // fetch over HTTPS would fail cert validation. Bypass it in Development.
        if (builder.Environment.IsDevelopment())
        {
            options.RequireHttpsMetadata = false;
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

builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();

builder.Services.AddScoped<IHierarchyReadService, HierarchyReadService>();

builder.Services.AddScoped<IProvinceAssignmentService, ProvinceAssignmentService>();

builder.Services.AddScoped<IProvinceReadService, ProvinceReadService>();

builder.Services.AddScoped<IHierarchyDeactivationService, HierarchyDeactivationService>();

builder.Services.AddScoped<IAreaManagerReadService, AreaManagerReadService>();

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
