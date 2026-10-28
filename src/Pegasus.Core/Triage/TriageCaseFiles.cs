using Pegasus.Core.Documents;

namespace Pegasus.Core.Triage;

/// <summary>Processes one queued copy of a linked Triage Case's files onto its instruction Case.</summary>
public interface IProcessQueuedTriageCaseFiles
{
    Task ExecuteAsync(Guid workItemId, CancellationToken cancellationToken);
}

/// <summary>One current file of a Triage Case: its occurrence, custody hash and role.</summary>
public sealed record TriageCaseFile(Guid OccurrenceId, string Sha256, DocumentSemanticRole SemanticRole);

/// <summary>
/// What a Triage Case's link to its instruction Case carries onto that Case
/// (operator, 26 October 2026): every current file of the Triage Case — its
/// photographs, documents and correspondence — in document order, except one
/// whose content the Case already holds. The Triage Case keeps its own files,
/// and an unlink takes nothing back. The copied photographs complete the
/// Case's images. The finding travels by
/// <see cref="TriageFindingFill"/>.
/// </summary>
public static class TriageCaseFilesPolicy
{
    public static IReadOnlyList<TriageCaseFile> SelectFilesToCopy(
        IReadOnlyList<TriageCaseFile> triageFiles,
        IEnumerable<string> caseFileSha256)
    {
        ArgumentNullException.ThrowIfNull(triageFiles);
        ArgumentNullException.ThrowIfNull(caseFileSha256);
        var held = new HashSet<string>(caseFileSha256, StringComparer.OrdinalIgnoreCase);
        return triageFiles.Where(file => held.Add(file.Sha256)).ToArray();
    }

    public static bool CompletesImages(IReadOnlyList<TriageCaseFile> copied)
    {
        ArgumentNullException.ThrowIfNull(copied);
        return copied.Any(file => file.SemanticRole == DocumentSemanticRole.Image);
    }
}
