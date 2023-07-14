using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Runtime.Serialization;

public class DocumentMetadataTableEntity : DocumentMetadata, ITableEntity
{

    public string PartitionKey { get; set; } // We'll set this as Status in conversion
    public string RowKey { get; set; } // We'll set this as DocumentId in conversion

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
    private const string PasswordAuthentication = "ConnectionString";
    private const string AadAuthetication = "AAD";
    private static string[] ValidAuthenticationMethods = new[] { PasswordAuthentication, AadAuthetication };
    private const string SpecialEntryName = "ta4hclientappisinitialized";



    public AzureTableDocumentMetadataStore(IOptions<AzureTableMetadataStorageSettings> options, ILogger<AzureTableDocumentMetadataStore> logger)
    {
        var settings = options.Value;
        var connectionString = settings.ConnectionString;
        var authenticationMethod = ValidAuthenticationMethods.Contains(settings.AuthenticationMethod) ? settings.AuthenticationMethod : throw new ConfigurationException("MetadataStorage:AzureTableSettings", settings.AuthenticationMethod, ValidAuthenticationMethods);
        var credential = new DefaultAzureCredential();
        var tableName = settings.TableName;
        TableServiceClient serviceClient = new TableServiceClient(new Uri(connectionString), credential);
        _tableClient = serviceClient.GetTableClient(tableName);
        _logger = logger;
    }
    public async Task CreateIfNotExistAsync()
    {
        await _tableClient.CreateIfNotExistsAsync();
    }

    public async Task<IEnumerable<DocumentMetadata>> GetNextDocumentsForProcessAsync(int batchSize)
    {

        Stopwatch stopwatch = Stopwatch.StartNew();
        var notStartedStatus = Enum.GetName(typeof(ProcessingStatus), ProcessingStatus.NotStarted);
        var query = _tableClient.QueryAsync<DocumentMetadataTableEntity>(filter: e => e.PartitionKey == "NotStarted", maxPerPage: 500);
        var entries = new List<DocumentMetadata>();
        await foreach (var page in query.AsPages())
        {
            var batchEntitiesToUpdate = page.Values.Take(batchSize - entries.Count).ToList();
            List<TableTransactionAction> addActions = new List<TableTransactionAction>();
            List<TableTransactionAction> deleteActions = new List<TableTransactionAction>();

            for (int i =0; i < batchEntitiesToUpdate.Count; i++)
            {
                // because the partition key of "NotStarted" entries is different from entries with other Status, we cannot update the entry.
                // We need instead to create a new one and delete the previous one.
                var existingEntry = batchEntitiesToUpdate[i];
                var newEntry =new DocumentMetadataTableEntity
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
        stopwatch.Stop();
        _logger.LogInformation("Scheduled {count} documents for processing", entries.Count);
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
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
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
