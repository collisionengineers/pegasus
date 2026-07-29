using System.Security.Claims;
using Pegasus.Core.Address;
using Pegasus.Core.Identity;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Pegasus.Web.Pages.Intake;

public sealed partial class ReviewModel(
    IIntakeReceiptQueries queries,
    IIntakeArtifactStore artifactStore,
    IAcceptIntake acceptIntake,
    ILogger<ReviewModel> logger,
    IInspectionAddressResolutionStore addressResolutionStore) : PageModel
{

    public IntakeReceipt Receipt { get; private set; } = null!;

    public bool IsDuplicate { get; private set; }
    public InspectionAddressResolutionSnapshot AddressResolution { get; private set; } = null!;


    [BindProperty]
    public string PrincipalCode { get; set; } = string.Empty;

    [BindProperty]
    public CaseType CaseType { get; set; } = Pegasus.Core.Cases.CaseType.Inspection;

    [BindProperty]
    public AuditAssessment? StandaloneAuditAssessment { get; set; }

    [BindProperty]
    public bool InstructionComplete { get; set; }

    [BindProperty]
    public bool ImagesComplete { get; set; }

    [BindProperty]
    public bool InstructionConfirmedByStaff { get; set; }

    [BindProperty]
    public bool ImagesConfirmedByStaff { get; set; }

    [BindProperty]
    public string AcceptanceOperationKey { get; set; } = string.Empty;
    [BindProperty]
    public long ExpectedAddressReceiptVersion { get; set; }

    [BindProperty]
    public string AddressSuggestionFingerprint { get; set; } = string.Empty;

    [BindProperty]
    public string AddressOperationId { get; set; } = string.Empty;

    [BindProperty]
    public string CorrectedInspectionAddress { get; set; } = string.Empty;


    public async Task<IActionResult> OnGetAsync(
        Guid id,
        bool duplicate = false,
        CancellationToken cancellationToken = default)
    {
        var result = await LoadReceiptAsync(id, cancellationToken);
        if (result is not null)
        {
            return result;
        }

        IsDuplicate = duplicate;
        PrincipalCode = Receipt.InstructionDraft?.SuggestedPrincipalCode ?? string.Empty;
        InstructionComplete = Receipt.InstructionDraft is not null
            && Receipt.MissingFields.Count == 0
            && AddressResolution.State is (
                InspectionAddressResolutionState.Accepted
                or InspectionAddressResolutionState.Corrected);
        InstructionConfirmedByStaff = InstructionComplete;
        AcceptanceOperationKey = Guid.NewGuid().ToString("N");
        return Page();
    }

    public async Task<IActionResult> OnPostAcceptAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await LoadReceiptAsync(id, cancellationToken);
        if (result is not null)
        {
            return result;
        }

        if (Receipt.Decision != IntakeDecision.DraftReady)
        {
            ModelState.AddModelError(
                string.Empty,
                "Only an instruction draft can be accepted as a case.");
        }
        if (AddressResolution.State is not InspectionAddressResolutionState.Accepted
            and not InspectionAddressResolutionState.Corrected)
        {
            ModelState.AddModelError(
                string.Empty,
                "Accept or correct the inspection-address suggestion before allocating a case reference.");
        }

        PrincipalCode = PrincipalCode.Trim().ToUpperInvariant();
        if (PrincipalCode.Length == 0)
        {
            ModelState.AddModelError(
                nameof(PrincipalCode),
                "Enter the confirmed principal code.");
        }
        else if (PrincipalCode.Length > 20)
        {
            ModelState.AddModelError(
                nameof(PrincipalCode),
                "The principal code must be 20 characters or fewer.");
        }

        if (!Enum.IsDefined(CaseType))
        {
            ModelState.AddModelError(nameof(CaseType), "Choose a valid case type.");
        }

        if (StandaloneAuditAssessment is { } assessment && !Enum.IsDefined(assessment))
        {
            ModelState.AddModelError(
                nameof(StandaloneAuditAssessment),
                "Choose a valid Audit assessment.");
        }
        else if (CaseType == Pegasus.Core.Cases.CaseType.Audit && StandaloneAuditAssessment is null)
        {
            ModelState.AddModelError(
                nameof(StandaloneAuditAssessment),
                "Confirm the standalone Audit assessment before accepting the case.");
        }
        else if (CaseType != Pegasus.Core.Cases.CaseType.Audit && StandaloneAuditAssessment is not null)
        {
            ModelState.AddModelError(
                nameof(StandaloneAuditAssessment),
                "An assessment can be recorded here only for a standalone Audit.");
        }

        if (!Guid.TryParseExact(AcceptanceOperationKey, "N", out var operationId))
        {
            ModelState.AddModelError(
                string.Empty,
                "The acceptance request is invalid. Reload the page and try again.");
        }

        var actor = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(actor))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var outcome = await acceptIntake.ExecuteAsync(
                new(
                    Receipt.Id,
                    AddressResolution.ReceiptVersion,
                    actor,
                    $"intake-accept:{operationId:N}",
                    CaseType,
                    PrincipalCode,
                    new(
                        InstructionComplete,
                        ImagesComplete,
                        InstructionConfirmedByStaff,
                        ImagesConfirmedByStaff),
                    StandaloneAuditAssessment),
                cancellationToken);

            TempData["IntakeQueueStatus"] = outcome.IsDuplicate
                ? "acceptance_duplicate"
                : "accepted";
            TempData["AcceptedCaseReference"] = outcome.Identity.AuditReference
                ?? outcome.Identity.Reference;
            return RedirectToPage("/Intake/Queue");
        }
        catch (CaseIdentitySequenceExhaustedException exception)
        {
            LogIdentitySequenceExhausted(logger, Receipt.Id, exception);
            ModelState.AddModelError(
                string.Empty,
                "The case reference sequence is exhausted. The intake receipt was not accepted.");
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            LogCaseAcceptanceFailed(logger, Receipt.Id, exception);
            ModelState.AddModelError(
                string.Empty,
                "The case could not be accepted. No reference was allocated; reload the receipt and try again.");
        }

        return Page();
    }
    public Task<IActionResult> OnPostAcceptAddressAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        ResolveAddressAsync(
            id,
            InspectionAddressStaffDecision.AcceptSuggestion,
            cancellationToken);

    public Task<IActionResult> OnPostCorrectAddressAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        ResolveAddressAsync(
            id,
            InspectionAddressStaffDecision.CorrectSuggestion,
            cancellationToken);


    public async Task<IActionResult> OnGetAssetAsync(
        Guid id,
        Guid assetId,
        CancellationToken cancellationToken = default)
    {
        var asset = await queries.GetAssetAsync(id, assetId, cancellationToken);
        if (asset is null)
        {
            return NotFound();
        }

        ReadOnlyMemory<byte>? content;
        try
        {
            content = await artifactStore.ReadAsync(asset.StorageKey, cancellationToken);
        }
        catch (IntakeArtifactIntegrityException)
        {
            return new ContentResult
            {
                StatusCode = StatusCodes.Status409Conflict,
                ContentType = "text/plain",
                Content = "The retained asset failed integrity validation and cannot be served."
            };
        }
        if (content is null)
        {
            return NotFound();
        }

        if (IsReviewableImage(asset.MediaType))
        {
            return File(content.Value.ToArray(), asset.MediaType);
        }

        return File(content.Value.ToArray(), "application/octet-stream", asset.FileName);
    }

    public static bool IsReviewableImage(string mediaType) =>
        mediaType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
        || mediaType.Equals("image/png", StringComparison.OrdinalIgnoreCase);

    public int DuplicateOccurrenceCount(IntakeAssetRecord asset) =>
        Receipt.AssetRecords.Count(candidate => candidate.ContentHash == asset.ContentHash);

    public static string DecisionLabel(IntakeDecision decision) => decision switch
    {
        IntakeDecision.DraftReady => "Instruction draft",
        IntakeDecision.NeedsSorting => "Needs sorting",
        IntakeDecision.OcrRequired => "Document text required",
        IntakeDecision.TechnicalFailure => "Technical failure",
        _ => "Unsupported"
    };

    public static string SourceChannelLabel(IntakeSourceChannel channel) => channel switch
    {
        IntakeSourceChannel.ManualUpload => "Manual upload",
        IntakeSourceChannel.Mailbox => "Approved inbox",
        _ => throw new InvalidOperationException($"Unknown intake source channel value '{(int)channel}'.")
    };

    private async Task<IActionResult> ResolveAddressAsync(
        Guid id,
        InspectionAddressStaffDecision decision,
        CancellationToken cancellationToken)
    {
        var loadResult = await LoadReceiptAsync(id, cancellationToken);
        if (loadResult is not null)
        {
            return loadResult;
        }

        if (!Guid.TryParseExact(AddressOperationId, "N", out var operationId))
        {
            ModelState.AddModelError(
                string.Empty,
                "The inspection-address request is invalid. Reload the receipt and try again.");
            PrepareAddressCommand();
            return Page();
        }

        var subject = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(subject, out var staffId) || staffId == Guid.Empty)
        {
            return Forbid();
        }

        var roles = new List<StaffRole>(3);
        if (User.IsInRole(StaffRoleNames.Administrator))
        {
            roles.Add(StaffRole.Administrator);
        }
        if (User.IsInRole(StaffRoleNames.Engineer))
        {
            roles.Add(StaffRole.Engineer);
        }
        if (User.IsInRole(StaffRoleNames.User))
        {
            roles.Add(StaffRole.User);
        }
        if (roles.Count == 0)
        {
            return Forbid();
        }

        try
        {
            var resolution = await addressResolutionStore.ResolveAsync(
                new(
                    id,
                    ExpectedAddressReceiptVersion,
                    AddressSuggestionFingerprint,
                    decision,
                    decision == InspectionAddressStaffDecision.CorrectSuggestion
                        ? CorrectedInspectionAddress
                        : null,
                    ActionActor.Staff(staffId, roles),
                    operationId,
                    HttpContext.TraceIdentifier),
                cancellationToken);
            TempData["InspectionAddressStatus"] =
                resolution.State == InspectionAddressResolutionState.Accepted
                    ? "Inspection address suggestion accepted."
                    : "Inspection address correction recorded.";
            return RedirectToPage("/Intake/Review", new { id });
        }
        catch (InspectionAddressResolutionConcurrencyException)
        {
            ModelState.AddModelError(
                string.Empty,
                "The intake evidence changed. Review the current inspection-address suggestion before confirming it.");
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(nameof(CorrectedInspectionAddress), exception.Message);
        }
        catch (InvalidOperationException)
        {
            ModelState.AddModelError(
                string.Empty,
                "The inspection address remains unresolved. Review the current source evidence.");
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            LogAddressResolutionFailed(logger, id, exception);
            ModelState.AddModelError(
                string.Empty,
                "The inspection address could not be recorded. Reload the receipt and try again.");
        }

        await LoadReceiptAsync(id, cancellationToken);
        PrepareAddressCommand();
        return Page();
    }

    private void PrepareAddressCommand()
    {
        ExpectedAddressReceiptVersion = AddressResolution.ReceiptVersion;
        AddressSuggestionFingerprint =
            AddressResolution.Evaluation.Suggestion?.Fingerprint ?? string.Empty;
        AddressOperationId = Guid.NewGuid().ToString("N");
        CorrectedInspectionAddress =
            AddressResolution.Evaluation.Suggestion?.Value ?? string.Empty;
    }

    private async Task<IActionResult?> LoadReceiptAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var receipt = await queries.GetAsync(id, cancellationToken);
        if (receipt is null)
        {
            return NotFound();
        }

        var addressResolution = await addressResolutionStore.GetAsync(id, cancellationToken);
        Receipt = receipt;
        AddressResolution = addressResolution ?? new(
            id,
            0,
            InspectionAddressResolutionState.Unresolved,
            Ext18InspectionAddressPolicy.Evaluate(receipt),
            null,
            null,
            null);
        PrepareAddressCommand();
        return null;
    }

    [LoggerMessage(
        EventId = 1200,
        Level = LogLevel.Warning,
        Message = "Case acceptance exhausted the identity sequence for intake receipt {ReceiptId}.")]
    private static partial void LogIdentitySequenceExhausted(
        ILogger logger,
        Guid receiptId,
        Exception exception);

    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Warning,
        Message = "Case acceptance failed closed for intake receipt {ReceiptId}.")]
    private static partial void LogCaseAcceptanceFailed(
        ILogger logger,
        Guid receiptId,
        Exception exception);

    [LoggerMessage(
        EventId = 1202,
        Level = LogLevel.Warning,
        Message = "Inspection-address resolution failed closed for intake receipt {ReceiptId}.")]
    private static partial void LogAddressResolutionFailed(
        ILogger logger,
        Guid receiptId,
        Exception exception);

}
