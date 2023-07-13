using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

public class LRUDictionary<TKey, TValue> : IDictionary<TKey, TValue>
{
    private readonly int capacity;
    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> cache;
    private readonly LinkedList<CacheItem> lruList;

    public LRUDictionary(int capacity)
    {
        this.capacity = capacity;
        this.cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(capacity);
        this.lruList = new LinkedList<CacheItem>();
    }

    public TValue this[TKey key]
    {
        get
        {
            if (TryGetValue(key, out var value))
            {
                return value;
            }
            else
            {
                throw new KeyNotFoundException($"The key {key} was not found in the cache.");
            }
        }
        set => AddOrUpdate(key, value);
    }

    public ICollection<TKey> Keys => cache.Keys;

    public ICollection<TValue> Values
    {
        get
        {
            var values = new List<TValue>(cache.Count);
            foreach (var node in cache.Values)
            {
                values.Add(node.Value.Value);
            }

            return values;
        }
    }

    public int Count => cache.Count;

    public bool IsReadOnly => false;

    public void Add(TKey key, TValue value) => AddOrUpdate(key, value);

    public void Add(KeyValuePair<TKey, TValue> item) => AddOrUpdate(item.Key, item.Value);

    public void Clear()
    {
        cache.Clear();
        lruList.Clear();
    }

    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        if (TryGetValue(item.Key, out var value))
        {
            return EqualityComparer<TValue>.Default.Equals(value, item.Value);
        }

        return false;
    }

    public bool ContainsKey(TKey key) => cache.ContainsKey(key);

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        foreach (var pair in this)
        {
            array[arrayIndex++] = pair;
        }
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        foreach (var node in cache)
        {
            yield return new KeyValuePair<TKey, TValue>(node.Value.Value.Key, node.Value.Value.Value);
        }
    }


    public bool Remove(TKey key)
    {
        if (cache.TryGetValue(key, out var node))
        {
            lruList.Remove(node);
            cache.Remove(key);
            return true;
        }

        return false;
    }

    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        if (Contains(item))
        {
            return Remove(item.Key);
        }

        return false;
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        if (cache.TryGetValue(key, out var node))
        {
            // Item found, move it to the front of the LRU list
            value = node.Value.Value;
            lruList.Remove(node);
            lruList.AddFirst(node);
            return true;
        }
        else
        {
            // Not found
            value = default;
            return false;
        }
    }

    private void AddOrUpdate(TKey key, TValue value)
    {
        if (cache.Count >= capacity && !cache.ContainsKey(key))
        {
            // Cache is full and the key isn't in the cache, remove the least recently used item
            var lastNode = lruList.Last;
            cache.Remove(lastNode.Value.Key);
            lruList.RemoveLast();
        }

        if (cache.TryGetValue(key, out var node))
        {
            // Item is already in the cache, move it to the front and update the value
            node.Value = new CacheItem(key, value);
            lruList.Remove(node);
            lruList.AddFirst(node);
        }
        else
        {
            // Add item at the front of the LRU list
            var newNode = new LinkedListNode<CacheItem>(new CacheItem(key, value));
            lruList.AddFirst(newNode);
            cache.Add(key, newNode);
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private record CacheItem(TKey Key, TValue Value);
}


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
    private static IDictionary<string, DocumentMetadataTableEntity> mapping = new Dictionary<string, DocumentMetadataTableEntity>();
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
    private readonly ILogger _logger;
    private const string PasswordAuthentication = "ConnectionString";
    private const string AadAuthetication = "AAD";
    private static string[] ValidAuthenticationMethods = new[] { PasswordAuthentication, AadAuthetication };
    private const string DefaultPartitionKey = "metadata";
    

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
        stopwatch.Stop();
        _logger.LogInformation("Scheduled {count} documents for processing", entries.Count);
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
        var specialEntry = new DocumentMetadataTableEntity
        {
            RowKey = "ta4hclientappisinitialized",
            PartitionKey = "ta4hclientappisinitialized",
            DocumentId = "ta4hclientappisinitialized",
            LastModified = DateTime.UtcNow
        };
        await _tableClient.AddEntityAsync(specialEntry);
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
            var _ = await _tableClient.GetEntityAsync<DocumentMetadataTableEntity>("ta4hclientappisinitialized", "ta4hclientappisinitialized");
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }
}
