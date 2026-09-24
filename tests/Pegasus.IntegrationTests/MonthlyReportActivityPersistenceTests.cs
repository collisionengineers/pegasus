using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
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
                RecordedAtUtc = laterReportGeneratedAt,
                ConfirmedBy = Guid.NewGuid().ToString("D"),
                ConfirmedAtUtc = laterReportGeneratedAt
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

    private static CaseReportGenerationEntity Generation(
        Guid id,
        Guid caseId,
        DateTimeOffset generatedAtUtc,
        string snapshotJson,
        string snapshotHash) => new()
        {
            Id = id,
            CaseId = caseId,
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
