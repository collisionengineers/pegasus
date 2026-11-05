using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>MI-04's Case list and its presets over the migrated LocalDB.</summary>
[Trait("Category", "SqlServer")]
public sealed class CaseListPersistenceTests
{
    private static readonly DateTimeOffset Received = new(2031, 6, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly CaseListQuery Everything = new(
        null,
        null,
        IncludeTriage: false,
        CaseListFacts.ClaimData | CaseListFacts.Assessment | CaseListFacts.ReportFigures | CaseListFacts.Sends | CaseListFacts.SentFees | CaseListFacts.Activity,
        new HashSet<string> { CaseDataFieldNames.VehicleRegistration },
        new HashSet<string> { AssessmentVocabulary.Outcome, AssessmentVocabulary.OriginalReportAssessor });

    [Fact]
    public async Task EachCaseIsOneRecordWithItsWorksFiguresSendsAndActivity()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var engineer = Guid.NewGuid();
        var signatory = Guid.NewGuid();
        var sender = Guid.NewGuid();
        Guid inspection, audit, inspectionAndAudit, auditWork, triage, outside;
        await using (var context = await database.CreateContextAsync())
        {
            var principal = await SeededPrincipals.QdosAsync(context);
            inspection = AddCase(context, principal, 1, "inspection", Received.AddDays(-30), engineer);
            audit = AddCase(context, principal, 2, "audit", Received);
            inspectionAndAudit = AddCase(context, principal, 3, "inspection_and_audit", Received, auditReference: true);
            triage = AddCase(context, principal, 4, "triage", Received);
            outside = AddCase(context, principal, 5, "inspection", Received.AddDays(-60));
            auditWork = AddAuditWork(context, inspectionAndAudit, Received.AddDays(2));

            // The origin receipt is the received date, not the Case's creation.
            context.CaseDataSnapshots.Add(CaseData(inspection, "AB12 CDE"));
            context.CaseAssessmentFields.AddRange(
                Field(inspection, AssessmentVocabulary.Outcome, "repairable"),
                Field(audit, AssessmentVocabulary.OriginalReportAssessor, "Smith Assessors"),
                Field(audit, AssessmentVocabulary.Outcome, "total_loss"),
                Field(inspectionAndAudit, AssessmentVocabulary.Outcome, "repairable"),
                Field(auditWork, AssessmentVocabulary.Outcome, "repairable"));

            var first = Report(context, inspection, inspection, Received.AddDays(1), $"{{\"agreedFee\":180.00,\"costs\":{{\"totals\":{{\"printed\":{{\"gross\":900.10}}}}}},\"signatoryStaffId\":\"{signatory:D}\"}}");
            Report(context, inspection, inspection, Received.AddDays(3), "{\"agreedFee\":250.00,\"costs\":{\"totals\":{\"printed\":{\"gross\":1234.56}}}}");
            var auditReport = Report(context, inspectionAndAudit, auditWork, Received.AddDays(4), "{\"agreedFee\":95.00,\"costs\":{\"totals\":{\"printed\":{\"gross\":0}}}}");
            context.Set<StaffMailSendOperationEntity>().AddRange(
                Send(first, sender, Received.AddDays(2)),
                // The same report again, by someone else: a re-send.
                Send(first, Guid.NewGuid(), Received.AddDays(5)),
                Send(auditReport, sender, Received.AddDays(6)));

            var document = Guid.NewGuid();
            var version = Guid.NewGuid();
            context.AddRange(
                new CaseDocumentEntity { Id = document, CaseId = inspection, Ordinal = 1, SourceOccurrenceIdentity = "images" },
                new DocumentVersionEntity
                {
                    Id = version, DocumentId = document, Version = 1, FileName = "front.jpg", MediaType = "image/jpeg",
                    ContentLength = 1, Sha256 = new string('e', 64), CustodyStatus = DocumentCustodyStatus.Confirmed,
                    CreatedAtUtc = Received, CreatedBy = "test", IsCurrent = true
                },
                Occurrence(inspection, document, version, 1, DocumentSemanticRole.Image, inReport: true),
                Occurrence(inspection, document, version, 2, DocumentSemanticRole.Image, inReport: false),
                Occurrence(inspection, document, version, 3, DocumentSemanticRole.Correspondence, inReport: false),
                new CaseTaskEntity { Id = Guid.NewGuid(), CaseId = inspection, Description = "Chase images", State = "Open", Version = 0, ConcurrencyToken = Guid.NewGuid(), CreatedAtUtc = Received },
                new CaseTaskEntity { Id = Guid.NewGuid(), CaseId = inspection, Description = "Done", State = "Completed", Version = 0, ConcurrencyToken = Guid.NewGuid(), CreatedAtUtc = Received },
                new CaseWorkflowEventEntity
                {
                    Id = Guid.NewGuid(), CaseId = inspection, EventType = AddCaseNote.EventType, OperationKey = "note:case-list",
                    RequestHash = new string('f', 64), ActorKind = "Staff", ActorSubjectId = engineer.ToString("D"), ActorRolesJson = "[]",
                    Reason = "Spoke to the repairer", OccurredAtUtc = Received, BeforeVersion = 1, AfterVersion = 2
                });
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<ICaseListQueries>();

        var all = await queries.GetAsync(Everything, CancellationToken.None);
        Assert.Equal(4, all.Count);
        Assert.DoesNotContain(all, record => record.CaseId == triage);

        var inspectionRecord = all.Single(record => record.CaseId == inspection);
        Assert.Equal(CaseType.Inspection, inspectionRecord.Type);
        Assert.Equal(new DateOnly(2031, 6, 10), inspectionRecord.ReceivedDate);
        Assert.Equal(CaseLifecycleState.Review, inspectionRecord.State);
        Assert.Equal(engineer, inspectionRecord.AssignedEngineerId);
        Assert.Equal("AB12 CDE", inspectionRecord.Primary.ClaimData[CaseDataFieldNames.VehicleRegistration]);
        Assert.Equal("repairable", inspectionRecord.Primary.Assessment[AssessmentVocabulary.Outcome]);
        // The fee is the one frozen in the report the first send carried; the
        // repair cost and signatory the latest report's. The later send is a
        // re-send, so the first send's date and sender stay the report's.
        Assert.Equal(180.00m, inspectionRecord.Primary.SentReportAgreedFee);
        Assert.Equal(1234.56m, inspectionRecord.Primary.LatestReportRepairCost);
        Assert.Null(inspectionRecord.Primary.LatestReportSignatoryId);
        Assert.Equal(Received.AddDays(2), inspectionRecord.Primary.FirstSentAtUtc);
        Assert.Equal(ActorKind.Staff, inspectionRecord.Primary.FirstSentByKind);
        Assert.Equal(sender.ToString("D"), inspectionRecord.Primary.FirstSentBySubjectId);
        Assert.Equal(1, inspectionRecord.Primary.Resends);
        Assert.Null(inspectionRecord.AuditWork);
        // Every Sent e-mail on the Case counts, the re-send included.
        Assert.Equal(new CaseListActivity(2, 1, 1, 0, 2, 0, 1, 1), inspectionRecord.Activity);

        var auditRecord = all.Single(record => record.CaseId == audit);
        Assert.Equal("Smith Assessors", auditRecord.Primary.Assessment[AssessmentVocabulary.OriginalReportAssessor]);
        Assert.Null(auditRecord.Primary.SentReportAgreedFee);

        var bothRecord = all.Single(record => record.CaseId == inspectionAndAudit);
        Assert.Equal("a.QDOS31003", bothRecord.AuditReference);
        Assert.NotNull(bothRecord.AuditWork);
        Assert.Equal(95.00m, bothRecord.AuditWork!.SentReportAgreedFee);
        Assert.Equal(Received.AddDays(6), bothRecord.AuditWork.FirstSentAtUtc);
        Assert.Equal(0, bothRecord.AuditWork.Resends);
        Assert.Null(bothRecord.Primary.SentReportAgreedFee);
        Assert.Null(bothRecord.Primary.FirstSentAtUtc);

        var withTriage = await queries.GetAsync(Everything with { IncludeTriage = true }, CancellationToken.None);
        Assert.Contains(withTriage, record => record.CaseId == triage && record.TriageState is not null && record.State is null);

        var june = await queries.GetAsync(
            Everything with { ReceivedFrom = new DateOnly(2031, 6, 1), ReceivedTo = new DateOnly(2031, 6, 10) },
            CancellationToken.None);
        Assert.Equal(new[] { inspection, audit, inspectionAndAudit }.Order(), june.Select(record => record.CaseId).Order());
        Assert.DoesNotContain(june, record => record.CaseId == outside);
        Assert.Equal(3, june.Count);
    }

    /// <summary>
    /// R673: the list is one row per report. An Inspection + Audit Case is two
    /// rows, its Inspection's then its Audit's, each with its own first send,
    /// sender, fee and case data; one e-mail carrying both reports is the
    /// first send of each. The Audit's row is listed, blank, before Create
    /// audit, and the Case-wide counts are on the Case's own row only.
    /// </summary>
    [Fact]
    public async Task EachReportIsOneRowWithItsOwnFirstSendSenderFeeAndCaseData()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        Guid apart, together, awaiting, triage;
        await using (var context = await database.CreateContextAsync())
        {
            var principal = await SeededPrincipals.QdosAsync(context);
            var first = await AddStaffAsync(context, "first.sender");
            var second = await AddStaffAsync(context, "second.sender");
            apart = AddCase(context, principal, 1, "inspection_and_audit", Received, auditReference: true);
            together = AddCase(context, principal, 2, "inspection_and_audit", Received, auditReference: true);
            awaiting = AddCase(context, principal, 3, "inspection_and_audit", Received);
            triage = AddCase(context, principal, 4, "triage", Received);
            var apartAuditWork = AddAuditWork(context, apart, Received.AddDays(2));
            var togetherAuditWork = AddAuditWork(context, together, Received.AddDays(1));

            // Create audit gave the Audit its own copy of the case data, corrected since.
            context.CaseDataSnapshots.AddRange(CaseData(apart, "AB12 CDE"), CaseData(apartAuditWork, "CD34 EFG"));

            // Each report sent on its own, by a different person.
            var apartInspectionReport = Report(context, apart, apart, Received.AddDays(1), "{\"agreedFee\":300.00}");
            var apartAuditReport = Report(context, apart, apartAuditWork, Received.AddDays(3), "{\"agreedFee\":95.00}");
            // Both reports in one e-mail.
            var togetherInspectionReport = Report(context, together, together, Received.AddDays(1), "{\"agreedFee\":310.00}");
            var togetherAuditReport = Report(context, together, togetherAuditWork, Received.AddDays(2), "{\"agreedFee\":105.00}");
            context.Set<StaffMailSendOperationEntity>().AddRange(
                Send(apartInspectionReport, first, Received.AddDays(2)),
                Send(apartAuditReport, second, Received.AddDays(4)),
                Send(togetherInspectionReport, first, Received.AddDays(5), alsoCarries: togetherAuditReport));
            await context.SaveChangesAsync();
        }

        var result = await CaseListAsync(
            database,
            includeTriage: true,
            "report", "case.audit_reference", "sent", "resends", "sent_by", "agreed_fee", "vehicle.registration", "activity.emails_sent");

        Assert.Equal(
            ["QDOS31001 Inspection", "QDOS31001 Audit", "QDOS31002 Inspection", "QDOS31002 Audit", "QDOS31003 Inspection", "QDOS31003 Audit", "t.QDOS31004 N/A"],
            result.Rows.Select(row => $"{row.Record.Reference} {Cell(result, row, "report")}"));

        // Each report's row reads its own sends and its own work's case data.
        var apartInspectionRow = RowOf(result, apart, CaseListSide.Inspection);
        var apartAuditRow = RowOf(result, apart, CaseListSide.Audit);
        Assert.Equal(new DateOnly(2031, 6, 12), Cell(result, apartInspectionRow, "sent"));
        Assert.Equal("first.sender", Cell(result, apartInspectionRow, "sent_by"));
        Assert.Equal(300.00m, Cell(result, apartInspectionRow, "agreed_fee"));
        Assert.Equal(0, Cell(result, apartInspectionRow, "resends"));
        Assert.Equal("AB12 CDE", Cell(result, apartInspectionRow, "vehicle.registration"));
        Assert.Equal(CaseListPolicy.NotApplicable, Cell(result, apartInspectionRow, "case.audit_reference"));
        Assert.Equal(new DateOnly(2031, 6, 14), Cell(result, apartAuditRow, "sent"));
        Assert.Equal("second.sender", Cell(result, apartAuditRow, "sent_by"));
        Assert.Equal(95.00m, Cell(result, apartAuditRow, "agreed_fee"));
        Assert.Equal(0, Cell(result, apartAuditRow, "resends"));
        Assert.Equal("CD34 EFG", Cell(result, apartAuditRow, "vehicle.registration"));
        Assert.Equal("a.QDOS31001", Cell(result, apartAuditRow, "case.audit_reference"));
        // The Case-wide counts are on the Case's own row, so a total counts the Case once.
        Assert.Equal(2, Cell(result, apartInspectionRow, "activity.emails_sent"));
        Assert.Equal(CaseListPolicy.NotApplicable, Cell(result, apartAuditRow, "activity.emails_sent"));

        // One e-mail carrying both reports is the first send of each, and one e-mail sent.
        var togetherInspectionRow = RowOf(result, together, CaseListSide.Inspection);
        var togetherAuditRow = RowOf(result, together, CaseListSide.Audit);
        Assert.Equal(new DateOnly(2031, 6, 15), Cell(result, togetherInspectionRow, "sent"));
        Assert.Equal(new DateOnly(2031, 6, 15), Cell(result, togetherAuditRow, "sent"));
        Assert.Equal("first.sender", Cell(result, togetherInspectionRow, "sent_by"));
        Assert.Equal("first.sender", Cell(result, togetherAuditRow, "sent_by"));
        Assert.Equal(310.00m, Cell(result, togetherInspectionRow, "agreed_fee"));
        Assert.Equal(105.00m, Cell(result, togetherAuditRow, "agreed_fee"));
        Assert.Equal(0, Cell(result, togetherInspectionRow, "resends"));
        Assert.Equal(0, Cell(result, togetherAuditRow, "resends"));
        Assert.Equal(1, Cell(result, togetherInspectionRow, "activity.emails_sent"));

        // Before Create audit the Audit's row is listed, blank.
        var awaitingAuditRow = RowOf(result, awaiting, CaseListSide.Audit);
        Assert.Null(awaitingAuditRow.Work);
        Assert.Null(Cell(result, awaitingAuditRow, "sent"));
        Assert.Null(Cell(result, awaitingAuditRow, "resends"));
        Assert.Null(Cell(result, awaitingAuditRow, "sent_by"));
        Assert.Null(Cell(result, awaitingAuditRow, "agreed_fee"));
        Assert.Equal(0, Cell(result, RowOf(result, awaiting, CaseListSide.Inspection), "activity.emails_sent"));
        Assert.Equal(CaseListPolicy.NotApplicable, Cell(result, awaitingAuditRow, "activity.emails_sent"));

        // A Triage Case's one row has no report.
        var triageRow = RowOf(result, triage, CaseListSide.Case);
        foreach (var key in new[] { "case.audit_reference", "sent", "resends", "sent_by", "agreed_fee" })
        {
            Assert.Equal(CaseListPolicy.NotApplicable, Cell(result, triageRow, key));
        }
        Assert.Equal(0, Cell(result, triageRow, "activity.emails_sent"));
    }

    /// <summary>
    /// R673: a report counts once, at its first send, and every later send of
    /// it is a re-send. Its Agreed fee is the one frozen in the report that
    /// first send carried, neither the first nor the latest generated; a
    /// report confirmed but never sent has none.
    /// </summary>
    [Fact]
    public async Task AReSendIsCountedOnItsOwnAndTheFeeIsTheOneTheFirstSendCarried()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        Guid resent, unsent;
        await using (var context = await database.CreateContextAsync())
        {
            var principal = await SeededPrincipals.QdosAsync(context);
            var first = await AddStaffAsync(context, "first.sender");
            var second = await AddStaffAsync(context, "second.sender");
            resent = AddCase(context, principal, 1, "inspection", Received);
            unsent = AddCase(context, principal, 2, "audit", Received);

            // Generated, then regenerated with another fee before the first
            // send, which carried the second; regenerated again and sent again.
            Report(context, resent, resent, Received.AddDays(1), "{\"agreedFee\":180.00,\"costs\":{\"totals\":{\"printed\":{\"gross\":900.10}}}}");
            var carried = Report(context, resent, resent, Received.AddDays(2), "{\"agreedFee\":200.00,\"costs\":{\"totals\":{\"printed\":{\"gross\":1000.00}}}}");
            var latest = Report(context, resent, resent, Received.AddDays(4), "{\"agreedFee\":250.00,\"costs\":{\"totals\":{\"printed\":{\"gross\":1234.56}}}}");
            // Confirmed, with a fee, and never sent.
            Report(context, unsent, unsent, Received.AddDays(1), "{\"agreedFee\":75.00,\"costs\":{\"totals\":{\"printed\":{\"gross\":420.00}}}}");
            context.Set<StaffMailSendOperationEntity>().AddRange(
                Send(carried, first, Received.AddDays(3)),
                Send(latest, second, Received.AddDays(5)));
            await context.SaveChangesAsync();
        }

        var result = await CaseListAsync(database, includeTriage: false, "sent", "resends", "sent_by", "agreed_fee", "repair_cost");

        // The re-send is counted on its own; the report keeps its first send's
        // date, sender and fee, and its repair cost is the latest report's.
        var resentRow = RowOf(result, resent, CaseListSide.Inspection);
        Assert.Equal(1, Cell(result, resentRow, "resends"));
        Assert.Equal(new DateOnly(2031, 6, 13), Cell(result, resentRow, "sent"));
        Assert.Equal("first.sender", Cell(result, resentRow, "sent_by"));
        Assert.Equal(200.00m, Cell(result, resentRow, "agreed_fee"));
        Assert.Equal(1234.56m, Cell(result, resentRow, "repair_cost"));

        // Unsent: no first send, no re-sends and no fee, though its report has one.
        var unsentRow = RowOf(result, unsent, CaseListSide.Audit);
        Assert.Null(Cell(result, unsentRow, "sent"));
        Assert.Null(Cell(result, unsentRow, "resends"));
        Assert.Null(Cell(result, unsentRow, "sent_by"));
        Assert.Null(Cell(result, unsentRow, "agreed_fee"));
        Assert.Equal(420.00m, Cell(result, unsentRow, "repair_cost"));
    }

    /// <summary>
    /// A3-12 (R326): the Activity figures are one set query. The list sends the
    /// Case rows, the Audit works, the one Activity statement and the query
    /// receipts: four, where each figure used to be its own read (nine in this
    /// scenario: rows, Audit works, six figure reads, receipts; the association
    /// reads exit early because no query receipt exists). The statement count
    /// is the same for 2 Cases and for 6.
    /// </summary>
    [Fact]
    public async Task TheActivityFiguresAreReadInOneStatementWhateverTheCaseCount()
    {
        var counter = new SqlStatementCounter();
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureDatabase: options => options.AddInterceptors(counter));
        await using (var context = await database.CreateContextAsync())
        {
            var principal = await SeededPrincipals.QdosAsync(context);
            AddCase(context, principal, 1, "inspection", Received);
            AddCase(context, principal, 2, "audit", Received);
            await context.SaveChangesAsync();
        }

        var activityOnly = new CaseListQuery(
            null, null, IncludeTriage: false, CaseListFacts.Activity, new HashSet<string>(), new HashSet<string>());

        async Task<(int Cases, int Statements)> MeasureAsync()
        {
            await using var scope = database.CreateAsyncScope();
            var queries = scope.ServiceProvider.GetRequiredService<ICaseListQueries>();
            counter.Reset();
            var records = await queries.GetAsync(activityOnly, CancellationToken.None);
            return (records.Count, counter.Count);
        }

        var few = await MeasureAsync();

        await using (var context = await database.CreateContextAsync())
        {
            var principal = await SeededPrincipals.QdosAsync(context);
            for (var sequence = 3; sequence <= 6; sequence++)
            {
                AddCase(context, principal, sequence, "inspection", Received);
            }

            await context.SaveChangesAsync();
        }

        var many = await MeasureAsync();

        Assert.Equal(2, few.Cases);
        Assert.Equal(6, many.Cases);
        Assert.True(
            few.Statements <= 4,
            $"The Activity list sent {few.Statements} statements; at most 4 are allowed (rows, Audit works, Activity, query receipts).");
        Assert.Equal(few.Statements, many.Statements);
    }

    [Fact]
    public async Task APresetIsKeptVersionByVersionAndARemovedNameIsFreeAgain()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ICaseListPresetStore>();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var id = Guid.NewGuid();
        var createKey = Guid.NewGuid().ToString("N");

        var created = await store.SaveAsync(new(id, "Invoicing", ["case.reference", "agreed_fee"], 0, actor, createKey), CancellationToken.None);
        var replayed = await store.SaveAsync(new(id, "Invoicing", ["case.reference", "agreed_fee"], 0, actor, createKey), CancellationToken.None);

        Assert.Equal(1, created.Version);
        Assert.Equal(created.Version, replayed.Version);
        var duplicate = await Assert.ThrowsAsync<CaseListPresetException>(() => store.SaveAsync(
            new(Guid.NewGuid(), "INVOICING", ["case.reference"], 0, actor, Guid.NewGuid().ToString("N")), CancellationToken.None));
        Assert.Equal(CaseListPresetError.DuplicateName, duplicate.Error);
        var stale = await Assert.ThrowsAsync<CaseListPresetException>(() => store.SaveAsync(
            new(id, "Invoicing", ["case.type"], 5, actor, Guid.NewGuid().ToString("N")), CancellationToken.None));
        Assert.Equal(CaseListPresetError.VersionConflict, stale.Error);

        var updated = await store.SaveAsync(new(id, "Invoicing", ["case.type"], 1, actor, Guid.NewGuid().ToString("N")), CancellationToken.None);
        Assert.Equal(["case.type"], updated.ColumnKeys);
        Assert.Equal(["case.type"], Assert.Single(await store.ListAsync(CancellationToken.None)).ColumnKeys);

        var removed = await store.RemoveAsync(new(id, 2, actor, Guid.NewGuid().ToString("N")), CancellationToken.None);
        Assert.NotNull(removed.RemovedAtUtc);
        Assert.Empty(await store.ListAsync(CancellationToken.None));
        var reused = await store.SaveAsync(new(Guid.NewGuid(), "Invoicing", ["case.reference"], 0, actor, Guid.NewGuid().ToString("N")), CancellationToken.None);
        Assert.Equal("Invoicing", reused.Name);
    }

    /// <summary>DA-03: the Case ids reach each read as one parameter, so more Cases than SQL Server allows parameters in a command still read.</summary>
    [Fact]
    public async Task AnUnfilteredListOfMoreCasesThanSqlServerHasParametersStillReads()
    {
        const int cases = 2150;
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var expected = new List<Guid>(cases);
        await using (var context = await database.CreateContextAsync())
        {
            var principal = await SeededPrincipals.QdosAsync(context);
            for (var sequence = 1; sequence <= cases; sequence++)
            {
                expected.Add(AddCase(context, principal, sequence, "inspection", Received));
            }

            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var all = await scope.ServiceProvider.GetRequiredService<ICaseListQueries>().GetAsync(Everything, CancellationToken.None);

        Assert.Equal(cases, all.Count);
        Assert.Equal(expected.Order(), all.Select(record => record.CaseId).Order());
    }

    /// <summary>The Case list for All time with the chosen columns, as the Management Reports page asks for it.</summary>
    private static async Task<CaseListResult> CaseListAsync(LocalDbTestDatabase database, bool includeTriage, params string[] columns)
    {
        await using var scope = database.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<GetCaseList>().ExecuteAsync(
            new(ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]), null, null, includeTriage, columns),
            CancellationToken.None);
    }

    /// <summary>A Case's one row for a report: its Inspection's, its Audit's, or a Triage Case's row.</summary>
    private static CaseListRow RowOf(CaseListResult result, Guid caseId, CaseListSide report) =>
        Assert.Single(result.Rows, row => row.Record.CaseId == caseId && row.Report == report);

    /// <summary>A row's cell as the CSV and the workbook carry it, in the operator's words.</summary>
    private static object? Cell(CaseListResult result, CaseListRow row, string key) =>
        result.Columns.Single(column => column.Key == key).Read(row, OperatorCaseListLabels.Instance);

    private static Guid AddCase(
        PegasusDbContext context,
        SeededPrincipalTestData principal,
        int sequence,
        string type,
        DateTimeOffset createdAtUtc,
        Guid? engineer = null,
        bool auditReference = false)
    {
        var id = Guid.NewGuid();
        var reference = $"{(type == "triage" ? "t." : type == "audit" ? "a." : string.Empty)}QDOS3100{sequence}";
        context.Cases.Add(new CaseEntity
        {
            Id = id,
            PrincipalId = principal.Id,
            SequenceLineageId = principal.SequenceLineageId,
            Year = 2031,
            Sequence = sequence,
            Reference = reference,
            AuditReference = auditReference ? $"a.{reference}" : null,
            Type = type,
            InitialState = type == "triage" ? null : "Review",
            CustodyState = "Confirmed",
            CreatedAtUtc = createdAtUtc,
            Version = 1,
            ConcurrencyToken = Guid.NewGuid()
        });
        if (type == "triage")
        {
            context.Triage.Add(new TriageEntity
            {
                CaseId = id,
                NormalizedVehicleRegistration = "AB12CDE",
                State = "open",
                CreatedAtUtc = createdAtUtc,
                CreationOperationKey = $"triage:{id:N}",
                Version = 0,
                ConcurrencyToken = Guid.NewGuid()
            });
        }
        else
        {
            context.CaseWorkflows.Add(new CaseWorkflowEntity
            {
                CaseId = id,
                State = "Review",
                AssignedEngineerId = engineer,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            });
        }

        return id;
    }

    /// <summary>An Inspection + Audit Case's Audit work, as Create audit adds it.</summary>
    private static Guid AddAuditWork(PegasusDbContext context, Guid caseId, DateTimeOffset createdAtUtc)
    {
        var id = Guid.NewGuid();
        context.CaseWorks.Add(new CaseWorkEntity { Id = id, CaseId = caseId, Kind = CaseWorkKinds.Audit, CreatedAtUtc = createdAtUtc });
        return id;
    }

    /// <summary>A work's own case data, received at <see cref="Received"/>, with its accepted registration.</summary>
    private static CaseDataSnapshotEntity CaseData(Guid workId, string registration) => new()
    {
        WorkId = workId,
        OriginReceivedAtUtc = Received,
        CompletenessPolicyKey = "test",
        CompletenessPolicyVersion = 1,
        AcceptedAtUtc = Received,
        Fields =
        [
            new CaseDataFieldEntity
            {
                WorkId = workId,
                FieldName = CaseDataFieldNames.VehicleRegistration,
                ValueKind = "fact",
                ValueType = "text",
                Value = registration,
                SourceKind = "intake_evidence",
                SourceIdentity = "test",
                SourceLabel = "test",
                PolicyKey = "test",
                PolicyVersion = 1
            }
        ]
    };

    /// <summary>A staff account with its one role, so the list names the sends it made.</summary>
    private static async Task<Guid> AddStaffAsync(PegasusDbContext context, string name)
    {
        var role = await context.Roles.SingleAsync(item => item.Name == StaffRoleNames.Engineer);
        var id = Guid.NewGuid();
        context.Users.Add(new PegasusIdentityUser
        {
            Id = id,
            UserName = name,
            NormalizedUserName = name.ToUpperInvariant(),
            IsEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        });
        context.UserRoles.Add(new IdentityUserRole<Guid> { UserId = id, RoleId = role.Id });
        return id;
    }

    private static CaseAssessmentFieldEntity Field(Guid workId, string path, string value) => new()
    {
        WorkId = workId,
        FieldPath = path,
        Value = value,
        RecordedByKind = "Staff",
        RecordedBy = Guid.NewGuid().ToString("D"),
        RecordedAtUtc = Received
    };

    /// <summary>A confirmed assessment report: its artifact's hash matches a custody-confirmed version.</summary>
    private static Guid Report(PegasusDbContext context, Guid caseId, Guid workId, DateTimeOffset generatedAtUtc, string snapshotJson)
    {
        var generation = Guid.NewGuid();
        var document = Guid.NewGuid();
        var version = Guid.NewGuid();
        var sha256 = Convert.ToHexStringLower(Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray());
        context.AddRange(
            new CaseReportGenerationEntity
            {
                Id = generation, CaseId = caseId, WorkId = workId, CaseVersion = 1, SnapshotHash = sha256,
                SnapshotJson = snapshotJson, TemplateVersion = "test", RendererVersion = "test", State = "Confirmed",
                GeneratedAtUtc = generatedAtUtc, Version = 1
            },
            new CaseDocumentEntity { Id = document, CaseId = caseId, Ordinal = 100 + generatedAtUtc.Day, SourceOccurrenceIdentity = $"report:{generation:N}" },
            new DocumentVersionEntity
            {
                Id = version, DocumentId = document, Version = 1, FileName = "report.pdf", MediaType = "application/pdf",
                ContentLength = 1, Sha256 = sha256, CustodyStatus = DocumentCustodyStatus.Confirmed,
                CreatedAtUtc = generatedAtUtc, CreatedBy = "test", IsCurrent = true
            },
            new GeneratedCaseArtifactEntity
            {
                Id = Guid.NewGuid(), GenerationId = generation, VersionId = version, Kind = nameof(CaseReportArtifactKind.AssessmentReport),
                Sha256 = sha256, State = "Confirmed", OperationKey = $"artifact:{generation:N}"
            });
        return generation;
    }

    /// <summary>An observed report e-mail; <paramref name="alsoCarries"/> names the other work's report when it carries both.</summary>
    private static StaffMailSendOperationEntity Send(Guid generation, Guid sender, DateTimeOffset sentAtUtc, Guid? alsoCarries = null)
    {
        var id = Guid.NewGuid();
        return new()
        {
            Id = id,
            ActorSubjectId = sender.ToString("D"),
            MailboxId = Guid.NewGuid(),
            MailboxGeneration = 1,
            OperationKey = $"send:{id:N}",
            PayloadHash = new string('2', 64),
            Purpose = StaffMailPurpose.CaseReport,
            ContextId = generation,
            ContextVersion = 1,
            SecondContextId = alsoCarries,
            SecondContextVersion = alsoCarries is null ? null : 1,
            ComposeMode = StaffMailComposeMode.New,
            RecipientsJson = "[]",
            Subject = "report",
            Body = "report",
            AttachmentsJson = "[]",
            State = StaffMailState.Sent,
            CorrelationMarker = $"mail:{id:N}",
            CreatedAtUtc = sentAtUtc,
            RequestedAtUtc = sentAtUtc,
            ObservedSentAtUtc = sentAtUtc,
            Version = 1,
            ConcurrencyToken = Guid.NewGuid()
        };
    }

    private static DocumentOccurrenceEntity Occurrence(
        Guid caseId,
        Guid document,
        Guid version,
        int ordinal,
        DocumentSemanticRole role,
        bool inReport) => new()
    {
        Id = Guid.NewGuid(),
        CaseId = caseId,
        DocumentId = document,
        VersionId = version,
        Ordinal = ordinal,
        SemanticRole = role,
        Source = DocumentSource.StaffUpload,
        SourceOccurrenceIdentity = $"occurrence:{ordinal}",
        RecordedAtUtc = Received,
        OperationKey = $"occurrence:{Guid.NewGuid():N}",
        InReport = inReport
    };
}
