public class InMemoryDocumentMetadataStore : IDocumentMetadataStore
{
    private readonly Dictionary<string, DocumentMetadata> store = new();
    private bool isInitialized = false;

    public Task CreateIfNotExistAsync()
    {
        // Nothing to do for in-memory store.
        return Task.CompletedTask;
    }

    public Task AddEntriesAsync(IEnumerable<DocumentMetadata> entries)
    {
        foreach (var entry in entries)
        {
            store[entry.DocumentId] = entry;
        }

        return Task.CompletedTask;
    }

    public Task<DocumentMetadata> GetDocumentMetadataAsync(string documentId)
    {
        if (store.TryGetValue(documentId, out var documentMetadata))
        {
            return Task.FromResult(documentMetadata);
        }
        return Task.FromResult<DocumentMetadata>(null);
    }

    public Task<IEnumerable<DocumentMetadata>> GetNextDocumentsToProcessAsync(int count)
    {
        var nextDocuments = store.Values
            .Where(e => e.Status == ProcessingStatus.NotStarted)
            .Take(count)
            .ToList();

        foreach (var doc in nextDocuments)
        {
            doc.Status = ProcessingStatus.Processing;
        }

        return Task.FromResult((IEnumerable<DocumentMetadata>)nextDocuments);
    }

    public Task UpdateEntryAsync(DocumentMetadata entry)
    {
        if (!store.ContainsKey(entry.DocumentId))
        {
            throw new KeyNotFoundException($"Document {entry.DocumentId} not found in the store.");
        }

        store[entry.DocumentId] = entry;
        return Task.CompletedTask;
    }

    public Task UpdateEntriesAsync(IEnumerable<DocumentMetadata> entries)
    {
        foreach (var entry in entries)
        {
            if (!store.ContainsKey(entry.DocumentId))
            {
                throw new KeyNotFoundException($"Document {entry.DocumentId} not found in the store.");
            }

            store[entry.DocumentId] = entry;
        }

        return Task.CompletedTask;
    }

    public Task MarkAsInitializedAsync()
    {
        isInitialized = true;
        return Task.CompletedTask;
    }

    public Task<bool> IsInitializedAsync()
    {
        return Task.FromResult(isInitialized);
    }
}
