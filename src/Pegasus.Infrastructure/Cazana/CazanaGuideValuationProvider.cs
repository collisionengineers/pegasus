using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pegasus.Core;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;

namespace Pegasus.Infrastructure.Cazana;

/// <summary>
/// Cazana as a connected guide source (ADR-0079): Get valuation signs in with
/// the Cazana Companion account, runs one metered Companion lookup for the
/// Case's registration at its accepted mileage, and answers Cazana's retail as
/// the card's retail and its trade (<c>remarket</c>) as the card's trade. The
/// quote carries the lookup's Companion Report to be filed on the Case in the
/// background over the same signed-in session, and carries the lookup's
/// unmasked VIN so an empty VIN on the work is filled.
///
/// <para>
/// <b>The month is a date.</b> Cazana values on a day. The current London
/// month is valued today; an earlier month on its last day; a later month
/// cannot be valued yet, so it is valued today and answered as the current
/// month (operator, 9 October 2026).
/// </para>
///
/// <para>
/// <b>Every failure is "unavailable".</b> The card says the same approved
/// sentence whatever stopped the valuation — the account, the plate, the
/// provider or the network — and the reason goes to the host log by its
/// failure code and stage. The one exception is Cazana answering that it holds
/// no data for the registration, which nothing here can fix: the card says
/// that instead (operator, 9 October 2026). The account is read on each
/// valuation, never when the provider is built, and neither the credentials
/// nor the session token ever reaches a URL or log.
/// </para>
/// </summary>
public sealed partial class CazanaGuideValuationProvider(
    IHttpClientFactory httpClientFactory,
    Func<CazanaCompanionAccount> account,
    TimeProvider timeProvider,
    ILogger<CazanaGuideValuationProvider> logger) : IGuideValuationProvider
{
    public const string HttpClientName = "Cazana";

    public ValuationSource Source => ValuationSource.Cazana;

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
                throw new CazanaCompanionFailure("mileage", null, "unit=unreadable");
            }

            var signIn = account();
            var client = new CazanaCompanionClient(httpClientFactory.CreateClient(HttpClientName));
            var authorization = await client.SignInAsync(signIn.Email, signIn.Password, cancellationToken);
            var uuid = await client.LookupAsync(authorization, Vrm(request.Registration), valuationDate, miles, cancellationToken);
            var response = await client.ReadAsync(authorization, uuid, cancellationToken);
            var valuation = CazanaJson.Get(response, "valuation");
            if (Figure(valuation, "retail") is not { } retail
                || Figure(valuation, "remarket") is not { } trade)
            {
                throw new CazanaCompanionFailure("read", null, "figures=absent");
            }

            // The VIN is Core's to judge (GlassVinFillPolicy): a masked or
            // short VIN carries as none and never opens the fill.
            return new GuideValuationQuote(retail, trade, guideMonth, request.Mileage)
            {
                Report = new CazanaCompanionReport(client, authorization, uuid, response, timeProvider),
                Vin = GlassVinFillPolicy.Carried(CazanaJson.String(valuation, "vin")),
            };
        }
        catch (CazanaCompanionFailure failure) when (Code(failure) == CazanaFailureCode.NotFound)
        {
            LogUnavailable(logger, request.CaseId, guideMonth, CazanaFailureCode.NotFound, Detail(failure), null);
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
                code == CazanaFailureCode.Transport ? exception : null);
            throw new GuideValuationProviderUnavailableException(Source, exception);
        }
    }

    /// <summary>
    /// A registration as Cazana looks it up: upper case, no spaces. One pass
    /// into a stack buffer and one new string, or the registration itself
    /// when it is already compact and upper case.
    /// </summary>
    internal static string Vrm(string registration)
    {
        Span<char> compact = registration.Length <= StackBufferLength
            ? stackalloc char[registration.Length]
            : new char[registration.Length];
        var length = 0;
        foreach (var character in registration)
        {
            if (!char.IsWhiteSpace(character))
            {
                compact[length++] = char.ToUpperInvariant(character);
            }
        }

        compact = compact[..length];
        return compact.SequenceEqual(registration.AsSpan()) ? registration : new string(compact);
    }

    private const int StackBufferLength = 128;

    private static decimal? Figure(JsonElement? valuation, string name) =>
        CazanaJson.Get(valuation, name) is { ValueKind: JsonValueKind.Number } value
        && value.TryGetDecimal(out var figure)
        && figure > 0
            ? figure
            : null;

    /// <summary>
    /// Whatever Cazana, the account or the network did, each of which the
    /// client names by its stage (a request that fails or a call that runs past
    /// its bound is the stage's transport failure) — never the caller giving
    /// up, which is the caller's own outcome and passes through.
    /// </summary>
    private static bool Unavailable(Exception exception) =>
        // An InvalidOperationException is a missing or unresolved account
        // setting: the reason is logged by key, never shown on the card.
        exception is CazanaCompanionFailure or InvalidOperationException;

    private static string Code(Exception exception) => exception switch
    {
        CazanaCompanionFailure failure => Code(failure),
        _ => CazanaFailureCode.Configuration,
    };

    private static string Code(CazanaCompanionFailure failure) => (failure.Stage, failure.Status) switch
    {
        ("mileage", _) => CazanaFailureCode.MileageUnit,
        ("branch", _) => CazanaFailureCode.Branch,
        ("lookup" or "read", HttpStatusCode.NotFound) => CazanaFailureCode.NotFound,
        (_, HttpStatusCode.BadRequest) => CazanaFailureCode.Rejected,
        (_, HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) => CazanaFailureCode.Forbidden,
        (_, HttpStatusCode.TooManyRequests) => CazanaFailureCode.Throttled,
        (_, not null) => CazanaFailureCode.ProviderFailed,
        (_, null) when failure.Transport => CazanaFailureCode.Transport,
        (_, null) => CazanaFailureCode.Unreadable,
    };

    private static string? Detail(Exception exception) => exception switch
    {
        CazanaCompanionFailure failure => $"stage={failure.Stage} {failure.Detail}",
        // A configuration refusal names its key and nothing secret.
        InvalidOperationException configuration => configuration.Message,
        _ => exception.GetType().Name,
    };

    /// <summary>
    /// The exception rides along only for a transport failure, where the code
    /// and stage cannot say what the network did.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Cazana valuation for case {CaseId} in {GuideMonth} was unavailable at {FailureCode} {Detail}")]
    private static partial void LogUnavailable(
        ILogger logger, Guid caseId, DateOnly guideMonth, string failureCode, string? detail, Exception? exception);

    internal static class CazanaFailureCode
    {
        public const string NotFound = "cazana.valuation.not_found";
        public const string Rejected = "cazana.valuation.rejected";
        public const string Forbidden = "cazana.valuation.forbidden";
        public const string Throttled = "cazana.valuation.throttled";
        public const string ProviderFailed = "cazana.valuation.provider_failed";
        public const string Unreadable = "cazana.valuation.unreadable";
        public const string Branch = "cazana.valuation.branch";
        public const string Transport = "cazana.valuation.transport";
        public const string Configuration = "cazana.valuation.configuration";
        public const string MileageUnit = "cazana.valuation.mileage_unit";
    }
}
