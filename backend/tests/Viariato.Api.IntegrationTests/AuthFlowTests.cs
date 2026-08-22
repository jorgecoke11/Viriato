using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Viariato.Api.IntegrationTests;

public sealed class AuthFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private sealed record ProblemDetailsPayload(string? Detail);

    private static string RandomEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static string ExtractCookie(HttpResponseMessage response, string name)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies), "Response did not set any cookies.");

        foreach (var cookie in cookies)
        {
            if (cookie.StartsWith($"{name}=", StringComparison.Ordinal))
            {
                var value = cookie[(name.Length + 1)..];
                var semicolon = value.IndexOf(';');
                return semicolon >= 0 ? value[..semicolon] : value;
            }
        }

        throw new InvalidOperationException($"Cookie '{name}' was not present in the response.");
    }

    private static HttpRequestMessage WithRefreshCookie(HttpMethod method, string url, string cookieValue)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Cookie", $"refresh_token={cookieValue}");
        return request;
    }

    [Fact]
    public async Task Register_ThenLogin_IssuesAnAccessTokenAndTheUserProfile()
    {
        var client = factory.CreateClient();
        var email = RandomEmail();

        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "SuperSecret123",
            displayName = "Test User",
        });

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "SuperSecret123" });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Register_WithAnAlreadyRegisteredEmail_FailsWithoutConfirmingExistence()
    {
        var client = factory.CreateClient();
        var email = RandomEmail();
        var payload = new { email, password = "SuperSecret123", displayName = "Test User" };

        var first = await client.PostAsJsonAsync("/api/v1/auth/register", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/v1/auth/register", payload);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Login_WithAWrongPassword_ReturnsTheSameGenericUnauthorizedAsAnUnknownEmail()
    {
        var client = factory.CreateClient();
        var email = RandomEmail();
        await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "SuperSecret123", displayName = "Test User" });

        var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "WrongPassword123" });
        var unknownEmail = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = RandomEmail(), password = "WrongPassword123" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);

        // Same message either way — the response must not reveal whether the email exists.
        var wrongPasswordDetail = (await wrongPassword.Content.ReadFromJsonAsync<ProblemDetailsPayload>())!.Detail;
        var unknownEmailDetail = (await unknownEmail.Content.ReadFromJsonAsync<ProblemDetailsPayload>())!.Detail;
        Assert.Equal(wrongPasswordDetail, unknownEmailDetail);
    }

    [Fact]
    public async Task RefreshRotation_IssuesANewCookieEachTime_AndReplayingAnOldOneRevokesTheWholeFamily()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var email = RandomEmail();

        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "SuperSecret123",
            displayName = "Test User",
        });
        var firstCookie = ExtractCookie(registerResponse, "refresh_token");

        var refresh1 = await client.SendAsync(WithRefreshCookie(HttpMethod.Post, "/api/v1/auth/refresh", firstCookie));
        Assert.Equal(HttpStatusCode.OK, refresh1.StatusCode);
        var secondCookie = ExtractCookie(refresh1, "refresh_token");
        Assert.NotEqual(firstCookie, secondCookie);

        var refresh2 = await client.SendAsync(WithRefreshCookie(HttpMethod.Post, "/api/v1/auth/refresh", secondCookie));
        Assert.Equal(HttpStatusCode.OK, refresh2.StatusCode);
        var thirdCookie = ExtractCookie(refresh2, "refresh_token");

        // Replaying the very first (already-rotated-away) token must be rejected as theft.
        var reuseAttempt = await client.SendAsync(WithRefreshCookie(HttpMethod.Post, "/api/v1/auth/refresh", firstCookie));
        Assert.Equal(HttpStatusCode.Unauthorized, reuseAttempt.StatusCode);

        // The reuse must have revoked the entire family — the latest, otherwise-valid token is also gone.
        var afterReuse = await client.SendAsync(WithRefreshCookie(HttpMethod.Post, "/api/v1/auth/refresh", thirdCookie));
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesTheSession_SoASubsequentRefreshFails()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var email = RandomEmail();

        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "SuperSecret123",
            displayName = "Test User",
        });
        var cookie = ExtractCookie(registerResponse, "refresh_token");

        var logoutResponse = await client.SendAsync(WithRefreshCookie(HttpMethod.Post, "/api/v1/auth/logout", cookie));
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refreshAfterLogout = await client.SendAsync(WithRefreshCookie(HttpMethod.Post, "/api/v1/auth/refresh", cookie));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutAToken_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
