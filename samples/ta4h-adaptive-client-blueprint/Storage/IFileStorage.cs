
/// <summary>
/// Interface for a generic File Storage.
/// </summary>
/// 

public interface IFileStorage
{
    /// <summary>
    /// Enumerate all files under a specific path / prefix, recursively.
    /// </summary>
    /// <param name="path">The path to start enumerating files from.</param>
    /// <returns>A list of file paths.</returns>
    Task<IEnumerable<string>> EnumerateFilesRecursiveAsync(string path = null);

    /// <summary>
    /// Reads a text file from a given file path.
    /// </summary>
    /// <param name="filePath">The path of the file to be read.</param>
    /// <returns>The contents of the file as a string.</returns>
    Task<string> ReadTextFileAsync(string filePath);


    /// <summary>
    /// Saves a JSON file, given an object of type T and a path.
    /// </summary>
    /// <param name="obj">The object to be serialized and saved as JSON.</param>
    /// <param name="filePath">The path where the JSON file will be saved.</param>
    /// <typeparam name="T">The type of object that will be serialized to JSON.</typeparam>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SaveJsonFileAsync<T>(T obj, string filePath);

}