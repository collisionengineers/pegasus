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
/// verdict stand in.
/// </summary>
public sealed record OriginalReportReading(
    string Sha256,
    string? Assessor,
    string? ReportDate,
    string? Roadworthiness,
    string? Outcome,
    bool OutcomeUnreadable);

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
/// (the literal "repairable"/"total loss" read, or the Provider API's declared
/// verdict): it fills when the report printed no outcome at all, and a
/// disagreement between the two leaves the cell blank.
///
/// A fill never overwrites a cell staff recorded; a cell the extraction
/// recorded takes the newer reading. There is no per-field review (operator,
/// 25 September 2026): a filled cell is the Case's value, tagged Extracted
/// until staff change it.
/// </summary>
public static class OriginalReportPrefillPolicy
{
    /// <summary>
    /// The Automation actor a filled cell records, so a reader can tell a
    /// value read from the filed report from any other automation's.
    /// </summary>
    public const string RecorderId = "original-report-extraction";

    /// <summary>The four cells a reading fills, in section order.</summary>
    public static IReadOnlyList<string> Paths { get; } =
    [
        AssessmentVocabulary.OriginalReportAssessor,
        AssessmentVocabulary.OriginalReportDate,
        AssessmentVocabulary.OriginalReportRoadworthiness,
        AssessmentVocabulary.OriginalReportOutcome
    ];

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

        var outcome = Outcome(candidate.Damage.Outcome);
        var status = Outcome(candidate.Damage.Repairability);
        var unreadable = outcome.Unreadable || status.Unreadable
            || (outcome.Code is not null && status.Code is not null && outcome.Code != status.Code);
        return new(
            sha256,
            candidate.Identity.Issuer?.Value,
            candidate.Identity.ReportDate?.Value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            RoadworthinessCode(candidate.Damage.Roadworthiness?.Value),
            unreadable ? null : outcome.Code ?? status.Code,
            unreadable);
    }

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

    /// <summary>A fill lands only where staff have not recorded the cell.</summary>
    public static bool Fills(bool staffRecorded) => !staffRecorded;

    private static (string? Code, bool Unreadable) Outcome(ThirdPartyReportFact<string?>? fact) =>
        fact switch
        {
            null => (null, false),
            { Source.Disposition: SourceCandidateDisposition.Missing } => (null, false),
            { Value: null } => (null, true),
            { Value: var printed } => OutcomeCode(printed) is { } code ? (code, false) : (null, true)
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
