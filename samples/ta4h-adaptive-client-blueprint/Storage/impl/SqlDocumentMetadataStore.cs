using System.Data.SqlClient;

public class SqlDocumentMetadataStore : IDocumentMetadataStore
{
    private readonly string connectionString;
    private const string TableName = "DocumentMetadata";
    private const string Id = nameof(DocumentMetadata.DocumentId);
    private const string InputPath = nameof(DocumentMetadata.InputPath);
    private const string Status = nameof(DocumentMetadata.Status);
    private const string LastModified = nameof(DocumentMetadata.LastModified);
    private const string ResultsPath = nameof(DocumentMetadata.ResultsPath);

    private const string InitializeDbEntryName = "ta4hclientappisinitialized";

    public SqlDocumentMetadataStore(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public async Task CreateIfNotExistAsync()
    {

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var tableCheckCommandText = $@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '{TableName}')
        CREATE TABLE {TableName} (
                    {Id} nvarchar(2048) PRIMARY KEY,
                    {InputPath} nvarchar(max) NOT NULL,
                    {Status} int NOT NULL,
                    {LastModified} datetime2 NOT NULL,
                    {ResultsPath} nvarchar(max) NOT NULL
        )";

        using var tableCheckCommand = new SqlCommand(tableCheckCommandText, connection);
        await tableCheckCommand.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<DocumentMetadata>> GetNextDocumentsForProcessAsync(int batchSize)
    {
        var entries = new List<DocumentMetadata>();

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        using var command = new SqlCommand($@"
        WITH cte AS (
            SELECT TOP (@BatchSize) {Id}, {InputPath}, {Status}, {LastModified}, {ResultsPath}
            FROM {TableName}
            WHERE {Status} = @Status
            ORDER BY {LastModified}
        )
        UPDATE cte
        SET {Status} = @NewStatus
        OUTPUT INSERTED.{Id}, INSERTED.{InputPath}, INSERTED.{Status}, INSERTED.{LastModified}, INSERTED.{ResultsPath}", connection);

        command.Parameters.AddWithValue("@BatchSize", batchSize);
        command.Parameters.AddWithValue("@Status", (int)ProcessingStatus.NotStarted);
        command.Parameters.AddWithValue("@NewStatus", (int)ProcessingStatus.Processing);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new DocumentMetadata
            {
                DocumentId = reader.GetString(0),
                InputPath = reader.GetString(1),
                Status = (ProcessingStatus)reader.GetInt32(2),
                LastModified = reader.GetDateTime(3),
                ResultsPath = reader.GetString(4)
            });
        }

        return entries;
    }

    public async Task UpdateEntryAsync(DocumentMetadata entry)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        using var command = new SqlCommand($@"
            UPDATE {TableName} 
            SET 
                {InputPath} = @InputPath,
                {Status} = @Status,
                {LastModified} = @LastModified,
                {ResultsPath} = @ResultsPath
            WHERE {Id} = @Id", connection);

        command.Parameters.AddWithValue("@Id", entry.DocumentId);
        command.Parameters.AddWithValue("@InputPath", entry.InputPath);
        command.Parameters.AddWithValue("@Status", (int)entry.Status);
        command.Parameters.AddWithValue("@LastModified", entry.LastModified);
        command.Parameters.AddWithValue("@ResultsPath", entry.ResultsPath);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<DocumentMetadata> GetDocumentMetadataAsync(string documentId)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        using var command = new SqlCommand($@"
            SELECT {Id}, {InputPath}, {Status}, {LastModified}, {ResultsPath} 
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
                ResultsPath = reader.GetString(4)
            };
        }

        return null;
    }

    public async Task UpdateEntriesAsync(IEnumerable<DocumentMetadata> entries)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var entry in entries)
        {
            using var command = new SqlCommand($@"
                UPDATE {TableName} 
                SET 
                    {InputPath} = @InputPath,
                    {Status} = @Status,
                    {LastModified} = @LastModified,
                    {ResultsPath} = @ResultsPath
                WHERE {Id} = @Id", connection);

            command.Parameters.AddWithValue("@Id", entry.DocumentId);
            command.Parameters.AddWithValue("@InputPath", entry.InputPath);
            command.Parameters.AddWithValue("@Status", (int)entry.Status);
            command.Parameters.AddWithValue("@LastModified", entry.LastModified);
            command.Parameters.AddWithValue("@ResultsPath", entry.ResultsPath);

            await command.ExecuteNonQueryAsync();
        }
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
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var entry in entries)
        {
            using var command = new SqlCommand($@"
            INSERT INTO {TableName} ({Id}, {InputPath}, {Status}, {LastModified}, {ResultsPath}) 
            VALUES (@Id, @InputPath, @Status, @LastModified, @ResultsPath)", connection);

            command.Parameters.AddWithValue("@Id", entry.DocumentId);
            command.Parameters.AddWithValue("@InputPath", entry.InputPath);
            command.Parameters.AddWithValue("@Status", (int)entry.Status);
            command.Parameters.AddWithValue("@LastModified", entry.LastModified);
            command.Parameters.AddWithValue("@ResultsPath", entry.ResultsPath);

            await command.ExecuteNonQueryAsync();
        }
    }


    public async Task<bool> IsInitializedAsync()
    {
        var specialEntry = await GetDocumentMetadataAsync(InitializeDbEntryName);
        return specialEntry != null;
    }
}
