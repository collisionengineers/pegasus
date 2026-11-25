using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Microsoft.Extensions.Options;
using Pegasus.Core.Intake;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Reports;
using Pegasus.Web.PhotoCapture;
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
    IUpdatePrincipalReportSending updatePrincipalReportSending,
    IApprovedMailboxStore approvedMailboxes,
    IReplacePrincipal replacePrincipal,
    IStandingPhotoLinkStore? standingLinks = null,
    StandingPhotoLinkAdministration? standingLinkAdministration = null,
    IOptions<PhotoCaptureOptions>? photoCaptureOptions = null,
    IConfiguration? configuration = null) : AdministrationPageModel
{
    public ContactDirectoryRecord? Contact { get; private set; }

    // An active Principal's page is tabs (operator, 13 October 2026): a ?tab= query
    // picks the one panel the server renders, and each panel's save comes back to it.
    public const string ContactTab = "contact";
    public const string ReportGenerationTab = "report-generation";
    // The Sending rules tab keeps the key of the Report sending tab it
    // replaced, so its address does not move (operator, 25 November 2026).
    public const string ReportSendingTab = "report-sending";
    public const string ApiTab = "principal-api";
    public static readonly IReadOnlyList<(string Key, string Label)> PrincipalTabs =
    [
        (ContactTab, OperatorLabels.PrincipalAdministration.ContactTab),
        (ReportGenerationTab, OperatorLabels.PrincipalAdministration.ReportGeneration),
        (ReportSendingTab, OperatorLabels.PrincipalAdministration.SendingRules),
        (ApiTab, OperatorLabels.PrincipalAdministration.PrincipalApi)
    ];
    [BindProperty(SupportsGet = true)] public string? Tab { get; set; }
    public string CurrentTab => Tab == PhotoLinksTab ? PhotoLinksTab : PrincipalTabs.Any(tab => tab.Key == Tab) ? Tab! : ContactTab;

    // A Principal, Claim Source or Repairer contact's standing photo link and
    // its defaults are its Photo links tab (FRD-28; R774), present only while
    // photo capture is composed.
    public const string PhotoLinksTab = "photo-links";
    public bool OffersPhotoLinks => standingLinks is not null
        && standingLinkAdministration is not null
        && photoCaptureOptions is not null
        && configuration is not null
        && ShellRail.PhotoCaptureEnabled(configuration)
        && Contact is { Role: ContactRole.Principal or ContactRole.ClaimSource or ContactRole.Repairer };

    /// <summary>The page's tabs: an active Principal's panels, a contact with a Photo links tab beside its Contact tab, else none.</summary>
    public IReadOnlyList<(string Key, string Label)> Tabs
    {
        get
        {
            var photoLinks = (PhotoLinksTab, OperatorLabels.PhotoCapture.PhotoLinksTab);
            if (Principal is { IsActive: true })
            {
                return OffersPhotoLinks ? [.. PrincipalTabs, photoLinks] : PrincipalTabs;
            }

            return OffersPhotoLinks ? [PrincipalTabs[0], photoLinks] : [];
        }
    }

    public StandingPhotoLink? StandingLink { get; private set; }
    /// <summary>The link's saved addresses: those taken from the contact, read-only, then the tab's own (R806, R815).</summary>
    public StandingPhotoLinkAddressBook AddressBook { get; private set; } = new([], []);

    /// <summary>Which saved address the tab draws as a form: <see cref="NewSavedAddress"/> for Add address, or a saved one's id.</summary>
    [BindProperty(SupportsGet = true, Name = "savedAddress")] public string? SavedAddressForm { get; set; }
    public const string NewSavedAddress = "new";
    public bool AddingSavedAddress => SavedAddressForm == NewSavedAddress;
    public Guid? EditingSavedAddressId => Guid.TryParse(SavedAddressForm, out var savedId) ? savedId : null;
    [BindProperty] public Guid SavedAddressId { get; set; }
    [BindProperty] public string? SavedAddressName { get; set; }
    [BindProperty] public string? SavedAddressText { get; set; }
    public string? StandingLinkUrl => StandingLink is { } link && photoCaptureOptions is not null
        ? Pegasus.Web.Pages.PhotoCapture.PhotoLinkMail.StandingUrl(photoCaptureOptions.Value, link.Slug)
        : null;
    public string? StandingLinkQrSvg => StandingLinkUrl is { } url ? PhotoLinkQr.Svg(url) : null;
    [BindProperty] public long StandingLinkVersion { get; set; }
    // Each standing link form's operation key: its history event's
    // correlation, so a form sent twice acts once (R846).
    [BindProperty] public string? PhotoLinkOperationKey { get; set; } = NewOperationKey();
    // A saved address with no name or no address shows its sentence beside its form, not in the panel (R868).
    public bool SavedAddressRefused { get; private set; }
    [BindProperty] public PhotoCaptureMode LinkForwardedMode { get; set; } = PhotoCaptureMode.Guided;
    [BindProperty] public bool LinkLiveCameraOnly { get; set; } = true;
    [BindProperty] public int LinkLifetimeDays { get; set; } = CaptureLimits.DefaultLifetimeDays;
    [BindProperty] public bool LinkOriginalsInBackground { get; set; } = true;
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

    // A Principal's Case defaults, edited in Cases and E-mail on the Contact
    // tab and saved by Save contact (operator, 25 November 2026; R1266, R1267).
    [BindProperty] public decimal? DefaultFee { get; set; }
    [BindProperty, Display(Name = OperatorLabels.PrincipalAdministration.LocationLabelField), StringLength(ContactDirectoryPolicy.MaximumDefaultInspectionLocationLabelLength, ErrorMessage = OperatorLabels.TooLongFormat)] public string? LocationLabel { get; set; }
    [BindProperty, Display(Name = OperatorLabels.Contacts.AddressField), StringLength(ContactDirectoryPolicy.MaximumDefaultInspectionAddressLength, ErrorMessage = OperatorLabels.TooLongFormat)] public string? LocationAddress { get; set; }
    [BindProperty, Display(Name = OperatorLabels.Contacts.PostcodeField), StringLength(ContactDirectoryPolicy.MaximumPostcodeLength, ErrorMessage = OperatorLabels.TooLongFormat)] public string? LocationPostcode { get; set; }
    [BindProperty] public string? SendFromMailbox { get; set; }
    // The Outgoing mailboxes that can send, as the report send form offers
    // them, read only for a Principal's Contact tab, the one tab that draws them.
    public IReadOnlyList<string> SendFromChoices { get; private set; } = [];

    [BindProperty] public long PrincipalExpectedVersion { get; set; }
    // Whether the Principal's reports carry vehicle images (Report generation).
    [BindProperty] public bool ReportImages { get; set; }
    // The VAT number the Principal's fee notes print; empty is the standard number.
    [BindProperty] public string? FeeNoteVatNumber { get; set; }
    // What the Principal's standalone Audits work from (R1138), the invoice
    // Generate report also makes in its own name (R1147), and the VAT number
    // and payment terms that invoice prints (R1148); empty leaves the row out.
    [BindProperty] public AuditWorkflowKind AuditWorkflow { get; set; }
    [BindProperty] public PrincipalInvoiceKind PrincipalInvoice { get; set; }
    [BindProperty] public string? InvoiceVatNumber { get; set; }
    [BindProperty] public string? InvoicePaymentTerms { get; set; }
    public static IReadOnlyList<AuditWorkflowKind> AuditWorkflowChoices { get; } = Enum.GetValues<AuditWorkflowKind>();
    public static IReadOnlyList<PrincipalInvoiceKind> PrincipalInvoiceChoices { get; } = Enum.GetValues<PrincipalInvoiceKind>();
    // What the Principal's reports are called (R740); empty is the standard name.
    [BindProperty] public string? InspectionReportNameFirst { get; set; }
    [BindProperty] public string? InspectionReportNameResend { get; set; }
    [BindProperty] public string? AuditReportNameFirst { get; set; }
    [BindProperty] public string? AuditReportNameResend { get; set; }
    [BindProperty] public string? ReportSettingsOperationKey { get; set; } = NewOperationKey();
    [BindProperty] public string? ReportSendingOperationKey { get; set; } = NewOperationKey();
    // The Principal's stored sending rules, as the Sending rules tab draws them.
    public PrincipalReportSendingRules ReportSendingRules { get; private set; } = PrincipalReportSendingRules.Default;
    public IReadOnlyList<ContactChoice> ClaimSourceChoices { get; private set; } = [];
    public IReadOnlyList<ContactChoice> RepairerChoices { get; private set; } = [];

    // The Sending rules tab's dialogs open by address on the tab, so each
    // works without script (as the Contacts list's New dialogs do):
    // ?rule=new, ?rule={number from 1}, ?every=1 and ?remove={number}.
    public const string NewRuleKey = "new";
    [BindProperty(SupportsGet = true, Name = "rule")] public string? RuleKey { get; set; }
    [BindProperty(SupportsGet = true, Name = "every")] public string? EveryKey { get; set; }
    [BindProperty(SupportsGet = true, Name = "remove")] public string? RemoveKey { get; set; }
    /// <summary>The Sending rules dialog the address opens, if any.</summary>
    public SendingDialog Dialog { get; private set; }
    /// <summary>The rule the open dialog edits or removes, from 0; null for a new rule and the every-Case list.</summary>
    public int? DialogRuleIndex { get; private set; }
    /// <summary>What the rule or On every Case dialog draws: the stored rule, or the draft as posted.</summary>
    public SendingRuleDraft? Draft { get; private set; }
    // A row the dialog adds or removes without script (SendingRuleDraft.Change).
    [BindProperty] public string? DraftChange { get; set; }
    // Move up and Move down: the rule's number from 1, and the step.
    [BindProperty] public int RuleNumber { get; set; }
    [BindProperty] public int MoveBy { get; set; }
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
        await LoadTabChoicesAsync(actor, cancellationToken);
        await LoadPrincipalChoicesAsync(actor, cancellationToken);
        await LoadPhotoLinkAsync(cancellationToken);
        return Page();
    }

    public Task<IActionResult> OnPostCreatePhotoLinkAsync(Guid id, CancellationToken cancellationToken) => ChangePhotoLinkAsync(id, "create", cancellationToken);
    public Task<IActionResult> OnPostPausePhotoLinkAsync(Guid id, CancellationToken cancellationToken) => ChangePhotoLinkAsync(id, "pause", cancellationToken);
    public Task<IActionResult> OnPostResumePhotoLinkAsync(Guid id, CancellationToken cancellationToken) => ChangePhotoLinkAsync(id, "resume", cancellationToken);
    public Task<IActionResult> OnPostReplacePhotoLinkAsync(Guid id, CancellationToken cancellationToken) => ChangePhotoLinkAsync(id, "replace", cancellationToken);
    public Task<IActionResult> OnPostRetirePhotoLinkAsync(Guid id, CancellationToken cancellationToken) => ChangePhotoLinkAsync(id, "retire", cancellationToken);
    public Task<IActionResult> OnPostUpdatePhotoLinkDefaultsAsync(Guid id, CancellationToken cancellationToken) => ChangePhotoLinkAsync(id, "defaults", cancellationToken);
    public Task<IActionResult> OnPostAddPhotoLinkAddressAsync(Guid id, CancellationToken cancellationToken) => ChangePhotoLinkAsync(id, "add-address", cancellationToken);
    public Task<IActionResult> OnPostChangePhotoLinkAddressAsync(Guid id, CancellationToken cancellationToken) => ChangePhotoLinkAsync(id, "change-address", cancellationToken);
    public Task<IActionResult> OnPostRemovePhotoLinkAddressAsync(Guid id, CancellationToken cancellationToken) => ChangePhotoLinkAsync(id, "remove-address", cancellationToken);

    /// <summary>The standing link's QR code as a file to print (Download QR).</summary>
    public async Task<IActionResult> OnGetPhotoLinkQrAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        ContactId = id;
        Contact = await contacts.GetAsync(actor, id, cancellationToken);
        if (Contact is null || !OffersPhotoLinks) return NotFound();
        if (!StaffAuthorization.IsAuthorized(actor, StaffAccessRight.ManageOrganizationsAndPrincipals)) return Forbid();
        var link = await standingLinks!.FindForOrganizationAsync(id, cancellationToken);
        if (link is null) return NotFound();
        var url = Pegasus.Web.Pages.PhotoCapture.PhotoLinkMail.StandingUrl(photoCaptureOptions!.Value, link.Slug);
        return File(System.Text.Encoding.UTF8.GetBytes(PhotoLinkQr.Svg(url)), "image/svg+xml", PhotoLinkQr.FileName(link.Slug));
    }

    // One handler per tab, as the other tabs: each change to the contact's
    // standing link is Core's (StandingPhotoLinkAdministration), writes its
    // history event under the form's operation key (R846) and comes back to
    // the Photo links tab. A change made over a stale read is shown as the
    // tab's refusal, as the other tabs show theirs; a saved address with no
    // name or no address, beside its form (R868).
    private async Task<IActionResult> ChangePhotoLinkAsync(Guid id, string action, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (id == Guid.Empty) return BadRequest();
        ContactId = id;
        Tab = PhotoLinksTab;
        // The tab's forms post none of the contact's own fields, so only its own refusals are shown.
        ClearModelStatePreservingErrors();
        Contact = await contacts.GetAsync(actor, id, cancellationToken);
        if (Contact is null || !OffersPhotoLinks) return NotFound();
        var administration = standingLinkAdministration!;
        var link = await standingLinks!.FindForOrganizationAsync(id, cancellationToken);
        if (action != "create" && link is null) return BadRequest();
        if (!IsOperationKeyValid(PhotoLinkOperationKey))
        {
            ModelState.AddModelError(string.Empty, OperatorLabels.Contacts.EditFormExpired);
            return await PhotoLinksPageAsync(actor, cancellationToken);
        }

        var operationKey = PhotoLinkOperationKey!;
        try
        {
            switch (action)
            {
                case "create":
                    await administration.CreateForContactAsync(actor, operationKey, id, cancellationToken);
                    break;
                case "pause":
                    await administration.PauseAsync(actor, operationKey, link!.Id, StandingLinkVersion, cancellationToken);
                    break;
                case "resume":
                    await administration.ResumeAsync(actor, operationKey, link!.Id, StandingLinkVersion, cancellationToken);
                    break;
                case "replace":
                    await administration.ReplaceAsync(actor, operationKey, link!.Id, StandingLinkVersion, typedSlug: null, cancellationToken);
                    break;
                case "retire":
                    await administration.RetireAsync(actor, operationKey, link!.Id, StandingLinkVersion, cancellationToken);
                    break;
                case "defaults":
                    await administration.UpdateDefaultsAsync(
                        actor,
                        operationKey,
                        link!.Id,
                        StandingLinkVersion,
                        LinkForwardedMode,
                        LinkLiveCameraOnly,
                        LinkLifetimeDays,
                        LinkOriginalsInBackground,
                        cancellationToken);
                    TempData["AdministrationStatus"] = OperatorLabels.PhotoCapture.Saved;
                    break;
                // The tab's own saved addresses (R806); those taken from the
                // contact are not the tab's to change (R815).
                case "add-address":
                    await administration.AddAddressAsync(
                        actor, operationKey, link!.Id, StandingLinkVersion, SavedAddressName, SavedAddressText, cancellationToken);
                    break;
                case "change-address":
                    await administration.ChangeAddressAsync(
                        actor, operationKey, link!.Id, StandingLinkVersion, SavedAddressId, SavedAddressName, SavedAddressText, cancellationToken);
                    break;
                case "remove-address":
                    await administration.RemoveAddressAsync(actor, operationKey, link!.Id, StandingLinkVersion, SavedAddressId, cancellationToken);
                    break;
                default:
                    return BadRequest();
            }

            return RedirectToPage(new { id = ContactId, tab = Tab });
        }
        catch (StaffAuthorizationException) { return Forbid(); }
        catch (StandingPhotoLinkException exception) when (exception.Error == StandingPhotoLinkError.AlreadyExists)
        {
            // The link is there, whoever made it: the tab shows it.
            return RedirectToPage(new { id = ContactId, tab = Tab });
        }
        // A saved address someone else removed meanwhile is a stale read, not a missing page.
        catch (StandingPhotoLinkException exception) when (exception.Error == StandingPhotoLinkError.NotFound
            && !action.EndsWith("-address", StringComparison.Ordinal)) { return NotFound(); }
        catch (StandingPhotoLinkException exception) when (exception.Error == StandingPhotoLinkError.InvalidDefaults) { return BadRequest(); }
        catch (StandingPhotoLinkException exception) when (exception.Error == StandingPhotoLinkError.InvalidAddress)
        {
            SavedAddressRefused = true;
            ModelState.AddModelError(nameof(SavedAddressName), OperatorLabels.PhotoCapture.SavedAddressNeedsNameAndAddress);
        }
        catch (StandingPhotoLinkException) { ModelState.AddModelError(string.Empty, OperatorLabels.ActionNotApplied); }
        return await PhotoLinksPageAsync(actor, cancellationToken);
    }

    // The Photo links tab drawn again with its refusal.
    private async Task<IActionResult> PhotoLinksPageAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        await PopulateAsync(actor, cancellationToken);
        await LoadPhotoLinkAsync(cancellationToken);
        return Page();
    }

    // The standing link, its saved addresses and its defaults, read only for
    // the Photo links tab, the one tab that draws them.
    private async Task LoadPhotoLinkAsync(CancellationToken cancellationToken)
    {
        if (CurrentTab != PhotoLinksTab || !OffersPhotoLinks) return;
        StandingLink = await standingLinks!.FindForOrganizationAsync(ContactId, cancellationToken);
        if (StandingLink is not { } link) return;
        AddressBook = await standingLinks.GetAddressBookAsync(link.Id, cancellationToken);
        StandingLinkVersion = link.Version;
        LinkForwardedMode = link.ForwardedMode;
        LinkLiveCameraOnly = link.LiveCameraOnly;
        LinkLifetimeDays = link.LinkLifetimeDays;
        LinkOriginalsInBackground = link.OriginalsInBackground;
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
                // A field the form did not carry keeps the record's value; the
                // Principal's Case defaults belong to a Principal only.
                var mode = Posted(nameof(PrincipalInspectionMode)) ? PrincipalInspectionMode : Contact.PrincipalInspectionMode ?? PrincipalInspectionMode;
                await administration.SaveAsync(new(actor, ContactId, ExpectedVersion, Name, Email, Telephone, Address, Postcode, Active, Contact.Role, isPrincipal ? PrincipalCode : null, mode, isPrincipal ? [] : LinkedPrincipalIds, OperationKey,
                    Posted(nameof(NotesOnEveryCase)) ? NotesOnEveryCase : Contact.NotesOnEveryCase,
                    isPrincipal ? Posted(nameof(DefaultFee)) ? DefaultFee : Contact.DefaultFee : null,
                    isPrincipal ? Posted(nameof(LocationLabel)) ? LocationLabel : Contact.DefaultInspectionLocationLabel : null,
                    isPrincipal ? Posted(nameof(LocationAddress)) ? LocationAddress : Contact.DefaultInspectionAddress : null,
                    isPrincipal ? Posted(nameof(LocationPostcode)) ? LocationPostcode : Contact.DefaultInspectionPostcode : null,
                    isPrincipal ? Posted(nameof(SendFromMailbox)) ? SendFromMailbox : Contact.SendFromMailbox : null), cancellationToken);
                TempData["AdministrationStatus"] = OperatorLabels.Contacts.Saved;
                return RedirectToPage("Index");
            }
            catch (ContactDirectoryException exception) { ModelState.AddModelError(string.Empty, OperatorLabels.Contacts.ErrorMessage(exception.Error)); }
            catch (ArgumentException exception)
            {
                // A named field error is keyed to its own control; every other
                // refusal keeps the general notice.
                var (field, message) = exception.ParamName switch
                {
                    nameof(Telephone) => (nameof(Telephone), OperatorLabels.Contacts.PhoneMustBeDigits),
                    nameof(SaveContactRequest.DefaultFee) => (nameof(DefaultFee), OperatorLabels.PrincipalAdministration.SubmittedValueInvalid),
                    nameof(SaveContactRequest.DefaultInspectionAddress) => (nameof(LocationAddress), OperatorLabels.PrincipalAdministration.AddressRequired),
                    nameof(SaveContactRequest.SendFromMailbox) => (nameof(SendFromMailbox), OperatorLabels.PrincipalAdministration.SubmittedValueInvalid),
                    nameof(SaveContactRequest.PrincipalInspectionMode) => (nameof(PrincipalInspectionMode), OperatorLabels.Contacts.SelectInspectionMode),
                    _ => (string.Empty, OperatorLabels.Contacts.DetailsNotAccepted)
                };
                ModelState.AddModelError(field, message);
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
            nameof(ReportImages),
            nameof(FeeNoteVatNumber),
            nameof(AuditWorkflow),
            nameof(PrincipalInvoice),
            nameof(InvoiceVatNumber),
            nameof(InvoicePaymentTerms),
            nameof(InspectionReportNameFirst),
            nameof(InspectionReportNameResend),
            nameof(AuditReportNameFirst),
            nameof(AuditReportNameResend),
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
                    ReportImages,
                    ExpectedVersion,
                    new PrincipalReportNames(
                        InspectionReportNameFirst,
                        InspectionReportNameResend,
                        AuditReportNameFirst,
                        AuditReportNameResend),
                    FeeNoteVatNumber,
                    AuditWorkflow,
                    PrincipalInvoice,
                    InvoiceVatNumber,
                    InvoicePaymentTerms), cancellationToken);
                TempData["AdministrationStatus"] = OperatorLabels.PrincipalAdministration.ReportSettingsUpdated;
                return RedirectToPage(new { id = ContactId, tab = Tab });
            }
            catch (OrganizationAdministrationException exception) { ModelState.AddModelError(string.Empty, PrincipalAdministrationErrorMessage(exception)); }
            catch (ReportNamesException exception) { ModelState.AddModelError(string.Empty, OperatorLabels.PrincipalAdministration.ReportNameRefused(exception.AuditReport, exception.Rule)); }
            catch (ArgumentException) { ModelState.AddModelError(string.Empty, OperatorLabels.PrincipalAdministration.SettingsNotAccepted); }
            catch (StaffAuthorizationException) { return Forbid(); }
        }
        var typed = (ReportImages, FeeNoteVatNumber, InspectionReportNameFirst, InspectionReportNameResend, AuditReportNameFirst, AuditReportNameResend);
        var typedInvoice = (AuditWorkflow, PrincipalInvoice, InvoiceVatNumber, InvoicePaymentTerms);
        await PopulateAsync(actor, cancellationToken);
        // What was typed stays.
        (ReportImages, FeeNoteVatNumber, InspectionReportNameFirst, InspectionReportNameResend, AuditReportNameFirst, AuditReportNameResend) = typed;
        (AuditWorkflow, PrincipalInvoice, InvoiceVatNumber, InvoicePaymentTerms) = typedInvoice;
        return Page();
    }

    // The Sending rules tab (operator, 25 November 2026; R1266 to R1272):
    // each dialog and each Move saves the Principal's whole rules value, its
    // own change made to the rules as stored; Normalize is the one check.
    public Task<IActionResult> OnPostSaveRuleAsync(Guid id, CancellationToken cancellationToken) => SaveSendingAsync(id, SendingSave.Rule, cancellationToken);
    public Task<IActionResult> OnPostSaveEveryCaseAsync(Guid id, CancellationToken cancellationToken) => SaveSendingAsync(id, SendingSave.EveryCase, cancellationToken);
    public Task<IActionResult> OnPostRemoveRuleAsync(Guid id, CancellationToken cancellationToken) => SaveSendingAsync(id, SendingSave.Remove, cancellationToken);
    public Task<IActionResult> OnPostMoveRuleAsync(Guid id, CancellationToken cancellationToken) => SaveSendingAsync(id, SendingSave.Move, cancellationToken);

    private enum SendingSave { Rule, EveryCase, Remove, Move }

    private async Task<IActionResult> SaveSendingAsync(Guid id, SendingSave save, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (id == Guid.Empty) return BadRequest();
        ContactId = id;
        Tab = ReportSendingTab;
        ClearModelStatePreservingErrors(
            nameof(PrincipalExpectedVersion),
            nameof(ExpectedVersion),
            nameof(ReportSendingOperationKey),
            nameof(RuleNumber),
            nameof(MoveBy));
        var expectedVersion = PrincipalExpectedVersion;
        var draft = save is SendingSave.Rule or SendingSave.EveryCase ? SendingRuleDraft.Read(Request.Form) : null;
        if (!await LoadPrincipalAsync(actor, cancellationToken, readCredential: false)) return NotFound();
        if (!IsOperationKeyValid(ReportSendingOperationKey)) ModelState.AddModelError(string.Empty, OperatorLabels.PrincipalAdministration.FormExpired);
        if (ModelState.IsValid)
        {
            var current = Principal!.ReportSending;
            var rules = save switch
            {
                SendingSave.Rule when RuleKey == NewRuleKey => new PrincipalReportSendingRules(current.EveryCase, [.. current.Rules, draft!.ToRule()]),
                SendingSave.Rule => RuleIndex(RuleKey, current) is { } index
                    ? new PrincipalReportSendingRules(current.EveryCase, [.. current.Rules.Select((rule, at) => at == index ? draft!.ToRule() : rule)])
                    : null,
                SendingSave.EveryCase => new PrincipalReportSendingRules(draft!.ToActions(), current.Rules),
                SendingSave.Remove => RuleIndex(RemoveKey, current) is { } index
                    ? new PrincipalReportSendingRules(current.EveryCase, [.. current.Rules.Where((_, at) => at != index)])
                    : null,
                _ => MoveBy is -1 or 1
                    && RuleIndex(RuleNumber.ToString(CultureInfo.InvariantCulture), current) is { } moving
                    && moving + MoveBy >= 0
                    && moving + MoveBy < current.Rules.Count
                        ? new PrincipalReportSendingRules(current.EveryCase, Moved(current.Rules, moving, moving + MoveBy))
                        : null
            };
            // The rule the form named is gone: the rules changed after the page was drawn.
            if (rules is null) ModelState.AddModelError(string.Empty, OperatorLabels.PrincipalAdministration.ErrorMessage(OrganizationAdministrationError.StaleVersion));
            else
            {
                try
                {
                    await updatePrincipalReportSending.ExecuteAsync(new(
                        Principal!.Id, expectedVersion, actor, ReportSendingOperationKey!,
                        rules, ExpectedVersion), cancellationToken);
                    TempData["AdministrationStatus"] = OperatorLabels.PrincipalAdministration.ReportSendingUpdated;
                    return RedirectToPage(new { id = ContactId, tab = Tab });
                }
                catch (ReportSendingRulesException exception) { ModelState.AddModelError(string.Empty, ReportSendingRulesErrorMessage(exception)); }
                catch (OrganizationAdministrationException exception) { ModelState.AddModelError(string.Empty, PrincipalAdministrationErrorMessage(exception)); }
                catch (ArgumentException) { ModelState.AddModelError(string.Empty, OperatorLabels.PrincipalAdministration.SettingsNotAccepted); }
                catch (StaffAuthorizationException) { return Forbid(); }
            }
        }
        // The dialog is drawn again with what was typed and its refusal; a
        // Move's refusal shows at the top of the panel.
        Draft = draft;
        await PopulateAsync(actor, cancellationToken);
        return Page();
    }

    /// <summary>
    /// The rule dialog with one row added or removed, as its And, Or, Add
    /// action and remove buttons post it without script: nothing is saved,
    /// and the dialog is drawn again from the posted draft.
    /// </summary>
    public async Task<IActionResult> OnPostRuleDraftAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (id == Guid.Empty) return BadRequest();
        ContactId = id;
        Tab = ReportSendingTab;
        ClearModelStatePreservingErrors();
        var posted = (PrincipalExpectedVersion, ReportSendingOperationKey);
        Draft = SendingRuleDraft.Read(Request.Form).Change(DraftChange);
        await PopulateAsync(actor, cancellationToken);
        if (Contact is null || Principal is null) return NotFound();
        // The draft still answers to the version and form the dialog was opened with.
        (PrincipalExpectedVersion, ReportSendingOperationKey) = posted;
        return Page();
    }

    private static IReadOnlyList<ReportSendingRule> Moved(IReadOnlyList<ReportSendingRule> rules, int from, int to)
    {
        var moved = rules.ToList();
        (moved[from], moved[to]) = (moved[to], moved[from]);
        return moved;
    }

    // A rule's number from 1, as an address or a form names it, as its index; null when there is no such rule.
    private static int? RuleIndex(string? number, PrincipalReportSendingRules rules) =>
        int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= 1 && parsed <= rules.Rules.Count
            ? parsed - 1
            : null;

    // The dialog the address opens, drawn only on an active Principal's Sending rules tab.
    private void ResolveSendingDialog()
    {
        Dialog = SendingDialog.None;
        DialogRuleIndex = null;
        if (Principal is not { IsActive: true } || CurrentTab != ReportSendingTab) return;
        var rules = ReportSendingRules;
        if (RuleKey == NewRuleKey)
        {
            Dialog = SendingDialog.Rule;
            Draft ??= SendingRuleDraft.NewRule;
        }
        else if (RuleIndex(RuleKey, rules) is { } rule)
        {
            Dialog = SendingDialog.Rule;
            DialogRuleIndex = rule;
            Draft ??= SendingRuleDraft.From(rules.Rules[rule]);
        }
        else if (!string.IsNullOrEmpty(EveryKey))
        {
            Dialog = SendingDialog.EveryCase;
            Draft ??= SendingRuleDraft.FromActions(rules.EveryCase);
        }
        else if (RuleIndex(RemoveKey, rules) is { } removed)
        {
            Dialog = SendingDialog.Remove;
            DialogRuleIndex = removed;
        }
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
            await LoadTabChoicesAsync(actor, cancellationToken);
            await LoadPrincipalChoicesAsync(actor, cancellationToken);
        }
    }

    // Each tab reads only the choices it draws (#1542): a Principal's Contact
    // tab the mailboxes a new message may leave from (the Outgoing ones that
    // can send, as the report send form offers them), and the Sending rules
    // tab the contacts a rule may name, then the dialog its address opens.
    private async Task LoadTabChoicesAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        if (CurrentTab == ContactTab && Contact is { Role: ContactRole.Principal })
        {
            SendFromChoices = (await approvedMailboxes.ListAsync(cancellationToken))
                .Where(mailbox => mailbox.CanSend)
                .Select(mailbox => mailbox.Address)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        if (Principal is { IsActive: true } && CurrentTab == ReportSendingTab)
        {
            ClaimSourceChoices = await contacts.ListByRoleAsync(actor, ContactRole.ClaimSource, cancellationToken);
            RepairerChoices = await contacts.ListByRoleAsync(actor, ContactRole.Repairer, cancellationToken);
        }
        ResolveSendingDialog();
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
        PrincipalExpectedVersion = Principal.Version;
        ReplacementExpectedVersion = Principal.Version;
        ReportImages = Principal.ReportImages;
        FeeNoteVatNumber = Principal.FeeNoteVatNumber;
        AuditWorkflow = Principal.AuditWorkflow;
        PrincipalInvoice = Principal.PrincipalInvoice;
        InvoiceVatNumber = Principal.InvoiceVatNumber;
        InvoicePaymentTerms = Principal.InvoicePaymentTerms;
        InspectionReportNameFirst = Principal.ReportNames.InspectionFirst;
        InspectionReportNameResend = Principal.ReportNames.InspectionResend;
        AuditReportNameFirst = Principal.ReportNames.AuditFirst;
        AuditReportNameResend = Principal.ReportNames.AuditResend;
        CredentialVersion = Credential?.Version ?? 0;
        ReportSendingRules = Principal.ReportSending;
    }

    private void CopyFrom(ContactDirectoryRecord contact)
    {
        Name = contact.Name; Email = contact.Email; Telephone = contact.Telephone; Address = contact.Address; Postcode = contact.Postcode; NotesOnEveryCase = contact.NotesOnEveryCase; Active = contact.Active; LinkedPrincipalIds = (contact.LinkedPrincipalIds ?? []).ToArray();
        // A Principal's Case defaults are its current Principal's, the one without a successor.
        if (contact.PrincipalInspectionMode is { } mode) PrincipalInspectionMode = mode;
        DefaultFee = contact.DefaultFee;
        LocationLabel = contact.DefaultInspectionLocationLabel;
        LocationAddress = contact.DefaultInspectionAddress;
        LocationPostcode = contact.DefaultInspectionPostcode;
        SendFromMailbox = contact.SendFromMailbox;
    }

    private bool TargetsContact(Guid id) => id != Guid.Empty && ContactId == id;

    private static string ReportSendingRulesErrorMessage(ReportSendingRulesException exception)
    {
        // "Rule {n}" for a rule, "On every Case" for the every-Case list.
        var where = exception.RuleIndex is { } index
            ? OperatorLabels.PrincipalAdministration.RuleNumber(index + 1)
            : OperatorLabels.PrincipalAdministration.OnEveryCase;
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
            _ => OperatorLabels.PrincipalAdministration.SettingsNotAccepted
        };
    }

    private string PrincipalAdministrationErrorMessage(OrganizationAdministrationException exception) =>
        exception.Error == OrganizationAdministrationError.StaleVersion
            && Contact is { } contact
            && ExpectedVersion != contact.Version
                ? OperatorLabels.PrincipalAdministration.ContactChanged
                : OperatorLabels.PrincipalAdministration.ErrorMessage(exception.Error);
}
