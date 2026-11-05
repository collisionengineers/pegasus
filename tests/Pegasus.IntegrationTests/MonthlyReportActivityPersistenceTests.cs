using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// MI-02's periods and Outcomes read from the real stores (R673): each report
/// counts once, in the London month of its first observed send, with the fee
/// and outcome frozen in the report that send carried; every later send of it
/// is a re-send in its own month. The monthly and per-Principal reads agree.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class MonthlyReportActivityPersistenceTests
{
    private static readonly DateTimeOffset From = new(2031, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2031, 9, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A report counts in the London month of its first send, with the fee
    /// frozen in the report that send carried. A Generate that was never
    /// sent, a regeneration and the Case's live fee change nothing; the later
    /// send is a re-send in its own month, and stays one in a period that
    /// starts after the first send.
    /// </summary>
    [Fact]
    public async Task CountsEachReportInTheLondonMonthOfItsFirstSendWithTheFeeOfTheReportItCarried()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var principalId = Guid.Empty;
        // The first send is UTC June but London July; the re-send is UTC July
        // but London August.
        var firstSentAt = new DateTimeOffset(2031, 6, 30, 23, 30, 0, TimeSpan.Zero);
        var resentAt = new DateTimeOffset(2031, 7, 31, 23, 30, 0, TimeSpan.Zero);

        await using (var context = await database.CreateContextAsync())
        {
            var principal = await SeededPrincipals.QdosAsync(context);
            principalId = principal.Id;
            var caseId = Guid.NewGuid();
            var unsentGenerationId = Guid.NewGuid();
            var sentGenerationId = Guid.NewGuid();
            var resentGenerationId = Guid.NewGuid();

            context.Add(Case(principal, caseId, "inspection"));
            context.CaseAssessmentFields.Add(new CaseAssessmentFieldEntity
            {
                WorkId = caseId,
                FieldPath = AssessmentVocabulary.AgreedFee,
                Value = "999.99",
                RecordedByKind = "Staff",
                RecordedBy = Guid.NewGuid().ToString("D"),
                RecordedAtUtc = resentAt
            });
            context.Set<CaseReportGenerationEntity>().AddRange(
                // Generated and never sent: no report sent and no fee.
                Generation(unsentGenerationId, caseId, new DateTimeOffset(2031, 6, 20, 12, 0, 0, TimeSpan.Zero), "{\"agreedFee\":80.00}"),
                Generation(sentGenerationId, caseId, firstSentAt.AddHours(-1), "{\"agreedFee\":120.00}"),
                // Regenerated after the first send and sent again: a re-send,
                // whose report's fee is not the report's.
                Generation(resentGenerationId, caseId, resentAt.AddHours(-1), "{\"agreedFee\":300.00}"));
            context.Set<StaffMailSendOperationEntity>().AddRange(
                SentOperation(sentGenerationId, firstSentAt),
                SentOperation(resentGenerationId, resentAt));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IMonthlyReportActivityQueries>();
        var rows = await queries.GetAsync(From, To, CancellationToken.None);
        var principalQueries = scope.ServiceProvider.GetRequiredService<IV1ActivityReportQueries>();
        var principalRow = Assert.Single(await principalQueries.GetAsync(From, To, CancellationToken.None));

        Assert.Equal(
        [
            new MonthlyReportActivity(principalId, "QDOS", 2031, 8, 0, 1, 0m),
            new MonthlyReportActivity(principalId, "QDOS", 2031, 7, 1, 0, 120m)
        ], rows);
        Assert.Equal(principalRow.Sent, rows.Sum(row => row.Sent));
        Assert.Equal(principalRow.Resends, rows.Sum(row => row.Resends));
        Assert.Equal(principalRow.AgreedFeeTotal, rows.Sum(row => row.AgreedFeeTotal));

        // From 1 July UTC the first send is before the period: the re-send is
        // still a re-send, with no report sent and no fee.
        var laterOnlyRows = await queries.GetAsync(From.AddMonths(1), To, CancellationToken.None);
        var laterOnlyPrincipalRow = Assert.Single(
            await principalQueries.GetAsync(From.AddMonths(1), To, CancellationToken.None));

        Assert.Equal([new MonthlyReportActivity(principalId, "QDOS", 2031, 8, 0, 1, 0m)], laterOnlyRows);
        Assert.Equal(
            (0, 1, 0m),
            (laterOnlyPrincipalRow.Sent, laterOnlyPrincipalRow.Resends, laterOnlyPrincipalRow.AgreedFeeTotal));
    }

    /// <summary>
    /// MI-02 per report: an Inspection + Audit Case's two reports each count
    /// in the month of the e-mail that first sent it, with the fee frozen in
    /// it, and the Audit report is the Audit share of each total. The e-mail
    /// that first sent the Audit report, after Create audit, carried the
    /// Inspection report again: a re-send in that month. The monthly and
    /// per-Principal reads agree.
    /// </summary>
    [Fact]
    public async Task EachReportCountsInTheMonthOfItsFirstSendWithItsOwnFeeAndTheAuditShare()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var principalId = Guid.Empty;
        var inspectionSentAt = new DateTimeOffset(2031, 6, 6, 12, 0, 0, TimeSpan.Zero);
        var auditSentAt = new DateTimeOffset(2031, 7, 2, 12, 0, 0, TimeSpan.Zero);

        await using (var context = await database.CreateContextAsync())
        {
            var principal = await SeededPrincipals.QdosAsync(context);
            principalId = principal.Id;
            var caseId = Guid.NewGuid();
            var auditWorkId = Guid.NewGuid();
            var inspectionGenerationId = Guid.NewGuid();
            var auditGenerationId = Guid.NewGuid();

            context.Add(Case(principal, caseId, "inspection_and_audit"));
            context.CaseWorks.Add(AuditWork(auditWorkId, caseId, new DateTimeOffset(2031, 6, 25, 12, 0, 0, TimeSpan.Zero)));
            context.Set<CaseReportGenerationEntity>().AddRange(
                Generation(inspectionGenerationId, caseId, inspectionSentAt.AddDays(-1), "{\"agreedFee\":100.00}"),
                Generation(auditGenerationId, caseId, auditSentAt.AddDays(-1), "{\"agreedFee\":40.00}", auditWorkId));
            context.Set<StaffMailSendOperationEntity>().AddRange(
                SentOperation(inspectionGenerationId, inspectionSentAt),
                SentOperation(auditGenerationId, auditSentAt, inspectionGenerationId));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IMonthlyReportActivityQueries>()
            .GetAsync(From, To, CancellationToken.None);
        var principalRow = Assert.Single(await scope.ServiceProvider.GetRequiredService<IV1ActivityReportQueries>()
            .GetAsync(From, To, CancellationToken.None));

        Assert.Equal(
        [
            new MonthlyReportActivity(principalId, "QDOS", 2031, 7, 1, 1, 40m, 1, 0, 40m),
            new MonthlyReportActivity(principalId, "QDOS", 2031, 6, 1, 0, 100m)
        ], rows);
        Assert.Equal((0, 1, 0m), (rows[0].InspectionSent, rows[0].InspectionResends, rows[0].InspectionAgreedFeeTotal));
        Assert.Equal((1, 0, 100m), (rows[1].InspectionSent, rows[1].InspectionResends, rows[1].InspectionAgreedFeeTotal));

        Assert.Equal((2, 1, 1), (principalRow.Sent, principalRow.AuditSent, principalRow.InspectionSent));
        Assert.Equal((1, 0, 1), (principalRow.Resends, principalRow.AuditResends, principalRow.InspectionResends));
        Assert.Equal(
            (140m, 40m, 100m),
            (principalRow.AgreedFeeTotal, principalRow.AuditAgreedFeeTotal, principalRow.InspectionAgreedFeeTotal));
    }

    /// <summary>
    /// R673: one e-mail that carried both of an Inspection + Audit Case's
    /// reports is a send of each: two reports sent, one of them the Audit's,
    /// and no re-send. The e-mail that later carried both again is two
    /// re-sends, one of them the Audit's, and adds no fee.
    /// </summary>
    [Fact]
    public async Task OneEmailCarryingBothReportsIsTwoReportsSentAndSendingBothAgainIsTwoResends()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var principalId = Guid.Empty;
        var sentAt = new DateTimeOffset(2031, 6, 21, 12, 0, 0, TimeSpan.Zero);
        var resentAt = new DateTimeOffset(2031, 7, 5, 12, 0, 0, TimeSpan.Zero);

        await using (var context = await database.CreateContextAsync())
        {
            var principal = await SeededPrincipals.QdosAsync(context);
            principalId = principal.Id;
            var caseId = Guid.NewGuid();
            var auditWorkId = Guid.NewGuid();
            var inspectionGenerationId = Guid.NewGuid();
            var auditGenerationId = Guid.NewGuid();

            context.Add(Case(principal, caseId, "inspection_and_audit"));
            context.CaseWorks.Add(AuditWork(auditWorkId, caseId, sentAt.AddDays(-10)));
            context.Set<CaseReportGenerationEntity>().AddRange(
                Generation(inspectionGenerationId, caseId, sentAt.AddDays(-12), "{\"agreedFee\":100.00}"),
                Generation(auditGenerationId, caseId, sentAt.AddDays(-1), "{\"agreedFee\":40.00}", auditWorkId));
            context.Set<StaffMailSendOperationEntity>().AddRange(
                SentOperation(auditGenerationId, sentAt, inspectionGenerationId),
                SentOperation(auditGenerationId, resentAt, inspectionGenerationId));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var principalQueries = scope.ServiceProvider.GetRequiredService<IV1ActivityReportQueries>();
        var rows = await scope.ServiceProvider.GetRequiredService<IMonthlyReportActivityQueries>()
            .GetAsync(From, To, CancellationToken.None);

        Assert.Equal(
        [
            new MonthlyReportActivity(principalId, "QDOS", 2031, 7, 0, 2, 0m, 0, 1, 0m),
            new MonthlyReportActivity(principalId, "QDOS", 2031, 6, 2, 0, 140m, 1, 0, 40m)
        ], rows);

        var june = Assert.Single(await principalQueries.GetAsync(From, From.AddMonths(1), CancellationToken.None));
        Assert.Equal((2, 1, 1), (june.Sent, june.AuditSent, june.InspectionSent));
        Assert.Equal((0, 0), (june.Resends, june.AuditResends));
        Assert.Equal((140m, 40m), (june.AgreedFeeTotal, june.AuditAgreedFeeTotal));

        var whole = Assert.Single(await principalQueries.GetAsync(From, To, CancellationToken.None));
        Assert.Equal((2, 1), (whole.Sent, whole.AuditSent));
        Assert.Equal((2, 1, 1), (whole.Resends, whole.AuditResends, whole.InspectionResends));
        Assert.Equal((140m, 40m), (whole.AgreedFeeTotal, whole.AuditAgreedFeeTotal));
    }

    /// <summary>
    /// Outcomes (item O; R673): each report first sent in the period counts
    /// under the outcome frozen in the report that send carried, never an
    /// unsent Generate's or a later re-send's; the Audit report differs from
    /// the Inspection's recorded outcome it reviews. The outcomes are the
    /// reports sent.
    /// </summary>
    [Fact]
    public async Task OutcomesCountEachReportByTheOutcomeFrozenInTheReportItsFirstSendCarriedAndTheAuditByAgreement()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var principalId = Guid.Empty;
        var sentAt = new DateTimeOffset(2031, 6, 21, 12, 0, 0, TimeSpan.Zero);

        await using (var context = await database.CreateContextAsync())
        {
            var principal = await SeededPrincipals.QdosAsync(context);
            principalId = principal.Id;
            var caseId = Guid.NewGuid();
            var auditWorkId = Guid.NewGuid();
            var unsentInspectionGenerationId = Guid.NewGuid();
            var inspectionGenerationId = Guid.NewGuid();
            var auditGenerationId = Guid.NewGuid();
            var resentInspectionGenerationId = Guid.NewGuid();

            context.Add(Case(principal, caseId, "inspection_and_audit"));
            context.CaseWorks.Add(AuditWork(auditWorkId, caseId, sentAt.AddDays(-5)));
            // The Inspection's recorded outcome is the report the Audit reviews.
            context.CaseAssessmentFields.Add(new CaseAssessmentFieldEntity
            {
                WorkId = caseId,
                FieldPath = AssessmentVocabulary.Outcome,
                Value = "repairable",
                RecordedByKind = "Staff",
                RecordedBy = Guid.NewGuid().ToString("D"),
                RecordedAtUtc = sentAt.AddDays(-15)
            });
            context.Set<CaseReportGenerationEntity>().AddRange(
                // Generated as cash in lieu, then regenerated as repairable
                // before it was sent.
                Generation(unsentInspectionGenerationId, caseId, sentAt.AddDays(-16), "{\"agreedFee\":100.00,\"report\":{\"outcome\":2}}"),
                Generation(inspectionGenerationId, caseId, sentAt.AddDays(-15), "{\"agreedFee\":100.00,\"report\":{\"outcome\":1}}"),
                Generation(auditGenerationId, caseId, sentAt.AddDays(-1), "{\"agreedFee\":40.00,\"report\":{\"outcome\":0}}", auditWorkId),
                // Regenerated as a contract repair after it was sent, and sent again.
                Generation(resentInspectionGenerationId, caseId, sentAt.AddDays(2), "{\"agreedFee\":100.00,\"report\":{\"outcome\":3}}"));
            context.Set<StaffMailSendOperationEntity>().AddRange(
                SentOperation(auditGenerationId, sentAt, inspectionGenerationId),
                SentOperation(resentInspectionGenerationId, sentAt.AddDays(3)));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var facts = await scope.ServiceProvider.GetRequiredService<IReportOutcomeQueries>()
            .GetAsync(From, To, CancellationToken.None);
        var month = Assert.Single(await scope.ServiceProvider.GetRequiredService<IMonthlyReportActivityQueries>()
            .GetAsync(From, To, CancellationToken.None));

        Assert.Equal(
            [
                new ReportOutcomeFact(principalId, "QDOS", AssessmentReportOutcome.Repairable, IsAudit: false, null),
                new ReportOutcomeFact(principalId, "QDOS", AssessmentReportOutcome.TotalLoss, IsAudit: true, "repairable")
            ],
            facts.OrderBy(fact => fact.IsAudit));
        Assert.Equal(new MonthlyReportActivity(principalId, "QDOS", 2031, 6, 2, 1, 140m, 1, 0, 40m), month);
    }

    /// <summary>#1158: the outcome is read from the snapshot in SQL; a report first sent frozen without one is unreadable, never a zero.</summary>
    [Fact]
    public async Task AReportFirstSentFrozenWithoutAnOutcomeMakesOutcomesUnreadable()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var sentAt = new DateTimeOffset(2031, 6, 5, 12, 0, 0, TimeSpan.Zero);

        await using (var context = await database.CreateContextAsync())
        {
            var principal = await SeededPrincipals.QdosAsync(context);
            var caseId = Guid.NewGuid();
            var generationId = Guid.NewGuid();

            context.Add(Case(principal, caseId, "inspection"));
            context.Set<CaseReportGenerationEntity>().Add(
                Generation(generationId, caseId, sentAt.AddHours(-1), "{\"agreedFee\":100.00,\"report\":{}}"));
            context.Set<StaffMailSendOperationEntity>().Add(SentOperation(generationId, sentAt));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        await Assert.ThrowsAsync<InvalidDataException>(() => scope.ServiceProvider.GetRequiredService<IReportOutcomeQueries>()
            .GetAsync(From, To, CancellationToken.None));
    }

    /// <summary>R46: the agreed fee is read from the snapshot in SQL; a report first sent without a valid one stops both activity reads, never a zero.</summary>
    [Theory]
    [InlineData("missing", "{\"report\":{}}", "has no valid agreed fee")]
    [InlineData("text", "{\"agreedFee\":\"abc\"}", "has no valid agreed fee")]
    [InlineData("not-json", "not json", "is unreadable")]
    public async Task AReportFirstSentWithoutAValidAgreedFeeMakesThePrincipalReportUnreadable(string row, string snapshotJson, string message)
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var sentAt = new DateTimeOffset(2031, 6, 5, 12, 0, 0, TimeSpan.Zero);
        var generationId = Guid.NewGuid();

        await using (var context = await database.CreateContextAsync())
        {
            var principal = await SeededPrincipals.QdosAsync(context);
            var caseId = Guid.NewGuid();

            context.Add(Case(principal, caseId, "inspection"));
            context.Set<CaseReportGenerationEntity>().Add(
                Generation(generationId, caseId, sentAt.AddHours(-1), snapshotJson));
            context.Set<StaffMailSendOperationEntity>().Add(SentOperation(generationId, sentAt));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var principalReport = await Assert.ThrowsAsync<InvalidDataException>(() => scope.ServiceProvider
            .GetRequiredService<IV1ActivityReportQueries>().GetAsync(From, To, CancellationToken.None));
        var monthlyReport = await Assert.ThrowsAsync<InvalidDataException>(() => scope.ServiceProvider
            .GetRequiredService<IMonthlyReportActivityQueries>().GetAsync(From, To, CancellationToken.None));

        Assert.Contains(message, principalReport.Message);
        Assert.Contains(message, monthlyReport.Message);
        if (row != "not-json")
        {
            Assert.Contains(generationId.ToString(), principalReport.Message);
            Assert.Contains(generationId.ToString(), monthlyReport.Message);
        }
    }

    /// <summary>
    /// R375: only the reports first sent in the period have their snapshots
    /// read, so a regenerated report holding a snapshot that is not JSON, sent
    /// again later, stops neither activity read.
    /// </summary>
    [Fact]
    public async Task AResentReportWithAnUnreadableSnapshotDoesNotStopEitherReadOfTheFirstSendFee()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var firstSentAt = new DateTimeOffset(2031, 6, 5, 12, 0, 0, TimeSpan.Zero);
        var resentAt = new DateTimeOffset(2031, 7, 6, 12, 0, 0, TimeSpan.Zero);

        await using (var context = await database.CreateContextAsync())
        {
            var principal = await SeededPrincipals.QdosAsync(context);
            var caseId = Guid.NewGuid();
            var generationIds = new[] { Guid.NewGuid(), Guid.NewGuid() };

            context.Add(Case(principal, caseId, "inspection"));
            context.Set<CaseReportGenerationEntity>().AddRange(
                Generation(generationIds[0], caseId, firstSentAt.AddHours(-1), "{\"agreedFee\":100.00}"),
                Generation(generationIds[1], caseId, resentAt.AddDays(-1), "not json"));
            context.Set<StaffMailSendOperationEntity>().AddRange(
                SentOperation(generationIds[0], firstSentAt),
                SentOperation(generationIds[1], resentAt));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var monthlyQueries = scope.ServiceProvider.GetRequiredService<IMonthlyReportActivityQueries>();
        var principalQueries = scope.ServiceProvider.GetRequiredService<IV1ActivityReportQueries>();
        var monthly = await monthlyQueries.GetAsync(From, To, CancellationToken.None);
        var principalRow = Assert.Single(await principalQueries.GetAsync(From, To, CancellationToken.None));

        Assert.Equal(100m, monthly.Sum(row => row.AgreedFeeTotal));
        Assert.Equal(100m, principalRow.AgreedFeeTotal);

        // The period ends exclusive at the first send's instant, so the fee
        // belongs to the half that starts there and not to the half that ends there.
        var endingAtTheFirstInstantMonthly = await monthlyQueries.GetAsync(From, firstSentAt, CancellationToken.None);
        var endingAtTheFirstInstantPrincipals = await principalQueries.GetAsync(From, firstSentAt, CancellationToken.None);

        Assert.Equal(0m, endingAtTheFirstInstantMonthly.Sum(row => row.AgreedFeeTotal));
        Assert.Equal(0m, endingAtTheFirstInstantPrincipals.Sum(row => row.AgreedFeeTotal));

        // The period starts inclusive at the first send's instant; a period
        // after it holds only the re-send, with no fee and its snapshot unread.
        var fromTheFirstInstant = await monthlyQueries.GetAsync(firstSentAt, To, CancellationToken.None);
        var afterTheFirstInstant = await monthlyQueries.GetAsync(firstSentAt.AddTicks(1), To, CancellationToken.None);
        var afterTheFirstInstantPrincipal = Assert.Single(
            await principalQueries.GetAsync(firstSentAt.AddTicks(1), To, CancellationToken.None));

        Assert.Equal(100m, fromTheFirstInstant.Sum(row => row.AgreedFeeTotal));
        Assert.Equal(0m, afterTheFirstInstant.Sum(row => row.AgreedFeeTotal));
        Assert.Equal(
            (0, 1, 0m),
            (afterTheFirstInstantPrincipal.Sent, afterTheFirstInstantPrincipal.Resends, afterTheFirstInstantPrincipal.AgreedFeeTotal));
    }

    private static CaseEntity Case(SeededPrincipalTestData principal, Guid id, string type) => new()
    {
        Id = id,
        PrincipalId = principal.Id,
        SequenceLineageId = principal.SequenceLineageId,
        Year = 2031,
        Sequence = 1,
        Reference = "QDOS31001",
        AuditReference = type == "inspection_and_audit" ? "a.QDOS31001" : null,
        Type = type,
        InitialState = "Review",
        CustodyState = "Confirmed",
        InstructionComplete = true,
        ImagesComplete = true,
        CreatedAtUtc = From,
        Version = 1,
        ConcurrencyToken = Guid.NewGuid()
    };

    private static CaseWorkEntity AuditWork(Guid id, Guid caseId, DateTimeOffset createdAtUtc) => new()
    {
        Id = id,
        CaseId = caseId,
        Kind = CaseWorkKinds.Audit,
        CreatedAtUtc = createdAtUtc
    };

    /// <summary>
    /// A report e-mail seen in Sent Items, carrying the report generation
    /// <paramref name="generationId"/> and, when it carried both works'
    /// reports, <paramref name="secondGenerationId"/>.
    /// </summary>
    private static StaffMailSendOperationEntity SentOperation(
        Guid generationId,
        DateTimeOffset sentAt,
        Guid? secondGenerationId = null) => new()
    {
        Id = Guid.NewGuid(),
        ActorSubjectId = Guid.NewGuid().ToString("D"),
        MailboxId = Guid.NewGuid(),
        MailboxGeneration = 1,
        OperationKey = $"send:{Guid.NewGuid():N}",
        PayloadHash = new string('2', 64),
        Purpose = StaffMailPurpose.CaseReport,
        ContextId = generationId,
        ContextVersion = 1,
        SecondContextId = secondGenerationId,
        SecondContextVersion = secondGenerationId is null ? null : 1,
        ComposeMode = StaffMailComposeMode.New,
        RecipientsJson = "[]",
        Subject = "report",
        Body = "report",
        AttachmentsJson = "[]",
        State = StaffMailState.Sent,
        CorrelationMarker = "test",
        CreatedAtUtc = sentAt,
        RequestedAtUtc = sentAt,
        ObservedSentAtUtc = sentAt,
        Version = 1,
        ConcurrencyToken = Guid.NewGuid()
    };

    private static CaseReportGenerationEntity Generation(
        Guid id,
        Guid caseId,
        DateTimeOffset generatedAtUtc,
        string snapshotJson,
        Guid? workId = null) => new()
        {
            Id = id,
            CaseId = caseId,
            WorkId = workId ?? caseId,
            CaseVersion = 1,
            SnapshotHash = $"{id:N}{id:N}",
            SnapshotJson = snapshotJson,
            TemplateVersion = "test",
            RendererVersion = "test",
            State = nameof(CaseReportGenerationState.Confirmed),
            GeneratedAtUtc = generatedAtUtc,
            Version = 1
        };
}
