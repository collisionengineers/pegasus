using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Documents;

/// <summary>
/// Changes the type of a document already filed on a Case, with the reason
/// the actor typed (operator, 22 October 2026; R204). The version is the
/// occurrence's current one, as Mark as original report names it, because a
/// change to Audit report reads that version first.
/// </summary>
public sealed record SetDocumentTypeCommand(
    Guid CaseId,
    Guid OccurrenceId,
    Guid VersionId,
    DocumentSemanticRole NewRole,
    ActionActor Actor,
    string Reason,
    string OperationKey,
    long ExpectedCaseVersion,
    string EditLeaseToken);

public interface ISetDocumentType
{
    Task ExecuteAsync(
        SetDocumentTypeCommand command,
        CancellationToken cancellationToken = default);
}

public interface ISetDocumentTypeStore
{
    /// <summary>
    /// Sets the type under the Case's guards in one transaction and records it
    /// on the Case's timeline. A change to Audit report records the original
    /// report as Mark as original report does, filling the Original report
    /// cells from <paramref name="reading"/>; any other change ignores it.
    /// </summary>
    Task SetDocumentTypeAsync(
        SetDocumentTypeCommand command,
        OriginalReportReading? reading,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The one command for a document's type (R204). A change to Audit report is
/// Mark as original report's act, so the document is read before the store's
/// transaction opens, exactly as <see cref="MarkAsOriginalReport"/> reads it;
/// any other change reads nothing.
/// </summary>
public sealed class SetDocumentType(ISetDocumentTypeStore store, IReadOriginalReport originalReport) : ISetDocumentType
{
    private readonly ISetDocumentTypeStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    private readonly IReadOriginalReport _originalReport =
        originalReport ?? throw new ArgumentNullException(nameof(originalReport));

    public async Task ExecuteAsync(
        SetDocumentTypeCommand command,
        CancellationToken cancellationToken = default)
    {
        DocumentTypePolicy.Validate(command);
        var reading = command.NewRole == DocumentSemanticRole.AuditReport
            ? await _originalReport.ForDocumentAsync(
                command.Actor,
                command.CaseId,
                command.OccurrenceId,
                command.VersionId,
                cancellationToken)
            : null;
        await _store.SetDocumentTypeAsync(command, reading, cancellationToken);
    }
}

/// <summary>
/// Which types a document may be given and the refusals of a change (operator,
/// 22 October 2026; R204): every type except Original source, Audit report
/// included as a second route to Mark as original report for the Automation
/// Actor; a typed reason of up to 500 characters; an issued report's documents
/// and an Audit report document may be retyped. On the Documents tab Audit
/// report is Mark as original report's alone (operator, 30 October 2026; R562).
/// </summary>
public static class DocumentTypePolicy
{
    /// <summary>The reason's bound: what the Case history's reason holds.</summary>
    public const int MaximumReasonLength = 500;

    /// <summary>Every type staff and the Automation Actor may choose: all but Original source.</summary>
    public static IReadOnlyList<DocumentSemanticRole> StaffMayChoose { get; } =
    [
        .. Enum.GetValues<DocumentSemanticRole>()
            .Where(role => role != DocumentSemanticRole.OriginalSource)
    ];

    /// <summary>
    /// The types the Documents tab offers <paramref name="file"/>, read from
    /// the loaded row alone: not the type it has; Image only for a JPEG or PNG
    /// file; never Audit report, which the tab reaches through Mark as
    /// original report (R562). A version that is not current, or was removed,
    /// offers none.
    /// </summary>
    public static IReadOnlyList<DocumentSemanticRole> Choices(CaseFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (!file.Version.IsCurrent || file.Version.IsLogicallyRemoved)
        {
            return [];
        }

        var current = file.Occurrence.SemanticRole;
        return
        [
            .. StaffMayChoose.Where(role => role != current && role switch
            {
                DocumentSemanticRole.Image => InstructionEvidenceImages.IsImage(file.Version.MediaType),
                DocumentSemanticRole.AuditReport => false,
                _ => true
            })
        ];
    }

    public static void Validate(SetDocumentTypeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        StaffAuthorization.Require(command.Actor, StaffAccessRight.PerformCasework);
        if (command.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(command));
        }
        if (command.OccurrenceId == Guid.Empty)
        {
            throw new ArgumentException("A document occurrence is required.", nameof(command));
        }
        if (command.VersionId == Guid.Empty)
        {
            throw new ArgumentException("A document version is required.", nameof(command));
        }
        if (!StaffMayChoose.Contains(command.NewRole))
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                "The document type cannot be chosen.");
        }
        if (command.ExpectedCaseVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                "The expected case version cannot be negative.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(command.OperationKey);
        if (command.OperationKey.Trim().Length > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                "The operation key cannot exceed 100 characters.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(command.EditLeaseToken);
        RequireReason(command.Reason);
    }

    /// <summary>The reason as recorded: required, trimmed, at most <see cref="MaximumReasonLength"/>.</summary>
    public static string RequireReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to change a document's type.", nameof(reason));
        }

        var trimmed = reason.Trim();
        return trimmed.Length <= MaximumReasonLength
            ? trimmed
            : throw new ArgumentOutOfRangeException(
                nameof(reason),
                $"The reason cannot exceed {MaximumReasonLength} characters.");
    }

    /// <summary>
    /// The refusals the store applies to the loaded document before it writes:
    /// the type it already has, and Image for a file that is not a JPEG or PNG.
    /// A change to Audit report is also held to Mark as original report's own
    /// rules (<see cref="OriginalReportPolicy.RequireEligible"/>).
    /// </summary>
    public static void RequireChange(
        DocumentSemanticRole currentRole,
        DocumentSemanticRole newRole,
        string mediaType)
    {
        if (newRole == currentRole)
        {
            throw new InvalidOperationException("The document already has that type.");
        }
        if (newRole == DocumentSemanticRole.Image && !InstructionEvidenceImages.IsImage(mediaType))
        {
            throw new InvalidOperationException("Only a JPEG or PNG file may have the Image type.");
        }
    }
}
