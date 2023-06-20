public class AzureBlobStorageSettings
{
    /// <summary>
    /// Connection string fot Azure Storage
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// The name of blob container where the documents are stored
    /// </summary>
    public string ContainerName { get; set; }
}

