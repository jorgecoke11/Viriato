using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Viariato.Api;
using Viariato.Api.RateLimiting;
using Viariato.Infrastructure;
using Viariato.Modules.Markets;
using Viariato.Modules.Markets.Endpoints;
using Viariato.Modules.Casos;
using Viariato.Modules.Casos.Endpoints;
using Viariato.Modules.Flujos;
using Viariato.Modules.Flujos.Endpoints;
using Viariato.Modules.Ops;
using Viariato.Modules.Ops.Endpoints;
using Viariato.Modules.RpaFleet;
using Viariato.Modules.RpaFleet.Auth;
using Viariato.Modules.RpaFleet.Endpoints;
using Viariato.Modules.Users;
using Viariato.Modules.Users.Endpoints;
using Viariato.Shared.Authorization;
using Viariato.Shared.Options;

// Keep JWT claim types exactly as issued (sub/email/name/role/perm) instead of ASP.NET Core's
// legacy remapping to long XML-schema URIs.
System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddUsersModule(builder.Configuration);
builder.Services.AddOpsModule(builder.Configuration);
builder.Services.AddMarketsModule(builder.Configuration);
builder.Services.AddFlujosModule(builder.Configuration);
builder.Services.AddRpaFleetModule(builder.Configuration);
builder.Services.AddCasosModule(builder.Configuration);
builder.Services.AddOpenApi();

// Markets exposes enums (MarketSignal, CriterionResult) straight through its DTOs — serialize them
// as their names, not raw ints, so the frontend doesn't need a numeric lookup table.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        o => !string.IsNullOrEmpty(o.SigningKey) && Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
        "Jwt:SigningKey must be at least 32 bytes.")
    .ValidateOnStart();

builder.Services.AddOptions<AuthOptions>()
    .Bind(builder.Configuration.GetSection(AuthOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddOptions<MarketsOptions>()
    .Bind(builder.Configuration.GetSection(MarketsOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddOptions<DocumentStorageOptions>()
    .Bind(builder.Configuration.GetSection(DocumentStorageOptions.SectionName))
    .ValidateOnStart();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer()
    .AddRpaFleetApiKeyScheme();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        bearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = "role",
        };
    });

builder.Services.AddAuthorization(options =>
{
    foreach (var permission in Permissions.All)
    {
        options.AddPolicy(permission, policy => policy.RequireClaim("perm", permission));
    }
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddAuthRateLimiting(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseExceptionHandler();

app.UseAuthCredentialsCapture();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    return canConnect
        ? Results.Ok(new { status = "healthy", database = "connected" })
        : Results.Problem("Database unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
});

// Version + commit are baked into the assembly at publish time (-p:Version / -p:SourceRevisionId
// in the Dockerfile), so this just reads back what was compiled in — no config, no file I/O.
// Public and unauthenticated: it's harmless build info, and the frontend shows it pre-login too.
var informationalVersion = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
var versionParts = (informationalVersion ?? "0.0.0-dev").Split('+', 2);
var versionResponse = new { version = versionParts[0], commit = versionParts.Length > 1 ? versionParts[1] : null };

app.MapGet("/api/v1/version", () => Results.Ok(versionResponse));

app.MapUsersEndpoints();
app.MapOpsEndpoints();
app.MapMarketsEndpoints();
app.MapFlujosEndpoints();
app.MapRpaFleetEndpoints();
app.MapCasosEndpoints();

app.Run();

// Exposes the implicit Program class to Viariato.Api.IntegrationTests via WebApplicationFactory<Program>.
public partial class Program;
