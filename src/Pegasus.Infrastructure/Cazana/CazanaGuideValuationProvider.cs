using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pegasus.Core;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;

namespace Pegasus.Infrastructure.Cazana;

/// <summary>
/// Cazana as a connected guide source (ADR-0066): Get valuation asks Cazana's
/// valuation API for the Case's registration at its accepted mileage and
/// answers Cazana's retail as the card's retail and its trade as the card's
/// trade. Cazana offers no report and its valuation names no VIN, so the quote
/// carries neither.
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
/// sentence whatever stopped the valuation — the key, the plate, the provider
/// or the network — and the reason goes to the host log by its failure code.
/// The one exception is Cazana answering that it holds no data for the
/// registration, which nothing here can fix: the card says that instead
/// (operator, 9 October 2026). The key is read on each valuation, never when
/// the provider is built, and travels only in the Authorization header so it
/// never reaches a logged or traced URL.
/// </para>
/// </summary>
public sealed partial class CazanaGuideValuationProvider(
    IHttpClientFactory httpClientFactory,
    Func<CazanaApiKey> apiKey,
    TimeProvider timeProvider,
    ILogger<CazanaGuideValuationProvider> logger) : IGuideValuationProvider
{
    public const string HttpClientName = "Cazana";

    internal static readonly Uri BaseUri = new("https://api.cazana.com/");

    public ValuationSource Source => ValuationSource.Cazana;

    public async Task<GuideValuationQuote> GetAsync(
        GuideValuationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var today = LondonCalendar.DateAt(timeProvider.GetUtcNow());
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        var guideMonth = request.GuideMonth > currentMonth ? currentMonth : request.GuideMonth;
        DateOnly? valuationDate = guideMonth < currentMonth ? guideMonth.AddMonths(1).AddDays(-1) : null;
        try
        {
            if (!CaseOdometer.TryWholeMiles(request.Mileage, request.MileageUnit, out var miles))
            {
                throw new CazanaFailure(CazanaFailureCode.MileageUnit, "unit=unreadable");
            }

            var key = apiKey();
            var figures = await ValueAsync(key, request.Registration, miles, valuationDate, cancellationToken);
            return new GuideValuationQuote(figures.Retail, figures.Trade, guideMonth, request.Mileage);
        }
        catch (CazanaFailure failure) when (failure.Code == CazanaFailureCode.NotFound)
        {
            LogUnavailable(logger, request.CaseId, guideMonth, failure.Code, failure.Detail);
            throw new GuideValuationNotValuedException(Source, GuideValuationNotValuedReason.NoVehicleData, failure);
        }
        catch (Exception exception) when (Unavailable(exception, cancellationToken))
        {
            LogUnavailable(logger, request.CaseId, guideMonth, Code(exception), Detail(exception));
            throw new GuideValuationProviderUnavailableException(Source, exception);
        }
    }

    private async Task<(decimal Retail, decimal Trade)> ValueAsync(
        CazanaApiKey key,
        string registration,
        long miles,
        DateOnly? valuationDate,
        CancellationToken cancellationToken)
    {
        var query = "valuation/1.0?vrm=" + Uri.EscapeDataString(Vrm(registration))
            + "&mileage=" + miles.ToString(CultureInfo.InvariantCulture);
        if (valuationDate is { } date)
        {
            query += "&date=" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        using var message = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUri, query));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Value);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await httpClientFactory.CreateClient(HttpClientName)
            .SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new CazanaFailure(StatusCode(response.StatusCode), $"status={(int)response.StatusCode} {ErrorDetail(body)}");
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("valuation", out var valuation)
                && valuation.ValueKind == JsonValueKind.Object
                && Figure(valuation, "retail") is { } retail
                && Figure(valuation, "trade") is { } trade)
            {
                return (retail, trade);
            }
        }
        catch (JsonException)
        {
            throw new CazanaFailure(CazanaFailureCode.Unreadable, "json=invalid");
        }

        throw new CazanaFailure(CazanaFailureCode.Unreadable, "figures=absent");
    }

    /// <summary>A registration as Cazana looks it up: upper case, no spaces.</summary>
    internal static string Vrm(string registration) =>
        string.Concat(registration.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();

    private static decimal? Figure(JsonElement valuation, string name) =>
        valuation.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDecimal(out var figure)
        && figure > 0
            ? figure
            : null;

    private static string StatusCode(HttpStatusCode status) => status switch
    {
        HttpStatusCode.NotFound => CazanaFailureCode.NotFound,
        HttpStatusCode.BadRequest => CazanaFailureCode.Rejected,
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => CazanaFailureCode.Forbidden,
        HttpStatusCode.TooManyRequests => CazanaFailureCode.Throttled,
        _ => CazanaFailureCode.ProviderFailed,
    };

    /// <summary>
    /// Cazana's own words for a refusal: its message and any detail strings
    /// (a 403 answers a bare string). Never more than a short line.
    /// </summary>
    private static string ErrorDetail(string body)
    {
        var words = new List<string>();
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                {
                    words.Add(error.GetString()!);
                }
                else if (error.ValueKind == JsonValueKind.Object)
                {
                    if (error.TryGetProperty("message", out var text) && text.ValueKind == JsonValueKind.String)
                    {
                        words.Add(text.GetString()!);
                    }

                    if (error.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                    {
                        words.AddRange(data.EnumerateArray()
                            .Where(item => item.ValueKind == JsonValueKind.String)
                            .Select(item => item.GetString()!));
                    }
                }
            }
        }
        catch (JsonException)
        {
            return "error=unreadable";
        }

        var detail = "error=" + string.Join("; ", words);
        return detail.Length <= 200 ? detail : detail[..200];
    }

    /// <summary>
    /// Whatever Cazana, the key or the network did — never the caller giving
    /// up, which is the caller's own outcome.
    /// </summary>
    private static bool Unavailable(Exception exception, CancellationToken cancellationToken) =>
        exception switch
        {
            CazanaFailure or HttpRequestException or TimeoutException => true,
            // A missing or unresolved key: the reason is logged by key,
            // never shown on the card.
            InvalidOperationException => true,
            OperationCanceledException => !cancellationToken.IsCancellationRequested,
            _ => false,
        };

    private static string Code(Exception exception) => exception switch
    {
        CazanaFailure failure => failure.Code,
        HttpRequestException or OperationCanceledException or TimeoutException => CazanaFailureCode.Transport,
        _ => CazanaFailureCode.Configuration,
    };

    private static string? Detail(Exception exception) => exception switch
    {
        CazanaFailure failure => failure.Detail,
        // A configuration refusal names its key and nothing secret.
        InvalidOperationException configuration => configuration.Message,
        _ => exception.GetType().Name,
    };

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Cazana valuation for case {CaseId} in {GuideMonth} was unavailable at {FailureCode} {Detail}")]
    private static partial void LogUnavailable(
        ILogger logger, Guid caseId, DateOnly guideMonth, string failureCode, string? detail);

    internal static class CazanaFailureCode
    {
        public const string NotFound = "cazana.valuation.not_found";
        public const string Rejected = "cazana.valuation.rejected";
        public const string Forbidden = "cazana.valuation.forbidden";
        public const string Throttled = "cazana.valuation.throttled";
        public const string ProviderFailed = "cazana.valuation.provider_failed";
        public const string Unreadable = "cazana.valuation.unreadable";
        public const string Transport = "cazana.valuation.transport";
        public const string Configuration = "cazana.valuation.configuration";
        public const string MileageUnit = "cazana.valuation.mileage_unit";
    }

    private sealed class CazanaFailure(string code, string detail) : Exception(code)
    {
        public string Code { get; } = code;

        public string Detail { get; } = detail;
    }
}
