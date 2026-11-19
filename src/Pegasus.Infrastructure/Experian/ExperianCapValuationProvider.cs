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
/// <b>Every failure is "unavailable".</b> The card says the same approved
/// sentence whatever stopped the valuation (the account, Experian, an answer
/// without CAP's figures or the network) and the reason goes to the host log
/// by its failure code and stage. The one exception is Experian answering
/// that it holds no vehicle for the registration: the card says that instead
/// (R1204).
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

                var (retail, trade) = Figures(body);
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
    /// <c>clean</c> (R1202). An answer without the block or without both
    /// figures has not valued the vehicle, so it is unavailable; the log names
    /// Experian's message ids.
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
            throw new ExperianFailure("vehicle", null, "valuation=absent" + ExperianClient.MessageIds(root));
        }

        if (Figure(valuation, "retail") is not { } retail || Figure(valuation, "clean") is not { } trade)
        {
            throw new ExperianFailure("vehicle", null, "figures=absent" + ExperianClient.MessageIds(root));
        }

        return (retail, trade);
    }

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

    internal static class ExperianCapFailureCode
    {
        public const string NotFound = "experian.cap.not_found";
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
