public class AzureTableMetadataStorageSettings
{
    /// <summary>
    /// Connection string for SQL db
    /// </summary>
    public string ConnectionString { get; set; }

    public string AuthenticationMethod { get; set; } = "ConnectionString";

    public string TableName { get; set; } = "DocumentMetadata";

}


