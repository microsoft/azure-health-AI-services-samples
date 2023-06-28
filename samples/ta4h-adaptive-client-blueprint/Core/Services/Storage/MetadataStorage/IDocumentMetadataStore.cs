public interface IDocumentMetadataStore
{
    /// <summary>
    /// Initialize the data store if it doesn't exist.
    /// </summary>
    Task CreateIfNotExistAsync();

    Task<DocumentMetadata> GetDocumentMetadataAsync(string documentId);

    Task MarkAsInitializedAsync();

    /// <summary>
    /// Add a batch of entries to the data store.
    /// </summary>
    Task AddEntriesAsync(IEnumerable<DocumentMetadata> entries);

    /// <summary>
    /// Get the next batch of documents to process and mark them as 'Processing'.
    /// </summary>
    Task<IEnumerable<DocumentMetadata>> GetNextDocumentsForProcessAsync(int count);

    /// <summary>
    /// Update a single entry in the data store.
    /// </summary>
    Task UpdateEntryAsync(DocumentMetadata entry);


    Task<bool> IsInitializedAsync();
    Task UpdateEntriesStatusAsync(List<DocumentMetadata> entries, ProcessingStatus newStatus, string jobId = null);
}
