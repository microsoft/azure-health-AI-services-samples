
/// <summary>
/// Interface for a generic File Storage.
/// </summary>
/// 

public interface IFileStorage
{
    /// <summary>
    /// Enumerate all files in a specific location.
    /// </summary>
    /// <param name="location">The location to enumerate files from.</param>
    /// <returns>A list of file paths.</returns>
    Task<IEnumerable<string>> EnumerateFilesAsync(string location = null);

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
    /// Reads a JSON file from a given file path and parse it into an object of a generic type T.
    /// </summary>
    /// <param name="filePath">The path of the JSON file to be read.</param>
    /// <typeparam name="T">The type of object the JSON file will be deserialized to.</typeparam>
    /// <returns>An object of type T that represents the JSON file.</returns>
    Task<T> ReadJsonFileAsync<T>(string filePath);

    /// <summary>
    /// Saves a text file, given a text string and a path.
    /// </summary>
    /// <param name="text">The text to be saved.</param>
    /// <param name="filePath">The path where the text will be saved.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SaveTextFileAsync(string text, string filePath);

    /// <summary>
    /// Saves a JSON file, given an object of type T and a path.
    /// </summary>
    /// <param name="obj">The object to be serialized and saved as JSON.</param>
    /// <param name="filePath">The path where the JSON file will be saved.</param>
    /// <typeparam name="T">The type of object that will be serialized to JSON.</typeparam>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SaveJsonFileAsync<T>(T obj, string filePath);

    /// <summary>
    /// Deletes a file at the specified path.
    /// </summary>
    /// <param name="filePath">The path of the file to be deleted.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DeleteFileAsync(string filePath);

    /// <summary>
    /// Checks if a file exists at the specified path.
    /// </summary>
    /// <param name="filePath">The path of the file to check.</param>
    /// <returns>A boolean indicating whether the file exists or not.</returns>
    Task<bool> FileExistsAsync(string filePath);
}