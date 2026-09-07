using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// Why one Glass's stage stopped. The code is the whole operator-facing
/// record: it is written to the session's <c>LastError</c> and never carries a
/// credential, a cookie, a CSRF token, the <c>ere_session</c> or any part of a
/// provider URL.
/// </summary>
/// <remarks>
/// <paramref name="outcomeUnknown"/> separates "this did not happen" from "we
/// cannot tell whether this happened". Only the second kind may have left
/// server-side state behind at Glass's, and it is never retried or replaced
/// automatically.
/// </remarks>
internal sealed class GlassMvaStageException(string failureCode, bool outcomeUnknown = false)
    : Exception(failureCode)
{
    public string FailureCode { get; } = failureCode;

    public bool OutcomeUnknown { get; } = outcomeUnknown;
}

/// <summary>
/// The one list of Glass's stage failure codes. A code names the stage and what
/// about it refused, so a session's <c>LastError</c> is enough to say where a
/// launch stopped without holding anything the provider handed over.
/// </summary>
internal static class GlassFailure
{
    public const string LoginRequest = "glass.login.request";
    public const string LoginCsrf = "glass.login.csrf";
    public const string LoginRedirect = "glass.login.redirect";
    public const string LoginLanding = "glass.login.landing";
    public const string LookupRequest = "glass.lookup.request";
    public const string LookupUnavailable = "glass.lookup.unavailable";
    public const string CandidatesRequest = "glass.candidates.request";
    public const string CandidatesRefused = "glass.candidates.refused";
    public const string CandidatesNone = "glass.candidates.none";
    public const string CandidatesAmbiguous = "glass.candidates.ambiguous";
    public const string ValuationRequest = "glass.valuation.request";
    public const string RefreshRequest = "glass.refresh.request";
    public const string VehicleRequest = "glass.vehicle.request";
    public const string VehicleIdentity = "glass.vehicle.identity";
    public const string DetailsRequest = "glass.details.request";
    public const string DetailsProfile = "glass.details.profile";
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
    public const string ExportUnreadable = "glass.export.unreadable";
    public const string ExportEmpty = "glass.export.empty";
    public const string IdentityRegistration = "glass.identity.registration";
    public const string IdentityMileage = "glass.identity.mileage";
    public const string IdentityNatCode = "glass.identity.natcode";
    public const string CallbackNotSaved = "glass.callback.not_saved";
    public const string CallbackExpired = "glass.callback.expired";
    public const string CustodyFailed = "glass.custody.failed";
    public const string TransportFailed = "glass.transport.failed";
    public const string TransportUnknown = "glass.transport.unknown";
}

/// <summary>What stage 6's fresh lookup established about the vehicle.</summary>
internal sealed record GlassVehicleLookup(string NatCode, int CandidateOrdinal);

/// <summary>
/// What stage 18 started and stage 19 proved about its launch URL. The
/// provider's own callback is kept whole rather than split into the estimate
/// and session it names, because relaying to it is what a completion does and
/// rebuilding that address from its parts would be a second copy of its shape.
/// </summary>
internal sealed record GlassEstimateLaunch(string EreId, Uri OriginalCallback, Uri EstimatorUrl);

/// <summary>
/// The Market Value Assessor transport: every HTTP stage of a Glass's Repair
/// Estimate session and nothing else. The session policy — what is persisted,
/// when, and what a failure means to the Case — belongs to
/// <see cref="GlassRepairEstimateGateway"/>.
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
/// <b>Nothing retries blindly.</b> The fresh VRM lookup is the one stage that
/// retries, once, after 250 ms, and only when the provider answered readable
/// JSON that reported no lookup. Vehicle creation and starting the estimate
/// change state inside the Glass's account, so a lost answer to either is
/// reported as unknown rather than repeated.
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
    /// <summary>The one delay before the one retryable stage runs again.</summary>
    private static readonly TimeSpan LookupRetryDelay = TimeSpan.FromMilliseconds(250);

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

    /// <summary>The grid every stock and export stage addresses.</summary>
    private const string Grid = "stocklistGrid";

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
        var csrf = CsrfToken().Match(page);
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
    /// (stages 5–10). The fresh lookup is the one stage that may be retried,
    /// because it reads and changes nothing.
    /// </summary>
    public async Task<GlassVehicleLookup> LookupAsync(
        string registration, long mileageMiles, CancellationToken cancellationToken)
    {
        var search = $"index/search-vrm/vrms_reg_no/{Uri.EscapeDataString(registration)}"
            + $"/valuate/1/vrms_mileage/{mileageMiles.ToString(CultureInfo.InvariantCulture)}";
        await TextAsync(
            new HttpRequestMessage(HttpMethod.Get, options.MarketValueAssessor(search)),
            ajax: true,
            GlassFailure.LookupRequest,
            cancellationToken);

        var natCode = await FreshLookupAsync(search, cancellationToken);
        var valuationDate = ValuationMonth();
        var candidates = await JsonAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                options.MarketValueAssessor($"three-phase-vehicle/get-vehicles/source/vrm/valdate/{valuationDate}")),
            GlassFailure.CandidatesRequest,
            cancellationToken);
        var ordinal = CandidateOrdinal(candidates, natCode);

        // The valuation body is deliberately discarded: Pegasus records no
        // Glass's valuation (D03), and the stage exists only because the
        // provider's own flow performs it before the vehicle is created.
        await TextAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                options.MarketValueAssessor(
                    $"three-phase-vehicle/get-values/vehicle/{ordinal.ToString(CultureInfo.InvariantCulture)}"
                    + $"/source/vrm/valdate/{valuationDate}")),
            ajax: true,
            GlassFailure.ValuationRequest,
            cancellationToken);
        await TextAsync(
            new HttpRequestMessage(HttpMethod.Get, options.MarketValueAssessor("three-phase-vehicle/refresh-vrm-count")),
            ajax: true,
            GlassFailure.RefreshRequest,
            cancellationToken);

        return new(natCode, ordinal);
    }

    /// <summary>
    /// Creates the vehicle inside the Glass's account (stage 11). This is the
    /// first stage that changes the provider's own state, so it is never
    /// retried: a lost answer is reported as unknown and reconciled by a
    /// person.
    /// </summary>
    public async Task<string> CreateVehicleAsync(
        string registration, long mileageMiles, CancellationToken cancellationToken)
    {
        var created = await JsonAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                options.MarketValueAssessor(
                    "index/create-new-vehicle/value/0/valuate/1"
                    + $"/mileage/{mileageMiles.ToString(CultureInfo.InvariantCulture)}"
                    + $"/valdate/{ValuationMonth()}/condition/false")),
            GlassFailure.VehicleRequest,
            cancellationToken,
            // A lost answer here may still have created the vehicle.
            outcomeUnknown: true);

        var vrm = Text(created, "vrm");
        var id = Text(created, "id");
        if (!SameRegistration(vrm, registration)
            || !long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var numeric)
            || numeric <= 0)
        {
            throw new GlassMvaStageException(GlassFailure.VehicleIdentity, outcomeUnknown: true);
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
        string vehicleId, string natCode, CancellationToken cancellationToken)
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
        if (!value.Contains(options.RepairProfileId, StringComparison.Ordinal)
            || !value.Contains(natCode, StringComparison.Ordinal))
        {
            throw new GlassMvaStageException(GlassFailure.DetailsProfile);
        }
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
        Uri originalCallback, string ereId, string rawQuery, CancellationToken cancellationToken)
    {
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
        if (!string.Equals(arguments[7], ereId, StringComparison.Ordinal))
        {
            throw new GlassMvaStageException(GlassFailure.RelayEstimate);
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
            var links = ExportLinks(grid);
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

    /// <summary>Downloads the export, refusing anything past the configured cap (stage 25).</summary>
    public async Task<byte[]> DownloadExportAsync(Uri export, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            new HttpRequestMessage(HttpMethod.Get, export), ajax: false, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new GlassMvaStageException(GlassFailure.DownloadRequest);
        }

        return await ReadAsync(response, options.MaximumExportBytes, GlassFailure.DownloadOversize, cancellationToken);
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

        var callerMatch = CallbackPath().Match(stated["caller"]);
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
    /// Stage 6, the one retryable stage: a fresh lookup that reads and changes
    /// nothing. A readable answer reporting no lookup is retried once after
    /// 250 ms; an unreadable one is not retried at all, because there is
    /// nothing to say it was safe.
    /// </summary>
    private async Task<string> FreshLookupAsync(string search, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var fresh = await JsonAsync(
                new HttpRequestMessage(HttpMethod.Get, options.MarketValueAssessor(search + "/nostocksearch/1")),
                GlassFailure.LookupRequest,
                cancellationToken);
            if (Number(fresh, "vrm_lookup") == 1)
            {
                return Text(fresh, "natcode") is { Length: > 0 } natCode
                    ? natCode
                    : throw new GlassMvaStageException(GlassFailure.LookupUnavailable);
            }
            if (attempt == 2)
            {
                throw new GlassMvaStageException(GlassFailure.LookupUnavailable);
            }

            await Task.Delay(LookupRetryDelay, timeProvider, cancellationToken);
        }
    }

    /// <summary>
    /// Which candidate the lookup's type number names. The provider answers a
    /// success flag and a rendered list, each entry carrying its own type
    /// number; nothing is guessed, so no candidate and more than one candidate
    /// are separate refusals and neither continues.
    /// </summary>
    private static int CandidateOrdinal(JsonElement candidates, string natCode)
    {
        if (Text(candidates, "success") is not "true" and not "True")
        {
            throw new GlassMvaStageException(GlassFailure.CandidatesRefused);
        }

        var html = Text(candidates, "html") ?? string.Empty;
        var matched = 0;
        var ordinal = 0;
        foreach (Match candidate in CandidateBlock().Matches(html))
        {
            var position = int.Parse(candidate.Groups[1].Value, CultureInfo.InvariantCulture);
            var body = html.AsSpan(candidate.Index);
            var next = CandidateBlock().Match(html, candidate.Index + candidate.Length);
            var length = (next.Success ? next.Index : html.Length) - candidate.Index;
            if (body[..length].Contains(natCode, StringComparison.Ordinal))
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

    /// <summary>Every export link the grid offers, as absolute addresses.</summary>
    private List<Uri> ExportLinks(string grid) =>
        [.. ExportLink().Matches(grid)
            .Select(match => Absolute(
                new Uri(WebUtility.HtmlDecode(match.Groups[1].Value), UriKind.RelativeOrAbsolute),
                options.MarketValueAssessorBaseUri))
            .Distinct()];

    /// <summary>
    /// The month Glass's values against, in Europe/London — the provider's own
    /// clock, not the host's, so a machine in another zone asks for the same
    /// month a person at Collision Engineers would.
    /// </summary>
    private string ValuationMonth() =>
        TimeZoneInfo.ConvertTime(
                timeProvider.GetUtcNow(),
                TimeZoneInfo.FindSystemTimeZoneById("Europe/London"))
            .ToString("yyyyMM", CultureInfo.InvariantCulture);

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

        var content = await ReadAsync(response, MaximumTextBytes, failureCode, cancellationToken);
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
        HttpResponseMessage response, int maximumBytes, string failureCode, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maximumBytes)
            {
                throw new GlassMvaStageException(failureCode);
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

    private static long? Number(JsonElement element, string name) =>
        long.TryParse(Text(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    /// <summary>
    /// Whether two registrations are the same plate. Glass's prints its own
    /// spacing and casing, so neither decides identity; nothing else about the
    /// characters is normalised away.
    /// </summary>
    internal static bool SameRegistration(string? left, string? right) =>
        string.Equals(Compact(left), Compact(right), StringComparison.OrdinalIgnoreCase);

    private static string Compact(string? value) =>
        new((value ?? string.Empty).Where(character => !char.IsWhiteSpace(character)).ToArray());

    [GeneratedRegex(
        @"name=""csrf_token""[^>]{0,200}?value=""([0-9a-fA-F]{32})""",
        RegexOptions.CultureInvariant,
        100)]
    private static partial Regex CsrfToken();

    [GeneratedRegex(
        @"^https://[^/]+/ere/ere-callback/ere_id/(\d+)/ere_session/([^/?]+)$",
        RegexOptions.CultureInvariant,
        100)]
    private static partial Regex CallbackPath();

    [GeneratedRegex(
        @"class=""three_phase_car_info car(\d+)""",
        RegexOptions.CultureInvariant,
        100)]
    private static partial Regex CandidateBlock();

    [GeneratedRegex(
        @"href=""([^""]{0,400}?/ndp_download/[^""]{0,200}?\.xml)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        100)]
    private static partial Regex ExportLink();
}
