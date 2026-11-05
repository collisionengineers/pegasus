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
    private static readonly DateTimeOffset SentAt = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EveryColumnHasOneStableKeyAndTheCaseGroupIsTheDefault()
    {
        var keys = CaseListColumns.All.Select(column => column.Key).ToArray();

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("report", keys);
        Assert.Contains("resends", keys);
        Assert.Contains("outcome", keys);
        Assert.Contains("original.agrees", keys);
        Assert.Contains("engineer.assigned", keys);
        Assert.Contains(CaseListColumns.SignOffEngineer, keys);
        Assert.Equal(
            ["case.reference", "report", "case.audit_reference", "case.principal", "case.type", "case.received", "case.stage", "sent", "resends"],
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
    public async Task AnInspectionAndAuditCaseIsTwoRowsItsInspectionsThenItsAudits()
    {
        var rows = await RowsAsync(Record(
            CaseType.InspectionAndAudit,
            primary: Work((AssessmentVocabulary.Outcome, "repairable")),
            audit: Work((AssessmentVocabulary.Outcome, "total_loss"))));

        Assert.Equal([CaseListSide.Inspection, CaseListSide.Audit], rows.Select(row => row.Report));
        Assert.Equal(["QDOS26001", "QDOS26001"], rows.Select(row => Cell("case.reference", row)));
        // The Report column names each row's report in the Case-type words.
        Assert.Equal(["Inspection", "Audit"], rows.Select(row => Cell("report", row)));
        Assert.Equal(["InspectionAndAudit", "InspectionAndAudit"], rows.Select(row => Cell("case.type", row)));
        // The Audit reference names the Audit's report, so only its row carries it.
        Assert.Equal([CaseListPolicy.NotApplicable, "a.QDOS26001"], rows.Select(row => Cell("case.audit_reference", row)));
        // Each row reads its own report's work.
        Assert.Equal(["Repairable", "Total loss"], rows.Select(row => Cell("outcome", row)));
        // The Audit reviews Collision Engineers' own Inspection; the Inspection's row reviews nothing.
        Assert.Equal([CaseListPolicy.NotApplicable, AssessmentReportWording.CompanyName], rows.Select(row => Cell("original.firm", row)));
        Assert.Equal([CaseListPolicy.NotApplicable, "Repairable"], rows.Select(row => Cell("original.outcome", row)));
        Assert.Equal([CaseListPolicy.NotApplicable, CaseListPolicy.Differs], rows.Select(row => Cell("original.agrees", row)));
    }

    /// <summary>R673: an Inspection + Audit Case's Audit row is listed before Create audit, blank.</summary>
    [Fact]
    public async Task BeforeCreateAuditTheAuditRowIsListedWithItsReportCellsBlankNotNotApplicable()
    {
        var rows = await RowsAsync(Record(
            CaseType.InspectionAndAudit,
            primary: Work((AssessmentVocabulary.Outcome, "repairable"), (AssessmentVocabulary.ValueEngineer, "900")) with
            {
                FirstSentAtUtc = SentAt,
                SentReportAgreedFee = 120m,
                ClaimData = Claims((CaseDataFieldNames.VehicleRegistration, "AB12 CDE"))
            }));

        Assert.Equal([CaseListSide.Inspection, CaseListSide.Audit], rows.Select(row => row.Report));
        var audit = rows[1];
        Assert.Null(audit.Work);
        Assert.Equal("Audit", Cell("report", audit));
        Assert.Equal("a.QDOS26001", Cell("case.audit_reference", audit));
        string[] blank =
        [
            "sent", "resends", "outcome", "salvage_category", "original.agrees", "sent_by",
            "agreed_fee", "engineers_value", "repair_cost", "vehicle.registration"
        ];
        Assert.All(blank, key => Assert.Null(Cell(key, audit)));
        // No Audit report yet, so no signatory.
        Assert.Null(audit.SignOffEngineer);
        // What the Audit reviews is known already: the Case's own Inspection.
        Assert.Equal(AssessmentReportWording.CompanyName, Cell("original.firm", audit));
        Assert.Equal("Repairable", Cell("original.outcome", audit));

        var inspection = rows[0];
        Assert.Equal(new DateOnly(2026, 9, 3), Cell("sent", inspection));
        Assert.Equal(120m, Cell("agreed_fee", inspection));
        Assert.Equal(900m, Cell("engineers_value", inspection));
        Assert.Equal("AB12 CDE", Cell("vehicle.registration", inspection));
    }

    [Fact]
    public async Task AnInspectionCaseIsOneInspectionRowThatReviewsNothing()
    {
        var row = Assert.Single(await RowsAsync(Record(
            CaseType.Inspection,
            primary: Work((AssessmentVocabulary.Outcome, "total_loss"), (AssessmentVocabulary.SalvageCategory, "S")))));

        Assert.Equal(CaseListSide.Inspection, row.Report);
        Assert.Equal("Inspection", Cell("report", row));
        Assert.Equal("Total loss", Cell("outcome", row));
        Assert.Equal("Cat S", Cell("salvage_category", row));
        Assert.Equal(CaseListPolicy.NotApplicable, Cell("case.audit_reference", row));
        Assert.Equal(CaseListPolicy.NotApplicable, Cell("original.firm", row));
        Assert.Equal(CaseListPolicy.NotApplicable, Cell("original.outcome", row));
        Assert.Equal(CaseListPolicy.NotApplicable, Cell("original.agrees", row));
    }

    [Fact]
    public async Task AStandaloneAuditIsOneAuditRowThatComparesItsOutcomeWithTheOriginalFirmsReport()
    {
        var row = Assert.Single(await RowsAsync(Record(CaseType.Audit, primary: Work(
            (AssessmentVocabulary.OriginalReportAssessor, "Smith Assessors"),
            (AssessmentVocabulary.OriginalReportOutcome, "repairable"),
            (AssessmentVocabulary.Outcome, "total_loss")))));

        Assert.Equal(CaseListSide.Audit, row.Report);
        Assert.Equal("Audit", Cell("report", row));
        Assert.Equal("Total loss", Cell("outcome", row));
        Assert.Equal("Smith Assessors", Cell("original.firm", row));
        Assert.Equal("Repairable", Cell("original.outcome", row));
        Assert.Equal(CaseListPolicy.Differs, Cell("original.agrees", row));
        // The Audit reference is an Inspection + Audit Case's.
        Assert.Equal(CaseListPolicy.NotApplicable, Cell("case.audit_reference", row));
    }

    [Fact]
    public async Task ASalvageCodeOfNoCategoryIsNotCategorisedNotNotApplicable()
    {
        var row = Assert.Single(await RowsAsync(Record(
            CaseType.Inspection,
            primary: Work((AssessmentVocabulary.SalvageCategory, AssessmentReportContract.NoSalvageCategory)))));

        Assert.Equal(CaseListPolicy.NotCategorised, Cell("salvage_category", row));
    }

    [Fact]
    public async Task ATriageCaseIsOneRowWithItsOwnStageAndNoReport()
    {
        var row = Assert.Single(await RowsAsync(Record(CaseType.Triage, state: null, triageState: TriageState.AwaitingInformation)));

        Assert.Equal(CaseListSide.Case, row.Report);
        Assert.Equal("Triage:AwaitingInformation", Cell("case.stage", row));
        string[] notApplicable =
        [
            "report", "case.audit_reference", "sent", "resends", "outcome", "salvage_category",
            "original.firm", "original.outcome", "original.agrees", "sent_by",
            "agreed_fee", "engineers_value", "repair_cost"
        ];
        Assert.All(notApplicable, key => Assert.Equal(CaseListPolicy.NotApplicable, Cell(key, row)));
        // No report, so no signatory.
        Assert.Null(row.SignOffEngineer);
    }

    [Fact]
    public async Task ActivityCountsAreReadOnceForEachCase()
    {
        var activity = CaseListActivity.None with { Images = 3, Notes = 2 };

        var both = await RowsAsync(Record(CaseType.InspectionAndAudit, audit: Work()) with { Activity = activity });
        var standalone = Assert.Single(await RowsAsync(Record(CaseType.Audit) with { Activity = activity }));

        // An Inspection + Audit Case's counts are on its Inspection row; its
        // Audit row reads N/A, so a total counts the Case once.
        Assert.Equal([3, CaseListPolicy.NotApplicable], both.Select(row => Cell("activity.images", row)));
        Assert.Equal([2, CaseListPolicy.NotApplicable], both.Select(row => Cell("activity.notes", row)));
        // A standalone Audit's one row is the Case's own.
        Assert.Equal(3, Cell("activity.images", standalone));
    }

    [Fact]
    public async Task ReSendsAreBlankUntilAReportIsFirstSentAndThenCountItsLaterSends()
    {
        // One e-mail carried both reports, a first send of each; the Inspection's was sent twice more.
        var both = await RowsAsync(Record(
            CaseType.InspectionAndAudit,
            primary: Work() with { FirstSentAtUtc = SentAt, Resends = 2 },
            audit: Work() with { FirstSentAtUtc = SentAt }));
        var unsent = Assert.Single(await RowsAsync(Record(CaseType.Inspection)));

        Assert.Equal([new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 3)], both.Select(row => Cell("sent", row)));
        Assert.Equal([2, 0], both.Select(row => Cell("resends", row)));
        Assert.Null(Cell("sent", unsent));
        Assert.Null(Cell("resends", unsent));
    }

    [Fact]
    public async Task EachRowReadsItsOwnWorksClaimData()
    {
        var rows = await RowsAsync(Record(
            CaseType.InspectionAndAudit,
            primary: Work() with
            {
                ClaimData = Claims((CaseDataFieldNames.VehicleRegistration, "AB12 CDE"), (CaseDataFieldNames.ClaimNumber, "C100"))
            },
            audit: Work() with { ClaimData = Claims((CaseDataFieldNames.VehicleRegistration, "AB12 CDF")) }));

        Assert.Equal(["AB12 CDE", "AB12 CDF"], rows.Select(row => Cell("vehicle.registration", row)));
        // The Audit's own copy has no claim number: blank, never the Inspection's.
        Assert.Equal(["C100", null], rows.Select(row => Cell("claim.number", row)));
    }

    [Fact]
    public async Task ClaimDatesBecomeDatesAndOtherTextStaysAsRecorded()
    {
        var row = Assert.Single(await RowsAsync(Record(CaseType.Inspection, primary: Work() with
        {
            ClaimData = Claims(
                (CaseDataFieldNames.IncidentDate, "2026-09-30"),
                (CaseDataFieldNames.InspectionDate, "next week"),
                (CaseDataFieldNames.VehicleRegistration, "AB12 CDE"))
        })));

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
        var list = new GetCaseList(queries, new Accounts());

        await list.ExecuteAsync(
            new(Administrator(), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), true, ["vehicle.registration", "outcome"]),
            default);
        await list.ExecuteAsync(new(Administrator(), null, null, false, ["agreed_fee", "resends"]), default);

        Assert.Collection(
            queries.Asked,
            query =>
            {
                Assert.Equal(CaseListFacts.ClaimData | CaseListFacts.Assessment, query.Needs);
                Assert.Equal([CaseDataFieldNames.VehicleRegistration], query.ClaimFields);
                Assert.Equal([AssessmentVocabulary.Outcome], query.AssessmentPaths);
                Assert.True(query.IncludeTriage);
            },
            query =>
            {
                // The agreed fee is the one frozen in the report its first send carried (R673).
                Assert.Equal(CaseListFacts.Sends | CaseListFacts.SentFees, query.Needs);
                Assert.Empty(query.ClaimFields);
                Assert.Empty(query.AssessmentPaths);
                Assert.False(query.IncludeTriage);
            });
    }

    [Fact]
    public async Task RowsAreNewestReceivedFirstThenByReferenceEachCasesInspectionBeforeItsAudit()
    {
        var rows = await RowsAsync(
            Record(CaseType.Inspection) with { CaseId = Guid.NewGuid(), Reference = "QDOS26003" },
            Record(CaseType.InspectionAndAudit) with { CaseId = Guid.NewGuid(), Reference = "QDOS26002", AuditReference = "a.QDOS26002" },
            Record(CaseType.Audit) with { CaseId = Guid.NewGuid(), Reference = "QDOS26004", ReceivedDate = new DateOnly(2026, 9, 2) });

        Assert.Equal(["QDOS26004", "QDOS26002", "QDOS26002", "QDOS26003"], rows.Select(row => row.Record.Reference));
        Assert.Equal(
            [CaseListSide.Audit, CaseListSide.Inspection, CaseListSide.Audit, CaseListSide.Inspection],
            rows.Select(row => row.Report));
    }

    [Fact]
    public async Task PeopleAreNamedAndTheSignatoryIsTheReportsOwnOnceOneIsFrozen()
    {
        var records = new[]
        {
            Record(CaseType.Inspection, primary: Work() with
            {
                LatestReportSignatoryId = Signatory,
                FirstSentAtUtc = SentAt,
                FirstSentByKind = ActorKind.Automation,
                FirstSentBySubjectId = "automation"
            }) with { Reference = "QDOS26002", ReceivedDate = new DateOnly(2026, 9, 1) },
            Record(CaseType.Inspection) with { CaseId = Guid.NewGuid(), Reference = "QDOS26003", ReceivedDate = new DateOnly(2026, 9, 2) }
        };

        var result = await new GetCaseList(new Queries(records), new Accounts()).ExecuteAsync(
            new(Administrator(), null, null, false, ["case.reference", "engineer.assigned", CaseListColumns.SignOffEngineer, "sent_by", "sent"]),
            default);

        // Newest received first.
        Assert.Equal(["QDOS26003", "QDOS26002"], result.Rows.Select(row => row.Record.Reference));
        var sent = result.Rows[1];
        Assert.Equal("engineer", sent.AssignedEngineer);
        Assert.Equal("signatory", sent.SignOffEngineer);
        Assert.Equal("Automation", sent.SentBy);
        Assert.Equal(new DateOnly(2026, 9, 3), Cell("sent", sent));
        // No report yet: the Case would sign with its assigned Engineer.
        Assert.Equal("engineer", result.Rows[0].SignOffEngineer);
    }

    [Fact]
    public async Task EachReportsRowNamesItsOwnSignatoryAndSender()
    {
        var rows = await RowsAsync(Record(
            CaseType.InspectionAndAudit,
            primary: Work() with
            {
                LatestReportSignatoryId = Signatory,
                FirstSentAtUtc = SentAt,
                FirstSentByKind = ActorKind.Staff,
                FirstSentBySubjectId = Engineer.ToString("D")
            },
            audit: Work()));

        // The Audit has no confirmed report yet, so it would sign with the
        // Case's Engineer, and nobody has sent it.
        Assert.Equal(["signatory", "engineer"], rows.Select(row => row.SignOffEngineer));
        Assert.Equal(["engineer", null], rows.Select(row => row.SentBy));
    }

    [Theory]
    [InlineData("engineer.assigned", 1)]
    [InlineData("sent_by", 1)]
    [InlineData("case.reference", 0)]
    public async Task StaffNamesAreReadOnlyWhenAChosenColumnShowsAPerson(string key, int reads)
    {
        var accounts = new Accounts();

        await new GetCaseList(new Queries([Record(CaseType.Inspection)]), accounts).ExecuteAsync(
            new(Administrator(), null, null, false, ["case.reference", key]), default);

        Assert.Equal(reads, accounts.NameReads);
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
            Record(CaseType.Inspection) with { Activity = CaseListActivity.None with { Queries = -1 } }
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
    public async Task TheSheetAndItsCsvCarryTheSameTypedCellsOneRowPerReport()
    {
        var record = Record(
            CaseType.InspectionAndAudit,
            primary: Work((AssessmentVocabulary.ValueEngineer, "1250.5")) with
            {
                FirstSentAtUtc = SentAt,
                SentReportAgreedFee = 180m,
                ClaimData = Claims((CaseDataFieldNames.ClaimantName, "=HYPERLINK(\"x\")"))
            },
            audit: Work() with { FirstSentAtUtc = SentAt, SentReportAgreedFee = 95m });

        // Chosen in any order, the columns come back in catalogue order.
        var result = await new GetCaseList(new Queries([record]), new Accounts()).ExecuteAsync(
            new(Administrator(), null, null, false, ["activity.images", "agreed_fee", "engineers_value", "claim.claimant_name", "case.received", "report", "case.reference"]),
            default);

        var sheet = CaseListTables.Build(result, Labels.Instance);
        Assert.Equal(CaseListTables.SheetName, sheet.Name);
        Assert.True(sheet.Totals);
        Assert.Equal(
            [WorkbookColumnKind.Text, WorkbookColumnKind.Text, WorkbookColumnKind.Date, WorkbookColumnKind.Text, WorkbookColumnKind.Money, WorkbookColumnKind.Money, WorkbookColumnKind.Count],
            sheet.Columns.Select(column => column.Kind));
        Assert.Collection(
            sheet.Rows,
            row => Assert.Equal(["QDOS26001", "Inspection", new DateOnly(2026, 9, 1), "=HYPERLINK(\"x\")", 180m, 1250.5m, 0], row),
            row => Assert.Equal(["QDOS26001", "Audit", new DateOnly(2026, 9, 1), null, 95m, null, CaseListPolicy.NotApplicable], row));

        var csv = WorkbookSheetCsv.Write(sheet);
        Assert.Equal(
            "Case/PO,Report,Received date,Claimant name,Agreed fee,Engineer's Value,Images\r\n"
            + "QDOS26001,Inspection,2026-09-01,\"'=HYPERLINK(\"\"x\"\")\",180.00,1250.50,0\r\n"
            + "QDOS26001,Audit,2026-09-01,,95.00,,N/A\r\n",
            csv);
        Assert.Equal("-12.50", WorkbookSheetCsv.Write(new("S", [new("Money", WorkbookColumnKind.Money)], [[-12.5m]], false)).Split("\r\n")[1]);
    }

    private static object? Cell(string key, CaseListRow row) =>
        CaseListColumns.All.Single(column => column.Key == key).Read(row, Labels.Instance);

    /// <summary>The rows the list makes of the records, one per report, with every column chosen.</summary>
    private static async Task<IReadOnlyList<CaseListRow>> RowsAsync(params CaseListRecord[] records) =>
        (await new GetCaseList(new Queries(records), new Accounts()).ExecuteAsync(
            new(Administrator(), null, null, IncludeTriage: true, [.. CaseListColumns.All.Select(column => column.Key)]),
            default)).Rows;

    private static CaseListWorkFacts Work(params (string Path, string Value)[] fields) =>
        new(fields.ToDictionary(field => field.Path, field => field.Value));

    private static Dictionary<string, string> Claims(params (string Field, string Value)[] fields) =>
        fields.ToDictionary(field => field.Field, field => field.Value);

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
        primary ?? CaseListWorkFacts.Empty,
        audit,
        CaseListActivity.None);

    private static ActionActor Administrator() => ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    private sealed class Labels : ICaseListLabels
    {
        public static Labels Instance { get; } = new();

        public string CaseType(CaseType type) => type.ToString();

        public string Stage(CaseLifecycleState state, bool hasOpenQuery) => hasOpenQuery ? "Query" : state.ToString();

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
        public int NameReads { get; private set; }

        public Task<StaffAccountQuerySlice> ListAsync(int offset, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by the list.");

        public Task<StaffAccountSummary?> GetAsync(Guid staffId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by the list.");

        public Task<IReadOnlyList<StaffAccountSummary>> GetManyAsync(IReadOnlyCollection<Guid> staffIds, CancellationToken cancellationToken)
        {
            NameReads++;
            return Task.FromResult<IReadOnlyList<StaffAccountSummary>>(
                new StaffAccountSummary[]
                {
                    new(Engineer, "engineer", true, false, StaffRole.Engineer),
                    new(Signatory, "signatory", true, false, StaffRole.Engineer)
                }.Where(account => staffIds.Contains(account.Id)).ToArray());
        }

        public Task<SignOffSignature?> GetSignOffSignatureAsync(Guid staffId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SignOffEngineerProfile>>(
            [
                new(Engineer, "Engineer", null, IsDefault: false),
                new(Signatory, "Signatory", null, IsDefault: true)
            ]);
    }
}
