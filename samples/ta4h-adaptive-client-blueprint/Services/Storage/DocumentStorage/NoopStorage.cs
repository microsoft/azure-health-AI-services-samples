public class NoopStorage : IFileStorage
{

    public IAsyncEnumerable<string> EnumerateFilesRecursiveAsync(string path = null)
    {
        return Enumerable.Empty<string>().ToAsyncEnumerable();
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