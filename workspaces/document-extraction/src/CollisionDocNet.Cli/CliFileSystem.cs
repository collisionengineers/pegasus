namespace CollisionDocNet.Cli;

internal interface ICliFileSystem
{
    Stream OpenRead(string path);
    Stream CreateNewFile(string path);
    bool FileExists(string path);
    bool DirectoryExists(string path);
    bool IsReparsePoint(string path);
    void CreateDirectory(string path);
    void MoveDirectory(string source, string destination);
    void DeleteDirectory(string path, bool recursive);
    string GetFullPath(string path);
    string GetPathRoot(string path);
    string GetFileName(string path);
    string? GetDirectoryName(string path);
    string GetRandomFileName();
    string Combine(string first, string second);
}

internal sealed class PhysicalCliFileSystem : ICliFileSystem
{
    public static PhysicalCliFileSystem Instance { get; } = new();
    private PhysicalCliFileSystem() { }

    public Stream OpenRead(string path) => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
        16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
    public Stream CreateNewFile(string path) => new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
        16 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough);
    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public bool IsReparsePoint(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public void MoveDirectory(string source, string destination) => Directory.Move(source, destination);
    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);
    public string GetFullPath(string path) => Path.GetFullPath(path);
    public string GetPathRoot(string path) => Path.GetPathRoot(path) ?? string.Empty;
    public string GetFileName(string path) => Path.GetFileName(path);
    public string? GetDirectoryName(string path) => Path.GetDirectoryName(path);
    public string GetRandomFileName() => Path.GetRandomFileName();
    public string Combine(string first, string second) => Path.Combine(first, second);
}
