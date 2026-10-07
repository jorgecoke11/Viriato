using System.Net;

namespace Viriato.Rpa.Client;

/// <summary>
/// The Viriato API answered, but with an error status. <see cref="Detail"/> carries the server's own
/// explanation (its problem-details <c>detail</c>) when it sent one. Network failures and timeouts are
/// NOT wrapped in this — they surface as <see cref="HttpRequestException"/>/<see cref="TaskCanceledException"/>.
/// </summary>
public sealed class ViriatoApiException : Exception
{
    public ViriatoApiException(HttpStatusCode statusCode, string? detail)
        : base(BuildMessage(statusCode, detail))
    {
        StatusCode = statusCode;
        Detail = detail;
    }

    public HttpStatusCode StatusCode { get; }

    public string? Detail { get; }

    /// <summary>The API key is missing, wrong or revoked — no retry will fix this.</summary>
    public bool IsUnauthorized => StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

    /// <summary>The step does not exist or is not claimed by this Despliegue.</summary>
    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;

    /// <summary>A state clash: the Despliegue is switched off, or the step is no longer in progress.</summary>
    public bool IsConflict => StatusCode == HttpStatusCode.Conflict;

    /// <summary>The key is valid but not allowed to do this (e.g. creating Cases without having been authorised to).</summary>
    public bool IsForbidden => StatusCode == HttpStatusCode.Forbidden;

    /// <summary>Worth retrying later: a server-side failure, throttling or a server-side timeout.</summary>
    public bool IsTransient => (int)StatusCode >= 500 || StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests;

    private static string BuildMessage(HttpStatusCode statusCode, string? detail) =>
        string.IsNullOrWhiteSpace(detail)
            ? $"La API de Viriato respondió {(int)statusCode} {statusCode}."
            : $"La API de Viriato respondió {(int)statusCode} {statusCode}: {detail}";
}
