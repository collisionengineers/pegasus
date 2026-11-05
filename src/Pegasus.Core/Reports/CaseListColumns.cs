using Pegasus.Core;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;

namespace Pegasus.Core.Reports;

/// <summary>The Case list's column groups, in the order the picker and the export show them.</summary>
public enum CaseListColumnGroup
{
    Case,
    Outcomes,
    Engineers,
    ClaimAndVehicle,
    Money,
    PartiesAndActivity
}

/// <summary>
/// One Case list column: a stable key a preset stores, its heading, its
/// spreadsheet type, the reads it needs and how it reads a row.
/// </summary>
public sealed record CaseListColumn(
    string Key,
    string Title,
    WorkbookColumnKind Kind,
    CaseListColumnGroup Group,
    CaseListFacts Needs,
    Func<CaseListRow, ICaseListLabels, object?> Read)
{
    /// <summary>The case-data fields the column reads from the row's work.</summary>
    public IReadOnlyList<string> ClaimFields { get; init; } = [];

    /// <summary>The recorded findings the column reads from each work.</summary>
    public IReadOnlyList<string> AssessmentPaths { get; init; } = [];

    /// <summary>True when the column prints a staff member's name, so the download resolves staff names for it.</summary>
    public bool ShowsStaff { get; init; }
}

/// <summary>
/// The one catalogue of Case list columns (MI-04), in output order. A key is
/// what a preset stores, so a key never changes meaning. Each row is one
/// report (R673), so a measure of a report is one column read from the row's
/// work.
/// </summary>
public static class CaseListColumns
{
    public const string SignOffEngineer = "engineer.sign_off";

    public static IReadOnlyList<CaseListColumn> All { get; } = Build();

    private static readonly Dictionary<string, CaseListColumn> ByKey =
        All.ToDictionary(column => column.Key, StringComparer.Ordinal);

    /// <summary>The columns ticked when nothing else is chosen: the Case group.</summary>
    public static IReadOnlyList<string> DefaultKeys { get; } =
        [.. All.Where(column => column.Group == CaseListColumnGroup.Case).Select(column => column.Key)];

    /// <summary>
    /// Chosen keys as columns in catalogue order, each once. At least one is
    /// required and every key must be in the catalogue.
    /// </summary>
    public static IReadOnlyList<CaseListColumn> Resolve(IReadOnlyCollection<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count == 0)
        {
            throw new ArgumentException("Choose at least one column.", nameof(keys));
        }
        if (keys.Any(key => key is null || !ByKey.ContainsKey(key)))
        {
            throw new ArgumentException("A chosen column is not in the Case list.", nameof(keys));
        }

        var chosen = keys.ToHashSet(StringComparer.Ordinal);
        return [.. All.Where(column => chosen.Contains(column.Key))];
    }

    private static List<CaseListColumn> Build()
    {
        var columns = new List<CaseListColumn>
        {
            Column("case.reference", "Case/PO", WorkbookColumnKind.Text, CaseListColumnGroup.Case,
                (row, _) => row.Record.Reference),
            Column("report", "Report", WorkbookColumnKind.Text, CaseListColumnGroup.Case,
                CaseListPolicy.ReportName),
            // The Audit reference names an Inspection + Audit Case's Audit
            // report, so only that report's row carries it.
            Column("case.audit_reference", "Audit reference", WorkbookColumnKind.Text, CaseListColumnGroup.Case,
                (row, _) => row.Report == CaseListSide.Audit && row.Record.Type == CaseType.InspectionAndAudit
                    ? row.Record.AuditReference
                    : CaseListPolicy.NotApplicable),
            Column("case.principal", "Principal", WorkbookColumnKind.Text, CaseListColumnGroup.Case,
                (row, _) => row.Record.PrincipalCode),
            Column("case.type", "Case type", WorkbookColumnKind.Text, CaseListColumnGroup.Case,
                (row, labels) => labels.CaseType(row.Record.Type)),
            Column("case.received", "Received date", WorkbookColumnKind.Date, CaseListColumnGroup.Case,
                (row, _) => row.Record.ReceivedDate),
            Column("case.stage", "Stage", WorkbookColumnKind.Text, CaseListColumnGroup.Case,
                (row, labels) => CaseListPolicy.Stage(row.Record, labels)),
            Report("sent", "Report first sent", WorkbookColumnKind.Date, CaseListColumnGroup.Case, CaseListFacts.Sends,
                (work, _) => work.FirstSentAtUtc is { } sent ? LondonCalendar.DateAt(sent) : null),
            // Blank until the report is first sent, then how many re-sends followed.
            Report("resends", "Re-sends", WorkbookColumnKind.Count, CaseListColumnGroup.Case, CaseListFacts.Sends,
                (work, _) => work.FirstSentAtUtc is null ? null : (object)work.Resends),
        };

        columns.Add(Report("outcome", "Outcome", WorkbookColumnKind.Text, CaseListColumnGroup.Outcomes, CaseListFacts.Assessment,
            (work, _) => CaseListPolicy.OutcomeWords(CaseListPolicy.Value(work, AssessmentVocabulary.Outcome)))
            with { AssessmentPaths = [AssessmentVocabulary.Outcome] });
        columns.Add(Report("salvage_category", "Salvage category", WorkbookColumnKind.Text, CaseListColumnGroup.Outcomes, CaseListFacts.Assessment,
            (work, labels) => CaseListPolicy.SalvageCategory(CaseListPolicy.Value(work, AssessmentVocabulary.SalvageCategory), labels))
            with { AssessmentPaths = [AssessmentVocabulary.SalvageCategory] });
        columns.Add(AuditReview("original.firm", "Original firm", CaseListPolicy.OriginalFirm)
            with { AssessmentPaths = [AssessmentVocabulary.OriginalReportAssessor] });
        columns.Add(AuditReview("original.outcome", "Original outcome", CaseListPolicy.OriginalOutcome)
            with { AssessmentPaths = [AssessmentVocabulary.OriginalReportOutcome, AssessmentVocabulary.Outcome] });
        columns.Add(AuditReview("original.agrees", "Audit agrees with original", CaseListPolicy.AuditAgrees)
            with { AssessmentPaths = [AssessmentVocabulary.OriginalReportOutcome, AssessmentVocabulary.Outcome] });

        columns.Add(Column("engineer.assigned", "Assigned engineer", WorkbookColumnKind.Text, CaseListColumnGroup.Engineers,
            (row, _) => row.AssignedEngineer) with { ShowsStaff = true });
        columns.Add(Column(SignOffEngineer, "Sign-off engineer", WorkbookColumnKind.Text, CaseListColumnGroup.Engineers,
            (row, _) => row.SignOffEngineer, CaseListFacts.ReportFigures) with { ShowsStaff = true });
        columns.Add(Column("sent_by", "Report sent by", WorkbookColumnKind.Text, CaseListColumnGroup.Engineers,
            (row, _) => row.Report == CaseListSide.Case ? CaseListPolicy.NotApplicable : row.SentBy,
            CaseListFacts.Sends) with { ShowsStaff = true });

        columns.Add(Claim("claim.number", "Claim number", CaseDataFieldNames.ClaimNumber));
        columns.Add(Claim("claim.claimant_name", "Claimant name", CaseDataFieldNames.ClaimantName));
        columns.Add(Claim("claim.claimant_contact_number", "Claimant contact number", CaseDataFieldNames.ClaimantContactNumber));
        columns.Add(Claim("claim.claimant_address", "Claimant address", CaseDataFieldNames.ClaimantAddress));
        columns.Add(Claim("claim.incident_date", "Incident date", CaseDataFieldNames.IncidentDate, WorkbookColumnKind.Date));
        columns.Add(Claim("vehicle.registration", "Registration", CaseDataFieldNames.VehicleRegistration));
        columns.Add(Claim("vehicle.make", "Make", CaseDataFieldNames.VehicleMake));
        columns.Add(Claim("vehicle.model", "Model", CaseDataFieldNames.VehicleModel));
        columns.Add(Claim("vehicle.year", "Year", CaseDataFieldNames.VehicleYear));
        columns.Add(Claim("vehicle.mileage", "Mileage", CaseDataFieldNames.VehicleMileage));
        columns.Add(Claim("inspection.date", "Inspection date", CaseDataFieldNames.InspectionDate, WorkbookColumnKind.Date));

        // The fee frozen in the report the first send carried, blank until it
        // is sent: the fee MI-02 adds up (R673).
        columns.Add(Report("agreed_fee", "Agreed fee", WorkbookColumnKind.Money, CaseListColumnGroup.Money,
            CaseListFacts.Sends | CaseListFacts.SentFees, (work, _) => work.SentReportAgreedFee));
        columns.Add(Money("engineers_value", "Engineer's Value", AssessmentVocabulary.ValueEngineer));
        columns.Add(Money("retail_value", "Retail value", AssessmentVocabulary.ValueRetail));
        columns.Add(Money("trade_value", "Trade value", AssessmentVocabulary.ValueTrade));
        columns.Add(Report("repair_cost", "Repair cost", WorkbookColumnKind.Money, CaseListColumnGroup.Money,
            CaseListFacts.ReportFigures, (work, _) => work.LatestReportRepairCost));
        columns.Add(Money("salvage_value", "Salvage value", AssessmentVocabulary.SalvageValue));
        columns.Add(Money("recovery_charge", "Recovery charge", AssessmentVocabulary.CostRecoveryCharge));

        columns.Add(Claim("party.repairer", "Repairer", CaseDataFieldNames.RepairerName) with { Group = CaseListColumnGroup.PartiesAndActivity });
        columns.Add(Claim("party.claim_source", "Claim source", CaseDataFieldNames.ClaimSourceName) with { Group = CaseListColumnGroup.PartiesAndActivity });
        columns.Add(Claim("party.storage", "Storage", CaseDataFieldNames.StorageBusinessName) with { Group = CaseListColumnGroup.PartiesAndActivity });
        columns.Add(Activity("activity.images", "Images", activity => activity.Images));
        columns.Add(Activity("activity.images_in_report", "Images in report", activity => activity.ImagesInReport));
        columns.Add(Activity("activity.documents", "Documents", activity => activity.Documents));
        columns.Add(Activity("activity.queries", "Queries", activity => activity.Queries));
        columns.Add(Activity("activity.emails_sent", "E-mails sent", activity => activity.EmailsSent));
        columns.Add(Activity("activity.chases", "Chases", activity => activity.Chases));
        columns.Add(Activity("activity.open_tasks", "Open tasks", activity => activity.OpenTasks));
        columns.Add(Activity("activity.notes", "Notes", activity => activity.Notes));
        return columns;
    }

    private static CaseListColumn Column(
        string key,
        string title,
        WorkbookColumnKind kind,
        CaseListColumnGroup group,
        Func<CaseListRow, ICaseListLabels, object?> read,
        CaseListFacts needs = CaseListFacts.None) =>
        new(key, title, kind, group, needs, read);

    /// <summary>A measure of the row's report: N/A on a Triage Case's row, blank before the report's work exists.</summary>
    private static CaseListColumn Report(
        string key,
        string title,
        WorkbookColumnKind kind,
        CaseListColumnGroup group,
        CaseListFacts needs,
        Func<CaseListWorkFacts, ICaseListLabels, object?> read) =>
        new(key, title, kind, group, needs, (row, labels) =>
            row.Report == CaseListSide.Case
                ? CaseListPolicy.NotApplicable
                : row.Work is { } work ? read(work, labels) : null);

    /// <summary>The Audit's review of the report it reviews: on an Audit report's row, N/A on every other.</summary>
    private static CaseListColumn AuditReview(string key, string title, Func<CaseListRecord, object?> read) =>
        new(key, title, WorkbookColumnKind.Text, CaseListColumnGroup.Outcomes, CaseListFacts.Assessment, (row, _) =>
            row.Report == CaseListSide.Audit ? read(row.Record) : CaseListPolicy.NotApplicable);

    private static CaseListColumn Money(string key, string measure, string path) =>
        Report(key, measure, WorkbookColumnKind.Money, CaseListColumnGroup.Money, CaseListFacts.Assessment,
            (work, _) => CaseListPolicy.Money(CaseListPolicy.Value(work, path)))
            with { AssessmentPaths = [path] };

    /// <summary>A case-data field from the row's work: each report reads its own copy.</summary>
    private static CaseListColumn Claim(
        string key,
        string title,
        string field,
        WorkbookColumnKind kind = WorkbookColumnKind.Text) =>
        new(key, title, kind, CaseListColumnGroup.ClaimAndVehicle, CaseListFacts.ClaimData, (row, _) =>
        {
            var value = row.Work is { } work && work.ClaimData.TryGetValue(field, out var recorded) && !string.IsNullOrWhiteSpace(recorded)
                ? recorded
                : null;
            return kind == WorkbookColumnKind.Date ? CaseListPolicy.Date(value) : value;
        })
        {
            ClaimFields = [field]
        };

    /// <summary>A Case-wide count: on the Case's own row, N/A on an Inspection + Audit Case's Audit row so a total counts each Case once.</summary>
    private static CaseListColumn Activity(string key, string title, Func<CaseListActivity, int> read) =>
        new(key, title, WorkbookColumnKind.Count, CaseListColumnGroup.PartiesAndActivity, CaseListFacts.Activity,
            (row, _) => row.IsCaseRow ? (object)read(row.Record.Activity) : CaseListPolicy.NotApplicable);
}
