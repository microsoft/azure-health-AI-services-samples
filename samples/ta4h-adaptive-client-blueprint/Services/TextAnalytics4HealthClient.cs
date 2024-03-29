﻿using Microsoft.Extensions.Logging;
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
    private readonly Random _jitterer = new Random(DateTime.Now.Millisecond);

    private const string ApiKeyHeaderName = "Ocp-Apim-Subscription-Key";
    private const string OperationLocationHeaderName = "Operation-Location";

    public TextAnalytics4HealthClient(ILogger<TextAnalytics4HealthClient> logger, IOptions<Ta4hOptions> options)
    {
        _options = options.Value;
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(_options.Endpoint);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _logger = logger;
    }

    public async Task<string> SendPayloadToProcessingAsync(Ta4hInputPayload payload)
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
        request.Headers.Add(ApiKeyHeaderName, _options.ApiKey);
        var response = await SendRequestWithRetryMechanismAsync(request);
        response.Headers.TryGetValues(OperationLocationHeaderName, out var values);
        var operationLocation = values.FirstOrDefault();
        var jobId = operationLocation.Split("?")[0].Split("/").Last();
        return jobId;
    }

    public async Task<TextAnlyticsJobResponse> GetHealthcareAnalysisOperationStatusAndResultsAsync(string jobId)
    {
        var url = $"/language/analyze-text/jobs/{jobId}?api-version={_options.ApiVersion}&top={_options.MaxDocsPerRequest}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(ApiKeyHeaderName, _options.ApiKey);
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
                if (attemptsCounter > 0)
                {
                    _logger.LogWarning("retrying call to {uri}: attempt number {attempt}, ", request.RequestUri, attemptsCounter + 1);
                }
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content?.ReadAsStringAsync();
                    _logger.LogError("attempt number {attempt}, {uri}, Got an http excpetion, retry-after header = {RetryAfter}, {statusCode}, {err}", attemptsCounter + 1, request.RequestUri, response.Headers.RetryAfter, response.StatusCode, errorContent);
                    var retryAfter = response.Headers.RetryAfter?.Delta;
                    throw new HttpException(response.StatusCode, errorContent, retryAfter);
                }
                return response;
            }
            catch (HttpException httpException) when (httpException.StatusCode == HttpStatusCode.TooManyRequests && attemptsCounter < maxAttemps)
            {
                attemptsCounter++;
                lastException = httpException;
                var retryAfter = (httpException.RetryAfter ?? TimeSpan.FromSeconds(30)) * (1 + _jitterer.NextDouble());

                _logger.LogWarning("Request to {url} failed with http 429 status. next retry in {tryAgainInSeconds} seconds", request.RequestUri, (int)retryAfter.TotalSeconds);
                await Task.Delay(retryAfter);
            }
            catch (TaskCanceledException taskCancelledException) when (attemptsCounter < maxAttemps)
            {
                attemptsCounter++;
                lastException = taskCancelledException;
                var tryAgainInSeconds = 1;
                _logger.LogWarning("Request to {url} failed with timeout: {message}. next retry in {tryAgainInSeconds}", request.RequestUri, taskCancelledException.Message, tryAgainInSeconds);
                await Task.Delay(TimeSpan.FromSeconds(tryAgainInSeconds));
            }
            catch (Exception ex) 
            {
                lastException = ex;
                _logger.LogError(ex, "Unexpected error");
            }
            var newRequest = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Content = request.Content,
                
            };
            newRequest.Headers.Add(ApiKeyHeaderName, _options.ApiKey);
            request = newRequest;
        }
        throw lastException;
    }
}
