using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class MonthlyReportActivityPersistenceTests
{
    private static readonly DateTimeOffset From = new(2031, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2031, 9, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UsesGenerationMonthAndTheFirstReportSnapshotFee()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var principalId = Guid.Empty;
        var firstReportGeneratedAt = new DateTimeOffset(2031, 6, 30, 22, 30, 0, TimeSpan.Zero);
        var firstReportVersionCreatedAt = firstReportGeneratedAt.AddHours(2);
        // The fee note is UTC June but London July; the later report is UTC
        // July but London August.
        var feeNoteVersionCreatedAt = new DateTimeOffset(2031, 6, 30, 23, 30, 0, TimeSpan.Zero);
        var laterReportGeneratedAt = new DateTimeOffset(2031, 7, 31, 22, 30, 0, TimeSpan.Zero);
        var laterReportVersionCreatedAt = new DateTimeOffset(2031, 7, 31, 23, 30, 0, TimeSpan.Zero);

        await using (var context = await database.CreateContextAsync())
        {
            var principal = await SeededPrincipals.QdosAsync(context);
            principalId = principal.Id;
            var caseId = Guid.NewGuid();
            var firstGenerationId = Guid.NewGuid();
            var laterGenerationId = Guid.NewGuid();
            var firstReportVersionId = Guid.NewGuid();
            var feeNoteVersionId = Guid.NewGuid();
            var laterReportVersionId = Guid.NewGuid();
            var caseDocumentIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            var firstReportHash = new string('a', 64);
            var feeNoteHash = new string('b', 64);
            var laterReportHash = new string('c', 64);

            context.Add(new CaseEntity
            {
                Id = caseId,
                PrincipalId = principal.Id,
                SequenceLineageId = principal.SequenceLineageId,
                Year = 2031,
                Sequence = 1,
                Reference = "QDOS31001",
                Type = "inspection",
                InitialState = "Review",
                CustodyState = "Confirmed",
                InstructionComplete = true,
                ImagesComplete = true,
                CreatedAtUtc = From,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            });
            context.CaseAssessmentFields.Add(new CaseAssessmentFieldEntity
            {
                WorkId = caseId,
                FieldPath = AssessmentVocabulary.AgreedFee,
                Value = "999.99",
                RecordedByKind = "Staff",
                RecordedBy = Guid.NewGuid().ToString("D"),
                RecordedAtUtc = laterReportGeneratedAt
            });
            context.Set<CaseReportGenerationEntity>().AddRange(
                Generation(firstGenerationId, caseId, firstReportGeneratedAt, "{\"agreedFee\":120.00}", new string('d', 64)),
                // The later generation's snapshot and generation time must not
                // move the Case's fee out of the first report's month.
                Generation(laterGenerationId, caseId, laterReportGeneratedAt, "{\"agreedFee\":999.99}", new string('e', 64)));
            context.Set<CaseDocumentEntity>().AddRange(
                new CaseDocumentEntity
                {
                    Id = caseDocumentIds[0],
                    CaseId = caseId,
                    Ordinal = 1,
                    SourceOccurrenceIdentity = "monthly-report:first"
                },
                new CaseDocumentEntity
                {
                    Id = caseDocumentIds[1],
                    CaseId = caseId,
                    Ordinal = 2,
                    SourceOccurrenceIdentity = "monthly-report:fee-note"
                },
                new CaseDocumentEntity
                {
                    Id = caseDocumentIds[2],
                    CaseId = caseId,
                    Ordinal = 3,
                    SourceOccurrenceIdentity = "monthly-report:later"
                });
            context.Set<DocumentVersionEntity>().AddRange(
                Version(firstReportVersionId, caseDocumentIds[0], firstReportVersionCreatedAt, firstReportHash, "first-report.pdf"),
                Version(feeNoteVersionId, caseDocumentIds[1], feeNoteVersionCreatedAt, feeNoteHash, "fee-note.pdf"),
                Version(laterReportVersionId, caseDocumentIds[2], laterReportVersionCreatedAt, laterReportHash, "later-report.pdf"));
            context.Set<GeneratedCaseArtifactEntity>().AddRange(
                Artifact(Guid.NewGuid(), firstGenerationId, firstReportVersionId, firstReportHash, nameof(CaseReportArtifactKind.AssessmentReport)),
                Artifact(Guid.NewGuid(), firstGenerationId, feeNoteVersionId, feeNoteHash, nameof(CaseReportArtifactKind.FeeNote)),
                Artifact(Guid.NewGuid(), laterGenerationId, laterReportVersionId, laterReportHash, nameof(CaseReportArtifactKind.AssessmentReport)));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IMonthlyReportActivityQueries>();
        var rows = await queries.GetAsync(From, To, CancellationToken.None);
        var principalQueries = scope.ServiceProvider.GetRequiredService<IV1ActivityReportQueries>();
        var principalRow = Assert.Single(await principalQueries.GetAsync(From, To, CancellationToken.None));

        Assert.Equal(
        [
            new MonthlyReportActivity(principalId, "QDOS", 2031, 7, 1, 0, 0, 0m),
            new MonthlyReportActivity(principalId, "QDOS", 2031, 6, 1, 1, 0, 120m)
        ], rows);
        Assert.Equal(principalRow.GeneratedArtifacts, rows.Sum(row => row.ReportsGenerated + row.FeeNotesGenerated));
        Assert.Equal(principalRow.ReportsProduced, rows.Sum(row => row.ReportsGenerated));
        Assert.Equal(principalRow.AgreedFeeTotal, rows.Sum(row => row.AgreedFeeTotal));

        var laterOnlyRows = await queries.GetAsync(From.AddMonths(1), To, CancellationToken.None);
        var laterOnlyPrincipalRow = Assert.Single(
            await principalQueries.GetAsync(From.AddMonths(1), To, CancellationToken.None));

        Assert.Equal([new MonthlyReportActivity(principalId, "QDOS", 2031, 7, 1, 0, 0, 0m)], laterOnlyRows);
        Assert.Equal(0m, laterOnlyPrincipalRow.AgreedFeeTotal);
        Assert.Equal(laterOnlyPrincipalRow.AgreedFeeTotal, laterOnlyRows.Sum(row => row.AgreedFeeTotal));
    }

    /// <summary>
    /// MI-02 per work: an Inspection + Audit Case with its Audit has two
    /// works, each with its own first report and frozen fee, and the Audit
    /// work's report, send and fee are the Audit share of each total. The
    /// monthly and per-Principal reads agree.
    /// </summary>
    [Fact]
    public async Task EachWorkCarriesItsOwnFirstReportFeeAndTheAuditShare()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var principalId = Guid.Empty;
        var inspectionGeneratedAt = new DateTimeOffset(2031, 6, 5, 12, 0, 0, TimeSpan.Zero);
        var auditGeneratedAt = new DateTimeOffset(2031, 6, 20, 12, 0, 0, TimeSpan.Zero);

        await using (var context = await database.CreateContextAsync())
        {
            var principal = await SeededPrincipals.QdosAsync(context);
            principalId = principal.Id;
            var caseId = Guid.NewGuid();
            var auditWorkId = Guid.NewGuid();
            var inspectionGenerationId = Guid.NewGuid();
            var auditGenerationId = Guid.NewGuid();
            var inspectionVersionId = Guid.NewGuid();
            var auditVersionId = Guid.NewGuid();
            var documentIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var inspectionHash = new string('a', 64);
            var auditHash = new string('b', 64);

            context.Add(new CaseEntity
            {
                Id = caseId,
                PrincipalId = principal.Id,
                SequenceLineageId = principal.SequenceLineageId,
                Year = 2031,
                Sequence = 1,
                Reference = "QDOS31001",
                AuditReference = "a.QDOS31001",
                Type = "inspection_and_audit",
                InitialState = "Review",
                CustodyState = "Confirmed",
                InstructionComplete = true,
                ImagesComplete = true,
                CreatedAtUtc = From,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            });
            context.CaseWorks.Add(new CaseWorkEntity
            {
                Id = auditWorkId,
                CaseId = caseId,
                Kind = CaseWorkKinds.Audit,
                CreatedAtUtc = inspectionGeneratedAt.AddDays(5)
            });
            context.Set<CaseReportGenerationEntity>().AddRange(
                Generation(inspectionGenerationId, caseId, inspectionGeneratedAt, "{\"agreedFee\":100.00}", new string('d', 64)),
                Generation(auditGenerationId, caseId, auditGeneratedAt, "{\"agreedFee\":40.00}", new string('e', 64), auditWorkId));
            context.Set<CaseDocumentEntity>().AddRange(
                new CaseDocumentEntity
                {
                    Id = documentIds[0],
                    CaseId = caseId,
                    Ordinal = 1,
                    SourceOccurrenceIdentity = "monthly-report:inspection"
                },
                new CaseDocumentEntity
                {
                    Id = documentIds[1],
                    CaseId = caseId,
                    Ordinal = 2,
                    SourceOccurrenceIdentity = "monthly-report:audit",
                    CustodyFolder = CaseCustodyFolders.Audit
                });
            context.Set<DocumentVersionEntity>().AddRange(
                Version(inspectionVersionId, documentIds[0], inspectionGeneratedAt, inspectionHash, "QDOS31001_assessment.pdf"),
                Version(auditVersionId, documentIds[1], auditGeneratedAt, auditHash, "A_QDOS31001_assessment.pdf"));
            context.Set<GeneratedCaseArtifactEntity>().AddRange(
                Artifact(Guid.NewGuid(), inspectionGenerationId, inspectionVersionId, inspectionHash, nameof(CaseReportArtifactKind.AssessmentReport)),
                Artifact(Guid.NewGuid(), auditGenerationId, auditVersionId, auditHash, nameof(CaseReportArtifactKind.AssessmentReport)));
            context.Set<StaffMailSendOperationEntity>().AddRange(
                SentOperation(inspectionGenerationId, inspectionGeneratedAt.AddDays(1)),
                SentOperation(auditGenerationId, auditGeneratedAt.AddDays(1)));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IMonthlyReportActivityQueries>()
            .GetAsync(From, To, CancellationToken.None);
        var principalRow = Assert.Single(await scope.ServiceProvider.GetRequiredService<IV1ActivityReportQueries>()
            .GetAsync(From, To, CancellationToken.None));

        var month = Assert.Single(rows);
        Assert.Equal(new MonthlyReportActivity(principalId, "QDOS", 2031, 6, 2, 0, 2, 140m, 1, 1, 40m), month);
        Assert.Equal(1, month.InspectionReportsGenerated);
        Assert.Equal(1, month.InspectionSent);
        Assert.Equal(100m, month.InspectionAgreedFeeTotal);

        Assert.Equal(2, principalRow.ReportsProduced);
        Assert.Equal(1, principalRow.AuditReportsProduced);
        Assert.Equal(1, principalRow.InspectionReportsProduced);
        Assert.Equal(2, principalRow.Sent);
        Assert.Equal(1, principalRow.AuditSent);
        Assert.Equal(140m, principalRow.AgreedFeeTotal);
        Assert.Equal(40m, principalRow.AuditAgreedFeeTotal);
        Assert.Equal(100m, principalRow.InspectionAgreedFeeTotal);
    }

    private static StaffMailSendOperationEntity SentOperation(Guid generationId, DateTimeOffset sentAt) => new()
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
        string snapshotHash,
        Guid? workId = null) => new()
        {
            Id = id,
            CaseId = caseId,
            WorkId = workId ?? caseId,
            CaseVersion = 1,
            SnapshotHash = snapshotHash,
            SnapshotJson = snapshotJson,
            TemplateVersion = "test",
            RendererVersion = "test",
            State = nameof(CaseReportGenerationState.Confirmed),
            GeneratedAtUtc = generatedAtUtc,
            Version = 1
        };

    private static DocumentVersionEntity Version(
        Guid id,
        Guid documentId,
        DateTimeOffset createdAtUtc,
        string hash,
        string fileName) => new()
        {
            Id = id,
            DocumentId = documentId,
            Version = 1,
            FileName = fileName,
            MediaType = "application/pdf",
            ContentLength = 1,
            Sha256 = hash,
            CustodyStatus = DocumentCustodyStatus.Confirmed,
            CreatedAtUtc = createdAtUtc,
            CreatedBy = "test",
            IsCurrent = true
        };

    private static GeneratedCaseArtifactEntity Artifact(
        Guid id,
        Guid generationId,
        Guid versionId,
        string hash,
        string kind) => new()
        {
            Id = id,
            GenerationId = generationId,
            VersionId = versionId,
            Kind = kind,
            Sha256 = hash,
            State = nameof(CaseReportArtifactStatus.Confirmed),
            OperationKey = $"monthly-report:{id:N}"
        };

}
