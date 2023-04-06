using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.AI.TextAnalytics;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using text_analytics_for_health_support_functions.Models;

namespace text_analytics_for_health_support_functions
{
    public class MedicalDocumentsToFhirFunction
    {
        private static readonly string TextAnalyticsKey = Environment.GetEnvironmentVariable("AzureAI_Key");
        private static readonly string TextAnalyticsEndPoint = Environment.GetEnvironmentVariable("AzureAI_Endpoint");
        private static readonly string TranslatorSubscriptionKey = Environment.GetEnvironmentVariable("General_CognitiveServices_Key");
        private static readonly string TranslatorEndpoint = " https://api.cognitive.microsofttranslator.com";

        [FunctionName("MedicalDocumentsToFhirFunction")]
        public async Task Run([BlobTrigger("medical-texts-input", Connection = "AzureWebStorageForData")]Stream myBlob, ILogger log)
        {
            //Read Document
            string document = await new StreamReader(myBlob).ReadToEndAsync();

            //Init Client
            var client = new TextAnalyticsClient(new Uri(TextAnalyticsEndPoint), new AzureKeyCredential(TextAnalyticsKey));

            //Detect Language
            var response = client.DetectLanguage(document);
            var language = response.Value;
            if (language.Iso6391Name != "en")
            {
                //If Language is not english, translate
                document = await TranslateTextRequest(TranslatorSubscriptionKey, TranslatorEndpoint, document);
            }

            if(!string.IsNullOrWhiteSpace(document))
            {
                //Start Job
                string operationLocation;
                using (var httpClient = new HttpClient())
                {
                    using (var request = new HttpRequestMessage(new HttpMethod("POST"), $"{TextAnalyticsEndPoint}/language/analyze-text/jobs?api-version=2022-05-15-preview"))
                    {
                        request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", TextAnalyticsKey);
                        //Define model to send with document data
                        var requestModel = new TextAnalyticsForHealthModel
                        {
                            AnalysisInput = new AnalysisInput
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
                            },
                            Tasks = new List<HealthTask>
                            {
                                new HealthTask
                                {
                                    Kind = "Healthcare",
                                    Parameters = new Parameters
                                    {
                                        ModelVersion = "latest",
                                        FhirVersion = "4.0.1"
                                    }
                                }
                            }
                        };
                        request.Content = new StringContent(JsonConvert.SerializeObject(requestModel));
                        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
                        var textAnalysisResponse = await httpClient.SendAsync(request);
                        operationLocation = textAnalysisResponse.Headers.GetValues("operation-location").FirstOrDefault();
                    }
                }

                //Check Job Status and create retry functionality with Polly
                var max_Retries = 10;
                var pollyContext = new Context("Retry 202");
                var retryPolicy = Policy.Handle<HttpRequestException>(ex => ex.Message.Contains("503"))
                .OrResult<HttpResponseMessage>(r => {
                    if (r.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        return false;
                    }
                    var tempResponse = r.Content.ReadAsStringAsync().Result;
                    var textAnalyticsForHealthResponse = JsonConvert.DeserializeObject<TextAnalyticsForHealthResponse>(tempResponse);
                    return textAnalyticsForHealthResponse.ReponseHealthTasks.Completed == 0;
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
                            request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", TextAnalyticsKey);
                            var textAnalysisResponse = httpClient.Send(request);
                            textAnalysisResponse.EnsureSuccessStatusCode();
                            return textAnalysisResponse;
                        }
                    }
                }, pollyContext);

                var finishedResponse = await successfullFhirResponse.Result.Content.ReadAsStringAsync();
                var responseItems = JsonConvert.DeserializeObject<TextAnalyticsForHealthResponse>(finishedResponse).ReponseHealthTasks.Items;
                if (responseItems.Any())
                {
                    var firstResponseItem = responseItems.FirstOrDefault();
                    if(firstResponseItem != null) 
                    {
                        var documents = firstResponseItem.Results.Documents;
                        if(documents != null && documents.Any())
                        {
                            var fhirBundle = documents.First().FhirBundle;
                            if (fhirBundle != null)
                            {
                                await PersistBundleOnStorageAccount(fhirBundle);
                            }
                        }
                    }
                }
            }
        }

        static private async Task PersistBundleOnStorageAccount(object fhirBundle)
        {
            var blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("AzureWebStorageForData"));
            var containerClient = blobServiceClient.GetBlobContainerClient("medical-texts-fhir");
            string fileName = "fhirbundle_" + Guid.NewGuid().ToString() + ".json";
            BlobClient blobClient = containerClient.GetBlobClient(fileName);
            await blobClient.UploadAsync(BinaryData.FromString(fhirBundle.ToString()), true);
        }
        static private async Task<string> TranslateTextRequest(string resourceKey, string endpoint, string inputText)
        {
            object[] body = new object[] { new { Text = inputText } };
            var requestBody = JsonConvert.SerializeObject(body);

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                // Build the request.
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(endpoint + "/translate?api-version=3.0&to=en");
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                request.Headers.Add("Ocp-Apim-Subscription-Key", resourceKey);
                request.Headers.Add("Ocp-Apim-Subscription-Region", "westeurope");

                // Send the request and get response.
                HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                // Read response as a string.
                string result = await response.Content.ReadAsStringAsync();
                TranslationResult[] deserializedOutput = JsonConvert.DeserializeObject<TranslationResult[]>(result);
                // Iterate over the deserialized results.
                if (deserializedOutput.Any())
                {
                    var o = deserializedOutput.FirstOrDefault();
                    if (o.DetectedLanguage.Language == "en")
                    {
                        return inputText;
                    }
                    if (o.Translations.Any())
                    {
                        return o.Translations.FirstOrDefault().Text;
                    }
                }
                return "";
            }
        }
    }
}
