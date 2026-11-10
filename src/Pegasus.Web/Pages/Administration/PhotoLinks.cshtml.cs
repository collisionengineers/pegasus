using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Pegasus.Core.Identity;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Workflow;
using Pegasus.Web.Pages.PhotoCapture;
using Pegasus.Web.PhotoCapture;
using Pegasus.Web.Presentation;
using Labels = Pegasus.Web.Presentation.OperatorLabels.PhotoCapture;

namespace Pegasus.Web.Pages.Administration;

/// <summary>
/// Administration › Photo links (v50 F to H; operator, 10 November 2026;
/// R911): the one website link for private customers, which Administrators
/// create at the address they type, pause, resume, replace, retire and set
/// the defaults of (R797), and Send a photo link's starting values and
/// lifetimes (R813, R817). Both came from elsewhere: the website link from the
/// Photo capture page, Send's settings from Workflow configuration. Absent,
/// as every photo capture page is, while photo capture is off (FRD-28 Feature
/// off).
/// </summary>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class PhotoLinksModel(
    IStandingPhotoLinkStore standingLinks,
    StandingPhotoLinkAdministration administration,
    GetWorkflowConfiguration getWorkflowConfiguration,
    UpdateWorkflowConfiguration updateWorkflowConfiguration,
    IOptions<PhotoCaptureOptions> options) : AdministrationPageModel
{
    public override Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        if (!ShellRail.PhotoCaptureEnabled(HttpContext.RequestServices.GetRequiredService<IConfiguration>()))
        {
            context.Result = NotFound();
            return Task.CompletedTask;
        }

        return next();
    }

    public CaseWorkflowConfiguration Configuration { get; private set; } = null!;

    /// <summary>The website standing link; null when none exists yet.</summary>
    public StandingPhotoLink? WebsiteLink { get; private set; }

    public string? WebsiteUrl => WebsiteLink is { } link ? PhotoLinkMail.StandingUrl(options.Value, link.Slug) : null;

    public string? WebsiteQrSvg => WebsiteUrl is { } url ? PhotoLinkQr.Svg(url) : null;

    /// <summary>What every standing address starts with, shown before the box the Administrator types the website's into.</summary>
    public string WebsiteAddressPrefix => new Uri(options.Value.PublicOrigin, "s/").AbsoluteUri;

    [BindProperty] public long ExpectedVersion { get; set; }

    /// <summary>The form's operation key: its history event's correlation, so a form sent twice acts once (R846).</summary>
    [BindProperty] public string? OperationKey { get; set; } = NewOperationKey();

    /// <summary>The website link's address as the Administrator typed it, on Create and Replace (R797).</summary>
    [BindProperty] public string? WebsiteSlug { get; set; }

    // The website link's defaults (R797); its mode stays guided.
    [BindProperty] public bool LinkLiveCameraOnly { get; set; }
    [BindProperty] public int LinkLifetimeDays { get; set; }
    [BindProperty] public bool LinkOriginalsInBackground { get; set; }

    // Send a photo link's starting values (R813) and the lifetimes it offers (R817).
    [BindProperty] public long WorkflowExpectedVersion { get; set; }
    [BindProperty] public string WorkflowOperationKey { get; set; } = NewOperationKey();
    [BindProperty] public PhotoCaptureMode PhotoLinkStartMode { get; set; } = PhotoCaptureMode.Guided;
    [BindProperty] public bool PhotoLinkStartLiveCameraOnly { get; set; }
    [BindProperty] public bool PhotoLinkStartOriginals { get; set; }
    [BindProperty] public int[] PhotoLinkLifetimesOffered { get; set; } = [];
    [BindProperty] public int PhotoLinkDefaultLifetimeDays { get; set; } = CaptureLimits.DefaultLifetimeDays;

    [TempData] public string? Notice { get; set; }

    /// <summary>A refused website link change with no words of its own, shown above the page once.</summary>
    [TempData] public string? Refusal { get; set; }

    /// <summary>A typed website address refused for its shape or because a link holds it, beside its box (R868).</summary>
    [TempData] public string? WebsiteAddressRefusal { get; set; }

    /// <summary>Whether a refused address belongs in Replace link's dialog, which then opens on load.</summary>
    public bool ReplaceRefused => WebsiteAddressRefusal is not null && WebsiteLink is not null;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor) || !MayManageLinks(actor)) return Forbid();
        await LoadAsync(actor, cancellationToken);
        DrawSendSettings();
        return Page();
    }

    public async Task<IActionResult> OnPostSaveSendSettingsAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor) || !MayManageLinks(actor)) return Forbid();
        ModelState.Remove(nameof(OperationKey));
        await LoadAsync(actor, cancellationToken);
        if (WorkflowExpectedVersion < 1 || WorkflowExpectedVersion > int.MaxValue)
            ModelState.AddModelError(string.Empty, "The settings version is invalid. Reload and try again.");
        if (!IsOperationKeyValid(WorkflowOperationKey))
            ModelState.AddModelError(string.Empty, "The form has expired. Reload and try again.");
        if (!ModelState.IsValid) return Page();

        try
        {
            // Every other workflow setting is saved as stored.
            await updateWorkflowConfiguration.ExecuteAsync(
                new(checked((int)WorkflowExpectedVersion), actor, WorkflowOperationKey)
                {
                    RequireInstructions = Configuration.RequireInstructions,
                    RequireImages = Configuration.RequireImages,
                    ChaseIntervalDays = Configuration.ChaseIntervalDays,
                    UnidentifiedTargetDays = Configuration.UnidentifiedTargetDays,
                    TriageTargetDays = Configuration.TriageTargetDays,
                    HeldTargetDays = Configuration.HeldTargetDays,
                    ReviewTargetDays = Configuration.ReviewTargetDays,
                    AiDraftTargetDays = Configuration.AiDraftTargetDays,
                    PhotoLinkStartMode = PhotoLinkStartMode,
                    PhotoLinkStartLiveCameraOnly = PhotoLinkStartLiveCameraOnly,
                    PhotoLinkStartOriginals = PhotoLinkStartOriginals,
                    PhotoLinkLifetimesOffered = PhotoLinkLifetimesOffered,
                    PhotoLinkDefaultLifetimeDays = PhotoLinkDefaultLifetimeDays
                },
                cancellationToken);
            TempData["Confirmation"] = OperatorLabels.WorkflowConfiguration.Saved;
            return RedirectToPage();
        }
        catch (WorkflowConfigurationVersionConflictException)
        {
            ModelState.AddModelError(string.Empty, OperatorLabels.WorkflowConfiguration.Changed);
        }
        catch (WorkflowConfigurationOperationConflictException)
        {
            ModelState.AddModelError(string.Empty, OperatorLabels.WorkflowConfiguration.FormUsed);
        }
        catch (PhotoLinkSendSettingsRefusedException exception)
        {
            // Each refused rule is said beside its own setting (R868); anything
            // else Core refuses has no words of its own.
            switch (exception.Rule)
            {
                case PhotoLinkSendSettingsRule.NoLifetimeOffered:
                    ModelState.AddModelError(nameof(PhotoLinkLifetimesOffered), Labels.OfferAtLeastOneLifetime);
                    break;
                case PhotoLinkSendSettingsRule.StartingLifetimeNotOffered:
                    ModelState.AddModelError(nameof(PhotoLinkDefaultLifetimeDays), Labels.StartingLifetimeNotOffered);
                    break;
                default:
                    ModelState.AddModelError(string.Empty, OperatorLabels.ActionNotApplied);
                    break;
            }
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }

        return Page();
    }

    public Task<IActionResult> OnPostCreateWebsiteLinkAsync(CancellationToken cancellationToken) =>
        ChangeWebsiteLinkAsync("create", cancellationToken);

    public Task<IActionResult> OnPostPauseWebsiteLinkAsync(CancellationToken cancellationToken) =>
        ChangeWebsiteLinkAsync("pause", cancellationToken);

    public Task<IActionResult> OnPostResumeWebsiteLinkAsync(CancellationToken cancellationToken) =>
        ChangeWebsiteLinkAsync("resume", cancellationToken);

    public Task<IActionResult> OnPostReplaceWebsiteLinkAsync(CancellationToken cancellationToken) =>
        ChangeWebsiteLinkAsync("replace", cancellationToken);

    public Task<IActionResult> OnPostRetireWebsiteLinkAsync(CancellationToken cancellationToken) =>
        ChangeWebsiteLinkAsync("retire", cancellationToken);

    public Task<IActionResult> OnPostUpdateWebsiteLinkDefaultsAsync(CancellationToken cancellationToken) =>
        ChangeWebsiteLinkAsync("defaults", cancellationToken);

    /// <summary>The website link's QR code as a file to print.</summary>
    public async Task<IActionResult> OnGetWebsiteQrAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor) || !MayManageLinks(actor)) return Forbid();
        var link = await standingLinks.FindWebsiteAsync(cancellationToken);
        if (link is null) return NotFound();
        return File(
            Encoding.UTF8.GetBytes(PhotoLinkQr.Svg(PhotoLinkMail.StandingUrl(options.Value, link.Slug))),
            "image/svg+xml",
            PhotoLinkQr.FileName(link.Slug));
    }

    // The change is Core's (StandingPhotoLinkAdministration); a change made
    // over a stale read comes back as the page's refusal notice, and a typed
    // address refused comes back beside its box (R868).
    private async Task<IActionResult> ChangeWebsiteLinkAsync(string action, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor) || !MayManageLinks(actor)) return Forbid();

        // The form's own key is a fresh GUID (R846); anything else is not the page's post.
        if (!IsOperationKeyValid(OperationKey)) return BadRequest();

        var operationKey = OperationKey!;
        var link = await standingLinks.FindWebsiteAsync(cancellationToken);
        if (action != "create" && link is null) return BadRequest();

        try
        {
            switch (action)
            {
                case "create":
                    await administration.CreateWebsiteAsync(actor, operationKey, WebsiteSlug, cancellationToken);
                    break;
                case "pause":
                    await administration.PauseAsync(actor, operationKey, link!.Id, ExpectedVersion, cancellationToken);
                    break;
                case "resume":
                    await administration.ResumeAsync(actor, operationKey, link!.Id, ExpectedVersion, cancellationToken);
                    break;
                case "replace":
                    await administration.ReplaceAsync(actor, operationKey, link!.Id, ExpectedVersion, WebsiteSlug, cancellationToken);
                    break;
                case "retire":
                    await administration.RetireAsync(actor, operationKey, link!.Id, ExpectedVersion, cancellationToken);
                    break;
                case "defaults":
                    await administration.UpdateDefaultsAsync(
                        actor,
                        operationKey,
                        link!.Id,
                        ExpectedVersion,
                        link.ForwardedMode,
                        LinkLiveCameraOnly,
                        LinkLifetimeDays,
                        LinkOriginalsInBackground,
                        cancellationToken);
                    Notice = Labels.Saved;
                    break;
                default:
                    return BadRequest();
            }
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (StandingPhotoLinkException exception) when (exception.Error == StandingPhotoLinkError.AlreadyExists)
        {
            // The link is there, whoever made it: the panel shows it.
        }
        catch (StandingPhotoLinkException exception) when (exception.Error == StandingPhotoLinkError.NotFound)
        {
            return NotFound();
        }
        catch (StandingPhotoLinkException exception) when (exception.Error == StandingPhotoLinkError.InvalidSlug)
        {
            WebsiteAddressRefusal = Labels.WebsiteAddressShape;
        }
        catch (StandingPhotoLinkException exception) when (exception.Error == StandingPhotoLinkError.SlugTaken)
        {
            WebsiteAddressRefusal = Labels.WebsiteAddressInUse;
        }
        catch (StandingPhotoLinkException)
        {
            // A stale read, which has no words of its own.
            Refusal = OperatorLabels.ActionNotApplied;
        }

        return RedirectToPage();
    }

    private async Task LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        var configuration = getWorkflowConfiguration.ExecuteAsync(actor, cancellationToken);
        var website = standingLinks.FindWebsiteAsync(cancellationToken);
        await Task.WhenAll(configuration, website);
        Configuration = await configuration;
        WebsiteLink = await website;
    }

    private void DrawSendSettings()
    {
        WorkflowExpectedVersion = Configuration.PolicyVersion;
        WorkflowOperationKey = NewOperationKey();
        PhotoLinkStartMode = Configuration.PhotoLinkStartMode;
        PhotoLinkStartLiveCameraOnly = Configuration.PhotoLinkStartLiveCameraOnly;
        PhotoLinkStartOriginals = Configuration.PhotoLinkStartOriginals;
        PhotoLinkLifetimesOffered = [.. Configuration.PhotoLinkLifetimesOffered];
        PhotoLinkDefaultLifetimeDays = Configuration.PhotoLinkDefaultLifetimeDays;
    }

    private static bool MayManageLinks(ActionActor actor) =>
        StaffAuthorization.IsAuthorized(actor, StaffAccessRight.ManageOrganizationsAndPrincipals);
}
