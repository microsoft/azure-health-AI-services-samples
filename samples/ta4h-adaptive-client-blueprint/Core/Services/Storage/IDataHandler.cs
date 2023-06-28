using TextAnalyticsHealthcareAdaptiveClient.TextAnalyticsApiSchema;

public interface IDataHandler
{
    Task<List<Ta4hInputPayload>> LoadNextBatchOfPayloadsAsync();
    Task StoreSuccessfulJobResultsAsync(Ta4hInputPayload payload, HealthcareResults results);
    Task UpdateProcessingJobAsync(Ta4hInputPayload payload, string jobId);
}