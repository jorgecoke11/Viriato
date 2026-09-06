using System.Net;
using System.Text;

namespace Viariato.Modules.Markets.Tests.Sourcing;

/// <summary>Returns canned content for every request, so scraping parsers can be tested without real network calls.</summary>
internal sealed class StubHttpMessageHandler(string content, string mediaType = "text/html") : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, mediaType),
        };
        return Task.FromResult(response);
    }

    public static HttpClient CreateClient(string content, string mediaType = "text/html") =>
        new(new StubHttpMessageHandler(content, mediaType)) { BaseAddress = new Uri("https://example.test") };
}
