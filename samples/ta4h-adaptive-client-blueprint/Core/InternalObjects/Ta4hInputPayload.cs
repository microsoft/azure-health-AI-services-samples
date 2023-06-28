using Azure.AI.TextAnalytics;

public class Ta4hInputPayload
{

    public Ta4hInputPayload()
    {
        Documents = new List<TextDocumentInput>();
        DocumentsMetadata = new List<DocumentMetadata>();
    }

    public List<TextDocumentInput> Documents { get; set; }

    public List<DocumentMetadata> DocumentsMetadata { get; set; }
    
    public int TotalCharLength => Documents.Sum(d => d.Text.Length);


}
