using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Unidentified;

[Authorize(Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[RequestFormLimits(MultipartBodyLengthLimit = IntakeEnvelopeLimits.MaximumContentLength)]
[RequestSizeLimit(IntakeEnvelopeLimits.MaximumContentLength)]
public sealed class DetailsModel(
    IUnidentifiedStore store,
    IResolveUnidentified resolve,
    IGetIntake getIntake,
    ISupplyAuditOriginalReport supplyOriginalReport,
    IReevaluateIntake reevaluateIntake,
    ISearchCases searchCases,
    ICaseQueryStore caseQueries) : StaffPageModel
{
    public UnidentifiedItem Item { get; private set; } = null!;

    public IReadOnlyList<UnidentifiedHistoryEntry> History { get; private set; } = [];

    /// <summary>
    /// The retained receipt behind a <see cref="UnidentifiedOriginKind.Receipt"/>
    /// origin — its filename, retained files, custody, and processing
    /// evidence — so staff can make a safe resolution decision instead of
    /// only seeing the origin GUID and reason. Null for a
    /// <see cref="UnidentifiedOriginKind.SubmissionGroup"/> origin or when the
    /// receipt lookup fails; the page degrades to the summary fields only.
    /// </summary>
    public IntakeReceipt? SourceReceipt { get; private set; }

    /// <summary>
    /// What the retained material is, classified the same way the Queues
    /// page's Unidentified rows are (<see cref="UnidentifiedMediaKindPolicy"/>,
    /// whose nullable overload also owns the no-receipt fallback) so a row
    /// and its detail page never disagree.
    /// </summary>
    public UnidentifiedMediaKind MediaKind =>
        UnidentifiedMediaKindPolicy.Classify(SourceReceipt?.SourceIdentity.Channel, SourceReceipt?.MediaType);

    public string MediaKindLabel => OperatorLabels.UnidentifiedMediaKind(MediaKind);

    /// <summary>
    /// The operator-meaningful handle for the retained file or message this
    /// item concerns: the original filename for an image or document, or the
    /// subject and sender for an e-mail (formatted by the one shared rule,
    /// <see cref="OperatorLabels.EmailHandle"/>). Never a GUID or internal
    /// reference.
    /// </summary>
    public string Handle
    {
        get
        {
            if (SourceReceipt is not { } receipt)
            {
                return "Not available";
            }

            if (MediaKind == UnidentifiedMediaKind.Email)
            {
                var subject = receipt.Evidence
                    .FirstOrDefault(item => item.Source == IntakeEvidenceSource.Subject)
                    ?.Detail;
                var sender = receipt.MailRouteDecision?.EffectiveSender?.Address;
                return OperatorLabels.EmailHandle(subject, sender);
            }

            return receipt.SourceFileName;
        }
    }

    /// <summary>
    /// The one primary control this page offers, decided by the reason and
    /// origin through <see cref="UnidentifiedNextStepPolicy"/> — the same
    /// answer the automation surface reads.
    /// </summary>
    public UnidentifiedNextStep NextStep =>
        UnidentifiedNextStepPolicy.Primary(Item.ReasonCode, Item.Origin.Kind);

    /// <summary>
    /// Whether the received item can still be turned into a case, under the
    /// same gates the received-item screen applies: no accepted case, no
    /// allocation in flight, and a decision the acceptance policy accepts.
    /// </summary>
    public bool CanCreateCase =>
        SourceReceipt is { } receipt
        && receipt.AcceptedCaseId is null
        && receipt.AllocationState is null
        && IntakeDecisionPolicy.CanBecomeCase(receipt.Decision);

    /// <summary>
    /// Cases the eliminator itself left as candidates for this material
    /// (a conflicting-identification item pre-lists them), so the link-to-case
    /// search starts from the recorded evidence rather than an empty box.
    /// </summary>
    public IReadOnlyList<CaseSearchItem> CandidateCases { get; private set; } = [];

    /// <summary>
    /// The cases a link-to-case search matched. Empty until the operator
    /// searches, or when the search form is not on the page.
    /// </summary>
    public IReadOnlyList<CaseSearchItem> SearchResults { get; private set; } = [];

    /// <summary>Whether "Link to Case" renders as an alternative beneath the primary control.</summary>
    public bool ShowsLinkAlternative => Item.State == UnidentifiedState.Open
        && NextStep is not UnidentifiedNextStep.LinkToCase;

    /// <summary>Whether the page shows the link-to-case search at all.</summary>
    public bool ShowsLinkSearch => Item.State == UnidentifiedState.Open
        && (NextStep == UnidentifiedNextStep.LinkToCase || ShowsLinkAlternative);

    /// <summary>The one-line outcome of a successful POST, shown above the record.</summary>
    public string? StatusMessage { get; private set; }

    [BindProperty(SupportsGet = true)]
    public string? LinkQuery { get; set; }

    [BindProperty]
    public IFormFile? Report { get; set; }

    [BindProperty]
    public long ExpectedVersion { get; set; }

    [BindProperty]
    public string ResolutionReason { get; set; } = string.Empty;

    [BindProperty]
    public UnidentifiedResolutionTargetKind TargetKind { get; set; } = UnidentifiedResolutionTargetKind.InstructionCase;

    [BindProperty]
    public string TargetId { get; set; } = string.Empty;

    [BindProperty]
    public string? TargetReference { get; set; }

    [BindProperty]
    public string OperationKey { get; set; } = string.Empty;

    [BindProperty]
    public string SupplyOperationKey { get; set; } = string.Empty;

    [BindProperty]
    public string ProcessAgainOperationKey { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        return await LoadAsync(id, cancellationToken);
    }

    public async Task<IActionResult> OnPostResolveAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await store.GetAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(OperationKey))
        {
            OperationKey = $"web-unidentified-resolve:{id:N}:{Guid.NewGuid():N}";
        }

        try
        {
            await resolve.ExecuteAsync(
                new(
                    id,
                    ExpectedVersion,
                    actor,
                    OperationKey,
                    ResolutionReason,
                    TargetKind,
                    TargetId,
                    TargetReference,
                    DateTimeOffset.UtcNow),
                cancellationToken);
        }
        catch (UnidentifiedVersionConflictException)
        {
            ModelState.AddModelError(string.Empty, "This item changed in another session. Reload it before resolving.");
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }

        return await LoadAsync(id, cancellationToken);
    }

    public async Task<IActionResult> OnPostSupplyOriginalReportAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await store.GetAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (Report is null || Report.Length is <= 0 or > IntakeEnvelopeLimits.MaximumContentLength)
        {
            ModelState.AddModelError(string.Empty, "Choose the engineer's report file to add.");
            return await LoadAsync(id, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(SupplyOperationKey))
        {
            SupplyOperationKey = $"web-unidentified-supply:{id:N}:{Guid.NewGuid():N}";
        }

        // Bounded copy: the length gate above is checked before anything is
        // buffered, and the stream is read once into memory of that size.
        await using var buffer = new MemoryStream(checked((int)Report.Length));
        await Report.CopyToAsync(buffer, cancellationToken);
        var content = buffer.GetBuffer().AsMemory(0, checked((int)buffer.Length));

        try
        {
            await supplyOriginalReport.ExecuteAsync(
                new(
                    id,
                    ExpectedVersion,
                    actor,
                    SupplyOperationKey,
                    Report.FileName,
                    Report.ContentType,
                    content),
                cancellationToken);
            StatusMessage =
                "Original report added. The Audit Case is being created; refresh in a moment.";
        }
        catch (UnidentifiedVersionConflictException)
        {
            ModelState.AddModelError(string.Empty, "This item changed in another session. Reload it before adding the report.");
        }
        catch (SuppliedOriginalReportRefusalException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }

        return await LoadAsync(id, cancellationToken);
    }

    public async Task<IActionResult> OnPostProcessAgainAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await store.GetAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (item.State != UnidentifiedState.Open
            || item.Origin.Kind != UnidentifiedOriginKind.Receipt
            || await getIntake.ExecuteAsync(new(item.Origin.Id, actor), cancellationToken)
                is not { } receipt)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(ProcessAgainOperationKey))
        {
            ProcessAgainOperationKey = $"web-unidentified-process-again:{id:N}:{Guid.NewGuid():N}";
        }

        try
        {
            await reevaluateIntake.ExecuteAsync(
                new(
                    receipt.Id,
                    receipt.Version,
                    actor,
                    ProcessAgainOperationKey,
                    $"Processed again from Unidentified {item.Reference}"),
                cancellationToken);
            StatusMessage = "The retained source is queued for processing again.";
        }
        catch (IntakeVersionConflictException)
        {
            ModelState.AddModelError(string.Empty, "The received item changed in another session. Reload and try again.");
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }

        return await LoadAsync(id, cancellationToken);
    }

    private async Task<IActionResult> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await store.GetAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        Item = item;
        ExpectedVersion = item.Version;
        History = await store.HistoryAsync(id, cancellationToken);

        if (string.IsNullOrWhiteSpace(OperationKey))
        {
            // Generated once per GET and carried by the hidden form field so a
            // retried POST (a lost response, a double submit) resubmits the
            // same key and replays through IResolveUnidentified's idempotency
            // check instead of being treated as a new, conflicting command. A
            // reload after a failed POST keeps the key that POST already
            // bound here, rather than handing out a fresh one.
            OperationKey = $"web-unidentified-resolve:{id:N}:{Guid.NewGuid():N}";
        }
        if (string.IsNullOrWhiteSpace(SupplyOperationKey))
        {
            SupplyOperationKey = $"web-unidentified-supply:{id:N}:{Guid.NewGuid():N}";
        }
        if (string.IsNullOrWhiteSpace(ProcessAgainOperationKey))
        {
            ProcessAgainOperationKey = $"web-unidentified-process-again:{id:N}:{Guid.NewGuid():N}";
        }

        if (item.Origin.Kind == UnidentifiedOriginKind.Receipt
            && TryGetActor(out var actor))
        {
            try
            {
                SourceReceipt = await getIntake.ExecuteAsync(new(item.Origin.Id, actor), cancellationToken);
            }
            catch (UnauthorizedAccessException)
            {
                // Degrade to the summary fields only; the Unidentified item
                // itself is still fully visible and resolvable.
                SourceReceipt = null;
            }
        }

        await LoadLinkCasesAsync(cancellationToken);

        return Page();
    }

    /// <summary>
    /// The cases the link-to-case search offers: the eliminator's own
    /// candidates for this receipt, then — when the operator searched — the
    /// search matches. Both read the same projection, so a pre-listed
    /// candidate and a search result render identically.
    /// </summary>
    private async Task LoadLinkCasesAsync(CancellationToken cancellationToken)
    {
        if (!ShowsLinkSearch || !TryGetActor(out var actor))
        {
            return;
        }

        var candidates = new List<CaseSearchItem>();
        var seen = new HashSet<Guid>();
        if (SourceReceipt?.CaseMatchDecision is
            { Outcome: CaseMatchOutcome.Ambiguous, Candidates.Count: > 0 } match)
        {
            foreach (var candidate in match.Candidates.Take(5))
            {
                if (seen.Add(candidate.CaseId)
                    && await caseQueries.GetAsync(new(candidate.CaseId, actor), cancellationToken)
                        is { } details)
                {
                    candidates.Add(details.Summary);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(LinkQuery))
        {
            var results = await searchCases.ExecuteAsync(
                new(actor, new CaseSearchFilters(Query: LinkQuery.Trim()), Page: 1, PageSize: 8),
                cancellationToken);
            foreach (var result in results.Items)
            {
                if (seen.Add(result.CaseId))
                {
                    candidates.Add(result);
                }
            }
            SearchResults = results.Items;
        }

        CandidateCases = candidates;
    }

    public string ReasonLabel => OperatorLabels.UnidentifiedReason(Item.ReasonCode);
}
