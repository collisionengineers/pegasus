namespace CollisionDocNet.Cli;

internal static class CliPathPolicy
{
    internal static string ResolveInput(string input, ICliFileSystem fileSystem)
    {
        RejectRemoteOrDevice(input);
        string fullPath = fileSystem.GetFullPath(input);
        RejectRemoteOrDevice(fullPath);
        if (!fileSystem.FileExists(fullPath) || fileSystem.DirectoryExists(fullPath))
        {
            throw new IOException("The selected input is not a readable regular file.");
        }
        RejectReparseChain(fullPath, fileSystem);
        return fullPath;
    }

    internal static string ResolveNewOutput(string output, ICliFileSystem fileSystem)
    {
        RejectRemoteOrDevice(output);
        string destination = fileSystem.GetFullPath(output);
        RejectRemoteOrDevice(destination);
        if (fileSystem.DirectoryExists(destination) || fileSystem.FileExists(destination))
        {
            throw new IOException("The output path already exists.");
        }
        string parent = fileSystem.GetDirectoryName(destination) ?? throw new IOException("The output path has no parent directory.");
        if (!fileSystem.DirectoryExists(parent)) throw new IOException("The output parent directory does not exist.");
        RejectReparseChain(parent, fileSystem);
        RequireDirectChild(parent, destination, fileSystem);
        return destination;
    }

    internal static void RequireDirectChild(string parent, string candidate, ICliFileSystem fileSystem)
    {
        string canonicalParent = fileSystem.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string canonicalCandidate = fileSystem.GetFullPath(candidate);
        string? candidateParent = fileSystem.GetDirectoryName(canonicalCandidate);
        if (!string.Equals(canonicalParent, candidateParent?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new IOException("A generated path escaped the selected output parent.");
        }
    }

    private static void RejectRemoteOrDevice(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && !uri.IsFile ||
            value.Contains("://", StringComparison.Ordinal) ||
            value.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("\\\\", StringComparison.Ordinal) ||
            value.StartsWith("//", StringComparison.Ordinal) ||
            value.StartsWith("\\\\?\\", StringComparison.Ordinal) ||
            value.StartsWith("\\\\.\\", StringComparison.Ordinal))
        {
            throw new ArgumentException("URI, UNC, network and device paths are not supported.", nameof(value));
        }
    }

    private static void RejectReparseChain(string path, ICliFileSystem fileSystem)
    {
        string current = fileSystem.GetFullPath(path);
        string root = fileSystem.GetPathRoot(current).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        while (!string.IsNullOrEmpty(current) && !string.Equals(current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                   root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            if ((fileSystem.FileExists(current) || fileSystem.DirectoryExists(current)) && fileSystem.IsReparsePoint(current))
            {
                throw new IOException("Reparse points and symbolic links are not accepted at the CLI boundary.");
            }
            current = fileSystem.GetDirectoryName(current) ?? string.Empty;
        }
    }
}
