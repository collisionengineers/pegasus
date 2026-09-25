using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Documents;

/// <summary>
/// Marks a filed document as the Audit's original report and fills the
/// Original report cells from its own reading (v28 P51). The document is read
/// before the store's transaction opens; a document that cannot be read is
/// still marked, and its cells stay hand-entered.
/// </summary>
public sealed class MarkAsOriginalReport(IMarkAsOriginalReportStore store, IReadOriginalReport originalReport)
{
    private readonly IMarkAsOriginalReportStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    private readonly IReadOriginalReport _originalReport =
        originalReport ?? throw new ArgumentNullException(nameof(originalReport));

    public async Task<OriginalReportRecorded> ExecuteAsync(
        MarkAsOriginalReportCommand command,
        CancellationToken cancellationToken = default)
    {
        OriginalReportPolicy.ValidateRequest(command);
        var reading = await _originalReport.ForDocumentAsync(
            command.Actor,
            command.CaseId,
            command.DocumentOccurrenceId,
            command.DocumentVersionId,
            cancellationToken);
        return await _store.MarkAsOriginalReportAsync(command, reading, cancellationToken);
    }
}

public static class OriginalReportPolicy
{
    public static void ValidateRequest(MarkAsOriginalReportCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        StaffAuthorization.Require(command.Actor, StaffAccessRight.PerformCasework);
        if (command.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(command));
        }
        if (command.DocumentOccurrenceId == Guid.Empty)
        {
            throw new ArgumentException("A document occurrence is required.", nameof(command));
        }
        if (command.DocumentVersionId == Guid.Empty)
        {
            throw new ArgumentException("A document version is required.", nameof(command));
        }
        if (command.ExpectedVersion < 0)
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
    }

    public static void RequireEligible(
        CaseType caseType,
        CaseLifecycleState state,
        DocumentSemanticRole currentRole)
    {
        if (caseType != CaseType.Audit)
        {
            throw new InvalidOperationException(
                "An original report can be recorded only for an Audit case.");
        }
        if (CaseLifecycleRules.IsClosed(state))
        {
            throw new InvalidOperationException(
                "An original report can be recorded only for an open Audit case.");
        }
        if (currentRole == DocumentSemanticRole.Image)
        {
            throw new InvalidOperationException(
                "An image cannot be marked as the original report.");
        }
    }
}
