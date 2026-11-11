using System.Globalization;
using System.Text.RegularExpressions;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.ThirdPartyReports;

namespace Pegasus.Core.Assessment;

/// <summary>
/// What one filed original report printed about the four Original report
/// cells, already in the cells' own vocabulary. <paramref name="Sha256"/> is the
/// exact bytes that were read, so the writer can refuse a reading of anything
/// but the file it is filling from. <paramref name="OutcomeUnreadable"/> says the
/// report did print an outcome but not one the cells can hold — a conflict,
/// an unrecognised word, or an outcome and a status that disagree — which is
/// different from printing none, because only printing none lets the intake
/// verdict stand in. <paramref name="Mileage"/> and <paramref name="MileageUnit"/>
/// are the odometer reading the report states, in the Case's own unit words, or
/// both null: it fills an empty Case mileage (R930).
/// </summary>
public sealed record OriginalReportReading(
    string Sha256,
    string? Assessor,
    string? ReportDate,
    string? Roadworthiness,
    string? Outcome,
    bool OutcomeUnreadable,
    long? Mileage = null,
    string? MileageUnit = null);

/// <summary>
/// A printed outcome or status as the report's reading left it: the usable
/// value (null where the report conflicted or the value was not usable) and
/// how the row was recorded.
/// </summary>
public sealed record PrintedOutcome(string? Value, SourceCandidateDisposition Disposition);

/// <summary>
/// How a filed original report fills an Audit's Original report cells (v28
/// P51, ruled 20 September 2026; #840). The third-party report extraction is
/// the source: a fact fills only when the report printed it once, or printed
/// the same value each time — a conflicting fact is never resolved by picking
/// one, because two different printed values in an engineer report are a
/// contradiction for staff, not a tie to break. Printed words map to the
/// cells' codes through the fixed tables below; a word they do not name leaves
/// the cell blank rather than guessing.
///
/// Repairable status alone has a second source, the Audit's intake verdict
/// (the literal "repairable"/"total loss" read, or the Principal API's declared
/// verdict): it fills when the report printed no outcome at all, and a
/// disagreement between the two leaves the cell blank.
///
/// A fill never overwrites a cell staff recorded; a cell the extraction
/// recorded takes the newer reading (<see cref="AssessmentPolicy.FillLands"/>).
/// There is no per-field review (operator, 25 September 2026): a filled cell
/// is the Case's value, tagged Extracted until staff change it.
///
/// The mileage the report states is not a cell: it fills the Case's own
/// mileage where the Case holds none, so a Case that waits only for its
/// mileage moves to Review by itself (operator, 11 November 2026; R930).
/// </summary>
public static class OriginalReportPrefillPolicy
{
    /// <summary>
    /// The Automation actor a filled cell records, so a reader can tell a
    /// value read from the filed report from any other automation's.
    /// </summary>
    public const string RecorderId = "original-report-extraction";

    /// <summary>The policy a Case mileage filled from the report records (R930).</summary>
    public const string MileagePolicyKey = "original-report-mileage";
    public const int MileagePolicyVersion = 1;

    private static readonly Regex Whitespace = new(
        @"\s+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// The printed roadworthiness answers the report families use: the
    /// narrative and Montgomery answer a "Roadworthy" label Yes or No; Laird's
    /// Legal Status and sPrint print the word itself.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> RoadworthinessWords =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["yes"] = "roadworthy",
            ["roadworthy"] = "roadworthy",
            ["no"] = "unroadworthy",
            ["unroadworthy"] = "unroadworthy"
        };

    /// <summary>
    /// The printed outcomes the report families use: the narrative title's
    /// "Repairable", Montgomery's REPAIR or TOTAL LOSS, and Laird's and
    /// sPrint's status word. Cash in lieu and contract repair are never read:
    /// no report prints either as its outcome.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> OutcomeWords =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["repairable"] = "repairable",
            ["repair"] = "repairable",
            ["total loss"] = "total_loss"
        };

    /// <summary>A printed roadworthiness answer as its cell code, or null.</summary>
    public static string? RoadworthinessCode(string? printed) => Code(RoadworthinessWords, printed);

    /// <summary>A printed outcome or status as its cell code, or null.</summary>
    public static string? OutcomeCode(string? printed) => Code(OutcomeWords, printed);

    /// <summary>
    /// Reads the cells' values off one report's extracted candidate. A null
    /// candidate — no report signature matched, or more than one did — reads
    /// as a report that printed nothing.
    /// </summary>
    public static OriginalReportReading Read(ThirdPartyReportCandidate? candidate, string sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        if (candidate is null)
        {
            return new(sha256, null, null, null, null, OutcomeUnreadable: false);
        }

        return Read(
            sha256,
            candidate.Identity.Issuer?.Value,
            candidate.Identity.ReportDate?.Value,
            candidate.Damage.Roadworthiness?.Value,
            Printed(candidate.Damage.Outcome),
            Printed(candidate.Damage.Repairability),
            (long?)candidate.Vehicle.Mileage?.Value,
            MileageUnit(
                candidate.Vehicle.MileageUnit?.Value,
                candidate.Vehicle.MileageUnit?.Source.Disposition,
                candidate.Vehicle.Mileage?.Source.Unit));
    }

    /// <summary>
    /// The same reading from the printed values themselves: what a recorded
    /// reading keeps of a report, which a standalone Audit's acceptance seeds
    /// from instead of reading the report again. A mileage whose unit is not
    /// known is not read: the Case saves a mileage only with its unit.
    /// </summary>
    public static OriginalReportReading Read(
        string sha256,
        string? issuer,
        DateOnly? reportDate,
        string? roadworthiness,
        PrintedOutcome? printedOutcome,
        PrintedOutcome? printedStatus,
        long? mileage = null,
        string? mileageUnit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        var outcome = Outcome(printedOutcome);
        var status = Outcome(printedStatus);
        var unreadable = outcome.Unreadable || status.Unreadable
            || (outcome.Code is not null && status.Code is not null && outcome.Code != status.Code);
        long? odometer = null;
        string? odometerUnit = null;
        if (mileage is >= 0 && CaseOdometer.TryParseUnit(mileageUnit, out var unit))
        {
            odometer = mileage;
            odometerUnit = CaseOdometer.Format(unit);
        }

        return new(
            sha256,
            issuer,
            reportDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            RoadworthinessCode(roadworthiness),
            unreadable ? null : outcome.Code ?? status.Code,
            unreadable,
            odometer,
            odometerUnit);
    }

    /// <summary>
    /// The unit a report's mileage is in: the unit printed beside it, else the
    /// unit the report family's reading rule states for its mileage. A printed
    /// unit the reader could not settle is no unit, never the stated one.
    /// </summary>
    public static string? MileageUnit(
        string? printedUnit,
        SourceCandidateDisposition? printedDisposition,
        string? statedUnit) =>
        printedDisposition is null or SourceCandidateDisposition.Missing
            ? statedUnit
            : printedUnit;

    /// <summary>
    /// The cell values one reading fills, keyed by path, each in the
    /// vocabulary's canonical form. A cell the reading cannot fill is absent,
    /// never null: a fill does not clear. A report that could not be read at
    /// all (a null reading) printed no outcome, so the intake verdict alone
    /// fills Repairable status.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Writes(
        OriginalReportReading? reading,
        AuditAssessment? intakeVerdict)
    {
        var verdict = intakeVerdict is { } value ? AuditAssessmentCode.ToCode(value) : null;
        var outcome = reading switch
        {
            null or { OutcomeUnreadable: false, Outcome: null } => verdict,
            { OutcomeUnreadable: true } => null,
            { Outcome: { } read } when verdict is null || verdict == read => read,
            _ => null
        };

        var writes = new Dictionary<string, string>(StringComparer.Ordinal);
        Add(writes, AssessmentVocabulary.OriginalReportAssessor, reading?.Assessor);
        Add(writes, AssessmentVocabulary.OriginalReportDate, reading?.ReportDate);
        Add(writes, AssessmentVocabulary.OriginalReportRoadworthiness, reading?.Roadworthiness);
        Add(writes, AssessmentVocabulary.OriginalReportOutcome, outcome);
        return writes;
    }

    private static PrintedOutcome? Printed(ThirdPartyReportFact<string?>? fact) =>
        fact is null ? null : new(fact.Value, fact.Source.Disposition);

    private static (string? Code, bool Unreadable) Outcome(PrintedOutcome? printed) =>
        printed switch
        {
            null => (null, false),
            { Disposition: SourceCandidateDisposition.Missing } => (null, false),
            { Value: null } => (null, true),
            { Value: var word } => OutcomeCode(word) is { } code ? (code, false) : (null, true)
        };

    private static string? Code(IReadOnlyDictionary<string, string> words, string? printed) =>
        printed is not null
        && words.TryGetValue(Whitespace.Replace(printed.Trim(), " "), out var code)
            ? code
            : null;

    private static void Add(Dictionary<string, string> writes, string path, string? value)
    {
        if (Carried(path, value) is { } normalized)
        {
            writes[path] = normalized;
        }
    }

    // The vocabulary owns the canonical form; a value it refuses is not filled.
    private static string? Carried(string path, string? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return AssessmentPolicy.NormalizeFieldValue(path, value);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
