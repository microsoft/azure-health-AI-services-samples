using Azure;
using Azure.AI.TextAnalytics;
using System.Diagnostics;

namespace StressTestConsoleClient
{
    class Program
    {
        private static readonly string TextAnalyticsEndPoint = "http://0.0.0.0";
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
            for (int i = 0; i < numberOfIterations; i++)
            {
                var documents = new List<TextDocumentInput>();
                for (int n = 0; n < numberOfDocumentsPerRequest; n++)
                {
                    documents.Add(new TextDocumentInput( $"{Guid.NewGuid()}", testDocuments[rnd.Next(1, 11)] ));
                }
                tasks.Add(StartAnalyzeHealthcareEntities(documents));
                Console.WriteLine($"[Elapsed Milliseconds: {stopwatch.ElapsedMilliseconds}] Number of request send: {i +1}");
            }
            await Task.WhenAll(tasks);
            Console.WriteLine($"All Done");
        }


        static async Task StartAnalyzeHealthcareEntities(List<TextDocumentInput> documents)
        {
            var client = new TextAnalyticsClient(new Uri(TextAnalyticsEndPoint), new AzureKeyCredential(TextAnalyticsSubscriptionKey));
            var result = await client.StartAnalyzeHealthcareEntitiesAsync(documents);
            File.AppendAllText($"{System.Environment.CurrentDirectory}/output/SendDocuments.txt", $"{result.Id}{Environment.NewLine}");
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
