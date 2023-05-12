using Newtonsoft.Json;
using StressTestConsoleClient.models;
using System;
using System.Net.Http;
using System.Reflection.Metadata;

namespace StressTestConsoleClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var endpoint = "https://berttesttafh.azurewebsites.net/api/RuntTA4HWorkloadFunction?code=cB-oGhSrLlixfhow8wYIDXqIWpvV3c_sLuclJdeWZEXLAzFu2maqBg==";
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
            Console.WriteLine("Starting stress test");
            for (int i = 0; i < numberOfIterations; i++)
            {
                var documents = new List<DocumentPayload>();
                for (int n = 0; n < numberOfDocumentsPerRequest; n++)
                {
                    documents.Add(new DocumentPayload { Id = $"{Guid.NewGuid()}", Text = testDocuments[rnd.Next(1, 11)] });
                }
                tasks.Add(SendPostRequest(client, endpoint, JsonConvert.SerializeObject(documents)));
                Console.WriteLine($"Number of documents send: {i +1}");
            }
            await Task.WhenAll(tasks);
            Console.WriteLine("All requests completed.");
        }

        static async Task SendPostRequest(HttpClient client, string endpoint, string payload)
        {
            using (var cts = new CancellationTokenSource(new TimeSpan(0, 5, 0)))
            {
                var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
                var response = await client.PostAsync(endpoint, content);
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseContent);
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
