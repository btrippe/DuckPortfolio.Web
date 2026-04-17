using System.Net;

namespace DuckPortfolio.BibleApi.Services;

public sealed class YouVersionApiException : Exception
{
    public YouVersionApiException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
