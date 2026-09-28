using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Web.Mcp;
using Pegasus.Web.Presentation;
using Labels = Pegasus.Web.Presentation.OperatorLabels.EmailTemplates;

namespace Pegasus.Web.Pages.Administration.EmailTemplates;

/// <summary>
/// E-mail templates (FRD-17): one row per template, with who last changed it
/// and when. Edit opens its body in a dialog; Save posts on the click with the
/// rendered version. A refused save reopens the dialog with the posted body.
/// </summary>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel(
    GetEmailTemplate getTemplate,
    UpdateEmailTemplate updateTemplate,
    IStaffAccountQueries staffAccounts) : AdministrationPageModel
{
    private IReadOnlyDictionary<Guid, string> _staffNames = new Dictionary<Guid, string>();

    public IReadOnlyList<EmailTemplate> Templates { get; private set; } = [];

    public bool AutomationComposed { get; private set; }

    /// <summary>The template whose save was refused; its dialog reopens with what was posted.</summary>
    public EmailTemplatePurpose? RefusedPurpose { get; private set; }

    public string RefusedBody { get; private set; } = string.Empty;

    public long RefusedVersion { get; private set; }

    /// <summary>Who last saved the template; a built-in body has no change to name.</summary>
    public string ChangedBy(EmailTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return template.UpdatedBy is { } subject
            ? ActorDisplayNames.Resolve(ActorKind.Staff, subject, _staffNames)
            : OperatorLabels.CaseWorkspace.AbsentValue;
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(
        EmailTemplatePurpose purpose,
        long expectedVersion,
        string? body,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (!Enum.IsDefined(purpose))
        {
            return NotFound();
        }

        try
        {
            if (!IsOperationKeyValid(operationKey))
            {
                ModelState.AddModelError(nameof(operationKey), Labels.Expired);
            }
            else
            {
                await updateTemplate.ExecuteAsync(
                    new UpdateEmailTemplateRequest(purpose, body ?? string.Empty, expectedVersion, actor, operationKey!),
                    cancellationToken);
                TempData["Confirmation"] = Labels.Saved;
                return RedirectToPage();
            }
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (UnknownEmailTemplatePlaceholderException exception)
        {
            ModelState.AddModelError(nameof(body), Labels.UnknownPlaceholder(exception.Placeholder));
        }
        catch (ArgumentOutOfRangeException)
        {
            ModelState.AddModelError(nameof(body), Labels.TooLong);
        }
        catch (ArgumentException)
        {
            ModelState.AddModelError(nameof(body), Labels.BodyRequired);
        }
        catch (EmailTemplateVersionConflictException)
        {
            ModelState.AddModelError(string.Empty, Labels.Stale);
        }
        catch (EmailTemplateOperationConflictException)
        {
            ModelState.AddModelError(string.Empty, Labels.OperationConflict);
        }

        RefusedPurpose = purpose;
        RefusedBody = body ?? string.Empty;
        RefusedVersion = expectedVersion;
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    private async Task LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        AutomationComposed = HttpContext.RequestServices.GetService<AutomationClientRegistry>() is not null;
        var templates = new List<EmailTemplate>();
        foreach (var purpose in Enum.GetValues<EmailTemplatePurpose>())
        {
            templates.Add(await getTemplate.ExecuteAsync(actor, purpose, cancellationToken));
        }

        Templates = templates;
        _staffNames = await ActorDisplayNames.ResolveStaffNamesAsync(
            staffAccounts,
            templates
                .Select(template => Guid.TryParse(template.UpdatedBy, out var id) ? id : Guid.Empty),
            cancellationToken);
    }
}
