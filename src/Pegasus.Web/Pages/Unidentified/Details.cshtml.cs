using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Unidentified;

[Authorize(Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class DetailsModel(
    IUnidentifiedStore store,
    IResolveUnidentified resolve,
    IGetIntake getIntake,
    IIntakeSubmissionGroupStore submissionGroups,
    IImageIntakeQueries imageIntakes) : StaffPageModel
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

    public IntakeSubmissionGroup? SourceSubmissionGroup { get; private set; }

    public ImageIntakeDetail? SourceImageIntake { get; private set; }

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

    [BindProperty]
    public long ExpectedVersion { get; set; }

    [BindProperty]
    public string ResolutionReason { get; set; } = string.Empty;

    public const string AddExistingCaseAction = "add-existing-case";
    public const string CreateCaseAction = "create-case";
    public const string RegisterImageAction = "register-image";
    public const string CloseAction = "close";

    [BindProperty(SupportsGet = true, Name = "action")]
    public string? ResolutionAction { get; set; }
    public bool OpenResolutionDialog { get; private set; }

    public bool CanCreateCase => SourceReceipt is { } receipt
        && receipt.AcceptedCaseId is null
        && receipt.AllocationState is null
        && IntakeDecisionPolicy.CanBecomeCase(receipt.Decision);

    public bool CanRegisterImage => SourceReceipt is { } receipt
        && receipt.Decision == IntakeDecision.NeedsSorting
        && receipt.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    [BindProperty]
    public string OperationKey { get; set; } = string.Empty;
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

        OpenResolutionDialog = true;
        if (!string.Equals(ResolutionAction, CloseAction, StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty, "Choose a supported resolution action.");
            return await LoadAsync(id, cancellationToken);
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
                    UnidentifiedResolutionTargetKind.ExternalReference,
                    "closed",
                    null,
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

        if (item.Origin.Kind == UnidentifiedOriginKind.SubmissionGroup)
        {
            SourceSubmissionGroup = await submissionGroups.GetAsync(item.Origin.Id, cancellationToken);
            SourceImageIntake = await imageIntakes.GetBySubmissionGroupAsync(item.Origin.Id, cancellationToken);
        }
        else if (item.Origin.Kind == UnidentifiedOriginKind.Receipt
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
        OpenResolutionDialog |= !string.IsNullOrWhiteSpace(ResolutionAction) || !ModelState.IsValid;

        return Page();
    }

    public string ReasonLabel => OperatorLabels.UnidentifiedReason(Item.ReasonCode);
}
