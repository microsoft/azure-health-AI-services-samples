namespace TextAnalyticsHealthcareAdaptiveClient.TextAnalyticsApiSchema;

public class TextAnlyticsJobResponse
{
    public string JobId { get; set; }
    public DateTime LastUpdatedDateTime { get; set; }
    public DateTime CreatedDateTime { get; set; }
    public DateTime ExpirationDateTime { get; set; }
    public string Status { get; set; }
    public List<object> Errors { get; set; }
    public AnalysisTasksSection Tasks { get; set; }
}
