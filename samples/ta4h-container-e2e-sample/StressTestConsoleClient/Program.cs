using Azure;
using Azure.AI.TextAnalytics;
using Newtonsoft.Json;
using System;
using System.Diagnostics;

namespace StressTestConsoleClient
{
    class Program
    {
        private static readonly string TextAnalyticsEndPoint = "http://0.0.0.0";
        //The subscription key is set in the container in the cluster, but the SDK requires a key, adding dummy key.
        private static string TextAnalyticsSubscriptionKey = "123";
        static async Task Main(string[] args)
        {
            var stopwatch = new Stopwatch();
            Console.WriteLine("How many iterations do you want send to the endpoint?");
            var numberOfIterations = 100;
            int.TryParse(Console.ReadLine(), out numberOfIterations);
            Console.WriteLine("How many documents per request?");
            var numberOfDocumentsPerRequest = 25;
            int.TryParse(Console.ReadLine(), out numberOfDocumentsPerRequest);
            var tasks = new List<Task>();
            var testDocuments = GetAllTestDocuments();
            Random rnd = new();
            stopwatch.Start();
            Console.WriteLine($"[Elapsed Milliseconds: {stopwatch.ElapsedMilliseconds}] Starting stress test");
            var client = new TextAnalyticsClient(new Uri(TextAnalyticsEndPoint), new AzureKeyCredential(TextAnalyticsSubscriptionKey));
            for (int i = 0; i < numberOfIterations; i++)
            {
                var documents = new List<TextDocumentInput>();
                for (int n = 0; n < numberOfDocumentsPerRequest; n++)
                {
                    documents.Add(new TextDocumentInput( $"{Guid.NewGuid()}", testDocuments[rnd.Next(1, 11)] ));
                }
                tasks.Add(StartAnalyzeHealthcareEntities(i +1,client, documents));
                await Task.Delay(20);
                Console.WriteLine($"[Elapsed Milliseconds: {stopwatch.ElapsedMilliseconds}] Request number {i +1}");
            }
            Console.WriteLine($"Checking if all requests were accepted");
            Console.WriteLine($"--------------------------------------");

            await Task.WhenAll(tasks);
            Console.WriteLine($"All Done!!");
        }


        static async Task StartAnalyzeHealthcareEntities(int index, TextAnalyticsClient client, List<TextDocumentInput> documents)
        {
            var result = await client.StartAnalyzeHealthcareEntitiesAsync(documents);
            var i = 0;
            while(true) {
                var WaitTime = TimeSpan.FromSeconds(Math.Pow(2, i));
                i++;
                if (string.IsNullOrWhiteSpace(result.Status.ToString()))
                {
                    Console.WriteLine($"({index}) [RETRY] no status, retrying in {WaitTime} seconds");
                    await result.UpdateStatusAsync();
                    await Task.Delay(WaitTime);
                }
                else if(result.Status == TextAnalyticsOperationStatus.Failed || result.Status == TextAnalyticsOperationStatus.Rejected)
                {
                    Console.WriteLine($"({index}) [FAILED] {result.Status}, retrying in {WaitTime} seconds");
                    result = await client.StartAnalyzeHealthcareEntitiesAsync(documents);
                    await Task.Delay(WaitTime);
                }
                else
                {
                    Console.WriteLine($"({index}) [SUCCESS] {result.Status}");
                    break;
                }
            }
        }

     

        static List<string> GetAllTestDocuments()
        {
            var txtFiles = Directory.EnumerateFiles($"{Environment.CurrentDirectory}\\test-data", "*.txt");
            var documents = new List<string>();
            foreach (string currentFile in txtFiles)
            {
                documents.Add(File.ReadAllText(currentFile));
            }
            return documents;
        }
    }
}
