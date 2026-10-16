using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Viariato.Api;

/// <summary>
/// Who is on the other end of a request. In production every request reaches the API through Caddy (and, before it, Cloudflare
/// Tunnel), so the connection always comes from Caddy's container: without this, every user shares one IP, and the per-IP limits
/// (logging in, renewing the session) are shared by everyone — a few reloads between several people logged them all out. The
/// client's IP travels in X-Forwarded-For, and it is believed only from proxies on private networks (Caddy, cloudflared, Docker's
/// own), never from the Internet: Caddy replaces the header for anyone who is not one of them.
/// </summary>
public static class IpDelCliente
{
    private static readonly (string Red, int Prefijo)[] RedesPrivadas =
        [("10.0.0.0", 8), ("172.16.0.0", 12), ("192.168.0.0", 16), ("127.0.0.0", 8), ("::1", 128), ("fc00::", 7)];

    public static IServiceCollection AddIpDelCliente(this IServiceCollection services) =>
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            // Cloudflare → cloudflared → Caddy → API is more than one hop: the header is walked from the right while the hop that
            // added the entry is on a private network, and the first address that is not is the client.
            options.ForwardLimit = null;
            options.KnownProxies.Clear();
            options.KnownNetworks.Clear();
            foreach (var (red, prefijo) in RedesPrivadas)
            {
                options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse(red), prefijo));
            }
        });
}
