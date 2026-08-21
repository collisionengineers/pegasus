using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Presentation;

/// <summary>
/// One suggestion the case search offers as the operator types. The id is a
/// form value only; every displayed part is the business reference and its
/// surrounding facts, never an internal identifier.
/// </summary>
public sealed record UploadCaseSuggestion(
    Guid CaseId,
    string Reference,
    string? Registration,
    string? Claimant,
    string Stage);

public sealed record UploadCaseAttachResult(bool Succeeded, string Message, Guid? CaseId = null);

/// <summary>
/// The one implementation behind both upload status pages' confirmation
/// decision: the case search that feeds the autocomplete, and the explicit
/// staff decision to add uploaded material to a found case. Orchestration
/// only — the search is <see cref="ISearchCases"/>, the attach is the
/// existing leased, replay-protected <see cref="ILinkIntake"/> (which itself
/// brings a registered Image-initiated Case through its merge transition), so
/// no business rule lives here.
/// </summary>
public interface IUploadCaseDecision
{
    Task<IReadOnlyList<UploadCaseSuggestion>> SearchAsync(
        string term,
        ActionActor actor,
        CancellationToken cancellationToken = default);

    /// <param name="caseId">The case chosen from the suggestions, when script filled it in.</param>
    /// <param name="reference">
    /// The typed case reference, used only when no <paramref name="caseId"/>
    /// arrived (the form works without script); it must resolve to exactly
    /// one case or the decision fails closed.
    /// </param>
    Task<UploadCaseAttachResult> AttachAsync(
        Guid receiptId,
        Guid? caseId,
        string? reference,
        string reason,
        ActionActor actor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The submission-level decision: add every still-open member of an
    /// upload group to one found case, under one reason. Members already on
    /// the chosen case are counted as done (replay safety); a member on a
    /// different case is left untouched and reported.
    /// </summary>
    Task<UploadCaseAttachResult> AttachGroupAsync(
        Guid groupId,
        IReadOnlyList<Guid> memberReceiptIds,
        Guid? caseId,
        string? reference,
        string reason,
        ActionActor actor,
        CancellationToken cancellationToken = default);
}

public sealed class UploadCaseDecision(
    ISearchCases searchCases,
    IGetCase getCase,
    IGetIntake getIntake,
    IAcquireCaseEditLease acquireCaseEditLease,
    ILinkIntake linkIntake) : IUploadCaseDecision
{
    private const int SuggestionLimit = 8;

    public async Task<IReadOnlyList<UploadCaseSuggestion>> SearchAsync(
        string term,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        var trimmed = term?.Trim() ?? string.Empty;
        if (trimmed.Length < 2)
        {
            return [];
        }

        var result = await searchCases.ExecuteAsync(
            new(actor, new(Query: trimmed), Page: 1, PageSize: SuggestionLimit),
            cancellationToken);
        return result.Items
            .Select(item => new UploadCaseSuggestion(
                item.CaseId,
                item.Reference,
                item.Registration,
                item.Claimant,
                OperatorLabels.CaseStage(item.State)))
            .ToArray();
    }

    public async Task<UploadCaseAttachResult> AttachAsync(
        Guid receiptId,
        Guid? caseId,
        string? reference,
        string reason,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        var receipt = await getIntake.ExecuteAsync(new(receiptId, actor), cancellationToken);
        if (receipt is null)
        {
            return new(false, "The uploaded item could not be found. Refresh and try again.");
        }

        var resolvedCaseId = caseId
            ?? await ResolveReferenceAsync(reference, actor, cancellationToken);
        if (resolvedCaseId is not { } targetCaseId || targetCaseId == Guid.Empty)
        {
            return new(false, "No single case matched that reference. Search and choose a case from the suggestions.");
        }

        // Replay safety at the flow level, checked before the heavier case
        // read: a repeated submission of a decision that already took effect
        // reports the same success rather than attempting a second mutation,
        // and the receipt already carries the reference for both messages.
        if (receipt.CurrentCaseId == targetCaseId)
        {
            return new(
                true,
                OperatorLabels.AssociatedWithCase(receipt.CurrentCaseReference, byStaffDecision: true),
                targetCaseId);
        }
        if (receipt.CurrentCaseId is not null)
        {
            return new(
                false,
                receipt.CurrentCaseReference is { } currentReference
                    ? $"This is already associated with case {currentReference}. Open the received item to change that association."
                    : "This is already associated with a case. Open the received item to change that association.");
        }

        var details = await getCase.ExecuteAsync(new(targetCaseId, actor), cancellationToken);
        if (details is null)
        {
            return new(false, "That case could not be found. Search and choose a case from the suggestions.");
        }

        try
        {
            var lease = await acquireCaseEditLease.ExecuteAsync(
                new(
                    targetCaseId,
                    details.Workflow.Version,
                    actor,
                    $"upload-attach-lease:{receiptId:N}:{targetCaseId:N}"),
                cancellationToken);
            await linkIntake.ExecuteAsync(
                new(
                    receiptId,
                    targetCaseId,
                    receipt.Version,
                    lease.Version,
                    lease.Token,
                    actor,
                    $"upload-attach:{receiptId:N}:{targetCaseId:N}",
                    reason),
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or KeyNotFoundException
            || IntakeExceptionPolicy.IsRecoverable(exception))
        {
            return new(false, "The uploaded material could not be added to that case. Refresh and try again.");
        }

        return new(
            true,
            OperatorLabels.AssociatedWithCase(details.Summary.Reference, byStaffDecision: true),
            targetCaseId);
    }

    public async Task<UploadCaseAttachResult> AttachGroupAsync(
        Guid groupId,
        IReadOnlyList<Guid> memberReceiptIds,
        Guid? caseId,
        string? reference,
        string reason,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memberReceiptIds);
        if (memberReceiptIds.Count == 0)
        {
            return new(false, "There is nothing left in this submission to add.");
        }

        var resolvedCaseId = caseId
            ?? await ResolveReferenceAsync(reference, actor, cancellationToken);
        if (resolvedCaseId is not { } targetCaseId || targetCaseId == Guid.Empty)
        {
            return new(false, "No single case matched that reference. Search and choose a case from the suggestions.");
        }

        var details = await getCase.ExecuteAsync(new(targetCaseId, actor), cancellationToken);
        if (details is null)
        {
            return new(false, "That case could not be found. Search and choose a case from the suggestions.");
        }

        var added = 0;
        var alreadyThere = 0;
        var elsewhere = 0;
        try
        {
            foreach (var receiptId in memberReceiptIds)
            {
                var receipt = await getIntake.ExecuteAsync(new(receiptId, actor), cancellationToken);
                if (receipt is null)
                {
                    continue;
                }
                if (receipt.CurrentCaseId == targetCaseId)
                {
                    alreadyThere++;
                    continue;
                }
                if (receipt.CurrentCaseId is not null)
                {
                    elsewhere++;
                    continue;
                }

                // Each link consumes its lease and advances the case version,
                // so every member takes a fresh case read and its own lease —
                // the same per-mutation contract the single-file decision uses.
                var current = added == 0
                    ? details
                    : await getCase.ExecuteAsync(new(targetCaseId, actor), cancellationToken)
                        ?? throw new InvalidOperationException("The case is no longer available.");
                // The same replay identities as the single-file decision:
                // adding this member to this case is one operation however
                // the operator reached it.
                var lease = await acquireCaseEditLease.ExecuteAsync(
                    new(
                        targetCaseId,
                        current.Workflow.Version,
                        actor,
                        $"upload-attach-lease:{receiptId:N}:{targetCaseId:N}"),
                    cancellationToken);
                await linkIntake.ExecuteAsync(
                    new(
                        receiptId,
                        targetCaseId,
                        receipt.Version,
                        lease.Version,
                        lease.Token,
                        actor,
                        $"upload-attach:{receiptId:N}:{targetCaseId:N}",
                        reason),
                    cancellationToken);
                added++;
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or KeyNotFoundException
            || IntakeExceptionPolicy.IsRecoverable(exception))
        {
            return new(false, "The submission could not be added to that case. Refresh and try again.");
        }

        if (added == 0 && alreadyThere == 0)
        {
            return new(false, "Nothing from this submission could be added to that case.");
        }

        var message = OperatorLabels.AssociatedWithCase(details.Summary.Reference, byStaffDecision: true);
        if (elsewhere > 0)
        {
            message += elsewhere == 1
                ? " One file was already on a different case and was left there."
                : $" {elsewhere} files were already on a different case and were left there."; 
        }
        return new(true, message, targetCaseId);
    }

    private async Task<Guid?> ResolveReferenceAsync(
        string? reference,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        var trimmed = reference?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        var result = await searchCases.ExecuteAsync(
            new(actor, new(CaseReference: trimmed), Page: 1, PageSize: 2),
            cancellationToken);
        var exact = result.Items
            .Where(item => string.Equals(item.Reference, trimmed, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var candidates = exact.Length > 0 ? exact : result.Items.ToArray();
        return candidates.Length == 1 ? candidates[0].CaseId : null;
    }
}
