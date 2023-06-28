namespace TextAnalyticsHealthcareAdaptiveClient.TextAnalyticsApiSchema;
public class HealthcareResults
{
    public List<HealthDocumentResult> Documents { get; set; }
    public List<HealthDocumentError> Errors { get; set; }
    public string ModelVersion { get; set; }
}
