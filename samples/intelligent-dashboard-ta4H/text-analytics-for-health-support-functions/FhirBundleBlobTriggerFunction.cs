using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;
using text_analytics_for_health_support_functions.Models;
using static System.Formats.Asn1.AsnWriter;

namespace text_analytics_for_health_support_functions
{
    public static class FhirBundleBlobTriggerFunction
    {
        private static readonly HttpClient httpClient = new HttpClient();

        [FunctionName("FhirBundleBlobTriggerFunction")]
        public static async Task Run([BlobTrigger("medical-texts-fhir/{name}", Connection = "AzureWebStorageForData")] Stream myBlob, string name, ILogger log)
        {
            JObject bundle;
            JArray entries;

            string authority = System.Environment.GetEnvironmentVariable("Authority");
            string audience = System.Environment.GetEnvironmentVariable("Audience");
            string clientId = System.Environment.GetEnvironmentVariable("ClientId");
            string clientSecret = System.Environment.GetEnvironmentVariable("ClientSecret");
            Uri fhirServerUrl = new Uri(System.Environment.GetEnvironmentVariable("FhirServerUrl"));
            bool conversionRequired = bool.Parse(System.Environment.GetEnvironmentVariable("UUIDtoResourceTypeConversion"));

            var streamReader = new StreamReader(myBlob);

            if (name.EndsWith(".json"))
            {
                log.LogInformation("file is a json file");

                // read the entire json file
                var fhirString = await streamReader.ReadToEndAsync();
                try
                {
                    bundle = JObject.Parse(fhirString);
                }
                catch (JsonReaderException)
                {
                    log.LogError("Input file is not a valid JSON document");
                    await MoveBlobToRejected(name, log);
                    return;
                }

                // log.LogInformation("File read");

                try
                {
                    if (conversionRequired)
                        FhirImportReferenceConverter.ConvertUUIDs(bundle);
                }
                catch
                {
                    log.LogError("Failed to resolve references in doc during the UUID to Resource Type conversion");
                    await MoveBlobToRejected(name, log);
                    return;
                }

                entries = (JArray)bundle["entry"];
                if (entries == null)
                {
                    log.LogError("No entries found in bundle");
                    throw new FhirImportException("No entries found in bundle");
                }

            }
            else if (name.EndsWith(".ndjson"))
            {
                log.LogInformation("file is a ndjson file");
                entries = new JArray();

                // Read ndjson file line by line
                while (!streamReader.EndOfStream)
                {
                    // Assuming no conversion required for ndjson files
                    var linecontent = await streamReader.ReadLineAsync();
                    var linejobject = JObject.Parse(linecontent);
                    entries.Add(linejobject);
                }
            }
            else
            {
                log.LogError($"{name} Input file is not a valid JSON or ndjson document");
                return;
            }


            try
            {
                log.LogInformation("Calling FHIR API now...");
                AuthenticationResult authenticationResult;


                int maxDegreeOfParallelism;
                if (!int.TryParse(System.Environment.GetEnvironmentVariable("MaxDegreeOfParallelism"), out maxDegreeOfParallelism))
                {
                    maxDegreeOfParallelism = 16;
                }

                try
                {
                    IConfidentialClientApplication app;
                    app = ConfidentialClientApplicationBuilder.Create(clientId)
                                                              .WithClientSecret(clientSecret)
                                                              .WithAuthority(new Uri(authority))
                    .Build();
                    authenticationResult = await app.AcquireTokenForClient(new List<string> { $"{audience}/.default" }).ExecuteAsync();

                }
                catch (Exception ee)
                {
                    log.LogCritical(string.Format("Unable to obtain token to access FHIR server in FhirImportService {0}", ee.ToString()));
                    throw;
                }

                //var entriesNum = Enumerable.Range(0,entries.Count-1);
                var actionBlock = new ActionBlock<int>(async i =>
                {
                    string resource_type = "";
                    string id = "";
                    string entry_json = "";

                    if (name.EndsWith(".json"))
                    {
                        entry_json = ((JObject)entries[i])["resource"].ToString();
                        if (string.IsNullOrEmpty(entry_json))
                        {
                            log.LogError("No 'resource' section found in JSON document");
                            throw new FhirImportException("'resource' not found or empty");
                        }

                        resource_type = (string)((JObject)entries[i])["resource"]["resourceType"];
                        id = (string)((JObject)entries[i])["resource"]["id"];
                    }

                    if (name.EndsWith(".ndjson"))
                    {
                        entry_json = ((JObject)entries[i]).ToString();
                        if (string.IsNullOrEmpty(entry_json))
                        {
                            log.LogError("No 'resource' section found in JSON document");
                            throw new FhirImportException("'resource' not found or empty");
                        }

                        resource_type = (string)((JObject)entries[i])["resourceType"];
                        id = (string)((JObject)entries[i])["id"];
                    }
                    var randomGenerator = new Random();

                    Thread.Sleep(TimeSpan.FromMilliseconds(randomGenerator.Next(50)));

                    if (string.IsNullOrEmpty(resource_type))
                    {
                        log.LogError("No resource_type found.");
                        throw new FhirImportException("No resource_type in resource.");
                    }

                    StringContent content = new StringContent(entry_json, Encoding.UTF8, "application/json");
                    var pollyDelays =
                            new[]
                            {
                                TimeSpan.FromMilliseconds(2000 + randomGenerator.Next(50)),
                                TimeSpan.FromMilliseconds(3000 + randomGenerator.Next(50)),
                                TimeSpan.FromMilliseconds(5000 + randomGenerator.Next(50)),
                                TimeSpan.FromMilliseconds(8000 + randomGenerator.Next(50))
                            };


                    HttpResponseMessage uploadResult = await Policy
                        .HandleResult<HttpResponseMessage>(response => !response.IsSuccessStatusCode)
                        .WaitAndRetryAsync(pollyDelays, (result, timeSpan, retryCount, context) =>
                        {
                            log.LogWarning($"Request failed with {result.Result.StatusCode}. Waiting {timeSpan} before next retry. Retry attempt {retryCount}");
                        })
                        .ExecuteAsync(() => {
                            var message = string.IsNullOrEmpty(id)
                                ? new HttpRequestMessage(HttpMethod.Post, new Uri(fhirServerUrl, $"/{resource_type}"))
                                : new HttpRequestMessage(HttpMethod.Put, new Uri(fhirServerUrl, $"/{resource_type}/{id}"));

                            message.Content = content;
                            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authenticationResult.AccessToken);
                            return httpClient.SendAsync(message);
                        });

                    if (!uploadResult.IsSuccessStatusCode)
                    {
                        string resultContent = await uploadResult.Content.ReadAsStringAsync();
                        log.LogError(resultContent);

                        // Throwing a generic exception here. This will leave the blob in storage and retry.
                        throw new Exception($"Unable to upload to server. Error code {uploadResult.StatusCode}");
                    }
                    else
                    {
                        log.LogInformation($"Uploaded /{resource_type}/{id}");
                    }
                },
                    new ExecutionDataflowBlockOptions
                    {
                        MaxDegreeOfParallelism = maxDegreeOfParallelism
                    }
                );

                for (var i = 0; i < entries.Count; i++)
                {
                    actionBlock.Post(i);
                }
                actionBlock.Complete();
                actionBlock.Completion.Wait();

                // We are done with this blob, upload was successful, it will be delete
                await GetBlobReference("medical-texts-fhir", name, log).DeleteIfExistsAsync();
            }
            catch (FhirImportException)
            {
                await MoveBlobToRejected(name, log);
            }
        }

        private static BlobClient GetBlobReference(string containerName, string blobName, ILogger log)
        {
            var connectionString = System.Environment.GetEnvironmentVariable("AzureWebStorageForData");
            try
            {
                BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
                BlobContainerClient blobContainerClient;
                blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
                BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);
                return blobClient;
            }
            catch
            {
                log.LogCritical("Unable to get blob reference. Check stroage connection string.");
                return null;
            }

        }

        private static async Task MoveBlobToRejected(string name, ILogger log)
        {
            //https://docs.microsoft.com/en-us/learn/modules/copy-blobs-from-command-line-and-code/7-move-blobs-using-net-storage-client

            BlobClient srcBlob = GetBlobReference("medical-texts-fhir", name, log);
            BlobClient destBlob = GetBlobReference("medical-texts-fhir-rejected", name, log);

            await destBlob.UploadAsync(srcBlob.Name);
            await srcBlob.DeleteAsync();
        }
    }
}
