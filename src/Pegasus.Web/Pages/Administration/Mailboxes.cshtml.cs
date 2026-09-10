using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Web.Mcp;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class MailboxesModel(
    ListApprovedMailboxes listApprovedMailboxes,
    UpdateApprovedMailbox updateApprovedMailbox,
    SetDefaultApprovedMailbox setDefaultApprovedMailbox,
    IApprovedMailboxPollStatusQueries pollStatusQueries,
    IApprovedMailboxSubscriptionStore subscriptionStore,
    IResolveApprovedMailboxIdentity resolveApprovedMailboxIdentity,
    ICheckApprovedMailboxAccess checkApprovedMailboxAccess,
    TimeProvider timeProvider,
    ListApprovedOutlookCategories listCategories,
    UpdateApprovedOutlookCategory updateCategory,
    IEditScopeLeases editScopes,
    IDescribeCaseEditAuthorityHolder describeEditAuthorityHolder)
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

    /// <summary>
    /// The default-sender dialog opens only from its own handlers. The bound
    /// form object exists on every POST, so its presence cannot decide this.
    /// </summary>
    public bool IsDefaultEditorOpen { get; private set; }

    private DefaultMailboxFormInput? ActiveDefaultForm =>
        IsDefaultEditorOpen ? DefaultMailboxForm : null;

    public EditScopeLease? MailboxEditLease { get; private set; }

    /// <summary>
    /// The mailbox policy this operator is already editing in another window,
    /// offered with the take-over that ends the other window's claim.
    /// </summary>
    public Guid TakeOverMailboxId { get; private set; }

    /// <summary>The same for an Outlook category.</summary>
    public Guid TakeOverCategoryId { get; private set; }

    [BindProperty]
    public MailboxFormInput? MailboxForm { get; set; }

    [BindProperty]
    public DefaultMailboxFormInput? DefaultMailboxForm { get; set; }

    [BindProperty]
    public CategoryFormInput? CategoryForm { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageApprovedMailboxes);
        await LoadAsync(actor, cancellationToken);
        PrepareFormState();
        return Page();
    }

    public async Task<IActionResult> OnPostEditMailboxAsync(
        Guid mailboxId,
        int expectedVersion,
        bool takeOver,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageApprovedMailboxes);
        await LoadAsync(actor, cancellationToken);
        var mailbox = Mailboxes.SingleOrDefault(item => item.Id == mailboxId);
        if (mailbox is null || mailbox.Version != expectedVersion)
        {
            ModelState.AddModelError(string.Empty,
                "The mailbox policy changed after this page was loaded. Review it and retry.");
            return Page();
        }

        try
        {
            MailboxEditLease = await editScopes.ClaimAsync(
                new(EditScopeKind.ApprovedMailbox, mailbox.Id, mailbox.Version, actor,
                    $"approved-mailbox-edit:{Guid.NewGuid():N}")
                {
                    TakeOver = takeOver
                },
                cancellationToken);
            MailboxForm = new()
            {
                MailboxId = mailbox.Id,
                Address = mailbox.Address,
                SelectedRouteScopes = mailbox.RouteScopes.Select(item => item.ToString()).ToArray(),
                SelectedState = mailbox.State.ToString(),
                ExpectedVersion = mailbox.Version,
                OperationKey = NewOperationKey(),
                VerifiedEncodedMessageSizeLimit = mailbox.VerifiedEncodedMessageSizeLimit,
                EditLeaseToken = MailboxEditLease.Token
            };
        }
        catch (EditScopeHeldElsewhereException)
        {
            TakeOverMailboxId = mailboxId;
            ModelState.AddModelError(string.Empty, EditModeDisplay.HeldElsewhere(MailboxRecordName));
        }
        catch (EditScopeConflictException)
        {
            ModelState.AddModelError(string.Empty,
                await EditConflictMessageAsync(EditScopeKind.ApprovedMailbox, mailboxId, actor, MailboxRecordName, cancellationToken));
        }
        catch (EditScopeVersionConflictException)
        {
            ModelState.AddModelError(string.Empty,
                "The mailbox policy changed while you were working. Reload and try again.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostCancelMailboxEditAsync(
        Guid mailboxId,
        string editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            await editScopes.ReleaseAsync(
                new(EditScopeKind.ApprovedMailbox, mailboxId, actor,
                    $"approved-mailbox-edit-release:{Guid.NewGuid():N}", editLeaseToken),
                cancellationToken);
        }
        catch (EditScopeExpiredException)
        {
            // An expired scope has no pending record mutation to cancel.
        }

        return RedirectToPage();
    }

    public Task<IActionResult> OnPostHeartbeatMailboxEditAsync(
        Guid mailboxId,
        string? editLeaseToken,
        CancellationToken cancellationToken) =>
        HeartbeatEditAsync(
            EditScopeKind.ApprovedMailbox,
            mailboxId,
            editLeaseToken,
            "mailbox policy",
            cancellationToken);

    public async Task<IActionResult> OnPostEditDefaultAsync(bool takeOver, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageApprovedMailboxes);
        await LoadAsync(actor, cancellationToken);
        IsDefaultEditorOpen = true;
        var input = RequireForm(DefaultMailboxForm, value => DefaultMailboxForm = value);
        if (!TryParseMailboxSelection(input.SelectedMailbox, out var mailboxId, out var expectedVersion))
        {
            ModelState.AddModelError(nameof(DefaultMailboxFormInput.SelectedMailbox),
                "Select an eligible staff-send mailbox.");
            return Page();
        }

        try
        {
            var lease = await editScopes.ClaimAsync(
                new(EditScopeKind.ApprovedMailbox, mailboxId, expectedVersion, actor,
                    $"approved-mailbox-default-edit:{Guid.NewGuid():N}")
                {
                    TakeOver = takeOver
                },
                cancellationToken);
            input.EditLeaseToken = lease.Token;
            input.OperationKey = NewOperationKey();
        }
        catch (EditScopeHeldElsewhereException)
        {
            TakeOverMailboxId = mailboxId;
            ModelState.AddModelError(string.Empty, EditModeDisplay.HeldElsewhere(MailboxRecordName));
        }
        catch (EditScopeConflictException)
        {
            ModelState.AddModelError(string.Empty,
                await EditConflictMessageAsync(EditScopeKind.ApprovedMailbox, mailboxId, actor, MailboxRecordName, cancellationToken));
        }
        catch (EditScopeVersionConflictException)
        {
            ModelState.AddModelError(string.Empty,
                "The selected mailbox changed while you were working. Reload and try again.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostCancelDefaultEditAsync(
        Guid mailboxId,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        await OnPostCancelMailboxEditAsync(mailboxId, editLeaseToken, cancellationToken);

    public async Task<IActionResult> OnPostUpdateAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageApprovedMailboxes);
        await LoadAsync(actor, cancellationToken);
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
                resolution = await resolveApprovedMailboxIdentity.ResolveAsync(
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
                        resolution?.SentFolderIdentity ?? existingMailbox?.SentFolderIdentity,
                        resolution?.FolderBindings ?? existingMailbox?.FolderBindings,
                        input.VerifiedEncodedMessageSizeLimit)
                    {
                        EditLeaseToken = input.EditLeaseToken
                    },
                    cancellationToken);
                TempData["AdministrationStatus"] =
                    $"The mailbox policy for {updated.Address} was saved.";
                return RedirectToPage();
            }
            catch (ApprovedMailboxUpdateException exception)
            {
                ModelState.AddModelError(string.Empty, MailboxErrorMessage(exception));
            }
            catch (EditScopeConflictException)
            {
                ModelState.AddModelError(string.Empty,
                    await EditConflictMessageAsync(EditScopeKind.ApprovedMailbox, input.MailboxId, actor, "mailbox policy", cancellationToken));
            }
            catch (EditScopeExpiredException)
            {
                ModelState.AddModelError(string.Empty, "Editing expired before this change was saved. Reload and try again.");
            }
            catch (EditScopeVersionConflictException)
            {
                ModelState.AddModelError(string.Empty, "The mailbox policy changed while you were working. Reload and try again.");
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

    public async Task<IActionResult> OnPostResolveFoldersAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageApprovedMailboxes);
        await LoadAsync(actor, cancellationToken);
        var input = RequireForm(MailboxForm, value => MailboxForm = value);
        ValidateForm(input, nameof(MailboxForm));
        var mailbox = Mailboxes.SingleOrDefault(item => item.Id == input.MailboxId);
        if (mailbox is null
            || mailbox.MailboxIdentity is null
            || input.ExpectedVersion != mailbox.Version
            || !IsOperationKeyValid(input.OperationKey))
        {
            ModelState.AddModelError(
                string.Empty,
                "The mailbox policy changed after this form was loaded. Review it and retry.");
        }

        ApprovedMailboxIdentityResolution? resolution = null;
        if (ModelState.IsValid)
        {
            resolution = await resolveApprovedMailboxIdentity.ResolveAsync(
                mailbox!.Address,
                cancellationToken);
            if (resolution is null
                || !string.Equals(
                    resolution.MailboxIdentity,
                    mailbox.MailboxIdentity,
                    StringComparison.Ordinal))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The logical folders could not be resolved for this exact mailbox.");
            }
        }

        if (ModelState.IsValid)
        {
            try
            {
                var updated = await updateApprovedMailbox.ExecuteAsync(
                    new(
                        mailbox!.Id,
                        mailbox.Address,
                        mailbox.RouteScopes,
                        mailbox.State,
                        mailbox.Version,
                        actor,
                        input.OperationKey,
                        mailbox.MailboxIdentity,
                        mailbox.InboxFolderIdentity,
                        mailbox.SentFolderIdentity,
                        resolution!.FolderBindings ?? [],
                        mailbox.VerifiedEncodedMessageSizeLimit)
                    {
                        EditLeaseToken = input.EditLeaseToken
                    },
                    cancellationToken);
                TempData["AdministrationStatus"] =
                    $"{updated.FolderBindings.Count} logical folder bindings were saved for {updated.Address}.";
                return RedirectToPage();
            }
            catch (ApprovedMailboxUpdateException exception)
            {
                ModelState.AddModelError(string.Empty, MailboxErrorMessage(exception));
            }
            catch (EditScopeConflictException)
            {
                ModelState.AddModelError(string.Empty,
                    await EditConflictMessageAsync(EditScopeKind.ApprovedMailbox, mailbox!.Id, actor, "mailbox policy", cancellationToken));
            }
            catch (EditScopeExpiredException)
            {
                ModelState.AddModelError(string.Empty, "Editing expired before this change was saved. Reload and try again.");
            }
            }

        await LoadAsync(actor, cancellationToken);
        PrepareFormState();
        return Page();
    }

    public async Task<IActionResult> OnPostSetDefaultAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageApprovedMailboxes);
        await LoadAsync(actor, cancellationToken);
        IsDefaultEditorOpen = true;
        var input = RequireForm(DefaultMailboxForm, value => DefaultMailboxForm = value);
        ValidateForm(input, nameof(DefaultMailboxForm));
        if (!TryParseMailboxSelection(input.SelectedMailbox, out var mailboxId, out var expectedVersion))
        {
            ModelState.AddModelError(
                nameof(DefaultMailboxFormInput.SelectedMailbox),
                "Select an eligible staff-send mailbox.");
        }
        if (!IsOperationKeyValid(input.OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                var selected = await setDefaultApprovedMailbox.ExecuteAsync(
                    new(
                        mailboxId,
                        expectedVersion,
                        input.ExpectedPreviousDefaultMailboxId,
                        input.ExpectedPreviousDefaultMailboxVersion,
                        actor,
                        input.OperationKey)
                    {
                        EditLeaseToken = input.EditLeaseToken
                    },
                    cancellationToken);
                TempData["AdministrationStatus"] =
                    $"{selected.Address} is the default Compose sender.";
                return RedirectToPage();
            }
            catch (ApprovedMailboxUpdateException exception)
            {
                ModelState.AddModelError(string.Empty, MailboxErrorMessage(exception));
            }
            catch (EditScopeConflictException)
            {
                ModelState.AddModelError(string.Empty,
                    await EditConflictMessageAsync(EditScopeKind.ApprovedMailbox, mailboxId, actor, "mailbox policy", cancellationToken));
            }
            catch (EditScopeExpiredException)
            {
                ModelState.AddModelError(string.Empty, "Editing expired before this change was saved. Reload and try again.");
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(
                    nameof(DefaultMailboxFormInput.SelectedMailbox),
                    "Select an eligible staff-send mailbox.");
            }
        }

        await LoadAsync(actor, cancellationToken);
        PrepareFormState();
        return Page();
    }

    public async Task<IActionResult> OnPostEditCategoryAsync(
        Guid categoryId,
        int expectedVersion,
        bool takeOver,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageApprovedOutlookCategories);
        await LoadAsync(actor, cancellationToken);
        var category = Categories.SingleOrDefault(item => item.Id == categoryId);
        if (category is null || category.Version != expectedVersion)
        {
            ModelState.AddModelError(string.Empty,
                "The Outlook category changed after this page was loaded. Review it and retry.");
            return Page();
        }

        try
        {
            var lease = await editScopes.ClaimAsync(
                new(EditScopeKind.ApprovedOutlookCategory, category.Id, category.Version, actor,
                    $"outlook-category-edit:{Guid.NewGuid():N}")
                {
                    TakeOver = takeOver
                },
                cancellationToken);
            CategoryForm = new()
            {
                CategoryId = category.Id,
                DisplayName = category.DisplayName,
                SelectedState = category.State.ToString(),
                ExpectedVersion = category.Version,
                OperationKey = NewOperationKey(),
                EditLeaseToken = lease.Token
            };
        }
        catch (EditScopeHeldElsewhereException)
        {
            TakeOverCategoryId = categoryId;
            ModelState.AddModelError(string.Empty, EditModeDisplay.HeldElsewhere(CategoryRecordName));
        }
        catch (EditScopeConflictException)
        {
            ModelState.AddModelError(string.Empty,
                await EditConflictMessageAsync(
                    EditScopeKind.ApprovedOutlookCategory,
                    categoryId,
                    actor,
                    CategoryRecordName,
                    cancellationToken));
        }
        catch (EditScopeVersionConflictException)
        {
            ModelState.AddModelError(string.Empty,
                "The Outlook category changed while you were working. Reload and try again.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostCancelCategoryEditAsync(
        Guid categoryId,
        string editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            await editScopes.ReleaseAsync(
                new(EditScopeKind.ApprovedOutlookCategory, categoryId, actor,
                    $"outlook-category-edit-release:{Guid.NewGuid():N}", editLeaseToken),
                cancellationToken);
        }
        catch (EditScopeExpiredException)
        {
            // A stale scope cannot protect a further mutation.
        }

        return RedirectToPage();
    }

    public Task<IActionResult> OnPostHeartbeatCategoryEditAsync(
        Guid categoryId,
        string? editLeaseToken,
        CancellationToken cancellationToken) =>
        HeartbeatEditAsync(
            EditScopeKind.ApprovedOutlookCategory,
            categoryId,
            editLeaseToken,
            "Outlook category",
            cancellationToken);

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
                        input.OperationKey)
                    {
                        EditLeaseToken = input.EditLeaseToken
                    },
                    cancellationToken);
                TempData["AdministrationStatus"] =
                    $"The mail category {saved.DisplayName} was saved.";
                return RedirectToPage();
            }
            catch (ApprovedOutlookCategoryUpdateException exception)
            {
                ModelState.AddModelError(string.Empty, CategoryErrorMessage(exception));
            }
            catch (EditScopeConflictException)
            {
                ModelState.AddModelError(string.Empty,
                    await EditConflictMessageAsync(
                        EditScopeKind.ApprovedOutlookCategory,
                        input.CategoryId,
                        actor,
                        "Outlook category",
                        cancellationToken));
            }
            catch (EditScopeExpiredException)
            {
                ModelState.AddModelError(string.Empty, "Editing expired before this change was saved. Reload and try again.");
            }
            catch (EditScopeVersionConflictException)
            {
                ModelState.AddModelError(string.Empty, "The Outlook category changed while you were working. Reload and try again.");
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
        && input.MailboxId == mailbox.Id
        && !string.IsNullOrWhiteSpace(input.EditLeaseToken);

    public string EditLeaseTokenFor(ApprovedMailbox mailbox) =>
        IsEditingMailbox(mailbox) ? MailboxForm!.EditLeaseToken : string.Empty;

    public bool IsEditingDefaultMailbox =>
        ActiveDefaultForm is { EditLeaseToken.Length: > 0 };

    public Guid DefaultEditingMailboxId =>
        ActiveDefaultForm is { } input
        && TryParseMailboxSelection(input.SelectedMailbox, out var mailboxId, out _)
            ? mailboxId
            : Guid.Empty;

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

    public long? VerifiedSendLimitFor(ApprovedMailbox mailbox) =>
        MailboxForm is { ExpectedVersion: > 0 } input && input.MailboxId == mailbox.Id
            ? input.VerifiedEncodedMessageSizeLimit
            : mailbox.VerifiedEncodedMessageSizeLimit;

    public IReadOnlyList<ApprovedMailbox> EligibleDefaultStaffSendMailboxes =>
        Mailboxes.Where(IsEligibleDefaultStaffSendMailbox).ToArray();

    public string DefaultMailboxSelectionFor(ApprovedMailbox mailbox) =>
        $"{mailbox.Id:D}|{mailbox.Version}";

    public bool IsDefaultMailboxSelection(ApprovedMailbox mailbox) =>
        ActiveDefaultForm is { } input
            ? string.Equals(
                input.SelectedMailbox,
                DefaultMailboxSelectionFor(mailbox),
                StringComparison.Ordinal)
            : mailbox.IsDefaultStaffSend;

    public bool DefaultMailboxPromptIsSelected =>
        ActiveDefaultForm is { } input
            ? !EligibleDefaultStaffSendMailboxes.Any(mailbox =>
                string.Equals(
                    input.SelectedMailbox,
                    DefaultMailboxSelectionFor(mailbox),
                    StringComparison.Ordinal))
            : !Mailboxes.Any(mailbox => mailbox.IsDefaultStaffSend);

    public string? StaleDefaultMailboxSelectionAddress
    {
        get
        {
            if (ActiveDefaultForm is not { } input
                || !DefaultMailboxPromptIsSelected
                || !TryParseMailboxSelection(input.SelectedMailbox, out var mailboxId, out _))
            {
                return null;
            }

            return Mailboxes.SingleOrDefault(mailbox => mailbox.Id == mailboxId)?.Address;
        }
    }

    public string DefaultMailboxOperationKey =>
        DefaultMailboxForm?.OperationKey ?? NewOperationKey();

    public string NewAddress =>
        MailboxForm is { ExpectedVersion: 0 } input ? input.Address : string.Empty;

    public long? NewVerifiedSendLimit =>
        MailboxForm is { ExpectedVersion: 0 } input ? input.VerifiedEncodedMessageSizeLimit : null;

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

    public bool IsEditingCategory(ApprovedOutlookCategory category) =>
        CategoryForm is { ExpectedVersion: > 0 } input
        && input.CategoryId == category.Id
        && !string.IsNullOrWhiteSpace(input.EditLeaseToken);

    public string CategoryEditLeaseTokenFor(ApprovedOutlookCategory category) =>
        IsEditingCategory(category) ? CategoryForm!.EditLeaseToken : string.Empty;

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

    private async Task<string> EditConflictMessageAsync(
        EditScopeKind scopeKind,
        Guid recordId,
        ActionActor actor,
        string recordName,
        CancellationToken cancellationToken)
    {
        var active = await editScopes.GetActiveAsync(scopeKind, recordId, actor, cancellationToken);
        if (active is null)
        {
            return $"Another member of staff is editing this {recordName}. Reload to try again.";
        }

        var isSelf = EditScopeAuthority.IsHolder(active.HolderKind, active.Holder, actor);
        var holder = isSelf
            ? CaseEditAuthorityHolder.Unnamed
            : await describeEditAuthorityHolder.ExecuteAsync(
                active.HolderKind,
                active.Holder,
                actor,
                cancellationToken);
        return EditModeDisplay.HeldBy(recordName, holder, isSelf);
    }

    /// <summary>The records as an operator reading an ownership sentence names them.</summary>
    private const string MailboxRecordName = "mailbox policy";

    private const string CategoryRecordName = "Outlook category";

    /// <summary>
    /// The release a leaving page beacons. It is not an operator action: it
    /// answers 204 whether or not a scope was still there to release, so a
    /// duplicate beacon and a beacon that lost a race with Cancel are both
    /// ordinary outcomes. Antiforgery is validated as it is for every post.
    /// </summary>
    public Task<IActionResult> OnPostReleaseMailboxScopeBeaconAsync(
        Guid mailboxId,
        string? editLeaseToken,
        CancellationToken cancellationToken) =>
        ReleaseScopeBeaconAsync(
            EditScopeKind.ApprovedMailbox,
            mailboxId,
            editLeaseToken,
            "approved-mailbox-edit-beacon",
            cancellationToken);

    public Task<IActionResult> OnPostReleaseCategoryScopeBeaconAsync(
        Guid categoryId,
        string? editLeaseToken,
        CancellationToken cancellationToken) =>
        ReleaseScopeBeaconAsync(
            EditScopeKind.ApprovedOutlookCategory,
            categoryId,
            editLeaseToken,
            "outlook-category-edit-beacon",
            cancellationToken);

    private async Task<IActionResult> ReleaseScopeBeaconAsync(
        EditScopeKind scopeKind,
        Guid recordId,
        string? editLeaseToken,
        string operationName,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (recordId == Guid.Empty || string.IsNullOrWhiteSpace(editLeaseToken))
        {
            return new NoContentResult();
        }

        try
        {
            await editScopes.ReleaseAsync(
                new(scopeKind, recordId, actor, $"{operationName}:{Guid.NewGuid():N}", editLeaseToken),
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

    private async Task<IActionResult> HeartbeatEditAsync(
        EditScopeKind scopeKind,
        Guid recordId,
        string? editLeaseToken,
        string recordName,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (recordId == Guid.Empty || string.IsNullOrWhiteSpace(editLeaseToken))
        {
            return new ConflictObjectResult($"Editing this {recordName} has ended. Reload it before making further changes.");
        }

        try
        {
            await editScopes.HeartbeatAsync(
                new(scopeKind, recordId, actor, editLeaseToken), cancellationToken);
            return new OkResult();
        }
        catch (EditScopeExpiredException)
        {
            return new ConflictObjectResult($"Editing this {recordName} has ended. Reload it before making further changes.");
        }
    }

    private async Task LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        AutomationComposed =
            HttpContext.RequestServices.GetService<AutomationClientRegistry>() is not null;
        Mailboxes = await listApprovedMailboxes.ExecuteAsync(actor, cancellationToken);
        PollStatuses = await pollStatusQueries.ListAsync(cancellationToken);
        Subscriptions = await subscriptionStore.ListAsync(cancellationToken);
        Categories = await listCategories.ExecuteAsync(actor, cancellationToken);
    }

    private void PrepareFormState()
    {
        if (MailboxForm is { ExpectedVersion: > 0 } mailboxInput)
        {
            var current = Mailboxes.SingleOrDefault(item => item.Id == mailboxInput.MailboxId);
            if (current is not null)
            {
                mailboxInput.ExpectedVersion = current.Version;
            }
            mailboxInput.OperationKey = NewOperationKey();
        }
        if (DefaultMailboxForm is { } defaultMailboxInput)
        {
            defaultMailboxInput.OperationKey = NewOperationKey();
        }
        if (CategoryForm is { ExpectedVersion: > 0 } categoryInput)
        {
            var current = Categories.SingleOrDefault(item => item.Id == categoryInput.CategoryId);
            if (current is not null)
            {
                categoryInput.ExpectedVersion = current.Version;
            }
            categoryInput.OperationKey = NewOperationKey();
        }

        NewMailboxId = MailboxForm is { ExpectedVersion: 0 } newMailbox
            && newMailbox.MailboxId != Guid.Empty
            ? newMailbox.MailboxId
            : Guid.NewGuid();
        NewMailboxOperationKey = NewOperationKey();
        NewCategoryId = CategoryForm is { ExpectedVersion: 0 } newCategory
            && newCategory.CategoryId != Guid.Empty
            ? newCategory.CategoryId
            : Guid.NewGuid();
        NewCategoryOperationKey = NewOperationKey();
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
                "Your change was not applied; review the current row and retry.",
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
            ApprovedMailboxUpdateError.MissingVerifiedSendLimit =>
                "Record the verified encoded-message size limit before enabling staff send.",
            ApprovedMailboxUpdateError.DefaultStaffSendMailboxIneligible =>
                "Select an approved, active staff-send mailbox with a verified send limit.",
            ApprovedMailboxUpdateError.DefaultStaffSendMailboxRequiresReplacement =>
                "Select another default Compose sender before disabling this mailbox or removing staff send.",
            _ => "The approved-mailbox change was not accepted."
        };

    private static string CategoryErrorMessage(
        ApprovedOutlookCategoryUpdateException exception) => exception.Error switch
    {
        ApprovedOutlookCategoryUpdateError.DuplicateDisplayName =>
            "That display name is already configured.",
        ApprovedOutlookCategoryUpdateError.VersionConflict =>
            "The category policy changed. Review it and retry.",
        ApprovedOutlookCategoryUpdateError.OperationConflict =>
            "This form was already used for another change. Review and retry.",
        ApprovedOutlookCategoryUpdateError.NotFound =>
            "The category policy no longer exists.",
        _ => "The category policy was not saved."
    };

    private static bool IsEligibleDefaultStaffSendMailbox(ApprovedMailbox mailbox) =>
        mailbox.State == ApprovedMailboxState.Approved
        && mailbox.RouteScopes.Contains(ApprovedMailboxRouteScope.StaffSend)
        && mailbox.RouteScopes.Contains(ApprovedMailboxRouteScope.SentEvidence)
        && mailbox.ActivatedAtUtc is not null
        && mailbox.MailboxIdentity is not null
        && mailbox.SentFolderIdentity is not null
        && mailbox.Generation > 0
        && mailbox.VerifiedEncodedMessageSizeLimit > 0;

    private static bool TryParseMailboxSelection(
        string selection,
        out Guid mailboxId,
        out int expectedVersion)
    {
        mailboxId = Guid.Empty;
        expectedVersion = 0;
        var values = selection.Split('|', StringSplitOptions.TrimEntries);
        return values.Length == 2
            && Guid.TryParse(values[0], out mailboxId)
            && mailboxId != Guid.Empty
            && int.TryParse(values[1], out expectedVersion)
            && expectedVersion > 0;
    }

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

        public string EditLeaseToken { get; set; } = string.Empty;

        [Range(1, long.MaxValue)]
        public long? VerifiedEncodedMessageSizeLimit { get; set; }
    }

    [ValidateNever]
    public sealed class DefaultMailboxFormInput
    {
        [Required]
        public string SelectedMailbox { get; set; } = string.Empty;

        public Guid? ExpectedPreviousDefaultMailboxId { get; set; }

        public int? ExpectedPreviousDefaultMailboxVersion { get; set; }

        public string OperationKey { get; set; } = string.Empty;

        public string EditLeaseToken { get; set; } = string.Empty;
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

        public string EditLeaseToken { get; set; } = string.Empty;
    }
}
