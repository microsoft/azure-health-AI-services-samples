using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data;
using System.Data.SqlClient;
using System.Runtime;
using System.Text;

public class SqlDocumentMetadataStore : IDocumentMetadataStore
{
    private readonly string _connectionString;
    private readonly string _authenticationMethod;
    private readonly ILogger _logger;

    private readonly TokenCredential _credential;

    private const string DocumentId = nameof(DocumentMetadata.DocumentId);
    private const string InputPath = nameof(DocumentMetadata.InputPath);
    private const string Status = nameof(DocumentMetadata.Status);
    private const string LastModified = nameof(DocumentMetadata.LastModified);
    private const string ResultsPath = nameof(DocumentMetadata.ResultsPath);
    private const string JobId = nameof(DocumentMetadata.JobId);

    private const string InitializeDbEntryName = "ta4hclientappisinitialized";

    private const string PasswordAuthentication = "Password";
    private const string AadAuthetication = "AAD";
    private static string[] ValidAuthenticationMethods = new[] { PasswordAuthentication, AadAuthetication };

    private bool staleDocumentsChecked = false;
    private bool? tableAlreadyInitialized = null;


    public SqlDocumentMetadataStore(ILogger<SqlDocumentMetadataStore> logger, IOptions<SQLMetadataStorageSettings> options)
    {
        var dbSettings = options.Value;
        _connectionString = dbSettings.ConnectionString;
        _authenticationMethod = ValidAuthenticationMethods.Contains(dbSettings.AuthenticationMethod) ? dbSettings.AuthenticationMethod : throw new ConfigurationException("MetadataStorage:SQLSettings", dbSettings.AuthenticationMethod, ValidAuthenticationMethods);
        _credential = new DefaultAzureCredential();
        TableName = dbSettings.TableName;
        _logger = logger;

    }

    private string TableName { get; }


    private async Task<SqlConnection> GetOpenSqlConnectionAsync()
    {
        var sqlConnection = new SqlConnection(_connectionString);
        if (_authenticationMethod == AadAuthetication)
        {
            var accessToken = await _credential.GetTokenAsync(
                new TokenRequestContext(new[] { "https://database.windows.net/.default" }), default);
            sqlConnection.AccessToken = accessToken.Token;

        }
        await sqlConnection.OpenAsync();
        return sqlConnection;
    }

    public async Task CreateIfNotExistAsync()
    {
        using var connection = await GetOpenSqlConnectionAsync();
        var tableCheckCommandText = $@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '{TableName}')
        CREATE TABLE {TableName} (
                    {DocumentId} nvarchar(2048) PRIMARY KEY,
                    {InputPath} nvarchar(max) NOT NULL,
                    {Status} int NOT NULL,
                    {LastModified} datetime2 NOT NULL,
                    {ResultsPath} nvarchar(max) NOT NULL,
                    {JobId} nvarchar(64) NULL
        )";

        using var tableCheckCommand = new SqlCommand(tableCheckCommandText, connection);
        await tableCheckCommand.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<DocumentMetadata>> GetNextDocumentsForProcessAsync(int batchSize)
    {
        if (tableAlreadyInitialized.Value && !staleDocumentsChecked)
        {
            // If isPreInitialized == true it means that the metadata storage table has already been initialized before in a previous run of this app.
            // It might have crushed due to an error, was stopped manually or failed to complete for some other reason. It is possible that in the previous run some documents were
            // marked as scheduled or sent to processing but were not updated since. So we first look for these documents before we continue for documents in "NotStarted" Status.
            var staleDocumentsEntries = await GetStaleDocumentsEntriesAsync(batchSize);
            if (staleDocumentsEntries.Any())
            {
                return staleDocumentsEntries;
            }
            else
            {
                // no more stale documents to load - continute to loading documents with Status "NotStarted"
                staleDocumentsChecked = true;
            }
        }
        return await GetDocumentsWithNotStartedStatusAsync(batchSize);
    }

    private async Task<IEnumerable<DocumentMetadata>> GetDocumentsWithNotStartedStatusAsync(int batchSize)
    {
        using var connection = await GetOpenSqlConnectionAsync();
        using var command = new SqlCommand($@"
        WITH cte AS (
            SELECT TOP (@BatchSize) {DocumentId}, {InputPath}, {Status}, {LastModified}, {ResultsPath}, {JobId}
            FROM {TableName}
            WHERE {Status} = @{Status}
            ORDER BY {LastModified}
        )
        UPDATE cte
        SET {Status} = @NewStatus
        OUTPUT INSERTED.{DocumentId}, INSERTED.{InputPath}, INSERTED.{Status}, INSERTED.{LastModified}, INSERTED.{ResultsPath}, INSERTED.{JobId}", connection);

        command.Parameters.AddWithValue("@BatchSize", batchSize);
        command.Parameters.AddWithValue($"@{Status}", (int)ProcessingStatus.NotStarted);
        command.Parameters.AddWithValue("@NewStatus", (int)ProcessingStatus.Scheduled);

        var entries = new List<DocumentMetadata>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new DocumentMetadata
            {
                DocumentId = reader.GetString(0),
                InputPath = reader.GetString(1),
                Status = (ProcessingStatus)reader.GetInt32(2),
                LastModified = reader.GetDateTime(3),
                ResultsPath = reader.GetString(4),
                JobId = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        }

        return entries;
    }

    private async Task<IEnumerable<DocumentMetadata>> GetStaleDocumentsEntriesAsync(int batchSize)
    {
        using var connection = await GetOpenSqlConnectionAsync();
        using var command = new SqlCommand($@"
            SELECT TOP (@BatchSize) {DocumentId}, {InputPath}, {Status}, {LastModified}, {ResultsPath}, {JobId}
            FROM {TableName}
            WHERE (({Status} = @ScheduledStatus OR {Status} = @ProcessingStatus) AND {LastModified} <= DATEADD(MINUTE, -30, GETUTCDATE()))
            ORDER BY {LastModified}", connection);

        command.Parameters.AddWithValue("@BatchSize", batchSize);
        command.Parameters.AddWithValue("@ScheduledStatus", (int)ProcessingStatus.Scheduled);
        command.Parameters.AddWithValue("@ProcessingStatus", (int)ProcessingStatus.Processing);

        var entries = new List<DocumentMetadata>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new DocumentMetadata
            {
                DocumentId = reader.GetString(0),
                InputPath = reader.GetString(1),
                Status = (ProcessingStatus)reader.GetInt32(2),
                LastModified = reader.GetDateTime(3),
                ResultsPath = reader.GetString(4),
                JobId = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        }

        return entries;
    }


    public async Task UpdateEntryAsync(DocumentMetadata entry)
    {
        entry.LastModified = DateTime.UtcNow;
        using var connection = await GetOpenSqlConnectionAsync();
        using var command = new SqlCommand($@"
            UPDATE {TableName} 
            SET 
                {InputPath} = @{InputPath},
                {Status} = @{Status},
                {LastModified} = @{LastModified},
                {ResultsPath} = @{ResultsPath},
                {JobId} = @{JobId}
            WHERE {DocumentId} = @{DocumentId}", connection);

        command.Parameters.AddWithValue($"@{DocumentId}", entry.DocumentId);
        command.Parameters.AddWithValue($"@{InputPath}", entry.InputPath);
        command.Parameters.AddWithValue($"@{Status}", (int)entry.Status);
        command.Parameters.AddWithValue($"@{LastModified}", entry.LastModified);
        command.Parameters.AddWithValue($"@{ResultsPath}", entry.ResultsPath);
        command.Parameters.AddWithValue($"@{JobId}", entry.JobId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<DocumentMetadata> GetDocumentMetadataAsync(string documentId)
    {
        using var connection = await GetOpenSqlConnectionAsync();
        using var command = new SqlCommand($@"
            SELECT {DocumentId}, {InputPath}, {Status}, {LastModified}, {ResultsPath}, {JobId}
            FROM {TableName} 
            WHERE {DocumentId} = @{DocumentId}", connection);

        command.Parameters.AddWithValue($"@{DocumentId}", documentId);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new DocumentMetadata
            {
                DocumentId = reader.GetString(0),
                InputPath = reader.GetString(1),
                Status = (ProcessingStatus)reader.GetInt32(2),
                LastModified = reader.GetDateTime(3),
                ResultsPath = reader.GetString(4),
                JobId = reader.IsDBNull(5) ? null : reader.GetString(5)
            };
        }

        return null;
    }


    public async Task UpdateEntriesStatusAsync(List<DocumentMetadata> entries, ProcessingStatus newStatus, string jobId = null)
    {
        var newLastModified = DateTime.UtcNow;
        var parameters = new StringBuilder();
        for (var i = 0; i < entries.Count(); i++)
        {
            entries[i].Status = newStatus;
            entries[i].LastModified = newLastModified;
            parameters.Append($"@{DocumentId}{i},");
        }
        parameters.Length--;  // remove last comma

        using var connection = await GetOpenSqlConnectionAsync();
        string commandString;
        if (string.IsNullOrEmpty(jobId))
        {
            commandString = $@"
            UPDATE {TableName} 
            SET 
                {Status} = @{Status},
                {LastModified} = @{LastModified}
            WHERE {DocumentId} IN ({parameters})";
        }
        else
        {
            entries.ForEach(e => e.JobId = jobId);
            commandString = $@"
            UPDATE {TableName} 
            SET 
                {Status} = @{Status},
                {JobId} = @{JobId},
                {LastModified} = @{LastModified}
            WHERE {DocumentId} IN ({parameters})";
        }

        using var command = new SqlCommand(commandString, connection);       

        var idParams = entries.Select((e, i) => new SqlParameter($"@{DocumentId}{i}", e.DocumentId)).ToArray();
        command.Parameters.AddRange(idParams);
        command.Parameters.AddWithValue($"@{Status}", (int)newStatus);
        if (!string.IsNullOrEmpty(jobId))
        {
            command.Parameters.AddWithValue($"@{JobId}", jobId);
        }
        command.Parameters.AddWithValue($"@{LastModified}", newLastModified);
        await command.ExecuteNonQueryAsync();
    }


    public async Task MarkAsInitializedAsync()
    {
        var specialEntry = new DocumentMetadata
        {
            DocumentId = InitializeDbEntryName,
            Status = ProcessingStatus.Succeeded,
            LastModified = DateTime.UtcNow,
            InputPath = "N/A",
            ResultsPath = "N/A"
        };
        await AddEntriesAsync(new[] { specialEntry });
        tableAlreadyInitialized = false; // if we needed to mark as initialized it means it wasn't initialized before

    }

    public async Task AddEntriesAsync(IEnumerable<DocumentMetadata> entries)
    {
        var table = new DataTable();
        table.Columns.Add(DocumentId, typeof(string));
        table.Columns.Add(InputPath, typeof(string));
        table.Columns.Add(Status, typeof(int));
        table.Columns.Add(LastModified, typeof(DateTime));
        table.Columns.Add(ResultsPath, typeof(string));
        table.Columns.Add(JobId, typeof(string));

        foreach (var entry in entries)
        {
            table.Rows.Add(entry.DocumentId, entry.InputPath, (int)entry.Status, entry.LastModified, entry.ResultsPath, entry.JobId);
        }
        using var connection = await GetOpenSqlConnectionAsync();
        using var bulkCopy = new SqlBulkCopy(connection);
        bulkCopy.DestinationTableName = TableName;
        await bulkCopy.WriteToServerAsync(table);
    }


    public async Task<bool> IsInitializedAsync()
    {
        var specialEntry = await GetDocumentMetadataAsync(InitializeDbEntryName);

        if (specialEntry != null)
        {
            // the existance of the Special Entry means the table has been initialized before
            tableAlreadyInitialized = true;
        }
        else
        {
            tableAlreadyInitialized = false;
        }
        return tableAlreadyInitialized.Value;
    }
}
