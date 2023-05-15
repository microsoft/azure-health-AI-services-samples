using Newtonsoft.Json;
using StressTestConsoleClient.models;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection.Metadata;

namespace StressTestConsoleClient
{
    class Program
    {
        private static List<DocumentStatus> _documentStatuses = new List<DocumentStatus>();

        static async Task Main(string[] args)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var endpoint = "http://localhost:7090/api/RuntTA4HWorkloadFunction";
            Console.WriteLine("How many iterations do you want send to the endpoint?");
            var numberOfIterations = 100;
            int.TryParse(Console.ReadLine(), out numberOfIterations);
            Console.WriteLine("How many documents per request?");
            var numberOfDocumentsPerRequest = 25;
            int.TryParse(Console.ReadLine(), out numberOfDocumentsPerRequest);
            var tasks = new List<Task>();
            var client = new HttpClient();
            client.Timeout = new TimeSpan(0,5,0);
            var testDocuments = GetAllTestDocuments();
            Random rnd = new();
            Console.WriteLine($"[Elapsed Milliseconds: {stopwatch.ElapsedMilliseconds}] Starting stress test");
            for (int i = 0; i < numberOfIterations; i++)
            {
                var documents = new List<DocumentPayload>();
                for (int n = 0; n < numberOfDocumentsPerRequest; n++)
                {
                    documents.Add(new DocumentPayload { Id = $"{Guid.NewGuid()}", Text = testDocuments[rnd.Next(1, 11)] });
                }
                tasks.Add(SendPostRequest(client, endpoint, JsonConvert.SerializeObject(documents)));
                Console.WriteLine($"[Elapsed Milliseconds: {stopwatch.ElapsedMilliseconds}] Number of documents send: {i +1}");
            }
            await Task.WhenAll(tasks);
            Console.WriteLine($"[Elapsed Milliseconds: {stopwatch.ElapsedMilliseconds}] Start fetching statuses");
            await CheckResponseStatus(stopwatch);
        }

        static async Task CheckResponseStatus(Stopwatch stopwatch)
        {
            var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));
            while (await periodicTimer.WaitForNextTickAsync())
            {
                Console.WriteLine($"[Elapsed Milliseconds: {stopwatch.ElapsedMilliseconds}] Successfully processed {_documentStatuses.Count(p => p.Finished)} documents, checking status of {_documentStatuses.Count(p => !p.Finished)} documents");
                using var client = new HttpClient();
                foreach (var item in _documentStatuses.Where(p => !p.Finished))
                {
                    using (var cts = new CancellationTokenSource(new TimeSpan(0, 5, 0)))
                    {
                        var response = await client.GetAsync(item.StatusQueryGetUri);
                        var responseContent = await response.Content.ReadAsStringAsync();
                        var statusInfo = JsonConvert.DeserializeObject<DurableAzureFunctionStatus>(responseContent);
                        if(statusInfo.RuntimeStatus == RuntimeStatus.Completed)
                        {
                            item.Finished = true;

                        }
                        else if (statusInfo.RuntimeStatus == RuntimeStatus.Failed ||
                                  statusInfo.RuntimeStatus == RuntimeStatus.Suspended || 
                                  statusInfo.RuntimeStatus == RuntimeStatus.Terminated)
                        {
                            item.Error = true;
                            item.ErrorMessage = statusInfo.Output;
                            Console.WriteLine($"[Elapsed Milliseconds: {stopwatch.ElapsedMilliseconds}] Error for document with id {statusInfo.FunctionInput.First().Id}. ErrorMessage: {statusInfo.Output}");
                        }
                    }
                }

                Console.WriteLine($"[Elapsed Milliseconds: {stopwatch.ElapsedMilliseconds}] Successfully processed {_documentStatuses.Count(p => p.Finished)} documents, checking status of {_documentStatuses.Count(p => !p.Finished)} documents");
                if ((_documentStatuses.Count(p => p.Finished) + _documentStatuses.Count(p => p.Error)) == _documentStatuses.Count)
                {
                    periodicTimer.Dispose();
                }
            }
        }
        static async Task SendPostRequest(HttpClient client, string endpoint, string payload)
        {
            using (var cts = new CancellationTokenSource(new TimeSpan(0, 5, 0)))
            {
                var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
                var response = await client.PostAsync(endpoint, content);
                var responseContent = await response.Content.ReadAsStringAsync();
                _documentStatuses.Add(JsonConvert.DeserializeObject<DocumentStatus>(responseContent));
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
