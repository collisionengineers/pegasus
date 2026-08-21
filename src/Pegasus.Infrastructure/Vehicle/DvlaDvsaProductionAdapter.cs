using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Vehicle;

namespace Pegasus.Infrastructure.Vehicle;

public sealed record DvlaDvsaProductionOptions(
    Uri DvlaBaseUri,
    string DvlaApiKey,
    Uri DvsaBaseUri,
    Uri DvsaTokenUri,
    string DvsaClientId,
    string DvsaClientSecret,
    string DvsaApiKey,
    string DvsaScope)
{
    public static DvlaDvsaProductionOptions Create(IReadOnlyDictionary<string, string?> values) => new(
        RequireUri(values, "Dvla:BaseUri", "driver-vehicle-licensing.api.gov.uk"),
        Require(values, "Dvla:ApiKey"),
        RequireUri(values, "Dvsa:BaseUri", "history.mot.api.gov.uk"),
        RequireHttpsUri(values, "Dvsa:TokenUri"),
        Require(values, "Dvsa:ClientId"),
        Require(values, "Dvsa:ClientSecret"),
        Require(values, "Dvsa:ApiKey"),
        Require(values, "Dvsa:Scope"));

    private static string Require(IReadOnlyDictionary<string, string?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
        {
            throw new InvalidOperationException($"{key} is required for the production vehicle adapter.");
        }
        return value.Trim();
    }

    private static Uri RequireUri(IReadOnlyDictionary<string, string?> values, string key, string host)
    {
        var uri = RequireHttpsUri(values, key);
        if (!uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{key} must use the approved provider host.");
        }
        return uri;
    }

    private static Uri RequireHttpsUri(IReadOnlyDictionary<string, string?> values, string key)
    {
        var value = Require(values, key);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"{key} must be an absolute HTTPS URI.");
        }
        return uri;
    }
}

internal sealed class DvlaDvsaProductionAdapter(
    DvlaDvsaProductionOptions options,
    HttpClient httpClient,
    TimeProvider timeProvider) : IVehicleLookupAdapter, IDisposable
{
    private readonly SemaphoreSlim tokenLock = new(1, 1);
    private string? dvsaToken;
    private DateTimeOffset dvsaTokenExpiresAtUtc;

    public void Dispose() => tokenLock.Dispose();

    public async Task<VehicleLookupResult> LookupAsync(
        VehicleLookupRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var retrievedAtUtc = timeProvider.GetUtcNow();
        var dvla = await ReadDvlaAsync(request.Registration, cancellationToken);
        var dvsa = await ReadDvsaAsync(request.Registration, cancellationToken);
        var identity = Hash($"{dvla.Identity}\n{dvsa.Identity}");

        VehicleLookupResult result;
        if (dvla.Vehicle is not null || dvsa.Tests.Count > 0)
        {
            var failure = dvla.Failure
                ?? dvsa.Failure
                ?? (dvla.NotFound ? new VehicleLookupFailure("dvla_not_found", Retryable: false) : null)
                ?? (dvsa.NotFound ? new VehicleLookupFailure("dvsa_not_found", Retryable: false) : null);
            var sourceAge = MaxAge(dvla.ResponseAge, dvsa.ResponseAge);
            result = new(
                request.Registration,
                failure is not null
                    ? VehicleLookupOutcome.Partial
                    : sourceAge > TimeSpan.Zero
                        ? VehicleLookupOutcome.Stale
                        : VehicleLookupOutcome.Current,
                "dvla-ves+dvsa-mot-history",
                "ves-1.2+mot-history-v1",
                identity,
                retrievedAtUtc,
                retrievedAtUtc - sourceAge,
                retrievedAtUtc - sourceAge,
                dvla.Vehicle,
                dvsa.Tests,
                failure);
        }
        else if (dvla.NotFound && dvsa.NotFound)
        {
            result = FailureResult(request.Registration, VehicleLookupOutcome.NotFound, identity, retrievedAtUtc, null);
        }
        else
        {
            var failure = dvla.Failure ?? dvsa.Failure
                ?? new VehicleLookupFailure("provider_unavailable", Retryable: true);
            var outcome = failure.Code.Contains("throttled", StringComparison.Ordinal)
                ? VehicleLookupOutcome.Throttled
                : failure.Code.Contains("invalid", StringComparison.Ordinal)
                    || failure.Code.Contains("denied", StringComparison.Ordinal)
                    || failure.Code.Contains("malformed", StringComparison.Ordinal)
                        ? VehicleLookupOutcome.Failed
                        : VehicleLookupOutcome.Unavailable;
            result = FailureResult(request.Registration, outcome, identity, retrievedAtUtc, failure);
        }
        result.EnsureValidFor(request);
        return result;
    }

    private async Task<ProviderVehicleResult> ReadDvlaAsync(
        string registration,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(options.DvlaBaseUri, "vehicles");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.TryAddWithoutValidation("x-api-key", options.DvlaApiKey);
        request.Content = JsonContent.Create(new { registrationNumber = registration });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var identity = Hash(body);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new(null, true, null, identity, response.Headers.Age ?? TimeSpan.Zero);
        }
        if (!response.IsSuccessStatusCode)
        {
            return new(null, false, ProviderFailure("dvla", response), identity, response.Headers.Age ?? TimeSpan.Zero);
        }
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return new(
                new(
                    Text(root, "make"),
                    Text(root, "model"),
                    Number(root, "yearOfManufacture"),
                    Number(root, "engineCapacity"),
                    Text(root, "fuelType")),
                false,
                null,
                identity,
                response.Headers.Age ?? TimeSpan.Zero);
        }
        catch (JsonException)
        {
            return new(null, false, new("dvla_malformed", Retryable: false), identity, response.Headers.Age ?? TimeSpan.Zero);
        }
    }

    private async Task<ProviderMotResult> ReadDvsaAsync(
        string registration,
        CancellationToken cancellationToken)
    {
        var token = await GetDvsaTokenAsync(cancellationToken);
        if (token.Failure is not null)
        {
            return new([], false, token.Failure, token.Identity, TimeSpan.Zero);
        }
        var uri = new Uri(options.DvsaBaseUri, Uri.EscapeDataString(registration));
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Headers.TryAddWithoutValidation("X-API-Key", options.DvsaApiKey);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var identity = Hash(body);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new([], true, null, identity, response.Headers.Age ?? TimeSpan.Zero);
        }
        if (!response.IsSuccessStatusCode)
        {
            return new([], false, ProviderFailure("dvsa", response), identity, response.Headers.Age ?? TimeSpan.Zero);
        }
        try
        {
            using var document = JsonDocument.Parse(body);
            var vehicles = document.RootElement.ValueKind == JsonValueKind.Array
                ? document.RootElement.EnumerateArray().ToArray()
                : [document.RootElement];
            var rawTests = vehicles
                .SelectMany(vehicle => vehicle.TryGetProperty("motTests", out var values)
                    && values.ValueKind == JsonValueKind.Array
                        ? values.EnumerateArray().ToArray()
                        : [])
                .ToArray();
            var tests = rawTests
                .Select(ParseMot)
                .Where(item => item is not null)
                .Cast<MotTestObservation>()
                .ToArray();
            // A vehicle with no MOT history and a vehicle whose entire MOT
            // history we failed to read look identical downstream — both
            // produce no mileage. They are not the same thing, and treating
            // them as the same is how a provider-side format change stayed
            // invisible in production. Reading none of what was offered is
            // a failure and says so (ENG-010).
            if (rawTests.Length > 0 && tests.Length == 0)
            {
                return new(
                    [],
                    false,
                    new("dvsa_unreadable_tests", Retryable: false),
                    identity,
                    response.Headers.Age ?? TimeSpan.Zero);
            }
            return new(tests, false, null, identity, response.Headers.Age ?? TimeSpan.Zero);
        }
        catch (JsonException)
        {
            return new([], false, new("dvsa_malformed", Retryable: false), identity, response.Headers.Age ?? TimeSpan.Zero);
        }
    }

    private async Task<TokenResult> GetDvsaTokenAsync(CancellationToken cancellationToken)
    {
        if (dvsaToken is not null && dvsaTokenExpiresAtUtc > timeProvider.GetUtcNow().AddMinutes(1))
        {
            return new(dvsaToken, null, Hash(dvsaToken));
        }
        await tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (dvsaToken is not null && dvsaTokenExpiresAtUtc > timeProvider.GetUtcNow().AddMinutes(1))
            {
                return new(dvsaToken, null, Hash(dvsaToken));
            }
            using var request = new HttpRequestMessage(HttpMethod.Post, options.DvsaTokenUri)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = options.DvsaClientId,
                    ["client_secret"] = options.DvsaClientSecret,
                    ["scope"] = options.DvsaScope
                })
            };
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var identity = Hash(body);
            if (!response.IsSuccessStatusCode)
            {
                return new(null, ProviderFailure("dvsa_auth", response), identity);
            }
            using var document = JsonDocument.Parse(body);
            var token = Text(document.RootElement, "access_token");
            var expires = Number(document.RootElement, "expires_in");
            if (string.IsNullOrWhiteSpace(token) || expires is null or <= 0)
            {
                return new(null, new("dvsa_auth_malformed", Retryable: false), identity);
            }
            dvsaToken = token;
            dvsaTokenExpiresAtUtc = timeProvider.GetUtcNow().AddSeconds(expires.Value);
            return new(token, null, identity);
        }
        catch (JsonException)
        {
            return new(null, new("dvsa_auth_malformed", Retryable: false), "dvsa-auth-malformed");
        }
        finally
        {
            tokenLock.Release();
        }
    }

    /// <summary>
    /// A DVSA date, which is not always a date. The MOT History API writes
    /// `completedDate` as a full instant — `2026-05-14T13:11:22.000Z` —
    /// which <see cref="DateOnly.TryParse(string, IFormatProvider,
    /// DateTimeStyles, out DateOnly)"/> rejects outright. Reading it as a
    /// date only meant every MOT test failed to parse and was dropped, for
    /// every vehicle, with no failure recorded anywhere: the mileage was
    /// always there and never once reached a case (ENG-010).
    /// </summary>
    private static DateOnly? ParseProviderDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var instant)
            ? DateOnly.FromDateTime(instant.UtcDateTime)
            : null;
    }

    private static MotTestObservation? ParseMot(JsonElement value)
    {
        if (ParseProviderDate(Text(value, "completedDate")) is not { } date
            || string.IsNullOrWhiteSpace(Text(value, "testResult")))
        {
            return null;
        }
        var expiry = ParseProviderDate(Text(value, "expiryDate"));
        long? mileage = LongNumber(value, "odometerValue");
        var unitText = Text(value, "odometerUnit");
        VehicleMileageUnit? unit = mileage is null
            ? null
            : unitText?.Contains("km", StringComparison.OrdinalIgnoreCase) == true
                ? VehicleMileageUnit.Kilometres
                : VehicleMileageUnit.Miles;
        return new(date, Text(value, "testResult")!, expiry, mileage, unit);
    }

    private static VehicleLookupResult FailureResult(
        string registration,
        VehicleLookupOutcome outcome,
        string identity,
        DateTimeOffset retrievedAtUtc,
        VehicleLookupFailure? failure) => new(
            registration,
            outcome,
            "dvla-ves+dvsa-mot-history",
            "ves-1.2+mot-history-v1",
            identity,
            retrievedAtUtc,
            null,
            null,
            null,
            [],
            failure);

    private static VehicleLookupFailure ProviderFailure(string provider, HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter?.Delta;
        return response.StatusCode switch
        {
            HttpStatusCode.BadRequest => new($"{provider}_invalid", Retryable: false),
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new($"{provider}_denied", Retryable: false),
            HttpStatusCode.TooManyRequests => new(
                $"{provider}_throttled",
                Retryable: true,
                retryAfter is { } delay && delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(30)),
            _ when (int)response.StatusCode >= 500 => new($"{provider}_unavailable", Retryable: true),
            _ => new($"{provider}_failed_{(int)response.StatusCode}", Retryable: false)
        };
    }

    private static string? Text(JsonElement value, string property) =>
        value.TryGetProperty(property, out var result) && result.ValueKind == JsonValueKind.String
            ? result.GetString()
            : null;

    private static int? Number(JsonElement value, string property) =>
        value.TryGetProperty(property, out var result) && result.TryGetInt32(out var number)
            ? number
            : null;

    private static long? LongNumber(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result))
        {
            return null;
        }
        if (result.ValueKind == JsonValueKind.Number && result.TryGetInt64(out var number))
        {
            return number;
        }
        return result.ValueKind == JsonValueKind.String
            && long.TryParse(result.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
                ? number
                : null;
    }

    private static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));
    private static string Hash(ReadOnlySpan<byte> value) => Convert.ToHexString(SHA256.HashData(value));

    private static TimeSpan MaxAge(TimeSpan left, TimeSpan right) =>
        left > right ? left : right;

    private sealed record ProviderVehicleResult(
        VehicleDetails? Vehicle,
        bool NotFound,
        VehicleLookupFailure? Failure,
        string Identity,
        TimeSpan ResponseAge);
    private sealed record ProviderMotResult(
        IReadOnlyList<MotTestObservation> Tests,
        bool NotFound,
        VehicleLookupFailure? Failure,
        string Identity,
        TimeSpan ResponseAge);
    private sealed record TokenResult(string? Token, VehicleLookupFailure? Failure, string Identity);
}
