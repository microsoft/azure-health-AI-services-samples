namespace TextAnalyticsHealthcareAdaptiveClient.TextAnalyticsApiSchema;
public class AnalysisTask
{
    public string Kind { get; set; }
    public DateTime LastUpdateDateTime { get; set; }
    public string Status { get; set; }
    public HealthcareResults Results { get; set; }
}
