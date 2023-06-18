using Azure;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using System.Text.Json;

public class AzureFileShareStorage : IFileStorage
{
    private readonly ShareClient _shareClient;
    private readonly ShareDirectoryClient _rootDirectoryClient;

    public AzureFileShareStorage(string connectionString, string shareName)
    {
        _shareClient = new ShareClient(connectionString, shareName);
        _rootDirectoryClient = _shareClient.GetRootDirectoryClient();
    }

    public async Task<IEnumerable<string>> EnumerateFilesAsync(string directoryPath = "")
    {
        var fileList = new List<string>();

        try
        {
            var directoryClient = _rootDirectoryClient.GetSubdirectoryClient(directoryPath);

            await foreach (ShareFileItem item in directoryClient.GetFilesAndDirectoriesAsync())
            {
                if (!item.IsDirectory)
                {
                    fileList.Add(item.Name);
                }
            }
        }
        catch (RequestFailedException ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }

        return fileList;
    }

    public async Task<IEnumerable<string>> EnumerateFilesRecursiveAsync(string directoryPath = "")
    {
        // This method uses recursion to enumerate files in subdirectories.
        var fileList = new List<string>();

        async Task EnumerateFilesInDirectory(string path)
        {
            var directoryClient = string.IsNullOrEmpty(path) ? _rootDirectoryClient : _rootDirectoryClient.GetSubdirectoryClient(path);

            await foreach (ShareFileItem item in directoryClient.GetFilesAndDirectoriesAsync())
            {
                if (item.IsDirectory)
                {
                    await EnumerateFilesInDirectory(Path.Combine(path, item.Name));
                }
                else
                {
                    fileList.Add(Path.Combine(path, item.Name));
                }
            }
        }

        try
        {
            await EnumerateFilesInDirectory(directoryPath);
        }
        catch (RequestFailedException ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }

        return fileList;
    }

    public async Task<string> ReadTextFileAsync(string filePath)
    {
        try
        {
            var fileClient = _rootDirectoryClient.GetFileClient(filePath);
            var response = await fileClient.DownloadAsync();
            using var streamReader = new StreamReader(response.Value.Content);
            return await streamReader.ReadToEndAsync();
        }
        catch (RequestFailedException ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return null;
        }
    }

    public async Task<T> ReadJsonFileAsync<T>(string filePath)
    {
        try
        {
            var content = await ReadTextFileAsync(filePath);
            return JsonSerializer.Deserialize<T>(content);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return default;
        }
    }

    public async Task SaveTextFileAsync(string text, string filePath)
    {
        try
        {
            var fileClient = _rootDirectoryClient.GetFileClient(filePath);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));
            await fileClient.CreateAsync(stream.Length);
            await fileClient.UploadRangeAsync(new HttpRange(0, stream.Length), stream);
        }
        catch (RequestFailedException ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    public async Task SaveJsonFileAsync<T>(T obj, string filePath)
    {
        try
        {
            var content = JsonSerializer.Serialize(obj);
            await SaveTextFileAsync(content, filePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    public async Task DeleteFileAsync(string filePath)
    {
        try
        {
            var fileClient = _rootDirectoryClient.GetFileClient(filePath);
            await fileClient.DeleteAsync();
        }
        catch (RequestFailedException ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    public async Task<bool> FileExistsAsync(string filePath)
    {
        try
        {
            var fileClient = _rootDirectoryClient.GetFileClient(filePath);
            await fileClient.GetPropertiesAsync();
            return true;
        }
        catch (RequestFailedException ex)
        {
            if (ex.Status == 404) // Not Found
            {
                return false;
            }
            Console.WriteLine($"An error occurred: {ex.Message}");
            return false;
        }
    }
}
