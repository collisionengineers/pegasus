using System.Net;
using Microsoft.Extensions.Logging;
using Pegasus.Core;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;

namespace Pegasus.Infrastructure.Brego;

/// <summary>
/// Brego as a connected guide source (ADR-0084): Get valuation signs in with
/// Collision Engineers' Brego valuation account by renewing its Cognito
/// tokens from the refresh token stored with the account when it was linked
/// (reusing the active BFF session across calls so Brego's single-web-session
/// limit is respected, until the account is replaced or linked again),
/// looks up the Case's registration and queries a point-in-time valuation at
/// its accepted mileage, and answers Brego's <c>retail.high</c> as the card's
/// retail and <c>trade.high</c> as the card's trade. The quote carries the
/// Brego Valuation Report to be generated and filed on the Case in the
/// background, and carries the lookup's unmasked VIN so an empty VIN on the
/// work is filled.
///
/// <para>
/// <b>The month is a date.</b> Brego values on a day. The current London
/// month is valued today; an earlier month on its last day; a later month
/// cannot be valued yet, so it is valued today and answered as the current
/// month (ADR-0084).
/// </para>
///
/// <para>
/// <b>Every failure is "unavailable".</b> The card says the same approved
/// sentence whatever stopped the valuation — the account, the plate, the
/// provider or the network — and the reason goes to the host log by its
/// failure code and stage. The one exception is Brego answering 404 that it
/// holds no data for the registration, which nothing here can fix: the card
/// says that instead. The account is read on each valuation, never when the
/// provider is built, and neither the refresh token nor the session tokens or
/// cookies ever reach a URL or log. An account with no stored refresh token
/// signs in with its saved password, as does one whose refresh token Cognito
/// refuses (R1290); without a password the first is unavailable at stage
/// <c>sign_in</c> without calling Brego and the second at
/// <c>cognito_refresh</c>. A refused password is unavailable at stage
/// <c>password</c>, and a login not set is unavailable as configuration.
/// </para>
/// </summary>
public sealed partial class BregoGuideValuationProvider(
    IHttpClientFactory httpClientFactory,
    Func<CancellationToken, Task<BregoValuationAccount>> account,
    TimeProvider timeProvider,
    ILogger<BregoGuideValuationProvider> logger,
    IBregoSessionStore? sessionStore = null) : IGuideValuationProvider, IDisposable
{
    public const string HttpClientName = "Brego";

    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private BregoSession? _session;

    public ValuationSource Source => ValuationSource.Brego;

    public void Dispose() => _sessionLock.Dispose();

    public async Task<GuideValuationQuote> GetAsync(
        GuideValuationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var today = LondonCalendar.DateAt(timeProvider.GetUtcNow());
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        var guideMonth = request.GuideMonth > currentMonth ? currentMonth : request.GuideMonth;
        var valuationDate = guideMonth < currentMonth ? guideMonth.AddMonths(1).AddDays(-1) : today;
        try
        {
            if (!CaseOdometer.TryWholeMiles(request.Mileage, request.MileageUnit, out var miles))
            {
                throw new BregoFailure("mileage", null, "unit=unreadable");
            }

            var signIn = await account(cancellationToken);
            var client = new BregoClient(httpClientFactory.CreateClient(HttpClientName));
            var vrm = Vrm(request.Registration);
            var (session, canRetry) = await GetSessionAsync(client, signIn, forceRefresh: false, cancellationToken);
            BregoVehicle vehicle;
            BregoFigures figures;
            try
            {
                vehicle = await client.LookupAsync(session, vrm, cancellationToken);
                figures = await client.ValueAsync(session, vehicle, valuationDate, miles, cancellationToken);
            }
            catch (BregoFailure failure) when (canRetry && failure.Status == HttpStatusCode.Unauthorized)
            {
                (session, _) = await GetSessionAsync(client, signIn, forceRefresh: true, cancellationToken, staleSession: session);
                vehicle = await client.LookupAsync(session, vrm, cancellationToken);
                figures = await client.ValueAsync(session, vehicle, valuationDate, miles, cancellationToken);
            }

            return new GuideValuationQuote(figures.Retail, figures.Trade, guideMonth, request.Mileage)
            {
                Report = new BregoValuationReport(
                    this,
                    client,
                    signIn,
                    session,
                    Guid.NewGuid().ToString("D"),
                    vehicle,
                    miles),
                Vin = GlassVinFillPolicy.Carried(vehicle.Vin),
            };
        }
        catch (BregoFailure failure) when (Code(failure) == BregoFailureCode.NotFound)
        {
            LogUnavailable(logger, request.CaseId, guideMonth, BregoFailureCode.NotFound, Detail(failure), null);
            throw new GuideValuationNotValuedException(Source, GuideValuationNotValuedReason.NoVehicleData, failure);
        }
        catch (Exception exception) when (Unavailable(exception))
        {
            var code = Code(exception);
            LogUnavailable(
                logger,
                request.CaseId,
                guideMonth,
                code,
                Detail(exception),
                code == BregoFailureCode.Transport ? exception : null);
            throw new GuideValuationProviderUnavailableException(Source, exception);
        }
    }

    internal async Task<(BregoSession Session, bool CanRetryOnUnauthorized)> GetSessionAsync(
        BregoClient client,
        BregoValuationAccount signIn,
        bool forceRefresh,
        CancellationToken cancellationToken,
        BregoSession? staleSession = null)
    {
        var current = Volatile.Read(ref _session);
        if (!forceRefresh && IsReusable(current, signIn))
        {
            return (current!, true);
        }

        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            current = _session;
            if ((!forceRefresh || (staleSession is not null && !ReferenceEquals(current, staleSession)))
                && IsReusable(current, signIn))
            {
                return (current!, true);
            }

            // The refresh token and cookie come from the account first: it is
            // read with its stored sign-in on each valuation, so a link made
            // since this host signed in is used without a restart. Only when
            // the account holds none does the session this host already holds
            // for the same generation supply them. A 401 from the platform
            // mints a new session: the cookie is left behind, the refresh
            // token is not.
            string? refreshToken = null;
            string? existingSessionCookie = null;
            if (!string.IsNullOrWhiteSpace(signIn.RefreshToken))
            {
                refreshToken = signIn.RefreshToken;
                existingSessionCookie = signIn.SessionCookie;
            }
            else if (current is not null
                && current.Generation == signIn.Generation
                && string.Equals(current.Email, signIn.Email, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(current.RefreshToken))
            {
                refreshToken = current.RefreshToken;
                existingSessionCookie = current.BregoSessionCookie;
            }

            // With no usable sign-in, an account with a saved password signs
            // in with it (R1290); one without is linked on the Integrations
            // page, and its valuations are unavailable until it is.
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                if (signIn.Password is null)
                {
                    throw new BregoFailure("sign_in", null, "refresh_token=absent");
                }

                return (await PasswordSessionAsync(client, signIn, cancellationToken), false);
            }

            BregoSession signedIn;
            try
            {
                signedIn = await client.SignInAsync(
                    signIn.Email,
                    refreshToken,
                    timeProvider,
                    cancellationToken,
                    forceRefresh ? null : existingSessionCookie);
            }
            catch (BregoFailure failure) when (failure.Stage == "cognito_refresh"
                && failure.Forbidden
                && signIn.Password is not null)
            {
                return (await PasswordSessionAsync(client, signIn, cancellationToken), false);
            }

            var created = signedIn with { Generation = signIn.Generation };
            if (sessionStore is not null
                && (!created.ReusedSessionCookie
                    || !string.Equals(created.RefreshToken, refreshToken, StringComparison.Ordinal)))
            {
                await sessionStore.TrySaveAsync(
                    signIn.Generation,
                    new BregoStoredSignIn(created.BregoSessionCookie, created.RefreshToken),
                    cancellationToken);
            }

            Volatile.Write(ref _session, created);
            return (created, created.ReusedSessionCookie);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    /// <summary>
    /// Signs in with the account's saved password (R1290) on a device Brego
    /// has approved, mints a new <c>brego-session</c> and stores the sign-in
    /// for the next press. A device Brego wants a code for is unavailable at
    /// <c>device_approval</c>: the code is entered on the Integrations page,
    /// never here, and nothing of this sign-in is kept.
    /// </summary>
    private async Task<BregoSession> PasswordSessionAsync(
        BregoClient client,
        BregoValuationAccount signIn,
        CancellationToken cancellationToken)
    {
        var tokens = await client.PasswordSignInAsync(signIn.Email, signIn.Password!, timeProvider, cancellationToken);
        if (!await client.IsDeviceApprovedAsync(tokens.AccessToken, cancellationToken))
        {
            throw new BregoFailure("device_approval", null, "device=challenge_required");
        }

        var minted = await client.MintSessionAsync(signIn.Email, tokens, timeProvider, cancellationToken);
        var created = minted with { Generation = signIn.Generation };
        if (sessionStore is not null)
        {
            await sessionStore.TrySaveAsync(
                signIn.Generation,
                new BregoStoredSignIn(created.BregoSessionCookie, created.RefreshToken),
                cancellationToken);
        }

        Volatile.Write(ref _session, created);
        return created;
    }

    /// <summary>
    /// A session this host holds serves the next press only while it is for
    /// the account's current generation: a replaced or newly linked account is
    /// used on the next press, not when the old session expires (R1288).
    /// </summary>
    private bool IsReusable(BregoSession? session, BregoValuationAccount signIn) =>
        session is not null
        && session.Generation == signIn.Generation
        && string.Equals(session.Email, signIn.Email, StringComparison.OrdinalIgnoreCase)
        && timeProvider.GetUtcNow() < session.ExpiresAtUtc;

    /// <summary>
    /// A registration as Brego looks it up: upper case, no spaces.
    /// </summary>
    internal static string Vrm(string registration)
    {
        Span<char> compact = registration.Length <= StackBufferLength
            ? stackalloc char[registration.Length]
            : new char[registration.Length];
        var length = 0;
        var changed = false;
        foreach (var character in registration)
        {
            if (char.IsWhiteSpace(character))
            {
                changed = true;
                continue;
            }

            var upper = char.ToUpperInvariant(character);
            changed |= upper != character;
            compact[length++] = upper;
        }

        return changed ? new string(compact[..length]) : registration;
    }

    private const int StackBufferLength = 128;

    private static bool Unavailable(Exception exception) =>
        exception is BregoFailure or InvalidOperationException;

    private static string Code(Exception exception) => exception switch
    {
        BregoFailure failure => Code(failure),
        _ => BregoFailureCode.Configuration,
    };

    private static string Code(BregoFailure failure) => (failure.Stage, failure.Status) switch
    {
        ("mileage", _) => BregoFailureCode.MileageUnit,
        ("device_approval", null) when !failure.Transport => BregoFailureCode.DeviceApproval,
        ("lookup" or "valuation", HttpStatusCode.NotFound) => BregoFailureCode.NotFound,
        (_, _) when failure.Forbidden => BregoFailureCode.Forbidden,
        (_, HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) => BregoFailureCode.Forbidden,
        (_, HttpStatusCode.BadRequest) => BregoFailureCode.Rejected,
        (_, HttpStatusCode.TooManyRequests) => BregoFailureCode.Throttled,
        (_, not null) => BregoFailureCode.ProviderFailed,
        (_, null) when failure.Transport => BregoFailureCode.Transport,
        (_, null) => BregoFailureCode.Unreadable,
    };

    private static string? Detail(Exception exception) => exception switch
    {
        BregoFailure failure => $"stage={failure.Stage} {failure.Detail}",
        InvalidOperationException configuration => configuration.Message,
        _ => exception.GetType().Name,
    };

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Brego valuation for case {CaseId} in {GuideMonth} was unavailable at {FailureCode} {Detail}")]
    private static partial void LogUnavailable(
        ILogger logger, Guid caseId, DateOnly guideMonth, string failureCode, string? detail, Exception? exception);

    internal static class BregoFailureCode
    {
        public const string NotFound = "brego.valuation.not_found";
        public const string Rejected = "brego.valuation.rejected";
        public const string Forbidden = "brego.valuation.forbidden";
        public const string Throttled = "brego.valuation.throttled";
        public const string ProviderFailed = "brego.valuation.provider_failed";
        public const string Unreadable = "brego.valuation.unreadable";
        public const string DeviceApproval = "brego.valuation.device_approval";
        public const string Transport = "brego.valuation.transport";
        public const string Configuration = "brego.valuation.configuration";
        public const string MileageUnit = "brego.valuation.mileage_unit";
    }
}
