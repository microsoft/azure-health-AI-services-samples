namespace TextAnalyticsHealthcareAdaptiveClient.TextAnalyticsApiSchema;

public class JobStatus
{
    public const string NotStarted = "notStarted";
    public const string Running = "running";
    public const string Cancelling = "cancelling";
    public const string Cancelled = "cancelled";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";


    public static bool IsTerminalStatus(string status) => status == JobStatus.Succeeded || status == JobStatus.Failed || status == JobStatus.Cancelled;
}
