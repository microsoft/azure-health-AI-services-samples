using TextAnalyticsHealthcareAdaptiveClient.TextAnalyticsApiSchema;

public interface IDataHandler
{
    Task<List<Ta4hInputPayload>> LoadNextBatchOfPayloadsAsync();
}