public class SQLMetadataStorageSettings
{
    /// <summary>
    /// Connection string for SQL db
    /// </summary>
    public string ConnectionString { get; set; }

    public string AuthenticationMethod { get; set; } = "Password";

    public string TableName { get; set; } = "DocumentMetadata";

}


