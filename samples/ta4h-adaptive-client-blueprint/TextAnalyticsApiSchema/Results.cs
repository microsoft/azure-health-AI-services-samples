namespace TextAnalyticsHealthcareAdaptiveClient.TextAnalyticsApiSchema;
public class HealthcareResults
{
    public List<HealthDocumentResult> Documents { get; set; }
    public List<HealthDocumentErrors> Errors { get; set; }
    public string ModelVersion { get; set; }
}
