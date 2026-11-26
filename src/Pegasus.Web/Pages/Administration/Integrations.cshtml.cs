using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Administration;

/// <summary>
/// The Integrations administration page (operator, 26 November 2026; R1289):
/// the automatic requests panel, whose one Save drives the five switches a new
/// Case reads (R546, R1286, R1287), and one panel per integration login
/// (R1288), showing the account and when it was replaced, with fields that are
/// only ever typed, never shown back.
/// </summary>
/// <remarks>
/// The page is every deployment's. A login's panel is drawn only where this
/// host composes its integration: a capability the host does not carry is
/// absent, never an inert panel.
/// </remarks>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed partial class IntegrationsModel(
    IAutomaticRequestControl automaticRequests,
    IIntegrationLoginAdministration logins,
    IFetchGuideValuation guideValuations,
    RunVehicleHistoryCheck vehicleHistoryCheck,
    ILogger<IntegrationsModel> logger) : AdministrationPageModel
{
    /// <summary>The automatic request switches, in the panel's order.</summary>
    private static readonly AutomaticRequestKind[] AutomaticKinds =
    [
        AutomaticRequestKind.GlassesValuation,
        AutomaticRequestKind.BregoValuation,
        AutomaticRequestKind.CazanaValuation,
        AutomaticRequestKind.CapValuation,
        AutomaticRequestKind.VehicleHistoryCheck,
    ];

    /// <summary>The login panels, in the page's order.</summary>
    private static readonly IntegrationLogin[] LoginOrder =
    [
        IntegrationLogin.GlassValuation,
        IntegrationLogin.Brego,
        IntegrationLogin.Cazana,
        IntegrationLogin.ExperianHistory,
        IntegrationLogin.ExperianCap,
    ];

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    [BindProperty]
    public bool GlassesValuationEnabled { get; set; }

    [BindProperty]
    public bool BregoValuationEnabled { get; set; }

    [BindProperty]
    public bool CazanaValuationEnabled { get; set; }

    [BindProperty]
    public bool CapValuationEnabled { get; set; }

    [BindProperty]
    public bool VehicleHistoryCheckEnabled { get; set; }

    /// <summary>The logins whose integration this host composes, in the page's order.</summary>
    public IReadOnlyList<IntegrationLoginStatus> Logins { get; private set; } = [];

    /// <summary>
    /// Whether the Brego panel offers its sign-in steps: the host composes the
    /// link and the account's e-mail is saved (R1290). An action that cannot
    /// work is absent.
    /// </summary>
    public bool BregoLinkOffered { get; private set; }

    /// <summary>The Brego sign-in that waits for its six-digit code, while one does.</summary>
    public BregoPendingCode? BregoPending { get; private set; }

    /// <summary>
    /// Whether the Brego panel offers Refresh login: the steps are offered
    /// and this host reads Brego's sign-in e-mail from the account's mailbox
    /// (R1294).
    /// </summary>
    public bool BregoRefreshOffered { get; private set; }

    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string? ErrorMessage { get; set; }

    // A refused login form is drawn again with the version it was posted with
    // and the account name typed into it; nothing else typed is written back.
    private IntegrationLogin? _refusedLogin;
    private long _refusedExpectedVersion;
    private string? _refusedAccountName;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    /// <summary>
    /// The automatic requests panel's one Save: each switch is written only
    /// when its checkbox differs from the stored state, so a save that changes
    /// nothing writes no switch history.
    /// </summary>
    public async Task<IActionResult> OnPostSaveAutomaticAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        if (!IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(string.Empty, OperatorLabels.Integrations.Expired);
            OperationKey = NewOperationKey();
            await LoadAsync(actor, cancellationToken);
            return Page();
        }

        var stored = await automaticRequests.ReadAsync(cancellationToken);
        foreach (var kind in AutomaticKinds)
        {
            var posted = Posted(kind);
            if (stored[kind] != posted)
            {
                await automaticRequests.SetEnabledAsync(
                    kind,
                    posted,
                    actor,
                    reason: null,
                    OperationKey,
                    cancellationToken);
            }
        }

        StatusMessage = OperatorLabels.Integrations.AutomaticSaved;
        return RedirectToPage();
    }

    /// <summary>
    /// Replaces one login with the values typed into its panel, at the version
    /// the panel was drawn with.
    /// </summary>
    public async Task<IActionResult> OnPostSaveLoginAsync(
        string? login,
        string? expectedVersion,
        string? username,
        string? email,
        string? password,
        string? clientId,
        string? clientSecret,
        CancellationToken cancellationToken)
    {
        // The submitted secrets are read once, here, and removed before any
        // redisplay can reach the model state they would otherwise sit in.
        ModelState.Remove(nameof(password));
        ModelState.Remove(nameof(clientSecret));
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        if (!Enum.TryParse<IntegrationLogin>(login, ignoreCase: false, out var target)
            || !Enum.IsDefined(target)
            || !long.TryParse(expectedVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
        {
            ErrorMessage = OperatorLabels.Integrations.NotAccepted;
            return RedirectToPage();
        }

        if (!IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(string.Empty, OperatorLabels.Integrations.Expired);
            OperationKey = NewOperationKey();
        }

        var values = new Dictionary<IntegrationLoginField, string>();
        foreach (var field in IntegrationLoginFields.Of(target))
        {
            var value = field switch
            {
                IntegrationLoginField.Username => username,
                IntegrationLoginField.Email => email,
                IntegrationLoginField.Password => password,
                IntegrationLoginField.ClientId => clientId,
                IntegrationLoginField.ClientSecret => clientSecret,
                _ => null,
            };
            if (string.IsNullOrWhiteSpace(value))
            {
                // An optional value left blank is not stored (Brego's
                // password, R1290).
                if (IntegrationLoginFields.IsRequired(target, field))
                {
                    ModelState.AddModelError(string.Empty, OperatorLabels.Integrations.Required(field));
                }
            }
            else
            {
                values[field] = value;
            }
        }

        if (!ModelState.IsValid)
        {
            _refusedLogin = target;
            _refusedExpectedVersion = version;
            _refusedAccountName = IntegrationLoginFields.AccountNameField(target) == IntegrationLoginField.Email
                ? email?.Trim()
                : username?.Trim();
            await LoadAsync(actor, cancellationToken);
            return Page();
        }

        IntegrationLoginStatus replaced;
        try
        {
            replaced = await logins.ReplaceAsync(actor, target, version, values, OperationKey, cancellationToken);
            StatusMessage = OperatorLabels.Integrations.Saved;
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException or ArgumentException)
        {
            // Another Administrator replaced the login since the page was
            // drawn, or Core refused the values.
            ErrorMessage = OperatorLabels.Integrations.NotAccepted;
            return RedirectToPage();
        }
        catch (Exception exception) when (exception is not OperationCanceledException
            and not StaffAuthorizationException)
        {
            // The replacement is one transaction, so a failure changed nothing.
            // The log names the login, never a value.
            LogReplaceFailed(logger, target, exception);
            ErrorMessage = OperatorLabels.Integrations.NotAccepted;
            return RedirectToPage();
        }

        // A Brego account saved with its password signs in with it at once
        // (R1290); one saved without signs in from Brego's e-mail where this
        // host reads the account's mailbox (R1294). Either runs at the version
        // the save left, and the credential stays saved whatever Brego answers.
        if (target == IntegrationLogin.Brego && BregoLink() is { } link)
        {
            if (values.ContainsKey(IntegrationLoginField.Password))
            {
                StatusMessage = null;
                await RunBregoStepAsync(
                    replaced.AccountName ?? string.Empty,
                    passwordSignIn: true,
                    async () => await link.SignInWithPasswordAsync(actor, replaced.Version, OperationKey, cancellationToken));
            }
            else if (link.ReadsSignInMail)
            {
                StatusMessage = null;
                await RunBregoStepAsync(
                    replaced.AccountName ?? string.Empty,
                    passwordSignIn: false,
                    async () => await link.RefreshAsync(actor, replaced.Version, OperationKey, cancellationToken));
            }
        }

        return RedirectToPage();
    }

    /// <summary>
    /// Refresh login: signs the saved account in from the sign-in e-mail Brego
    /// sends to its mailbox, in one press (R1294).
    /// </summary>
    public async Task<IActionResult> OnPostRefreshBregoLoginAsync(
        string? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        if (BregoLinkStepRefused(expectedVersion, out var version) is { } refused)
        {
            ErrorMessage = refused;
            return RedirectToPage();
        }

        var accountLink = BregoLink()!;
        if (!accountLink.ReadsSignInMail)
        {
            ErrorMessage = OperatorLabels.Integrations.NotAccepted;
            return RedirectToPage();
        }

        var email = await BregoAccountNameAsync(actor, cancellationToken);
        await RunBregoStepAsync(
            email,
            passwordSignIn: false,
            async () => await accountLink.RefreshAsync(actor, version, OperationKey, cancellationToken));
        return RedirectToPage();
    }

    /// <summary>Send sign-in link: Brego e-mails a sign-in link to the saved account.</summary>
    public async Task<IActionResult> OnPostSendBregoLinkAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        if (BregoLink() is not { } link || !IsOperationKeyValid(OperationKey))
        {
            ErrorMessage = BregoLink() is null
                ? OperatorLabels.Integrations.NotAccepted
                : OperatorLabels.Integrations.Expired;
            return RedirectToPage();
        }

        var email = await BregoAccountNameAsync(actor, cancellationToken);
        if (await RunBregoStepAsync(email, passwordSignIn: false, async () =>
            {
                await link.SendLinkAsync(actor, OperationKey, cancellationToken);
                return null;
            }))
        {
            StatusMessage = OperatorLabels.Integrations.BregoLinkSent(email);
        }

        return RedirectToPage();
    }

    /// <summary>Link account: redeems the pasted sign-in link for the saved account.</summary>
    public async Task<IActionResult> OnPostLinkBregoAsync(
        string? link,
        string? expectedVersion,
        CancellationToken cancellationToken)
    {
        // The pasted link is read once, here, and never written back.
        ModelState.Remove(nameof(link));
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        if (BregoLinkStepRefused(expectedVersion, out var version) is { } refused)
        {
            ErrorMessage = refused;
            return RedirectToPage();
        }

        if (string.IsNullOrWhiteSpace(link))
        {
            ErrorMessage = OperatorLabels.Integrations.BregoNoToken;
            return RedirectToPage();
        }

        string pasted = link;
        var accountLink = BregoLink()!;
        var email = await BregoAccountNameAsync(actor, cancellationToken);
        await RunBregoStepAsync(
            email,
            passwordSignIn: false,
            async () => await accountLink.LinkAsync(actor, pasted, version, OperationKey, cancellationToken));
        return RedirectToPage();
    }

    /// <summary>Confirm code: enters the six-digit code a waiting sign-in asked for.</summary>
    public async Task<IActionResult> OnPostConfirmBregoCodeAsync(
        string? code,
        string? expectedVersion,
        CancellationToken cancellationToken)
    {
        // The code is read once, here, and never written back.
        ModelState.Remove(nameof(code));
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        if (BregoLinkStepRefused(expectedVersion, out var version) is { } refused)
        {
            ErrorMessage = refused;
            return RedirectToPage();
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            ErrorMessage = OperatorLabels.Integrations.BregoCodeFormat;
            return RedirectToPage();
        }

        string entered = code;
        var accountLink = BregoLink()!;
        var email = await BregoAccountNameAsync(actor, cancellationToken);
        await RunBregoStepAsync(
            email,
            passwordSignIn: false,
            async () => await accountLink.ConfirmCodeAsync(actor, entered, version, OperationKey, cancellationToken));
        return RedirectToPage();
    }

    /// <summary>The form field a login value is posted as.</summary>
    public static string FieldName(IntegrationLoginField field) => field switch
    {
        IntegrationLoginField.Username => "username",
        IntegrationLoginField.Email => "email",
        IntegrationLoginField.Password => "password",
        IntegrationLoginField.ClientId => "clientId",
        IntegrationLoginField.ClientSecret => "clientSecret",
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "The login field is unknown."),
    };

    /// <summary>A password or client secret: typed into a password box, never written back.</summary>
    public static bool IsSecret(IntegrationLoginField field) =>
        field is IntegrationLoginField.Password or IntegrationLoginField.ClientSecret;

    /// <summary>The version a login's form carries: the one it was posted with when it was refused.</summary>
    public long ExpectedVersion(IntegrationLoginStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return _refusedLogin == status.Login ? _refusedExpectedVersion : status.Version;
    }

    /// <summary>
    /// The value a non-secret box opens with: empty, except the account name
    /// typed into a refused form.
    /// </summary>
    public string? FieldValue(IntegrationLogin login, IntegrationLoginField field) =>
        _refusedLogin == login && field == IntegrationLoginFields.AccountNameField(login)
            ? _refusedAccountName
            : null;

    private bool Posted(AutomaticRequestKind kind) => kind switch
    {
        AutomaticRequestKind.GlassesValuation => GlassesValuationEnabled,
        AutomaticRequestKind.BregoValuation => BregoValuationEnabled,
        AutomaticRequestKind.CazanaValuation => CazanaValuationEnabled,
        AutomaticRequestKind.CapValuation => CapValuationEnabled,
        AutomaticRequestKind.VehicleHistoryCheck => VehicleHistoryCheckEnabled,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The automatic request kind is unknown."),
    };

    private async Task LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        // The login forms do not post the checkboxes. Seed them from the
        // stored state; ModelState still wins for a redisplayed automatic
        // requests form.
        var automatic = await automaticRequests.ReadAsync(cancellationToken);
        GlassesValuationEnabled = automatic[AutomaticRequestKind.GlassesValuation];
        BregoValuationEnabled = automatic[AutomaticRequestKind.BregoValuation];
        CazanaValuationEnabled = automatic[AutomaticRequestKind.CazanaValuation];
        CapValuationEnabled = automatic[AutomaticRequestKind.CapValuation];
        VehicleHistoryCheckEnabled = automatic[AutomaticRequestKind.VehicleHistoryCheck];

        var statuses = await logins.ListAsync(actor, cancellationToken);
        Logins =
        [
            .. LoginOrder
                .Where(IsComposed)
                .Select(login => statuses.First(status => status.Login == login))
        ];

        var link = BregoLink();
        var brego = Logins.FirstOrDefault(status => status.Login == IntegrationLogin.Brego);
        BregoLinkOffered = link is not null && !string.IsNullOrWhiteSpace(brego?.AccountName);
        BregoRefreshOffered = BregoLinkOffered && link!.ReadsSignInMail;
        BregoPending = BregoLinkOffered
            ? await link!.ReadPendingAsync(actor, cancellationToken)
            : null;
    }

    /// <summary>The Brego sign-in steps, where this host composes Brego; null elsewhere.</summary>
    private IBregoAccountLink? BregoLink() =>
        HttpContext.RequestServices.GetService<IBregoAccountLink>();

    /// <summary>The saved Brego account's e-mail address, which the step sentences name.</summary>
    private async Task<string> BregoAccountNameAsync(ActionActor actor, CancellationToken cancellationToken) =>
        (await logins.ListAsync(actor, cancellationToken))
            .FirstOrDefault(status => status.Login == IntegrationLogin.Brego)?.AccountName ?? string.Empty;

    /// <summary>
    /// The refusal of a link or code step before Brego is asked: no link on
    /// this host, an expired form or an unreadable version; null when the step
    /// may run.
    /// </summary>
    private string? BregoLinkStepRefused(string? expectedVersion, out long version)
    {
        version = 0;
        if (BregoLink() is null
            || !long.TryParse(expectedVersion, NumberStyles.None, CultureInfo.InvariantCulture, out version))
        {
            return OperatorLabels.Integrations.NotAccepted;
        }

        return IsOperationKeyValid(OperationKey) ? null : OperatorLabels.Integrations.Expired;
    }

    /// <summary>
    /// Runs one Brego sign-in step and states its outcome: Linked as linked, a
    /// code asked for by the code step the page then shows, a refusal in its
    /// own sentence and a stale version as not accepted. True when the step
    /// was not refused.
    /// </summary>
    private async Task<bool> RunBregoStepAsync(
        string email,
        bool passwordSignIn,
        Func<Task<BregoLinkStep?>> step)
    {
        try
        {
            if (await step() == BregoLinkStep.Linked)
            {
                StatusMessage = OperatorLabels.Integrations.BregoLinked;
            }

            return true;
        }
        catch (BregoLinkRefusedException refusal)
        {
            ErrorMessage = OperatorLabels.Integrations.BregoRefusal(refusal.Refusal, email, passwordSignIn);
        }
        catch (DbUpdateConcurrencyException)
        {
            ErrorMessage = OperatorLabels.Integrations.NotAccepted;
        }
        catch (Exception exception) when (exception is not OperationCanceledException
            and not StaffAuthorizationException)
        {
            // The log names no value: no link, code, password or address.
            LogBregoStepFailed(logger, exception);
            ErrorMessage = OperatorLabels.Integrations.NotAccepted;
        }

        return false;
    }

    /// <summary>Whether this host composes the integration the login signs in to.</summary>
    private bool IsComposed(IntegrationLogin login) => login switch
    {
        IntegrationLogin.GlassValuation => guideValuations.IsConnected(ValuationSource.Glasses),
        IntegrationLogin.Brego => guideValuations.IsConnected(ValuationSource.Brego),
        IntegrationLogin.Cazana => guideValuations.IsConnected(ValuationSource.Cazana),
        IntegrationLogin.ExperianHistory => vehicleHistoryCheck.IsConnected,
        IntegrationLogin.ExperianCap => guideValuations.IsConnected(ValuationSource.Cap),
        _ => false,
    };

    [LoggerMessage(
        EventId = 1289,
        Level = LogLevel.Error,
        Message = "Replacing the {Login} integration login failed and changed nothing.")]
    private static partial void LogReplaceFailed(ILogger logger, IntegrationLogin login, Exception exception);

    [LoggerMessage(
        EventId = 1290,
        Level = LogLevel.Error,
        Message = "A Brego sign-in step on the Integrations page failed.")]
    private static partial void LogBregoStepFailed(ILogger logger, Exception exception);
}
