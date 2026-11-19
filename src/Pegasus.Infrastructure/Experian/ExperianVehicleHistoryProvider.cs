using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Vehicle;

namespace Pegasus.Infrastructure.Experian;

/// <summary>
/// Experian as the connected vehicle history check (ADR-0089). Run Experian
/// check asks <c>POST /v2/vehicle/</c>, through the shared
/// <see cref="ExperianClient"/>, for the registration's condition (write-off)
/// markers only (R741), and answers the markers the insurer has not removed
/// (R743).
///
/// <para>
/// <b>Every failure is "unavailable".</b> The page says the same approved
/// sentence whatever stopped the check — the account, Experian or the
/// network — and the reason goes to the host log by its failure code and
/// stage. The one exception is Experian answering that it holds no vehicle
/// for the registration, which is its own answer (R748).
/// </para>
/// </summary>
public sealed partial class ExperianVehicleHistoryProvider(
    ExperianClient client,
    ILogger<ExperianVehicleHistoryProvider> logger) : IVehicleHistoryCheckProvider
{
    public async Task<VehicleHistoryCheckResult> CheckAsync(string registration, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registration);
        try
        {
            // A Custom Search for the condition block alone (R741).
            var (status, body) = await client.QueryVehicleAsync(
                JsonSerializer.SerializeToUtf8Bytes(
                    new VehicleQuery(registration, new VehicleQueryData(Condition: true))),
                cancellationToken);

            using (body)
            {
                if (status == HttpStatusCode.NotFound && ExperianClient.IsVehicleNotFound(body))
                {
                    LogNotFound(logger, ExperianFailureCode.NotFound);
                    return VehicleHistoryCheckResult.NotFound;
                }

                if ((int)status is < 200 or > 299)
                {
                    throw new ExperianFailure("vehicle", status, null);
                }

                return VehicleHistoryCheckResult.Found(WriteOffs(body));
            }
        }
        catch (Exception exception) when (exception is ExperianFailure or InvalidOperationException)
        {
            var code = Code(exception);
            LogUnavailable(
                logger,
                code,
                Detail(exception),
                code == ExperianFailureCode.Transport ? exception.InnerException : null);
            throw new VehicleHistoryCheckUnavailableException(exception);
        }
    }

    /// <summary>
    /// <c>vehicle.provenance.condition[]</c>, without the records the insurer
    /// has removed (R743). Category is <c>losstype</c>, else <c>status</c>.
    /// </summary>
    internal static IReadOnlyList<VehicleWriteOffRecord> WriteOffs(JsonDocument? body)
    {
        if (body?.RootElement is not { ValueKind: JsonValueKind.Object } root
            || !root.TryGetProperty("vehicle", out var vehicle)
            || vehicle.ValueKind != JsonValueKind.Object)
        {
            throw new ExperianFailure("vehicle", null, "vehicle=absent");
        }

        if (!vehicle.TryGetProperty("provenance", out var provenance)
            || provenance.ValueKind != JsonValueKind.Object
            || !provenance.TryGetProperty("condition", out var conditions)
            || conditions.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var records = new List<VehicleWriteOffRecord>();
        foreach (var condition in conditions.EnumerateArray())
        {
            if (condition.ValueKind != JsonValueKind.Object
                || !string.IsNullOrWhiteSpace(Text(condition, "dateremoved")))
            {
                continue;
            }

            records.Add(new(
                Text(condition, "losstype") ?? Text(condition, "status"),
                Cause(Text(condition, "causeofdamage")),
                Date(Text(condition, "lossdate")),
                Text(condition, "insurername"),
                Text(condition, "claimnumber")));
        }

        return records;
    }

    private static string? Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    /// <summary>The schema's <c>causeofdamage</c> codes in its own words; any other value as sent.</summary>
    private static string? Cause(string? code) => code?.ToUpperInvariant() switch
    {
        null => null,
        "F" => "Fire",
        "A" => "Accident",
        "W" => "Water",
        "V" => "Vandalism",
        "U" => "Unknown",
        _ => code,
    };

    /// <summary>The calendar date as Experian wrote it, whatever time or offset follows.</summary>
    private static DateOnly? Date(string? value) =>
        value is { Length: >= 10 }
        && DateOnly.TryParseExact(value[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    private static string Code(Exception exception) => exception switch
    {
        ExperianFailure failure => failure.Status switch
        {
            HttpStatusCode.BadRequest => ExperianFailureCode.Rejected,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ExperianFailureCode.Forbidden,
            HttpStatusCode.NotFound => ExperianFailureCode.ProviderFailed,
            HttpStatusCode.TooManyRequests => ExperianFailureCode.Throttled,
            not null => ExperianFailureCode.ProviderFailed,
            null when failure.InnerException is not null => ExperianFailureCode.Transport,
            null => ExperianFailureCode.Unreadable,
        },
        _ => ExperianFailureCode.Configuration,
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
        Message = "Experian vehicle history check was unavailable at {FailureCode} {Detail}")]
    private static partial void LogUnavailable(ILogger logger, string failureCode, string detail, Exception? exception);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Experian vehicle history check answered {FailureCode}")]
    private static partial void LogNotFound(ILogger logger, string failureCode);

    internal static class ExperianFailureCode
    {
        public const string NotFound = "experian.history.not_found";
        public const string Rejected = "experian.history.rejected";
        public const string Forbidden = "experian.history.forbidden";
        public const string Throttled = "experian.history.throttled";
        public const string ProviderFailed = "experian.history.provider_failed";
        public const string Unreadable = "experian.history.unreadable";
        public const string Transport = "experian.history.transport";
        public const string Configuration = "experian.history.configuration";
    }

    /// <summary>The <c>VehicleQueryV2</c> request: <c>{"vrm":…,"data":{"condition":true}}</c>.</summary>
    private sealed record VehicleQuery(
        [property: System.Text.Json.Serialization.JsonPropertyName("vrm")] string Vrm,
        [property: System.Text.Json.Serialization.JsonPropertyName("data")] VehicleQueryData Data);

    private sealed record VehicleQueryData(
        [property: System.Text.Json.Serialization.JsonPropertyName("condition")] bool Condition);
}
