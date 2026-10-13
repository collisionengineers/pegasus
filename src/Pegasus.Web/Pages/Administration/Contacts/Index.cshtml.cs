using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration.Contacts;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel(
    IContactDirectoryQueries contacts,
    IContactDirectoryAdministration administration) : AdministrationPageModel
{
    public IReadOnlyList<ContactDirectoryRecord> Contacts { get; private set; } = [];
    public IReadOnlyList<ContactDirectoryRecord> PossibleMatches { get; private set; } = [];
    public IReadOnlyList<PrincipalAdministrationDetails> PrincipalChoices { get; private set; } = [];

    [BindProperty(SupportsGet = true)] public ContactRole? Type { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public ContactRole? CreateType { get; set; }

    [BindProperty] public Guid ContactId { get; set; }
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

    public bool CreateDialogOpen => CreateType is not null;
    public bool CanAssociateSelectedType => CreateType is { } role && role != ContactRole.Principal;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (CreateType is { } role && !Enum.IsDefined(role)) CreateType = null;
        if (CreateType is not null) ContactId = Guid.NewGuid();
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostFindMatchesAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!EnsureCreateType()) return await ReturnWizardAsync(actor, cancellationToken);
        PossibleMatches = await contacts.FindPossibleMatchesAsync(actor, Name, cancellationToken);
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
                await administration.SaveAsync(BuildSaveRequest(actor), cancellationToken);
                TempData["AdministrationStatus"] = "The contact was saved.";
                return RedirectToPage(new { Search, Type });
            }
            catch (ContactDirectoryException exception)
            {
                ModelState.AddModelError(string.Empty, ContactErrorMessage(exception.Error));
            }
            catch (ArgumentException exception)
            {
                // A named field error is shown against its own control; every
                // other refusal keeps the general notice.
                if (exception.ParamName == nameof(Telephone))
                {
                    ModelState.AddModelError(nameof(Telephone), "Phone must be digits.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "The contact details were not accepted.");
                }
            }
        }

        return await ReturnWizardAsync(actor, cancellationToken);
    }

    private SaveContactRequest BuildSaveRequest(ActionActor actor)
    {
        var selectedRole = CreateType!.Value;
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
            selectedRole,
            selectedRole == ContactRole.Principal ? PrincipalCode : null,
            PrincipalInspectionMode,
            selectedRole == ContactRole.Principal
                ? []
                : (AssociatedPrincipalIds ?? []).Where(id => id != Guid.Empty).Distinct().ToArray(),
            OperationKey);
    }

    private bool EnsureCreateType()
    {
        if (CreateType is { } role && Enum.IsDefined(role)) return true;
        ModelState.AddModelError(string.Empty, "Choose a contact type.");
        return false;
    }

    private async Task<IActionResult> ReturnWizardAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    // The Principal choices serve the create dialog and the Linked principals column.
    private async Task LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        PrincipalChoices = await contacts.ListPrincipalChoicesAsync(actor, cancellationToken);
        Contacts = await contacts.ListAsync(new(actor, Type, Search), cancellationToken);
    }

    /// <summary>The codes of the active Principals a contact is linked to, in the choices' order.</summary>
    public string[] LinkedPrincipalCodes(ContactDirectoryRecord contact) =>
        [.. PrincipalChoices.Where(choice => contact.LinkedPrincipalIds?.Contains(choice.Principal.Id) == true).Select(choice => choice.Principal.Code)];

    private static string ContactErrorMessage(ContactDirectoryError error) => error switch
    {
        ContactDirectoryError.DuplicateOrganizationName => "A contact with that name already exists.",
        ContactDirectoryError.DuplicatePrincipalCode => "That principal code is already in use.",
        ContactDirectoryError.InvalidPrincipalAssociation => "Check the selected linked principals.",
        ContactDirectoryError.StaleVersion => "This contact changed. Choose it again.",
        ContactDirectoryError.OperationConflict => "This form was already used for a different change.",
        _ => "The contact could not be saved."
    };
}
