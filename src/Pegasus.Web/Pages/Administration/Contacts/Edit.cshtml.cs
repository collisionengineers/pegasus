using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Address;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Reports;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Administration.Contacts;

[Authorize(Policy = StaffRoleNames.Administrator)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EditModel(
    IContactDirectoryQueries contacts,
    IContactDirectoryAdministration administration,
    IGetPrincipal getPrincipal,
    IGetPrincipalCredential getCredential,
    IIssuePrincipalCredential issueCredential,
    IPausePrincipalCredential pauseCredential,
    IResumePrincipalCredential resumeCredential,
    IRevokePrincipalCredential revokeCredential,
    IUpdatePrincipalReportSettings updatePrincipalReportSettings,
    IUpdatePrincipalDefaultInspectionLocation updatePrincipalDefaultInspectionLocation,
    IUpdatePrincipalSalvageMatrix updatePrincipalSalvageMatrix,
    IUpdatePrincipalReportSending updatePrincipalReportSending,
    IApprovedMailboxStore approvedMailboxes,
    IReplacePrincipal replacePrincipal) : AdministrationPageModel
{
    public ContactDirectoryRecord? Contact { get; private set; }

    // An active Principal's page is tabs (operator, 13 October 2026): a ?tab= query
    // picks the one panel the server renders, and each panel's save comes back to it.
    public const string ContactTab = "contact";
    public const string ReportGenerationTab = "report-generation";
    public const string ReportSendingTab = "report-sending";
    public const string SalvageMatrixTab = "salvage-matrix";
    public const string LocationTab = "default-inspection-location";
    public const string ApiTab = "principal-api";
    public static readonly IReadOnlyList<(string Key, string Label)> PrincipalTabs =
    [
        (ContactTab, OperatorLabels.PrincipalAdministration.ContactTab),
        (ReportGenerationTab, OperatorLabels.PrincipalAdministration.ReportGeneration),
        (ReportSendingTab, OperatorLabels.PrincipalAdministration.ReportSending),
        (SalvageMatrixTab, OperatorLabels.PrincipalAdministration.SalvageMatrix),
        (LocationTab, OperatorLabels.PrincipalAdministration.DefaultInspectionLocation),
        (ApiTab, OperatorLabels.PrincipalAdministration.PrincipalApi)
    ];
    [BindProperty(SupportsGet = true)] public string? Tab { get; set; }
    public string CurrentTab => PrincipalTabs.Any(tab => tab.Key == Tab) ? Tab! : ContactTab;
    // A refused Replace code shows its message beside Replace code, not in the panel.
    public bool ReplaceRefused { get; private set; }

    // Whether the form carried a field at all: a field the form does not yet render
    // keeps the record's current value rather than clearing it.
    private bool Posted(string field) => Request.HasFormContentType && Request.Form.ContainsKey(field);
    public PrincipalAdministrationSummary? Principal { get; private set; }
    public PrincipalCredentialRecord? Credential { get; private set; }
    public string? IssuedSecret { get; private set; }
    public IReadOnlyList<PrincipalAdministrationDetails> PrincipalChoices { get; private set; } = [];
    public IReadOnlyList<string> AcceptedIdentities => Principal?.Code is { } code
        && PrincipalMailRoutePolicy.AcceptedIdentities.TryGetValue(code, out var identities) ? identities : [];

    [BindProperty] public Guid ContactId { get; set; }
    [BindProperty] public long ExpectedVersion { get; set; }
    [BindProperty, Display(Name = OperatorLabels.Contacts.OrganisationNameField), Required(ErrorMessage = OperatorLabels.Contacts.NameRequired), StringLength(ContactDirectoryPolicy.MaximumNameLength, ErrorMessage = OperatorLabels.TooLongFormat)] public string Name { get; set; } = string.Empty;
    [BindProperty, Display(Name = OperatorLabels.Contacts.ContactPersonField), StringLength(ContactDirectoryPolicy.MaximumContactPersonLength, ErrorMessage = OperatorLabels.TooLongFormat)] public string? ContactPerson { get; set; }
    [BindProperty, Display(Name = OperatorLabels.Contacts.EmailField), StringLength(ContactDirectoryPolicy.MaximumEmailLength, ErrorMessage = OperatorLabels.TooLongFormat)] public string? Email { get; set; }
    [BindProperty, Display(Name = OperatorLabels.Contacts.PhoneField), StringLength(ContactDirectoryPolicy.MaximumTelephoneLength, ErrorMessage = OperatorLabels.TooLongFormat)] public string? Telephone { get; set; }
    [BindProperty, Display(Name = OperatorLabels.Contacts.AddressField), StringLength(ContactDirectoryPolicy.MaximumAddressLength, ErrorMessage = OperatorLabels.TooLongFormat)] public string? Address { get; set; }
    [BindProperty, Display(Name = OperatorLabels.Contacts.PostcodeField), StringLength(ContactDirectoryPolicy.MaximumPostcodeLength, ErrorMessage = OperatorLabels.TooLongFormat)] public string? Postcode { get; set; }
    [BindProperty, Display(Name = OperatorLabels.Contacts.NotesOnEveryCaseField), StringLength(ContactDirectoryPolicy.MaximumNotesOnEveryCaseLength, ErrorMessage = OperatorLabels.TooLongFormat)] public string? NotesOnEveryCase { get; set; }
    [BindProperty] public bool Active { get; set; } = true;
    [BindProperty, Display(Name = OperatorLabels.PrincipalAdministration.CodeField), StringLength(OrganizationAdministrationPolicy.MaximumPrincipalCodeLength, ErrorMessage = OperatorLabels.TooLongFormat)] public string? PrincipalCode { get; set; }
    [BindProperty] public CaseInspectionMode PrincipalInspectionMode { get; set; } = CaseInspectionMode.PhysicalAddress;
    [BindProperty] public Guid[] LinkedPrincipalIds { get; set; } = [];
    [BindProperty] public string OperationKey { get; set; } = NewOperationKey();

    [BindProperty] public long PrincipalExpectedVersion { get; set; }
    [BindProperty] public decimal? DefaultFee { get; set; }
    [BindProperty] public string? ReportSettingsOperationKey { get; set; } = NewOperationKey();
    [BindProperty] public string? SalvageMatrixOperationKey { get; set; } = NewOperationKey();
    // The salvage matrix rows as shown: the stored bands, or what was typed
    // when a save was refused.
    public IReadOnlyList<SalvageMatrixEntry> SalvageRows { get; private set; } = [];
    [BindProperty] public string? ReportSendingOperationKey { get; set; } = NewOperationKey();
    // The report sending rules as shown: the stored rules, or what was typed
    // when a save was refused.
    public PrincipalReportSendingRules ReportSendingRules { get; private set; } = PrincipalReportSendingRules.Default;
    public IReadOnlyList<string> SendFromChoices { get; private set; } = [];
    public IReadOnlyList<ContactChoice> ClaimSourceChoices { get; private set; } = [];
    public IReadOnlyList<ContactChoice> RepairerChoices { get; private set; } = [];
    [BindProperty] public bool LocationIsImageBasedAssessment { get; set; }
    [BindProperty, Display(Name = OperatorLabels.PrincipalAdministration.LocationLabelField), StringLength(200, ErrorMessage = OperatorLabels.TooLongFormat)] public string? LocationLabel { get; set; }
    [BindProperty, Display(Name = OperatorLabels.Contacts.AddressField), StringLength(500, ErrorMessage = OperatorLabels.TooLongFormat)] public string? LocationAddress { get; set; }
    [BindProperty, Display(Name = OperatorLabels.Contacts.PostcodeField), StringLength(20, ErrorMessage = OperatorLabels.TooLongFormat)] public string? LocationPostcode { get; set; }
    [BindProperty] public string? LocationOperationKey { get; set; } = NewOperationKey();
    [BindProperty] public long CredentialVersion { get; set; }
    [BindProperty] public string? CredentialOperationKey { get; set; } = NewOperationKey();
    [BindProperty] public long ReplacementExpectedVersion { get; set; }
    [BindProperty, Display(Name = OperatorLabels.PrincipalAdministration.NewCodeField), Required(ErrorMessage = OperatorLabels.PrincipalAdministration.NewCodeRequired), StringLength(OrganizationAdministrationPolicy.MaximumPrincipalCodeLength, ErrorMessage = OperatorLabels.TooLongFormat)] public string SuccessorCode { get; set; } = string.Empty;
    [BindProperty] public string? ReplacementOperationKey { get; set; } = NewOperationKey();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        ContactId = id;
        Contact = await contacts.GetAsync(actor, id, cancellationToken);
        if (Contact is null) return NotFound();
        ExpectedVersion = Contact.Version;
        CopyFrom(Contact);
        await LoadPrincipalAsync(actor, cancellationToken, readCredential: CurrentTab == ApiTab);
        InitializePrincipalSettings();
        await LoadReportSendingChoicesAsync(actor, cancellationToken);
        await LoadPrincipalChoicesAsync(actor, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!TargetsContact(id)) return BadRequest();
        ContactId = id;
        Tab = ContactTab;
        Contact = await contacts.GetAsync(actor, id, cancellationToken);
        if (Contact is null) return NotFound();
        // These fields belong to separate consequential Principal actions.
        ModelState.Remove(nameof(SuccessorCode));
        if (!IsOperationKeyValid(OperationKey)) ModelState.AddModelError(string.Empty, OperatorLabels.Contacts.EditFormExpired);
        if (!Enum.IsDefined(PrincipalInspectionMode)) ModelState.AddModelError(nameof(PrincipalInspectionMode), OperatorLabels.Contacts.SelectInspectionMode);
        if (ModelState.IsValid)
        {
            try
            {
                var isPrincipal = Contact.Role == ContactRole.Principal;
                await administration.SaveAsync(new(actor, ContactId, ExpectedVersion, Name, ContactPerson, Email, Telephone, Address, Postcode, Active, Contact.Role, isPrincipal ? PrincipalCode : null, PrincipalInspectionMode, isPrincipal ? [] : LinkedPrincipalIds, OperationKey,
                    Posted(nameof(NotesOnEveryCase)) ? NotesOnEveryCase : Contact.NotesOnEveryCase), cancellationToken);
                TempData["AdministrationStatus"] = OperatorLabels.Contacts.Saved;
                return RedirectToPage("Index");
            }
            catch (ContactDirectoryException exception) { ModelState.AddModelError(string.Empty, OperatorLabels.Contacts.ErrorMessage(exception.Error)); }
            catch (ArgumentException exception)
            {
                // A named field error is keyed to its own control; every other
                // refusal keeps the general notice.
                if (exception.ParamName == nameof(Telephone))
                {
                    ModelState.AddModelError(nameof(Telephone), OperatorLabels.Contacts.PhoneMustBeDigits);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, OperatorLabels.Contacts.DetailsNotAccepted);
                }
            }
        }
        await PopulateAsync(actor, cancellationToken, copyContactFields: false);
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateReportSettingsAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (id == Guid.Empty) return BadRequest();
        ContactId = id;
        Tab = ReportGenerationTab;
        ClearModelStatePreservingErrors(
            nameof(DefaultFee),
            nameof(PrincipalExpectedVersion),
            nameof(ExpectedVersion),
            nameof(ReportSettingsOperationKey));
        var expectedVersion = PrincipalExpectedVersion;
        if (!await LoadPrincipalAsync(actor, cancellationToken, readCredential: false)) return NotFound();
        if (!IsOperationKeyValid(ReportSettingsOperationKey)) ModelState.AddModelError(string.Empty, OperatorLabels.PrincipalAdministration.FormExpired);
        if (ModelState.IsValid)
        {
            try
            {
                await updatePrincipalReportSettings.ExecuteAsync(new(
                    Principal!.Id, expectedVersion, actor, ReportSettingsOperationKey!,
                    "Updated report settings",
                    DefaultFee ?? 0m,
                    ExpectedVersion), cancellationToken);
                TempData["AdministrationStatus"] = OperatorLabels.PrincipalAdministration.ReportSettingsUpdated;
                return RedirectToPage(new { id = ContactId, tab = Tab });
            }
            catch (OrganizationAdministrationException exception) { ModelState.AddModelError(string.Empty, PrincipalAdministrationErrorMessage(exception)); }
            catch (ArgumentException) { ModelState.AddModelError(string.Empty, OperatorLabels.PrincipalAdministration.SettingsNotAccepted); }
            catch (StaffAuthorizationException) { return Forbid(); }
        }
        var typedFee = DefaultFee;
        await PopulateAsync(actor, cancellationToken);
        // What was typed stays.
        DefaultFee = typedFee;
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateSalvageMatrixAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (id == Guid.Empty) return BadRequest();
        ContactId = id;
        Tab = SalvageMatrixTab;
        ClearModelStatePreservingErrors(
            nameof(PrincipalExpectedVersion),
            nameof(ExpectedVersion),
            nameof(SalvageMatrixOperationKey));
        var expectedVersion = PrincipalExpectedVersion;
        var posted = PostedSalvageRows();
        if (!await LoadPrincipalAsync(actor, cancellationToken, readCredential: false)) return NotFound();
        if (!IsOperationKeyValid(SalvageMatrixOperationKey)) ModelState.AddModelError(string.Empty, OperatorLabels.PrincipalAdministration.FormExpired);
        if (ModelState.IsValid)
        {
            try
            {
                await updatePrincipalSalvageMatrix.ExecuteAsync(new(
                    Principal!.Id, expectedVersion, actor, SalvageMatrixOperationKey!,
                    SalvageMatrix.FromEntries(posted), ExpectedVersion), cancellationToken);
                TempData["AdministrationStatus"] = OperatorLabels.PrincipalAdministration.SalvageMatrixUpdated;
                return RedirectToPage(new { id = ContactId, tab = Tab });
            }
            catch (SalvageMatrixException exception) { ModelState.AddModelError(string.Empty, SalvageMatrixErrorMessage(exception)); }
            catch (OrganizationAdministrationException exception) { ModelState.AddModelError(string.Empty, PrincipalAdministrationErrorMessage(exception)); }
            catch (ArgumentException) { ModelState.AddModelError(string.Empty, OperatorLabels.PrincipalAdministration.SettingsNotAccepted); }
            catch (StaffAuthorizationException) { return Forbid(); }
        }
        await PopulateAsync(actor, cancellationToken);
        // What was typed stays; the blank spare rows are drawn again anyway.
        SalvageRows = [.. posted.Where(row => !row.IsBlank)];
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateReportSendingAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (id == Guid.Empty) return BadRequest();
        ContactId = id;
        Tab = ReportSendingTab;
        ClearModelStatePreservingErrors(
            nameof(PrincipalExpectedVersion),
            nameof(ExpectedVersion),
            nameof(ReportSendingOperationKey));
        var expectedVersion = PrincipalExpectedVersion;
        var posted = PostedReportSending();
        if (!await LoadPrincipalAsync(actor, cancellationToken, readCredential: false)) return NotFound();
        if (!IsOperationKeyValid(ReportSendingOperationKey)) ModelState.AddModelError(string.Empty, OperatorLabels.PrincipalAdministration.FormExpired);
        if (ModelState.IsValid)
        {
            try
            {
                await updatePrincipalReportSending.ExecuteAsync(new(
                    Principal!.Id, expectedVersion, actor, ReportSendingOperationKey!,
                    posted, ExpectedVersion), cancellationToken);
                TempData["AdministrationStatus"] = OperatorLabels.PrincipalAdministration.ReportSendingUpdated;
                return RedirectToPage(new { id = ContactId, tab = Tab });
            }
            catch (ReportSendingRulesException exception) { ModelState.AddModelError(string.Empty, ReportSendingRulesErrorMessage(exception)); }
            catch (OrganizationAdministrationException exception) { ModelState.AddModelError(string.Empty, PrincipalAdministrationErrorMessage(exception)); }
            catch (ArgumentException) { ModelState.AddModelError(string.Empty, OperatorLabels.PrincipalAdministration.SettingsNotAccepted); }
            catch (StaffAuthorizationException) { return Forbid(); }
        }
        await PopulateAsync(actor, cancellationToken);
        // What was typed stays.
        ReportSendingRules = posted;
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateLocationAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (id == Guid.Empty) return BadRequest();
        ContactId = id;
        Tab = LocationTab;
        ClearModelStatePreservingErrors(
            nameof(LocationIsImageBasedAssessment),
            nameof(LocationLabel),
            nameof(LocationAddress),
            nameof(LocationPostcode),
            nameof(PrincipalExpectedVersion),
            nameof(ExpectedVersion),
            nameof(LocationOperationKey));
        var expectedVersion = PrincipalExpectedVersion;
        if (!await LoadPrincipalAsync(actor, cancellationToken, readCredential: false)) return NotFound();
        if (!IsOperationKeyValid(LocationOperationKey)) ModelState.AddModelError(string.Empty, OperatorLabels.PrincipalAdministration.FormExpired);
        if (!LocationIsImageBasedAssessment && string.IsNullOrWhiteSpace(LocationAddress)) ModelState.AddModelError(nameof(LocationAddress), OperatorLabels.PrincipalAdministration.AddressRequired);
        if (ModelState.IsValid)
        {
            try
            {
                await updatePrincipalDefaultInspectionLocation.ExecuteAsync(new(actor, Principal!.Id, expectedVersion, LocationOperationKey!, LocationIsImageBasedAssessment ? InspectionAddressEvidenceKind.ImageBasedAssessment : InspectionAddressEvidenceKind.PhysicalAddress, LocationLabel, LocationAddress, LocationPostcode, "manual", null, null,
                    ExpectedVersion), cancellationToken);
                TempData["AdministrationStatus"] = OperatorLabels.PrincipalAdministration.DefaultLocationUpdated;
                return RedirectToPage(new { id = ContactId, tab = Tab });
            }
            catch (OrganizationAdministrationException exception) { ModelState.AddModelError(string.Empty, PrincipalAdministrationErrorMessage(exception)); }
            catch (ArgumentException) { ModelState.AddModelError(string.Empty, OperatorLabels.PrincipalAdministration.DefaultLocationNotAccepted); }
            catch (StaffAuthorizationException) { return Forbid(); }
        }
        var (imageBased, label, address, postcode) = (LocationIsImageBasedAssessment, LocationLabel, LocationAddress, LocationPostcode);
        await PopulateAsync(actor, cancellationToken);
        // What was typed stays.
        (LocationIsImageBasedAssessment, LocationLabel, LocationAddress, LocationPostcode) = (imageBased, label, address, postcode);
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
            nameof(ReplacementExpectedVersion),
            nameof(ExpectedVersion),
            nameof(ReplacementOperationKey));
        var expectedVersion = ReplacementExpectedVersion;
        if (!await LoadPrincipalAsync(actor, cancellationToken, readCredential: false)) return NotFound();
        if (!IsOperationKeyValid(ReplacementOperationKey)) ModelState.AddModelError(string.Empty, OperatorLabels.PrincipalAdministration.FormExpired);
        if (ModelState.IsValid)
        {
            try
            {
                await replacePrincipal.ExecuteAsync(new(Principal!.Id, expectedVersion, SuccessorCode, actor, ReplacementOperationKey!, Reason: null,
                    ExpectedVersion), cancellationToken);
                TempData["AdministrationStatus"] = OperatorLabels.PrincipalAdministration.Replaced;
                return RedirectToPage("Index");
            }
            catch (OrganizationAdministrationException exception) { ModelState.AddModelError(string.Empty, PrincipalAdministrationErrorMessage(exception)); }
            // The rule a typed code fails is named; anything else keeps the general notice (R539).
            catch (ArgumentException exception) when (exception.ParamName == nameof(SuccessorCode)) { ModelState.AddModelError(nameof(SuccessorCode), OperatorLabels.PrincipalAdministration.CodeLettersAndDigitsOnly(OperatorLabels.PrincipalAdministration.NewCodeField)); }
            catch (ArgumentException) { ModelState.AddModelError(string.Empty, OperatorLabels.PrincipalAdministration.ReplacementNotAccepted); }
            catch (StaffAuthorizationException) { return Forbid(); }
        }
        ReplaceRefused = true;
        await PopulateAsync(actor, cancellationToken);
        return Page();
    }

    private async Task<IActionResult> ChangeCredentialAsync(Guid id, string action, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (id == Guid.Empty) return BadRequest();
        ContactId = id;
        Tab = ApiTab;
        ClearModelStatePreservingErrors(
            nameof(CredentialVersion),
            nameof(ExpectedVersion),
            nameof(CredentialOperationKey));
        var submittedVersion = CredentialVersion;
        if (!await LoadPrincipalAsync(actor, cancellationToken, readCredential: false)) return NotFound();
        if (!IsOperationKeyValid(CredentialOperationKey)) ModelState.AddModelError(string.Empty, OperatorLabels.PrincipalAdministration.FormExpired);
        if (ModelState.IsValid)
        {
            try
            {
                var request = new PrincipalCredentialCommandRequest(Principal!.Id, submittedVersion, actor, CredentialOperationKey!, Reason: null,
                    ExpectedVersion);
                if (action == "issue")
                {
                    var result = await issueCredential.ExecuteAsync(request, cancellationToken);
                    // The issued record is the credential the page draws.
                    await PopulateAsync(actor, cancellationToken, readCredential: false);
                    ExpectedVersion = Contact!.Version;
                    Credential = result.Credential;
                    IssuedSecret = result.Secret;
                    CredentialVersion = Credential.Version;
                    CredentialOperationKey = NewOperationKey();
                    return Page();
                }
                Credential = action switch
                {
                    "pause" => await pauseCredential.ExecuteAsync(request, cancellationToken),
                    "resume" => await resumeCredential.ExecuteAsync(request, cancellationToken),
                    "revoke" => await revokeCredential.ExecuteAsync(request, cancellationToken),
                    _ => throw new InvalidOperationException("Unknown credential action.")
                };
                TempData["AdministrationStatus"] = OperatorLabels.PrincipalAdministration.CredentialUpdated;
                return RedirectToPage(new { id = ContactId, tab = Tab });
            }
            catch (PrincipalCredentialException exception)
            {
                ModelState.AddModelError(string.Empty, OperatorLabels.PrincipalAdministration.CredentialErrorMessage(exception.Error));
            }
            catch (OrganizationAdministrationException exception) { ModelState.AddModelError(string.Empty, PrincipalAdministrationErrorMessage(exception)); }
            catch (ArgumentException) { ModelState.AddModelError(string.Empty, OperatorLabels.PrincipalAdministration.CredentialNotAccepted); }
            catch (StaffAuthorizationException) { return Forbid(); }
        }
        await PopulateAsync(actor, cancellationToken);
        return Page();
    }

    private async Task PopulateAsync(ActionActor actor, CancellationToken cancellationToken, bool copyContactFields = true, bool readCredential = true)
    {
        Contact = await contacts.GetAsync(actor, ContactId, cancellationToken);
        if (Contact is not null)
        {
            if (copyContactFields) CopyFrom(Contact);
            await LoadPrincipalAsync(actor, cancellationToken, readCredential: readCredential && CurrentTab == ApiTab);
            InitializePrincipalSettings();
            await LoadReportSendingChoicesAsync(actor, cancellationToken);
            await LoadPrincipalChoicesAsync(actor, cancellationToken);
        }
    }

    // The mailboxes a new message may leave from (the Outgoing ones that can
    // send, as the report send form offers them) and the contacts a rule may name,
    // read only for the Report sending tab, the one tab that draws them (#1542).
    private async Task LoadReportSendingChoicesAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        if (Principal is not { IsActive: true } || CurrentTab != ReportSendingTab) return;
        SendFromChoices = (await approvedMailboxes.ListAsync(cancellationToken))
            .Where(mailbox => mailbox.CanSend)
            .Select(mailbox => mailbox.Address)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        ClaimSourceChoices = await contacts.ListByRoleAsync(actor, ContactRole.ClaimSource, cancellationToken);
        RepairerChoices = await contacts.ListByRoleAsync(actor, ContactRole.Repairer, cancellationToken);
    }

    // The Principals a contact may link to: drawn only on the one panel of a
    // contact that is not a Principal (#1542).
    private async Task LoadPrincipalChoicesAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        if (Contact is not { Role: not ContactRole.Principal }) return;
        PrincipalChoices = await contacts.ListPrincipalChoicesAsync(actor, ContactId, cancellationToken);
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
                    ? OperatorLabels.PrincipalAdministration.SubmittedValueInvalid
                    : error.ErrorMessage
            }))
            .ToArray();
        ModelState.Clear();
        foreach (var error in errors)
        {
            ModelState.AddModelError(error.Name, error.Message);
        }
    }

    private async Task<bool> LoadPrincipalAsync(ActionActor actor, CancellationToken cancellationToken, bool readCredential = true)
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
        if (readCredential) Credential = await getCredential.ExecuteAsync(actor, Principal.Id, cancellationToken);
        return true;
    }

    private void InitializePrincipalSettings()
    {
        if (Principal is null) return;
        PrincipalCode = Principal.Code;
        // The mode the save compares with: the current Principal's, the one without a successor.
        PrincipalInspectionMode = Contact?.PrincipalInspectionMode ?? Principal.InspectionMode;
        PrincipalExpectedVersion = Principal.Version;
        ReplacementExpectedVersion = Principal.Version;
        DefaultFee = Principal.DefaultFee;
        LocationIsImageBasedAssessment = Principal.DefaultInspectionAddress is null;
        LocationLabel = Principal.DefaultInspectionLocationLabel;
        LocationAddress = Principal.DefaultInspectionAddress;
        LocationPostcode = Principal.DefaultInspectionPostcode;
        CredentialVersion = Credential?.Version ?? 0;
        ReportSendingRules = Principal.ReportSending;
        SalvageRows = (Principal.SalvageMatrix?.Bands ?? [])
            .Select(band => new SalvageMatrixEntry(
                band.Category,
                band.From.ToString("0.00", CultureInfo.InvariantCulture),
                band.To.ToString("0.00", CultureInfo.InvariantCulture),
                band.Percentage.ToString("0.00", CultureInfo.InvariantCulture)))
            .ToArray();
    }

    // The rows post as four repeated fields read in order, so a blank cell
    // keeps its row's place.
    private SalvageMatrixEntry[] PostedSalvageRows()
    {
        var form = Request.Form;
        var categories = form["SalvageBandCategory"];
        var from = form["SalvageBandFrom"];
        var to = form["SalvageBandTo"];
        var percentage = form["SalvageBandPercentage"];
        return Enumerable.Range(0, categories.Count)
            .Select(index => new SalvageMatrixEntry(
                categories[index] ?? string.Empty,
                index < from.Count ? from[index] : null,
                index < to.Count ? to[index] : null,
                index < percentage.Count ? percentage[index] : null))
            .ToArray();
    }

    // The panel posts plain fields: repeated inputs for the lists, and one
    // numbered group of fields per rule, read in the order the RuleIndex
    // fields list them. Nothing is checked here; Normalize refuses what is wrong.
    private PrincipalReportSendingRules PostedReportSending()
    {
        var form = Request.Form;
        string? Text(string name) => form[name].ToString() is { Length: > 0 } value && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
        string[] Many(string name) => [.. form[name].Select(value => value ?? string.Empty)];
        bool Flag(string name) => form.ContainsKey(name);
        bool? Choice(string name) => form[name].ToString() switch { "true" => true, "false" => false, _ => null };
        static string[] Split(string? value, params char[] separators) =>
            (value ?? string.Empty).Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        char[] addressSeparators = [',', ';', ' ', '\r', '\n'];
        char[] textSeparators = [',', ';', '\r', '\n'];

        var rules = new List<ReportSendingRule>();
        foreach (var rawIndex in form["RuleIndex"])
        {
            if (!int.TryParse(rawIndex, NumberStyles.None, CultureInfo.InvariantCulture, out var index)) continue;
            var prefix = $"Rule{index}";
            var conditions = new List<ReportSendingCondition>();
            for (var slot = 0; form.ContainsKey($"{prefix}Cond{slot}Kind"); slot++)
            {
                var field = $"{prefix}Cond{slot}";
                if (!Enum.TryParse<ReportSendingConditionKind>(form[$"{field}Kind"].ToString(), out var kind)
                    || !Enum.IsDefined(kind)) continue;
                IReadOnlyList<string> values = kind switch
                {
                    ReportSendingConditionKind.ClaimSource => Many($"{field}ClaimSource"),
                    ReportSendingConditionKind.Repairer => Many($"{field}Repairer"),
                    ReportSendingConditionKind.Outcome => Many($"{field}Outcome"),
                    ReportSendingConditionKind.CaseType => Many($"{field}CaseType"),
                    ReportSendingConditionKind.SenderNot => Split(Text($"{field}Text"), addressSeparators),
                    ReportSendingConditionKind.PreviousJob => [],
                    _ => Split(Text($"{field}Text"), textSeparators)
                };
                conditions.Add(new(kind, values));
            }
            var actions = new ReportSendingActions(
                Split(Text($"{prefix}CcAdd"), addressSeparators),
                Split(Text($"{prefix}CcRemove"), addressSeparators),
                Text($"{prefix}Remind"),
                Text($"{prefix}Hold"),
                Text($"{prefix}Stop"),
                Flag($"{prefix}RateCard"),
                new ReportSendingAttachOverride(
                    Choice($"{prefix}AttachFeeNoteSeparate"),
                    Choice($"{prefix}AttachEstimate"),
                    Choice($"{prefix}AttachAudatex"),
                    Choice($"{prefix}AttachVehicleImagesDocument"),
                    Choice($"{prefix}AttachFigureBreakdown")));
            // A row left entirely blank is the spare row; it is not a rule.
            if (conditions.Count == 0 && actions.IsEmpty) continue;
            var match = Enum.TryParse<ReportSendingRuleMatch>(form[$"{prefix}Match"].ToString(), out var parsed)
                && Enum.IsDefined(parsed) ? parsed : ReportSendingRuleMatch.All;
            rules.Add(new(match, conditions, actions));
        }

        var first = Text("RsNameFirst");
        var resend = Text("RsNameResend");
        return new(
            Text("RsSendFrom"),
            Many("RsSendTo"),
            Flag("RsSendToOnly"),
            Flag("RsReplyAll"),
            Many("RsCc"),
            Many("RsNeverCc"),
            new(
                Flag("RsFeeNoteSeparate"),
                Flag("RsEstimate"),
                Flag("RsAudatex"),
                Flag("RsReportImages"),
                Flag("RsVehicleImagesDocument"),
                Flag("RsFigureBreakdown")),
            Flag("RsGarageFigures"),
            Text("RsHold"),
            Many("RsReminders"),
            rules,
            first is null && resend is null ? null : new ReportAttachmentNamePattern(first ?? string.Empty, resend ?? string.Empty));
    }

    private void CopyFrom(ContactDirectoryRecord contact)
    {
        Name = contact.Name; ContactPerson = contact.ContactPerson; Email = contact.Email; Telephone = contact.Telephone; Address = contact.Address; Postcode = contact.Postcode; NotesOnEveryCase = contact.NotesOnEveryCase; Active = contact.Active; LinkedPrincipalIds = (contact.LinkedPrincipalIds ?? []).ToArray();
    }

    private bool TargetsContact(Guid id) => id != Guid.Empty && ContactId == id;

    private static string SalvageMatrixErrorMessage(SalvageMatrixException exception)
    {
        var category = OperatorLabels.PrincipalAdministration.SalvageCategory(exception.Category);
        return exception.Rule switch
        {
            SalvageMatrixRule.Incomplete => $"{category}: each band needs From, To and Percentage paid.",
            SalvageMatrixRule.FromAfterTo => $"{category}: From must not be more than To.",
            SalvageMatrixRule.InvalidAmount => $"{category}: amounts must be £0.00 or more.",
            SalvageMatrixRule.InvalidPercentage => $"{category}: Percentage paid must be 0 to 100.",
            SalvageMatrixRule.Overlap => $"{category}: bands overlap ({BandRange(exception.Band!)} and {BandRange(exception.OtherBand!)}).",
            _ => OperatorLabels.PrincipalAdministration.SettingsNotAccepted
        };
    }

    private static string ReportSendingRulesErrorMessage(ReportSendingRulesException exception)
    {
        var where = exception.RuleIndex is { } index
            ? $"Rule {index + 1}"
            : exception.Field switch
            {
                nameof(PrincipalReportSendingRules.SendFromMailbox) => OperatorLabels.PrincipalAdministration.SendFrom,
                nameof(PrincipalReportSendingRules.SendTo) => OperatorLabels.PrincipalAdministration.SendTo,
                nameof(PrincipalReportSendingRules.Cc) => OperatorLabels.PrincipalAdministration.Cc,
                nameof(PrincipalReportSendingRules.NeverCc) => OperatorLabels.PrincipalAdministration.NeverCc,
                nameof(PrincipalReportSendingRules.Hold) => OperatorLabels.PrincipalAdministration.Hold,
                nameof(PrincipalReportSendingRules.Reminders) => OperatorLabels.PrincipalAdministration.Reminders,
                _ => OperatorLabels.PrincipalAdministration.AttachmentName
            };
        return exception.Rule switch
        {
            ReportSendingRulesRule.InvalidAddress => $"{where}: enter valid e-mail addresses.",
            ReportSendingRulesRule.AddressInCcAndNeverCc => "An address cannot be in both Cc and Never cc.",
            ReportSendingRulesRule.SendToOnlyNeedsAddresses => "Send to only needs at least one Send to address.",
            ReportSendingRulesRule.TextRequired => $"{where}: the text is required.",
            ReportSendingRulesRule.UnknownContact => $"{where}: choose a contact from the list.",
            ReportSendingRulesRule.UnknownOutcome => $"{where}: choose an outcome from the list.",
            ReportSendingRulesRule.UnknownCaseType => $"{where}: choose a case type from the list.",
            ReportSendingRulesRule.InvalidRefPrefix => $"{where}: {OperatorLabels.PrincipalAdministration.RefPrefixLettersOnly}",
            ReportSendingRulesRule.EmptyRule => $"{where}: add at least one condition.",
            ReportSendingRulesRule.EmptyCondition => $"{where}: a condition needs a value.",
            ReportSendingRulesRule.EmptyActions => $"{where}: add at least one action.",
            ReportSendingRulesRule.UnknownNameToken => "Attachment name: use only {ref}, {reg} and {outcome}.",
            _ => OperatorLabels.PrincipalAdministration.SettingsNotAccepted
        };
    }

    private static string BandRange(SalvageMatrixBand band) =>
        $"{band.From.ToString("#,##0.00", CultureInfo.InvariantCulture)} to {band.To.ToString("#,##0.00", CultureInfo.InvariantCulture)}";

    private string PrincipalAdministrationErrorMessage(OrganizationAdministrationException exception) =>
        exception.Error == OrganizationAdministrationError.StaleVersion
            && Contact is { } contact
            && ExpectedVersion != contact.Version
                ? OperatorLabels.PrincipalAdministration.ContactChanged
                : OperatorLabels.PrincipalAdministration.ErrorMessage(exception.Error);
}
