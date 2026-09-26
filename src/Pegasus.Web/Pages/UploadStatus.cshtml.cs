using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;
using Labels = Pegasus.Web.Presentation.OperatorLabels.Upload;

namespace Pegasus.Web.Pages;

/// <summary>
/// A stored upload of one file (v30 Upload E): the same review surface as a
/// group of several, with its one decision on the right.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class UploadStatusModel(
    IQueuedIntakeStatusQueries queries,
    IUploadOutcomeQueries outcomeQueries,
    IUploadCaseDecision caseDecision,
    IGetIntake getIntake,
    ISearchCases searchCases,
    IIntakeSubmissionGroupStore submissionGroups,
    TimeProvider timeProvider) : UploadConfirmationPageModel(caseDecision)
{
    public QueuedIntakeStatus Status { get; private set; } = null!;

    public bool IsDuplicate { get; private set; }

    /// <summary>
    /// The confirmation outcome, once processing has left Received/Processing.
    /// Built from the same status read this page already queries — no second
    /// endpoint, no second poll.
    /// </summary>
    public UploadOutcomeView? Outcome { get; private set; }

    /// <summary>The file's receipt, staged or processed, for its size, kind and image.</summary>
    public IntakeReceipt? Receipt { get; private set; }

    /// <summary>
    /// How long before this page reloads itself, or null once the file has
    /// stopped moving and there is nothing left to wait for.
    /// </summary>
    public int? AutomaticRefreshMilliseconds =>
        Status.Status is QueuedIntakeStatusKind.Received or QueuedIntakeStatusKind.Processing
            ? UploadStatusRefresh.DelayMilliseconds(Status, timeProvider.GetUtcNow())
            : null;

    /// <summary>
    /// The terminal failure value when the authenticated principal has no
    /// usable staff actor and the richer outcome cannot be built.
    /// </summary>
    public string? FailureReason =>
        Status.Status == QueuedIntakeStatusKind.Failed && Outcome is null
            ? OperatorLabels.IntakeFailure(Status.FailureCode)
            : null;

    /// <summary>What the page draws.</summary>
    public UploadReviewView Review { get; private set; } = null!;

    private Guid _receiptId;

    protected override IReadOnlyList<Guid> SearchReceiptIds =>
        Outcome is { Attach: { } attach } ? [attach.ReceiptId] : [];

    /// <param name="duplicate">
    /// Carried on the URL so the duplicate value survives the page's own refreshes.
    /// </param>
    /// <param name="q">The Find term.</param>
    public async Task<IActionResult> OnGetAsync(
        Guid id,
        bool duplicate,
        string? q,
        CancellationToken cancellationToken)
    {
        var status = await queries.GetAsync(id, cancellationToken);
        if (status is null)
        {
            return NotFound();
        }

        Status = status;
        IsDuplicate = duplicate;
        _receiptId = status.ProcessedReceiptId ?? status.StagedReceiptId;

        if (TryGetActor(out var actor))
        {
            if (await FindManualSiblingGroupAsync(status, actor, cancellationToken) is { } group)
            {
                return RedirectToPage("/UploadGroupStatus", new { id = group.Id });
            }

            Receipt = await getIntake.ExecuteAsync(new(_receiptId, actor), cancellationToken);
            // The confirmation decision needs a full receipt read for a terminal
            // status; Received/Processing never reach the branch that needs one.
            if (status.Status is QueuedIntakeStatusKind.Complete or QueuedIntakeStatusKind.Failed)
            {
                Outcome = await outcomeQueries.BuildAsync(status, submissionGroupId: null, actor, cancellationToken);
            }

            await SearchAsync(q, actor, cancellationToken);
        }

        await BuildReviewAsync(id, cancellationToken);
        return Page();
    }

    private async Task BuildReviewAsync(Guid id, CancellationToken cancellationToken)
    {
        var haveActor = TryGetActor(out var actor);
        var unreadable = Receipt is { Decision: IntakeDecision.Unsupported or IntakeDecision.TechnicalFailure }
            && Outcome is { Kind: UploadOutcomeKind.NeedsReview or UploadOutcomeKind.CannotBecomeCase };
        var phase = UploadReviewPhase.Report;
        UploadReviewReport? report = null;
        UploadReviewDestination? destination = null;
        UploadOutcomeAction? proposal = null;
        IReadOnlyList<UploadCaseSuggestion> suggestions = [];
        switch (Status.Status)
        {
            case QueuedIntakeStatusKind.Received or QueuedIntakeStatusKind.Processing:
                phase = UploadReviewPhase.Pending;
                break;
            case QueuedIntakeStatusKind.Failed:
                report = new(Labels.ReviewRequiredEyebrow, "The file could not be processed", Outcome?.Message ?? FailureReason, Outcome?.PrimaryAction);
                break;
            default:
                switch (Outcome)
                {
                    case null or { Kind: UploadOutcomeKind.Working }:
                        phase = UploadReviewPhase.Pending;
                        break;
                    case { Kind: UploadOutcomeKind.Attached } attached:
                        phase = UploadReviewPhase.Attached;
                        destination = haveActor
                            ? await UploadReviewDestinations.LookupAsync(searchCases, actor!, Receipt?.CurrentCaseReference, attached.PrimaryAction?.Url, cancellationToken)
                            : null;
                        break;
                    case { Attach: { } attach } open:
                        phase = UploadReviewPhase.Decision;
                        suggestions = attach.SuggestedDestinations;
                        proposal = open.Kind == UploadOutcomeKind.ReadyToCreate ? open.PrimaryAction : null;
                        break;
                    case { Kind: UploadOutcomeKind.ImageCaseRegistered } registered:
                        report = new(Labels.CompleteEyebrow, $"Registered as {Labels.ImageIntake}", registered.Record is { } state ? $"{Labels.RegisteredAutomatically} · {state.State}" : registered.Message, registered.PrimaryAction);
                        break;
                    case { Kind: UploadOutcomeKind.Resolved } resolved:
                        report = new(Labels.CompleteEyebrow, resolved.StateLabel, resolved.Message, resolved.PrimaryAction);
                        break;
                    case { Kind: UploadOutcomeKind.NeedsReview } review:
                        report = new(
                            Labels.ReviewRequiredEyebrow,
                            unreadable ? Labels.UnreadableTitle : "This upload needs review",
                            unreadable ? Labels.UnreadableSentence : review.Message,
                            review.PrimaryAction is { } action ? action with { Label = Labels.OpenUnidentified } : null);
                        break;
                    case var other:
                        report = new(Labels.ReviewRequiredEyebrow, other.StateLabel, other.Message, other.PrimaryAction);
                        break;
                }

                break;
        }

        var attachReceiptId = Outcome?.Attach?.ReceiptId ?? UploadCaseConfirmation?.ReceiptId ?? UploadCaseDraft?.ReceiptId ?? _receiptId;
        var receiptVersion = UploadCaseConfirmation?.Input.ExpectedReceiptVersion
            ?? UploadCaseDraft?.ExpectedReceiptVersion
            ?? Outcome?.Attach?.ReceiptVersion
            ?? Receipt?.Version
            ?? 0;
        var known = suggestions.Concat(SearchResults);
        Review = new UploadReviewView(
            phase,
            Status.ReceivedAtUtc,
            [ReviewFile(unreadable)],
            0,
            $"/Upload/Status/{id:D}",
            "Attach",
            UploadCaseConfirmation?.Input.OperationId ?? UploadCaseDraft?.OperationId ?? Guid.NewGuid(),
            new Dictionary<Guid, long> { [attachReceiptId] = receiptVersion })
        {
            NowUtc = timeProvider.GetUtcNow(),
            AutoRefreshMilliseconds = AutomaticRefreshMilliseconds,
            Error = TempData["UploadConfirmationError"] as string,
            IsDuplicate = IsDuplicate,
            Candidates = suggestions.Select(UploadReviewCandidate.From).ToArray(),
            SearchResults = SearchResults.Select(UploadReviewCandidate.From).ToArray(),
            SearchTerm = SearchTerm,
            SearchFailed = SearchFailed,
            Record = Outcome?.Record is { } record ? new UploadReviewRecord(record.Reference, $"{Labels.RegisteredAutomatically} · {record.State}", record.Url) : null,
            Proposal = proposal is null ? null : proposal with { Label = Labels.Proposal },
            Destination = destination,
            Report = report,
            Confirmation = ReviewConfirmation(UploadCaseConfirmation, known),
            CouldNotBeReadCount = unreadable ? 1 : 0,
            SingleReceiptId = attachReceiptId
        };
    }

    private UploadReviewFile ReviewFile(bool unreadable)
    {
        var (label, tone) = (Status.Status, Outcome?.Kind) switch
        {
            (QueuedIntakeStatusKind.Received, _) => (Labels.StateReceived, "is-running"),
            (QueuedIntakeStatusKind.Processing, _) => (Labels.StateProcessing, "is-running"),
            (QueuedIntakeStatusKind.Failed, _) => (Labels.StateFailed, "is-error"),
            (_, null or UploadOutcomeKind.Working) => (Labels.StateProcessing, "is-running"),
            (_, UploadOutcomeKind.Attached) => (Labels.StateAdded, "is-done"),
            _ when unreadable => (Labels.StateCouldNotBeRead, "is-error"),
            _ => (Labels.StateReady, string.Empty)
        };
        var imageReceiptId = Outcome?.ThumbnailReceiptId
            ?? (Receipt is { MediaType: var mediaType } && mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? Receipt.Id : null);
        return new UploadReviewFile(
            0,
            Status.StagedReceiptId,
            Status.SourceFileName,
            Receipt?.SourceLength,
            Labels.Kind(Receipt?.MediaType, Status.SourceFileName),
            imageReceiptId is { } imageId ? $"/Received/{imageId:D}/Image" : null,
            Receipt is null ? null : $"/Received/{Receipt.Id:D}/Source",
            label,
            tone,
            unreadable)
        {
            Photographs = UploadReviewFile.PhotographsOf(Receipt)
        };
    }

    protected override IActionResult RedirectToSurface(Guid id) =>
        RedirectToPage("/UploadStatus", new { id });

    protected override Task<IActionResult> RenderSurfaceAsync(
        Guid surfaceId,
        CancellationToken cancellationToken) =>
        OnGetAsync(surfaceId, duplicate: false, q: null, cancellationToken: cancellationToken);

    protected override async Task<bool> SurfaceContainsReceiptAsync(
        Guid surfaceId,
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        var status = await queries.GetAsync(surfaceId, cancellationToken);
        if (status is null || (status.ProcessedReceiptId ?? status.StagedReceiptId) != receiptId
            || !TryGetActor(out var actor))
        {
            return false;
        }

        return await FindManualSiblingGroupAsync(status, actor, cancellationToken) is null;
    }

    private async Task<IntakeSubmissionGroup?> FindManualSiblingGroupAsync(
        QueuedIntakeStatus status,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        var receiptId = status.ProcessedReceiptId ?? status.StagedReceiptId;
        var receipt = await getIntake.ExecuteAsync(new(receiptId, actor), cancellationToken);
        if (receipt is null)
        {
            return null;
        }

        var group = await submissionGroups.FindForMemberSourceAsync(
            receipt.SourceIdentity, cancellationToken);
        return group is { Channel: IntakeSourceChannel.ManualUpload, HasSiblingMembers: true }
            ? group
            : null;
    }
}
