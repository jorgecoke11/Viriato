using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Viariato.Infrastructure;
using Viariato.Modules.Users.Auth;
using Viariato.Modules.Users.Contracts;
using Viariato.Modules.Users.Domain;
using Viariato.Modules.Users.Validation;
using Viariato.Shared;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;
using Viariato.Shared.Options;

namespace Viariato.Modules.Users.Endpoints;

internal static class AuthEndpoints
{
    private const string RefreshCookieName = "refresh_token";
    private const string CookiePath = "/api/v1/auth";

    private static readonly string DummyPasswordHash =
        new PasswordHasher<User>().HashPassword(new User(), "dummy-password-not-used-1234567890");

    public static void MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth");

        group.MapPost("/register", RegisterAsync).RequireRateLimiting(RateLimitPolicyNames.AuthByCredentials);
        group.MapPost("/login", LoginAsync).RequireRateLimiting(RateLimitPolicyNames.AuthByCredentials);
        group.MapPost("/refresh", RefreshAsync).RequireRateLimiting(RateLimitPolicyNames.AuthByIp);
        group.MapPost("/logout", LogoutAsync);
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        RegisterRequestValidator validator,
        AppDbContext db,
        IPasswordHasher<User> hasher,
        TokenService tokens,
        IOptions<AuthOptions> authOptions,
        IHostEnvironment env,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid)
        {
            return ProblemResults.ValidationProblem(validation);
        }

        var normalized = request.Email.Trim().ToLowerInvariant();
        var existing = await db.Set<User>().FirstOrDefaultAsync(u => u.EmailNormalized == normalized, ct);

        // Always hash, even on the duplicate-email path, so response timing does not reveal whether the email exists.
        hasher.HashPassword(new User(), request.Password);

        if (existing is not null)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { ["email"] = ["No se pudo completar el registro."] },
                detail: "No se pudo completar el registro.",
                instance: http.Request.Path);
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Email = request.Email,
            EmailNormalized = normalized,
            DisplayName = request.DisplayName,
            CreatedAt = now,
            UpdatedAt = now,
        };
        user.PasswordHash = hasher.HashPassword(user, request.Password);

        db.Add(user);

        var userRole = await db.Set<Role>().SingleAsync(r => r.Name == SystemRoles.User, ct);
        db.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id, GrantedAt = now });

        await db.SaveChangesAsync(ct);

        var issued = await tokens.IssueAsync(user, GetClientIp(http), GetUserAgent(http), ct);
        SetRefreshCookie(http, issued.RawRefreshToken, authOptions.Value.RefreshTokenDays, IsSecure(env));

        return Results.Ok(new AuthResponse(issued.AccessToken, issued.ExpiresIn, user.ToDto(issued.Roles, issued.Permissions)));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        LoginRequestValidator validator,
        AppDbContext db,
        IPasswordHasher<User> hasher,
        TokenService tokens,
        IOptions<AuthOptions> authOptions,
        IHostEnvironment env,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid)
        {
            return ProblemResults.ValidationProblem(validation);
        }

        var normalized = request.Email.Trim().ToLowerInvariant();
        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.EmailNormalized == normalized, ct);

        // Always run password verification, even when the user does not exist, to avoid timing attacks.
        var hashToVerify = user?.PasswordHash ?? DummyPasswordHash;
        var verifyResult = hasher.VerifyHashedPassword(user ?? new User(), hashToVerify, request.Password);
        var passwordOk = verifyResult is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;

        if (user is null || !user.IsActive || !passwordOk)
        {
            return GenericLoginFailure(http);
        }

        var issued = await tokens.IssueAsync(user, GetClientIp(http), GetUserAgent(http), ct);
        SetRefreshCookie(http, issued.RawRefreshToken, authOptions.Value.RefreshTokenDays, IsSecure(env));

        return Results.Ok(new AuthResponse(issued.AccessToken, issued.ExpiresIn, user.ToDto(issued.Roles, issued.Permissions)));
    }

    private static async Task<IResult> RefreshAsync(
        AppDbContext db,
        TokenService tokens,
        IOptions<AuthOptions> authOptions,
        IHostEnvironment env,
        ILoggerFactory loggerFactory,
        HttpContext http,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Viariato.Modules.Users.Auth.Refresh");

        if (!http.Request.Cookies.TryGetValue(RefreshCookieName, out var rawToken) || string.IsNullOrEmpty(rawToken))
        {
            return ProblemResults.Unauthorized(http, "No se encontró una sesión activa.");
        }

        var result = await tokens.ValidateAndConsumeAsync(rawToken, ct);

        switch (result.Outcome)
        {
            case RefreshOutcome.NotFound:
                DeleteRefreshCookie(http, IsSecure(env));
                return ProblemResults.Unauthorized(http, "Sesión inválida.");

            case RefreshOutcome.Reused:
                logger.LogWarning("Refresh token reuse detected for family {FamilyId}", result.Token!.FamilyId);
                DeleteRefreshCookie(http, IsSecure(env));
                return ProblemResults.Unauthorized(http, "Sesión inválida.");

            case RefreshOutcome.Expired:
                DeleteRefreshCookie(http, IsSecure(env));
                return ProblemResults.Unauthorized(http, "La sesión ha expirado.");
        }

        var issued = await tokens.RotateAsync(result.Token!, GetClientIp(http), GetUserAgent(http), ct);
        SetRefreshCookie(http, issued.RawRefreshToken, authOptions.Value.RefreshTokenDays, IsSecure(env));

        var user = await db.Set<User>().SingleAsync(u => u.Id == result.Token!.UserId, ct);

        return Results.Ok(new AuthResponse(issued.AccessToken, issued.ExpiresIn, user.ToDto(issued.Roles, issued.Permissions)));
    }

    private static async Task<IResult> LogoutAsync(
        TokenService tokens,
        IHostEnvironment env,
        HttpContext http,
        CancellationToken ct)
    {
        if (http.Request.Cookies.TryGetValue(RefreshCookieName, out var rawToken) && !string.IsNullOrEmpty(rawToken))
        {
            var token = await tokens.FindByRawTokenAsync(rawToken, ct);
            if (token is not null)
            {
                await tokens.RevokeFamilyAsync(token.FamilyId, ct);
            }
        }

        DeleteRefreshCookie(http, IsSecure(env));
        return Results.NoContent();
    }

    private static void SetRefreshCookie(HttpContext http, string rawToken, int days, bool secure)
    {
        http.Response.Cookies.Append(RefreshCookieName, rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            MaxAge = TimeSpan.FromDays(days),
        });
    }

    private static void DeleteRefreshCookie(HttpContext http, bool secure)
    {
        http.Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
        });
    }

    private static bool IsSecure(IHostEnvironment env) => !env.IsDevelopment();

    private static string? GetClientIp(HttpContext http) => http.Connection.RemoteIpAddress?.ToString();

    private static string? GetUserAgent(HttpContext http)
    {
        var value = http.Request.Headers.UserAgent.ToString();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static IResult GenericLoginFailure(HttpContext http) => Results.Problem(
        type: "https://viariato.app/errors/invalid-credentials",
        title: "Invalid credentials",
        statusCode: StatusCodes.Status401Unauthorized,
        detail: "Email o contraseña incorrectos.",
        instance: http.Request.Path);
}
