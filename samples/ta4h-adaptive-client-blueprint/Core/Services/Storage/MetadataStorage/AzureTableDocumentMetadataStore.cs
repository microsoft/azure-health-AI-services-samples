using Azure;
using Azure.Data.Tables;
using Azure.Identity;


public class DocumentMetadataTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } // We'll set this as Status in conversion
    public string RowKey { get; set; } // We'll set this as DocumentId in conversion

    public string DocumentId { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string InputPath { get; set; }
    public string Status { get; set; }
    public DateTime LastModified { get; set; }
    public string ResultsPath { get; set; }
    public string JobId { get; set; }
}

public static class DocumentMetadataExtensions
{
    private static Dictionary<string, DocumentMetadataTableEntity> mapping = new Dictionary<string, DocumentMetadataTableEntity>();
    public static DocumentMetadataTableEntity ToTableEntity(this DocumentMetadata documentMetadata)
    {
        var tableEntity = new DocumentMetadataTableEntity
        {
            PartitionKey = "meatadata",
            DocumentId = documentMetadata.DocumentId,
            RowKey = documentMetadata.DocumentId.Replace("/", "SLASH"),
            InputPath = documentMetadata.InputPath,
            Status = Enum.GetName(typeof(ProcessingStatus), documentMetadata.Status),
            LastModified = documentMetadata.LastModified,
            ResultsPath = documentMetadata.ResultsPath,
            JobId = documentMetadata.JobId        
        };
        if (mapping.TryGetValue(tableEntity.DocumentId, out var ta))
        {
            ta.Status = tableEntity.Status;
            ta.LastModified = tableEntity.LastModified;
            ta.JobId = tableEntity.JobId;
            return ta;
        }
        return tableEntity;
    }

    public static DocumentMetadata ToDocumentMetadata(this DocumentMetadataTableEntity entity)
    {
        mapping[entity.DocumentId] = entity;
        return new DocumentMetadata
        {
            DocumentId = entity.DocumentId,
            InputPath = entity.InputPath,
            Status = (ProcessingStatus)Enum.Parse(typeof(ProcessingStatus), entity.Status),
            LastModified = entity.LastModified,
            ResultsPath = entity.ResultsPath,
            JobId = entity.JobId
        };
    }
}


public class AzureTableDocumentMetadataStore : IDocumentMetadataStore
{
    private readonly TableClient _tableClient;
    private const string PasswordAuthentication = "ConnectionString";
    private const string AadAuthetication = "AAD";
    private static string[] ValidAuthenticationMethods = new[] { PasswordAuthentication, AadAuthetication };
    private const string DefaultPartitionKey = "metadata";
    

    public AzureTableDocumentMetadataStore(AzureTableMetadataStorageSettings settings)
    {
        var connectionString = settings.ConnectionString;
        var authenticationMethod = ValidAuthenticationMethods.Contains(settings.AuthenticationMethod) ? settings.AuthenticationMethod : throw new ConfigurationException("MetadataStorage:AzureTableSettings", settings.AuthenticationMethod, ValidAuthenticationMethods);
        var credential = new DefaultAzureCredential();
        var tableName = settings.TableName;
        TableServiceClient serviceClient = new TableServiceClient(new Uri(connectionString), credential);
        _tableClient = serviceClient.GetTableClient(tableName);
    }
    public async Task CreateIfNotExistAsync()
    {
        await _tableClient.CreateIfNotExistsAsync();
    }

    public async Task<IEnumerable<DocumentMetadata>> GetNextDocumentsForProcessAsync(int batchSize)
    {
        var notStartedStatus = Enum.GetName(typeof(ProcessingStatus), ProcessingStatus.NotStarted);
        var query = _tableClient.QueryAsync<DocumentMetadataTableEntity>(filter: e => e.Status == notStartedStatus, maxPerPage: 500);
        var entries = new List<DocumentMetadata>();
        await foreach (var page in query.AsPages())
        {
            var batchEntitiesToUpdate = page.Values.Take(batchSize - entries.Count).ToList();
            string scheduledStr = Enum.GetName(typeof(ProcessingStatus), ProcessingStatus.Scheduled);
            batchEntitiesToUpdate.ForEach(e => { e.Status = scheduledStr; });
            await BatchUpdateTableEntities(batchEntitiesToUpdate);
            entries.AddRange(batchEntitiesToUpdate.Select(e => e.ToDocumentMetadata()));

            if (entries.Count >= batchSize)
            {
                break;
            }
        }
        return entries;
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

    public Task UpdateEntryAsync(DocumentMetadata entry)
    {
        // we assume rowid and partitionkey do not change
        return UpdateEntriesStatusAsync(new List<DocumentMetadata> { entry }, entry.Status, entry.JobId);
    }

    public async Task<DocumentMetadata> GetDocumentMetadataAsync(string documentId)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<DocumentMetadataTableEntity>(DefaultPartitionKey, documentId);
            return response.Value.ToDocumentMetadata();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
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
        var tableEntries = entries.Select(e => e.ToTableEntity());
        await BatchUpdateTableEntities(tableEntries.ToList());
    }

    public async Task MarkAsInitializedAsync()
    {
        var specialEntry = new DocumentMetadata
        {
            DocumentId = "ta4hclientappisinitialized",
            Status = ProcessingStatus.Succeeded,
            LastModified = DateTime.UtcNow,
            InputPath = "N/A",
            ResultsPath = "N/A"
        };
        await _tableClient.AddEntityAsync(specialEntry.ToTableEntity());
    }

    public async Task AddEntriesAsync(IEnumerable<DocumentMetadata> entries)
    {
        List<TableTransactionAction> transactionActions = new List<TableTransactionAction>();

        foreach (var entry in entries)
        {
            transactionActions.Add(new TableTransactionAction(TableTransactionActionType.Add, entry.ToTableEntity()));
        }
        await _tableClient.SubmitTransactionAsync(transactionActions);
    }

    public async Task<bool> IsInitializedAsync()
    {
        try
        {
            var _ = await _tableClient.GetEntityAsync<DocumentMetadataTableEntity>(DefaultPartitionKey, "ta4hclientappisinitialized");
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }
}
