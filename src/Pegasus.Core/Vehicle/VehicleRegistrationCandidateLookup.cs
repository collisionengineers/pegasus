using System.Text.RegularExpressions;

namespace Pegasus.Core.Vehicle;

public enum MachineReadRegistrationSource
{
    DocumentOcr,
    VehicleRecognition,
    EmbeddedDocumentText,
    StaffConfirmed,
    CaseSearch
}

public sealed record MachineReadRegistration(
    string RawValue,
    MachineReadRegistrationSource Source,
    string SourceReference);

public sealed record VehicleRegistrationCandidateAttempt(
    int Order,
    string Registration,
    VehicleLookupResult Result);

public sealed record VehicleRegistrationCandidateLookupResult(
    MachineReadRegistration Reading,
    IReadOnlyList<string> Candidates,
    IReadOnlyList<VehicleRegistrationCandidateAttempt> Attempts,
    string? AcceptedRegistration,
    VehicleLookupResult? AcceptedResult)
{
    public bool IsAmbiguous => AcceptedResult is null;
}

/// <summary>
/// Resolves only bounded machine-reading substitutions through the existing
/// vehicle lookup port. It neither searches Cases nor talks to a provider.
/// </summary>
public sealed partial class VehicleRegistrationCandidateLookup(IVehicleLookupAdapter adapter)
{
    private const int MaximumCandidates = 8;
    private readonly IVehicleLookupAdapter adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));

    public async Task<VehicleRegistrationCandidateLookupResult> LookupAsync(
        MachineReadRegistration reading,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reading);
        if (reading.Source is not (MachineReadRegistrationSource.DocumentOcr or MachineReadRegistrationSource.VehicleRecognition))
            throw new ArgumentException("Registration alternatives require OCR or vehicle-recognition evidence.", nameof(reading));
        if (string.IsNullOrWhiteSpace(reading.SourceReference))
            throw new ArgumentException("Machine-reading provenance is required.", nameof(reading));

        var candidates = GenerateCandidates(reading.RawValue);
        var tasks = candidates.Select(async (candidate, order) =>
        {
            var request = new VehicleLookupRequest(candidate);
            var result = await adapter.LookupAsync(request, cancellationToken).ConfigureAwait(false);
            result.EnsureValidFor(request);
            return new VehicleRegistrationCandidateAttempt(order, candidate, result);
        }).ToArray();
        var attempts = await Task.WhenAll(tasks).ConfigureAwait(false);

        var viable = attempts.Where(attempt => attempt.Result.Outcome is
            VehicleLookupOutcome.Current or VehicleLookupOutcome.Stale or VehicleLookupOutcome.Partial).ToArray();
        var accepted = viable.Length == 1 && attempts.All(attempt =>
            attempt == viable[0] || attempt.Result.Outcome == VehicleLookupOutcome.NotFound)
                ? viable[0]
                : null;
        return new(reading, candidates, attempts, accepted?.Registration, accepted?.Result);
    }

    public static IReadOnlyList<string> GenerateCandidates(string rawValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawValue);
        var original = WhitespaceRegex().Replace(rawValue, string.Empty).ToUpperInvariant();
        if (original.Length is 0 or > 7
            || original.Any(character => !char.IsAsciiLetterUpper(character) && !char.IsAsciiDigit(character)))
            return [];

        var positions = original.Select((character, index) => (character, index))
            .Where(item => item.character is 'O' or '0' or 'I' or '1').ToArray();
        var results = new List<(string Value, int Substitutions, int Ordinal)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var combinations = 1 << positions.Length;
        for (var mask = 0; mask < combinations; mask++)
        {
            var characters = original.ToCharArray();
            for (var bit = 0; bit < positions.Length; bit++)
            {
                if ((mask & (1 << bit)) == 0) continue;
                var (character, index) = positions[bit];
                characters[index] = character switch { 'O' => '0', '0' => 'O', 'I' => '1', '1' => 'I', _ => character };
            }
            var candidate = new string(characters);
            if (IsSupportedRegistration(candidate) && seen.Add(candidate))
                results.Add((candidate, System.Numerics.BitOperations.PopCount((uint)mask), mask));
        }

        if (results.Count > MaximumCandidates)
            return [];

        return results
            .OrderBy(candidate => candidate.Substitutions)
            .ThenBy(candidate => candidate.Ordinal)
            .Select(candidate => candidate.Value)
            .ToArray();
    }

    private static bool IsSupportedRegistration(string value) =>
        CurrentRegex().IsMatch(value) || PrefixRegex().IsMatch(value) || SuffixRegex().IsMatch(value)
        || DigitsThenLettersRegex().IsMatch(value) || LettersThenDigitsRegex().IsMatch(value);

    [GeneratedRegex(@"\s", RegexOptions.CultureInvariant, 100)] private static partial Regex WhitespaceRegex();
    [GeneratedRegex(@"^[A-Z]{2}[0-9]{2}[A-Z]{3}$", RegexOptions.CultureInvariant, 100)] private static partial Regex CurrentRegex();
    [GeneratedRegex(@"^[A-Z][0-9]{1,3}[A-Z]{3}$", RegexOptions.CultureInvariant, 100)] private static partial Regex PrefixRegex();
    [GeneratedRegex(@"^[A-Z]{3}[0-9]{1,3}[A-Z]$", RegexOptions.CultureInvariant, 100)] private static partial Regex SuffixRegex();
    [GeneratedRegex(@"^[0-9]{1,4}[A-Z]{1,3}$", RegexOptions.CultureInvariant, 100)] private static partial Regex DigitsThenLettersRegex();
    // This shared syntactic shape covers GB dateless letters-first and Northern
    // Ireland registrations without maintaining a duplicate regular expression.
    [GeneratedRegex(@"^[A-Z]{1,3}[0-9]{1,4}$", RegexOptions.CultureInvariant, 100)] private static partial Regex LettersThenDigitsRegex();
}
