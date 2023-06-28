using Azure;
using Azure.Storage.Blobs;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public class AzureBlobStorage : IFileStorage
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobContainerClient _containerClient;
    private readonly JsonSerializerOptions _jsonSerializationOptions;

    public AzureBlobStorage(string connectionString, string containerName)
    {
        _blobServiceClient = new BlobServiceClient(connectionString);
        _containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        _jsonSerializationOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }


    public async Task<IEnumerable<string>> EnumerateFilesRecursiveAsync(string prefix = "")
    {
        var blobs = new List<string>();

        try
        {
            await foreach (var blobItem in _containerClient.GetBlobsAsync(prefix: prefix))
            {
                blobs.Add(blobItem.Name);
            }
        }
        catch (RequestFailedException ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }

        return blobs;
    }

    public async Task<string> ReadTextFileAsync(string blobName)
    {
        var blobClient = _containerClient.GetBlobClient(blobName);

        try
        {
            var response = await blobClient.DownloadContentAsync();
            return response.Value.Content.ToString();
        }
        catch (RequestFailedException ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return null;
        }
    }


    public async Task SaveJsonFileAsync<T>(T obj, string blobName)
    {
        var blobClient = _containerClient.GetBlobClient(blobName);

        try
        {
            var jsonString = JsonSerializer.Serialize(obj, _jsonSerializationOptions);
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonString));
            await blobClient.UploadAsync(stream, true);
        }
        catch (RequestFailedException ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

}
