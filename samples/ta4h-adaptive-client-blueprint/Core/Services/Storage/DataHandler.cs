using Azure.AI.TextAnalytics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TextAnalyticsHealthcareAdaptiveClient.TextAnalyticsApiSchema;

public class DataHandler : IDataHandler
{
    private readonly IFileStorage _inputFileStorage;
    private readonly IFileStorage _outputFileStorage;


    private Ta4hOptions _options;
    private DataProcessingOptions _dataProcessingOptions;
    private readonly ILogger _logger;
    private readonly IDocumentMetadataStore _metadataStore;
    private bool initializationComplete = false;

    public DataHandler(ILogger<DataHandler> logger, FileStorageManager fileStorageManager, IDocumentMetadataStore metadataStore, IOptions<Ta4hOptions> options, IOptions<DataProcessingOptions> dataProcessingOptions)
    {
        _inputFileStorage = fileStorageManager.InputStorage;
        _outputFileStorage = fileStorageManager.OutputStorage;
        _metadataStore = metadataStore;
        _options = options.Value;
        _dataProcessingOptions = dataProcessingOptions.Value;
        _logger = logger;
    }

    public async Task<List<Ta4hInputPayload>> LoadNextBatchOfPayloadsAsync()
    {
        await EnsureInitializedAsync();
        var docsMetadata = await _metadataStore.GetNextDocumentsForProcessAsync(_dataProcessingOptions.MaxBatchSize);
        _logger.LogInformation("start next batch - {count} docs for processing", docsMetadata.Count());
        var docs = new List<TextDocumentInput>();
        try
        {
            docs.AddRange(await BatchProcessor.ProcessInBatchesAsync(
                docsMetadata, 10, ReadDocumentFromStorage));
        }
        catch (AggregateException aex)
        {
            foreach (var docMetadata in docsMetadata)
            {
                try
                {
                    docs.Add(await ReadDocumentFromStorage(docMetadata));

                }
                catch (FileNotFoundException)
                {
                    _logger.LogError("file for documentId {documentId} was not found. will not be processed", docMetadata.DocumentId);
                    docMetadata.Status = ProcessingStatus.NotFound;
                    await _metadataStore.UpdateEntryAsync(docMetadata);
                }
            }
        }
        IEnumerable<(DocumentMetadata metadata, TextDocumentInput doc)> zipped = docsMetadata.Zip(docs, (metadata, doc) => (metadata, doc));
        _logger.LogInformation("{count} docs were read from storage", docs.Count());

        List<Ta4hInputPayload> payloads = ToTa4hInputPayloads(zipped);
        _logger.LogInformation("{count} payloads were created", payloads.Count);
        return payloads;
    }

    public async Task StoreSuccessfulJobResultsAsync(Ta4hInputPayload payload, HealthcareResults results)
    {
        try
        {
            var resultsStored = await BatchProcessor.ProcessInBatchesAsync(
                results.Documents, results.Documents.Count > 10 ? 10 : results.Documents.Count, async (doc) =>
                {
                    var docMetadata = payload.DocumentsMetadata.First(d => d.DocumentId == doc.Id);
                    await _outputFileStorage.SaveJsonFileAsync(doc, docMetadata.ResultsPath);
                    return docMetadata.ResultsPath;
                });
        }
        catch (AggregateException ae)
        {
            foreach (var ex in ae.InnerExceptions)
            {
                _logger.LogError("error in SaveJsonFileAsync: {ex}", ex.ToString());
            }
        }
        try
        {
            _logger.LogDebug("Updating docs metadata from job id {jobId}: doc ids = {docids}", payload.DocumentsMetadata.First().JobId, string.Join(" | ", payload.DocumentsMetadata.Select(m => m.DocumentId)));
            await _metadataStore.UpdateEntriesStatusAsync(payload.DocumentsMetadata, ProcessingStatus.Succeeded, null);
        }
        catch (Exception ex)
        {
            _logger.LogError("error in UpdateEntriesAsync: {ex}", ex.ToString());
        }
    }

    public async Task StoreFailedJobResultsAsync(Ta4hInputPayload payload)
    {            
        foreach (var docMetadata in payload.DocumentsMetadata)
        {
            try
            {
                docMetadata.Status = ProcessingStatus.Failed;
                await _metadataStore.UpdateEntryAsync(docMetadata);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to update results of failed document {docId}: {ex}", docMetadata.DocumentId, ex.ToString());
            }
        }
    }


    private async Task EnsureInitializedAsync()
    {
        if (initializationComplete)
        {
            return;
        }
        await _metadataStore.CreateIfNotExistAsync();
        var isInitialized = await _metadataStore.IsInitializedAsync();
        var maxDataSize = 587;
        if (!isInitialized)
        {
            var batch = new List<DocumentMetadata>();
            await foreach (var filename in _inputFileStorage.EnumerateFilesRecursiveAsync())
            {
                if (maxDataSize < 1)
                {
                    break;
                }

                if (filename.EndsWith(".txt"))
                {
                    var entry = new DocumentMetadata
                    {
                        DocumentId = string.Join("/", filename.Split(Path.DirectorySeparatorChar)).Replace(".txt", ""),
                        InputPath = filename,
                        LastModified = DateTime.UtcNow,
                        Status = ProcessingStatus.NotStarted,
                        ResultsPath = filename.Replace(".txt", ".result.json")
                    };
                    batch.Add(entry);
                    if (batch.Count >= 100)
                    {
                        _logger.LogInformation("writing batch of documents metadata");
                        await _metadataStore.AddEntriesAsync(batch);
                        batch.Clear();
                    }
                    maxDataSize--;
                }
            }
            if (batch.Any())
            {
                await _metadataStore.AddEntriesAsync(batch);
            }
            await _metadataStore.MarkAsInitializedAsync();
        }
        
        initializationComplete = true;
    }


    private async Task<TextDocumentInput> ReadDocumentFromStorage(DocumentMetadata documentMetadata)
    {
        var text = await _inputFileStorage.ReadTextFileAsync(documentMetadata.InputPath);
        var docId = documentMetadata.DocumentId;
        _logger.LogDebug("Read document {docId}", docId);
        var language = _options.Language;
        return new TextDocumentInput(docId, text) { Language = language };
    }

    private List<Ta4hInputPayload> ToTa4hInputPayloads(IEnumerable<(DocumentMetadata metadata, TextDocumentInput doc)> documents)
    {
        int maxCharactersPerRequest = _options.MaxCharactersPerRequest;
        int maxDocsPerRequest = _options.MaxDocsPerRequest;
        var random = new Random();
        var result = new List<Ta4hInputPayload>();

        Ta4hInputPayload nextPayload = new();

        foreach (var (metadata, doc) in documents)
        {
            if (nextPayload.Documents.Count == maxDocsPerRequest || nextPayload.TotalCharLength + doc.Text.Length >= maxCharactersPerRequest)
            {
                result.Add(nextPayload);
                nextPayload = new();
                nextPayload.Documents.Add(doc);
                nextPayload.DocumentsMetadata.Add(metadata);
                if (_options.RandomizeRequestSize && _options.MaxDocsPerRequest > 2)
                {
                    maxDocsPerRequest = random.Next(2, _options.MaxDocsPerRequest + 1);
                }
            }
            else
            {
                nextPayload.Documents.Add(doc);
                nextPayload.DocumentsMetadata.Add(metadata);
            }
        }
        if (nextPayload.Documents.Any())
        {
            result.Add(nextPayload);
        }
        _logger.LogInformation($"Completed batching, {documents.Count()}, {result.Sum(r => r.Documents.Count)}");
        return result;
    }

    public Task UpdateProcessingJobAsync(Ta4hInputPayload payload, string jobId)
    {
        return _metadataStore.UpdateEntriesStatusAsync(payload.DocumentsMetadata, ProcessingStatus.Processing, jobId);
    }
}