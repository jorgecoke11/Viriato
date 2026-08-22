using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Viariato.Shared;

namespace Viariato.Api.RateLimiting;

public static class AuthRateLimiting
{
    private const string EmailItemKey = "RateLimitEmail";

    // 5 attempts / 15 minutes per §6.4. Overridable via RateLimiting:Auth:{PermitLimit,WindowMinutes}
    // so integration tests can raise the ceiling instead of tripping over it mid-flow.
    public static void AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue("RateLimiting:Auth:PermitLimit", 5);
        var window = TimeSpan.FromMinutes(configuration.GetValue("RateLimiting:Auth:WindowMinutes", 15));

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(RateLimitPolicyNames.AuthByCredentials, httpContext =>
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var email = httpContext.Items[EmailItemKey] as string ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter($"{ip}:{email}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0,
                });
            });

            options.AddPolicy(RateLimitPolicyNames.AuthByIp, httpContext =>
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0,
                });
            });

            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Type = "https://viariato.app/errors/rate-limited",
                    Title = "Too many requests",
                    Status = StatusCodes.Status429TooManyRequests,
                    Detail = "Has superado el número de intentos permitidos. Inténtalo de nuevo más tarde.",
                    Instance = context.HttpContext.Request.Path,
                }, ct);
            };
        });
    }

    /// <summary>
    /// Buffers and peeks the request body for an "email" field so the rate limiter can partition
    /// login/register attempts by IP + email, without the partition resolver itself doing I/O.
    /// </summary>
    public static IApplicationBuilder UseAuthCredentialsCapture(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? string.Empty;
            var isCredentialsEndpoint = HttpMethods.IsPost(context.Request.Method) &&
                (path.EndsWith("/auth/login", StringComparison.OrdinalIgnoreCase) ||
                 path.EndsWith("/auth/register", StringComparison.OrdinalIgnoreCase));

            if (isCredentialsEndpoint)
            {
                context.Request.EnableBuffering();
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;

                try
                {
                    using var document = JsonDocument.Parse(body);
                    if (document.RootElement.TryGetProperty("email", out var emailProperty))
                    {
                        context.Items[EmailItemKey] = emailProperty.GetString()?.Trim().ToLowerInvariant();
                    }
                }
                catch (JsonException)
                {
                    // Malformed body: let model binding produce the validation error downstream.
                }
            }

            await next();
        });
    }
}
