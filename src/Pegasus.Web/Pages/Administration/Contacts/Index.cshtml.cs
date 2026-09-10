using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Administration.Contacts;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel(
    IContactDirectoryQueries contacts,
    IContactDirectoryAdministration administration,
    IEditScopeLeases editScopes,
    IGetPrincipal getPrincipal) : AdministrationPageModel
{
    public IReadOnlyList<ContactDirectoryRecord> Contacts { get; private set; } = [];
    public IReadOnlyList<ContactDirectoryRecord> PossibleMatches { get; private set; } = [];
    public IReadOnlyList<PrincipalAdministrationDetails> PrincipalChoices { get; private set; } = [];
    public ContactDirectoryRecord? ExistingContact { get; private set; }

    [BindProperty(SupportsGet = true)] public ContactRole? Type { get; set; }
    [BindProperty(SupportsGet = true)] public ContactSort Sort { get; set; } = ContactSort.Name;
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public ContactRole? CreateType { get; set; }

    [BindProperty] public Guid ContactId { get; set; }
    [BindProperty] public Guid? ExistingContactId { get; set; }
    [BindProperty] public long ExpectedVersion { get; set; }
    [BindProperty] public string? LeaseToken { get; set; }
    [BindProperty] public string OperationKey { get; set; } = NewOperationKey();
    [BindProperty, Required, StringLength(ContactDirectoryPolicy.MaximumNameLength)]
    public string Name { get; set; } = string.Empty;
    [BindProperty, StringLength(ContactDirectoryPolicy.MaximumContactPersonLength)]
    public string? ContactPerson { get; set; }
    [BindProperty, StringLength(ContactDirectoryPolicy.MaximumEmailLength)]
    public string? Email { get; set; }
    [BindProperty, StringLength(ContactDirectoryPolicy.MaximumTelephoneLength)]
    public string? Telephone { get; set; }
    [BindProperty, StringLength(ContactDirectoryPolicy.MaximumAddressLength)]
    public string? Address { get; set; }
    [BindProperty, StringLength(ContactDirectoryPolicy.MaximumPostcodeLength)]
    public string? Postcode { get; set; }
    [BindProperty, StringLength(OrganizationAdministrationPolicy.MaximumPrincipalCodeLength)]
    public string? PrincipalCode { get; set; }
    [BindProperty] public CaseInspectionMode PrincipalInspectionMode { get; set; } = CaseInspectionMode.PhysicalAddress;
    [BindProperty] public Guid[] AssociatedPrincipalIds { get; set; } = [];

    /// <summary>
    /// Posted by the take-over control the page offers when this operator is
    /// already editing the chosen contact in another window.
    /// </summary>
    [BindProperty] public bool TakeOver { get; set; }

    public bool CreateDialogOpen => CreateType is not null;
    public bool EditingExisting => ExistingContactId is not null && !string.IsNullOrWhiteSpace(LeaseToken);
    public bool CanAssociateSelectedType => CreateType is { } role && role != ContactRole.Principal;

    /// <summary>
    /// Set when the chosen contact is already being edited by this operator in
    /// another window, so the page offers the take-over that ends the other
    /// window's claim instead of a choice that would be refused again.
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

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!Enum.IsDefined(Sort)) Sort = ContactSort.Name;
        if (CreateType is { } role && !Enum.IsDefined(role)) CreateType = null;
        if (CreateType is not null)
        {
            ContactId = Guid.NewGuid();
            await PopulateWizardAsync(actor, cancellationToken);
        }
        await LoadContactsAsync(actor, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostFindMatchesAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!EnsureCreateType()) return await ReturnWizardAsync(actor, cancellationToken);
        PossibleMatches = await contacts.FindPossibleMatchesAsync(actor, Name, cancellationToken);
        return await ReturnWizardAsync(actor, cancellationToken);
    }

    public async Task<IActionResult> OnPostChooseExistingAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!EnsureCreateType() || ExistingContactId is not { } existingId)
        {
            return await ReturnWizardAsync(actor, cancellationToken);
        }

        var contact = await contacts.GetAsync(actor, existingId, cancellationToken);
        if (contact is null) return NotFound();
        if (contact.Roles.Contains(CreateType!.Value))
        {
            ModelState.AddModelError(string.Empty, "This contact already has that type.");
            PossibleMatches = [contact];
            return await ReturnWizardAsync(actor, cancellationToken);
        }

        try
        {
            var lease = await editScopes.ClaimAsync(
                new(EditScopeKind.Contact, contact.OrganizationId, contact.Version, actor, OperationKey)
                {
                    TakeOver = TakeOver
                },
                cancellationToken);
            ExistingContact = contact;
            ContactId = contact.OrganizationId;
            ExpectedVersion = contact.Version;
            LeaseToken = lease.Token;
            CopyFrom(contact);
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
            ModelState.AddModelError(string.Empty, "This contact changed. Choose it again.");
        }

        return await ReturnWizardAsync(actor, cancellationToken);
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!EnsureCreateType()) return await ReturnWizardAsync(actor, cancellationToken);
        if (!IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Start again.");
        }
        if (!Enum.IsDefined(PrincipalInspectionMode))
        {
            ModelState.AddModelError(nameof(PrincipalInspectionMode), "Select an inspection type.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                var request = await BuildSaveRequestAsync(actor, cancellationToken);
                await administration.SaveAsync(request, cancellationToken);
                TempData["AdministrationStatus"] = "The contact was saved.";
                return RedirectToPage(new { Search, Type, Sort });
            }
            catch (ContactDirectoryException exception)
            {
                ModelState.AddModelError(string.Empty, ContactErrorMessage(exception.Error));
            }
            catch (EditScopeConflictException)
            {
                ModelState.AddModelError(string.Empty, HeldByAnother);
            }
            catch (EditScopeExpiredException)
            {
                ModelState.AddModelError(string.Empty, "Your edit session expired. Choose the contact again.");
            }
            catch (EditScopeVersionConflictException)
            {
                ModelState.AddModelError(string.Empty, "This contact changed. Choose it again.");
            }
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

        return await ReturnWizardAsync(actor, cancellationToken);
    }

    public async Task<IActionResult> OnPostCancelAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (ExistingContactId is { } contactId && !string.IsNullOrWhiteSpace(LeaseToken))
        {
            try
            {
                await editScopes.ReleaseAsync(
                    new(EditScopeKind.Contact, contactId, actor, NewOperationKey(), LeaseToken),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is EditScopeExpiredException or EditScopeConflictException)
            {
            }
        }
        return RedirectToPage(new { Search, Type, Sort });
    }

    /// <summary>
    /// The release a leaving page beacons. It is not an operator action: it
    /// answers 204 whether or not a scope was still there to release, so a
    /// duplicate beacon and a beacon that lost a race with Cancel are both
    /// ordinary outcomes. Antiforgery is validated as it is for every post.
    /// </summary>
    public async Task<IActionResult> OnPostReleaseScopeBeaconAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (ExistingContactId is not { } contactId || string.IsNullOrWhiteSpace(LeaseToken))
        {
            return new NoContentResult();
        }

        try
        {
            await editScopes.ReleaseAsync(
                new(EditScopeKind.Contact, contactId, actor, NewOperationKey(), LeaseToken),
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

    private async Task<SaveContactRequest> BuildSaveRequestAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        var selectedRole = CreateType!.Value;
        if (ExistingContactId is not { } existingId)
        {
            var newContactAssociations = AssociationsFor(selectedRole, ContactId);
            return new(
                actor,
                ContactId == Guid.Empty ? Guid.NewGuid() : ContactId,
                0,
                Name,
                ContactPerson,
                Email,
                Telephone,
                Address,
                Postcode,
                true,
                [selectedRole],
                selectedRole == ContactRole.Principal ? PrincipalCode : null,
                PrincipalInspectionMode,
                newContactAssociations,
                OperationKey,
                string.Empty);
        }

        var existing = await contacts.GetAsync(actor, existingId, cancellationToken)
            ?? throw new ContactDirectoryException(ContactDirectoryError.ContactNotFound);
        ExistingContact = existing;
        var associations = AssociationsFor(selectedRole, existing.OrganizationId);
        var roles = existing.Roles.Append(selectedRole).Distinct().OrderBy(role => role).ToArray();
        var preservedAssociations = existing.PrincipalAssociations ?? [];
        var mergedAssociations = preservedAssociations
            .Concat(associations)
            .DistinctBy(item => (item.Role, item.PrincipalId))
            .ToArray();
        var principalCode = PrincipalCode;
        var principalInspectionMode = PrincipalInspectionMode;
        if (selectedRole != ContactRole.Principal && existing.Roles.Contains(ContactRole.Principal))
        {
            var principalId = existing.OwnPrincipalIds is { Count: > 0 } ownPrincipalIds
                ? ownPrincipalIds[0]
                : Guid.Empty;
            var principal = principalId == Guid.Empty
                ? null
                : await getPrincipal.ExecuteAsync(actor, principalId, cancellationToken);
            if (principal?.Principal.OrganizationId != existing.OrganizationId)
            {
                throw new ContactDirectoryException(ContactDirectoryError.ContactNotFound);
            }
            principalCode = principal.Principal.Code;
            principalInspectionMode = principal.Principal.InspectionMode;
        }
        return new(
            actor,
            existing.OrganizationId,
            ExpectedVersion,
            Name,
            ContactPerson,
            Email,
            Telephone,
            Address,
            Postcode,
            existing.Active,
            roles,
            principalCode,
            principalInspectionMode,
            mergedAssociations,
            OperationKey,
            LeaseToken ?? string.Empty,
            existing.GuidanceTemplate,
            existing.GuidanceTemplateVersion);
    }

    private ContactPrincipalAssociation[] AssociationsFor(ContactRole role, Guid contactId) =>
        role == ContactRole.Principal
            ? []
            : (AssociatedPrincipalIds ?? [])
                .Where(id => id != Guid.Empty)
                .Distinct()
                .Select(id => new ContactPrincipalAssociation(contactId, role, id))
                .ToArray();

    private bool EnsureCreateType()
    {
        if (CreateType is { } role && Enum.IsDefined(role)) return true;
        ModelState.AddModelError(string.Empty, "Choose a contact type.");
        return false;
    }

    private async Task<IActionResult> ReturnWizardAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        await PopulateWizardAsync(actor, cancellationToken);
        await LoadContactsAsync(actor, cancellationToken);
        return Page();
    }

    private async Task PopulateWizardAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        PrincipalChoices = await contacts.ListPrincipalChoicesAsync(actor, cancellationToken);
        if (ExistingContactId is { } existingId && ExistingContact is null)
        {
            ExistingContact = await contacts.GetAsync(actor, existingId, cancellationToken);
        }
    }

    private async Task LoadContactsAsync(ActionActor actor, CancellationToken cancellationToken) =>
        Contacts = await contacts.ListAsync(new(actor, Type, Sort, Search), cancellationToken);

    private void CopyFrom(ContactDirectoryRecord contact)
    {
        Name = contact.Name;
        ContactPerson = contact.ContactPerson;
        Email = contact.Email;
        Telephone = contact.Telephone;
        Address = contact.Address;
        Postcode = contact.Postcode;
        if (CreateType != ContactRole.Principal)
        {
            AssociatedPrincipalIds = (contact.PrincipalAssociations ?? [])
                .Where(item => item.Role == CreateType)
                .Select(item => item.PrincipalId)
                .ToArray();
        }
    }

    public static string RoleLabel(ContactRole role) => role switch
    {
        ContactRole.Principal => "Principal",
        ContactRole.ClaimSource => "Claim Source",
        ContactRole.Repairer => "Repairer",
        ContactRole.Storage => "Storage",
        ContactRole.ThirdPartyEngineer => "Third Party Engineer",
        _ => role.ToString()
    };

    private static string ContactErrorMessage(ContactDirectoryError error) => error switch
    {
        ContactDirectoryError.DuplicateOrganizationName => "A contact with that name already exists. Choose it from the matches instead.",
        ContactDirectoryError.DuplicatePrincipalCode => "That principal code is already in use.",
        ContactDirectoryError.InvalidPrincipalAssociation => "Check the selected linked principals.",
        ContactDirectoryError.StaleVersion => "This contact changed. Choose it again.",
        ContactDirectoryError.OperationConflict => "This form was already used for a different change.",
        _ => "The contact could not be saved."
    };
}
