using Microsoft.AspNetCore.Authentication;

namespace Viariato.Modules.RpaFleet.Auth;

public static class RpaFleetAuthenticationExtensions
{
    public static AuthenticationBuilder AddRpaFleetApiKeyScheme(this AuthenticationBuilder builder) =>
        builder.AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyDefaults.AuthenticationScheme, null);
}
