public class DocumentMetadata
{
    public string DocumentId { get; set; }

    public string BlobStorageUrl { get; set; }

    public ProcessingStatus Status { get; set; }

    public DateTime LastModified { get; set; }

    public string ProcessingResultLocation { get; set; }

    // ... any other metadata fields you need
}
