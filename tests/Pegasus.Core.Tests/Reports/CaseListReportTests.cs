using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Reports;

public sealed class CaseListReportTests
{
    private static readonly Guid Engineer = Guid.NewGuid();
    private static readonly Guid Signatory = Guid.NewGuid();

    [Fact]
    public void EveryColumnHasOneStableKeyAndTheCaseGroupIsTheDefault()
    {
        var keys = CaseListColumns.All.Select(column => column.Key).ToArray();

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("outcome.inspection", keys);
        Assert.Contains("outcome.audit", keys);
        Assert.Contains("original.agrees", keys);
        Assert.Contains("engineer.assigned", keys);
        Assert.Contains(CaseListColumns.SignOffEngineer, keys);
        Assert.Equal(
            ["case.reference", "case.audit_reference", "case.principal", "case.type", "case.received", "case.stage", "sent.inspection", "sent.audit"],
            CaseListColumns.DefaultKeys);
        Assert.Equal(
            Enum.GetValues<CaseListColumnGroup>(),
            CaseListColumns.All.Select(column => column.Group).Distinct());
    }

    [Fact]
    public void ChosenColumnsComeBackOnceInCatalogueOrderAndUnknownOrNoColumnsAreRefused()
    {
        var columns = CaseListColumns.Resolve(["engineer.assigned", "case.reference", "engineer.assigned"]);

        Assert.Equal(["case.reference", "engineer.assigned"], columns.Select(column => column.Key));
        Assert.Throws<ArgumentException>(() => CaseListColumns.Resolve([]));
        Assert.Throws<ArgumentException>(() => CaseListColumns.Resolve(["case.reference", "report.types"]));
    }

    [Fact]
    public void AnInspectionCaseReadsNotApplicableForEveryAuditColumn()
    {
        var row = Row(Record(CaseType.Inspection, primary: Work(("assessment.outcome", "total_loss"), ("assessment.category", "S"))));

        Assert.Equal("Total loss", Cell("outcome.inspection", row));
        Assert.Equal("Cat S", Cell("salvage_category.inspection", row));
        Assert.Equal(CaseListPolicy.NotApplicable, Cell("outcome.audit", row));
        Assert.Equal(CaseListPolicy.NotApplicable, Cell("original.firm", row));
        Assert.Equal(CaseListPolicy.NotApplicable, Cell("original.outcome", row));
        Assert.Equal(CaseListPolicy.NotApplicable, Cell("original.agrees", row));
        Assert.Equal(CaseListPolicy.NotApplicable, Cell("case.audit_reference", row));
        Assert.Equal(CaseListPolicy.NotApplicable, Cell("agreed_fee.audit", row));
        Assert.Equal(CaseListPolicy.NotApplicable, Cell("sent_by.audit", row));
    }

    [Fact]
    public void AStandaloneAuditComparesItsOutcomeWithTheOriginalFirmsReport()
    {
        var row = Row(Record(CaseType.Audit, primary: Work(
            (AssessmentVocabulary.OriginalReportAssessor, "Smith Assessors"),
            (AssessmentVocabulary.OriginalReportOutcome, "repairable"),
            (AssessmentVocabulary.Outcome, "total_loss"))));

        Assert.Equal(CaseListPolicy.NotApplicable, Cell("outcome.inspection", row));
        Assert.Equal("Total loss", Cell("outcome.audit", row));
        Assert.Equal("Smith Assessors", Cell("original.firm", row));
        Assert.Equal("Repairable", Cell("original.outcome", row));
        Assert.Equal(CaseListPolicy.Differs, Cell("original.agrees", row));
    }

    [Fact]
    public void AnInspectionAndAuditCaseReviewsCollisionEngineersOwnInspection()
    {
        var withoutAudit = Row(Record(CaseType.InspectionAndAudit, primary: Work((AssessmentVocabulary.Outcome, "repairable"))));

        Assert.Equal(AssessmentReportWording.CompanyName, Cell("original.firm", withoutAudit));
        Assert.Equal("Repairable", Cell("original.outcome", withoutAudit));
        // The Audit applies but has not been created: blank, not N/A.
        Assert.Null(Cell("outcome.audit", withoutAudit));
        Assert.Null(Cell("original.agrees", withoutAudit));

        var withAudit = Row(Record(
            CaseType.InspectionAndAudit,
            primary: Work((AssessmentVocabulary.Outcome, "repairable")),
            audit: Work((AssessmentVocabulary.Outcome, "repairable")) with { FirstReportAgreedFee = 95m }));

        Assert.Equal(CaseListPolicy.Agrees, Cell("original.agrees", withAudit));
        Assert.Equal(95m, Cell("agreed_fee.audit", withAudit));
        Assert.Null(Cell("agreed_fee.inspection", withAudit));
        Assert.Equal("a.QDOS26001", Cell("case.audit_reference", withAudit));
    }

    [Fact]
    public void ASalvageCodeOfNoCategoryIsNotCategorisedNotNotApplicable()
    {
        var row = Row(Record(CaseType.Inspection, primary: Work((AssessmentVocabulary.SalvageCategory, AssessmentReportContract.NoSalvageCategory))));

        Assert.Equal(CaseListPolicy.NotCategorised, Cell("salvage_category.inspection", row));
    }

    [Fact]
    public void ATriageCaseShowsItsOwnStageAndNoWorkColumns()
    {
        var row = Row(Record(CaseType.Triage, state: null, triageState: TriageState.AwaitingInformation));

        Assert.Equal("Triage:AwaitingInformation", Cell("case.stage", row));
        Assert.Equal(CaseListPolicy.NotApplicable, Cell("outcome.inspection", row));
        Assert.Equal(CaseListPolicy.NotApplicable, Cell("engineers_value.audit", row));
    }

    [Fact]
    public void ClaimDatesBecomeDatesAndOtherTextStaysAsRecorded()
    {
        var row = Row(Record(CaseType.Inspection) with
        {
            ClaimData = new Dictionary<string, string>
            {
                [CaseDataFieldNames.IncidentDate] = "2026-09-30",
                [CaseDataFieldNames.InspectionDate] = "next week",
                [CaseDataFieldNames.VehicleRegistration] = "AB12 CDE"
            }
        });

        Assert.Equal(new DateOnly(2026, 9, 30), Cell("claim.incident_date", row));
        Assert.Equal("next week", Cell("inspection.date", row));
        Assert.Equal("AB12 CDE", Cell("vehicle.registration", row));
        Assert.Null(Cell("claim.claimant_name", row));
    }

    [Fact]
    public async Task TheListIsForAdministratorsAndNeedsBothDatesOrAllTime()
    {
        var list = new GetCaseList(new Queries([]), new Accounts());

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => list.ExecuteAsync(
            new(ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]), null, null, false, ["case.reference"]), default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => list.ExecuteAsync(
            new(Administrator(), new DateOnly(2026, 9, 1), null, false, ["case.reference"]), default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => list.ExecuteAsync(
            new(Administrator(), new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 1), false, ["case.reference"]), default));
        await Assert.ThrowsAsync<ArgumentException>(() => list.ExecuteAsync(
            new(Administrator(), null, null, false, []), default));

        var allTime = await list.ExecuteAsync(new(Administrator(), null, null, false, ["case.reference"]), default);
        Assert.Empty(allTime.Rows);
    }

    [Fact]
    public async Task TheQueryIsAskedForOnlyWhatTheChosenColumnsRead()
    {
        var queries = new Queries([]);

        await new GetCaseList(queries, new Accounts()).ExecuteAsync(
            new(Administrator(), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), true, ["vehicle.registration", "outcome.audit"]),
            default);

        var query = Assert.Single(queries.Asked);
        Assert.Equal(CaseListFacts.ClaimData | CaseListFacts.Assessment, query.Needs);
        Assert.Equal([CaseDataFieldNames.VehicleRegistration], query.ClaimFields);
        Assert.Equal([AssessmentVocabulary.Outcome], query.AssessmentPaths);
        Assert.True(query.IncludeTriage);
    }

    [Fact]
    public async Task PeopleAreNamedAndTheSignatoryIsTheReportsOwnOnceOneIsFrozen()
    {
        var records = new[]
        {
            Record(CaseType.Inspection, primary: Work() with
            {
                LatestReportSignatoryId = Signatory,
                FirstSentAtUtc = new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero),
                FirstSentByKind = ActorKind.Automation,
                FirstSentBySubjectId = "automation"
            }) with { Reference = "QDOS26002", ReceivedDate = new DateOnly(2026, 9, 1) },
            Record(CaseType.Inspection) with { CaseId = Guid.NewGuid(), Reference = "QDOS26003", ReceivedDate = new DateOnly(2026, 9, 2) }
        };

        var result = await new GetCaseList(new Queries(records), new Accounts()).ExecuteAsync(
            new(Administrator(), null, null, false, ["case.reference", "engineer.assigned", CaseListColumns.SignOffEngineer, "sent_by.inspection", "sent.inspection"]),
            default);

        // Newest received first.
        Assert.Equal(["QDOS26003", "QDOS26002"], result.Rows.Select(row => row.Record.Reference));
        var sent = result.Rows[1];
        Assert.Equal("engineer", sent.AssignedEngineer);
        Assert.Equal("signatory", sent.SignOffEngineer);
        Assert.Equal("Automation", sent.InspectionSentBy);
        Assert.Equal(new DateOnly(2026, 9, 3), CaseListColumns.All.Single(column => column.Key == "sent.inspection").Read(sent, Labels.Instance));
        // No report yet: the Case would sign with its assigned Engineer.
        Assert.Equal("engineer", result.Rows[0].SignOffEngineer);
    }

    [Fact]
    public async Task AnInvalidAdapterRowMakesTheWholeListUnavailable()
    {
        IReadOnlyList<CaseListRecord> invalid =
        [
            Record(CaseType.Inspection) with { Reference = " " },
            Record(CaseType.Inspection) with { State = null },
            Record(CaseType.Triage, state: null, triageState: TriageState.Open),
            Record(CaseType.Inspection, audit: Work()),
            Record(CaseType.Inspection) with { Activity = CaseListActivity.None with { Images = 1, ImagesInReport = 2 } },
            Record(CaseType.Inspection) with { Activity = CaseListActivity.None with { Queries = 1, AmendmentRequests = 2 } }
        ];

        foreach (var record in invalid)
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => new GetCaseList(new Queries([record]), new Accounts())
                .ExecuteAsync(new(Administrator(), null, null, IncludeTriage: false, ["case.reference"]), default));
        }

        var duplicate = Record(CaseType.Inspection);
        await Assert.ThrowsAsync<InvalidDataException>(() => new GetCaseList(new Queries([duplicate, duplicate]), new Accounts())
            .ExecuteAsync(new(Administrator(), null, null, false, ["case.reference"]), default));
    }

    [Fact]
    public void TheSheetAndItsCsvCarryTheSameTypedCells()
    {
        var row = Row(Record(CaseType.Audit, primary: Work((AssessmentVocabulary.ValueEngineer, "1250.5")) with { FirstReportAgreedFee = 180m }) with
        {
            ClaimData = new Dictionary<string, string> { [CaseDataFieldNames.ClaimantName] = "=HYPERLINK(\"x\")" }
        });
        var result = new CaseListResult(null, null,
            CaseListColumns.Resolve(["case.reference", "case.received", "claim.claimant_name", "engineers_value.inspection", "engineers_value.audit", "agreed_fee.audit", "activity.images"]),
            [row]);

        var sheet = CaseListTables.Build(result, Labels.Instance);
        Assert.Equal(CaseListTables.SheetName, sheet.Name);
        Assert.True(sheet.Totals);
        Assert.Equal(
            [WorkbookColumnKind.Text, WorkbookColumnKind.Date, WorkbookColumnKind.Text, WorkbookColumnKind.Money, WorkbookColumnKind.Money, WorkbookColumnKind.Money, WorkbookColumnKind.Count],
            sheet.Columns.Select(column => column.Kind));
        Assert.Equal(["QDOS26001", new DateOnly(2026, 9, 1), "=HYPERLINK(\"x\")", 180m, CaseListPolicy.NotApplicable, 1250.5m, 0], sheet.Rows.Single());

        var csv = WorkbookSheetCsv.Write(sheet);
        Assert.Equal(
            "Case/PO,Received date,Claimant name,Agreed fee · Audit,Engineer's Value · Inspection,Engineer's Value · Audit,Images\r\n"
            + "QDOS26001,2026-09-01,\"'=HYPERLINK(\"\"x\"\")\",180.00,N/A,1250.50,0\r\n",
            csv);
        Assert.Equal("-12.50", WorkbookSheetCsv.Write(new("S", [new("Money", WorkbookColumnKind.Money)], [[-12.5m]], false)).Split("\r\n")[1]);
    }

    private static object? Cell(string key, CaseListRow row) =>
        CaseListColumns.All.Single(column => column.Key == key).Read(row, Labels.Instance);

    private static CaseListRow Row(CaseListRecord record) => new(record, null, null, null, null);

    private static CaseListWorkFacts Work(params (string Path, string Value)[] fields) =>
        new(fields.ToDictionary(field => field.Path, field => field.Value));

    private static CaseListRecord Record(
        CaseType type,
        CaseListWorkFacts? primary = null,
        CaseListWorkFacts? audit = null,
        CaseLifecycleState? state = CaseLifecycleState.Review,
        TriageState? triageState = null) => new(
        Guid.Parse("11111111-1111-4111-8111-111111111111"),
        "QDOS26001",
        type == CaseType.InspectionAndAudit ? "a.QDOS26001" : null,
        "QDOS",
        type,
        new DateOnly(2026, 9, 1),
        state,
        triageState,
        Engineer,
        null,
        new Dictionary<string, string>(),
        primary ?? CaseListWorkFacts.Empty,
        audit,
        CaseListActivity.None);

    private static ActionActor Administrator() => ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    private sealed class Labels : ICaseListLabels
    {
        public static Labels Instance { get; } = new();

        public string CaseType(CaseType type) => type.ToString();

        public string Stage(CaseLifecycleState state) => state.ToString();

        public string TriageStage(TriageState state) => $"Triage:{state}";

        public string SalvageCategory(string code) => $"Cat {code}";
    }

    private sealed class Queries(IReadOnlyList<CaseListRecord> records) : ICaseListQueries
    {
        public List<CaseListQuery> Asked { get; } = [];

        public Task<IReadOnlyList<CaseListRecord>> GetAsync(CaseListQuery query, CancellationToken cancellationToken)
        {
            Asked.Add(query);
            return Task.FromResult(records);
        }
    }

    private sealed class Accounts : IStaffAccountQueries
    {
        public Task<StaffAccountQuerySlice> ListAsync(int offset, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by the list.");

        public Task<StaffAccountSummary?> GetAsync(Guid staffId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by the list.");

        public Task<IReadOnlyList<StaffAccountSummary>> GetManyAsync(IReadOnlyCollection<Guid> staffIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StaffAccountSummary>>(
                new StaffAccountSummary[]
                {
                    new(Engineer, "engineer", true, false, StaffRole.Engineer),
                    new(Signatory, "signatory", true, false, StaffRole.Engineer)
                }.Where(account => staffIds.Contains(account.Id)).ToArray());

        public Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SignOffEngineerProfile>>(
            [
                new(Engineer, "Engineer", null, [1], "image/png", IsDefault: false),
                new(Signatory, "Signatory", null, [1], "image/png", IsDefault: true)
            ]);
    }
}
