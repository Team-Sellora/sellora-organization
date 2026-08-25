using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Sellora.CoreService.Api.Authorization;
using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Domain.Tenancy;
using Sellora.CoreService.Api.Tenancy;
using Sellora.CoreService.Infrastructure.Persistence;

using Serilog;

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
        // In production this must be removed and a trusted cert used.
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
builder.Services.AddSwaggerGen();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddDbContext<CoreDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=sellora-core.db"));

var app = builder.Build();
app.UseSerilogRequestLogging();
// DEV ONLY: create the SQLite database and seed two companies' demo data so the
// tenant filter can be demonstrated. Real services use migrations (see CSP later).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    db.Database.EnsureCreated();

    if (!db.DemoRecords.IgnoreQueryFilters().Any())
    {
        db.DemoRecords.AddRange(
            new() { CompanyId = "COMP-001", Name = "Alpha record (Company 1)" },
            new() { CompanyId = "COMP-001", Name = "Beta record (Company 1)" },
            new() { CompanyId = "COMP-002", Name = "Gamma record (Company 2)" },
            new() { CompanyId = "COMP-002", Name = "Delta record (Company 2)" }
        );
        db.SaveChanges();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Order matters: authentication before authorization.
app.UseAuthentication();
app.UseAuthorization();

// Temporary probe endpoint to prove JWT validation works.
// Returns 401 without a valid token, 200 with one.
app.MapGet("/whoami", (HttpContext ctx) =>
{
    var name = ctx.User.Identity?.Name ?? "(no name claim)";
    var claims = ctx.User.Claims.Select(c => new { c.Type, c.Value });
    return Results.Ok(new { name, claims });
})
.RequireAuthorization();

// Temporary probe: requires the SalesRep policy. Used to verify role policies.
app.MapGet("/salesrep-only", () => Results.Ok(new { message = "You are a SalesRep" }))
    .RequireAuthorization(RolePolicies.RequireSalesRep);

// Returns demo records — automatically filtered to the caller's company by the
// DbContext's global query filter. No companyId is read from the request.
app.MapGet("/demo-records", async (CoreDbContext db) =>
{
    var records = await db.DemoRecords.ToListAsync();
    return Results.Ok(records);
})
.RequireAuthorization();

app.Run();

// Exposes the implicit Program class to the test project (WebApplicationFactory).
public partial class Program { }