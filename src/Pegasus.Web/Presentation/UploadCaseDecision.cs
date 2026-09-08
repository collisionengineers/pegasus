using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using System.Security.Cryptography;
using System.Text;
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
    string Stage,
    long? Version = null);

public sealed record UploadCaseAttachResult(
    bool Succeeded,
    string Message,
    Guid? CaseId = null,
    int CompletedMembers = 0);

/// <summary>The reviewed identity carried by an attach confirmation form.</summary>
public sealed record UploadCaseAttachmentInput(
    Guid OperationId,
    long ExpectedReceiptVersion,
    long ExpectedCaseVersion);

/// <summary>
/// The server-rendered second step used when a non-script form supplies a
/// typed reference. Resolving text is not consent to write.
/// </summary>
public sealed record UploadCaseAttachmentConfirmation(
    Guid ReceiptId,
    Guid CaseId,
    string Reference,
    string Reason,
    UploadCaseAttachmentInput Input);

/// <summary>
/// Values a failed first confirmation must retain.  This is deliberately not
/// an authority token: the next post still resolves the receipt and target
/// afresh.  It only prevents a recoverable validation failure from silently
/// discarding the staff member's explanation and chosen reference.
/// </summary>
public sealed record UploadCaseAttachmentDraft(
    Guid ReceiptId,
    Guid OperationId,
    long? ExpectedReceiptVersion,
    Guid? CaseId,
    long? ExpectedCaseVersion,
    string? Reference,
    string Reason);

/// <summary>
/// The one implementation behind both upload status pages' confirmation
/// decision: the receipt-scoped case search that feeds the autocomplete, and
/// the explicit staff decision to add uploaded material to a found case.
/// Orchestration only — exact typed-reference resolution uses
/// <see cref="ISearchCases"/>, while displayed destinations come from the
/// receipt-scoped policy port; the attach is the existing leased,
/// replay-protected <see cref="ILinkIntake"/> (which itself
/// brings a registered Image-initiated Case through its merge transition), so
/// no business rule lives here.
/// </summary>
public interface IUploadCaseDecision
{
    Task<IReadOnlyList<UploadCaseSuggestion>> SearchForUploadAsync(
        Guid receiptId,
        string term,
        ActionActor actor,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UploadCaseSuggestion>> SearchForUploadsAsync(
        IReadOnlyList<Guid> receiptIds,
        string term,
        ActionActor actor,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UploadCaseSuggestion>> GetSuggestionsForUploadAsync(
        Guid receiptId,
        ActionActor actor,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UploadCaseSuggestion>> GetSuggestionsForUploadsAsync(
        IReadOnlyList<Guid> receiptIds,
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
        UploadCaseAttachmentInput input,
        ActionActor actor,
        CancellationToken cancellationToken = default);

    Task<UploadCaseAttachmentConfirmation?> PrepareAsync(
        Guid receiptId,
        string? reference,
        string reason,
        Guid operationId,
        long expectedReceiptVersion,
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
        Guid operationId,
        IReadOnlyDictionary<Guid, long> expectedReceiptVersions,
        long expectedCaseVersion,
        ActionActor actor,
        CancellationToken cancellationToken = default);
}

public sealed class UploadCaseDecision(
    ISearchCases searchCases,
    IGetIntake getIntake,
    IAcquireCaseEditLease acquireCaseEditLease,
    ILinkIntake linkIntake,
    IIntakeAssociationDestinationQueries destinations) : IUploadCaseDecision
{
    public async Task<IReadOnlyList<UploadCaseSuggestion>> SearchForUploadAsync(
        Guid receiptId,
        string term,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        return await SearchForUploadsAsync([receiptId], term, actor, cancellationToken);
    }

    public async Task<IReadOnlyList<UploadCaseSuggestion>> SearchForUploadsAsync(
        IReadOnlyList<Guid> receiptIds,
        string term,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        if (receiptIds is null || receiptIds.Count == 0 || receiptIds.Any(id => id == Guid.Empty))
        {
            return [];
        }

        IReadOnlyDictionary<Guid, IntakeAssociationDestination>? common = null;
        foreach (var receiptId in receiptIds.Distinct())
        {
            var receipt = await getIntake.ExecuteAsync(new(receiptId, actor), cancellationToken);
            if (receipt is null)
            {
                return [];
            }

            var candidates = (await destinations.SearchAsync(receipt, term, actor, cancellationToken))
                .ToDictionary(item => item.CaseId);
            common = common is null
                ? candidates
                : common.Where(item => candidates.ContainsKey(item.Key))
                    .ToDictionary(item => item.Key, item => item.Value);
            if (common.Count == 0)
            {
                return [];
            }
        }

        return common!.Values.Select(item => new UploadCaseSuggestion(
                item.CaseId,
                item.Reference,
                item.Registration,
                item.Claimant,
                OperatorLabels.CaseStage(item.State),
                item.Version))
            .ToArray();
    }

    public async Task<IReadOnlyList<UploadCaseSuggestion>> GetSuggestionsForUploadAsync(
        Guid receiptId,
        ActionActor actor,
        CancellationToken cancellationToken = default) =>
        await GetSuggestionsForUploadsAsync([receiptId], actor, cancellationToken);

    public async Task<IReadOnlyList<UploadCaseSuggestion>> GetSuggestionsForUploadsAsync(
        IReadOnlyList<Guid> receiptIds,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        if (receiptIds is null || receiptIds.Count == 0 || receiptIds.Any(id => id == Guid.Empty))
        {
            return [];
        }

        IReadOnlyDictionary<Guid, IntakeAssociationDestination>? common = null;
        foreach (var receiptId in receiptIds.Distinct())
        {
            var receipt = await getIntake.ExecuteAsync(new(receiptId, actor), cancellationToken);
            if (receipt is null)
            {
                return [];
            }

            var candidates = (await destinations.GetSuggestedAsync(receipt, actor, cancellationToken))
                .ToDictionary(item => item.CaseId);
            common = common is null
                ? candidates
                : common.Where(item => candidates.ContainsKey(item.Key))
                    .ToDictionary(item => item.Key, item => item.Value);
            if (common.Count == 0)
            {
                return [];
            }
        }

        return common!.Values.Select(item => new UploadCaseSuggestion(
                item.CaseId,
                item.Reference,
                item.Registration,
                item.Claimant,
                OperatorLabels.CaseStage(item.State),
                item.Version))
            .ToArray();
    }

    public async Task<UploadCaseAttachmentConfirmation?> PrepareAsync(
        Guid receiptId,
        string? reference,
        string reason,
        Guid operationId,
        long expectedReceiptVersion,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        if (operationId == Guid.Empty || expectedReceiptVersion < 0)
        {
            return null;
        }

        var receipt = await getIntake.ExecuteAsync(new(receiptId, actor), cancellationToken);
        if (receipt is null || receipt.Version != expectedReceiptVersion)
        {
            return null;
        }

        var targetCaseId = await ResolveReferenceAsync(reference, actor, cancellationToken);
        if (targetCaseId is not { } resolved || resolved == Guid.Empty)
        {
            return null;
        }

        var destination = await destinations.GetAsync(receipt, resolved, actor, cancellationToken);
        return destination is null
            ? null
            : new(
                receiptId,
                destination.CaseId,
                destination.Reference,
                reason,
                new(operationId, receipt.Version, destination.Version));
    }

    public async Task<UploadCaseAttachResult> AttachAsync(
        Guid receiptId,
        Guid? caseId,
        string? reference,
        string reason,
        UploadCaseAttachmentInput input,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        var receipt = await getIntake.ExecuteAsync(new(receiptId, actor), cancellationToken);
        if (receipt is null)
        {
            return new(false, "The uploaded item could not be found. Refresh and try again.");
        }

        if (input.OperationId == Guid.Empty || input.ExpectedReceiptVersion < 0 || input.ExpectedCaseVersion < 0)
        {
            return new(false, "This confirmation is incomplete. Refresh and choose the case again.");
        }
        // Typed input without a target version must take the rendered
        // confirmation step prepared by the page handler; it cannot write.
        if (caseId is null)
        {
            return new(false, "Review the selected case and confirm the decision.");
        }

        var resolvedCaseId = caseId;
        if (resolvedCaseId is not { } targetCaseId || targetCaseId == Guid.Empty)
        {
            return new(false, "No single case matched that reference. Search and choose a case from the suggestions.");
        }

        var operationKey = DecisionOperationKey(
            "upload-attach", input.OperationId, receiptId, targetCaseId,
            input.ExpectedReceiptVersion, input.ExpectedCaseVersion, actor, reason);

        // A matching Case alone is not proof of a replay: another staff
        // member, another reason, or a prior action must remain visible as a
        // conflict. The persisted operation identity is the exact staff
        // decision that is safe to acknowledge again.
        if (receipt.CurrentCaseId == targetCaseId)
        {
            if (!string.Equals(receipt.ManualAssociationOperationKey, operationKey, StringComparison.Ordinal))
            {
                return new(false, "This item is already associated with that case by a different decision. Refresh to review it.");
            }
            return new(
                true,
                OperatorLabels.AssociatedWithCase(receipt.CurrentCaseReference, byStaffDecision: true),
                targetCaseId);
        }
        if (receipt.Version != input.ExpectedReceiptVersion)
        {
            return new(false, "This upload changed after it was reviewed. Refresh and try again.");
        }
        if (receipt.CurrentCaseId is not null)
        {
            return new(
                false,
                receipt.CurrentCaseReference is { } currentReference
                    ? $"This is already associated with case {currentReference}. Open the received item to change that association."
                    : "This is already associated with a case. Open the received item to change that association.");
        }

        var destination = await destinations.GetAsync(receipt, targetCaseId, actor, cancellationToken);
        if (destination is null)
        {
            return new(false, "That case is not currently available for this upload. Search and choose another case.");
        }

        if (destination.Version != input.ExpectedCaseVersion)
        {
            return new(false, "That case changed after it was reviewed. Refresh and choose it again.");
        }

        try
        {
            var lease = await acquireCaseEditLease.ExecuteAsync(
                new(
                    targetCaseId,
                    input.ExpectedCaseVersion,
                    actor,
                    $"upload-attach-lease:{operationKey}"),
                cancellationToken);
            await linkIntake.ExecuteAsync(
                new(
                    receiptId,
                    targetCaseId,
                    receipt.Version,
                    lease.Version,
                    lease.Token,
                    actor,
                    operationKey,
                    reason),
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is not StaffAuthorizationException
            && (exception is ArgumentException or InvalidOperationException or KeyNotFoundException
                or IntakeAssociationConflictException
                || IntakeExceptionPolicy.IsTransientFailure(exception)))
        {
            return new(false, "The uploaded material could not be added to that case. Refresh and try again.");
        }

        return new(
            true,
            OperatorLabels.AssociatedWithCase(destination.Reference, byStaffDecision: true),
            targetCaseId);
    }

    public async Task<UploadCaseAttachResult> AttachGroupAsync(
        Guid groupId,
        IReadOnlyList<Guid> memberReceiptIds,
        Guid? caseId,
        string? reference,
        string reason,
        Guid operationId,
        IReadOnlyDictionary<Guid, long> expectedReceiptVersions,
        long expectedCaseVersion,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memberReceiptIds);
        if (memberReceiptIds.Count == 0)
        {
            return new(false, "There is nothing left in this submission to add.");
        }

        if (operationId == Guid.Empty || expectedCaseVersion < 0
            || expectedReceiptVersions is null
            || memberReceiptIds.Any(id => !expectedReceiptVersions.TryGetValue(id, out var version) || version < 0))
        {
            return new(false, "This confirmation is incomplete. Refresh and choose the case again.");
        }
        if (caseId is null)
        {
            return new(false, "Review the selected case and confirm the decision.");
        }

        var resolvedCaseId = caseId;
        if (resolvedCaseId is not { } targetCaseId || targetCaseId == Guid.Empty)
        {
            return new(false, "No single case matched that reference. Search and choose a case from the suggestions.");
        }

        var firstReceipt = await getIntake.ExecuteAsync(new(memberReceiptIds[0], actor), cancellationToken);
        if (firstReceipt is null)
        {
            return new(false, "A file in this submission is no longer available. Refresh before trying again.");
        }
        var destination = firstReceipt.CurrentCaseId is null
            ? await destinations.GetAsync(firstReceipt, targetCaseId, actor, cancellationToken)
            : null;
        if (firstReceipt.CurrentCaseId is null
            && (destination is null || destination.Version != expectedCaseVersion))
        {
            return new(false, "That case changed after it was reviewed. Refresh and choose it again.");
        }

        var added = 0;
        var alreadyThere = 0;
        var elsewhere = 0;
        var nextCaseVersion = expectedCaseVersion;
        try
        {
            foreach (var receiptId in memberReceiptIds)
            {
                var receipt = await getIntake.ExecuteAsync(new(receiptId, actor), cancellationToken);
                if (receipt is null)
                {
                    return PartialFailure(added + alreadyThere, "A file in this submission is no longer available. Refresh before trying again.");
                }
                var operationKey = DecisionOperationKey(
                    $"upload-attach-group:{groupId:N}", operationId, receiptId, targetCaseId,
                    expectedReceiptVersions[receiptId], nextCaseVersion, actor, reason);
                if (receipt.CurrentCaseId == targetCaseId)
                {
                    if (string.Equals(receipt.ManualAssociationOperationKey, operationKey, StringComparison.Ordinal))
                    {
                        alreadyThere++;
                        nextCaseVersion++;
                        continue;
                    }

                    return PartialFailure(added + alreadyThere, "A file in this submission already has a different decision for that case. Refresh to review it.");
                }
                if (receipt.Version != expectedReceiptVersions[receiptId])
                {
                    return PartialFailure(added + alreadyThere, "A file in this submission changed after it was reviewed. Refresh to review it.");
                }
                if (receipt.CurrentCaseId is not null)
                {
                    elsewhere++;
                    continue;
                }

                var currentDestination = await destinations.GetAsync(receipt, targetCaseId, actor, cancellationToken);
                if (currentDestination is null || currentDestination.Version != nextCaseVersion)
                {
                    return PartialFailure(added + alreadyThere, "A file in this submission can no longer be added to that case. Refresh to review it.");
                }

                // Each owned link consumes its lease and advances the Case by
                // one. Carry that known result forward rather than refreshing
                // and accidentally treating another writer as our own change.
                var lease = await acquireCaseEditLease.ExecuteAsync(
                    new(
                        targetCaseId,
                        nextCaseVersion,
                        actor,
                        $"upload-attach-lease:{operationKey}"),
                    cancellationToken);
                await linkIntake.ExecuteAsync(
                    new(
                        receiptId,
                        targetCaseId,
                        receipt.Version,
                        lease.Version,
                        lease.Token,
                        actor,
                        operationKey,
                        reason),
                    cancellationToken);
                added++;
                nextCaseVersion++;
            }
        }
        catch (Exception exception) when (
            exception is not StaffAuthorizationException
            && (exception is ArgumentException or InvalidOperationException or KeyNotFoundException
                or IntakeAssociationConflictException
                || IntakeExceptionPolicy.IsTransientFailure(exception)))
        {
            return PartialFailure(added + alreadyThere,
                "The remaining files could not be added to that case. Refresh and try again.");
        }

        if (elsewhere > 0)
        {
            return PartialFailure(added + alreadyThere,
                elsewhere == 1
                    ? "One file is already on a different case and was left there."
                    : $"{elsewhere} files are already on different cases and were left there.");
        }

        if (added == 0 && alreadyThere == 0)
        {
            return new(false, "Nothing from this submission could be added to that case.");
        }

        var message = OperatorLabels.AssociatedWithCase(
            destination?.Reference ?? firstReceipt.CurrentCaseReference,
            byStaffDecision: true);
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

    private static string DecisionOperationKey(
        string scope,
        Guid operationId,
        Guid receiptId,
        Guid caseId,
        long receiptVersion,
        long caseVersion,
        ActionActor actor,
        string reason)
    {
        var material = $"{scope}|{operationId:N}|{receiptId:N}|{caseId:N}|{receiptVersion}|{caseVersion}|{actor.Kind}|{actor.SubjectId}|{reason.Trim()}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)))[..32].ToLowerInvariant();
        return $"upload-confirm:{hash}";
    }

    private static UploadCaseAttachResult PartialFailure(int completed, string reason) =>
        new(false, completed == 0
            ? reason
            : $"{completed} file{(completed == 1 ? " was" : "s were")} completed before this stopped. {reason}",
            CompletedMembers: completed);
}
