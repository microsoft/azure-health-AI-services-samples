using Azure.AI.TextAnalytics;

namespace TextAnalyticsHealthcareAdaptiveClient.TextAnalyticsApiSchema;
public class Ta4hInputPayload
{

    public Ta4hInputPayload()
    {
        Documents = new List<TextDocumentInput>();
    }

    public List<TextDocumentInput> Documents { get; set; }

    public string JobId { get; set; }

    public int TotalCharLength => Documents.Sum(d => d.Text.Length);
}
