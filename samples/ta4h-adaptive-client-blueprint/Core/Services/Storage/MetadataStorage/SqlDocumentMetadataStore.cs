using Azure.Core;
using Azure.Identity;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Runtime;
using System.Text;

public class SqlDocumentMetadataStore : IDocumentMetadataStore
{
    private readonly string _connectionString;
    private readonly string _authenticationMethod;
    private readonly TokenCredential _credential;

    private const string Id = nameof(DocumentMetadata.DocumentId);
    private const string InputPath = nameof(DocumentMetadata.InputPath);
    private const string Status = nameof(DocumentMetadata.Status);
    private const string LastModified = nameof(DocumentMetadata.LastModified);
    private const string ResultsPath = nameof(DocumentMetadata.ResultsPath);
    private const string JobId = nameof(DocumentMetadata.JobId);

    private const string InitializeDbEntryName = "ta4hclientappisinitialized";

    private const string PasswordAuthenctions = "Password";
    private const string AadAuthenctions = "AAD";
    private static string[] ValidAuthenticationMethods = new[] { PasswordAuthenctions, AadAuthenctions }; 

    public SqlDocumentMetadataStore(SQLMetadataStorageSettings dbSettings)
    {
        _connectionString = dbSettings.ConnectionString;
        _authenticationMethod = ValidAuthenticationMethods.Contains(dbSettings.AuthenticationMethod) ? dbSettings.AuthenticationMethod : throw new ConfigurationException("MetadataStorage:SQLSettings", dbSettings.AuthenticationMethod, ValidAuthenticationMethods);
        _credential = new DefaultAzureCredential();
        TableName = dbSettings.TableName;

    }

    private string TableName { get; }


    private async Task<SqlConnection> GetOpenSqlConnectionAsync()
    {
        var sqlConnection = new SqlConnection(_connectionString);
        if (_authenticationMethod == AadAuthenctions)
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
                    {Id} nvarchar(2048) PRIMARY KEY,
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
        var entries = new List<DocumentMetadata>();

        using var connection = await GetOpenSqlConnectionAsync();
        using var command = new SqlCommand($@"
        WITH cte AS (
            SELECT TOP (@BatchSize) {Id}, {InputPath}, {Status}, {LastModified}, {ResultsPath}, {JobId}
            FROM {TableName}
            WHERE {Status} = @Status
            ORDER BY {LastModified}
        )
        UPDATE cte
        SET {Status} = @NewStatus
        OUTPUT INSERTED.{Id}, INSERTED.{InputPath}, INSERTED.{Status}, INSERTED.{LastModified}, INSERTED.{ResultsPath}, INSERTED.{JobId}", connection);

        command.Parameters.AddWithValue("@BatchSize", batchSize);
        command.Parameters.AddWithValue("@Status", (int)ProcessingStatus.NotStarted);
        command.Parameters.AddWithValue("@NewStatus", (int)ProcessingStatus.Scheduled);

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
        using var connection = await GetOpenSqlConnectionAsync();
        using var command = new SqlCommand($@"
            UPDATE {TableName} 
            SET 
                {InputPath} = @InputPath,
                {Status} = @Status,
                {LastModified} = @LastModified,
                {ResultsPath} = @ResultsPath,
                {JobId} = @JobId
            WHERE {Id} = @Id", connection);

        command.Parameters.AddWithValue("@Id", entry.DocumentId);
        command.Parameters.AddWithValue("@InputPath", entry.InputPath);
        command.Parameters.AddWithValue("@Status", (int)entry.Status);
        command.Parameters.AddWithValue("@LastModified", entry.LastModified);
        command.Parameters.AddWithValue("@ResultsPath", entry.ResultsPath);
        command.Parameters.AddWithValue("@JobId", entry.JobId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<DocumentMetadata> GetDocumentMetadataAsync(string documentId)
    {
        using var connection = await GetOpenSqlConnectionAsync();
        using var command = new SqlCommand($@"
            SELECT {Id}, {InputPath}, {Status}, {LastModified}, {ResultsPath}, {JobId}
            FROM {TableName} 
            WHERE {Id} = @Id", connection);

        command.Parameters.AddWithValue("@Id", documentId);

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
            parameters.Append($"@Id{i},");
        }
        parameters.Length--;  // remove last comma

        using var connection = await GetOpenSqlConnectionAsync();
        string commandString;
        if (string.IsNullOrEmpty(jobId))
        {
            commandString = $@"
            UPDATE {TableName} 
            SET 
                {Status} = @Status,
                {LastModified} = @LastModified
            WHERE {Id} IN ({parameters})";
        }
        else
        {
            entries.ForEach(e => e.JobId = jobId);
            commandString = $@"
            UPDATE {TableName} 
            SET 
                {Status} = @Status,
                {JobId} = @JobId,
                {LastModified} = @LastModified
            WHERE {Id} IN ({parameters})";
        }

        using var command = new SqlCommand(commandString, connection);       

        var idParams = entries.Select((e, i) => new SqlParameter($"@Id{i}", e.DocumentId)).ToArray();
        command.Parameters.AddRange(idParams);
        command.Parameters.AddWithValue("@Status", (int)newStatus);
        if (!string.IsNullOrEmpty(jobId))
        {
            command.Parameters.AddWithValue("@JobId", jobId);
        }
        command.Parameters.AddWithValue("@LastModified", newLastModified);
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
    }

    public async Task AddEntriesAsync(IEnumerable<DocumentMetadata> entries)
    {
        var table = new DataTable();
        table.Columns.Add(Id, typeof(string));
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
        return specialEntry != null;
    }
}
