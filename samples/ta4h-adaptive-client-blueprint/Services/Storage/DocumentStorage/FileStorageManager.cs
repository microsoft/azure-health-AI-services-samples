
/// <summary>
/// Interface for a generic File Storage.
/// </summary>
/// 

public class FileStorageManager
{
    public IFileStorage InputStorage { get; set; }

    public IFileStorage OutputStorage { get; set; }

}
