using System.Net;

public class HttpException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string ResponseContent { get; }

    public HttpException(HttpStatusCode statusCode, string responseContent)
        : base($"HTTP error occurred with status code: {statusCode}")
    {
        StatusCode = statusCode;
        ResponseContent = responseContent;
    }
}
