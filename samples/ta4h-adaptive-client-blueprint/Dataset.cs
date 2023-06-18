using Azure.AI.TextAnalytics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections;
using TextAnalyticsHealthcareAdaptiveClient.TextAnalyticsApiSchema;

public class Dataset : IEnumerable<TextDocumentInput>
{
    private readonly IFileStorage _fileStorage;

    private Ta4hOptions _options;
    private DataProcessingOptions _dataProcessingOptions;
    private IEnumerator<Ta4hInputPayload> _enumerator;
    private bool isDone = false;
    private IEnumerable<TextDocumentInput> _inputData;
    private readonly ILogger _logger;

    public Dataset(ILogger<Dataset> logger, FileStorageManager fileStorageManager, IOptions<Ta4hOptions> options, IOptions<DataProcessingOptions> dataProcessingOptions)
    {
        _fileStorage = fileStorageManager.InputStorage;
        _options = options.Value;
        _dataProcessingOptions = dataProcessingOptions.Value;
        _logger = logger;
        _logger.LogInformation("Shuffle={Shuffle}, InitialQueueSize={InitialQueueSize}, RepeatTimes={RepeatTimes}", _dataProcessingOptions.Shuffle, _dataProcessingOptions.InitialQueueSize, _dataProcessingOptions.RepeatTimes);
        _logger.LogInformation("ModelVersion={ModelVersion}, RandomizeRequestSize={RandomizeRequestSize}, MaxDocsPerRequest={MaxDocsPerRequest}, MaxCharactersPerRequest={MaxCharactersPerRequest}", _options.ModelVersion, _options.RandomizeRequestSize, _options.MaxDocsPerRequest, _options.MaxCharactersPerRequest);
    }

    public async Task InitializeAsync()
    {
        List<string> fileNames = await GetListOfFilesForProcessingAsync();
        _inputData = await BatchProcessor.ProcessInBatchesAsync(
            fileNames, 10, ReadDocumetnFromStorage);
        if (_dataProcessingOptions.Shuffle)
        {
            _inputData = _inputData.OrderBy(a => Guid.NewGuid());
        }
        _enumerator = Batch(_inputData.OrderBy(a => Guid.NewGuid())).GetEnumerator();
    }

    private async Task<TextDocumentInput> ReadDocumetnFromStorage(string filename)
    {
        var text = await _fileStorage.ReadTextFileAsync(filename);
        var docId = string.Join("/", filename.Split(Path.DirectorySeparatorChar)).Replace(".txt", "");
        _logger.LogDebug("Read document {docId}", docId);
        return new TextDocumentInput(docId, text);
    }

    private async Task<List<string>> GetListOfFilesForProcessingAsync()
    {
        var fileNames = (await _fileStorage.EnumerateFilesRecursiveAsync()).Where(filename => filename.EndsWith(".txt")).ToList();
        _logger.LogInformation($"found {fileNames.Count} files in storage");
        return fileNames;
    }

    public Ta4hInputPayload GetNextPayload()
    {
        if (_enumerator.MoveNext())
        {
            var batch = _enumerator.Current;
            return batch;
        }
        isDone = true;
        return new Ta4hInputPayload();
    }

    public bool IsComplete => isDone;

    private IEnumerable<Ta4hInputPayload> Batch(IEnumerable<TextDocumentInput> docs)
    {
        int maxCharactersPerRequest = _options.MaxCharactersPerRequest;
        int maxDocsPerRequest = _options.MaxDocsPerRequest;
        var random = new Random();

        Ta4hInputPayload nextPayload = new()
        {
            Documents = new()
        };
        docs = docs.ToList();
        for (int _ = 0; _ < _dataProcessingOptions.RepeatTimes; _++)
        {
            
            _logger.LogInformation("Start Batching data, round number {num}. {docCount} docs", (_ + 1), docs.Count());
            foreach (var doc in docs)
            {
                if (nextPayload.Documents.Count == maxDocsPerRequest || nextPayload.TotalCharLength + doc.Text.Length >= maxCharactersPerRequest)
                {
                    yield return nextPayload;
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
                yield return nextPayload;
                nextPayload = new()
                {
                    Documents = new()
                };
            }
        }
    }

    public IEnumerator<TextDocumentInput> GetEnumerator()
    {
        return _inputData.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}