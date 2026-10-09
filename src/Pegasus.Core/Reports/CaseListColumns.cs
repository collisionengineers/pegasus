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
    /// <summary>The case-data fields the column reads from the Case's own work.</summary>
    public IReadOnlyList<string> ClaimFields { get; init; } = [];

    /// <summary>The recorded findings the column reads from each work.</summary>
    public IReadOnlyList<string> AssessmentPaths { get; init; } = [];
}

/// <summary>
/// The one catalogue of Case list columns (MI-04), in output order. A key is
/// what a preset stores, so a key never changes meaning; a column split by
/// work has an <c>.inspection</c> and an <c>.audit</c> key.
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

    public static bool TryGet(string key, out CaseListColumn column) =>
        ByKey.TryGetValue(key, out column!);

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
            Column("case.audit_reference", "Audit reference", WorkbookColumnKind.Text, CaseListColumnGroup.Case,
                (row, _) => row.Record.Type == CaseType.InspectionAndAudit
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
        };
        columns.AddRange(Sided("sent", "Report first sent", WorkbookColumnKind.Date, CaseListColumnGroup.Case,
            CaseListFacts.Sends, [], (work, _) => work.FirstSentAtUtc is { } sent ? LondonCalendar.DateAt(sent) : null));

        columns.AddRange(Sided("outcome", "Outcome", WorkbookColumnKind.Text, CaseListColumnGroup.Outcomes,
            CaseListFacts.Assessment, [AssessmentVocabulary.Outcome],
            (work, _) => CaseListPolicy.OutcomeWords(CaseListPolicy.Value(work, AssessmentVocabulary.Outcome))));
        columns.AddRange(Sided("salvage_category", "Salvage category", WorkbookColumnKind.Text, CaseListColumnGroup.Outcomes,
            CaseListFacts.Assessment, [AssessmentVocabulary.SalvageCategory],
            (work, labels) => CaseListPolicy.SalvageCategory(CaseListPolicy.Value(work, AssessmentVocabulary.SalvageCategory), labels)));
        columns.Add(Column("original.firm", "Original firm", WorkbookColumnKind.Text, CaseListColumnGroup.Outcomes,
            (row, _) => CaseListPolicy.OriginalFirm(row.Record), CaseListFacts.Assessment)
            with { AssessmentPaths = [AssessmentVocabulary.OriginalReportAssessor] });
        columns.Add(Column("original.outcome", "Original outcome", WorkbookColumnKind.Text, CaseListColumnGroup.Outcomes,
            (row, _) => CaseListPolicy.OriginalOutcome(row.Record), CaseListFacts.Assessment)
            with { AssessmentPaths = [AssessmentVocabulary.OriginalReportOutcome, AssessmentVocabulary.Outcome] });
        columns.Add(Column("original.agrees", "Audit agrees with original", WorkbookColumnKind.Text, CaseListColumnGroup.Outcomes,
            (row, _) => CaseListPolicy.AuditAgrees(row.Record), CaseListFacts.Assessment)
            with { AssessmentPaths = [AssessmentVocabulary.OriginalReportOutcome, AssessmentVocabulary.Outcome] });

        columns.Add(Column("engineer.assigned", "Assigned engineer", WorkbookColumnKind.Text, CaseListColumnGroup.Engineers,
            (row, _) => row.AssignedEngineer));
        columns.Add(Column(SignOffEngineer, "Sign-off engineer", WorkbookColumnKind.Text, CaseListColumnGroup.Engineers,
            (row, _) => row.SignOffEngineer, CaseListFacts.ReportFigures));
        columns.Add(Column("sent_by.inspection", ReportColumnTitles.Inspection("Report sent by"), WorkbookColumnKind.Text,
            CaseListColumnGroup.Engineers,
            (row, _) => CaseListPolicy.Applies(CaseListSide.Inspection, row.Record.Type) ? row.InspectionSentBy : CaseListPolicy.NotApplicable,
            CaseListFacts.Sends));
        columns.Add(Column("sent_by.audit", ReportColumnTitles.Audit("Report sent by"), WorkbookColumnKind.Text,
            CaseListColumnGroup.Engineers,
            (row, _) => CaseListPolicy.Applies(CaseListSide.Audit, row.Record.Type) ? row.AuditSentBy : CaseListPolicy.NotApplicable,
            CaseListFacts.Sends));

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

        columns.AddRange(Sided("agreed_fee", "Agreed fee", WorkbookColumnKind.Money, CaseListColumnGroup.Money,
            CaseListFacts.ReportFigures, [], (work, _) => work.FirstReportAgreedFee));
        columns.AddRange(Money("engineers_value", "Engineer's Value", AssessmentVocabulary.ValueEngineer));
        columns.AddRange(Money("retail_value", "Retail value", AssessmentVocabulary.ValueRetail));
        columns.AddRange(Money("trade_value", "Trade value", AssessmentVocabulary.ValueTrade));
        columns.AddRange(Sided("repair_cost", "Repair cost", WorkbookColumnKind.Money, CaseListColumnGroup.Money,
            CaseListFacts.ReportFigures, [], (work, _) => work.LatestReportRepairCost));
        columns.AddRange(Money("salvage_value", "Salvage value", AssessmentVocabulary.SalvageValue));
        columns.AddRange(Money("recovery_charge", "Recovery charge", AssessmentVocabulary.CostRecoveryCharge));

        columns.Add(Claim("party.repairer", "Repairer", CaseDataFieldNames.RepairerName) with { Group = CaseListColumnGroup.PartiesAndActivity });
        columns.Add(Claim("party.claim_source", "Claim source", CaseDataFieldNames.ClaimSourceName) with { Group = CaseListColumnGroup.PartiesAndActivity });
        columns.Add(Claim("party.storage", "Storage", CaseDataFieldNames.StorageBusinessName) with { Group = CaseListColumnGroup.PartiesAndActivity });
        columns.Add(Activity("activity.images", "Images", activity => activity.Images));
        columns.Add(Activity("activity.images_in_report", "Images in report", activity => activity.ImagesInReport));
        columns.Add(Activity("activity.documents", "Documents", activity => activity.Documents));
        columns.Add(Activity("activity.queries", "Queries", activity => activity.Queries));
        columns.Add(Activity("activity.disputes", "Disputes", activity => activity.Disputes));
        columns.Add(Activity("activity.amendment_requests", "Amendment requests", activity => activity.AmendmentRequests));
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

    /// <summary>One measure split by work: N/A where the side does not apply, blank before its work exists.</summary>
    private static IEnumerable<CaseListColumn> Sided(
        string key,
        string measure,
        WorkbookColumnKind kind,
        CaseListColumnGroup group,
        CaseListFacts needs,
        IReadOnlyList<string> assessmentPaths,
        Func<CaseListWorkFacts, ICaseListLabels, object?> read)
    {
        yield return Side(CaseListSide.Inspection, $"{key}.inspection", ReportColumnTitles.Inspection(measure));
        yield return Side(CaseListSide.Audit, $"{key}.audit", ReportColumnTitles.Audit(measure));

        CaseListColumn Side(CaseListSide side, string sideKey, string title) =>
            new(sideKey, title, kind, group, needs, (row, labels) =>
                !CaseListPolicy.Applies(side, row.Record.Type)
                    ? CaseListPolicy.NotApplicable
                    : CaseListPolicy.Work(row.Record, side) is { } work ? read(work, labels) : null)
            {
                AssessmentPaths = assessmentPaths
            };
    }

    private static IEnumerable<CaseListColumn> Money(string key, string measure, string path) =>
        Sided(key, measure, WorkbookColumnKind.Money, CaseListColumnGroup.Money, CaseListFacts.Assessment, [path],
            (work, _) => CaseListPolicy.Money(CaseListPolicy.Value(work, path)));

    private static CaseListColumn Claim(
        string key,
        string title,
        string field,
        WorkbookColumnKind kind = WorkbookColumnKind.Text) =>
        new(key, title, kind, CaseListColumnGroup.ClaimAndVehicle, CaseListFacts.ClaimData, (row, _) =>
        {
            var value = row.Record.ClaimData.TryGetValue(field, out var recorded) && !string.IsNullOrWhiteSpace(recorded)
                ? recorded
                : null;
            return kind == WorkbookColumnKind.Date ? CaseListPolicy.Date(value) : value;
        })
        {
            ClaimFields = [field]
        };

    private static CaseListColumn Activity(string key, string title, Func<CaseListActivity, int> read) =>
        new(key, title, WorkbookColumnKind.Count, CaseListColumnGroup.PartiesAndActivity, CaseListFacts.Activity,
            (row, _) => read(row.Record.Activity));
}
