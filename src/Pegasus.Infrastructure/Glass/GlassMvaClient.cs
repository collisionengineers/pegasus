using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Pegasus.Core;
using Pegasus.Core.Assessment;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// Why one Glass's stage stopped. The code is the whole operator-facing
/// record: it is written to an estimate session's <c>LastError</c>, or to the
/// host log when Get valuation could not answer, and never carries a
/// credential, a cookie, a CSRF token, the <c>ere_session</c> or any part of a
/// provider URL.
/// </summary>
/// <remarks>
/// <paramref name="outcomeUnknown"/> separates "this did not happen" from "we
/// cannot tell whether this happened". Only the second kind may have left
/// server-side state behind at Glass's, and it is never retried or replaced
/// automatically.
/// </remarks>
internal sealed class GlassMvaStageException(
    string failureCode, bool outcomeUnknown = false, string? detail = null)
    : Exception(detail is null ? failureCode : $"{failureCode} {detail}")
{
    public string FailureCode { get; } = failureCode;

    public bool OutcomeUnknown { get; } = outcomeUnknown;

    /// <summary>
    /// What the provider's own answer said, in numbers and flags only, for
    /// the host log, or the export reader's reason after
    /// <see cref="GlassReaderReason"/> has cut it to position numbers, field
    /// names and code values, or <c>regex=timeout</c> when the adapter's own
    /// read budget ran out. Never a registration, a body, a token or a URL.
    /// </summary>
    public string? Detail { get; } = detail;
}

/// <summary>
/// The one list of Glass's stage failure codes. A code names the stage and what
/// about it refused, so a session's <c>LastError</c> or a valuation's log line
/// is enough to say where it stopped without holding anything the provider
/// handed over.
/// </summary>
internal static class GlassFailure
{
    public const string LoginRequest = "glass.login.request";
    public const string LoginCsrf = "glass.login.csrf";
    public const string LoginRedirect = "glass.login.redirect";
    public const string LoginLanding = "glass.login.landing";
    public const string LookupRequest = "glass.lookup.request";
    public const string LookupNotFound = "glass.lookup.notfound";
    public const string CandidatesRequest = "glass.candidates.request";
    public const string CandidatesRefused = "glass.candidates.refused";
    public const string CandidatesNone = "glass.candidates.none";
    public const string CandidatesAmbiguous = "glass.candidates.ambiguous";
    public const string ValuationRequest = "glass.valuation.request";
    public const string ValuationNotPossible = "glass.valuation.not_possible";
    public const string ValuationVehicleAge = "glass.valuation.vehicle_age";
    public const string ValuationUnreadable = "glass.valuation.unreadable";
    public const string RefreshRequest = "glass.refresh.request";
    public const string ReportRequest = "glass.report.request";
    public const string ReportNone = "glass.report.none";
    public const string ReportAmbiguous = "glass.report.ambiguous";
    public const string ReportOffOrigin = "glass.report.off_origin";
    public const string VehicleRequest = "glass.vehicle.request";
    public const string VehicleIdentity = "glass.vehicle.identity";
    public const string PlaceholderRequest = "glass.placeholder.request";
    public const string PlaceholderRefused = "glass.placeholder.refused";
    public const string PlaceholderId = "glass.placeholder.id";
    public const string PlaceholderIdentity = "glass.placeholder.identity";
    public const string DetailsRequest = "glass.details.request";
    public const string DetailsProfile = "glass.details.profile";
    public const string DetailsIdentity = "glass.details.identity";
    public const string SelectRequest = "glass.select.request";
    public const string SelectCount = "glass.select.count";
    public const string StartRequest = "glass.start.request";
    public const string StartStatus = "glass.start.status";
    public const string StartUrl = "glass.start.url";
    public const string StartCaller = "glass.start.caller";
    public const string RelayRequest = "glass.relay.request";
    public const string RelayShape = "glass.relay.shape";
    public const string RelayEstimate = "glass.relay.ere_id";
    public const string RelayOutcome = "glass.relay.outcome";
    public const string ExportRequest = "glass.export.request";
    public const string ExportNone = "glass.export.none";
    public const string ExportAmbiguous = "glass.export.ambiguous";
    public const string ExportOffOrigin = "glass.export.off_origin";
    public const string DownloadRequest = "glass.download.request";
    public const string DownloadOversize = "glass.download.oversize";
    public const string ExportUnreadable = GlassRepairEstimateSessionPolicy.ExportUnreadableFailureCode;
    public const string ExportEmpty = "glass.export.empty";
    public const string IdentityRegistration = "glass.identity.registration";
    public const string IdentityMileage = "glass.identity.mileage";
    public const string IdentityNatCode = "glass.identity.natcode";
    public const string CallbackNotSaved = "glass.callback.not_saved";
    public const string CallbackExpired = "glass.callback.expired";
    public const string CustodyFailed = "glass.custody.failed";
    public const string TransportFailed = "glass.transport.failed";
    public const string TransportUnknown = "glass.transport.unknown";
    public const string Interrupted = "glass.interrupted";
}

/// <summary>What stage 6's fresh lookup established about the vehicle.</summary>
internal sealed record GlassVehicleLookup(string NatCode, int CandidateOrdinal);

/// <summary>
/// The two figures Glass's valuation page answers: Retail Transacted, which a
/// guide card holds as its retail, and Glass's Trade.
/// </summary>
internal sealed record GlassGuideFigures(decimal Retail, decimal Trade);

/// <summary>
/// What stage 18 started and stage 19 proved about its launch URL. The
/// provider's own callback is kept whole rather than split into the estimate
/// and session it names, because relaying to it is what a completion does and
/// rebuilding that address from its parts would be a second copy of its shape.
/// </summary>
internal sealed record GlassEstimateLaunch(string EreId, Uri OriginalCallback, Uri EstimatorUrl);

/// <summary>
/// The Market Value Assessor transport: every HTTP stage of a Glass's Repair
/// Estimate session and of a Glass's valuation, and nothing else. The session
/// policy — what is persisted, when, and what a failure means to the Case —
/// belongs to <see cref="GlassRepairEstimateGateway"/>; what a valuation
/// answers belongs to <see cref="GlassGuideValuationProvider"/>.
///
/// <para>
/// <b>HTTP 200 is not stage success.</b> Every stage checks the application
/// field the provider actually answers with after it has checked the status,
/// because the live evidence records a lookup that reported failure inside a
/// 200. Each check that fails throws <see cref="GlassMvaStageException"/> with
/// its own code.
/// </para>
///
/// <para>
/// <b>Nothing retries blindly.</b> Only the candidate list is read again,
/// and only when the provider answered readable JSON that reported nothing
/// yet: twice more, 250 ms apart, because the portal's own page reads it
/// after the lookup and a list that is not ready is an empty answer, not a
/// refusal. The plate search itself is never repeated; the portal does not
/// repeat it either. Vehicle creation, inserting a placeholder and starting
/// the estimate change state inside the Glass's account, so a lost answer to
/// any of them is reported as unknown rather than repeated.
/// </para>
///
/// <para>
/// <b>The lookup follows the portal's own rule.</b> The stock search is the
/// lookup: when the account already holds the registration
/// (<c>stockcount &gt; 0</c>) the portal asks the operator and "Continue with
/// New Entry" repeats the search as a fresh one (<c>nostocksearch/1</c>);
/// when it holds nothing the stock search's own answer is the lookup and no
/// fresh search is ever made. An answer without a type number — a negative
/// <c>vrm_lookup</c>, or a <c>natcode</c> that is absent, blank or the JSON
/// <c>false</c> the portal answered for a plate its VRM supplier does not
/// know (2 October 2026) — is the portal's "vehicle details have not been
/// found" and is refused at once. The live evidence for this is the
/// provider's <c>searches.js</c> and the captured answers; the spike only
/// ever ran registrations the account already stocked.
/// </para>
///
/// <para>
/// <b>Cookies are the caller's.</b> The jar is handed in and mutated in place
/// so the gateway can protect it between processes; the handler is configured
/// not to manage cookies, because a pooled handler's container would be shared
/// by every session on the host.
/// </para>
/// </summary>
internal sealed partial class GlassMvaClient(
    HttpClient httpClient,
    GlassRepairEstimateOptions options,
    IDictionary<string, string> cookies,
    TimeProvider timeProvider)
{
    /// <summary>The one delay before a retryable read runs again.</summary>
    private static readonly TimeSpan LookupRetryDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>How many times the candidate list is read before it is refused.</summary>
    private const int CandidateReads = 3;

    /// <summary>
    /// Every parameter the provider's launch URL must carry. The rewrite
    /// replaces one of them and proves the rest arrived unchanged.
    /// </summary>
    private static readonly string[] LaunchParameters =
        ["WorkTime", "inURI", "outURI", "ucode", "scode", "EuComp", "caller"];

    /// <summary>
    /// A page or fragment beyond this is not a Glass's response this adapter
    /// knows how to read, so it is refused unread rather than buffered.
    /// </summary>
    private const int MaximumTextBytes = 4 * 1024 * 1024;

    /// <summary>
    /// How long any one pattern below may take to match. Every pattern is
    /// bounded, so this guards against a stalled host, not against
    /// backtracking: the 100 ms it replaced ran out on the sign-in page's
    /// first read after a restart (issue 1021, 5 October 2026).
    /// </summary>
    private const int MatchTimeoutMilliseconds = 1000;

    /// <summary>The grid every stock and export stage addresses.</summary>
    private const string Grid = "stocklistGrid";

    /// <summary>
    /// The account's "Vehicle Valuation Report – Glass's Values Only" print
    /// template, the one filed as a valuation's evidence (operator, 1 October
    /// 2026).
    /// </summary>
    private const string ValuationReportTemplate = "0";

    /// <summary>
    /// Signs in and proves the session is authenticated (stages 1–4): read the
    /// login page, take its single-use CSRF token, post the form, require a
    /// same-origin redirect rather than a re-rendered login form, and require
    /// the landing page to be the stock list and not the login form again.
    /// </summary>
    public async Task SignInAsync(string username, string password, CancellationToken cancellationToken)
    {
        var login = options.MarketValueAssessor("login/index");
        var page = await TextAsync(
            new HttpRequestMessage(HttpMethod.Get, login), ajax: false, GlassFailure.LoginRequest, cancellationToken);
        var csrf = Matched(() => CsrfToken().Match(page), GlassFailure.LoginCsrf);
        if (!csrf.Success)
        {
            throw new GlassMvaStageException(GlassFailure.LoginCsrf);
        }

        var form = new HttpRequestMessage(HttpMethod.Post, login)
        {
            Content = new FormUrlEncodedContent(
            [
                new("remember_me", "0"),
                new("csrf_token", csrf.Groups[1].Value),
                new("login_name", username),
                new("password", password),
            ]),
        };
        using var posted = await SendAsync(form, ajax: false, cancellationToken);
        if (posted.StatusCode is not (HttpStatusCode.Found or HttpStatusCode.SeeOther)
            || posted.Headers.Location is not { } location
            || !options.IsMarketValueAssessor(Absolute(location, login)))
        {
            // A re-rendered login form, an error page or a redirect anywhere
            // but Glass's own origin: the credential did not sign in, and
            // following an off-origin redirect would carry this session's
            // cookies to whatever host named itself.
            throw new GlassMvaStageException(GlassFailure.LoginRedirect);
        }

        var landing = await TextAsync(
            new HttpRequestMessage(HttpMethod.Get, Absolute(location, login)),
            ajax: false,
            GlassFailure.LoginRequest,
            cancellationToken);
        if (landing.Contains("name=\"Form_Login\"", StringComparison.Ordinal)
            || !landing.Contains(Grid, StringComparison.Ordinal))
        {
            throw new GlassMvaStageException(GlassFailure.LoginLanding);
        }
    }

    /// <summary>
    /// Looks the registration up and establishes its Glass's type number
    /// (stages 5–10). A plate the provider does not know is refused here with
    /// <see cref="GlassFailure.LookupNotFound"/> after one search; nothing in
    /// these stages changes the account.
    /// </summary>
    public async Task<GlassVehicleLookup> LookupAsync(
        string registration, long mileageMiles, CancellationToken cancellationToken)
    {
        // The estimate needs only the vehicle: its valuation page is read
        // because the provider's own flow reads it before the vehicle is
        // created, and its figures are left to Get valuation.
        var (lookup, _) = await ValuationPageAsync(registration, mileageMiles, ValuationMonth(), cancellationToken);
        await RefreshAsync(cancellationToken);
        return lookup;
    }

    /// <summary>
    /// Values the registration in the given month (stages 5–10, as the
    /// estimate's lookup runs them) and reads the two figures off the page.
    /// A page the portal answers as "valuation not possible", or one without
    /// exactly one of each figure, is refused before the refresh, as the
    /// portal itself only refreshes after a page it could show.
    /// </summary>
    public async Task<GlassGuideFigures> ValueAsync(
        string registration, long mileageMiles, DateOnly month, CancellationToken cancellationToken)
    {
        var (_, page) = await ValuationPageAsync(registration, mileageMiles, Month(month), cancellationToken);
        var figures = GuideFigures(page);
        await RefreshAsync(cancellationToken);
        return figures;
    }

    /// <summary>
    /// Creates the vehicle inside the Glass's account (stage 11). This is the
    /// first stage that changes the provider's own state, so it is never
    /// retried: a lost answer is reported as unknown and reconciled by a
    /// person.
    /// </summary>
    public Task<string> CreateVehicleAsync(
        string registration, long mileageMiles, CancellationToken cancellationToken) =>
        CreateVehicleForAsync(registration, mileageMiles, ValuationMonth(), cancellationToken);

    /// <summary>
    /// Saves a valued vehicle to the account's stock list in the month it was
    /// valued, as the portal does after every valuation in insurance mode.
    /// </summary>
    public Task<string> CreateVehicleAsync(
        string registration, long mileageMiles, DateOnly month, CancellationToken cancellationToken) =>
        CreateVehicleForAsync(registration, mileageMiles, Month(month), cancellationToken);

    /// <summary>
    /// The address of the account's valuation report for one stocked vehicle:
    /// the print the portal produces on demand, answered as a page carrying
    /// exactly one download link on Glass's own origin.
    /// </summary>
    public async Task<Uri> ValuationReportAsync(string vehicleId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(vehicleId) || !vehicleId.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("A Glass's stock vehicle id is required.", nameof(vehicleId));
        }

        var answer = await TextAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                options.MarketValueAssessor(
                    $"pdf-print/storess/template/{ValuationReportTemplate}/printaction/vehicle-valuation/vehicles/{vehicleId}")),
            ajax: true,
            GlassFailure.ReportRequest,
            cancellationToken);
        var links = Links(answer, ReportLink(), GlassFailure.ReportRequest);
        return links.Count switch
        {
            1 when options.IsMarketValueAssessor(links[0]) => links[0],
            1 => throw new GlassMvaStageException(GlassFailure.ReportOffOrigin),
            0 => throw new GlassMvaStageException(GlassFailure.ReportNone),
            _ => throw new GlassMvaStageException(GlassFailure.ReportAmbiguous),
        };
    }

    private async Task<string> CreateVehicleForAsync(
        string registration, long mileageMiles, string valuationDate, CancellationToken cancellationToken)
    {
        var created = await JsonAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                options.MarketValueAssessor(
                    "index/create-new-vehicle/value/0/valuate/1"
                    + $"/mileage/{mileageMiles.ToString(CultureInfo.InvariantCulture)}"
                    + $"/valdate/{valuationDate}/condition/false")),
            GlassFailure.VehicleRequest,
            cancellationToken,
            // A lost answer here may still have created the vehicle.
            outcomeUnknown: true);

        var vrm = Text(created, "vrm");
        var id = Text(created, "id");
        if (!GlassRepairEstimateSessionPolicy.SameRegistration(vrm, registration)
            || !long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var numeric)
            || numeric <= 0)
        {
            throw new GlassMvaStageException(GlassFailure.VehicleIdentity, outcomeUnknown: true);
        }

        return id!;
    }

    /// <summary>
    /// The portal's "Add Unqualified Vehicle" form, exactly as the operator's
    /// browser posted it on 2 October 2026: every control at its default, the
    /// make and model lists at "All", a two-digit month (a one-digit one is
    /// refused by the portal's own validation) and a free-text model that
    /// names the Case's registration so the stock list says which Case the
    /// placeholder belongs to. The unqualified vehicle carries no Glass's
    /// data of its own; the Engineer identifies the real vehicle inside the
    /// estimator.
    /// </summary>
    private static readonly (string Name, string Value)[] PlaceholderForm =
    [
        ("make", "*"),
        ("uqmodel", "*"),
        ("edit_make", "All"),
        ("edit_model", string.Empty),
        ("edit_trim", string.Empty),
        ("edit_month", "01"),
        ("edit_year", "2025"),
        ("edit_plate", "25"),
        ("edit_cc", string.Empty),
        ("edit_body", "default"),
        ("edit_bodytext", "Body type"),
        ("edit_fuel", "default"),
        ("edit_fueltext", "Fuel type"),
        ("edit_transm", "default"),
        ("edit_transmtext", "Transmission"),
        ("edit_gear", string.Empty),
        ("edit_drive", "default"),
        ("edit_drivetext", "Drive/power train"),
    ];

    /// <summary>
    /// Inserts a placeholder vehicle into the account's stock list for a
    /// registration the provider does not know, and answers its stock id.
    /// Like vehicle creation this changes the account, so it is never
    /// retried: a lost or unreadable answer, and an answer naming no usable
    /// id, are reported as unknown. The portal's own validation refusal (a
    /// non-empty <c>title</c>) created nothing and is a known outcome.
    /// </summary>
    public async Task<string> InsertPlaceholderAsync(string registration, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, options.MarketValueAssessor("index/unqualified-vehicle-insert"))
        {
            Content = new FormUrlEncodedContent(PlaceholderForm.Select(field =>
                new KeyValuePair<string?, string?>(field.Name, field.Name == "edit_model" ? registration : field.Value))),
        };
        // A lost answer here may still have inserted the placeholder.
        var inserted = await JsonAsync(request, GlassFailure.PlaceholderRequest, cancellationToken, outcomeUnknown: true);
        if (!string.IsNullOrEmpty(Text(inserted, "title")))
        {
            throw new GlassMvaStageException(GlassFailure.PlaceholderRefused);
        }

        var id = Text(inserted, "new_vehicle_id");
        if (!long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var numeric) || numeric <= 0)
        {
            throw new GlassMvaStageException(GlassFailure.PlaceholderId, outcomeUnknown: true);
        }

        return id!;
    }

    /// <summary>
    /// Proves the created vehicle is the one the estimate will be started for
    /// (stages 12–14): its detail fragments load and its valuation page names
    /// both the requested repair profile and the type number the lookup
    /// settled on.
    /// </summary>
    public async Task RequireVehicleAsync(
        string vehicleId, string natCode, string registration, long mileageMiles,
        CancellationToken cancellationToken)
    {
        var inputs = await VehicleControlsAsync(vehicleId, cancellationToken);
        if (string.IsNullOrWhiteSpace(natCode) || Field(inputs, "id") != vehicleId || Field(inputs, "natcode") != natCode
            || !GlassRepairEstimateSessionPolicy.SameRegistration(Field(inputs, "registration_number"), registration)
            || !long.TryParse(Field(inputs, "mileage"), NumberStyles.None, CultureInfo.InvariantCulture, out var mileage)
            || mileage != mileageMiles)
        {
            throw new GlassMvaStageException(GlassFailure.DetailsIdentity);
        }
    }

    /// <summary>
    /// Proves a stock vehicle is the placeholder this session inserted and
    /// answers the type number the portal gave it. A placeholder has no
    /// registration and no mileage of its own — the Engineer identifies the
    /// real vehicle inside the estimator — so its identity is its id, an empty
    /// registration control, a numeric type number (the same one as before,
    /// when the session already recorded it) and the repair profile.
    /// </summary>
    public async Task<string> RequirePlaceholderAsync(
        string vehicleId, string? natCode, CancellationToken cancellationToken)
    {
        var inputs = await VehicleControlsAsync(vehicleId, cancellationToken);
        var stated = Field(inputs, "natcode");
        if (Field(inputs, "id") != vehicleId
            || string.IsNullOrEmpty(stated) || !stated.All(char.IsAsciiDigit)
            || (natCode is not null && stated != natCode)
            || Field(inputs, "registration_number") is not { Length: 0 })
        {
            throw new GlassMvaStageException(GlassFailure.PlaceholderIdentity);
        }

        return stated;
    }

    /// <summary>
    /// Stages 12–14's reads: the vehicle's detail fragments and its valuation
    /// page, whose named controls are the only things that establish identity
    /// (scripts, comments and unrelated text can carry the right numbers for
    /// the wrong vehicle), and which must offer the configured repair profile.
    /// </summary>
    private async Task<Dictionary<string, string>[]> VehicleControlsAsync(
        string vehicleId, CancellationToken cancellationToken)
    {
        await TextAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                options.MarketValueAssessor($"index/vehicle-details/id/{vehicleId}/valuate/true/keep_page/1")),
            ajax: true,
            GlassFailure.DetailsRequest,
            cancellationToken);
        await TextAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                options.MarketValueAssessor(
                    $"index/vehicle-detail-inline-fragment/id/{vehicleId}/view/dealer/grid/{Grid}")),
            ajax: true,
            GlassFailure.DetailsRequest,
            cancellationToken);

        var value = await TextAsync(
            new HttpRequestMessage(
                HttpMethod.Get, options.MarketValueAssessor($"index/vehicle-details-value/id/{vehicleId}")),
            ajax: true,
            GlassFailure.DetailsRequest,
            cancellationToken);
        // Only named controls establish identity. Scripts, comments and unrelated
        // text in the page can contain the right numbers for the wrong vehicle.
        return Matched(() =>
        {
            var controls = InertHtml().Replace(value, string.Empty);
            var inputs = InputControl().Matches(controls).Cast<Match>()
                .Select(match => Attributes(match.Groups[1].Value)).ToArray();
            var profiles = SelectControl().Matches(controls).Cast<Match>()
                .Where(match => Attributes(match.Groups[1].Value).GetValueOrDefault("name") == "ere_profile")
                .ToArray();
            if (profiles.Length != 1 || Attributes(profiles[0].Groups[1].Value).ContainsKey("disabled")
                || !OptionControl().Matches(profiles[0].Groups[2].Value).Cast<Match>()
                    .Select(match => Attributes(match.Groups[1].Value))
                    .Any(option => !option.ContainsKey("disabled")
                        && option.GetValueOrDefault("value") == options.RepairProfileId))
            {
                throw new GlassMvaStageException(GlassFailure.DetailsProfile);
            }

            return inputs;
        }, GlassFailure.DetailsIdentity);
    }

    /// <summary>
    /// One named control's value. The captured page repeats id in two forms:
    /// equal repeats are valid; contradictory repeats, a disabled control or
    /// an absent one do not identify a vehicle.
    /// </summary>
    private static string? Field(Dictionary<string, string>[] inputs, string name)
    {
        var values = inputs.Where(input => input.GetValueOrDefault("name") == name)
            .Select(input => input.ContainsKey("disabled") ? null : input.GetValueOrDefault("value"))
            .Distinct(StringComparer.Ordinal).ToArray();
        return values.Length == 1 ? values[0] : null;
    }

    private static Dictionary<string, string> Attributes(string html)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in HtmlAttribute().Matches(html))
        {
            if (!attributes.TryAdd(match.Groups[1].Value,
                WebUtility.HtmlDecode(match.Groups[2].Success ? match.Groups[2].Value
                    : match.Groups[3].Success ? match.Groups[3].Value : match.Groups[4].Value)))
            {
                throw new GlassMvaStageException(GlassFailure.DetailsIdentity);
            }
        }
        return attributes;
    }

    /// <summary>
    /// Leaves exactly this vehicle selected in the grid (stages 15–17). The
    /// selection is server-side session state, so a resumed session re-asserts
    /// it before exporting rather than assuming the grid still holds it.
    /// </summary>
    public async Task SelectOnlyAsync(string vehicleId, CancellationToken cancellationToken)
    {
        // The exact update-vehicle-select route is the spike's record of the
        // observed portal call; the supplied captures cover the count check but
        // not the selection itself, so a live run is what proves this path.
        await TextAsync(
            new HttpRequestMessage(
                HttpMethod.Get, options.MarketValueAssessor($"index/update-vehicle-select/grid/{Grid}/select/false")),
            ajax: true,
            GlassFailure.SelectRequest,
            cancellationToken);
        await TextAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                options.MarketValueAssessor(
                    $"index/update-vehicle-select/grid/{Grid}/id/{vehicleId}/select/true")),
            ajax: true,
            GlassFailure.SelectRequest,
            cancellationToken);

        var counted = await JsonAsync(
            new HttpRequestMessage(
                HttpMethod.Get, options.MarketValueAssessor($"index/get-selected-vehicle-count/grid/{Grid}")),
            GlassFailure.SelectRequest,
            cancellationToken);
        if (Text(counted, "count") != "1")
        {
            throw new GlassMvaStageException(GlassFailure.SelectCount);
        }
    }

    /// <summary>
    /// Starts the calculation and produces the URL the operator opens (stages
    /// 18–19). The provider's launch URL is accepted only when every segment it
    /// carries reads as expected, and only its <c>caller</c> is replaced — with
    /// Pegasus's own one-use callback.
    /// </summary>
    /// <remarks>
    /// Never retried. <c>start-ere</c> allocates the estimate inside the Glass's
    /// account, so a lost answer leaves an estimate that may exist; the gateway
    /// records that as unknown and a person reconciles it.
    /// </remarks>
    public async Task<GlassEstimateLaunch> StartEstimateAsync(
        string existingEreId, Uri pegasusCallback, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, options.MarketValueAssessor("ere/start-ere"))
        {
            Content = new FormUrlEncodedContent(
            [
                new("profile_id", options.RepairProfileId),
                new("ere_id", existingEreId),
            ]),
        };
        // A lost answer here may still have allocated the estimate.
        var started = await JsonAsync(request, GlassFailure.StartRequest, cancellationToken, outcomeUnknown: true);
        if (Text(started, "status") != "ok")
        {
            throw new GlassMvaStageException(GlassFailure.StartStatus);
        }

        var launchUrl = Text(started, "ere_url");
        if (launchUrl is null || !Uri.TryCreate(launchUrl, UriKind.Absolute, out var launch)
            || !options.IsEstimator(launch))
        {
            throw new GlassMvaStageException(GlassFailure.StartUrl, outcomeUnknown: true);
        }

        return Rewrite(launch, pegasusCallback);
    }

    /// <summary>
    /// Hands the operator's Save &amp; Exit back to Glass's exactly as it
    /// arrived (stage 23). The query is relayed verbatim — it is the provider's
    /// own message and re-encoding it would change what Glass's verifies — and
    /// the answer must name this estimate and report success.
    /// </summary>
    public async Task RelayCallbackAsync(
        Uri originalCallback,
        IReadOnlyCollection<string> estimateIds,
        string rawQuery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(estimateIds);
        if (!options.IsMarketValueAssessor(originalCallback))
        {
            throw new GlassMvaStageException(GlassFailure.RelayRequest);
        }

        var relay = new UriBuilder(originalCallback)
        {
            Query = rawQuery.StartsWith('?') ? rawQuery[1..] : rawQuery,
        }.Uri;
        var request = new HttpRequestMessage(HttpMethod.Get, relay);
        request.Headers.Referrer = options.EstimatorBaseUri;
        var html = await TextAsync(request, ajax: false, GlassFailure.RelayRequest, cancellationToken);

        var arguments = CallbackArguments(html);
        if (arguments.Count != 10)
        {
            throw new GlassMvaStageException(GlassFailure.RelayShape);
        }
        if (!estimateIds.Contains(arguments[7], StringComparer.Ordinal))
        {
            // The relayed id is a provider number; anything else is not repeated.
            var relayed = arguments[7].All(char.IsAsciiDigit) ? arguments[7] : "non-numeric";
            throw new GlassMvaStageException(
                GlassFailure.RelayEstimate,
                detail: $"expected_ids={estimateIds.Count} relayed={relayed}");
        }
        if (arguments[8] != "1")
        {
            throw new GlassMvaStageException(GlassFailure.RelayOutcome);
        }
    }

    /// <summary>
    /// Waits for Glass's to publish exactly one export for the selected vehicle
    /// and returns its address (stage 24). Zero links after the bounded wait is
    /// an unknown outcome — the export may still be forming — while more than
    /// one, or one off Glass's own origin, is refused outright.
    /// </summary>
    public async Task<Uri> WaitForExportAsync(CancellationToken cancellationToken)
    {
        var deadline = timeProvider.GetUtcNow() + options.ExportTimeout;
        while (true)
        {
            var grid = await TextAsync(
                new HttpRequestMessage(HttpMethod.Get, options.MarketValueAssessor($"ere/export-vehicle/grid/{Grid}")),
                ajax: true,
                GlassFailure.ExportRequest,
                cancellationToken);
            var links = Links(grid, ExportLink(), GlassFailure.ExportRequest);
            if (links.Count == 1)
            {
                return options.IsMarketValueAssessor(links[0])
                    ? links[0]
                    : throw new GlassMvaStageException(GlassFailure.ExportOffOrigin);
            }
            if (links.Count > 1)
            {
                throw new GlassMvaStageException(GlassFailure.ExportAmbiguous);
            }
            if (timeProvider.GetUtcNow() >= deadline)
            {
                throw new GlassMvaStageException(GlassFailure.ExportNone, outcomeUnknown: true);
            }

            await Task.Delay(options.ExportPollInterval, timeProvider, cancellationToken);
        }
    }

    /// <summary>
    /// Downloads a file Glass's published — the estimate's export (stage 25) or
    /// a valuation report — refusing anything past the configured cap.
    /// </summary>
    public async Task<byte[]> DownloadExportAsync(Uri export, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            new HttpRequestMessage(HttpMethod.Get, export), ajax: false, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new GlassMvaStageException(GlassFailure.DownloadRequest);
        }

        return await ReadAsync(response, options.MaximumExportBytes, GlassFailure.DownloadOversize, outcomeUnknown: false, cancellationToken);
    }

    /// <summary>
    /// Replaces only the launch URL's <c>caller</c>, then re-reads every other
    /// parameter from the rewritten URL and requires it to be exactly what the
    /// provider sent. A launch URL missing one of them, carrying two of any of
    /// them, or naming a caller that is not a Glass's callback is refused.
    /// </summary>
    private GlassEstimateLaunch Rewrite(Uri launch, Uri pegasusCallback)
    {
        var stated = ParseQuery(launch.Query);
        if (stated is null)
        {
            throw new GlassMvaStageException(GlassFailure.StartUrl, outcomeUnknown: true);
        }
        foreach (var name in LaunchParameters)
        {
            if (!stated.ContainsKey(name))
            {
                throw new GlassMvaStageException(GlassFailure.StartUrl, outcomeUnknown: true);
            }
        }
        if (stated["ucode"].Length != 8 || stated["scode"].Length != 4)
        {
            throw new GlassMvaStageException(GlassFailure.StartUrl, outcomeUnknown: true);
        }

        var callerMatch = Matched(() => CallbackPath().Match(stated["caller"]), GlassFailure.StartCaller, outcomeUnknown: true);
        if (!Uri.TryCreate(stated["caller"], UriKind.Absolute, out var caller)
            || !callerMatch.Success
            || !options.IsMarketValueAssessor(caller)
            || caller.Query.Length != 0)
        {
            throw new GlassMvaStageException(GlassFailure.StartCaller, outcomeUnknown: true);
        }

        var rewritten = new UriBuilder(launch)
        {
            Query = string.Join(
                '&',
                stated.Select(pair => $"{Uri.EscapeDataString(pair.Key)}="
                    + Uri.EscapeDataString(pair.Key == "caller" ? pegasusCallback.AbsoluteUri : pair.Value))),
        }.Uri;

        // The rewrite is only allowed to have moved the caller. Reading the
        // result back is what proves that, rather than trusting the builder.
        var produced = ParseQuery(rewritten.Query);
        if (produced is null
            || produced.Count != stated.Count
            || produced["caller"] != pegasusCallback.AbsoluteUri
            || stated.Any(pair => pair.Key != "caller"
                && (!produced.TryGetValue(pair.Key, out var value) || value != pair.Value)))
        {
            throw new GlassMvaStageException(GlassFailure.StartCaller, outcomeUnknown: true);
        }

        return new(callerMatch.Groups[1].Value, caller, rewritten);
    }

    /// <summary>
    /// Stages 5–9: the lookup, the candidate it settles on, and that
    /// candidate's valuation page for the month, as the portal reads them.
    /// </summary>
    private async Task<(GlassVehicleLookup Lookup, string Page)> ValuationPageAsync(
        string registration, long mileageMiles, string valuationDate, CancellationToken cancellationToken)
    {
        var search = $"index/search-vrm/vrms_reg_no/{Uri.EscapeDataString(registration)}"
            + $"/valuate/1/vrms_mileage/{mileageMiles.ToString(CultureInfo.InvariantCulture)}";
        var (natCode, lookupDetail) = await LookupNatCodeAsync(search, cancellationToken);
        var candidates = await CandidatesAsync(valuationDate, lookupDetail, cancellationToken);
        var ordinal = CandidateOrdinal(candidates, natCode);
        var page = await TextAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                options.MarketValueAssessor(
                    $"three-phase-vehicle/get-values/vehicle/{ordinal.ToString(CultureInfo.InvariantCulture)}"
                    + $"/source/vrm/valdate/{valuationDate}")),
            ajax: true,
            GlassFailure.ValuationRequest,
            cancellationToken);
        return (new(natCode, ordinal), page);
    }

    /// <summary>Stage 10: the portal's own refresh of the account's lookup count.</summary>
    private async Task RefreshAsync(CancellationToken cancellationToken) =>
        await TextAsync(
            new HttpRequestMessage(HttpMethod.Get, options.MarketValueAssessor("three-phase-vehicle/refresh-vrm-count")),
            ajax: true,
            GlassFailure.RefreshRequest,
            cancellationToken);

    /// <summary>
    /// Retail Transacted and Glass's Trade, read off the valuation page. The
    /// portal answers "valuation not possible" with JSON in place of the page;
    /// that is refused naming only which of its fields were present, and as
    /// the vehicle's age when its message says the vehicle is not valued
    /// "due to the age" (Glass's values cars and motorcycles for 20 years and
    /// light commercial vehicles for 15). Each
    /// figure comes from its own value box — comments and scripts are removed
    /// first, because the page carries a commented-out box of its own — and a
    /// box that is missing, repeated or holds anything but an amount is
    /// unreadable rather than guessed at.
    /// </summary>
    internal static GlassGuideFigures GuideFigures(string page)
    {
        var body = page.Trim('﻿', ' ', '\t', '\r', '\n');
        if (body.StartsWith('{'))
        {
            string detail;
            bool vehicleAge;
            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                var message = Text(root, "errormsg");
                detail = $"success={Text(root, "success") ?? "absent"}"
                    + $" errormsg={(string.IsNullOrEmpty(message) ? "absent" : "present")}";
                vehicleAge = message?.Contains("due to the age", StringComparison.OrdinalIgnoreCase) == true;
            }
            catch (JsonException)
            {
                throw new GlassMvaStageException(GlassFailure.ValuationUnreadable);
            }

            throw new GlassMvaStageException(
                vehicleAge ? GlassFailure.ValuationVehicleAge : GlassFailure.ValuationNotPossible,
                detail: detail);
        }

        return Matched(() =>
        {
            var html = InertHtml().Replace(body, string.Empty);
            var boxes = ValueBox().Matches(html);
            return new GlassGuideFigures(
                Figure(html, boxes, "three_phase_transacted"), Figure(html, boxes, "three_phase_glass_trade"));
        }, GlassFailure.ValuationUnreadable);
    }

    private static decimal Figure(string html, MatchCollection boxes, string boxId)
    {
        var at = boxes.Cast<Match>()
            .Select((box, index) => (Box: box, Index: index))
            .Where(candidate => candidate.Box.Groups[1].Value == boxId)
            .ToArray();
        if (at.Length != 1)
        {
            throw new GlassMvaStageException(GlassFailure.ValuationUnreadable, detail: $"box={boxId} count={at.Length}");
        }

        var start = at[0].Box.Index;
        var end = at[0].Index + 1 < boxes.Count ? boxes[at[0].Index + 1].Index : html.Length;
        var values = ValueText().Matches(html[start..end]);
        if (values.Count != 1)
        {
            throw new GlassMvaStageException(GlassFailure.ValuationUnreadable, detail: $"box={boxId} values={values.Count}");
        }

        var amount = WebUtility.HtmlDecode(values[0].Groups[1].Value).Trim().TrimStart('£').Trim();
        return decimal.TryParse(
                amount,
                NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var figure)
            ? figure
            : throw new GlassMvaStageException(GlassFailure.ValuationUnreadable, detail: $"box={boxId} amount=unreadable");
    }

    /// <summary>
    /// Stages 5–6: the stock search is the lookup, and only a registration
    /// the account already stocks is looked up again as a fresh search,
    /// exactly as the portal's "Continue with New Entry" does. Nothing is
    /// repeated. An answer whose <c>vrm_lookup</c> cannot be read is not the
    /// portal's answer and is refused as a bad request; one that reports the
    /// vehicle was not found, or carries no type number at all (the portal
    /// answers <c>"natcode":false</c> for a plate its supplier does not
    /// know), is the provider's own "not found".
    /// </summary>
    private async Task<(string NatCode, string Detail)> LookupNatCodeAsync(
        string search, CancellationToken cancellationToken)
    {
        var answer = await JsonAsync(
            new HttpRequestMessage(HttpMethod.Get, options.MarketValueAssessor(search)),
            GlassFailure.LookupRequest,
            cancellationToken);
        if (Number(answer, "stockcount") > 0)
        {
            answer = await JsonAsync(
                new HttpRequestMessage(HttpMethod.Get, options.MarketValueAssessor(search + "/nostocksearch/1")),
                GlassFailure.LookupRequest,
                cancellationToken);
        }

        var lookup = Number(answer, "vrm_lookup");
        var natCode = StringOf(answer, "natcode");
        var detail = $"stockcount={Text(answer, "stockcount") ?? "?"} vrm_lookup={Text(answer, "vrm_lookup") ?? "?"}"
            + $" natcode={(string.IsNullOrEmpty(natCode) ? "absent" : "present")}";
        if (lookup is null)
        {
            throw new GlassMvaStageException(GlassFailure.LookupRequest, detail: detail);
        }
        if (lookup < 0 || string.IsNullOrEmpty(natCode))
        {
            throw new GlassMvaStageException(GlassFailure.LookupNotFound, detail: detail);
        }

        return (natCode, detail);
    }

    /// <summary>
    /// Stage 7: the candidate list the lookup produced (a read the portal's
    /// page makes straight after the lookup). An answer whose success flag
    /// is not set is read again, twice, 250 ms apart, before it is refused:
    /// the read changes nothing, and the refusal carries what the lookup
    /// and the list each answered, in numbers and flags only.
    /// </summary>
    private async Task<JsonElement> CandidatesAsync(
        string valuationDate, string lookupDetail, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var candidates = await JsonAsync(
                new HttpRequestMessage(
                    HttpMethod.Get,
                    options.MarketValueAssessor($"three-phase-vehicle/get-vehicles/source/vrm/valdate/{valuationDate}")),
                GlassFailure.CandidatesRequest,
                cancellationToken);
            if (Text(candidates, "success") is "true" or "True")
            {
                return candidates;
            }
            if (attempt == CandidateReads)
            {
                var keys = candidates.ValueKind == JsonValueKind.Object
                    ? string.Join(',', candidates.EnumerateObject().Select(property => property.Name))
                    : candidates.ValueKind.ToString();
                throw new GlassMvaStageException(
                    GlassFailure.CandidatesRefused,
                    detail: $"lookup[{lookupDetail}] candidates[success={Text(candidates, "success") ?? "absent"}"
                        + $" keys={keys} html={(Text(candidates, "html") ?? string.Empty).Length} attempt={attempt}]");
            }

            await Task.Delay(LookupRetryDelay, timeProvider, cancellationToken);
        }
    }

    /// <summary>
    /// Which candidate the lookup's type number names. The provider answers a
    /// rendered list, each entry carrying its own type number; nothing is
    /// guessed, so no candidate and more than one candidate are separate
    /// refusals and neither continues.
    /// </summary>
    private static int CandidateOrdinal(JsonElement candidates, string natCode)
    {
        var html = Text(candidates, "html") ?? string.Empty;
        var blocks = Matched(() => CandidateBlock().Matches(html).ToArray(), GlassFailure.CandidatesRequest);
        var matched = 0;
        var ordinal = 0;
        for (var index = 0; index < blocks.Length; index++)
        {
            var candidate = blocks[index];
            var position = int.Parse(candidate.Groups[1].Value, CultureInfo.InvariantCulture);
            var end = index + 1 < blocks.Length ? blocks[index + 1].Index : html.Length;
            if (html.AsSpan(candidate.Index, end - candidate.Index).Contains(natCode, StringComparison.Ordinal))
            {
                matched++;
                ordinal = position;
            }
        }

        return matched switch
        {
            1 => ordinal,
            0 => throw new GlassMvaStageException(GlassFailure.CandidatesNone),
            _ => throw new GlassMvaStageException(GlassFailure.CandidatesAmbiguous),
        };
    }

    /// <summary>
    /// The arguments of the relay's <c>ere_callback_xml</c> call, in order and
    /// unquoted. A comma inside a quoted argument does not separate arguments.
    /// </summary>
    private static List<string> CallbackArguments(string html)
    {
        const string marker = "ere_callback_xml(";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new GlassMvaStageException(GlassFailure.RelayShape);
        }

        var arguments = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var index = start + marker.Length; index < html.Length; index++)
        {
            var character = html[index];
            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }
            if (quoted)
            {
                current.Append(character);
                continue;
            }
            if (character == ',')
            {
                arguments.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }
            if (character == ')')
            {
                arguments.Add(current.ToString().Trim());
                return arguments;
            }

            current.Append(character);
        }

        throw new GlassMvaStageException(GlassFailure.RelayShape);
    }

    /// <summary>Every download link of one kind a page offers, as absolute addresses.</summary>
    private List<Uri> Links(string html, Regex link, string failureCode) =>
        Matched(
            () => link.Matches(html)
                .Select(match => Absolute(
                    new Uri(WebUtility.HtmlDecode(match.Groups[1].Value), UriKind.RelativeOrAbsolute),
                    options.MarketValueAssessorBaseUri))
                .Distinct()
                .ToList(),
            failureCode);

    /// <summary>
    /// One read of a provider page by a pattern. The patterns below are
    /// bounded, so their match budget guards against a stalled host, not
    /// against backtracking; a budget that runs out is the stage's own
    /// refusal, with <c>regex=timeout</c> as its detail, and never a
    /// transport failure. A stage that may already have acted at the
    /// provider says so, as it does for any other refusal.
    /// </summary>
    internal static T Matched<T>(Func<T> match, string failureCode, bool outcomeUnknown = false)
    {
        ArgumentNullException.ThrowIfNull(match);
        try
        {
            return match();
        }
        catch (RegexMatchTimeoutException)
        {
            throw new GlassMvaStageException(failureCode, outcomeUnknown, "regex=timeout");
        }
    }

    /// <summary>
    /// The month Glass's values against, in Europe/London — the provider's own
    /// clock, not the host's, so a machine in another zone asks for the same
    /// month a person at Collision Engineers would.
    /// </summary>
    private string ValuationMonth() =>
        LondonCalendar.LocalAt(timeProvider.GetUtcNow())
            .ToString("yyyyMM", CultureInfo.InvariantCulture);

    private static string Month(DateOnly month) => month.ToString("yyyyMM", CultureInfo.InvariantCulture);

    private async Task<JsonElement> JsonAsync(
        HttpRequestMessage request,
        string failureCode,
        CancellationToken cancellationToken,
        bool outcomeUnknown = false)
    {
        var body = await TextAsync(request, ajax: true, failureCode, cancellationToken, outcomeUnknown);
        try
        {
            // Glass's JSON arrives with a UTF-8 byte-order mark at both ends;
            // neither is content and System.Text.Json reads neither.
            using var document = JsonDocument.Parse(body.Trim('﻿', ' ', '\r', '\n'));
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new GlassMvaStageException(failureCode, outcomeUnknown);
        }
    }

    private async Task<string> TextAsync(
        HttpRequestMessage request,
        bool ajax,
        string failureCode,
        CancellationToken cancellationToken,
        bool outcomeUnknown = false)
    {
        using var response = await SendAsync(request, ajax, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new GlassMvaStageException(failureCode, outcomeUnknown);
        }

        var content = await ReadAsync(response, MaximumTextBytes, failureCode, outcomeUnknown, cancellationToken);
        return Encoding.UTF8.GetString(content);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, bool ajax, CancellationToken cancellationToken)
    {
        if (cookies.Count > 0)
        {
            request.Headers.TryAddWithoutValidation(
                "Cookie", string.Join("; ", cookies.Select(pair => $"{pair.Key}={pair.Value}")));
        }
        if (ajax)
        {
            request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
            request.Headers.Referrer = options.MarketValueAssessor("index");
        }

        var response = await httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        Accept(response);
        return response;
    }

    /// <summary>
    /// Keeps the session's own cookie jar current. Only the name and value
    /// matter here: every request this adapter makes goes to one origin, so a
    /// cookie's domain and path decide nothing, and an emptied value is the
    /// provider dropping the cookie.
    /// </summary>
    private void Accept(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var headers))
        {
            return;
        }

        foreach (var header in headers)
        {
            var pair = header.Split(';', 2)[0];
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var name = pair[..separator].Trim();
            var value = pair[(separator + 1)..].Trim();
            if (value.Length == 0)
            {
                cookies.Remove(name);
            }
            else
            {
                cookies[name] = value;
            }
        }
    }

    private static async Task<byte[]> ReadAsync(
        HttpResponseMessage response, int maximumBytes, string failureCode, bool outcomeUnknown,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maximumBytes)
            {
                throw new GlassMvaStageException(failureCode, outcomeUnknown);
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// The query as an ordered name/value map, or null when a name appears
    /// twice — an ambiguous launch URL is refused rather than resolved by
    /// picking one of them.
    /// </summary>
    private static Dictionary<string, string>? ParseQuery(string query)
    {
        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0 || !parsed.TryAdd(
                    Uri.UnescapeDataString(part[..separator]),
                    Uri.UnescapeDataString(part[(separator + 1)..])))
            {
                return null;
            }
        }

        return parsed;
    }

    private static Uri Absolute(Uri candidate, Uri baseUri) =>
        candidate.IsAbsoluteUri ? candidate : new Uri(baseUri, candidate);

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
                _ => null,
            }
            : null;

    /// <summary>
    /// A field only when the provider answered it as a string. The portal
    /// answers <c>"natcode":false</c> for a plate it does not know, and a
    /// boolean read as text would pass for a type number.
    /// </summary>
    private static string? StringOf(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? Number(JsonElement element, string name) =>
        long.TryParse(Text(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    [GeneratedRegex(@"<!--.*?-->|<(script|style)\b[^>]*>.*?</\1\s*>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
    private static partial Regex InertHtml();

    [GeneratedRegex("<input\\b((?:[^>\"']|\"[^\"]*\"|'[^']*')*)>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
    private static partial Regex InputControl();

    [GeneratedRegex("<select\\b((?:[^>\"']|\"[^\"]*\"|'[^']*')*)>(.*?)</select\\s*>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
    private static partial Regex SelectControl();

    [GeneratedRegex("<option\\b((?:[^>\"']|\"[^\"]*\"|'[^']*')*)>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
    private static partial Regex OptionControl();

    [GeneratedRegex("([a-zA-Z_:][a-zA-Z0-9_:.-]*)(?:\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^\\s>]+)))?",
        RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
    private static partial Regex HtmlAttribute();

    [GeneratedRegex(
        @"name=""csrf_token""[^>]{0,200}?value=""([0-9a-fA-F]{32})""",
        RegexOptions.CultureInvariant,
        MatchTimeoutMilliseconds)]
    private static partial Regex CsrfToken();

    [GeneratedRegex(
        @"^https://[^/]+/ere/ere-callback/ere_id/(\d+)/ere_session/([^/?]+)$",
        RegexOptions.CultureInvariant,
        MatchTimeoutMilliseconds)]
    private static partial Regex CallbackPath();

    [GeneratedRegex(
        @"class=""three_phase_car_info car(\d+)""",
        RegexOptions.CultureInvariant,
        MatchTimeoutMilliseconds)]
    private static partial Regex CandidateBlock();

    [GeneratedRegex(
        @"href=""([^""]{0,400}?/ndp_download/[^""]{0,200}?\.xml)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        MatchTimeoutMilliseconds)]
    private static partial Regex ExportLink();

    [GeneratedRegex(
        @"href=""([^""]{0,400}?/ndp_download/[^""]{0,200}?\.pdf)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        MatchTimeoutMilliseconds)]
    private static partial Regex ReportLink();

    [GeneratedRegex(
        @"<div\b[^>]*\bid=""(three_phase_[a-z_]+)""[^>]*\bclass=""three_phase_value_box""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        MatchTimeoutMilliseconds)]
    private static partial Regex ValueBox();

    [GeneratedRegex(
        @"class=""three_phase_value_text""\s*>([^<]*)<",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        MatchTimeoutMilliseconds)]
    private static partial Regex ValueText();
}
