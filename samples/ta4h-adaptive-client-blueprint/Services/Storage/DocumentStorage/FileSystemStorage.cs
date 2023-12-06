using Newtonsoft.Json;

public class FileSystemStorage : IFileStorage
{
    private readonly string _rootPath;
    private readonly JsonSerializerSettings _jsonSerializationOptions;

    public FileSystemStorage(string rootPath)
    {
        _rootPath = rootPath;
        _jsonSerializationOptions = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };
    }

    public IAsyncEnumerable<string> EnumerateFilesRecursiveAsync(string path = null)
    {
        var rootPathLength = Path.GetFullPath(_rootPath + Path.DirectorySeparatorChar).Length;
        path = path is null ? _rootPath : Path.Combine(_rootPath, path);
        var filenames = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Select(x => x.Substring(rootPathLength));
        return filenames.ToAsyncEnumerable();
    }

    public async Task<string> ReadTextFileAsync(string filePath)
    {
        try
        {
            var fullPath = Path.Combine(_rootPath, filePath);
            return await File.ReadAllTextAsync(fullPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return null;
        }
    }


    public async Task SaveJsonFileAsync<T>(T obj, string filePath)
    {
        try
        {
            var jsonString = JsonConvert.SerializeObject(obj, _jsonSerializationOptions);
            filePath = string.Join(Path.DirectorySeparatorChar, filePath.Split("/"));
            var fullPath = Path.Combine(_rootPath, filePath);
            string directoryName = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }

            await File.WriteAllTextAsync(fullPath, jsonString);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

}
