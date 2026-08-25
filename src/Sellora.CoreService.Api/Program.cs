using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Sellora.CoreService.Api.Authorization;
using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Domain.Tenancy;
using Sellora.CoreService.Api.Tenancy;
using Sellora.CoreService.Infrastructure.Persistence;
using Sellora.CoreService.Api.Middleware;

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
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

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

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();

app.MapControllers();

app.UseMiddleware<CorrelationIdMiddleware>();

// Database initialization
using (var scope = app.Services.CreateScope())
{
  var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
  db.Database.EnsureCreated();
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



app.MapHealthChecks("/health");

if (!app.Environment.IsProduction())
{
    app.MapGet("/nonexistent", () => { throw new InvalidOperationException("Test failure"); });
}

app.Run();

// Exposes the implicit Program class to the test project (WebApplicationFactory).
public partial class Program { }
