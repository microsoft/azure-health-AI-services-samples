using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Runtime.Serialization;

public class DocumentMetadataTableEntity : DocumentMetadata, ITableEntity
{

    public string PartitionKey { get; set; }
    public string RowKey { get; set; }

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string StatusString
    {
        get => Status.ToString();
        set => Status = Enum.Parse<ProcessingStatus>(value, true);
    }

    [IgnoreDataMember] // This property won't be stored in Azure Table Storage
    public override ProcessingStatus Status { get; set; }

}

public static class DocumentMetadataExtensions
{
    public static DocumentMetadataTableEntity AsDocumentMetadataTableEntity(this DocumentMetadata documentMetadata)
    {
        if (documentMetadata is DocumentMetadataTableEntity)
        {
            return documentMetadata as DocumentMetadataTableEntity;
        }
        var tableEntity = new DocumentMetadataTableEntity
        {
            DocumentId = documentMetadata.DocumentId,
            RowKey = documentMetadata.DocumentId.Replace("/", "SLASH"),
            PartitionKey = documentMetadata.Status == ProcessingStatus.NotStarted ? "NotStarted" : "Started",
            InputPath = documentMetadata.InputPath,
            Status = documentMetadata.Status,
            LastModified = documentMetadata.LastModified,
            ResultsPath = documentMetadata.ResultsPath,
            JobId = documentMetadata.JobId        
        };

        return tableEntity;
    }
}


public class AzureTableDocumentMetadataStore : IDocumentMetadataStore
{
    private readonly TableClient _tableClient;
    private readonly ILogger _logger;
    private const string ConnectionStringAuthentication = "ConnectionString";
    private const string AadAuthetication = "AAD";
    private static string[] ValidAuthenticationMethods = new[] { ConnectionStringAuthentication, AadAuthetication };
    private const string SpecialEntryName = "ta4hclientappisinitialized";
    private bool staleDocumentsChecked = false;
    private bool? tableAlreadyInitialized = null;


    public AzureTableDocumentMetadataStore(IOptions<AzureTableMetadataStorageSettings> options, ILogger<AzureTableDocumentMetadataStore> logger)
    {
        var settings = options.Value;
        var connectionString = settings.ConnectionString;
        var authenticationMethod = ValidAuthenticationMethods.Contains(settings.AuthenticationMethod) ? settings.AuthenticationMethod : throw new ConfigurationException("MetadataStorage:AzureTableSettings", settings.AuthenticationMethod, ValidAuthenticationMethods);
        TableServiceClient serviceClient;
        if (authenticationMethod == AadAuthetication)
        {
            var credential = new DefaultAzureCredential();
            serviceClient = new TableServiceClient(new Uri(connectionString), credential);
        }
        else
        {
            serviceClient = new TableServiceClient(connectionString);
        }
        _tableClient = serviceClient.GetTableClient(settings.TableName);
        _logger = logger;
    }
    public async Task CreateIfNotExistAsync()
    {
        await _tableClient.CreateIfNotExistsAsync();
    }

    public async Task<IEnumerable<DocumentMetadata>> GetNextDocumentsForProcessAsync(int batchSize)
    {
        if (tableAlreadyInitialized.Value && !staleDocumentsChecked)
        {
            // If isPreInitialized == true it means that the metadata storage table has already been initialized before in a previous run of this app.
            // It might have crushed due to an error, was stopped manually or failed to complete for some other reason. It is possible that in the previous run some documents were
            // marked as scheduled or sent to processing but were not updated since. So we first look for these documents before we continue for documents in "NotStarted" Status.
            List<DocumentMetadata> staleDocumetEntries = await GetStaleDocumentsEntriesAsync(batchSize);
            if (staleDocumetEntries.Any())
            {
                return staleDocumetEntries;
            }
            else
            {
                // no more stale documents to load - continute to loading documents with Status "NotStarted"
                staleDocumentsChecked = true;
            }
        }
        return await GetDocumentsWithNotStartedStatusAsync(batchSize);
    }

    /// <summary>
    /// Get a batch of DocumentMetadata entries for documents with Status "NotStarted"
    /// </summary>
    /// <param name="batchSize"></param>
    /// <returns></returns>
    private async Task<List<DocumentMetadata>> GetDocumentsWithNotStartedStatusAsync(int batchSize)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        var query = _tableClient.QueryAsync<DocumentMetadataTableEntity>(filter: e => e.PartitionKey == "NotStarted", maxPerPage: 500);
        var entries = await ProcessQueryResults(query, batchSize);
        stopwatch.Stop();
        _logger.LogInformation("Scheduled {count} documents for processing, took {ms} ms to read from metadata.", entries.Count, stopwatch.ElapsedMilliseconds);
        return entries;
    }

    /// <summary>
    /// Get a batch of DocumentMetadata entries that have been scheduled before but were not completed
    /// </summary>
    /// <param name="batchSize"></param>
    private async Task<List<DocumentMetadata>> GetStaleDocumentsEntriesAsync(int batchSize)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        var query = _tableClient.QueryAsync<DocumentMetadataTableEntity>(
            filter: e => e.PartitionKey == "Started" && 
            (e.StatusString == "Scheduled" || e.StatusString == "Processing") && 
            e.Timestamp < DateTimeOffset.UtcNow - TimeSpan.FromMinutes(60),
            maxPerPage: 500);
        var entries = await ProcessQueryResults(query, batchSize);
        stopwatch.Stop();
        _logger.LogInformation("Scheduled {count} documents for processing, took {ms} ms to read from metadata.", entries.Count, stopwatch.ElapsedMilliseconds);
        return entries;
    }

    private async Task<List<DocumentMetadata>> ProcessQueryResults(AsyncPageable<DocumentMetadataTableEntity> query, int batchSize)
    {
        var entries = new List<DocumentMetadata>();
        await foreach (var page in query.AsPages())
        {
            var batchEntitiesToUpdate = page.Values.Take(batchSize - entries.Count).ToList();
            List<TableTransactionAction> addActions = new List<TableTransactionAction>();
            List<TableTransactionAction> deleteActions = new List<TableTransactionAction>();

            for (int i = 0; i < batchEntitiesToUpdate.Count; i++)
            {
                // because the partition key of "NotStarted" entries is different from entries with other Status, we cannot update the entry.
                // We need instead to create a new one and delete the previous one.
                var existingEntry = batchEntitiesToUpdate[i];
                var newEntry = new DocumentMetadataTableEntity
                {
                    DocumentId = existingEntry.DocumentId,
                    RowKey = existingEntry.RowKey,
                    PartitionKey = "Started",
                    InputPath = existingEntry.InputPath,
                    Status = ProcessingStatus.Scheduled,
                    LastModified = DateTime.UtcNow,
                    ResultsPath = existingEntry.ResultsPath
                };
                deleteActions.Add(new TableTransactionAction(TableTransactionActionType.Delete, existingEntry));
                addActions.Add(new TableTransactionAction(TableTransactionActionType.Add, newEntry));
                if (addActions.Count == 100 || (addActions.Count > 0 && i == batchEntitiesToUpdate.Count - 1))
                {
                    await _tableClient.SubmitTransactionAsync(addActions);
                    await _tableClient.SubmitTransactionAsync(deleteActions);
                    entries.AddRange(addActions.Select(a => a.Entity as DocumentMetadataTableEntity));
                    addActions.Clear();
                    deleteActions.Clear();
                }
            }

            if (entries.Count >= batchSize)
            {
                break;
            }
        }
        return entries;
    }

    public Task UpdateEntryAsync(DocumentMetadata entry)
    {
        // we assume rowid and partitionkey do not change
        return UpdateEntriesStatusAsync(new List<DocumentMetadata> { entry }, entry.Status, entry.JobId);
    }


    public async Task UpdateEntriesStatusAsync(List<DocumentMetadata> entries, ProcessingStatus newStatus, string jobId = null)
    {
        foreach (var entry in entries)
        {
            entry.Status = newStatus;
            if (jobId != null)
            {
                entry.JobId = jobId;
            }
        }
        var tableEntries = entries.Select(e => e.AsDocumentMetadataTableEntity());
        await BatchUpdateTableEntities(tableEntries.ToList());
    }

    public async Task MarkAsInitializedAsync()
    {
        var specialEntry = new DocumentMetadataTableEntity
        {
            RowKey = SpecialEntryName,
            PartitionKey = SpecialEntryName,
            LastModified = DateTime.UtcNow,
            Status = ProcessingStatus.Succeeded
        };
        tableAlreadyInitialized = false; // if we needed to mark as initialized it means it wasn't initialized before
        await _tableClient.AddEntityAsync(specialEntry);
    }

    public async Task AddEntriesAsync(IEnumerable<DocumentMetadata> entries)
    {
        List<TableTransactionAction> transactionActions = new List<TableTransactionAction>();

        foreach (var entry in entries)
        {
            transactionActions.Add(new TableTransactionAction(TableTransactionActionType.Add, entry.AsDocumentMetadataTableEntity()));
        }
        await _tableClient.SubmitTransactionAsync(transactionActions);
    }

    public async Task<bool> IsInitializedAsync()
    {
        try
        {
            var _ = await _tableClient.GetEntityAsync<DocumentMetadataTableEntity>(SpecialEntryName, SpecialEntryName);
            // the existance of the Special Entry means the table has been initialized before
            tableAlreadyInitialized = true;
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // this means the sepecial entity doesn't exist
            return false;
        }
    }

    private async Task BatchUpdateTableEntities(List<DocumentMetadataTableEntity> batchEntitiesToUpdate)
    {
        List<TableTransactionAction> transactionActions = new List<TableTransactionAction>();

        foreach (var entry in batchEntitiesToUpdate)
        {
            entry.LastModified = DateTime.UtcNow;
            transactionActions.Add(new TableTransactionAction(TableTransactionActionType.UpdateMerge, entry));
            if (transactionActions.Count == 100)
            {
                var responses = await _tableClient.SubmitTransactionAsync(transactionActions);
                transactionActions.Clear();
            }
        }
        if (transactionActions.Any())
        {
            var responses = await _tableClient.SubmitTransactionAsync(transactionActions);
        }
    }
}
