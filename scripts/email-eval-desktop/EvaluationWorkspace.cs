using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pegasus.EmailEvaluation.Desktop;

public sealed record EvaluationLogEntry(
    [property: JsonPropertyName("timestampUtc")] string TimestampUtc,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("sourcePath")] string SourcePath,
    [property: JsonPropertyName("suggestedCategory")] string? SuggestedCategory,
    [property: JsonPropertyName("selectedFamily")] string SelectedFamily,
    [property: JsonPropertyName("selectedCategory")] string SelectedCategory,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("destinationPath")] string DestinationPath);

public sealed class EvaluationWorkspace
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    public EvaluationWorkspace(string selectedFolder)
    {
        SelectedFolder = Path.GetFullPath(selectedFolder);
        OutputRoot = Path.Combine(SelectedFolder, "emailevallocal");
        LogPath = Path.Combine(OutputRoot, "evaluation-log.jsonl");
    }

    public string SelectedFolder { get; }
    public string OutputRoot { get; }
    public string LogPath { get; }

    public void EnsureTaxonomyFolders(CategoryCatalog catalog)
    {
        Directory.CreateDirectory(OutputRoot);
        foreach (var family in catalog.Categories.GroupBy(category => category.Family, StringComparer.Ordinal))
        {
            foreach (var category in family)
            {
                Directory.CreateDirectory(Path.Combine(OutputRoot, family.Key, category.Name));
            }
        }
    }

    public HashSet<string> ReadCompletedSourcePaths()
    {
        var completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(LogPath))
        {
            return completed;
        }

        var lineNumber = 0;
        foreach (var line in File.ReadLines(LogPath))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                throw new InvalidDataException($"The evaluation log contains an empty line at {lineNumber}.");
            }

            EvaluationLogEntry? entry;
            try
            {
                entry = JsonSerializer.Deserialize<EvaluationLogEntry>(line, JsonOptions);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"The evaluation log is malformed at line {lineNumber}.", exception);
            }

            if (entry is null || string.IsNullOrWhiteSpace(entry.SourcePath))
            {
                throw new InvalidDataException($"The evaluation log is missing sourcePath at line {lineNumber}.");
            }

            completed.Add(Path.GetFullPath(entry.SourcePath));
        }

        return completed;
    }

    public string ResolveDestination(string family, string category, string fileName)
    {
        var safeFamily = family is "Received" or "Sent" or "Other"
            ? family
            : throw new ArgumentException("The selected category family is invalid.", nameof(family));
        var safeCategory = ValidateCategoryName(category);
        var safeFileName = Path.GetFileName(fileName);
        if (!string.Equals(safeFileName, fileName, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(safeFileName))
        {
            throw new ArgumentException("The email filename is invalid.", nameof(fileName));
        }

        var directory = Path.Combine(OutputRoot, safeFamily, safeCategory);
        var fullDirectory = Path.GetFullPath(directory);
        if (!IsUnderRoot(fullDirectory))
        {
            throw new InvalidOperationException("The destination is outside the evaluation workspace.");
        }

        return Path.Combine(fullDirectory, safeFileName);
    }

    public string Commit(
        string sourcePath,
        string family,
        string category,
        string? suggestedCategory,
        string reason,
        DateTimeOffset timestampUtc)
    {
        var source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("The source email no longer exists.", source);
        }

        var destination = ResolveDestination(family, category, Path.GetFileName(source));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        try
        {
            File.Copy(source, destination, overwrite: false);
        }
        catch (IOException exception) when (File.Exists(destination))
        {
            throw new IOException("A filed email with this name already exists in the selected category.", exception);
        }

        var entry = new EvaluationLogEntry(
            timestampUtc.ToUniversalTime().ToString("O"),
            Path.GetFileName(source),
            source,
            suggestedCategory,
            family,
            category,
            reason,
            Path.GetFullPath(destination));
        var logExisted = File.Exists(LogPath);
        var originalLogLength = logExisted ? new FileInfo(LogPath).Length : 0;

        try
        {
            var line = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;
            using var stream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            var bytes = Encoding.UTF8.GetBytes(line);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);
        }
        catch (Exception logException)
        {
            Exception? rollbackException = null;
            try
            {
                if (logExisted)
                {
                    using var log = new FileStream(LogPath, FileMode.Open, FileAccess.Write, FileShare.Read);
                    log.SetLength(originalLogLength);
                    log.Flush(flushToDisk: true);
                }
                else
                {
                    File.Delete(LogPath);
                }
            }
            catch (Exception exception)
            {
                rollbackException = exception;
            }

            try
            {
                File.Delete(destination);
            }
            catch (Exception exception)
            {
                rollbackException = rollbackException is null
                    ? exception
                    : new AggregateException(rollbackException, exception);
            }

            if (rollbackException is not null)
            {
                throw new IOException("The evaluation log could not be written and rollback failed.", new AggregateException(logException, rollbackException));
            }

            throw;
        }

        return destination;
    }

    public static string ValidateCategoryName(string category)
    {
        var value = category.Trim();
        if (string.IsNullOrWhiteSpace(value)
            || value is "." or ".."
            || value.EndsWith(' ') || value.EndsWith('.')
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || value.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || value.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("The category name is not a valid Windows folder name.", nameof(category));
        }

        var stem = value.TrimEnd(' ', '.');
        var reserved = stem.Split('.')[0];
        if (reserved.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || reserved.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || reserved.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || reserved.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || (reserved.Length == 4
                && (reserved.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    || reserved.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && reserved[3] is >= '1' and <= '9'))
        {
            throw new ArgumentException("The category name is a reserved Windows device name.", nameof(category));
        }

        return value;
    }

    private bool IsUnderRoot(string path) =>
        path.StartsWith(OutputRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || string.Equals(path, OutputRoot, StringComparison.OrdinalIgnoreCase);
}
