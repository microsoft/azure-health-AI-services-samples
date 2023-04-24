using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;
using TextAnalyticsForHealthAsync_Client.Models;

namespace TextAnalyticsForHealthAsync_Client
{
    public static class RuntTA4HWorkloadFunction
    {
        private static readonly string TextAnalyticsEndPoint = "http://localhost:5000/text/analytics/v3.1/entities/health/jobs";

        [FunctionName("RuntTA4HWorkloadFunction")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            //Read Document
            string document = await new StreamReader(req.Body).ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(document))
            {
                //Start Job
                string operationLocation;
                using (var httpClient = new HttpClient())
                {
                    using (var request = new HttpRequestMessage(new HttpMethod("POST"), $"{TextAnalyticsEndPoint}"))
                    {
                        //Define model to send with document data
                        var requestModel = new AnalysisInput
                        {
                            Documents = new List<HealthDocument>
                                {
                                   new HealthDocument
                                   {
                                       Id = "1",
                                       Language = "en",
                                       Text = document
                                   }
                                }
                        };
                        request.Content = new StringContent(JsonConvert.SerializeObject(requestModel));
                        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
                        var textAnalysisResponse = await httpClient.SendAsync(request);
                        operationLocation = textAnalysisResponse.Headers.GetValues("operation-location").FirstOrDefault();
                    }
                }
                operationLocation = operationLocation.Substring("http://localhost".Length, operationLocation.Length - "http://localhost".Length);
                operationLocation = "http://localhost:5000" + operationLocation;
                //Check Job Status and create retry functionality with Polly
                var max_Retries = 10;
                var pollyContext = new Context("Retry 202");
                var retryPolicy = Policy.Handle<HttpRequestException>(ex => ex.Message.Contains("503"))
                .OrResult<HttpResponseMessage>(r =>
                {
                    if (r.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        return false;
                    }
                    var tempResponse = r.Content.ReadAsStringAsync().Result;
                    var textAnalyticsForHealthResponse = JsonConvert.DeserializeObject<TextAnalyticsForHealthResponse>(tempResponse);
                    return textAnalyticsForHealthResponse.Status != "succeeded";
                })
                .WaitAndRetry(retryCount: max_Retries, sleepDurationProvider: (attemptCount) => TimeSpan.FromSeconds(attemptCount * 5),
                 onRetry: (exception, sleepDuration, attemptNumber, context) =>
                 {
                     Console.WriteLine($"{context.OperationKey}: Retry number {attemptNumber} ");
                 });

                var successfullFhirResponse = retryPolicy.ExecuteAndCapture(ctx =>
                {
                    using (var httpClient = new HttpClient())
                    {
                        using (var request = new HttpRequestMessage(new HttpMethod("GET"), operationLocation))
                        {
                            var textAnalysisResponse = httpClient.Send(request);
                            textAnalysisResponse.EnsureSuccessStatusCode();
                            return textAnalysisResponse;
                        }
                    }
                }, pollyContext);

                var finishedResponse = await successfullFhirResponse.Result.Content.ReadAsStringAsync();
                return new OkObjectResult(finishedResponse);
            }
            return new BadRequestErrorMessageResult("");
        }
    }
}
