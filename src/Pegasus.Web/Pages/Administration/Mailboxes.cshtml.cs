using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Web.Mcp;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class MailboxesModel(
    ListApprovedMailboxes listApprovedMailboxes,
    UpdateApprovedMailbox updateApprovedMailbox,
    IApprovedMailboxPollStatusQueries pollStatusQueries,
    IApprovedMailboxSubscriptionStore subscriptionStore,
    IResolveApprovedMailboxIdentity resolveApprovedMailboxIdentity,
    ICheckApprovedMailboxAccess checkApprovedMailboxAccess,
    TimeProvider timeProvider,
    ListApprovedOutlookCategories listCategories,
    UpdateApprovedOutlookCategory updateCategory)
    : AdministrationPageModel
{
    public IReadOnlyList<ApprovedMailbox> Mailboxes { get; private set; } = [];

    public IReadOnlyList<ApprovedMailboxPollStatus> PollStatuses { get; private set; } = [];

    public IReadOnlyList<ApprovedMailboxSubscription> Subscriptions { get; private set; } = [];

    public IReadOnlyList<ApprovedOutlookCategory> Categories { get; private set; } = [];

    public bool AutomationComposed { get; private set; }

    public Guid NewMailboxId { get; private set; }

    public string NewMailboxOperationKey { get; private set; } = NewOperationKey();

    public Guid NewCategoryId { get; private set; }

    public string NewCategoryOperationKey { get; private set; } = NewOperationKey();

    public bool IsNewMailboxEditorOpen { get; private set; }

    public bool IsNewCategoryEditorOpen { get; private set; }

    [BindProperty]
    public MailboxFormInput? MailboxForm { get; set; }

    [BindProperty]
    public CategoryFormInput? CategoryForm { get; set; }

    public async Task<IActionResult> OnGetAsync(
        Guid? mailboxId,
        int? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageApprovedMailboxes);
        await LoadAsync(actor, cancellationToken);
        if (mailboxId is { } selectedMailboxId)
        {
            var mailbox = Mailboxes.SingleOrDefault(item => item.Id == selectedMailboxId);
            if (mailbox is null) return NotFound();
            if (expectedVersion != mailbox.Version)
            {
                ModelState.AddModelError(string.Empty,
                    "The mailbox policy changed. Reload the page and open Settings again.");
            }
            else
            {
                MailboxForm = new()
                {
                    MailboxId = mailbox.Id,
                    Address = mailbox.Address,
                    SelectedRouteScopes = mailbox.RouteScopes.Select(item => item.ToString()).ToArray(),
                    SelectedState = mailbox.State.ToString(),
                    ExpectedVersion = mailbox.Version,
                    OperationKey = NewOperationKey()
                };
            }
        }
        PrepareFormState();
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageApprovedMailboxes);
        await LoadMailboxesAsync(actor, cancellationToken);
        var input = RequireForm(MailboxForm, value => MailboxForm = value);
        ValidateForm(input, nameof(MailboxForm));
        var routeScopes = ParseRouteScopes(input.SelectedRouteScopes);
        if (!Enum.TryParse<ApprovedMailboxState>(
                input.SelectedState,
                ignoreCase: false,
                out var state)
            || !Enum.IsDefined(state))
        {
            ModelState.AddModelError(
                nameof(MailboxFormInput.SelectedState),
                "Select a supported mailbox state.");
        }
        if (input.MailboxId == Guid.Empty || !IsOperationKeyValid(input.OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        }

        var isNewMailbox = input.ExpectedVersion == 0;
        var existingMailbox = isNewMailbox
            ? null
            : Mailboxes.SingleOrDefault(mailbox => mailbox.Id == input.MailboxId);
        ApprovedMailboxIdentityResolution? resolution = null;
        var requiresIdentityCheck = isNewMailbox
            || existingMailbox is { State: ApprovedMailboxState.Disabled }
                && (state == ApprovedMailboxState.Approved
                    || !string.Equals(input.Address, existingMailbox.Address, StringComparison.OrdinalIgnoreCase));
        if (ModelState.IsValid && requiresIdentityCheck)
        {
            string normalizedAddress;
            try
            {
                normalizedAddress = ApprovedMailboxAddress.Normalize(input.Address);
            }
            catch (ArgumentException)
            {
                normalizedAddress = string.Empty;
                ModelState.AddModelError(
                    nameof(MailboxFormInput.Address),
                    "Enter a supported mailbox address and route scope.");
            }

            if (ModelState.IsValid)
            {
                resolution = existingMailbox is
                {
                    State: ApprovedMailboxState.Disabled,
                    MailboxIdentity: { } mailboxIdentity,
                    InboxFolderIdentity: { } inboxFolderIdentity,
                    SentFolderIdentity: { } sentFolderIdentity
                }
                    && state == ApprovedMailboxState.Approved
                    && string.Equals(
                        normalizedAddress,
                        existingMailbox.Address,
                        StringComparison.Ordinal)
                    ? new(
                        mailboxIdentity,
                        inboxFolderIdentity,
                        sentFolderIdentity)
                    : await resolveApprovedMailboxIdentity.ResolveAsync(
                        normalizedAddress,
                        cancellationToken);
                if (resolution is null)
                {
                    ModelState.AddModelError(
                        nameof(MailboxFormInput.Address),
                        "The address could not be found in the mail system.");
                }
                else if (!await CanReadInboxAsync(resolution, cancellationToken))
                {
                    ModelState.AddModelError(
                        nameof(MailboxFormInput.Address),
                        "Pegasus could not verify read access to this mailbox. The change was not saved.");
                }
            }
        }

        if (ModelState.IsValid)
        {
            try
            {
                var updated = await updateApprovedMailbox.ExecuteAsync(
                    new(
                        input.MailboxId,
                        input.Address,
                        routeScopes,
                        state,
                        input.ExpectedVersion,
                        actor,
                        input.OperationKey,
                        resolution?.MailboxIdentity ?? existingMailbox?.MailboxIdentity,
                        resolution?.InboxFolderIdentity ?? existingMailbox?.InboxFolderIdentity,
                        resolution?.SentFolderIdentity ?? existingMailbox?.SentFolderIdentity),
                    cancellationToken);
                TempData["AdministrationStatus"] =
                    $"The mailbox policy for {updated.Address} was saved.";
                return RedirectToPage();
            }
            catch (ApprovedMailboxUpdateException exception)
            {
                ModelState.AddModelError(string.Empty, MailboxErrorMessage(exception));
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(
                    nameof(MailboxFormInput.Address),
                    "Enter a supported mailbox address and route scope.");
            }
        }

        IsNewMailboxEditorOpen = isNewMailbox;
        await LoadAsync(actor, cancellationToken);
        PrepareFormState();
        return Page();
    }

    public async Task<IActionResult> OnPostSaveCategoryAsync(
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageApprovedOutlookCategories);
        var input = RequireForm(CategoryForm, value => CategoryForm = value);
        ValidateForm(input, nameof(CategoryForm));
        if (!Enum.TryParse<ApprovedOutlookCategoryState>(
                input.SelectedState,
                ignoreCase: false,
                out var state)
            || !Enum.IsDefined(state))
        {
            ModelState.AddModelError(
                nameof(CategoryFormInput.SelectedState),
                "Select a supported state.");
        }
        if (input.CategoryId == Guid.Empty || !IsOperationKeyValid(input.OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                var saved = await updateCategory.ExecuteAsync(
                    new(
                        input.CategoryId,
                        input.DisplayName,
                        state,
                        input.ExpectedVersion,
                        actor,
                        input.OperationKey),
                    cancellationToken);
                TempData["AdministrationStatus"] =
                    $"The mail category {saved.DisplayName} was saved.";
                return RedirectToPage();
            }
            catch (ApprovedOutlookCategoryUpdateException exception)
            {
                ModelState.AddModelError(string.Empty, CategoryErrorMessage(exception));
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(
                    nameof(CategoryFormInput.DisplayName),
                    "Enter a supported display name.");
            }
        }

        IsNewCategoryEditorOpen = input.ExpectedVersion == 0;
        await LoadAsync(actor, cancellationToken);
        PrepareFormState();
        return Page();
    }

    public string AddressFor(ApprovedMailbox mailbox) =>
        MailboxForm is { ExpectedVersion: > 0 } input && input.MailboxId == mailbox.Id
            ? input.Address
            : mailbox.Address;

    public bool IsEditingMailbox(ApprovedMailbox mailbox) =>
        MailboxForm is { ExpectedVersion: > 0 } input
        && input.MailboxId == mailbox.Id;

    public int ExpectedVersionFor(ApprovedMailbox mailbox) =>
        MailboxForm is { ExpectedVersion: > 0 } input && input.MailboxId == mailbox.Id
            ? input.ExpectedVersion
            : mailbox.Version;

    public string OperationKeyFor(ApprovedMailbox mailbox) =>
        MailboxForm is { ExpectedVersion: > 0 } input && input.MailboxId == mailbox.Id
            ? input.OperationKey
            : NewOperationKey();

    public bool IsRouteSelected(
        ApprovedMailbox mailbox,
        ApprovedMailboxRouteScope routeScope) =>
        MailboxForm is { ExpectedVersion: > 0 } input && input.MailboxId == mailbox.Id
            ? input.SelectedRouteScopes.Contains(routeScope.ToString(), StringComparer.Ordinal)
            : mailbox.RouteScopes.Contains(routeScope);

    public bool IsStateSelected(
        ApprovedMailbox mailbox,
        ApprovedMailboxState state) =>
        MailboxForm is { ExpectedVersion: > 0 } input && input.MailboxId == mailbox.Id
            ? input.SelectedState == state.ToString()
            : mailbox.State == state;

    public string NewAddress =>
        MailboxForm is { ExpectedVersion: 0 } input ? input.Address : string.Empty;

    public ApprovedMailboxPollStatus? PollStatusForMailbox(ApprovedMailbox mailbox) =>
        PollStatuses.SingleOrDefault(item =>
            string.Equals(item.MailboxAddress, mailbox.Address, StringComparison.OrdinalIgnoreCase));

    public string PollFreshnessFor(ApprovedMailboxPollStatus status) =>
        status.IsFresh(timeProvider.GetUtcNow()) ? "Fresh" : "Stale";

    public bool IsNewRouteSelected(ApprovedMailboxRouteScope routeScope) =>
        MailboxForm is { ExpectedVersion: 0 } input
        && input.SelectedRouteScopes.Contains(routeScope.ToString(), StringComparer.Ordinal);

    public bool IsNewStateSelected(ApprovedMailboxState state) =>
        MailboxForm is { ExpectedVersion: 0 } input
            ? input.SelectedState == state.ToString()
            : state == ApprovedMailboxState.Approved;

    public string PollStatusFor(ApprovedMailbox mailbox)
    {
        var status = PollStatuses.SingleOrDefault(item =>
            string.Equals(
                item.MailboxAddress,
                mailbox.Address,
                StringComparison.OrdinalIgnoreCase));
        return Presentation.OperatorLabels.MailSettings.PollStatus(mailbox, status);
    }

    public string SubscriptionStatusFor(ApprovedMailbox mailbox)
    {
        var subscription = Subscriptions.SingleOrDefault(item =>
            item.ApprovedMailboxId == mailbox.Id);
        return Presentation.OperatorLabels.MailSettings.SubscriptionStatus(subscription);
    }

    public string CategoryDisplayNameFor(ApprovedOutlookCategory category) =>
        CategoryForm is { ExpectedVersion: > 0 } input && input.CategoryId == category.Id
            ? input.DisplayName
            : category.DisplayName;

    public int ExpectedCategoryVersionFor(ApprovedOutlookCategory category) =>
        CategoryForm is { ExpectedVersion: > 0 } input && input.CategoryId == category.Id
            ? input.ExpectedVersion
            : category.Version;

    public string CategoryOperationKeyFor(ApprovedOutlookCategory category) =>
        CategoryForm is { ExpectedVersion: > 0 } input && input.CategoryId == category.Id
            ? input.OperationKey
            : NewOperationKey();

    public bool IsCategoryStateSelected(
        ApprovedOutlookCategory category,
        ApprovedOutlookCategoryState state) =>
        CategoryForm is { ExpectedVersion: > 0 } input && input.CategoryId == category.Id
            ? input.SelectedState == state.ToString()
            : category.State == state;

    public string NewCategoryDisplayName =>
        CategoryForm is { ExpectedVersion: 0 } input ? input.DisplayName : string.Empty;

    public bool IsNewCategoryStateSelected(ApprovedOutlookCategoryState state) =>
        CategoryForm is { ExpectedVersion: 0 } input
            ? input.SelectedState == state.ToString()
            : state == ApprovedOutlookCategoryState.Active;

    private TForm RequireForm<TForm>(TForm? form, Action<TForm> assign)
        where TForm : class, new()
    {
        if (form is not null)
        {
            return form;
        }

        ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        var created = new TForm();
        assign(created);
        return created;
    }

    private void ValidateForm<TForm>(TForm form, string prefix)
        where TForm : class
    {
        var validationResults = new List<ValidationResult>();
        if (Validator.TryValidateObject(
                form,
                new ValidationContext(form),
                validationResults,
                validateAllProperties: true))
        {
            return;
        }

        foreach (var result in validationResults)
        {
            var members = result.MemberNames.DefaultIfEmpty(string.Empty);
            foreach (var member in members)
            {
                var key = string.IsNullOrEmpty(member) ? prefix : $"{prefix}.{member}";
                ModelState.AddModelError(key, result.ErrorMessage ?? "The value is not valid.");
            }
        }
    }

    private HashSet<ApprovedMailboxRouteScope> ParseRouteScopes(
        IReadOnlyCollection<string> selectedRouteScopes)
    {
        var routeScopes = new HashSet<ApprovedMailboxRouteScope>();
        foreach (var value in selectedRouteScopes)
        {
            if (!Enum.TryParse<ApprovedMailboxRouteScope>(
                    value,
                    ignoreCase: false,
                    out var routeScope)
                || !Enum.IsDefined(routeScope))
            {
                ModelState.AddModelError(
                    nameof(MailboxFormInput.SelectedRouteScopes),
                    "Select only supported mailbox route scopes.");
                continue;
            }

            routeScopes.Add(routeScope);
        }
        if (routeScopes.Count == 0)
        {
            ModelState.AddModelError(
                nameof(MailboxFormInput.SelectedRouteScopes),
                "Select at least one mailbox route scope.");
        }

        return routeScopes;
    }

    private async Task<bool> CanReadInboxAsync(
        ApprovedMailboxIdentityResolution resolution,
        CancellationToken cancellationToken)
    {
        try
        {
            return await (resolveApprovedMailboxIdentity as ICheckApprovedMailboxAccess
                    ?? checkApprovedMailboxAccess)
                .CanReadInboxAsync(resolution, cancellationToken);
        }
        catch (ApprovedMailboxAccessDeniedException)
        {
            return false;
        }
    }

    private async Task LoadMailboxesAsync(ActionActor actor, CancellationToken cancellationToken) =>
        Mailboxes = await listApprovedMailboxes.ExecuteAsync(actor, cancellationToken);

    private async Task LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        AutomationComposed =
            HttpContext.RequestServices.GetService<AutomationClientRegistry>() is not null;
        await LoadMailboxesAsync(actor, cancellationToken);
        PollStatuses = await pollStatusQueries.ListAsync(cancellationToken);
        Subscriptions = await subscriptionStore.ListAsync(cancellationToken);
        Categories = await listCategories.ExecuteAsync(actor, cancellationToken);
    }

    private void PrepareFormState()
    {
        if (MailboxForm is { } mailboxInput && !IsOperationKeyValid(mailboxInput.OperationKey))
        {
            mailboxInput.OperationKey = NewOperationKey();
        }
        if (CategoryForm is { } categoryInput && !IsOperationKeyValid(categoryInput.OperationKey))
        {
            categoryInput.OperationKey = NewOperationKey();
        }

        NewMailboxId = MailboxForm is { ExpectedVersion: 0 } newMailbox
            && newMailbox.MailboxId != Guid.Empty
            ? newMailbox.MailboxId
            : Guid.NewGuid();
        NewMailboxOperationKey = MailboxForm is { ExpectedVersion: 0 } newMailboxForm
            && IsOperationKeyValid(newMailboxForm.OperationKey)
            ? newMailboxForm.OperationKey
            : NewOperationKey();
        NewCategoryId = CategoryForm is { ExpectedVersion: 0 } newCategory
            && newCategory.CategoryId != Guid.Empty
            ? newCategory.CategoryId
            : Guid.NewGuid();
        NewCategoryOperationKey = CategoryForm is { ExpectedVersion: 0 } newCategoryForm
            && IsOperationKeyValid(newCategoryForm.OperationKey)
            ? newCategoryForm.OperationKey
            : NewOperationKey();
    }

    private static string MailboxErrorMessage(ApprovedMailboxUpdateException exception) =>
        exception.Error switch
        {
            ApprovedMailboxUpdateError.NotFound =>
                "The mailbox policy no longer exists. Your change was not applied.",
            ApprovedMailboxUpdateError.DuplicateAddress =>
                "That mailbox address already has a policy. Update the existing row instead.",
            ApprovedMailboxUpdateError.VersionConflict =>
                "The mailbox policy changed after this form was loaded. " +
                "Your change was not applied; reload and review the current row before retrying.",
            ApprovedMailboxUpdateError.OperationConflict =>
                "This form was already used for another mailbox change. Review the current row and retry.",
            ApprovedMailboxUpdateError.MissingMailboxIdentity =>
                "This mailbox cannot be approved for that route scope yet.",
            ApprovedMailboxUpdateError.InvalidMailboxIdentity =>
                "The resolved identity for this mailbox was not valid. Try again.",
            ApprovedMailboxUpdateError.MailboxIdentityImmutable =>
                "This mailbox's address cannot be changed once saved. Disable it and add a new one.",
            ApprovedMailboxUpdateError.DuplicateMailboxIdentity =>
                "That address already resolves to a mailbox approved under another row.",
            _ => "The approved-mailbox change was not accepted."
        };

    private static string CategoryErrorMessage(
        ApprovedOutlookCategoryUpdateException exception) => exception.Error switch
    {
        ApprovedOutlookCategoryUpdateError.DuplicateDisplayName =>
            "That display name is already configured.",
        ApprovedOutlookCategoryUpdateError.VersionConflict =>
            "The category policy changed. Your change was not applied; reload and review the current row before retrying.",
        ApprovedOutlookCategoryUpdateError.OperationConflict =>
            "This form was already used for another change. Review and retry.",
        ApprovedOutlookCategoryUpdateError.NotFound =>
            "The category policy no longer exists.",
        _ => "The category policy was not saved."
    };

    [ValidateNever]
    public sealed class MailboxFormInput
    {
        public Guid MailboxId { get; set; }

        [Required, StringLength(320, MinimumLength = 3)]
        public string Address { get; set; } = string.Empty;

        public string[] SelectedRouteScopes { get; set; } = [];

        [Required]
        public string SelectedState { get; set; } = ApprovedMailboxState.Approved.ToString();

        [Range(0, int.MaxValue)]
        public int ExpectedVersion { get; set; }

        public string OperationKey { get; set; } = string.Empty;
    }

    [ValidateNever]
    public sealed class CategoryFormInput
    {
        public Guid CategoryId { get; set; }

        [Required, StringLength(255, MinimumLength = 1)]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        public string SelectedState { get; set; } =
            ApprovedOutlookCategoryState.Active.ToString();

        [Range(0, int.MaxValue)]
        public int ExpectedVersion { get; set; }

        public string OperationKey { get; set; } = string.Empty;

    }
}
