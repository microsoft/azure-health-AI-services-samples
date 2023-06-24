﻿using System.Text.Json;
using System.Text.Json.Serialization;

public class FileSystemStorage : IFileStorage
{
    private readonly string _rootPath;
    private readonly JsonSerializerOptions _jsonSerializationOptions;

    public FileSystemStorage(string rootPath)
    {
        _rootPath = rootPath;
        _jsonSerializationOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public Task<IEnumerable<string>> EnumerateFilesAsync(string location = null)
    {
        var rootPathLength = Path.GetFullPath(_rootPath + Path.DirectorySeparatorChar).Length;
        var path = location is null ? _rootPath : Path.Combine(_rootPath, location);
        var filenames = Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly).Select(x => x.Substring(rootPathLength));
        return Task.FromResult(filenames);
    }

    public Task<IEnumerable<string>> EnumerateFilesRecursiveAsync(string path = null)
    {
        var rootPathLength = Path.GetFullPath(_rootPath + Path.DirectorySeparatorChar).Length;
        path = path is null ? _rootPath : Path.Combine(_rootPath, path);
        var filenames = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Select(x => x.Substring(rootPathLength));
        return Task.FromResult(filenames);
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

    public async Task<T> ReadJsonFileAsync<T>(string filePath)
    {
        try
        {
            var fullPath = Path.Combine(_rootPath, filePath);
            var jsonString = await File.ReadAllTextAsync(fullPath);
            return JsonSerializer.Deserialize<T>(jsonString);
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
            var fullPath = Path.Combine(_rootPath, filePath);

            await File.WriteAllTextAsync(fullPath, text);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    public async Task SaveJsonFileAsync<T>(T obj, string filePath)
    {
        try
        {
            var jsonString = JsonSerializer.Serialize(obj, _jsonSerializationOptions);
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

    public async Task DeleteFileAsync(string filePath)
    {
        try
        {
            var fullPath = Path.Combine(_rootPath, filePath);
            File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    public async Task<bool> FileExistsAsync(string filePath)
    {
        try
        {
            var fullPath = Path.Combine(_rootPath, filePath);
            return File.Exists(fullPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return false;
        }
    }
}