public class DocumentMetadata
{
    public string DocumentId { get; set; }

    public string InputPath { get; set; }

    public ProcessingStatus Status { get; set; }

    public DateTime LastModified { get; set; }

    public string ResultsPath { get; set; }

    public string JobId { get; set; }

}
