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

namespace TextAnalyticsForHealthAsync_Client
{
    //https://learn.microsoft.com/en-us/azure/cognitive-services/language-service/concepts/data-limits
    public static class RuntTA4HWorkloadFunction
    {
        private static readonly string TextAnalyticsEndPoint = Environment.GetEnvironmentVariable("TextAnalyticsEndPoint");
        private static readonly string TextAnalyticsSubscriptionKey = Environment.GetEnvironmentVariable("TextAnalyticsSubscriptionKey");
        private static readonly string OutputStorageConnectionString = Environment.GetEnvironmentVariable("OutputStorageConnectionString"); 
        [FunctionName("RuntTA4HWorkloadFunction")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            //Read Document
            string inputString = await new StreamReader(req.Body).ReadToEndAsync();
            var documentInfoList = JsonConvert.DeserializeObject<List<TextDocumentInput>>(inputString);
            if(documentInfoList.Count == 0 || !documentInfoList.Where(p => !string.IsNullOrWhiteSpace(p.Text)).Any())
            {
                return new BadRequestErrorMessageResult("No documents found, please provide minimal 1 document string");
            }

            if(documentInfoList.Count > 25)
            {
                return new BadRequestErrorMessageResult("Batch request contains too many records. Max 25 records are permitted");
            }

            if(documentInfoList.SelectMany(p => p.Text).Count() > 125000)
            {
                return new BadRequestErrorMessageResult("Batch request contains too many records. Max 125 000 characters are allowed");
            }

            //Process documents through TA4H
            var documents = documentInfoList.Where(p => !string.IsNullOrWhiteSpace(p.Text));
            var client = new TextAnalyticsClient(new Uri(TextAnalyticsEndPoint), new AzureKeyCredential(TextAnalyticsSubscriptionKey));
            AnalyzeHealthcareEntitiesOperation healthOperation = await client.StartAnalyzeHealthcareEntitiesAsync(documents);
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
                        Text = documentInfoList.First(p => p.Id == analyzedResult.Id).Text,
                        HealthcareEntitiesResult = analyzedResult.Entities
                    };
                    var blobName = new string($"{analyzedResult.Id}_{DateTime.Now.ToString("yyyyMMddHHmmss")}".ToLower().Take(250).ToArray());
                    BlobClient blobClient = containerClient.GetBlobClient(blobName);
                    var content = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result));
                    using (var ms = new MemoryStream(content))
                    {
                        await blobClient.UploadAsync(ms);
                    }
                }
            }

            return new OkResult();
        }
    }
}
