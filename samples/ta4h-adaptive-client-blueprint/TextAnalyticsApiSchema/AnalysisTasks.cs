namespace TextAnalyticsHealthcareAdaptiveClient.TextAnalyticsApiSchema;
public class AnalysisTasksSection
{
    public int Completed { get; set; }
    public int Failed { get; set; }
    public int InProgress { get; set; }
    public int Total { get; set; }
    public List<AnalysisTask> Items { get; set; }
}
