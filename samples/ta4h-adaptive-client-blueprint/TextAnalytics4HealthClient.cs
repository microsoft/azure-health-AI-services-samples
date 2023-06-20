using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net;
using System.Text;
using TextAnalyticsHealthcareAdaptiveClient.TextAnalyticsApiSchema;


public class TextAnalytics4HealthClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly Ta4hOptions _options;

    public TextAnalytics4HealthClient(ILogger<TextAnalytics4HealthClient> logger, IOptions<Ta4hOptions> options)
    {

        _options = options.Value;
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(_options.Endpoint);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _logger = logger;
    }

    public async Task<string> StartHealthcareAnalysisOperationAsync(Ta4hInputPayload payload)
    {
        var url = $"/language/analyze-text/jobs?api-version={_options.ApiVersion}";

        var requestBody = new
        {
            tasks = new[]
            {
                new
                {
                    kind = "Healthcare",
                    parameters = new { modelVersion = _options.ModelVersion }
                }
            },
            analysisInput = new
            {
                documents = payload.Documents
            }
        };

        var serializedRequestBody = JsonConvert.SerializeObject(requestBody);
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(serializedRequestBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Ocp-Apim-Subscription-Key", _options.ApiKey);
        var response = await SendRequestWithRetryMechanismAsync(request);
        response.Headers.TryGetValues("Operation-Location", out var values);
        var operationLocation = values.FirstOrDefault();
        var jobId = operationLocation.Split("?")[0].Split("/").Last();
        payload.JobId = jobId;
        return jobId;
    }

    public async Task<TextAnlyticsJobResponse> GetHealthcareAnalysisOperationStatusAndResultsAsync(string jobId)
    {
        var url = $"/language/analyze-text/jobs/{jobId}?api-version={_options.ApiVersion}&top={_options.MaxDocsPerRequest}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Ocp-Apim-Subscription-Key", _options.ApiKey);
        var response = await SendRequestWithRetryMechanismAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<TextAnlyticsJobResponse>(content);
    }

    private async Task<HttpResponseMessage> SendRequestWithRetryMechanismAsync(HttpRequestMessage request)
    {
        int attemptsCounter = 0;
        int maxAttemps = 5;
        Exception lastException = null;
        while (attemptsCounter < maxAttemps)
        {
            try
            {
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content?.ReadAsStringAsync();
                    _logger.LogWarning("Got an http excpetion, retry-after header = {RetryAfter}", response.Headers.RetryAfter);
                    throw new HttpException(response.StatusCode, errorContent);
                }
                return response;
            }
            catch (HttpException httpException) when (httpException.StatusCode == HttpStatusCode.TooManyRequests && ++attemptsCounter < maxAttemps)
            {
                lastException = httpException;
                var jitterer = new Random(request.RequestUri.OriginalString.GetHashCode());
                var tryAgainInSeconds = jitterer.Next(15, 60);
                _logger.LogWarning("Request to {url} failed with http 429 status. next retry in {tryAgainInSeconds} seconds", request.RequestUri, tryAgainInSeconds);
                await Task.Delay(TimeSpan.FromSeconds(1));
                _logger.LogInformation("Waited to retry for {tryAgainInSeconds}, ready to try again", tryAgainInSeconds);
            }
            catch (TaskCanceledException taskCancelledException)
            {
                attemptsCounter++;
                var tryAgainInSeconds = 1;
                _logger.LogWarning("Request to {url} failed with timeout: {message}. next retry in {tryAgainInSeconds}", request.RequestUri, taskCancelledException.Message, tryAgainInSeconds);
                await Task.Delay(TimeSpan.FromSeconds(tryAgainInSeconds));
            }
            var newRequest = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Content = request.Content,
                
            };
            newRequest.Headers.Add("Ocp-Apim-Subscription-Key", _options.ApiKey);
            request = newRequest;
        }
        throw lastException;
    }
}
