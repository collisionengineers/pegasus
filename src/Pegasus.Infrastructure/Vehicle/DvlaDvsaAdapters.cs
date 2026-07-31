using System.Text.Json;
using System.Text.Json.Serialization;
using Pegasus.Core.Vehicle;

namespace Pegasus.Infrastructure.Vehicle;

public sealed class DvlaDvsaReplayAdapter
    : IVehicleLookupAdapter
{
    private const long MaximumFixtureBytes = 1024 * 1024;
    private const int SupportedSchemaVersion = 1;
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private readonly string fixtureRoot;
    private readonly TimeProvider timeProvider;

    public DvlaDvsaReplayAdapter(string fixtureRoot, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fixtureRoot);
        this.fixtureRoot = Path.GetFullPath(fixtureRoot);
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<VehicleLookupResult> LookupAsync(
        VehicleLookupRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var fixturePath = Path.GetFullPath(
            Path.Combine(fixtureRoot, $"{request.Registration}.vehicle-lookup.json"));
        if (!IsDescendant(fixturePath, fixtureRoot))
        {
            throw new InvalidDataException("The vehicle replay fixture resolved outside its configured root.");
        }

        if (Directory.Exists(fixtureRoot)
            && (File.GetAttributes(fixtureRoot) & FileAttributes.ReparsePoint) != 0)
        {
            return CreateInvalidFixture(
                request.Registration,
                timeProvider.GetUtcNow(),
                "fixture_root_reparse_point");
        }

        FileStream stream;
        try
        {
            var attributes = File.GetAttributes(fixturePath);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                return CreateInvalidFixture(request.Registration, timeProvider.GetUtcNow(), "fixture_reparse_point");
            }

            stream = new FileStream(
                fixturePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch (FileNotFoundException)
        {
            return CreateUnavailable(request.Registration, timeProvider.GetUtcNow());
        }
        catch (DirectoryNotFoundException)
        {
            return CreateUnavailable(request.Registration, timeProvider.GetUtcNow());
        }

        if (stream.Length > MaximumFixtureBytes)
        {
            await stream.DisposeAsync();
            return CreateInvalidFixture(request.Registration, timeProvider.GetUtcNow(), "fixture_oversized");
        }
        await using (stream)
        {
            ReplayFixture? fixture;
            try
            {
                fixture = await JsonSerializer.DeserializeAsync<ReplayFixture>(
                    stream,
                    SerializerOptions,
                    cancellationToken);
            }
            catch (JsonException)
            {
                return CreateInvalidFixture(request.Registration, timeProvider.GetUtcNow(), "fixture_invalid_json");
            }

            var retrievedAtUtc = timeProvider.GetUtcNow();
            if (fixture is null
                || fixture.SchemaVersion != SupportedSchemaVersion
                || !request.Registration.Equals(fixture.Registration, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(fixture.Provider)
                || string.IsNullOrWhiteSpace(fixture.ProviderVersion)
                || string.IsNullOrWhiteSpace(fixture.ResponseIdentity)
                || fixture.MotTests?.Contains(null) == true)
            {
                return CreateInvalidFixture(request.Registration, retrievedAtUtc, "fixture_contract_invalid");
            }

            TimeSpan? retryAfter = null;
            if (fixture.Failure?.RetryAfterSeconds is { } seconds)
            {
                if (!double.IsFinite(seconds)
                    || seconds <= 0
                    || seconds > TimeSpan.MaxValue.TotalSeconds)
                {
                    return CreateInvalidFixture(request.Registration, retrievedAtUtc, "fixture_contract_invalid");
                }

                retryAfter = TimeSpan.FromSeconds(seconds);
            }

            var failure = fixture.Failure is null
                ? null
                : new VehicleLookupFailure(
                    fixture.Failure.Code,
                    fixture.Failure.Retryable,
                    retryAfter);
            MotTestObservation[] motTests = fixture.MotTests is null
                ? []
                : fixture.MotTests.Select(observation => observation!).ToArray();
            var result = new VehicleLookupResult(
                fixture.Registration,
                fixture.Outcome,
                fixture.Provider,
                fixture.ProviderVersion,
                fixture.ResponseIdentity,
                retrievedAtUtc,
                fixture.EffectiveAtUtc,
                fixture.SourceObservedAtUtc,
                fixture.Vehicle,
                motTests,
                failure);

            try
            {
                result.EnsureValidFor(request);
                return result;
            }
            catch (InvalidDataException)
            {
                return CreateInvalidFixture(request.Registration, retrievedAtUtc, "fixture_contract_invalid");
            }
        }
    }

    private static VehicleLookupResult CreateUnavailable(
        string registration,
        DateTimeOffset retrievedAtUtc) =>
        new(
            registration,
            VehicleLookupOutcome.Unavailable,
            "dvla-dvsa-replay",
            SupportedSchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"missing:{registration}",
            retrievedAtUtc,
            EffectiveAtUtc: null,
            SourceObservedAtUtc: null,
            Vehicle: null,
            MotTests: [],
            Failure: new VehicleLookupFailure("fixture_unavailable", Retryable: false));

    private static VehicleLookupResult CreateInvalidFixture(
        string registration,
        DateTimeOffset retrievedAtUtc,
        string failureCode) =>
        new(
            registration,
            VehicleLookupOutcome.Failed,
            "dvla-dvsa-replay",
            SupportedSchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"invalid:{registration}",
            retrievedAtUtc,
            EffectiveAtUtc: null,
            SourceObservedAtUtc: null,
            Vehicle: null,
            MotTests: [],
            Failure: new VehicleLookupFailure(failureCode, Retryable: false));

    private static bool IsDescendant(string path, string root)
    {
        var relative = Path.GetRelativePath(root, path);
        return !relative.Equals("..", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }

    private sealed record ReplayFixture(
        int SchemaVersion,
        string Registration,
        VehicleLookupOutcome Outcome,
        string Provider,
        string ProviderVersion,
        string ResponseIdentity,
        DateTimeOffset? EffectiveAtUtc,
        DateTimeOffset? SourceObservedAtUtc,
        VehicleDetails? Vehicle,
        List<MotTestObservation?>? MotTests,
        ReplayFailure? Failure);

    private sealed record ReplayFailure(
        string Code,
        bool Retryable,
        double? RetryAfterSeconds);
}
