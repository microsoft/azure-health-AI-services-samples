using TextAnalyticsHealthcareAdaptiveClient.TextAnalyticsApiSchema;

public record QueueItem(Ta4hInputPayload Payload, int InputSize, DateTime CreatedDateTime, DateTime NextCheckDateTime, DateTime LastCheckedDateTime)
{

}

