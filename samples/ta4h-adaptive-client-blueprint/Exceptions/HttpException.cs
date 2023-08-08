using System.Net;

public class HttpException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string ResponseContent { get; }

    public TimeSpan? RetryAfter  { get; }

    public HttpException(HttpStatusCode statusCode, string responseContent, TimeSpan? retryAfter = null)
        : base($"HTTP error occurred with status code: {statusCode}")
    {
        StatusCode = statusCode;
        ResponseContent = responseContent;
        RetryAfter = retryAfter;
    }
}
