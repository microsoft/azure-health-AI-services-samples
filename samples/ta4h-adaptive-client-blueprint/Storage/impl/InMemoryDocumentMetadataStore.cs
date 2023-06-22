public class InMemoryDocumentMetadataStore : IDocumentMetadataStore
{
    private readonly Dictionary<string, DocumentMetadata> store = new();

    public Task InitializeAsync()
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
}
