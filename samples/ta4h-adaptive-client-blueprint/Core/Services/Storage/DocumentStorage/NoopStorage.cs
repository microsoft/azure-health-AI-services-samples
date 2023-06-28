public class NoopStorage : IFileStorage
{

    public Task<IEnumerable<string>> EnumerateFilesRecursiveAsync(string path = null)
    {
        return Task.FromResult(Enumerable.Empty<string>());
    }


    public Task<string> ReadTextFileAsync(string filePath)
    {
        return Task.FromResult<string>(null);
    }

    public Task SaveJsonFileAsync<T>(T obj, string filePath)
    {
        return Task.CompletedTask;
    }

}