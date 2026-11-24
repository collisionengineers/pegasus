using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Pegasus.Core;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;

namespace Pegasus.Infrastructure.Experian;

/// <summary>
/// CAP as a connected guide source, valued through Experian (ADR-0089, R1201).
/// Get valuation asks <c>POST /v2/vehicle/</c>, through its own
/// <see cref="ExperianClient"/> and so with the Experian account that holds
/// CAP valuations (R1207), for the registration's CAP valuation at the Case's
/// accepted mileage in whole miles. CAP's <c>retail</c> is the card's retail and its
/// <c>clean</c> the card's trade (R1202). Experian publishes no report of the
/// valuation and the quote names no VIN: the figures are the whole answer
/// (R1205).
///
/// <para>
/// <b>The month is today's.</b> Experian's request carries no valuation date,
/// so CAP is valued as it stands today and every answer is the current London
/// month, whatever month was asked (R1203).
/// </para>
///
/// <para>
/// <b>An answer without CAP's figures is "cannot value".</b> Experian's
/// message 108 is a mileage too high to value and 109 one too low; 107, and
/// any other answer that names the vehicle without CAP's figures, is CAP
/// declining it. The card shows each as information (R1236). Experian
/// answering that it holds no vehicle for the registration says that instead
/// (R1204). Each is logged with Experian's message ids.
/// </para>
///
/// <para>
/// <b>Every other failure is "unavailable".</b> The card says the same
/// approved sentence whatever else stopped the valuation (the account, a
/// permission refusal, Experian, an answer naming no vehicle or the network)
/// and the reason goes to the host log by its failure code and stage.
/// </para>
/// </summary>
public sealed partial class ExperianCapValuationProvider(
    ExperianClient client,
    TimeProvider timeProvider,
    ILogger<ExperianCapValuationProvider> logger) : IGuideValuationProvider
{
    public ValuationSource Source => ValuationSource.Cap;

    public async Task<GuideValuationQuote> GetAsync(
        GuideValuationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var today = LondonCalendar.DateAt(timeProvider.GetUtcNow());
        var guideMonth = new DateOnly(today.Year, today.Month, 1);
        try
        {
            if (!CaseOdometer.TryWholeMiles(request.Mileage, request.MileageUnit, out var miles)
                || miles is < 0 or > int.MaxValue)
            {
                throw new ExperianFailure("mileage", null, "unit=unreadable");
            }

            var (status, body) = await client.QueryVehicleAsync(
                JsonSerializer.SerializeToUtf8Bytes(
                    new CapQuery(request.Registration, (int)miles, new(new(Valuation: true)))),
                cancellationToken);
            using (body)
            {
                if (status == HttpStatusCode.NotFound && ExperianClient.IsVehicleNotFound(body))
                {
                    LogNotFound(logger, request.CaseId, ExperianCapFailureCode.NotFound);
                    throw new GuideValuationNotValuedException(Source, GuideValuationNotValuedReason.NoVehicleData);
                }

                if ((int)status is < 200 or > 299)
                {
                    throw new ExperianFailure("vehicle", status, null);
                }

                decimal retail, trade;
                try
                {
                    (retail, trade) = Figures(body);
                }
                catch (GuideValuationNotValuedException notValued)
                {
                    LogNotValued(
                        logger,
                        request.CaseId,
                        ExperianCapFailureCode.NotValued,
                        notValued.Reason,
                        notValued.InnerException is { } failure ? Detail(failure) : string.Empty);
                    throw;
                }

                return new GuideValuationQuote(retail, trade, guideMonth, request.Mileage);
            }
        }
        catch (Exception exception) when (exception is ExperianFailure
            || (exception is InvalidOperationException and not GuideValuationNotValuedException))
        {
            var code = Code(exception);
            LogUnavailable(
                logger,
                request.CaseId,
                code,
                Detail(exception),
                code == ExperianCapFailureCode.Transport ? exception.InnerException : null);
            throw new GuideValuationProviderUnavailableException(Source, exception);
        }
    }

    /// <summary>
    /// <c>vehicle.valuations.cap.valuation</c>: its <c>retail</c> and its
    /// <c>clean</c> (R1202). An answer that names the vehicle without the
    /// block or without both figures has not valued it: CAP cannot value it,
    /// for the reason <see cref="NotValuedReason"/> reads from Experian's
    /// messages, or it is unavailable when they refuse a permission (R1236).
    /// An answer naming no vehicle is unavailable.
    /// </summary>
    internal static (decimal Retail, decimal Trade) Figures(JsonDocument? body)
    {
        if (body?.RootElement is not { ValueKind: JsonValueKind.Object } root
            || Block(root, "vehicle") is not { } vehicle)
        {
            throw new ExperianFailure("vehicle", null, "vehicle=absent");
        }

        if (Block(vehicle, "valuations") is not { } valuations
            || Block(valuations, "cap") is not { } cap
            || Block(cap, "valuation") is not { } valuation)
        {
            throw NotValuedOrUnavailable(root, "valuation=absent");
        }

        if (Figure(valuation, "retail") is not { } retail || Figure(valuation, "clean") is not { } trade)
        {
            throw NotValuedOrUnavailable(root, "figures=absent");
        }

        return (retail, trade);
    }

    private static Exception NotValuedOrUnavailable(JsonElement root, string detail)
    {
        var failure = new ExperianFailure("vehicle", null, detail + ExperianClient.MessageIds(root));
        return NotValuedReason(ExperianClient.MessageIdList(root)) is { } reason
            ? new GuideValuationNotValuedException(ValuationSource.Cap, reason, failure)
            : failure;
    }

    /// <summary>
    /// Why CAP did not value a vehicle Experian names, from Experian's
    /// message ids (operator, 24 November 2026; R1236): 108, a mileage too
    /// high; 109, one too low; 107 or anything else, CAP declining it; null
    /// for a permission refusal (100) without 107, which stays unavailable.
    /// </summary>
    internal static GuideValuationNotValuedReason? NotValuedReason(IReadOnlyList<string> ids) =>
        ids.Contains("108") ? GuideValuationNotValuedReason.MileageTooHigh
        : ids.Contains("109") ? GuideValuationNotValuedReason.MileageTooLow
        : ids.Contains("107") || !ids.Contains("100") ? GuideValuationNotValuedReason.Declined
        : null;

    private static JsonElement? Block(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value : null;

    private static decimal? Figure(JsonElement valuation, string name) =>
        valuation.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDecimal(out var figure)
        && figure > 0
            ? figure
            : null;

    private static string Code(Exception exception) => exception switch
    {
        ExperianFailure { Stage: "mileage" } => ExperianCapFailureCode.MileageUnit,
        ExperianFailure failure => failure.Status switch
        {
            HttpStatusCode.BadRequest => ExperianCapFailureCode.Rejected,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ExperianCapFailureCode.Forbidden,
            HttpStatusCode.TooManyRequests => ExperianCapFailureCode.Throttled,
            not null => ExperianCapFailureCode.ProviderFailed,
            null when failure.InnerException is not null => ExperianCapFailureCode.Transport,
            null => ExperianCapFailureCode.Unreadable,
        },
        _ => ExperianCapFailureCode.Configuration,
    };

    private static string Detail(Exception exception) => exception switch
    {
        ExperianFailure failure => $"stage={failure.Stage}"
            + (failure.Status is { } status ? $" status={(int)status}" : string.Empty)
            + (failure.Detail is { } detail ? " " + detail : string.Empty),
        // A configuration refusal names its key and nothing secret.
        _ => exception.Message,
    };

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Experian CAP valuation for case {CaseId} was unavailable at {FailureCode} {Detail}")]
    private static partial void LogUnavailable(
        ILogger logger, Guid caseId, string failureCode, string detail, Exception? exception);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Experian CAP valuation for case {CaseId} answered {FailureCode}")]
    private static partial void LogNotFound(ILogger logger, Guid caseId, string failureCode);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Experian CAP valuation for case {CaseId} answered {FailureCode} reason={Reason} {Detail}")]
    private static partial void LogNotValued(
        ILogger logger, Guid caseId, string failureCode, GuideValuationNotValuedReason reason, string detail);

    internal static class ExperianCapFailureCode
    {
        public const string NotFound = "experian.cap.not_found";
        public const string NotValued = "experian.cap.not_valued";
        public const string Rejected = "experian.cap.rejected";
        public const string Forbidden = "experian.cap.forbidden";
        public const string Throttled = "experian.cap.throttled";
        public const string ProviderFailed = "experian.cap.provider_failed";
        public const string Unreadable = "experian.cap.unreadable";
        public const string Transport = "experian.cap.transport";
        public const string Configuration = "experian.cap.configuration";
        public const string MileageUnit = "experian.cap.mileage_unit";
    }

    /// <summary>
    /// The <c>VehicleQueryV2</c> request:
    /// <c>{"vrm":…,"currentmileage":…,"data":{"cap":{"valuation":true}}}</c>.
    /// </summary>
    private sealed record CapQuery(
        [property: JsonPropertyName("vrm")] string Vrm,
        [property: JsonPropertyName("currentmileage")] int CurrentMileage,
        [property: JsonPropertyName("data")] CapQueryData Data);

    private sealed record CapQueryData(
        [property: JsonPropertyName("cap")] CapQueryFlags Cap);

    private sealed record CapQueryFlags(
        [property: JsonPropertyName("valuation")] bool Valuation);
}
