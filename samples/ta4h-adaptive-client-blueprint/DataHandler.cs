using Azure.AI.TextAnalytics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TextAnalyticsHealthcareAdaptiveClient.TextAnalyticsApiSchema;

public class DataHandler : IDataHandler
{
    private readonly IFileStorage _fileStorage;

    private Ta4hOptions _options;
    private DataProcessingOptions _dataProcessingOptions;
    private readonly ILogger _logger;
    private readonly IDocumentMetadataStore _metadataStore;
    private bool initializationComplete = false;

    public DataHandler(ILogger<DataHandler> logger, FileStorageManager fileStorageManager, IDocumentMetadataStore metadataStore, IOptions<Ta4hOptions> options, IOptions<DataProcessingOptions> dataProcessingOptions)
    {
        _fileStorage = fileStorageManager.InputStorage;
        _metadataStore = metadataStore;
        _options = options.Value;
        _dataProcessingOptions = dataProcessingOptions.Value;
        _logger = logger;
    }

    public async Task<List<Ta4hInputPayload>> LoadNextBatchOfPayloadsAsync()
    {
        await EnsureInitializedAsync();
        var docsMetadata = await _metadataStore.GetNextDocumentsToProcessAsync(_dataProcessingOptions.MaxBatchSize);
        _logger.LogInformation("start next batch - {count} docs for processing", docsMetadata.Count());
        var docs = await BatchProcessor.ProcessInBatchesAsync(
            docsMetadata, 10, ReadDocumentFromStorage);
        var zipped = docsMetadata.Zip(docs, (metadata, doc) =>  (metadata, doc ));
        _logger.LogInformation("{count} docs were read from storage", docs.Count());

        var payloads = ToTa4hInputPayloads(zipped);
        _logger.LogInformation("{count} payload were created", payloads.Count);
        return payloads;
    }


    private async Task EnsureInitializedAsync()
    {
        if (initializationComplete)
        {
            return;
        }
        await _metadataStore.CreateIfNotExistAsync();
        var isInitialized = await _metadataStore.IsInitializedAsync();
        if (!isInitialized)
        {
            var fileNames = await _fileStorage.EnumerateFilesRecursiveAsync();
            fileNames = fileNames.Where(fn => fn.EndsWith(".txt"));
            _logger.LogInformation("found {nfiles} files", fileNames.Count());
            var batch = new List<DocumentMetadata>();
            foreach (var filename in fileNames)
            {
                var entry = new DocumentMetadata
                {
                    DocumentId = string.Join("/", filename.Split(Path.DirectorySeparatorChar)).Replace(".txt", ""),
                    InputPath = filename,
                    LastModified = DateTime.UtcNow,
                    Status = ProcessingStatus.NotStarted
                };
                batch.Add(entry);
                if (batch.Count > 100)
                {
                    _logger.LogInformation("writing batch of documents metadata");
                    await _metadataStore.AddEntriesAsync(batch);
                    batch.Clear();
                }
            }
            if (batch.Any())
            {
                await _metadataStore.AddEntriesAsync(batch);
            }
        }
        await _metadataStore.MarkAsInitializedAsync();
        initializationComplete = true;
    }


    private async Task<TextDocumentInput> ReadDocumentFromStorage(DocumentMetadata documentMetadata)
    {
        var text = await _fileStorage.ReadTextFileAsync(documentMetadata.InputPath);
        var docId = documentMetadata.DocumentId;
        _logger.LogDebug("Read document {docId}", docId);
        var language = _options.Language;
        return new TextDocumentInput(docId, text) { Language = language };
    }

    private List<Ta4hInputPayload> ToTa4hInputPayloads(IEnumerable<(DocumentMetadata metadata, TextDocumentInput doc>)
    {
        int maxCharactersPerRequest = _options.MaxCharactersPerRequest;
        int maxDocsPerRequest = _options.MaxDocsPerRequest;
        var random = new Random();
        var result = new List<Ta4hInputPayload>();

        Ta4hInputPayload nextPayload = new()
        {
            Documents = new()
        };
        docs = docs.ToList();

        foreach (var doc in docs)
        {
            if (nextPayload.Documents.Count == maxDocsPerRequest || nextPayload.TotalCharLength + doc.Text.Length >= maxCharactersPerRequest)
            {
                result.Add(nextPayload);
                nextPayload = new()
                {
                    Documents = new()
                };
                if (_options.RandomizeRequestSize)
                {
                    maxDocsPerRequest = random.Next(1, _options.MaxDocsPerRequest);
                }
            }
            else
            {
                nextPayload.Documents.Add(doc);
            }
        }
        if (nextPayload.Documents.Any())
        {
            result.Add(nextPayload);
        }
        return result;
    }
}