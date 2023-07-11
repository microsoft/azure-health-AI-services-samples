﻿using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly.Extensions.Http;
using Polly.Retry;
using Polly;

public static class SeviceCollectionExtensions
{

    const string FileSystem = "FileSystem";
    const string AzureBlob = "AzureBlob";
    const string Noop = "Noop";
    static string[] FileStorageValidTypes = new[] { FileSystem, AzureBlob, Noop };
    const string InMemory = "InMemory";
    const string SQL = "SQL";
    const string AzureTable = "AzureTable";
    static string[] MetadataStorageValidTypes = new[] { InMemory, SQL};

    public static IServiceCollection AddFileStorage(this IServiceCollection services, IConfiguration configuration)
    {

        IFileStorage getFileStorage(string configSection)
        {
            var storageType = configuration[$"{configSection}:StorageType"];
            if (storageType == FileSystem)
            {
                var settingsSection = $"{configSection}:FileSystemSettings";
                var section = configuration.GetSection(settingsSection) ?? throw new ConfigurationException(settingsSection, null);
                var fileSystemStorageSettings = section.Get<FileSystemStorageSettings>();
                return new FileSystemStorage(fileSystemStorageSettings.BasePath);
            }
            else if (storageType == AzureBlob)
            {
                var settingsSection = $"{configSection}:AzureBlobSettings";
                var section = configuration.GetSection(settingsSection) ?? throw new ConfigurationException(settingsSection, null);
                var azureBlobStorageSettings = section.Get<AzureBlobStorageSettings>();
                return new AzureBlobStorage(azureBlobStorageSettings.ConnectionString, azureBlobStorageSettings.AuthenticationMethod, azureBlobStorageSettings.ContainerName);
            }
            else if (storageType == Noop)
            {
                return new NoopStorage();
            }
            else
            {
                throw new ConfigurationException($"{configSection}:StorageType", storageType, FileStorageValidTypes);
            }
        }

        IFileStorage inputFileStorage = getFileStorage("InputStorage");
        IFileStorage outputFileStorage = getFileStorage("OutputStorage");

        services.AddSingleton(new FileStorageManager
        {
            InputStorage = inputFileStorage,
            OutputStorage = outputFileStorage
        });
        return services;
    }

    public static IServiceCollection AddMetadataStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var configKey = "MetadataStorage:StorageType";
        var metadataStorageType = configuration[configKey];
        if (metadataStorageType == InMemory)
        {
            services.AddSingleton<IDocumentMetadataStore>(new InMemoryDocumentMetadataStore());
        }
        else if (metadataStorageType == SQL)
        {
            var settingsSection = "MetadataStorage:SQLSettings";
            var section = configuration.GetSection(settingsSection) ?? throw new ConfigurationException(settingsSection, null);
            var dbSettings = section.Get<SQLMetadataStorageSettings>();
            services.AddSingleton<IDocumentMetadataStore>(new SqlDocumentMetadataStore(dbSettings));

        }
        else if (metadataStorageType == AzureTable)
        {
            var settingsSection = "MetadataStorage:AzureTableSettings";
            var section = configuration.GetSection(settingsSection) ?? throw new ConfigurationException(settingsSection, null);
            var settings = section.Get<AzureTableMetadataStorageSettings>();
            services.AddSingleton<IDocumentMetadataStore>(new AzureTableDocumentMetadataStore(settings));
        }
          

        else
        {
            throw new ConfigurationException(configKey, metadataStorageType, MetadataStorageValidTypes);
        }    
        return services;
    }

    public static ILoggingBuilder AddApplicationInsightsLogging(this ILoggingBuilder logging, IConfiguration configuraiton)
    {
        var instrumentationKey = configuraiton["ApplicationInsights:InstrumentationKey"];
        if (instrumentationKey != null)
        {
            logging.AddApplicationInsights(instrumentationKey);
            logging.AddFilter<Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider>
                             ("", LogLevel.Debug);
            logging.Services.AddDependencyTracking(configuraiton);
        }
        return logging;
    }

    public static void AddDependencyTracking(this IServiceCollection services, IConfiguration configuration)
    {
        // Application Insights Dependency Tracking
        var telemetryConfiguration = new TelemetryConfiguration
        {
            InstrumentationKey = configuration["ApplicationInsights:InstrumentationKey"]
        };
        var dependencyTrackingModule = new DependencyTrackingTelemetryModule();
        dependencyTrackingModule.Initialize(telemetryConfiguration);
        services.AddSingleton<TelemetryConfiguration>(telemetryConfiguration);
        services.AddSingleton<DependencyTrackingTelemetryModule>(dependencyTrackingModule);
    }

    public static IServiceCollection AddHttpClientWithPolicies(this IServiceCollection services)
    {
        services.AddHttpClient<TextAnalytics4HealthClient>()
        .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30))
        .SetHandlerLifetime(TimeSpan.FromHours(1))
        .AddPolicyHandler(GetRetryPolicies());
        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicies()
    {
        var jitterer = new Random();

        AsyncRetryPolicy<HttpResponseMessage> transientErrorspolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(6, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2,
                                                                        retryAttempt)));
        AsyncRetryPolicy<HttpResponseMessage> tooManyRequestsPolicy = Policy
        .HandleResult<HttpResponseMessage>(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(5, retryAttempt => TimeSpan.FromSeconds(jitterer.Next(15, 60) + 60 * (retryAttempt - 1))); // Longer initial wait time for 429s.

        AsyncRetryPolicy<HttpResponseMessage> timeoutPolicy = Policy<HttpResponseMessage>
            .Handle<TaskCanceledException>() // Handles timeouts, which are represented by TaskCanceledException - this seems to cause a deadlock
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));


        // Combine the policies
        var retryPolicy = Policy.WrapAsync(tooManyRequestsPolicy, transientErrorspolicy);
        return transientErrorspolicy;

    }
}
