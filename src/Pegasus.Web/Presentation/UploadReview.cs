using System.Globalization;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The one review surface both upload status pages render (v30 Upload E,
/// "Inspection studio", 25 September 2026): the files on the left, inspected
/// one at a time, and the upload's one decision on the right. Built by the
/// page models from the reads they already make; the partial draws only what
/// is here and never reaches back into a page model.
/// </summary>
public enum UploadReviewPhase
{
    /// <summary>A file is still stored, queued or processing.</summary>
    Pending,

    /// <summary>The upload waits for the operator's one Case decision.</summary>
    Decision,

    /// <summary>Every readable file was added to one Case.</summary>
    Attached,

    /// <summary>The submission was discarded; its material is retained.</summary>
    Discarded,

    /// <summary>A settled outcome with nothing to decide here: a title, a sentence and the record that owns it.</summary>
    Report
}

/// <summary>One file in the upload, in submission order.</summary>
public sealed record UploadReviewFile(
    int Ordinal,
    Guid StagedReceiptId,
    string Name,
    long? Bytes,
    string Kind,
    string? ImageUrl,
    string? OriginalUrl,
    string StateLabel,
    string StateTone,
    bool CouldNotBeRead,
    UploadOutcomeAction? Action = null)
{
    public bool IsImage => ImageUrl is not null;

    public string Size => Bytes is { } bytes ? OperatorLabels.Upload.FileSize(bytes) : string.Empty;

    /// <summary>The photographs Pegasus kept out of this document (a PDF or Word file), in stored order; empty for an image or a file not yet processed.</summary>
    public IReadOnlyList<UploadReviewPhotograph> Photographs { get; init; } = [];

    /// <summary>The retained photographs of a processed receipt, addressed by their authorised staff read.</summary>
    public static IReadOnlyList<UploadReviewPhotograph> PhotographsOf(IntakeReceipt? receipt) =>
        receipt is null
            ? []
            : InstructionEvidenceImages.Select(receipt.AssetRecords)
                .Where(asset => asset.Kind == IntakeAssetKind.EmbeddedImage)
                .Select(asset => new UploadReviewPhotograph(
                    asset.Id,
                    asset.FileName,
                    $"/Received/{receipt.Id:D}/Asset/{asset.Id:D}"))
                .ToArray();
}

/// <summary>One photograph pulled out of an uploaded document.</summary>
public sealed record UploadReviewPhotograph(Guid AssetId, string Name, string Url);

/// <summary>A Case the operator may choose: a suggestion or a search result.</summary>
public sealed record UploadReviewCandidate(
    Guid CaseId,
    string Reference,
    string? Registration,
    string? Claimant,
    string? Principal,
    string Stage,
    long? Version)
{
    public static UploadReviewCandidate From(UploadCaseSuggestion suggestion) =>
        new(
            suggestion.CaseId,
            suggestion.Reference,
            suggestion.Registration,
            suggestion.Claimant,
            suggestion.Principal,
            suggestion.Stage,
            suggestion.Version);
}

/// <summary>The confirmed destination, once the upload has been added.</summary>
public sealed record UploadReviewDestination(
    string Reference,
    string? Registration,
    string? Claimant,
    string? Principal,
    string? Stage,
    string Url);

/// <summary>The subordinate Image intake record the upload registered automatically.</summary>
public sealed record UploadReviewRecord(string Reference, string Detail, string Url);

/// <summary>A settled outcome's words: what happened, one sentence, and where it lives.</summary>
public sealed record UploadReviewReport(string Eyebrow, string Title, string? Sentence, UploadOutcomeAction? Action, bool OfferRefresh = false);

/// <summary>The exact target the confirmation dialog repeats before the write.</summary>
public sealed record UploadReviewConfirmation(
    UploadReviewCandidate Target,
    Guid OperationId,
    long ExpectedCaseVersion);

public sealed record UploadReviewView(
    UploadReviewPhase Phase,
    DateTimeOffset ReceivedAtUtc,
    IReadOnlyList<UploadReviewFile> Files,
    int Inspected,
    string PageUrl,
    string AttachHandler,
    Guid OperationId,
    IReadOnlyDictionary<Guid, long> ReceiptVersions)
{
    public DateTimeOffset NowUtc { get; init; } = DateTimeOffset.UtcNow;

    public int? AutoRefreshMilliseconds { get; init; }

    public string? Error { get; init; }

    public bool IsDuplicate { get; init; }

    public IReadOnlyList<UploadReviewCandidate> Candidates { get; init; } = [];

    public IReadOnlyList<UploadReviewCandidate> SearchResults { get; init; } = [];

    public string? SearchTerm { get; init; }

    public bool SearchFailed { get; init; }

    public UploadReviewRecord? Record { get; init; }

    /// <summary>The editable new-Case proposal route for instruction material (FRD-18 row 3).</summary>
    public UploadOutcomeAction? Proposal { get; init; }

    public UploadReviewDestination? Destination { get; init; }

    public UploadReviewReport? Report { get; init; }

    public UploadReviewConfirmation? Confirmation { get; init; }

    public int CouldNotBeReadCount { get; init; }

    /// <summary>The single-file surface names its receipt; the group surface names its roster.</summary>
    public Guid? SingleReceiptId { get; init; }

    public bool CanDiscard { get; init; }

    public long GroupVersion { get; init; }

    public IReadOnlyDictionary<Guid, long> DiscardVersions { get; init; } = new Dictionary<Guid, long>();

    public bool OpenDiscard { get; init; }

    public bool SearchOpen => Candidates.Count == 0 || SearchTerm is not null || SearchFailed;

    public bool OffersLeave => Phase is UploadReviewPhase.Decision
        || (Phase == UploadReviewPhase.Report && Report is { OfferRefresh: false });

    public bool OffersDiscard => CanDiscard && OffersLeave;

    public string? Registration => Candidates.Select(candidate => candidate.Registration).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    public string FileCount => OperatorLabels.Upload.Files(Files.Count);

    public string TotalSize
    {
        get
        {
            var known = Files.Where(file => file.Bytes is not null).ToArray();
            return known.Length == 0 ? string.Empty : OperatorLabels.Upload.FileSize(known.Sum(file => file.Bytes!.Value));
        }
    }

    public string InspectedOfCount => string.Create(CultureInfo.InvariantCulture, $"{Inspected + 1} of {Files.Count}");
}

/// <summary>The confirmed destination's facts, read once the upload has been added.</summary>
public static class UploadReviewDestinations
{
    public static async Task<UploadReviewDestination?> LookupAsync(
        Pegasus.Core.Cases.ISearchCases searchCases,
        Pegasus.Core.Identity.ActionActor actor,
        string? reference,
        string? url,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var target = url ?? "/Cases";
        try
        {
            var result = await searchCases.ExecuteAsync(
                new(actor, new(CaseReference: reference, IncludeTriage: true), Page: 1, PageSize: 2),
                cancellationToken);
            var item = result.Items.FirstOrDefault(candidate =>
                string.Equals(candidate.Reference, reference, StringComparison.OrdinalIgnoreCase));
            return item is null
                ? new(reference, null, null, null, null, target)
                : new(
                    item.Reference,
                    item.Registration,
                    item.Claimant,
                    item.Principal,
                    item.TriageState is { } triageState
                        ? OperatorLabels.TriageState(triageState)
                        : OperatorLabels.CaseStage(item.State),
                    target);
        }
        catch (Exception exception) when (exception is not Pegasus.Core.Identity.StaffAuthorizationException
            && !cancellationToken.IsCancellationRequested)
        {
            // The destination is already recorded; a failed facts read only
            // leaves the card with the reference it certainly has.
            return new(reference, null, null, null, null, target);
        }
    }
}
