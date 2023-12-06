using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Newtonsoft.Json;
using System.Text;

public class AzureBlobStorage : IFileStorage
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobContainerClient _containerClient;
    private readonly JsonSerializerSettings _jsonSerializationOptions;

    private const string ConstringAuthentication = "ConnectionString";
    private const string AadAuthentication = "AAD";
    private static string[] ValidAuthenticationMethods = new[] { ConstringAuthentication, AadAuthentication };

    public AzureBlobStorage(AzureBlobStorageSettings settings)
    {
        if (settings.AuthenticationMethod == ConstringAuthentication)
        {
            _blobServiceClient = new BlobServiceClient(settings.ConnectionString);
        }
        else if (settings.AuthenticationMethod == AadAuthentication)
        {
            var credential = new DefaultAzureCredential();
            _blobServiceClient = new BlobServiceClient(new Uri(settings.ConnectionString), credential);
        }
        else
        {
            throw new ConfigurationException("AzureBlobSettings:AuthenticationMethod", settings.AuthenticationMethod, ValidAuthenticationMethods);
        }
        _containerClient = _blobServiceClient.GetBlobContainerClient(settings.ContainerName);
        _containerClient.CreateIfNotExists();
        _jsonSerializationOptions = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };
    }



    public async IAsyncEnumerable<string> EnumerateFilesRecursiveAsync(string prefix = "")
    {
        IAsyncEnumerable<BlobItem> blobItems;

        try
        {
            blobItems = _containerClient.GetBlobsAsync(prefix: prefix);
        }
        catch (RequestFailedException ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            yield break; // ends the method execution if an exception is caught
        }

        await foreach (var blobItem in blobItems)
        {
            yield return blobItem.Name;
        }
    }

    public async Task<string> ReadTextFileAsync(string blobName)
    {
        var blobClient = _containerClient.GetBlobClient(blobName);

        try
        {
            var response = await blobClient.DownloadContentAsync();
            return response.Value.Content.ToString();
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == "BlobNotFound")
        {
            throw new FileNotFoundException("File not found", blobName, ex);
        }
    }


    public async Task SaveJsonFileAsync<T>(T obj, string blobName)
    {
        var blobClient = _containerClient.GetBlobClient(blobName);
        var jsonString = JsonConvert.SerializeObject(obj, _jsonSerializationOptions);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonString));
        await blobClient.UploadAsync(stream, true);
    }

}
