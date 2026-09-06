using System.Net;
using System.Text;

namespace Viariato.Modules.Markets.Tests.Sourcing;

/// <summary>Like <see cref="StubHttpMessageHandler"/> but returns different canned content depending
/// on a substring match against the request URL — needed when a source hits more than one page.</summary>
internal sealed class RoutedHttpMessageHandler(IReadOnlyDictionary<string, string> contentByUrlSubstring) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();
        var match = contentByUrlSubstring.First(kv => url.EndsWith(kv.Key, StringComparison.OrdinalIgnoreCase));

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(match.Value, Encoding.UTF8, "text/html"),
        };
        return Task.FromResult(response);
    }

    public static HttpClient CreateClient(IReadOnlyDictionary<string, string> contentByUrlSubstring) =>
        new(new RoutedHttpMessageHandler(contentByUrlSubstring)) { BaseAddress = new Uri("https://example.test") };
}
