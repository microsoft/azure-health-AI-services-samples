public class Ta4hInputPayload
{

    public Ta4hInputPayload()
    {
        Documents = new List<DocumentInput>();
        DocumentsMetadata = new List<DocumentMetadata>();
    }

    public List<DocumentInput> Documents { get; set; }

    public List<DocumentMetadata> DocumentsMetadata { get; set; }
    
    public int TotalCharLength => Documents.Sum(d => d.Text.Length);

}

