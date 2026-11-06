using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Vehicle;

namespace Pegasus.Infrastructure.Experian;

/// <summary>
/// Experian as the connected vehicle history check (ADR-0089). Run Experian
/// check signs in with the Experian account (OAuth2 password grant), asks
/// <c>POST /v2/vehicle/</c> for the registration's condition (write-off)
/// markers only (R741), and answers the markers the insurer has not removed
/// (R743).
///
/// <para>
/// <b>Every failure is "unavailable".</b> The page says the same approved
/// sentence whatever stopped the check — the account, Experian or the
/// network — and the reason goes to the host log by its failure code and
/// stage. The one exception is Experian answering that it holds no vehicle
/// for the registration, which is its own answer (R748). The account is read
/// on each check, never when the provider is built; neither the credentials
/// nor the token ever reaches a URL or log, and the registration travels in
/// the body.
/// </para>
/// </summary>
public sealed partial class ExperianVehicleHistoryProvider(
    IHttpClientFactory httpClientFactory,
    Func<ExperianAccount> account,
    TimeProvider timeProvider,
    ILogger<ExperianVehicleHistoryProvider> logger) : IVehicleHistoryCheckProvider, IDisposable
{
    public const string HttpClientName = "Experian";

    /// <summary>Each call's one bound, headers and body together (the named client sets none).</summary>
    internal static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim tokenLock = new(1, 1);
    private string? token;
    private DateTimeOffset tokenExpiresAtUtc;

    public async Task<VehicleHistoryCheckResult> CheckAsync(string registration, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registration);
        try
        {
            var signIn = account();
            var http = httpClientFactory.CreateClient(HttpClientName);
            var bearer = await TokenAsync(http, signIn, refresh: false, cancellationToken);
            var (status, body) = await QueryAsync(http, signIn, bearer, registration, cancellationToken);
            if (status == HttpStatusCode.Unauthorized)
            {
                // A token Experian has withdrawn early is renewed once.
                body?.Dispose();
                bearer = await TokenAsync(http, signIn, refresh: true, cancellationToken);
                (status, body) = await QueryAsync(http, signIn, bearer, registration, cancellationToken);
            }

            using (body)
            {
                if (status == HttpStatusCode.NotFound && IsVehicleNotFound(body))
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

    public void Dispose() => tokenLock.Dispose();

    /// <summary>
    /// The bearer token, from the in-process cache while it has a minute left,
    /// else a fresh sign-in. Experian's gateway takes the password grant as a
    /// JSON body with a <c>Grant_type</c> header.
    /// </summary>
    private async Task<string> TokenAsync(
        HttpClient http,
        ExperianAccount signIn,
        bool refresh,
        CancellationToken cancellationToken)
    {
        await tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (!refresh && token is not null && tokenExpiresAtUtc > timeProvider.GetUtcNow().AddMinutes(1))
            {
                return token;
            }

            var message = new HttpRequestMessage(HttpMethod.Post, signIn.TokenUri)
            {
                Content = JsonContent(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string>
                {
                    ["username"] = signIn.Username,
                    ["password"] = signIn.Password,
                    ["client_id"] = signIn.ClientId,
                    ["client_secret"] = signIn.ClientSecret,
                })),
            };
            message.Headers.TryAddWithoutValidation("Grant_type", "password");
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var (status, body) = await SendAsync("token", http, message, cancellationToken);
            using (body)
            {
                if ((int)status is < 200 or > 299)
                {
                    throw new ExperianFailure("token", status, null);
                }

                if (body?.RootElement is not { ValueKind: JsonValueKind.Object } root
                    || !root.TryGetProperty("access_token", out var accessToken)
                    || accessToken.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(accessToken.GetString())
                    || ExpiresInSeconds(root) is not { } seconds)
                {
                    throw new ExperianFailure("token", null, "token=malformed");
                }

                token = accessToken.GetString()!;
                tokenExpiresAtUtc = timeProvider.GetUtcNow().AddSeconds(seconds);
                return token;
            }
        }
        finally
        {
            tokenLock.Release();
        }
    }

    private static Task<(HttpStatusCode Status, JsonDocument? Body)> QueryAsync(
        HttpClient http,
        ExperianAccount signIn,
        string bearer,
        string registration,
        CancellationToken cancellationToken)
    {
        // A Custom Search for the condition block alone (R741).
        var message = new HttpRequestMessage(HttpMethod.Post, VehicleUri(signIn.BaseUri))
        {
            Content = JsonContent(JsonSerializer.SerializeToUtf8Bytes(
                new VehicleQuery(registration, new VehicleQueryData(Condition: true)))),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return SendAsync("vehicle", http, message, cancellationToken);
    }

    /// <summary>
    /// One call, named by its stage: the status and the parsed body (none when
    /// the body is empty or not JSON on a refusal). <see cref="CallTimeout"/>
    /// bounds the call; the caller's own cancellation passes through.
    /// </summary>
    private static async Task<(HttpStatusCode Status, JsonDocument? Body)> SendAsync(
        string stage,
        HttpClient http,
        HttpRequestMessage message,
        CancellationToken cancellationToken)
    {
        using (message)
        using (var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            bounded.CancelAfter(CallTimeout);
            try
            {
                using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, bounded.Token);
                var bytes = await response.Content.ReadAsByteArrayAsync(bounded.Token);
                var succeeded = (int)response.StatusCode is >= 200 and <= 299;
                JsonDocument? body = null;
                if (bytes.Length > 0)
                {
                    try
                    {
                        body = JsonDocument.Parse(bytes);
                    }
                    catch (JsonException) when (!succeeded)
                    {
                        // A refusal's body is read only for the not-found answer.
                    }
                }

                return (response.StatusCode, body);
            }
            catch (JsonException)
            {
                throw new ExperianFailure(stage, null, "json=invalid");
            }
            catch (IOException exception)
            {
                throw new ExperianFailure(stage, null, "body=unreadable", exception);
            }
            catch (HttpRequestException exception)
            {
                throw new ExperianFailure(stage, null, "transport", exception);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ExperianFailure(stage, null, "transport", exception);
            }
        }
    }

    private static ByteArrayContent JsonContent(byte[] body)
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private static Uri VehicleUri(Uri baseUri)
    {
        var root = baseUri.AbsoluteUri.EndsWith('/') ? baseUri.AbsoluteUri : baseUri.AbsoluteUri + "/";
        return new Uri(new Uri(root), "v2/vehicle/");
    }

    /// <summary><c>expires_in</c> as Experian sends it: a number, or a number in a string.</summary>
    private static double? ExpiresInSeconds(JsonElement root) =>
        root.TryGetProperty("expires_in", out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetDouble(out var seconds) && seconds > 0 => seconds,
                JsonValueKind.String when double.TryParse(
                    value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                    && seconds > 0 => seconds,
                _ => null,
            }
            : null;

    /// <summary>
    /// Experian's own "not found" answer (<c>VehicleNotFoundResponse</c>), not a
    /// gateway's 404 for a wrong address.
    /// </summary>
    private static bool IsVehicleNotFound(JsonDocument? body) =>
        body?.RootElement is { ValueKind: JsonValueKind.Object } root
        && root.TryGetProperty("errors", out var errors)
        && errors.ValueKind == JsonValueKind.Object;

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

    /// <summary>Where and how one call stopped. Its message carries no secret.</summary>
    private sealed class ExperianFailure(
        string stage,
        HttpStatusCode? status,
        string? detail,
        Exception? innerException = null)
        : Exception($"Experian {stage} failed.", innerException)
    {
        public string Stage { get; } = stage;

        public HttpStatusCode? Status { get; } = status;

        public string? Detail { get; } = detail;
    }
}
