using Azure.AI.TextAnalytics;
using Azure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.Http;
using System.Collections.Generic;
using Newtonsoft.Json;
using Azure.Storage.Blobs;
using TextAnalyticsForHealthAsync_Client.Models;
using System.Linq;
using System.ComponentModel;
using System.Text;
using System.Reflection.Metadata;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Xml.Linq;

namespace TextAnalyticsForHealthAsync_Client
{
    public static class RuntTA4HWorkloadFunction
    {
        private static readonly string TextAnalyticsEndPoint = Environment.GetEnvironmentVariable("TextAnalyticsEndPoint");
        private static string TextAnalyticsSubscriptionKey = Environment.GetEnvironmentVariable("TextAnalyticsSubscriptionKey");
        private static readonly string OutputStorageConnectionString = Environment.GetEnvironmentVariable("OutputStorageConnectionString"); 
        [FunctionName("RuntTA4HWorkloadFunction")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
                 [DurableClient] IDurableOrchestrationClient starter, ILogger log)
        {
            //Read Document
            string inputString = await new StreamReader(req.Body).ReadToEndAsync();
            var documentInfoList = JsonConvert.DeserializeObject<List<TextDocumentInput>>(inputString);
            if(documentInfoList.Count == 0 || !documentInfoList.Where(p => !string.IsNullOrWhiteSpace(p.Text)).Any())
            {
                return new BadRequestErrorMessageResult("No documents found, please provide minimal 1 document string");
            }
            var documents = documentInfoList.Where(p => !string.IsNullOrWhiteSpace(p.Text));
            string instanceId = await starter.StartNewAsync("ProcessDocumentFunction", documents);
            log.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("ProcessDocumentFunction")]
        public static async Task RunOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var documents = context.GetInput<List<TextDocumentInput>>();
            foreach (var document in documents)
            {
                await context.CallActivityAsync(nameof(ProcessDocument), document);
            }
        }

        [FunctionName(nameof(ProcessDocument))]
        public static async Task ProcessDocument([ActivityTrigger] TextDocumentInput document, ILogger log)
        {
            var client = new TextAnalyticsClient(new Uri(TextAnalyticsEndPoint), new AzureKeyCredential(TextAnalyticsSubscriptionKey));
            AnalyzeHealthcareEntitiesOperation healthOperation = await client.StartAnalyzeHealthcareEntitiesAsync(new List<TextDocumentInput> { document });
            await healthOperation.WaitForCompletionAsync();

            var blobServiceClient = new BlobServiceClient(OutputStorageConnectionString);
            string containerName = "healthcareentitiesresults";
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            if (!containerClient.Exists())
            {
                containerClient.Create();
            }
            await foreach (AnalyzeHealthcareEntitiesResultCollection resultCollection in healthOperation.Value)
            {
                foreach (AnalyzeHealthcareEntitiesResult analyzedResult in resultCollection)
                {
                    var result = new DocumentResult
                    {
                        Id = analyzedResult.Id,
                        Text = document.Text,
                        HealthcareEntitiesResult = analyzedResult.Entities
                    };
                    var blobName = new string($"{document.Id}_{DateTime.Now.ToString("yyyyMMddHHmmss")}.json".ToLower().Take(250).ToArray());
                    BlobClient blobClient = containerClient.GetBlobClient(blobName);
                    var content = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result));
                    using (var ms = new MemoryStream(content))
                    {
                        await blobClient.UploadAsync(ms);
                    }
                }
            }
        }
    }
}
