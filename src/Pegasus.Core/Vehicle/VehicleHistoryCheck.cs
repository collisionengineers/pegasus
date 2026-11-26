using System.Globalization;
using System.Text;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Vehicle;

/// <summary>
/// One MIAFTR (condition) entry a history check found, as the provider names
/// it: its category, its theft status and its loss date (R1218). Any value may
/// be absent. An entry the insurer has since removed is never carried (R743).
/// </summary>
public sealed record VehicleWriteOffRecord(
    string? Category,
    string? TheftStatus,
    DateOnly? LossDate);

/// <summary>
/// A vehicle imported from outside the EU, with the year it was first
/// registered in the UK and its year of manufacture where known (R1218).
/// </summary>
public sealed record VehicleImportRecord(int? FirstRegisteredInUkYear, int? ManufactureYear);

/// <summary>
/// A history check's answer: the vehicle was found, with its MIAFTR entries
/// (none for a clean vehicle), whether a plate change is on record (R1217),
/// whether it was imported from outside the EU and whether it has no DVLA
/// registration (R1218); or the provider holds no vehicle for the registration.
/// </summary>
public sealed record VehicleHistoryCheckResult(
    bool VehicleFound,
    IReadOnlyList<VehicleWriteOffRecord> WriteOffs,
    bool PlateChangeOnRecord = false,
    VehicleImportRecord? ImportedFromOutsideEu = null,
    bool NoDvlaRegistration = false)
{
    public static VehicleHistoryCheckResult NotFound { get; } = new(false, []);

    public static VehicleHistoryCheckResult Found(
        IReadOnlyList<VehicleWriteOffRecord> writeOffs,
        bool plateChangeOnRecord = false,
        VehicleImportRecord? importedFromOutsideEu = null,
        bool noDvlaRegistration = false) =>
        new(
            true,
            writeOffs ?? throw new ArgumentNullException(nameof(writeOffs)),
            plateChangeOnRecord,
            importedFromOutsideEu,
            noDvlaRegistration);
}

/// <summary>
/// The connected vehicle history provider: Experian (ADR-0089). A host that
/// registers none offers no Run Experian check, and the Vehicle history
/// area says Experian is not connected.
/// </summary>
public interface IVehicleHistoryCheckProvider
{
    Task<VehicleHistoryCheckResult> CheckAsync(string registration, CancellationToken cancellationToken);
}

/// <summary>
/// The check could not answer: no provider is connected, or the connected one
/// failed. The page says the same either way, and the provider logs why.
/// </summary>
public sealed class VehicleHistoryCheckUnavailableException(Exception? innerException = null)
    : InvalidOperationException("No vehicle history check could answer.", innerException);

/// <summary>What one press of Run Experian check did.</summary>
public enum VehicleHistoryCheckOutcome
{
    /// <summary>The Vehicle history text now holds the check's wording.</summary>
    Recorded,

    /// <summary>Experian holds no vehicle for the registration; nothing was written (R748).</summary>
    NotFound,
}

/// <summary>
/// The Vehicle history text a check records (operator, 20 November 2026;
/// R1218, replacing R742's lines). It is Case data, printed by the report
/// verbatim, and carries none of Experian's guidance to look at another part
/// of its answer.
/// </summary>
public static class VehicleHistoryCheckNarrative
{
    /// <summary>A clean vehicle (R742; the reference reports' sentence without "AutoCheck").</summary>
    public const string Clean =
        "We confirm having checked this vehicle on the Experian database and there is no record of any adverse history.";

    /// <summary>The lead sentence when anything was found (R1218).</summary>
    public const string FoundLead = "According to the Experian database:";

    /// <summary>A plate change on record (R1217, without Experian's guidance; R1218).</summary>
    public const string PlateChangeLine = "There is a plate change on record.";

    /// <summary>A vehicle with no DVLA registration, Experian's message 29 (R1218).</summary>
    public const string NoDvlaRegistrationLine = "There is no DVLA registration for this vehicle.";

    private const string ImportLine = "The vehicle was imported from outside the EU.";

    /// <summary>
    /// The clean sentence when nothing was found; otherwise the lead sentence,
    /// then the MIAFTR entries newest first: one entry as
    /// <c>An entry exists on the MIAFTR register: {entry}</c>, several under
    /// <c>There are {n} entries on the MIAFTR register, the most recent with a
    /// loss dated {dd/MM/yyyy}:</c>, each entry as
    /// <c>{Category} - {Theft status} - loss dated {dd/MM/yyyy}.</c> with an
    /// absent value left out; then the plate change, import and no DVLA
    /// registration lines that apply (R1218).
    /// </summary>
    public static string Compose(VehicleHistoryCheckResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var lines = new List<string>();
        var entries = result.WriteOffs
            .OrderByDescending(record => record.LossDate ?? DateOnly.MinValue)
            .ToList();
        if (entries.Count == 1)
        {
            lines.Add("An entry exists on the MIAFTR register: " + Entry(entries[0]));
        }
        else if (entries.Count > 1)
        {
            lines.Add(entries[0].LossDate is { } latest
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"There are {entries.Count} entries on the MIAFTR register, the most recent with a loss dated {Date(latest)}:")
                : string.Create(CultureInfo.InvariantCulture, $"There are {entries.Count} entries on the MIAFTR register:"));
            lines.AddRange(entries.Select(Entry));
        }

        if (result.PlateChangeOnRecord)
        {
            lines.Add(PlateChangeLine);
        }

        if (result.ImportedFromOutsideEu is { } import)
        {
            var line = new StringBuilder(ImportLine);
            if (import.FirstRegisteredInUkYear is { } registered && import.ManufactureYear is { } made)
            {
                line.Append(CultureInfo.InvariantCulture, $" It was first registered in the UK in {registered}; its year of manufacture is {made}.");
            }
            else if (import.FirstRegisteredInUkYear is { } registeredOnly)
            {
                line.Append(CultureInfo.InvariantCulture, $" It was first registered in the UK in {registeredOnly}.");
            }
            else if (import.ManufactureYear is { } madeOnly)
            {
                line.Append(CultureInfo.InvariantCulture, $" Its year of manufacture is {madeOnly}.");
            }

            lines.Add(line.ToString());
        }

        if (result.NoDvlaRegistration)
        {
            lines.Add(NoDvlaRegistrationLine);
        }

        return lines.Count == 0 ? Clean : FoundLead + "\n" + string.Join('\n', lines);
    }

    private static string Entry(VehicleWriteOffRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var parts = new List<string>(3);
        if (Words(record.Category) is { } category)
        {
            parts.Add(category);
        }

        if (Words(record.TheftStatus) is { } theft)
        {
            parts.Add(theft);
        }

        if (record.LossDate is { } lossDate)
        {
            parts.Add("loss dated " + Date(lossDate));
        }

        return string.Join(" - ", parts) + ".";
    }

    private static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// Experian's capitals in the reports' case: "CAT S STRUCTURAL DAMAGE" is
    /// "Cat S Structural Damage", "NON-STRUCTURE" is "Non-Structure"; a
    /// one-letter word stays a capital.
    /// </summary>
    private static string? Words(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word =>
            string.Join('-', word.Split('-').Select(part => part.Length <= 1
                ? part.ToUpperInvariant()
                : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()))));
    }
}

public sealed record RunVehicleHistoryCheckRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string EditLeaseToken,
    string Registration);

/// <summary>
/// Run Experian check (ADR-0089): asks the connected provider about the
/// current work's accepted registration and records the answer's wording as
/// the work's Vehicle history, replacing what it held (R744), through the
/// same assessment save, guards and history as a staff edit. A vehicle
/// Experian does not hold writes nothing (R748); a write-off never ticks
/// Previous total loss (R745).
/// </summary>
public sealed class RunVehicleHistoryCheck(
    IEnumerable<IVehicleHistoryCheckProvider> providers,
    ICaseDataQueries caseData,
    SaveAssessment saveAssessment)
{
    /// <summary>The reason the Case history records for the save.</summary>
    public const string Reason = "Experian vehicle history check recorded.";

    private readonly IVehicleHistoryCheckProvider? _provider =
        (providers ?? throw new ArgumentNullException(nameof(providers))).SingleOrDefault();
    private readonly ICaseDataQueries _caseData =
        caseData ?? throw new ArgumentNullException(nameof(caseData));
    private readonly SaveAssessment _saveAssessment =
        saveAssessment ?? throw new ArgumentNullException(nameof(saveAssessment));

    /// <summary>Whether a provider is connected, so the page offers the check only where it can run.</summary>
    public bool IsConnected => _provider is not null;

    /// <summary>
    /// The current work's accepted registration, the one the check checks:
    /// confirmed, else the intake fact; never a suggestion. Null or blank when
    /// the work has none, so there is nothing to check.
    /// </summary>
    public static string? AcceptedRegistration(CaseDataProjection data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return (data.Vehicle.Registration.Confirmed ?? data.Vehicle.Registration.Fact)?.Value;
    }

    public async Task<VehicleHistoryCheckOutcome> ExecuteAsync(
        RunVehicleHistoryCheckRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        if (request.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.OperationKey))
        {
            throw new ArgumentException("An operation key is required.", nameof(request));
        }

        var registration = new VehicleLookupRequest(request.Registration).Registration;
        var provider = _provider ?? throw new VehicleHistoryCheckUnavailableException();
        var data = await _caseData.GetAsync(request.CaseId, CaseWorkSelector.Current, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        if (!string.Equals(AcceptedRegistration(data), registration, StringComparison.Ordinal))
        {
            throw new AcceptedVehicleRegistrationConflictException(request.CaseId);
        }

        var result = await provider.CheckAsync(registration, cancellationToken);
        ArgumentNullException.ThrowIfNull(result);
        if (!result.VehicleFound)
        {
            return VehicleHistoryCheckOutcome.NotFound;
        }

        await _saveAssessment.ExecuteAsync(
            new(
                request.CaseId,
                request.ExpectedVersion,
                request.Actor,
                request.OperationKey,
                Reason,
                request.EditLeaseToken,
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [AssessmentVocabulary.HistoryCheck] = VehicleHistoryCheckNarrative.Compose(result),
                }),
            cancellationToken);
        return VehicleHistoryCheckOutcome.Recorded;
    }
}
