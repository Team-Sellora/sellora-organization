using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Sellora.CoreService.Api.Authorization;

var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

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

app.Run();

// Exposes the implicit Program class to the test project (WebApplicationFactory).
public partial class Program { }
