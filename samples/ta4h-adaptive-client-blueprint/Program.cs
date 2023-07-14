using Azure.AI.TextAnalytics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TextAnalyticsHealthcareAdaptiveClient.Core.Services;

class Program
{

    static ILogger Logger { get; set; }

    static async Task Main(string[] args)
    {
        using IHost host = CreateHostBuilder(args).Build();
        var runner = host.Services.GetRequiredService<HealthcareAnalysisRunner>();
        Logger = host.Services.GetRequiredService<ILogger<Program>>();
        var cts = new CancellationTokenSource();

        try
        {
            await runner.ExecuteAsync(cts.Token);
            Logger.LogInformation("Application completed successfully. exiting program in 10 seconds...");
            await Task.Delay(10000);
        }
        catch (Exception e)
        {
            cts.Cancel();
            Logger.LogError("Unhandled exception: {e}", e);
            Logger.LogError(e, "an unexpected exception occured. exiting program in 10 seconds...");
            await Task.Delay(10000);
            throw;
        }

    }


    static IHostBuilder CreateHostBuilder(string[] args)
    {
        var hostBuilder = Host.CreateDefaultBuilder(args);
        hostBuilder.ConfigureAppConfiguration((hostingContext, configuration) =>
        {
            // This adds support for JSON configuration files and environment variables
            configuration
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
        })
        .ConfigureServices((hostContext, services) =>
        {
            var configuraiton = hostContext.Configuration;
            // Bind the configuration section to your options class
            services
            .Configure<Ta4hOptions>(configuraiton.GetSection("Ta4hOptions"))
            .Configure<DataProcessingOptions>(configuraiton.GetSection("DataProcessingOptions"))
            .AddFileStorage(configuraiton)
            .AddMetadataStorage(configuraiton)
            .AddSingleton<IDataHandler, DataHandler>()
            .AddSingleton<TextAnalytics4HealthClient>()
            .AddSingleton<HealthcareAnalysisRunner>();

             //.AddHttpClientWithPolicies();

        }).ConfigureLogging((hostContext, logging) =>
        {
            var configuraiton = hostContext.Configuration;
            logging.AddConfiguration(configuraiton.GetSection("Logging"));
            logging.AddApplicationInsightsLogging(configuraiton);
        });
        return hostBuilder;
    }

}
