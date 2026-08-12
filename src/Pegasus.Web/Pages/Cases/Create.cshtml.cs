using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// Where a received file becomes a case.
/// </summary>
/// <remarks>
/// The manual route used to be a form at the bottom of the received-item
/// screen, reached by finding the row in a list, and it refused anything but
/// an item that needed sorting. An upload now lands here directly with what
/// extraction found already in the boxes, and this is the only place in the
/// application that begins a staff allocation through <see cref="IAllocateIntake"/>.
///
/// <para><strong>One button.</strong> Creating a case takes up to three writes
/// — the corrected draft, the inspection address, and the acceptance itself.
/// They are sequenced here, on one submit, because
/// the operator's action is a single one: check the detail, create the case.
/// Three separate forms each demanding their own reason is the narration the
/// operator notes forbid.</para>
///
/// <para><strong>The version chain.</strong> Each write bumps the receipt
/// version, and each step takes the version the <em>previous step returned</em>
/// — never a re-read. Re-reading would reintroduce exactly the race the
/// acceptance replay guard exists to prevent: a version fetched after the fact
/// is not the version the operator reviewed.</para>
///
/// <para><strong>Replay.</strong> Every operation key derives from one
/// page-level operation id carried in a hidden field, so a double submit or a
/// retry after a mid-sequence failure replays the steps that already committed
/// instead of duplicating them. <see cref="ExpectedReceiptVersion"/> is
/// deliberately <em>not</em> advanced when a later step fails: the correction's
/// replay fingerprint includes the version it expected, so changing it would
/// turn a resumed submit into a conflict.</para>
/// </remarks>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed partial class CreateModel(
    IGetIntake getIntake,
    IResolveIntake resolveIntake,
    IAllocateIntake allocateIntake,
    IInspectionAddressResolutionStore addressResolutionStore,
    IProviderInspectionModeStore providerInspectionModeStore,
    ILogger<CreateModel> logger) : PageModel
{
    /// <summary>
    /// How the inspection address on this screen is being settled.
    /// </summary>
    /// <remarks>
    /// The choice is always explicit. EXT-18 prohibits inferring an address,
    /// and a screen that silently preferred one source over another would be
    /// inferring on the operator's behalf.
    /// </remarks>
    public enum AddressChoiceKind
    {
        UseFoundAddress,
        UseEnteredAddress
    }

    public IntakeReceipt Receipt { get; private set; } = null!;

    public InspectionAddressResolutionSnapshot AddressResolution { get; private set; } = null!;

    public bool ProviderIsImageBased { get; private set; }

    public string? UploadOutcomeMessage { get; private set; }

    /// <summary>
    /// Set when the item cannot become a case at all, so the page states why
    /// instead of offering a button that would fail.
    /// </summary>
    public string? RefusalMessage { get; private set; }

    [BindProperty]
    public Guid ReceiptId { get; set; }

    // Bound as nullable strings deliberately. Under nullable reference types a
    // non-nullable bound string is implicitly required, and these fields are
    // legitimately empty: there is no fingerprint when nothing was extracted,
    // and no Audit reason unless the case is an Audit. Their real rules are
    // stated below, in the operator's words, rather than by the binder.
    [BindProperty]
    public string? OperationId { get; set; }

    [BindProperty]
    public long ExpectedReceiptVersion { get; set; }

    [BindProperty]
    public string? AddressSuggestionFingerprint { get; set; }

    [BindProperty]
    public string? Reason { get; set; }

    [BindProperty]
    public string? PrincipalCode { get; set; }

    [BindProperty]
    public CaseType CaseType { get; set; } = CaseType.Inspection;

    [BindProperty]
    public string? SuggestedPrincipalCode { get; set; }

    [BindProperty]
    public string? ClaimantName { get; set; }

    [BindProperty]
    public string? ClaimNumber { get; set; }

    [BindProperty]
    public string? VehicleRegistration { get; set; }

    [BindProperty]
    public string? VehicleMake { get; set; }

    [BindProperty]
    public string? VehicleModel { get; set; }

    [BindProperty]
    public long? VehicleMileage { get; set; }

    [BindProperty]
    public string? AccidentCircumstances { get; set; }

    [BindProperty]
    public DateOnly? DateOfIncident { get; set; }

    [BindProperty]
    public DateOnly? InstructionDate { get; set; }

    [BindProperty]
    public DateOnly? InspectionDate { get; set; }

    [BindProperty]
    public AddressChoiceKind AddressChoice { get; set; } = AddressChoiceKind.UseFoundAddress;

    [BindProperty]
    public string? InspectionAddress { get; set; }

    [BindProperty]
    public bool InstructionComplete { get; set; }

    [BindProperty]
    public bool ImagesComplete { get; set; }

    [BindProperty]
    public bool InstructionConfirmedByStaff { get; set; }

    [BindProperty]
    public bool ImagesConfirmedByStaff { get; set; }

    /// <summary>
    /// The address extraction proposed, when it proposed exactly one.
    /// </summary>
    public InspectionAddressSuggestion? AddressSuggestion =>
        AddressResolution.Evaluation.Suggestion;

    /// <summary>
    /// Whether a person has already settled the address on an earlier attempt,
    /// in which case this screen shows it and does not ask again.
    /// </summary>
    public bool AddressAlreadySettled =>
        InspectionAddressResolutionPolicy.IsStaffResolved(AddressResolution.State);

    /// <summary>
    /// Whether the operator is asked for an address at all.
    /// </summary>
    public bool AsksForAddress => !InspectionAddressResolutionPolicy.SatisfiesCaseCreation(
        AddressResolution.State,
        ProviderIsImageBased);

    public InstructionDraftFieldsView DraftFields => new(
        new(
            SuggestedPrincipalCode,
            ClaimantName,
            ClaimNumber,
            VehicleRegistration,
            VehicleMake,
            VehicleModel,
            VehicleMileage,
            AccidentCircumstances,
            DateOfIncident,
            InstructionDate,
            EffectiveInspectionAddress(),
            InspectionDate),
        Receipt.Fields,
        IncludePrincipalCode: false,
        IncludeInspectionAddress: false);

    /// <summary>
    /// The resolution state in words. The persisted enum name is not an
    /// operator label and never reaches the screen.
    /// </summary>
    public static string AddressStateLabel(InspectionAddressResolutionState state) => state switch
    {
        InspectionAddressResolutionState.Unresolved => "Not found",
        InspectionAddressResolutionState.Suggested => "Found in the document",
        InspectionAddressResolutionState.Accepted => "Confirmed",
        InspectionAddressResolutionState.Corrected => "Corrected by staff",
        InspectionAddressResolutionState.Supplied => "Entered by staff",
        _ => throw new InvalidOperationException(
            $"Unknown inspection-address resolution state '{(int)state}'.")
    };

    public async Task<IActionResult> OnGetAsync(
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        var loadResult = await LoadAsync(receiptId, cancellationToken);
        if (loadResult is not null)
        {
            return loadResult;
        }

        // A reference is allocated once and never reused, so an item that
        // already has a case is shown its case rather than a form that would
        // try to make a second one.
        if ((Receipt.AcceptedCaseId ?? Receipt.CurrentCaseId) is { } existingCaseId)
        {
            TempData["CaseDetailsStatus"] = "This item already has a case.";
            return RedirectToPage("/Cases/Details", new { id = existingCaseId });
        }

        UploadOutcomeMessage = TempData["UploadOutcomeMessage"] as string;
        RefusalMessage = DescribeRefusal();
        var draft = Receipt.InstructionDraft;
        SuggestedPrincipalCode = draft?.SuggestedPrincipalCode;
        PrincipalCode = draft?.SuggestedPrincipalCode ?? string.Empty;
        ClaimantName = draft?.ClaimantName;
        ClaimNumber = draft?.ClaimNumber;
        VehicleRegistration = draft?.VehicleRegistration;
        VehicleMake = draft?.VehicleMake;
        VehicleModel = draft?.VehicleModel;
        VehicleMileage = draft?.VehicleMileage;
        AccidentCircumstances = draft?.AccidentCircumstances;
        DateOfIncident = draft?.DateOfIncident;
        InstructionDate = draft?.InstructionDate;
        InspectionDate = draft?.InspectionDate;
        CaseType = Receipt.MailClassificationDecision?.CaseType ?? CaseType.Inspection;
        AddressChoice = AddressSuggestion is null
            ? AddressChoiceKind.UseEnteredAddress
            : AddressChoiceKind.UseFoundAddress;
        InspectionAddress = AddressSuggestion is null
            ? draft?.InspectionAddress ?? string.Empty
            : string.Empty;
        ReceiptId = Receipt.Id;
        OperationId = Guid.NewGuid().ToString("N");
        ExpectedReceiptVersion = Receipt.Version;
        AddressSuggestionFingerprint = AddressSuggestion?.Fingerprint ?? string.Empty;
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken = default)
    {
        var loadResult = await LoadAsync(ReceiptId, cancellationToken);
        if (loadResult is not null)
        {
            return loadResult;
        }

        if ((Receipt.AcceptedCaseId ?? Receipt.CurrentCaseId) is { } allocatedCaseId)
        {
            TempData["CaseDetailsStatus"] = "This item already has a case.";
            return RedirectToPage("/Cases/Details", new { id = allocatedCaseId });
        }

        RefusalMessage = DescribeRefusal();
        if (RefusalMessage is not null)
        {
            return Page();
        }

        if (!StaffActorFactory.TryCreate(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
        {
            return Forbid();
        }

        // Everything is checked before anything is written. A correction that
        // lands and is then refused by the acceptance gate leaves the item
        // blocked with no warning, which is precisely the trap this screen
        // exists to close.
        var postedDraft = ValidateAndBuildDraft();
        if (!Guid.TryParseExact(OperationId, "N", out var operationId))
        {
            ModelState.AddModelError(
                string.Empty,
                "This request is no longer valid. Reload the page and try again.");
        }
        if (ExpectedReceiptVersion < 0)
        {
            ModelState.AddModelError(
                string.Empty,
                "The reviewed details are missing. Reload the page and try again.");
        }
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Validation above trimmed and normalised these, and refused the post
        // if any required one was empty, so they are non-null from here.
        var reason = Reason!;
        var principalCode = PrincipalCode!;

        try
        {
            // 1. The corrected draft. Always run, never skipped on a
            //    "nothing changed" test: one code path, a decision normalised
            //    by Core's own completeness rule, and an honest record that a
            //    person keyed or confirmed these values.
            var corrected = await resolveIntake.ExecuteAsync(
                new(
                    Receipt.Id,
                    ExpectedReceiptVersion,
                    actor,
                    $"case-create-draft:{operationId:N}",
                    reason,
                    IntakeResolutionKind.CorrectDraft,
                    postedDraft),
                cancellationToken);
            var version = corrected.Version;

            // 2. The inspection address, where a person still has to settle it.
            if (AsksForAddress)
            {
                var isSupplying = AddressSuggestion is null;
                var snapshot = await addressResolutionStore.ResolveAsync(
                    new(
                        Receipt.Id,
                        version,
                        isSupplying ? null : AddressSuggestionFingerprint,
                        isSupplying
                            ? InspectionAddressStaffDecision.SupplyAddress
                            : AddressChoice == AddressChoiceKind.UseFoundAddress
                                ? InspectionAddressStaffDecision.AcceptSuggestion
                                : InspectionAddressStaffDecision.CorrectSuggestion,
                        AddressChoice == AddressChoiceKind.UseFoundAddress && !isSupplying
                            ? null
                            : InspectionAddress,
                        actor,
                        DeriveOperationId(operationId, "address"),
                        HttpContext.TraceIdentifier),
                    cancellationToken);
                version = snapshot.ReceiptVersion;
            }

            // 3. The acceptance itself, at the version the last write returned.
            var allocation = await allocateIntake.AttemptStaffCreateAsync(
                new(
                    Receipt.Id,
                    version,
                    actor,
                    $"intake-accept:{operationId:N}",
                    reason,
                    CaseType,
                    principalCode,
                    new(
                        InstructionComplete,
                        ImagesComplete,
                        InstructionConfirmedByStaff,
                        ImagesConfirmedByStaff),
                    null,
                    corrected.InstructionDraft?.InspectionDate),
                cancellationToken);

            if (allocation.State.Status != IntakeAllocationProjectionStatus.Succeeded
                || allocation.State.CaseId is not { } caseId)
            {
                TempData["IntakeDetailsError"] = allocation.State.SafeReason
                    ?? "The case could not be created. No reference was allocated.";
                return RedirectToPage("/Intake/Details", new { id = Receipt.Id });
            }

            TempData["CaseDetailsStatus"] =
                $"Case {allocation.State.AuditReference ?? allocation.State.CaseReference} was created.";
            return RedirectToPage("/Cases/Details", new { id = caseId });
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (InspectionAddressResolutionConcurrencyException)
        {
            ModelState.AddModelError(
                string.Empty,
                "The details changed while you were working. Check the inspection address and try again.");
        }
        catch (CaseAcceptanceOperationConflictException)
        {
            ModelState.AddModelError(
                string.Empty,
                "This item was already turned into a case using different details. Reload the page.");
        }
        catch (CaseIdentitySequenceExhaustedException exception)
        {
            LogIdentitySequenceExhausted(logger, Receipt.Id, exception);
            ModelState.AddModelError(
                string.Empty,
                "The case reference sequence is exhausted. No case was created.");
        }
        catch (Exception exception) when (
            exception is IntakeVersionConflictException or IntakeOperationConflictException)
        {
            LogCaseCreationConflicted(logger, Receipt.Id, exception);
            ModelState.AddModelError(
                string.Empty,
                "This item changed while you were working. Reload and try again.");
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            LogCaseCreationFailed(logger, Receipt.Id, exception);
            ModelState.AddModelError(
                string.Empty,
                "The case could not be created. No reference was allocated; reload the page and try again.");
        }

        // The same operation id and the same expected version are re-rendered,
        // so pressing the button again resumes the sequence rather than
        // starting a second one. The address snapshot below is reloaded, so a
        // step that did commit is not asked for twice.
        await LoadAsync(ReceiptId, cancellationToken);
        return Page();
    }

    /// <summary>
    /// Validates every posted value and returns the draft the correction will
    /// write. Errors accumulate in <see cref="PageModel.ModelState"/>; the
    /// draft is returned regardless so the page can re-render what was typed.
    /// </summary>
    private InstructionDraft ValidateAndBuildDraft()
    {
        Reason = (Reason ?? string.Empty).Trim();
        if (Reason.Length == 0)
        {
            ModelState.AddModelError(nameof(Reason), "Record why this case is being created.");
        }
        else if (Reason.Length > 500)
        {
            ModelState.AddModelError(
                nameof(Reason),
                "The reason must be 500 characters or fewer.");
        }

        PrincipalCode = (PrincipalCode ?? string.Empty).Trim().ToUpperInvariant();
        if (PrincipalCode.Length == 0)
        {
            ModelState.AddModelError(nameof(PrincipalCode), "Enter the confirmed principal code.");
        }
        else if (PrincipalCode.Length > CasePrincipalCode.MaximumLength)
        {
            ModelState.AddModelError(
                nameof(PrincipalCode),
                $"The principal code must be {CasePrincipalCode.MaximumLength} characters or fewer.");
        }

        if (!Enum.IsDefined(CaseType))
        {
            ModelState.AddModelError(nameof(CaseType), "Choose a valid case type.");
        }

        ValidateAddressChoice();
        ValidateAuditCannotBeManuallyCreated();

        var draft = new InstructionDraft(
            Optional(SuggestedPrincipalCode) ?? Optional(PrincipalCode),
            Optional(ClaimantName),
            Optional(ClaimNumber),
            Optional(VehicleRegistration),
            Optional(VehicleMake),
            Optional(VehicleModel),
            VehicleMileage,
            Optional(AccidentCircumstances),
            DateOfIncident,
            InstructionDate,
            EffectiveInspectionAddress(),
            InspectionDate);
        // Only identity-critical detail blocks allocation. Thin ordinary detail
        // is retained on the case in `Not ready`, which is what the requirement
        // asks for once Principal and Case type are established.
        foreach (var missing in InstructionDraftCompleteness.MissingIdentityCriticalFieldNames(draft))
        {
            ModelState.AddModelError(
                string.Empty,
                $"{missing} is needed before this item can become a case.");
        }

        return draft;
    }

    private void ValidateAddressChoice()
    {
        if (!AsksForAddress)
        {
            return;
        }

        if (AddressSuggestion is null)
        {
            // Nothing was extracted, so the only route is a person stating the
            // location. That is not inference and EXT-18 permits it.
            if (string.IsNullOrWhiteSpace(InspectionAddress))
            {
                ModelState.AddModelError(
                    nameof(InspectionAddress),
                    "Enter the inspection address. Nothing in the document said where the vehicle is.");
            }

            return;
        }

        if (!Enum.IsDefined(AddressChoice))
        {
            ModelState.AddModelError(
                nameof(AddressChoice),
                "Choose whether to use the address that was found or one you enter.");
            return;
        }

        if (AddressChoice == AddressChoiceKind.UseEnteredAddress
            && string.IsNullOrWhiteSpace(InspectionAddress))
        {
            ModelState.AddModelError(
                nameof(InspectionAddress),
                "Enter the inspection address you want to use instead.");
        }

        if (string.IsNullOrEmpty(AddressSuggestionFingerprint))
        {
            ModelState.AddModelError(
                string.Empty,
                "The address evidence on this page is stale. Reload and try again.");
        }
    }

    private void ValidateAuditCannotBeManuallyCreated()
    {
        if (CaseType == CaseType.Audit)
        {
            ModelState.AddModelError(
                string.Empty,
                "Audits are created automatically from the retained Audit instruction and original report.");
        }
    }

    /// <summary>
    /// The address that goes into the draft, so the completeness check and the
    /// panel above it agree about what the operator chose.
    /// </summary>
    private string? EffectiveInspectionAddress()
    {
        if (ProviderIsImageBased)
        {
            // The provider's own recorded mode, not something derived from the
            // document. The address panel says as much, and a physical address
            // can still be recorded on the case afterwards with a reason.
            return AddressResolution.ResolvedValue
                ?? AddressSuggestion?.Value
                ?? Ext18InspectionAddressPolicy.ImageBasedAssessment;
        }

        if (AddressAlreadySettled)
        {
            return AddressResolution.ResolvedValue;
        }

        return AddressSuggestion is not null && AddressChoice == AddressChoiceKind.UseFoundAddress
            ? AddressSuggestion.Value
            : Optional(InspectionAddress);
    }

    private string? DescribeRefusal()
    {
        if (Receipt.MailClassificationDecision?.CaseType == CaseType.Audit)
        {
            return "This Audit is created automatically from the retained Audit instruction and original report.";
        }

        // Little or no text came out of the document, which is exactly the
        // hand-keyed case: the correction step normalises the decision, so it
        // is allowed through rather than refused.
        if (Receipt.Decision == IntakeDecision.OcrRequired
            || IntakeDecisionPolicy.CanBecomeCase(Receipt.Decision))
        {
            return null;
        }

        return Receipt.Decision switch
        {
            IntakeDecision.BlockedIntake =>
                "This item was blocked, with the reason recorded. It cannot become a case until it is corrected on the received item.",
            IntakeDecision.ImageIntakeRegistered =>
                "This item was registered as vehicle images. Image material never becomes a case on its own.",
            IntakeDecision.Unsupported =>
                "This file could not be read, so there is nothing to create a case from.",
            _ =>
                "This file failed while it was being processed, so there is nothing to create a case from."
        };
    }

    private async Task<IActionResult?> LoadAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        if (!StaffActorFactory.TryCreate(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
        {
            return Forbid();
        }

        var receipt = await getIntake.ExecuteAsync(new(receiptId, actor), cancellationToken);
        if (receipt is null)
        {
            return NotFound();
        }

        Receipt = receipt;
        ReceiptId = receipt.Id;
        // The mode belongs to the principal the case is actually allocated
        // against. On the first GET that is the extracted suggestion, but as
        // soon as an operator confirms a different principal it is theirs —
        // otherwise reassigning an extracted image-based draft to a
        // physical-address principal silently skips address confirmation and
        // records "Image Based Assessment" against a provider that inspects in
        // person.
        var effectivePrincipalCode = Optional(PrincipalCode)
            ?? receipt.InstructionDraft?.SuggestedPrincipalCode;
        ProviderIsImageBased = await IsImageBasedAsync(effectivePrincipalCode, cancellationToken);
        AddressResolution = await addressResolutionStore.GetAsync(receiptId, cancellationToken)
            ?? new(
                receiptId,
                receipt.Version,
                InspectionAddressResolutionState.Unresolved,
                Ext18InspectionAddressPolicy.Evaluate(receipt),
                null,
                null,
                null);
        return null;
    }

    private async Task<bool> IsImageBasedAsync(
        string? principalCode,
        CancellationToken cancellationToken) =>
        !string.IsNullOrWhiteSpace(principalCode)
        && await providerInspectionModeStore.GetForPrincipalAsync(
            principalCode.Trim().ToUpperInvariant(),
            cancellationToken) == CaseInspectionMode.ImageBasedAssessment;

    private static string? Optional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    /// <summary>
    /// A stable identifier for one step of one submit, derived from the page's
    /// operation id, so a retry reuses it and replays rather than duplicating.
    /// </summary>
    private static Guid DeriveOperationId(Guid operationId, string purpose) =>
        new(SHA256.HashData(
            Encoding.UTF8.GetBytes($"case-create/{operationId:N}/{purpose}"))
            .AsSpan(0, 16));

    [LoggerMessage(
        EventId = 1210,
        Level = LogLevel.Warning,
        Message = "Case creation exhausted the identity sequence for intake receipt {ReceiptId}.")]
    private static partial void LogIdentitySequenceExhausted(
        ILogger logger,
        Guid receiptId,
        Exception exception);

    [LoggerMessage(
        EventId = 1211,
        Level = LogLevel.Warning,
        Message = "Case creation conflicted for intake receipt {ReceiptId}.")]
    private static partial void LogCaseCreationConflicted(
        ILogger logger,
        Guid receiptId,
        Exception exception);

    [LoggerMessage(
        EventId = 1212,
        Level = LogLevel.Warning,
        Message = "Case creation failed closed for intake receipt {ReceiptId}.")]
    private static partial void LogCaseCreationFailed(
        ILogger logger,
        Guid receiptId,
        Exception exception);
}
