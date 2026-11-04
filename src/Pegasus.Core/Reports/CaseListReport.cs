using System.Globalization;
using System.Text;
using Pegasus.Core.Actors;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Reports;

/// <summary>Which reads the chosen columns need, so the adapter makes only those.</summary>
[Flags]
public enum CaseListFacts
{
    None = 0,
    ClaimData = 1,
    Assessment = 2,
    ReportFigures = 4,
    Sends = 8,
    Activity = 16,

    /// <summary>The agreed fee frozen in the report each report's first send carried, read with <see cref="Sends"/>.</summary>
    SentFees = 32
}

/// <summary>
/// One work's facts (MI-04): its own accepted case-data values and recorded
/// findings for the fields and paths asked for, the repair cost and
/// signatory of its latest confirmed report, and its report's sends
/// (<see cref="ReportSendPolicy"/>, R673): the first, who made it and the
/// agreed fee frozen in the report it carried (the fee MI-02 adds up), and
/// how many re-sends followed.
/// </summary>
public sealed record CaseListWorkFacts(
    IReadOnlyDictionary<string, string> Assessment,
    decimal? SentReportAgreedFee = null,
    decimal? LatestReportRepairCost = null,
    Guid? LatestReportSignatoryId = null,
    DateTimeOffset? FirstSentAtUtc = null,
    ActorKind? FirstSentByKind = null,
    string? FirstSentBySubjectId = null,
    int Resends = 0)
{
    public static CaseListWorkFacts Empty { get; } = new(new Dictionary<string, string>());

    /// <summary>The accepted value of each case-data field asked for, from this work's own copy; Create audit gives the Audit its copy.</summary>
    public IReadOnlyDictionary<string, string> ClaimData { get; init; } = new Dictionary<string, string>();
}

/// <summary>The Case's activity counts. Queries are mail classified as a query linked to the Case, disputes and amendment requests included.</summary>
public sealed record CaseListActivity(
    int Images,
    int ImagesInReport,
    int Documents,
    int Queries,
    int EmailsSent,
    int Chases,
    int OpenTasks,
    int Notes)
{
    public static CaseListActivity None { get; } = new(0, 0, 0, 0, 0, 0, 0, 0);
}

/// <summary>
/// One Case as the adapter reads it. <see cref="Primary"/> is the Case's own
/// work; <see cref="AuditWork"/> is an Inspection + Audit Case's Audit once it
/// has been created.
/// </summary>
public sealed record CaseListRecord(
    Guid CaseId,
    string Reference,
    string? AuditReference,
    string PrincipalCode,
    CaseType Type,
    DateOnly ReceivedDate,
    CaseLifecycleState? State,
    TriageState? TriageState,
    Guid? AssignedEngineerId,
    Guid? SignOffEngineerId,
    CaseListWorkFacts Primary,
    CaseListWorkFacts? AuditWork,
    CaseListActivity Activity)
{
    /// <summary>Whether the Case holds an open query, which its stage column reads as Query.</summary>
    public bool HasOpenQuery { get; init; }
}

/// <summary>
/// One report's row, the row the columns read (MI-04; R673): the Case's
/// record, which report the row is (<see cref="CaseListSide.Inspection"/> or
/// <see cref="CaseListSide.Audit"/>; <see cref="CaseListSide.Case"/> on a
/// Triage Case's one row, which has no report), that report's work, null for
/// an Inspection + Audit Case's Audit before Create audit, and the people
/// named.
/// </summary>
public sealed record CaseListRow(
    CaseListRecord Record,
    CaseListSide Report,
    CaseListWorkFacts? Work,
    string? AssignedEngineer,
    string? SignOffEngineer,
    string? SentBy)
{
    /// <summary>
    /// Whether this is the row of the Case's own work, which carries the
    /// Case-wide counts. An Inspection + Audit Case's Audit row reads them as
    /// not applicable, so a total counts each Case once.
    /// </summary>
    public bool IsCaseRow => Report != CaseListSide.Audit || Record.Type != CaseType.InspectionAndAudit;
}

/// <summary>
/// The Case list's filter. Both dates null is All time; otherwise both are
/// London dates and the period covers whole days. The field and path sets
/// name exactly what the chosen columns read.
/// </summary>
public sealed record CaseListQuery(
    DateOnly? ReceivedFrom,
    DateOnly? ReceivedTo,
    bool IncludeTriage,
    CaseListFacts Needs,
    IReadOnlySet<string> ClaimFields,
    IReadOnlySet<string> AssessmentPaths);

public interface ICaseListQueries
{
    Task<IReadOnlyList<CaseListRecord>> GetAsync(CaseListQuery query, CancellationToken cancellationToken);
}

/// <summary>The words the Case list borrows from the operator vocabulary, which Core cannot see.</summary>
public interface ICaseListLabels
{
    string CaseType(CaseType type);

    /// <summary>The stage word; a Case with an open query reads as Query.</summary>
    string Stage(CaseLifecycleState state, bool hasOpenQuery);

    string TriageStage(TriageState state);

    /// <summary>A recorded salvage category code other than <see cref="AssessmentReportContract.NoSalvageCategory"/>.</summary>
    string SalvageCategory(string code);
}

/// <summary>Which report a row is: the Inspection's, the Audit's, or none on a Triage Case's row.</summary>
public enum CaseListSide
{
    Case,
    Inspection,
    Audit
}

/// <summary>
/// The Case list's rules (MI-04). A column that does not apply to the row's
/// report or the Case's type reads <see cref="NotApplicable"/>; one that
/// applies with nothing recorded yet is blank. A failed or invalid read is
/// never a cell: the whole list is unavailable.
/// </summary>
public static class CaseListPolicy
{
    public const string NotApplicable = "N/A";
    public const string Agrees = "Agrees";
    public const string Differs = "Differs";

    /// <summary>The salvage code that records no category, in words that cannot be mistaken for <see cref="NotApplicable"/>.</summary>
    public const string NotCategorised = "Not categorised";

    /// <summary>The report a row is, in the Case-type words: Inspection or Audit; not applicable on a Triage Case's row.</summary>
    public static string ReportName(CaseListRow row, ICaseListLabels labels)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(labels);
        return row.Report switch
        {
            CaseListSide.Inspection => labels.CaseType(CaseType.Inspection),
            CaseListSide.Audit => labels.CaseType(CaseType.Audit),
            _ => NotApplicable
        };
    }

    /// <summary>Whether a Case has the report of a side: an Inspection + Audit Case has both.</summary>
    public static bool Applies(CaseListSide side, CaseType type) => side switch
    {
        CaseListSide.Case => true,
        CaseListSide.Inspection => type is CaseType.Inspection or CaseType.InspectionAndAudit,
        CaseListSide.Audit => type is CaseType.Audit or CaseType.InspectionAndAudit,
        _ => throw new ArgumentOutOfRangeException(nameof(side))
    };

    /// <summary>
    /// The work that is a side (<see cref="CaseWorkPolicy.IsAuditReport"/>):
    /// a standalone Audit's own work, or an Inspection + Audit Case's Audit
    /// work, which is null until Create audit.
    /// </summary>
    public static CaseListWorkFacts? Work(CaseListRecord record, CaseListSide side)
    {
        ArgumentNullException.ThrowIfNull(record);
        return side switch
        {
            CaseListSide.Inspection => record.Primary,
            CaseListSide.Audit => record.Type == CaseType.Audit ? record.Primary : record.AuditWork,
            _ => throw new ArgumentOutOfRangeException(nameof(side))
        };
    }

    /// <summary>
    /// Who wrote the report the Audit reviews: the recorded assessor on a
    /// standalone Audit, Collision Engineers on an Inspection + Audit Case.
    /// </summary>
    public static object? OriginalFirm(CaseListRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Type switch
        {
            CaseType.Audit => Value(record.Primary, AssessmentVocabulary.OriginalReportAssessor),
            CaseType.InspectionAndAudit => AssessmentReportWording.CompanyName,
            _ => NotApplicable
        };
    }

    /// <summary>
    /// The reviewed report's outcome code: the recorded original outcome on a
    /// standalone Audit, the Inspection's own outcome on an Inspection + Audit
    /// Case; null when nothing is recorded, and not applicable otherwise.
    /// </summary>
    public static string? OriginalOutcomeCode(CaseListRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return OriginalOutcomeCode(record.Type, record.Primary);
    }

    /// <summary>The same rule from the Case's own work's recorded facts, for a reader that has no Case list row.</summary>
    public static string? OriginalOutcomeCode(CaseType type, CaseListWorkFacts primary) => type switch
    {
        CaseType.Audit => Value(primary, AssessmentVocabulary.OriginalReportOutcome),
        CaseType.InspectionAndAudit => Value(primary, AssessmentVocabulary.Outcome),
        _ => null
    };

    public static object? OriginalOutcome(CaseListRecord record) =>
        !Applies(CaseListSide.Audit, record.Type)
            ? NotApplicable
            : OutcomeWords(OriginalOutcomeCode(record));

    /// <summary>Whether the Audit's outcome is the reviewed report's: blank until both are recorded.</summary>
    public static object? AuditAgrees(CaseListRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!Applies(CaseListSide.Audit, record.Type))
        {
            return NotApplicable;
        }

        var original = OriginalOutcomeCode(record);
        var audit = Value(Work(record, CaseListSide.Audit), AssessmentVocabulary.Outcome);
        return original is null || audit is null
            ? null
            : original == audit ? Agrees : Differs;
    }

    public static string? OutcomeWords(string? code) =>
        code is null ? null : CaseReportDeliveryNaming.OutcomeWords(AssessmentReportOutcomes.Parse(code));

    public static string? SalvageCategory(string? code, ICaseListLabels labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        return code switch
        {
            null => null,
            AssessmentReportContract.NoSalvageCategory => NotCategorised,
            _ => labels.SalvageCategory(code)
        };
    }

    public static string Stage(CaseListRecord record, ICaseListLabels labels)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(labels);
        return record.Type == CaseType.Triage
            ? labels.TriageStage(record.TriageState!.Value)
            : labels.Stage(record.State!.Value, record.HasOpenQuery);
    }

    public static string? Value(CaseListWorkFacts? work, string path) =>
        work is not null && work.Assessment.TryGetValue(path, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    public static decimal? Money(string? value) =>
        value is not null && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : null;

    /// <summary>A recorded ISO date as a date; any other text stays as it was recorded.</summary>
    public static object? Date(string? value) =>
        value is null
            ? null
            : DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date
                : value;
}

public sealed record CaseListRequest(
    ActionActor Actor,
    DateOnly? ReceivedFrom,
    DateOnly? ReceivedTo,
    bool IncludeTriage,
    IReadOnlyCollection<string> ColumnKeys);

public sealed record CaseListResult(
    DateOnly? ReceivedFrom,
    DateOnly? ReceivedTo,
    IReadOnlyList<CaseListColumn> Columns,
    IReadOnlyList<CaseListRow> Rows);

/// <summary>
/// MI-04: one row per report of each Case received in the period (or ever),
/// open and closed, with the columns the Administrator chose (R673). An
/// Inspection + Audit Case has two rows, its Inspection's then its Audit's;
/// every other Case has one. Triage Cases are included only when asked for.
/// </summary>
public sealed class GetCaseList(ICaseListQueries queries, IStaffAccountQueries staffAccounts)
{
    /// <summary>The reports a Case can have, in its rows' order.</summary>
    private static readonly CaseListSide[] ReportSides = [CaseListSide.Inspection, CaseListSide.Audit];

    /// <summary>A Triage Case's one row, which has no report.</summary>
    private static readonly CaseListSide[] TriageSides = [CaseListSide.Case];

    public async Task<CaseListResult> ExecuteAsync(CaseListRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ViewOperationalReports);
        if (request.ReceivedFrom.HasValue != request.ReceivedTo.HasValue
            || request.ReceivedFrom > request.ReceivedTo)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Choose both received dates, the first no later than the second, or All time.");
        }

        var columns = CaseListColumns.Resolve(request.ColumnKeys);
        var needs = columns.Aggregate(CaseListFacts.None, (all, column) => all | column.Needs);
        var records = await queries.GetAsync(
            new(
                request.ReceivedFrom,
                request.ReceivedTo,
                request.IncludeTriage,
                needs,
                columns.SelectMany(column => column.ClaimFields).ToHashSet(StringComparer.Ordinal),
                columns.SelectMany(column => column.AssessmentPaths).ToHashSet(StringComparer.Ordinal)),
            cancellationToken);
        ArgumentNullException.ThrowIfNull(records);
        if (records.Any(record => IsInvalid(record, request.IncludeTriage))
            || records.Select(record => record.CaseId).Distinct().Count() != records.Count)
        {
            throw new InvalidDataException("The Case list query returned an invalid row.");
        }

        var names = await ActorDisplayNames.ResolveStaffNamesAsync(
            staffAccounts,
            columns.Any(column => column.ShowsStaff) ? records.SelectMany(StaffIds) : [],
            cancellationToken);
        var profiles = columns.Any(column => column.Key == CaseListColumns.SignOffEngineer)
            ? await staffAccounts.ListSignOffEngineersAsync(cancellationToken)
            : [];
        var rows = records
            .OrderByDescending(record => record.ReceivedDate)
            .ThenBy(record => record.Reference, StringComparer.OrdinalIgnoreCase)
            .SelectMany(record => Sides(record).Select(side =>
            {
                var work = side == CaseListSide.Case ? record.Primary : CaseListPolicy.Work(record, side);
                return new CaseListRow(
                    record,
                    side,
                    work,
                    Staff(record.AssignedEngineerId, names),
                    Staff(SignOffEngineerId(record, work, profiles), names),
                    side == CaseListSide.Case ? null : SentBy(work, names));
            }))
            .ToArray();
        return new(request.ReceivedFrom, request.ReceivedTo, columns, rows);
    }

    /// <summary>A Case's rows: one for each report its type has, the Inspection's first; a Triage Case's one row.</summary>
    private static IEnumerable<CaseListSide> Sides(CaseListRecord record) =>
        record.Type == CaseType.Triage
            ? TriageSides
            : ReportSides.Where(side => CaseListPolicy.Applies(side, record.Type));

    /// <summary>
    /// The signatory of a row's report: the one frozen in that report's latest
    /// confirmed generation, else the one the Case would sign with now
    /// (<see cref="CaseSignOffEngineerResolver"/>). An Audit row before Create
    /// audit has no report yet, so no signatory.
    /// </summary>
    private static Guid? SignOffEngineerId(
        CaseListRecord record,
        CaseListWorkFacts? work,
        IReadOnlyList<SignOffEngineerProfile> profiles) =>
        work is null
            ? null
            : work.LatestReportSignatoryId
                ?? (record.Type == CaseType.Triage
                    ? null
                    : CaseSignOffEngineerResolver.Resolve(record.SignOffEngineerId, record.AssignedEngineerId, profiles)?.StaffId);

    private static IEnumerable<Guid> StaffIds(CaseListRecord record)
    {
        IEnumerable<Guid?> ids =
        [
            record.AssignedEngineerId,
            record.SignOffEngineerId,
            record.Primary.LatestReportSignatoryId,
            record.AuditWork?.LatestReportSignatoryId,
            SenderId(record.Primary),
            SenderId(record.AuditWork)
        ];
        return ids.Where(id => id.HasValue).Select(id => id!.Value);
    }

    private static Guid? SenderId(CaseListWorkFacts? work) =>
        work?.FirstSentByKind == ActorKind.Staff && Guid.TryParse(work.FirstSentBySubjectId, out var id) ? id : null;

    private static string? Staff(Guid? id, IReadOnlyDictionary<Guid, string> names) =>
        id is { } staffId ? ActorDisplayNames.Resolve(ActorKind.Staff, staffId.ToString("D"), names) : null;

    private static string? SentBy(CaseListWorkFacts? work, IReadOnlyDictionary<Guid, string> names) =>
        work?.FirstSentByKind is { } kind
            ? ActorDisplayNames.Resolve(kind, work.FirstSentBySubjectId ?? string.Empty, names)
            : null;

    private static bool IsInvalid(CaseListRecord record, bool includeTriage) =>
        record.CaseId == Guid.Empty
        || string.IsNullOrWhiteSpace(record.Reference)
        || string.IsNullOrWhiteSpace(record.PrincipalCode)
        || record.Primary is null
        || record.Activity is null
        || (record.Type == CaseType.Triage
            ? !includeTriage || record.TriageState is null
            : record.State is null)
        || (record.AuditWork is not null && record.Type != CaseType.InspectionAndAudit)
        || record.Activity.Images < 0
        || record.Activity.ImagesInReport < 0
        || record.Activity.ImagesInReport > record.Activity.Images
        || record.Activity.Documents < 0
        || record.Activity.Queries < 0
        || record.Activity.EmailsSent < 0
        || record.Activity.Chases < 0
        || record.Activity.OpenTasks < 0
        || record.Activity.Notes < 0;
}

/// <summary>The Case list as one typed sheet, its columns in catalogue order.</summary>
public static class CaseListTables
{
    public const string SheetName = "Cases";

    public static WorkbookSheet Build(CaseListResult result, ICaseListLabels labels)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(labels);
        return new(
            SheetName,
            result.Columns.Select(column => new WorkbookColumn(column.Title, column.Kind)).ToArray(),
            result.Rows
                .Select(row => (IReadOnlyList<object?>)result.Columns.Select(column => column.Read(row, labels)).ToArray())
                .ToArray(),
            Totals: true);
    }
}

/// <summary>
/// One sheet as CSV, the same columns and values the workbook carries: text
/// through the formula-injection guard, numbers plain, money to the penny and
/// dates as ISO dates. CRLF-terminated; the caller owns the response.
/// </summary>
public static class WorkbookSheetCsv
{
    public static string Write(WorkbookSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var builder = new StringBuilder()
            .AppendJoin(',', sheet.Columns.Select(column => EscapeField(column.Title)))
            .Append("\r\n");
        foreach (var row in sheet.Rows)
        {
            builder.AppendJoin(',', row.Select(Field)).Append("\r\n");
        }

        return builder.ToString();
    }

    /// <summary>RFC 4180 quoting, and a leading <c>'</c> on text a spreadsheet would read as a formula.</summary>
    public static string EscapeField(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var safe = value.Length > 0 && value[0] is '=' or '+' or '-' or '@'
            ? "'" + value
            : value;
        return safe.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? safe
            : $"\"{safe.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string Field(object? value) => value switch
    {
        null => string.Empty,
        int number => number.ToString(CultureInfo.InvariantCulture),
        long number => number.ToString(CultureInfo.InvariantCulture),
        decimal money => money.ToString("0.00", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset moment => LondonCalendar.TimeAt(moment).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        TimeSpan duration => duration.ToString("c", CultureInfo.InvariantCulture),
        string text => EscapeField(text),
        _ => EscapeField(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)
    };
}

/// <summary>The Case list's workbook download.</summary>
public sealed class ExportCaseList(IWorkbookWriter writer)
{
    public byte[] Execute(ActionActor actor, CaseListResult result, ICaseListLabels labels)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.ViewOperationalReports);
        return writer.Write([CaseListTables.Build(result, labels)]);
    }
}
