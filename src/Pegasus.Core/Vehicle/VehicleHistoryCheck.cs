using System.Globalization;
using System.Text;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Vehicle;

/// <summary>
/// One write-off (condition) record a history check found, as the provider
/// names it. Any value may be absent. A record the insurer has since removed
/// is never carried (R743).
/// </summary>
public sealed record VehicleWriteOffRecord(
    string? Category,
    string? Cause,
    DateOnly? LossDate,
    string? Insurer,
    string? ClaimNumber);

/// <summary>
/// A history check's answer: the vehicle was found, with its write-off
/// records (none for a clean vehicle), or the provider holds no vehicle for
/// the registration.
/// </summary>
public sealed record VehicleHistoryCheckResult(
    bool VehicleFound,
    IReadOnlyList<VehicleWriteOffRecord> WriteOffs)
{
    public static VehicleHistoryCheckResult NotFound { get; } = new(false, []);

    public static VehicleHistoryCheckResult Found(IReadOnlyList<VehicleWriteOffRecord> writeOffs) =>
        new(true, writeOffs ?? throw new ArgumentNullException(nameof(writeOffs)));
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
/// The Vehicle history text a check records (operator, 6 November 2026;
/// R742). It is Case data, printed by the report verbatim.
/// </summary>
public static class VehicleHistoryCheckNarrative
{
    /// <summary>A clean vehicle (R742; the reference reports' sentence without "AutoCheck").</summary>
    public const string Clean =
        "We confirm having checked this vehicle on the Experian database and there is no record of any adverse history.";

    /// <summary>The lead sentence when records were found (R742).</summary>
    public const string FoundLead =
        "We confirm having checked this vehicle on the Experian database, which records:";

    private const string WriteOffLabel = "Write-off: ";

    /// <summary>
    /// The clean sentence, or the lead sentence and one line per write-off:
    /// <c>Write-off: {category}, {cause}, loss date {dd/MM/yyyy}, {insurer},
    /// claim {number}</c>, an absent value left out with its comma (R742).
    /// </summary>
    public static string Compose(IReadOnlyList<VehicleWriteOffRecord> writeOffs)
    {
        ArgumentNullException.ThrowIfNull(writeOffs);
        if (writeOffs.Count == 0)
        {
            return Clean;
        }

        var text = new StringBuilder(FoundLead);
        foreach (var record in writeOffs)
        {
            ArgumentNullException.ThrowIfNull(record);
            var parts = new List<string>(5);
            Add(parts, record.Category);
            Add(parts, record.Cause);
            if (record.LossDate is { } lossDate)
            {
                parts.Add("loss date " + lossDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
            }

            Add(parts, record.Insurer);
            if (!string.IsNullOrWhiteSpace(record.ClaimNumber))
            {
                parts.Add("claim " + record.ClaimNumber.Trim());
            }

            text.Append('\n').Append(WriteOffLabel).Append(string.Join(", ", parts));
        }

        return text.ToString();
    }

    private static void Add(List<string> parts, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add(value.Trim());
        }
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
        var accepted = (data.Vehicle.Registration.Confirmed ?? data.Vehicle.Registration.Fact)?.Value;
        if (!string.Equals(accepted, registration, StringComparison.Ordinal))
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
                    [AssessmentVocabulary.HistoryCheck] = VehicleHistoryCheckNarrative.Compose(result.WriteOffs),
                }),
            cancellationToken);
        return VehicleHistoryCheckOutcome.Recorded;
    }
}
