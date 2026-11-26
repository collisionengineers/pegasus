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
                ModelState.AddModelError(string.Empty, OperatorLabels.Integrations.Required(field));
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

        try
        {
            await logins.ReplaceAsync(actor, target, version, values, OperationKey, cancellationToken);
            StatusMessage = OperatorLabels.Integrations.Saved;
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException or ArgumentException)
        {
            // Another Administrator replaced the login since the page was
            // drawn, or Core refused the values.
            ErrorMessage = OperatorLabels.Integrations.NotAccepted;
        }
        catch (Exception exception) when (exception is not OperationCanceledException
            and not StaffAuthorizationException)
        {
            // The replacement is one transaction, so a failure changed nothing.
            // The log names the login, never a value.
            LogReplaceFailed(logger, target, exception);
            ErrorMessage = OperatorLabels.Integrations.NotAccepted;
        }

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
}
