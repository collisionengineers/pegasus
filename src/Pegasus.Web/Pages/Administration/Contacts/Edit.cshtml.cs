using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Administration.Contacts;

[Authorize(Policy = StaffRoleNames.Administrator)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EditModel(
    IContactDirectoryQueries contacts,
    IContactDirectoryAdministration administration,
    IEditScopeLeases editScopes,
    IGetPrincipal getPrincipal,
    IGetPrincipalCredential getCredential,
    IIssuePrincipalCredential issueCredential,
    IPausePrincipalCredential pauseCredential,
    IResumePrincipalCredential resumeCredential,
    IRevokePrincipalCredential revokeCredential,
    IUpdatePrincipalReportSettings updatePrincipalReportSettings,
    IUpdatePrincipalDefaultInspectionLocation updatePrincipalDefaultInspectionLocation,
    IReplacePrincipal replacePrincipal) : AdministrationPageModel
{
    public ContactDirectoryRecord? Contact { get; private set; }
    public PrincipalAdministrationSummary? Principal { get; private set; }
    public PrincipalCredentialRecord? Credential { get; private set; }
    public string? IssuedSecret { get; private set; }
    public IReadOnlyList<PrincipalAdministrationDetails> PrincipalChoices { get; private set; } = [];
    public IReadOnlyList<string> AcceptedIdentities => Principal?.Code is { } code
        && PrincipalMailRoutePolicy.AcceptedIdentities.TryGetValue(code, out var identities) ? identities : [];

    [BindProperty] public Guid ContactId { get; set; }
    [BindProperty] public long ExpectedVersion { get; set; }
    [BindProperty, Required, StringLength(ContactDirectoryPolicy.MaximumNameLength)] public string Name { get; set; } = string.Empty;
    [BindProperty, StringLength(ContactDirectoryPolicy.MaximumContactPersonLength)] public string? ContactPerson { get; set; }
    [BindProperty, StringLength(ContactDirectoryPolicy.MaximumEmailLength)] public string? Email { get; set; }
    [BindProperty, StringLength(ContactDirectoryPolicy.MaximumTelephoneLength)] public string? Telephone { get; set; }
    [BindProperty, StringLength(ContactDirectoryPolicy.MaximumAddressLength)] public string? Address { get; set; }
    [BindProperty, StringLength(ContactDirectoryPolicy.MaximumPostcodeLength)] public string? Postcode { get; set; }
    [BindProperty, StringLength(ContactDirectoryPolicy.MaximumGuidanceTemplateLength)] public string? GuidanceTemplate { get; set; }
    [BindProperty] public long GuidanceTemplateVersion { get; set; }
    [BindProperty] public bool Active { get; set; } = true;
    [BindProperty] public ContactRole[] Roles { get; set; } = [];
    [BindProperty, StringLength(20)] public string? PrincipalCode { get; set; }
    [BindProperty] public CaseInspectionMode PrincipalInspectionMode { get; set; } = CaseInspectionMode.PhysicalAddress;
    [BindProperty] public string[] PrincipalAssociationKeys { get; set; } = [];
    [BindProperty] public string OperationKey { get; set; } = NewOperationKey();
    [BindProperty] public string LeaseToken { get; set; } = string.Empty;

    [BindProperty] public long PrincipalExpectedVersion { get; set; }
    [BindProperty] public PrincipalReportGenerationPolicy ReportGenerationPolicy { get; set; }
    [BindProperty] public bool IncludeOriginalInstructionSender { get; set; }
    [BindProperty] public string[] AdditionalReportRecipients { get; set; } = [];
    [BindProperty] public string? ReportSettingsOperationKey { get; set; } = NewOperationKey();
    [BindProperty] public bool LocationIsImageBasedAssessment { get; set; }
    [BindProperty, StringLength(200)] public string? LocationLabel { get; set; }
    [BindProperty, StringLength(500)] public string? LocationAddress { get; set; }
    [BindProperty, StringLength(20)] public string? LocationPostcode { get; set; }
    [BindProperty] public string? LocationOperationKey { get; set; } = NewOperationKey();
    [BindProperty] public long CredentialVersion { get; set; }
    [BindProperty] public string? CredentialOperationKey { get; set; } = NewOperationKey();
    [BindProperty, StringLength(OrganizationAdministrationPolicy.MaximumReasonLength)] public string? CredentialReason { get; set; }
    [BindProperty] public long ReplacementExpectedVersion { get; set; }
    [BindProperty, StringLength(OrganizationAdministrationPolicy.MaximumPrincipalCodeLength)] public string SuccessorCode { get; set; } = string.Empty;
    [BindProperty, StringLength(OrganizationAdministrationPolicy.MaximumReasonLength)] public string? ReplacementReason { get; set; }
    [BindProperty] public string ReplacementOperationKey { get; set; } = NewOperationKey();
    public bool IsEditing => !string.IsNullOrWhiteSpace(LeaseToken);

    /// <summary>
    /// Set when this operator is already editing this contact in another
    /// window, so the page offers the take-over that ends the other window's
    /// claim instead of an Edit that would be refused again.
    /// </summary>
    public bool CanTakeOverEdit { get; private set; }

    /// <summary>The record as the operator reading an ownership sentence names it.</summary>
    private const string RecordName = "contact";

    /// <summary>
    /// Another colleague's claim. This area never resolves the holder's name,
    /// so the shared wording is used with an unnamed holder rather than a
    /// second sentence of its own.
    /// </summary>
    private static readonly string HeldByAnother =
        EditModeDisplay.HeldBy(RecordName, CaseEditAuthorityHolder.Unnamed, isSelf: false);

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        ContactId = id;
        Contact = await contacts.GetAsync(actor, id, cancellationToken);
        if (Contact is null) return NotFound();
        ExpectedVersion = Contact.Version;
        CopyFrom(Contact);
        await LoadPrincipalAsync(actor, cancellationToken);
        InitializePrincipalSettings();
        PrincipalChoices = await contacts.ListPrincipalChoicesAsync(actor, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostEditAsync(
        Guid id,
        bool takeOver,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!TargetsContact(id)) return BadRequest();
        ContactId = id;
        Contact = await contacts.GetAsync(actor, id, cancellationToken);
        if (Contact is null) return NotFound();
        ModelState.Clear();
        CopyFrom(Contact);
        await LoadPrincipalAsync(actor, cancellationToken);
        InitializePrincipalSettings();
        try
        {
            var lease = await editScopes.ClaimAsync(
                new(EditScopeKind.Contact, ContactId, ExpectedVersion, actor, OperationKey)
                {
                    TakeOver = takeOver
                },
                cancellationToken);
            LeaseToken = lease.Token;
        }
        catch (EditScopeHeldElsewhereException)
        {
            CanTakeOverEdit = true;
            ModelState.AddModelError(string.Empty, EditModeDisplay.HeldElsewhere(RecordName));
        }
        catch (EditScopeConflictException)
        {
            ModelState.AddModelError(string.Empty, HeldByAnother);
        }
        catch (EditScopeVersionConflictException)
        {
            ModelState.AddModelError(string.Empty, "This contact changed. Reload it before editing.");
        }
        await PopulateAsync(actor, cancellationToken, copyContactFields: false);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!TargetsContact(id)) return BadRequest();
        ContactId = id;
        Contact = await contacts.GetAsync(actor, id, cancellationToken);
        if (Contact is null) return NotFound();
        // These fields belong to separate consequential Principal actions.
        ModelState.Remove(nameof(SuccessorCode));
        ModelState.Remove(nameof(ReplacementReason));
        ModelState.Remove(nameof(LeaseToken));
        if (!IsOperationKeyValid(OperationKey)) ModelState.AddModelError(string.Empty, "The form has expired. Open the contact again.");
        if (!Enum.IsDefined(PrincipalInspectionMode)) ModelState.AddModelError(nameof(PrincipalInspectionMode), "Select an inspection mode.");
        if (ModelState.IsValid)
        {
            try
            {
                var associations = ParsePrincipalAssociations();
                await administration.SaveAsync(new(actor, ContactId, ExpectedVersion, Name, ContactPerson, Email, Telephone, Address, Postcode, Active, Roles, PrincipalCode, PrincipalInspectionMode, associations, OperationKey, LeaseToken ?? string.Empty, GuidanceTemplate, GuidanceTemplateVersion), cancellationToken);
                TempData["AdministrationStatus"] = "The contact was saved.";
                return RedirectToPage("Index");
            }
            catch (ContactDirectoryException exception) { ModelState.AddModelError(string.Empty, ContactErrorMessage(exception.Error)); }
            catch (EditScopeConflictException) { ModelState.AddModelError(string.Empty, HeldByAnother); }
            catch (EditScopeExpiredException) { ModelState.AddModelError(string.Empty, "Your edit session expired. Reload the contact."); }
            catch (EditScopeVersionConflictException) { ModelState.AddModelError(string.Empty, "This contact changed. Reload it before making further changes."); }
            catch (ArgumentException exception)
            {
                // A named field error is shown against its own control; every
                // other refusal keeps the general notice.
                if (exception.ParamName == nameof(Telephone))
                {
                    ModelState.AddModelError(nameof(Telephone), "Telephone must be digits.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "The contact details were not accepted.");
                }
            }
        }
        await PopulateAsync(actor, cancellationToken, copyContactFields: false);
        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!TargetsContact(id)) return BadRequest();
        ContactId = id;
        if (!string.IsNullOrEmpty(LeaseToken))
        {
            try { await editScopes.ReleaseAsync(new(EditScopeKind.Contact, ContactId, actor, OperationKey, LeaseToken), cancellationToken); }
            catch (Exception exception) when (exception is EditScopeExpiredException or EditScopeConflictException) { }
        }
        return RedirectToPage("Index");
    }

    /// <summary>
    /// The release a leaving page beacons. It is not an operator action: it
    /// answers 204 whether or not a scope was still there to release, so a
    /// duplicate beacon and a beacon that lost a race with Cancel are both
    /// ordinary outcomes. Antiforgery is validated as it is for every post.
    /// </summary>
    public async Task<IActionResult> OnPostReleaseScopeBeaconAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!TargetsContact(id)) return BadRequest();
        ContactId = id;
        if (ContactId == Guid.Empty || string.IsNullOrWhiteSpace(LeaseToken)) return new NoContentResult();
        try
        {
            await editScopes.ReleaseAsync(
                new(EditScopeKind.Contact, ContactId, actor, NewOperationKey(), LeaseToken),
                cancellationToken);
        }
        catch (Exception exception)
            when (exception is EditScopeExpiredException or EditScopeConflictException)
        {
            // The scope has already gone or has already been re-claimed by a
            // newer window of this operator's own session.
        }
        return new NoContentResult();
    }

    public async Task<IActionResult> OnPostHeartbeatEditAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!TargetsContact(id)) return BadRequest();
        ContactId = id;
        if (ContactId == Guid.Empty || string.IsNullOrWhiteSpace(LeaseToken))
        {
            return new ConflictObjectResult("Editing this contact has ended. Reload it before making further changes.");
        }

        try
        {
            await editScopes.HeartbeatAsync(
                new(EditScopeKind.Contact, ContactId, actor, LeaseToken), cancellationToken);
            return new OkResult();
        }
        catch (EditScopeExpiredException)
        {
            return new ConflictObjectResult("Editing this contact has ended. Reload it before making further changes.");
        }
    }

    public async Task<IActionResult> OnPostUpdateReportSettingsAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (id == Guid.Empty) return BadRequest();
        ContactId = id;
        ClearModelStatePreservingErrors(
            nameof(ReportGenerationPolicy),
            nameof(IncludeOriginalInstructionSender),
            nameof(AdditionalReportRecipients),
            nameof(PrincipalExpectedVersion),
            nameof(ExpectedVersion),
            nameof(LeaseToken),
            nameof(ReportSettingsOperationKey));
        if (!IsEditing) ModelState.AddModelError(string.Empty, "Select Edit contact before changing principal settings.");
        var expectedVersion = PrincipalExpectedVersion;
        if (!await LoadPrincipalAsync(actor, cancellationToken)) return NotFound();
        if (!IsOperationKeyValid(ReportSettingsOperationKey)) ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        if (!Enum.IsDefined(ReportGenerationPolicy)) ModelState.AddModelError(nameof(ReportGenerationPolicy), "Select a report generation route.");
        if (ModelState.IsValid)
        {
            try
            {
                var recipients = PrincipalReportRecipientSettings.Normalize(
                    IncludeOriginalInstructionSender, AdditionalReportRecipients);
                await updatePrincipalReportSettings.ExecuteAsync(new(
                    Principal!.Id, expectedVersion, actor, ReportSettingsOperationKey!,
                    "Updated report settings", ReportGenerationPolicy, recipients,
                    ExpectedVersion, LeaseToken), cancellationToken);
                TempData["AdministrationStatus"] = "The principal's report settings were updated.";
                return RedirectToPage(new { id = ContactId });
            }
            catch (OrganizationAdministrationException exception) { ModelState.AddModelError(string.Empty, PrincipalErrorMessage(exception.Error)); }
            catch (EditScopeConflictException) { ModelState.AddModelError(string.Empty, HeldByAnother); }
            catch (EditScopeExpiredException) { ModelState.AddModelError(string.Empty, "Your edit session expired. Reload the contact."); }
            catch (EditScopeVersionConflictException) { ModelState.AddModelError(string.Empty, "This contact changed. Reload it before making further changes."); }
            catch (ArgumentException) { ModelState.AddModelError(string.Empty, "The settings were not accepted."); }
            catch (StaffAuthorizationException) { return Forbid(); }
        }
        await PopulateAsync(actor, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateLocationAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (id == Guid.Empty) return BadRequest();
        ContactId = id;
        ClearModelStatePreservingErrors(
            nameof(LocationIsImageBasedAssessment),
            nameof(LocationLabel),
            nameof(LocationAddress),
            nameof(LocationPostcode),
            nameof(PrincipalExpectedVersion),
            nameof(ExpectedVersion),
            nameof(LeaseToken),
            nameof(LocationOperationKey));
        if (!IsEditing) ModelState.AddModelError(string.Empty, "Select Edit contact before changing principal settings.");
        var expectedVersion = PrincipalExpectedVersion;
        if (!await LoadPrincipalAsync(actor, cancellationToken)) return NotFound();
        if (!IsOperationKeyValid(LocationOperationKey)) ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        if (!LocationIsImageBasedAssessment && string.IsNullOrWhiteSpace(LocationAddress)) ModelState.AddModelError(nameof(LocationAddress), "An address is required.");
        if (ModelState.IsValid)
        {
            try
            {
                await updatePrincipalDefaultInspectionLocation.ExecuteAsync(new(actor, Principal!.Id, expectedVersion, LocationOperationKey!, LocationIsImageBasedAssessment ? InspectionAddressEvidenceKind.ImageBasedAssessment : InspectionAddressEvidenceKind.PhysicalAddress, LocationLabel, LocationAddress, LocationPostcode, "manual", null, null,
                    ExpectedVersion, LeaseToken), cancellationToken);
                TempData["AdministrationStatus"] = "The principal's default inspection location was updated.";
                return RedirectToPage(new { id = ContactId });
            }
            catch (OrganizationAdministrationException exception) { ModelState.AddModelError(string.Empty, PrincipalErrorMessage(exception.Error)); }
            catch (EditScopeConflictException) { ModelState.AddModelError(string.Empty, HeldByAnother); }
            catch (EditScopeExpiredException) { ModelState.AddModelError(string.Empty, "Your edit session expired. Reload the contact."); }
            catch (EditScopeVersionConflictException) { ModelState.AddModelError(string.Empty, "This contact changed. Reload it before making further changes."); }
            catch (ArgumentException) { ModelState.AddModelError(string.Empty, "The default location was not accepted."); }
            catch (StaffAuthorizationException) { return Forbid(); }
        }
        await PopulateAsync(actor, cancellationToken);
        return Page();
    }

    public Task<IActionResult> OnPostIssueCredentialAsync(Guid id, CancellationToken cancellationToken) => ChangeCredentialAsync(id, "issue", cancellationToken);
    public Task<IActionResult> OnPostPauseCredentialAsync(Guid id, CancellationToken cancellationToken) => ChangeCredentialAsync(id, "pause", cancellationToken);
    public Task<IActionResult> OnPostResumeCredentialAsync(Guid id, CancellationToken cancellationToken) => ChangeCredentialAsync(id, "resume", cancellationToken);
    public Task<IActionResult> OnPostRevokeCredentialAsync(Guid id, CancellationToken cancellationToken) => ChangeCredentialAsync(id, "revoke", cancellationToken);

    public async Task<IActionResult> OnPostReplaceAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (id == Guid.Empty) return BadRequest();
        ContactId = id;
        ClearModelStatePreservingErrors(
            nameof(SuccessorCode),
            nameof(ReplacementReason),
            nameof(ReplacementExpectedVersion),
            nameof(ExpectedVersion),
            nameof(LeaseToken),
            nameof(ReplacementOperationKey));
        if (!IsEditing) ModelState.AddModelError(string.Empty, "Select Edit contact before changing principal settings.");
        var expectedVersion = ReplacementExpectedVersion;
        if (!await LoadPrincipalAsync(actor, cancellationToken)) return NotFound();
        if (!IsOperationKeyValid(ReplacementOperationKey)) ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        if (string.IsNullOrWhiteSpace(SuccessorCode)) ModelState.AddModelError(nameof(SuccessorCode), "A new principal code is required.");
        if (ModelState.IsValid)
        {
            try
            {
                await replacePrincipal.ExecuteAsync(new(Principal!.Id, expectedVersion, SuccessorCode, actor, ReplacementOperationKey, ReplacementReason,
                    ExpectedVersion, LeaseToken), cancellationToken);
                TempData["AdministrationStatus"] = "The predecessor was disabled and its linked successor was created.";
                return RedirectToPage("Index");
            }
            catch (OrganizationAdministrationException exception) { ModelState.AddModelError(string.Empty, PrincipalErrorMessage(exception.Error)); }
            catch (EditScopeConflictException) { ModelState.AddModelError(string.Empty, HeldByAnother); }
            catch (EditScopeExpiredException) { ModelState.AddModelError(string.Empty, "Your edit session expired. Reload the contact."); }
            catch (EditScopeVersionConflictException) { ModelState.AddModelError(string.Empty, "This contact changed. Reload it before making further changes."); }
            catch (ArgumentException) { ModelState.AddModelError(string.Empty, "The replacement details were not accepted."); }
            catch (StaffAuthorizationException) { return Forbid(); }
        }
        await PopulateAsync(actor, cancellationToken);
        return Page();
    }

    private async Task<IActionResult> ChangeCredentialAsync(Guid id, string action, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (id == Guid.Empty) return BadRequest();
        ContactId = id;
        ClearModelStatePreservingErrors(
            nameof(CredentialReason),
            nameof(CredentialVersion),
            nameof(ExpectedVersion),
            nameof(LeaseToken),
            nameof(CredentialOperationKey));
        if (!IsEditing) ModelState.AddModelError(string.Empty, "Select Edit contact before changing principal settings.");
        var submittedVersion = CredentialVersion;
        if (!await LoadPrincipalAsync(actor, cancellationToken)) return NotFound();
        if (!IsOperationKeyValid(CredentialOperationKey)) ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        if (ModelState.IsValid)
        {
            try
            {
                var request = new PrincipalCredentialCommandRequest(Principal!.Id, submittedVersion, actor, CredentialOperationKey!, CredentialReason,
                    ExpectedVersion, LeaseToken);
                if (action == "issue")
                {
                    var result = await issueCredential.ExecuteAsync(request, cancellationToken);
                    Credential = result.Credential;
                    IssuedSecret = result.Secret;
                    CredentialVersion = Credential.Version;
                    CredentialOperationKey = NewOperationKey();
                    InitializePrincipalSettings();
                    if (Contact is not null) CopyFrom(Contact);
                    return Page();
                }
                Credential = action switch
                {
                    "pause" => await pauseCredential.ExecuteAsync(request, cancellationToken),
                    "resume" => await resumeCredential.ExecuteAsync(request, cancellationToken),
                    "revoke" => await revokeCredential.ExecuteAsync(request, cancellationToken),
                    _ => throw new InvalidOperationException("Unknown credential action.")
                };
                TempData["AdministrationStatus"] = "The provider credential was updated.";
                return RedirectToPage(new { id = ContactId });
            }
            catch (PrincipalCredentialException exception)
            {
                ModelState.AddModelError(string.Empty, exception.Error switch
                {
                    PrincipalCredentialError.StaleVersion => "The API key changed. Retry from the current settings.",
                    PrincipalCredentialError.OperationConflict => "The form was already used for a different operation.",
                    PrincipalCredentialError.PrincipalInactive => "The principal is disabled.",
                    _ => "The API key change was not accepted."
                });
            }
            catch (EditScopeConflictException) { ModelState.AddModelError(string.Empty, HeldByAnother); }
            catch (EditScopeExpiredException) { ModelState.AddModelError(string.Empty, "Your edit session expired. Reload the contact."); }
            catch (EditScopeVersionConflictException) { ModelState.AddModelError(string.Empty, "This contact changed. Reload it before making further changes."); }
            catch (ArgumentException) { ModelState.AddModelError(string.Empty, "The API key change was not accepted."); }
            catch (StaffAuthorizationException) { return Forbid(); }
        }
        await PopulateAsync(actor, cancellationToken);
        return Page();
    }

    private async Task PopulateAsync(ActionActor actor, CancellationToken cancellationToken, bool copyContactFields = true)
    {
        Contact = await contacts.GetAsync(actor, ContactId, cancellationToken);
        if (Contact is not null)
        {
            if (copyContactFields) CopyFrom(Contact);
            await LoadPrincipalAsync(actor, cancellationToken);
            InitializePrincipalSettings();
        }
        PrincipalChoices = await contacts.ListPrincipalChoicesAsync(actor, cancellationToken);
    }

    private void ClearModelStatePreservingErrors(params string[] fieldNames)
    {
        var errors = fieldNames
            .Select(name => (Name: name, Entry: ModelState.TryGetValue(name, out var entry) ? entry : null))
            .Where(item => item.Entry is { Errors.Count: > 0 })
            .SelectMany(item => item.Entry!.Errors.Select(error => new
            {
                item.Name,
                Message = string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? "The submitted value is not valid."
                    : error.ErrorMessage
            }))
            .ToArray();
        ModelState.Clear();
        foreach (var error in errors)
        {
            ModelState.AddModelError(error.Name, error.Message);
        }
    }

    private async Task<bool> LoadPrincipalAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        Contact ??= await contacts.GetAsync(actor, ContactId, cancellationToken);
        var principalIds = Contact?.OwnPrincipalIds ?? [];
        var details = await Task.WhenAll(principalIds.Select(id => getPrincipal.ExecuteAsync(actor, id, cancellationToken)));
        Principal = details.Where(detail => detail?.Principal.OrganizationId == ContactId)
            .Select(detail => detail!.Principal)
            .OrderByDescending(principal => principal.IsActive)
            .ThenBy(principal => principal.Code, StringComparer.Ordinal)
            .FirstOrDefault();
        if (Principal is null) return false;
        Credential = await getCredential.ExecuteAsync(actor, Principal.Id, cancellationToken);
        return true;
    }

    private void InitializePrincipalSettings()
    {
        if (Principal is null) return;
        PrincipalCode = Principal.Code;
        PrincipalInspectionMode = Principal.InspectionMode;
        PrincipalExpectedVersion = Principal.Version;
        ReplacementExpectedVersion = Principal.Version;
        ReportGenerationPolicy = Principal.ReportGenerationPolicy;
        IncludeOriginalInstructionSender = (Principal.ReportRecipients ?? PrincipalReportRecipientSettings.None).IncludeOriginalInstructionSender;
        AdditionalReportRecipients = (Principal.ReportRecipients ?? PrincipalReportRecipientSettings.None).AdditionalAddresses.ToArray();
        LocationIsImageBasedAssessment = Principal.DefaultInspectionAddress is null;
        LocationLabel = Principal.DefaultInspectionLocationLabel;
        LocationAddress = Principal.DefaultInspectionAddress;
        LocationPostcode = Principal.DefaultInspectionPostcode;
        CredentialVersion = Credential?.Version ?? 0;
    }

    private void CopyFrom(ContactDirectoryRecord contact)
    {
        Name = contact.Name; ContactPerson = contact.ContactPerson; Email = contact.Email; Telephone = contact.Telephone; Address = contact.Address; Postcode = contact.Postcode; GuidanceTemplate = contact.GuidanceTemplate; GuidanceTemplateVersion = contact.GuidanceTemplateVersion; Active = contact.Active; Roles = contact.Roles.ToArray(); PrincipalAssociationKeys = (contact.PrincipalAssociations ?? []).Select(association => $"{association.Role}:{association.PrincipalId:D}").ToArray();
    }

    private ContactPrincipalAssociation[] ParsePrincipalAssociations() =>
        PrincipalAssociationKeys.Select(key =>
        {
            var parts = key.Split(':');
            if (parts.Length != 2 || !Enum.TryParse<ContactRole>(parts[0], out var role)
                || role == ContactRole.Principal || !Guid.TryParse(parts[1], out var principalId))
            {
                throw new ArgumentException("Select valid role-specific principal links.");
            }
            return new ContactPrincipalAssociation(ContactId, role, principalId);
        }).ToArray();

    private bool TargetsContact(Guid id) => id != Guid.Empty && ContactId == id;

    public static string RoleLabel(ContactRole role) => role switch { ContactRole.Principal => "Principal", ContactRole.ClaimSource => "Claim Source", ContactRole.Repairer => "Repairer", ContactRole.Storage => "Storage", ContactRole.ThirdPartyEngineer => "Third Party Engineer", _ => role.ToString() };
    private static string ContactErrorMessage(ContactDirectoryError error) => error switch { ContactDirectoryError.DuplicateOrganizationName => "A contact with that organisation name already exists. Select it before adding a role.", ContactDirectoryError.DuplicatePrincipalCode => "That principal code already exists.", ContactDirectoryError.InvalidPrincipalAssociation => "A contact cannot link to itself as a principal, and only non-principal types can link to principals.", ContactDirectoryError.StaleVersion => "The contact changed after you opened it. Reload it and try again.", _ => "The contact could not be saved." };
    private static string PrincipalErrorMessage(OrganizationAdministrationError error) => error switch
    {
        OrganizationAdministrationError.PrincipalNotFound => "The principal no longer exists.",
        OrganizationAdministrationError.PrincipalInactive => "The principal is disabled. Change the settings on its successor instead.",
        OrganizationAdministrationError.PrincipalAlreadyReplaced => "The predecessor already has a linked successor.",
        OrganizationAdministrationError.DuplicatePrincipalCode => "That normalized successor code already exists.",
        OrganizationAdministrationError.StaleVersion => "The principal changed after this page was loaded. Review the current settings and retry.",
        OrganizationAdministrationError.OperationConflict => "The form was already used for a different operation. Retry from the current page.",
        _ => "The settings change was not accepted."
    };
}
