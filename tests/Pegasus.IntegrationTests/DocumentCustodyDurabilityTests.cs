using Pegasus.Core.Custody;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class DocumentCustodyDurabilityTests
{
    [Fact]
    public async Task CaseDocumentsOriginalReportMarkIsDurableReplaySafeAndUnique()
    {
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database, "audit");
            var firstOccurrenceId = await SeedCurrentDocumentAsync(database, caseId, 0, "audit-report.pdf");
            var secondOccurrenceId = await SeedCurrentDocumentAsync(database, caseId, 1, "other-report.pdf");
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
            MarkAsOriginalReportCommand command;
            OriginalReportRecorded recorded;

            await using (var scope = database.CreateAsyncScope())
            {
                var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                    .ClaimAsync(
                        new(caseId, 0, actor, $"original-report-lease:{Guid.NewGuid():N}"),
                        CancellationToken.None);
                command = new(
                    caseId,
                    lease.Version,
                    actor,
                    $"original-report:{Guid.NewGuid():N}",
                    lease.Token,
                    firstOccurrenceId,
                    await VersionIdAsync(database, firstOccurrenceId));
                var mark = scope.ServiceProvider.GetRequiredService<MarkAsOriginalReport>();

                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    mark.ExecuteAsync(
                        command with
                        {
                            OperationKey = $"wrong-case-document:{Guid.NewGuid():N}",
                            DocumentOccurrenceId = Guid.NewGuid()
                        },
                        CancellationToken.None));

                recorded = await mark.ExecuteAsync(command, CancellationToken.None);
                var replay = await mark.ExecuteAsync(command, CancellationToken.None);

                Assert.Equal(recorded, replay);
            }

            await using (var verification = await database.CreateContextAsync())
            {
                var occurrences = await verification.Set<DocumentOccurrenceEntity>()
                    .OrderBy(value => value.Id)
                    .ToArrayAsync();
                Assert.Equal(
                    DocumentSemanticRole.AuditReport,
                    occurrences.Single(value => value.Id == firstOccurrenceId).SemanticRole);
                Assert.Equal(
                    DocumentSemanticRole.Instruction,
                    occurrences.Single(value => value.Id == secondOccurrenceId).SemanticRole);
                var history = await verification.CaseWorkflowEvents
                    .SingleAsync(value => value.EventType == "original_report_recorded");
                Assert.Equal("Original report: audit-report.pdf", history.Reason);
                Assert.Equal(command.OperationKey, history.OperationKey);
            }

            await using (var scope = database.CreateAsyncScope())
            {
                var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                    .ClaimAsync(
                        new(caseId, recorded.CaseVersion, actor, $"second-original-report-lease:{Guid.NewGuid():N}"),
                        CancellationToken.None);
                var second = command with
                {
                    ExpectedVersion = lease.Version,
                    OperationKey = $"second-original-report:{Guid.NewGuid():N}",
                    EditLeaseToken = lease.Token,
                    DocumentOccurrenceId = secondOccurrenceId,
                    DocumentVersionId = await VersionIdAsync(database, secondOccurrenceId)
                };

                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    scope.ServiceProvider.GetRequiredService<MarkAsOriginalReport>()
                        .ExecuteAsync(second, CancellationToken.None));
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// The marked document's own reading fills the Original report cells in
    /// the Mark's transaction (v28 P51, #840): a cell staff recorded keeps
    /// its value, as does one the Automation actor recorded on purpose
    /// (operator, 7 October 2026); one the extraction recorded takes the
    /// reading, every filled cell is recorded as the report extraction, the
    /// history keeps both values, and a replay writes nothing more.
    /// </summary>
    [Fact]
    public async Task MarkingFillsTheOriginalReportCellsStaffHaveNotRecorded()
    {
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database, "audit");
            var occurrenceId = await SeedCurrentDocumentAsync(database, caseId, 0, "laird-report.pdf");
            var versionId = await VersionIdAsync(database, occurrenceId);
            var seededAt = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
            await using (var context = await database.CreateContextAsync())
            {
                context.CaseAssessmentFields.AddRange(
                    new CaseAssessmentFieldEntity
                    {
                        WorkId = caseId,
                        FieldPath = AssessmentVocabulary.OriginalReportAssessor,
                        Value = "Northside Assessors",
                        RecordedByKind = nameof(ActorKind.Staff),
                        RecordedBy = "staff-engineer",
                        RecordedAtUtc = seededAt
                    },
                    new CaseAssessmentFieldEntity
                    {
                        WorkId = caseId,
                        FieldPath = AssessmentVocabulary.OriginalReportRoadworthiness,
                        Value = "unroadworthy",
                        RecordedByKind = nameof(ActorKind.Automation),
                        RecordedBy = OriginalReportPrefillPolicy.RecorderId,
                        RecordedAtUtc = seededAt
                    },
                    new CaseAssessmentFieldEntity
                    {
                        WorkId = caseId,
                        FieldPath = AssessmentVocabulary.OriginalReportOutcome,
                        Value = "total_loss",
                        RecordedByKind = nameof(ActorKind.Automation),
                        RecordedBy = "pegasus-automation",
                        RecordedAtUtc = seededAt
                    });
                await context.SaveChangesAsync();
            }

            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
            var reading = new OriginalReportReading(
                new string('a', 64), "Laird Assessors", "2026-09-01", "roadworthy", "repairable", false);
            await using (var scope = database.CreateAsyncScope())
            {
                var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                    .ClaimAsync(
                        new(caseId, 0, actor, $"original-report-fill-lease:{Guid.NewGuid():N}"),
                        CancellationToken.None);
                var command = new MarkAsOriginalReportCommand(
                    caseId,
                    lease.Version,
                    actor,
                    $"original-report-fill:{Guid.NewGuid():N}",
                    lease.Token,
                    occurrenceId,
                    versionId);
                var store = scope.ServiceProvider.GetRequiredService<IMarkAsOriginalReportStore>();

                await Assert.ThrowsAsync<InvalidOperationException>(() => store.MarkAsOriginalReportAsync(
                    command with
                    {
                        OperationKey = $"original-report-other-version:{Guid.NewGuid():N}",
                        DocumentVersionId = Guid.NewGuid()
                    },
                    reading,
                    CancellationToken.None));

                var recorded = await store.MarkAsOriginalReportAsync(command, reading, CancellationToken.None);
                var replay = await store.MarkAsOriginalReportAsync(
                    command, reading with { Assessor = "Connexus Vehicle Assessors" }, CancellationToken.None);

                Assert.Equal(recorded, replay);
            }

            await using var verification = await database.CreateContextAsync();
            var cells = await verification.CaseAssessmentFields
                .Where(item => item.WorkId == caseId)
                .ToDictionaryAsync(item => item.FieldPath);
            Assert.Equal("Northside Assessors", cells[AssessmentVocabulary.OriginalReportAssessor].Value);
            Assert.Equal(nameof(ActorKind.Staff), cells[AssessmentVocabulary.OriginalReportAssessor].RecordedByKind);
            Assert.Equal("total_loss", cells[AssessmentVocabulary.OriginalReportOutcome].Value);
            Assert.Equal("pegasus-automation", cells[AssessmentVocabulary.OriginalReportOutcome].RecordedBy);
            foreach (var (path, value) in new[]
            {
                (AssessmentVocabulary.OriginalReportDate, "2026-09-01"),
                (AssessmentVocabulary.OriginalReportRoadworthiness, "roadworthy")
            })
            {
                Assert.Equal(value, cells[path].Value);
                Assert.Equal(nameof(ActorKind.Automation), cells[path].RecordedByKind);
                Assert.Equal(OriginalReportPrefillPolicy.RecorderId, cells[path].RecordedBy);
            }

            var history = await verification.ActionHistory
                .SingleAsync(item => item.EventKind == "original_report_recorded");
            Assert.Contains("\"original_report.roadworthiness\":\"unroadworthy\"", history.BeforeJson, StringComparison.Ordinal);
            Assert.Contains("\"original_report.roadworthiness\":\"roadworthy\"", history.AfterJson, StringComparison.Ordinal);
            Assert.DoesNotContain(AssessmentVocabulary.OriginalReportAssessor, history.AfterJson, StringComparison.Ordinal);
            Assert.DoesNotContain(AssessmentVocabulary.OriginalReportOutcome, history.AfterJson, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// A fileless Principal API Audit keeps its declared verdict on the Case from
    /// creation (#919). Marking its report later fills Repairable status from
    /// that verdict when the report printed no outcome or the same one, and
    /// leaves the cell blank when the report disagrees.
    /// </summary>
    [Theory]
    [InlineData(null, "total_loss")]
    [InlineData("total_loss", "total_loss")]
    [InlineData("repairable", null)]
    public async Task MarkingAFilelessAuditsReportReconcilesItsDeclaredVerdict(
        string? printedOutcome, string? expectedOutcome)
    {
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database, "audit");
            var occurrenceId = await SeedCurrentDocumentAsync(database, caseId, 0, "laird-report.pdf");
            var versionId = await VersionIdAsync(database, occurrenceId);
            await using (var context = await database.CreateContextAsync())
            {
                // The declared verdict is on the Case; no Original report cell is filled yet.
                (await context.Cases.SingleAsync(item => item.Id == caseId)).StandaloneAuditAssessment = "total_loss";
                await context.SaveChangesAsync();
            }

            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
            var reading = new OriginalReportReading(
                new string('a', 64), "Laird Assessors", "2026-09-01", "roadworthy", printedOutcome, false);
            await using (var scope = database.CreateAsyncScope())
            {
                var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                    .ClaimAsync(
                        new(caseId, 0, actor, $"fileless-mark-lease:{Guid.NewGuid():N}"),
                        CancellationToken.None);
                await scope.ServiceProvider.GetRequiredService<IMarkAsOriginalReportStore>()
                    .MarkAsOriginalReportAsync(
                        new MarkAsOriginalReportCommand(
                            caseId,
                            lease.Version,
                            actor,
                            $"fileless-mark:{Guid.NewGuid():N}",
                            lease.Token,
                            occurrenceId,
                            versionId),
                        reading,
                        CancellationToken.None);
            }

            await using var verification = await database.CreateContextAsync();
            var cells = await verification.CaseAssessmentFields
                .Where(item => item.WorkId == caseId)
                .ToDictionaryAsync(item => item.FieldPath);
            Assert.Equal(
                expectedOutcome,
                cells.TryGetValue(AssessmentVocabulary.OriginalReportOutcome, out var outcome) ? outcome.Value : null);
            Assert.Equal("Laird Assessors", cells[AssessmentVocabulary.OriginalReportAssessor].Value);
            Assert.Equal("roadworthy", cells[AssessmentVocabulary.OriginalReportRoadworthiness].Value);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// A reading of bytes other than the marked version's fills nothing: the
    /// role is still recorded and the cells stay hand-entered.
    /// </summary>
    [Fact]
    public async Task AReadingOfOtherBytesFillsNoOriginalReportCell()
    {
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database, "audit");
            var occurrenceId = await SeedCurrentDocumentAsync(database, caseId, 0, "other-report.pdf");
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
            await using (var scope = database.CreateAsyncScope())
            {
                var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                    .ClaimAsync(
                        new(caseId, 0, actor, $"original-report-mismatch-lease:{Guid.NewGuid():N}"),
                        CancellationToken.None);
                await scope.ServiceProvider.GetRequiredService<IMarkAsOriginalReportStore>()
                    .MarkAsOriginalReportAsync(
                        new(
                            caseId,
                            lease.Version,
                            actor,
                            $"original-report-mismatch:{Guid.NewGuid():N}",
                            lease.Token,
                            occurrenceId,
                            await VersionIdAsync(database, occurrenceId)),
                        new(new string('b', 64), "Laird Assessors", "2026-09-01", "roadworthy", "repairable", false),
                        CancellationToken.None);
            }

            await using var verification = await database.CreateContextAsync();
            Assert.Equal(
                DocumentSemanticRole.AuditReport,
                (await verification.Set<DocumentOccurrenceEntity>().SingleAsync(item => item.Id == occurrenceId))
                    .SemanticRole);
            Assert.False(await verification.CaseAssessmentFields.AnyAsync(item => item.WorkId == caseId));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// A report recognised among a receipt's filed files is recorded by the
    /// system exactly as a staff Mark records one (#901): the Audit report
    /// role, the Original report cells staff have not recorded, the history
    /// line in the system's name, and a replay that writes nothing more. Once
    /// recorded, the Case no longer awaits its report.
    /// </summary>
    [Fact]
    public async Task ARecognisedReportIsRecordedByTheSystemAsAStaffMarkRecordsOne()
    {
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database, "audit");
            var receiptId = Guid.NewGuid();
            var assetId = Guid.NewGuid();
            var occurrenceId = await SeedCurrentDocumentAsync(
                database, caseId, 0, "connexus-report.pdf",
                AutomaticCaseEvidencePromotionOperationKey.For(caseId, receiptId, assetId),
                DocumentSemanticRole.Correspondence);
            await SeedCurrentDocumentAsync(
                database, caseId, 1, "other-case-file.pdf", role: DocumentSemanticRole.Correspondence,
                sha256: new string('b', 64));
            await using (var context = await database.CreateContextAsync())
            {
                context.CaseAssessmentFields.Add(new CaseAssessmentFieldEntity
                {
                    WorkId = caseId,
                    FieldPath = AssessmentVocabulary.OriginalReportAssessor,
                    Value = "Northside Assessors",
                    RecordedByKind = nameof(ActorKind.Staff),
                    RecordedBy = "staff-engineer",
                    RecordedAtUtc = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero)
                });
                await context.SaveChangesAsync();
            }

            var reading = new OriginalReportReading(
                new string('a', 64), "Connexus Vehicle Assessors", "2026-03-09", "unroadworthy", "repairable", false);
            await using (var scope = database.CreateAsyncScope())
            {
                var store = scope.ServiceProvider.GetRequiredService<IRecogniseOriginalReportStore>();
                // Found by the bytes' hash, however the file was filed, and whatever
                // case the hash was recorded in: mail intake writes capitals.
                var found = await store.FindAwaitingCandidatesAsync(
                    caseId,
                    receiptId,
                    [new(assetId, new string('A', 64)), new(Guid.NewGuid(), new string('c', 64))],
                    CancellationToken.None);
                Assert.True(found.CaseAwaitsReport);
                var candidate = Assert.Single(found.Filed);
                Assert.Equal(new(assetId, occurrenceId, await VersionIdAsync(database, occurrenceId)), candidate);
                var command = new RecordRecognisedOriginalReport(
                    caseId,
                    receiptId,
                    candidate.DocumentOccurrenceId,
                    candidate.DocumentVersionId,
                    ActionActor.SystemWorker("intake-processing"),
                    OriginalReportRecognitionOperationKey.For(receiptId));

                var recorded = await store.RecordRecognisedAsync(command, reading, CancellationToken.None);
                var replay = await store.RecordRecognisedAsync(command, reading, CancellationToken.None);

                Assert.NotNull(recorded);
                Assert.Equal(recorded, replay);
                Assert.Equal(1, recorded.CaseVersion);
                var afterwards = await store.FindAwaitingCandidatesAsync(
                    caseId, receiptId, [new(assetId, new string('a', 64))], CancellationToken.None);
                Assert.False(afterwards.CaseAwaitsReport);
                Assert.Empty(afterwards.Filed);
            }

            await using var verification = await database.CreateContextAsync();
            Assert.Equal(
                DocumentSemanticRole.AuditReport,
                (await verification.Set<DocumentOccurrenceEntity>().SingleAsync(item => item.Id == occurrenceId))
                    .SemanticRole);
            var cells = await verification.CaseAssessmentFields
                .Where(item => item.WorkId == caseId)
                .ToDictionaryAsync(item => item.FieldPath);
            Assert.Equal("Northside Assessors", cells[AssessmentVocabulary.OriginalReportAssessor].Value);
            Assert.Equal("2026-03-09", cells[AssessmentVocabulary.OriginalReportDate].Value);
            Assert.Equal(OriginalReportPrefillPolicy.RecorderId, cells[AssessmentVocabulary.OriginalReportDate].RecordedBy);
            var history = await verification.CaseWorkflowEvents
                .SingleAsync(value => value.EventType == "original_report_recorded");
            Assert.Equal("Original report: connexus-report.pdf", history.Reason);
            Assert.Equal(nameof(ActorKind.SystemWorker), history.ActorKind);
            Assert.Equal(OriginalReportRecognitionOperationKey.For(receiptId), history.OperationKey);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// Recognition yields to a member of staff editing the Case and writes
    /// nothing, so its retry records the report once they finish; a Case that
    /// is not an open Audit awaiting its report records nothing at all.
    /// </summary>
    [Fact]
    public async Task RecognitionYieldsToACaseEditorAndRecordsNothingOnACaseNoLongerAwaitingItsReport()
    {
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database, "audit");
            var receiptId = Guid.NewGuid();
            var assetId = Guid.NewGuid();
            var report = await SeedCurrentDocumentAsync(
                database, caseId, 0, "report.pdf",
                AutomaticCaseEvidencePromotionOperationKey.For(caseId, receiptId, assetId),
                DocumentSemanticRole.Correspondence);
            var command = new RecordRecognisedOriginalReport(
                caseId, receiptId, report, await VersionIdAsync(database, report),
                ActionActor.SystemWorker("intake-processing"), OriginalReportRecognitionOperationKey.For(receiptId));
            var reading = new OriginalReportReading(
                new string('a', 64), "Connexus Vehicle Assessors", "2026-03-09", "unroadworthy", "repairable", false);
            await using var scope = database.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IRecogniseOriginalReportStore>();

            await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>().ClaimAsync(
                new(caseId, 0, ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
                    $"recognition-editor:{Guid.NewGuid():N}"),
                CancellationToken.None);
            await Assert.ThrowsAsync<IntakeDependencyUnavailableException>(
                () => store.RecordRecognisedAsync(command, reading, CancellationToken.None));

            // The editor finished and the Case was completed: it no longer awaits its report.
            await using (var context = await database.CreateContextAsync())
            {
                var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == caseId);
                workflow.State = nameof(CaseLifecycleState.PostReportComplete);
                workflow.EditLeaseExpiresAtUtc = null;
                await context.SaveChangesAsync();
            }

            var completed = await store.FindAwaitingCandidatesAsync(
                caseId, receiptId, [new(assetId, new string('a', 64))], CancellationToken.None);
            Assert.False(completed.CaseAwaitsReport);
            Assert.Empty(completed.Filed);
            Assert.Null(await store.RecordRecognisedAsync(command, reading, CancellationToken.None));

            await using var verification = await database.CreateContextAsync();
            Assert.False(await verification.Set<DocumentOccurrenceEntity>()
                .AnyAsync(item => item.SemanticRole == DocumentSemanticRole.AuditReport));
            Assert.False(await verification.CaseWorkflowEvents
                .AnyAsync(value => value.EventType == "original_report_recorded"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RemovedOriginalReportCanBeReplacedButCannotBeMarkedAgain()
    {
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database, "audit");
            var removedOccurrenceId = await SeedCurrentDocumentAsync(
                database, caseId, 0, "removed-original-report.pdf");
            var replacementOccurrenceId = await SeedCurrentDocumentAsync(
                database, caseId, 1, "replacement-original-report.pdf");
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
            OriginalReportRecorded firstMark;

            await using (var scope = database.CreateAsyncScope())
            {
                var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                    .ClaimAsync(
                        new(caseId, 0, actor, $"first-original-report-lease:{Guid.NewGuid():N}"),
                        CancellationToken.None);
                firstMark = await scope.ServiceProvider.GetRequiredService<MarkAsOriginalReport>()
                    .ExecuteAsync(
                        new(
                            caseId,
                            lease.Version,
                            actor,
                            $"first-original-report:{Guid.NewGuid():N}",
                            lease.Token,
                            removedOccurrenceId,
                            await VersionIdAsync(database, removedOccurrenceId)),
                        CancellationToken.None);
            }

            await using (var scope = database.CreateAsyncScope())
            {
                var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                    .ClaimAsync(
                        new(
                            caseId,
                            firstMark.CaseVersion,
                            actor,
                            $"remove-original-report-lease:{Guid.NewGuid():N}"),
                        CancellationToken.None);
                await scope.ServiceProvider.GetRequiredService<ILogicallyRemoveDocument>()
                    .ExecuteAsync(
                        new(
                            caseId,
                            removedOccurrenceId,
                            actor,
                            "Superseded original report.",
                            $"remove-original-report:{Guid.NewGuid():N}",
                            lease.Version,
                            lease.Token),
                        CancellationToken.None);
            }

            OriginalReportRecorded replacement;
            await using (var scope = database.CreateAsyncScope())
            {
                var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                    .ClaimAsync(
                        new(
                            caseId,
                            firstMark.CaseVersion + 1,
                            actor,
                            $"replacement-original-report-lease:{Guid.NewGuid():N}"),
                        CancellationToken.None);
                var mark = scope.ServiceProvider.GetRequiredService<MarkAsOriginalReport>();
                var remarkedVersionId = await VersionIdAsync(database, removedOccurrenceId);
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    mark.ExecuteAsync(
                        new(
                            caseId,
                            lease.Version,
                            actor,
                            $"remark-removed-original-report:{Guid.NewGuid():N}",
                            lease.Token,
                            removedOccurrenceId,
                            remarkedVersionId),
                        CancellationToken.None));

                replacement = await mark.ExecuteAsync(
                    new(
                        caseId,
                        lease.Version,
                        actor,
                        $"replacement-original-report:{Guid.NewGuid():N}",
                        lease.Token,
                        replacementOccurrenceId,
                        await VersionIdAsync(database, replacementOccurrenceId)),
                    CancellationToken.None);
            }

            Assert.Equal(replacementOccurrenceId, replacement.DocumentOccurrenceId);
            Assert.Equal(firstMark.CaseVersion + 2, replacement.CaseVersion);
            await using var verification = await database.CreateContextAsync();
            var occurrences = await verification.Set<DocumentOccurrenceEntity>()
                .Where(value => value.CaseId == caseId)
                .ToArrayAsync();
            Assert.Equal(
                DocumentSemanticRole.AuditReport,
                occurrences.Single(value => value.Id == removedOccurrenceId).SemanticRole);
            Assert.Equal(
                DocumentSemanticRole.AuditReport,
                occurrences.Single(value => value.Id == replacementOccurrenceId).SemanticRole);
            var documentIds = occurrences.Select(value => value.DocumentId).ToArray();
            var versions = await verification.Set<DocumentVersionEntity>()
                .Where(value => documentIds.Contains(value.DocumentId))
                .ToArrayAsync();
            var removedVersionId = occurrences
                .Single(value => value.Id == removedOccurrenceId)
                .VersionId;
            var replacementVersionId = occurrences
                .Single(value => value.Id == replacementOccurrenceId)
                .VersionId;
            var removedVersion = versions.Single(value => value.Id == removedVersionId);
            Assert.False(removedVersion.IsCurrent);
            Assert.True(removedVersion.IsLogicallyRemoved);
            var replacementVersion = versions.Single(value => value.Id == replacementVersionId);
            Assert.True(replacementVersion.IsCurrent);
            Assert.False(replacementVersion.IsLogicallyRemoved);
            Assert.Equal(
                2,
                await verification.CaseWorkflowEvents.CountAsync(
                    value => value.EventType == "original_report_recorded"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RemovingAFileWritesOneNoteTheOperatorCanActuallySee()
    {
        // The point of this test is the ROUND TRIP, not the row. A note written
        // to CaseHistory persists happily, reports success, and never appears on
        // the Notes tab — which is how the Release 22 note defect reached
        // production. So it is asserted through the store's history read, the
        // same read the Notes section makes.
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database);
            var occurrenceId = await SeedCurrentImageAsync(database, caseId);
            await using var scope = database.CreateAsyncScope();
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
            var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                .ClaimAsync(
                    new(caseId, 0, actor, $"removal-note-lease:{Guid.NewGuid():N}"),
                    CancellationToken.None);
            var command = new LogicallyRemoveDocumentCommand(
                caseId,
                occurrenceId,
                actor,
                "Wrong vehicle — the photograph belongs to another claim.",
                $"removal-note:{Guid.NewGuid():N}",
                lease.Version,
                lease.Token);

            var remover = scope.ServiceProvider.GetRequiredService<ILogicallyRemoveDocument>();
            await remover.ExecuteAsync(command, CancellationToken.None);
            // Replay must not add a second note.
            await remover.ExecuteAsync(command, CancellationToken.None);

            // Read through ICaseQueryStore — the component that builds the very
            // History collection the Notes tab renders. Asserting the row in
            // CaseWorkflowEvents directly would pass just as happily for a row
            // written to CaseHistory, which is the defect this guards against.
            var history = await scope.ServiceProvider.GetRequiredService<ICaseQueryStore>()
                .ListHistoryAsync(caseId, CancellationToken.None);
            var note = Assert.Single(
                history,
                entry => entry.EventType == "case_document_removed");
            Assert.Equal(command.Reason, note.Reason);
            Assert.Equal(ActorKind.Staff.ToString(), note.ActorKind);
            Assert.Equal(actor.SubjectId.ToString(), note.Actor);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// Tagging an image is a Case mutation like any other: it carries the
    /// lease, the expected version and an operation key, moves the version,
    /// and replays exactly. The tag row keeps who applied it and when.
    /// </summary>
    [Fact]
    public async Task TaggingAnImageIsDurableExactlyReplayableAndReversible()
    {
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database);
            var occurrenceId = await SeedCurrentImageAsync(database, caseId);
            await using var scope = database.CreateAsyncScope();
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
            var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                .ClaimAsync(
                    new(caseId, 0, actor, $"image-tag-lease:{Guid.NewGuid():N}"),
                    CancellationToken.None);
            var command = new TagCaseImageCommand(
                caseId,
                occurrenceId,
                ImageTagVocabulary.ThirdPartyId,
                actor,
                $"image-tag:{Guid.NewGuid():N}",
                lease.Version,
                lease.Token);
            var tagger = scope.ServiceProvider.GetRequiredService<ITagCaseImage>();

            await tagger.ExecuteAsync(command, CancellationToken.None);
            await tagger.ExecuteAsync(command, CancellationToken.None);

            long taggedVersion;
            await using (var verification = await database.CreateContextAsync())
            {
                var assignment = await verification.Set<DocumentOccurrenceTagEntity>()
                    .SingleAsync(item => item.OccurrenceId == occurrenceId);
                Assert.Equal(ImageTagVocabulary.ThirdPartyId, assignment.TagId);
                Assert.Equal(nameof(ActorKind.Staff), assignment.AppliedByKind);
                Assert.Equal(actor.SubjectId, assignment.AppliedBySubjectId);
                Assert.Equal(command.OperationKey, assignment.OperationKey);
                var history = await verification.ActionHistory.SingleAsync(item =>
                    item.CorrelationId == command.OperationKey);
                Assert.Equal("case_image_tagged", history.EventKind);
                Assert.Null(history.Reason);
                taggedVersion = await verification.CaseWorkflows
                    .Where(item => item.CaseId == caseId)
                    .Select(item => item.Version)
                    .SingleAsync();
                Assert.Equal(lease.Version + 1, taggedVersion);
            }

            // A second tag under the first operation key is refused, and the
            // tag comes off under its own key.
            await Assert.ThrowsAsync<InvalidOperationException>(() => tagger.ExecuteAsync(
                command with { OperationKey = $"image-tag:{Guid.NewGuid():N}", ExpectedCaseVersion = taggedVersion },
                CancellationToken.None));
            // The tag mutation consumed the lease it was given — every real
            // mutation clears it on completion, the same as any other Case
            // edit — so the untag needs a lease claimed fresh against the
            // post-tag version.
            var untagLease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                .ClaimAsync(
                    new(caseId, taggedVersion, actor, $"image-untag-lease:{Guid.NewGuid():N}"),
                    CancellationToken.None);
            var untagCommand = new UntagCaseImageCommand(
                caseId,
                occurrenceId,
                ImageTagVocabulary.ThirdPartyId,
                actor,
                $"image-untag:{Guid.NewGuid():N}",
                taggedVersion,
                untagLease.Token);
            var untagger = scope.ServiceProvider.GetRequiredService<IUntagCaseImage>();
            await untagger.ExecuteAsync(untagCommand, CancellationToken.None);
            await untagger.ExecuteAsync(untagCommand, CancellationToken.None);

            await using var afterRemoval = await database.CreateContextAsync();
            Assert.Empty(await afterRemoval.Set<DocumentOccurrenceTagEntity>()
                .Where(item => item.OccurrenceId == occurrenceId)
                .ToArrayAsync());
            Assert.Equal(
                "case_image_untagged",
                await afterRemoval.ActionHistory
                    .Where(item => item.CorrelationId == untagCommand.OperationKey)
                    .Select(item => item.EventKind)
                    .SingleAsync());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// A new image is in the report (operator, 26 September 2026). Tagging it
    /// Third party takes it out at once and moves its preparation version;
    /// staff may put it back with In report, which replays exactly.
    /// </summary>
    [Fact]
    public async Task ANewImageIsInTheReportAndTaggingItThirdPartyTakesItOut()
    {
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database);
            var occurrenceId = await SeedCurrentImageAsync(database, caseId);
            await using var scope = database.CreateAsyncScope();
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
            var leases = scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>();
            Assert.True((await OccurrenceAsync(database, occurrenceId)).InReport);

            var lease = await leases.ClaimAsync(
                new(caseId, 0, actor, $"third-party-lease:{Guid.NewGuid():N}"), CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<ITagCaseImage>().ExecuteAsync(
                new(caseId, occurrenceId, ImageTagVocabulary.ThirdPartyId, actor,
                    $"third-party-tag:{Guid.NewGuid():N}", lease.Version, lease.Token),
                CancellationToken.None);

            var takenOut = await OccurrenceAsync(database, occurrenceId);
            Assert.False(takenOut.InReport);
            Assert.Null(takenOut.SupportingOrder);
            Assert.Equal(1, takenOut.PreparationVersion);

            var putBackLease = await leases.ClaimAsync(
                new(caseId, lease.Version + 1, actor, $"in-report-lease:{Guid.NewGuid():N}"), CancellationToken.None);
            var putBack = new SetCaseImageInReportCommand(
                caseId, occurrenceId, true, actor, $"in-report:{Guid.NewGuid():N}", lease.Version + 1, putBackLease.Token);
            var inReport = scope.ServiceProvider.GetRequiredService<ISetCaseImageInReport>();
            await inReport.ExecuteAsync(putBack, CancellationToken.None);
            await inReport.ExecuteAsync(putBack, CancellationToken.None);

            await using var verification = await database.CreateContextAsync();
            var back = await verification.Set<DocumentOccurrenceEntity>().SingleAsync(item => item.Id == occurrenceId);
            Assert.True(back.InReport);
            Assert.Equal(2, back.PreparationVersion);
            Assert.Equal(
                "case_image_in_report",
                await verification.ActionHistory
                    .Where(item => item.CorrelationId == putBack.OperationKey)
                    .Select(item => item.EventKind)
                    .SingleAsync());
            Assert.Equal(
                lease.Version + 2,
                await verification.CaseWorkflows.Where(item => item.CaseId == caseId).Select(item => item.Version).SingleAsync());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// The tag decides how an image in the report prints (operator, 26
    /// September 2026): tagging one image Overview clears the image blocker
    /// with no Case save, the Close-up being optional (operator, 7 October
    /// 2026), and tagging stales the current report.
    /// </summary>
    [Fact]
    public async Task TaggingOneOverviewAndOneCloseUpClearsTheImageBlockerWithoutACaseSave()
    {
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database);
            var overview = await SeedCurrentImageAsync(database, caseId);
            var closeUp = await SeedCurrentImageAsync(database, caseId);
            var generationId = await SeedCurrentGenerationAsync(database, caseId);
            await using var scope = database.CreateAsyncScope();
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
            var leases = scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>();
            var queries = scope.ServiceProvider.GetRequiredService<ICaseAssetPreparationQueries>();
            var tagger = scope.ServiceProvider.GetRequiredService<ITagCaseImage>();
            Assert.Equal(
                [CaseReportReadiness.OverviewImageRequirement],
                ImageBlockers(await queries.ListForCaseAsync(caseId, CancellationToken.None)));

            var version = 0L;
            foreach (var (occurrenceId, tagId) in new[]
            {
                (overview, ImageTagVocabulary.OverviewId),
                (closeUp, ImageTagVocabulary.CloseUpId),
            })
            {
                var lease = await leases.ClaimAsync(
                    new(caseId, version, actor, $"tag-lease:{Guid.NewGuid():N}"), CancellationToken.None);
                await tagger.ExecuteAsync(
                    new(caseId, occurrenceId, tagId, actor, $"tag:{Guid.NewGuid():N}", lease.Version, lease.Token),
                    CancellationToken.None);
                version = lease.Version + 1;
            }

            Assert.Empty(ImageBlockers(await queries.ListForCaseAsync(caseId, CancellationToken.None)));
            await using var verification = await database.CreateContextAsync();
            Assert.Equal(
                nameof(CaseReportGenerationState.Stale),
                await verification.Set<CaseReportGenerationEntity>()
                    .Where(item => item.Id == generationId)
                    .Select(item => item.State)
                    .SingleAsync());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// A tag that takes an image out of the report says so on the Case
    /// history, as staff taking it out does. The tag replays exactly, and
    /// taking the tag off leaves the image out: staff put it back.
    /// </summary>
    [Theory]
    [InlineData("00000000-0000-4000-8000-0000000017a3")]
    [InlineData("00000000-0000-4000-8000-0000000017a4")]
    public async Task ATagThatTakesAnImageOutWritesTheHistoryLineAndUntaggingLeavesItOut(string tag)
    {
        var tagId = Guid.Parse(tag);
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database);
            var occurrenceId = await SeedCurrentImageAsync(database, caseId);
            await using var scope = database.CreateAsyncScope();
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
            var leases = scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>();
            var tagger = scope.ServiceProvider.GetRequiredService<ITagCaseImage>();

            var lease = await leases.ClaimAsync(
                new(caseId, 0, actor, $"tag-lease:{Guid.NewGuid():N}"), CancellationToken.None);
            var tagged = new TagCaseImageCommand(
                caseId, occurrenceId, tagId, actor, $"tag:{Guid.NewGuid():N}", lease.Version, lease.Token);
            await tagger.ExecuteAsync(tagged, CancellationToken.None);
            await tagger.ExecuteAsync(tagged, CancellationToken.None);

            await using (var verification = await database.CreateContextAsync())
            {
                var history = await verification.ActionHistory.AsNoTracking()
                    .Where(item => item.AggregateType == "case_document" && item.AggregateId == caseId.ToString("D"))
                    .ToListAsync();
                Assert.Single(history, item => item.EventKind == "case_image_tagged");
                var takenOut = Assert.Single(history, item => item.EventKind == "case_image_out_of_report");
                Assert.NotEqual(tagged.OperationKey, takenOut.CorrelationId);
                Assert.Equal(actor.SubjectId, takenOut.ActorSubjectId);
                Assert.Contains(occurrenceId.ToString("D"), takenOut.AfterJson, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("\"inReport\":false", takenOut.AfterJson, StringComparison.Ordinal);
            }

            var untagLease = await leases.ClaimAsync(
                new(caseId, lease.Version + 1, actor, $"untag-lease:{Guid.NewGuid():N}"), CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<IUntagCaseImage>().ExecuteAsync(
                new(caseId, occurrenceId, tagId, actor, $"untag:{Guid.NewGuid():N}", untagLease.Version, untagLease.Token),
                CancellationToken.None);

            var untagged = await OccurrenceAsync(database, occurrenceId);
            Assert.False(untagged.InReport);
            Assert.Equal(1, untagged.PreparationVersion);
            await using var after = await database.CreateContextAsync();
            Assert.Empty(await after.Set<DocumentOccurrenceTagEntity>()
                .Where(item => item.OccurrenceId == occurrenceId)
                .ToListAsync());
            Assert.Equal(
                0,
                await after.ActionHistory.CountAsync(item =>
                    item.AggregateId == caseId.ToString("D") && item.EventKind == "case_image_in_report"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// A tag that leaves the image in the report writes no report line: an
    /// image already out stays out and says nothing more.
    /// </summary>
    [Fact]
    public async Task TaggingAnImageAlreadyOutOfTheReportWritesNoSecondHistoryLine()
    {
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database);
            var occurrenceId = await SeedCurrentImageAsync(database, caseId);
            await using var scope = database.CreateAsyncScope();
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
            var leases = scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>();
            var tagger = scope.ServiceProvider.GetRequiredService<ITagCaseImage>();

            var version = 0L;
            foreach (var tagId in new[] { ImageTagVocabulary.ThirdPartyId, ImageTagVocabulary.ReflectionId })
            {
                var lease = await leases.ClaimAsync(
                    new(caseId, version, actor, $"tag-lease:{Guid.NewGuid():N}"), CancellationToken.None);
                await tagger.ExecuteAsync(
                    new(caseId, occurrenceId, tagId, actor, $"tag:{Guid.NewGuid():N}", lease.Version, lease.Token),
                    CancellationToken.None);
                version = lease.Version + 1;
            }

            await using var verification = await database.CreateContextAsync();
            Assert.Equal(
                1,
                await verification.ActionHistory.CountAsync(item =>
                    item.AggregateId == caseId.ToString("D") && item.EventKind == "case_image_out_of_report"));
            Assert.Equal(1, (await OccurrenceAsync(database, occurrenceId)).PreparationVersion);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// Whether the report uses an image is one of the facts a generation
    /// freezes, so switching In report makes the current generation stale.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SwitchingInReportMakesTheCurrentGenerationStale(bool putBackIn)
    {
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database);
            await SeedCurrentImageAsync(database, caseId);
            var switched = await SeedCurrentImageAsync(database, caseId);
            if (putBackIn)
            {
                await using var seed = await database.CreateContextAsync();
                (await seed.Set<DocumentOccurrenceEntity>().SingleAsync(item => item.Id == switched)).InReport = false;
                await seed.SaveChangesAsync();
            }
            var generationId = await SeedCurrentGenerationAsync(database, caseId);
            await using var scope = database.CreateAsyncScope();
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
            var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>().ClaimAsync(
                new(caseId, 0, actor, $"in-report-lease:{Guid.NewGuid():N}"), CancellationToken.None);

            await scope.ServiceProvider.GetRequiredService<ISetCaseImageInReport>().ExecuteAsync(
                new(caseId, switched, putBackIn, actor, $"in-report:{Guid.NewGuid():N}", lease.Version, lease.Token),
                CancellationToken.None);

            await using var verification = await database.CreateContextAsync();
            var generation = await verification.Set<CaseReportGenerationEntity>()
                .SingleAsync(item => item.Id == generationId);
            Assert.Equal(nameof(CaseReportGenerationState.Stale), generation.State);
            Assert.Equal(putBackIn, (await OccurrenceAsync(database, switched)).InReport);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>The Overview blocker report readiness names over these preparations.</summary>
    internal static IReadOnlyList<string> ImageBlockers(IReadOnlyList<CaseAssetPreparation> preparations) =>
    [
        .. CaseReportReadiness.Evaluate(new CaseReportReadinessInput(
                new CaseAssessmentProjection(
                    Guid.NewGuid(), "QDOS001", 0, CaseLifecycleState.ReportPreparation, null, [], [],
                    new AssessmentCaseOwnedData(
                        null, null, null, null, null, null, "tbc", null, new DateOnly(2031, 5, 6),
                        null, null, null, null, null)),
                null,
                null,
                [],
                null,
                null,
                preparations,
                new Dictionary<Guid, DocumentVersion>()))
            .Reasons
            .Select(reason => reason.Requirement)
            .Where(requirement => requirement is CaseReportReadiness.OverviewImageRequirement)
    ];

    private static async Task<DocumentOccurrenceEntity> OccurrenceAsync(LocalDbTestDatabase database, Guid occurrenceId)
    {
        await using var context = await database.CreateContextAsync();
        return await context.Set<DocumentOccurrenceEntity>().AsNoTracking().SingleAsync(item => item.Id == occurrenceId);
    }

    private static async Task<Guid> SeedCurrentGenerationAsync(LocalDbTestDatabase database, Guid caseId)
    {
        await using var context = await database.CreateContextAsync();
        var generationId = Guid.NewGuid();
        context.Add(new CaseReportGenerationEntity
        {
            Id = generationId,
            CaseId = caseId,
            WorkId = caseId,
            CaseVersion = 0,
            SnapshotHash = new string('2', 64),
            SnapshotJson = ReportGenerationSnapshotFixture.Json(caseId, "seed-generation-current"),
            TemplateVersion = "assessment-report/v1",
            RendererVersion = "renderer/v1",
            State = nameof(CaseReportGenerationState.Confirmed),
            GeneratedAtUtc = new DateTimeOffset(2031, 5, 6, 10, 0, 0, TimeSpan.Zero),
            Version = 1
        });
        await context.SaveChangesAsync();
        return generationId;
    }

    /// <summary>
    /// A replay asserts the audited action, not the current state. The image
    /// the tag was put on can legitimately stop being taggable afterwards — it
    /// is removed, or a new version supersedes it — and the retry of the same
    /// operation key still has to answer with the action it already recorded.
    /// The taggable-image rule ran before the replay check and turned that
    /// retry into a refusal, which is a lost tag for any caller that retries.
    /// </summary>
    [Fact]
    public async Task AnImageTagReplaysAfterTheTaggedVersionStopsBeingTaggable()
    {
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database);
            var occurrenceId = await SeedCurrentImageAsync(database, caseId);
            await using var scope = database.CreateAsyncScope();
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
            var leases = scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>();
            var lease = await leases.ClaimAsync(
                new(caseId, 0, actor, $"image-tag-lease:{Guid.NewGuid():N}"),
                CancellationToken.None);
            var command = new TagCaseImageCommand(
                caseId,
                occurrenceId,
                ImageTagVocabulary.ThirdPartyId,
                actor,
                $"image-tag:{Guid.NewGuid():N}",
                lease.Version,
                lease.Token);
            var tagger = scope.ServiceProvider.GetRequiredService<ITagCaseImage>();
            await tagger.ExecuteAsync(command, CancellationToken.None);

            long taggedVersion;
            await using (var verification = await database.CreateContextAsync())
            {
                taggedVersion = await verification.CaseWorkflows
                    .Where(item => item.CaseId == caseId)
                    .Select(item => item.Version)
                    .SingleAsync();
            }

            // The tagged version stops being taggable: the operator removes the
            // file.
            var removalLease = await leases.ClaimAsync(
                new(caseId, taggedVersion, actor, $"image-removal-lease:{Guid.NewGuid():N}"),
                CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<ILogicallyRemoveDocument>()
                .ExecuteAsync(
                    new(
                        caseId,
                        occurrenceId,
                        actor,
                        "Wrong vehicle — the photograph belongs to another claim.",
                        $"image-removal:{Guid.NewGuid():N}",
                        taggedVersion,
                        removalLease.Token),
                    CancellationToken.None);

            // The exact replay returns, and adds nothing.
            await tagger.ExecuteAsync(command, CancellationToken.None);

            await using var afterReplay = await database.CreateContextAsync();
            Assert.Equal(
                "case_image_tagged",
                await afterReplay.ActionHistory
                    .Where(item => item.CorrelationId == command.OperationKey)
                    .Select(item => item.EventKind)
                    .SingleAsync());

            // A first submission on the same image is still refused: the rule
            // moved behind the replay check, it did not go away.
            var removedVersion = await afterReplay.CaseWorkflows
                .Where(item => item.CaseId == caseId)
                .Select(item => item.Version)
                .SingleAsync();
            var refusedLease = await leases.ClaimAsync(
                new(caseId, removedVersion, actor, $"image-tag-refused-lease:{Guid.NewGuid():N}"),
                CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => tagger.ExecuteAsync(
                command with
                {
                    OperationKey = $"image-tag:{Guid.NewGuid():N}",
                    ExpectedCaseVersion = refusedLease.Version,
                    EditLeaseToken = refusedLease.Token
                },
                CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// A tag belongs to the case that holds the image: another case's lease
    /// and version reach nothing, and the vocabulary refuses a second entry
    /// with a name it already has, whatever the casing.
    /// </summary>
    [Fact]
    public async Task ImageTagsRefuseAnotherCaseAndDuplicateVocabularyNames()
    {
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database);
            var occurrenceId = await SeedCurrentImageAsync(database, caseId);
            await using var scope = database.CreateAsyncScope();
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
            var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                .ClaimAsync(
                    new(caseId, 0, actor, $"image-tag-lease:{Guid.NewGuid():N}"),
                    CancellationToken.None);

            // The image is reached through the case that holds it: another
            // case's identifier finds no occurrence, whatever it carries.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                scope.ServiceProvider.GetRequiredService<ITagCaseImage>().ExecuteAsync(
                    new(
                        Guid.NewGuid(),
                        occurrenceId,
                        ImageTagVocabulary.ThirdPartyId,
                        actor,
                        $"image-tag-cross:{Guid.NewGuid():N}",
                        lease.Version,
                        lease.Token),
                    CancellationToken.None));

            var creator = scope.ServiceProvider.GetRequiredService<ICreateImageTag>();
            var operationKey = $"image-tag-create:{Guid.NewGuid():N}";
            var created = await creator.ExecuteAsync(
                new("Underside", ImageTagColour.Grey, actor, operationKey), CancellationToken.None);
            var replayed = await creator.ExecuteAsync(
                new("Underside", ImageTagColour.Grey, actor, operationKey), CancellationToken.None);

            Assert.False(created.IsReplay);
            Assert.True(replayed.IsReplay);
            Assert.Equal(created.Tag.Id, replayed.Tag.Id);
            Assert.False(created.Tag.IsBuiltIn);
            await Assert.ThrowsAsync<ImageTagNameInUseException>(() => creator.ExecuteAsync(
                new("UNDERSIDE", ImageTagColour.Blue, actor, $"image-tag-create:{Guid.NewGuid():N}"),
                CancellationToken.None));

            var vocabulary = await scope.ServiceProvider
                .GetRequiredService<IReadImageTagVocabulary>()
                .ListAsync(CancellationToken.None);
            Assert.Equal(
                [.. new[] { ImageTagVocabulary.OverviewName, ImageTagVocabulary.CloseUpName, ImageTagVocabulary.ThirdPartyName, ImageTagVocabulary.ReflectionName }.OrderBy(name => name, StringComparer.Ordinal)],
                vocabulary.Where(tag => tag.IsBuiltIn).Select(tag => tag.Name).OrderBy(name => name, StringComparer.Ordinal));
            Assert.Contains(vocabulary, tag => tag.Id == created.Tag.Id);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CancelledContentWriteLeavesNoImmutableDestinationAndRetrySucceeds()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "Pegasus.IntegrationTests",
            Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LocalDocumentContentStore(root);
            var caseId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var content = "complete managed document content"u8.ToArray();
            var sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                store.StoreAsync(
                    caseId,
                    "QDOS001",
                    versionId,
                    content,
                    sha256,
                    cancellationSource.Token));

            var directory = Path.Combine(
                root,
                "cases",
                "QDOS001",
                "managed",
                versionId.ToString("N"));
            Assert.False(File.Exists(Path.Combine(directory, "content")));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));

            await store.StoreAsync(
                caseId,
                "QDOS001",
                versionId,
                content,
                sha256,
                CancellationToken.None);

            await using var retained = await store.OpenReadAsync(
                caseId,
                "QDOS001",
                versionId,
                sha256,
                content.LongLength,
                CancellationToken.None);
            using var copy = new MemoryStream();
            await retained.CopyToAsync(copy);
            Assert.Equal(content, copy.ToArray());
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task FailedDatabaseSaveRollsBackCaseAndRemovesUnreferencedContent()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "Pegasus.IntegrationTests",
            Guid.NewGuid().ToString("N"));
        var interceptor = new FailNextDocumentSaveInterceptor();
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                configureDatabase: options => options.AddInterceptors(interceptor),
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database);
            await using var scope = database.CreateAsyncScope();
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
            var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                .ClaimAsync(
                    new(
                        caseId,
                        ExpectedVersion: 0,
                        actor,
                        $"document-add-lease:{Guid.NewGuid():N}"),
                    CancellationToken.None);
            var command = new AddCaseDocumentCommand(
                caseId,
                "evidence.txt",
                "text/plain",
                "retained evidence"u8.ToArray(),
                DocumentSemanticRole.Other,
                DocumentSource.StaffUpload,
                $"durability:{Guid.NewGuid():N}",
                actor,
                $"document-add:{Guid.NewGuid():N}",
                lease.Version,
                lease.Token);
            var addDocument = scope.ServiceProvider.GetRequiredService<IAddCaseDocument>();
            interceptor.FailNextDocumentSave();

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                addDocument.ExecuteAsync(command, CancellationToken.None));

            var managedDirectory = Path.Combine(
                root,
                "custody",
                "cases",
                "QDOS001",
                "managed");
            Assert.Empty(Directory.EnumerateFiles(
                managedDirectory,
                "content",
                SearchOption.AllDirectories));
            await using (var context = await database.CreateContextAsync())
            {
                Assert.Empty(await context.Set<DocumentVersionEntity>().ToArrayAsync());
                Assert.Equal(
                    3,
                    await context.Set<CaseEntity>()
                        .Where(value => value.Id == caseId)
                        .Select(value => value.Version)
                        .SingleAsync());
                Assert.Equal(
                    0,
                    await context.CaseWorkflows
                        .Where(value => value.CaseId == caseId)
                        .Select(value => value.Version)
                        .SingleAsync());
            }

            var added = await addDocument.ExecuteAsync(command, CancellationToken.None);

            Assert.False(added.IsReplay);
            Assert.Single(Directory.EnumerateFiles(
                managedDirectory,
                "content",
                SearchOption.AllDirectories));
            await using (var context = await database.CreateContextAsync())
            {
                Assert.Equal(
                    1,
                    await context.CaseWorkflows
                        .Where(value => value.CaseId == caseId)
                        .Select(value => value.Version)
                        .SingleAsync());
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task<Guid> SeedCaseAsync(
        LocalDbTestDatabase database,
        string caseType = "inspection")
    {
        await using var context = await database.CreateContextAsync();
        var seeded = await SeededPrincipals.QdosAsync(context);
        var receiptId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var occurredAtUtc = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        context.AddRange(
            new IntakeReceiptEntity
            {
                Id = receiptId,
                SourceFileName = "durability.eml",
                MediaType = "message/rfc822",
                SourceLength = 1,
                SourceHash = new string('0', 64),
                SourceChannel = "manual_upload",
                ExternalReceiptToken = $"durability:{Guid.NewGuid():N}",
                ReceivedAtUtc = occurredAtUtc,
                ProcessedAtUtc = occurredAtUtc,
                SourceReaderKey = "durability-test",
                SourceReaderVersion = "1",
                Version = 0,
                Decision = "case_created",
                DecisionReason = "Durability test fixture.",
                EvidenceJson = "[]",
                FieldsJson = "[]",
                OcrCandidatesJson = "[]"
            },
            new CaseEntity
            {
                Id = caseId,
                PrincipalId = seeded.Id,
                SequenceLineageId = seeded.SequenceLineageId,
                Year = 2031,
                Sequence = 1,
                Reference = caseType == "audit" ? "a.QDOS001" : "QDOS001",
                Type = caseType,
                InitialState = "NotReady",
                // Lowercase, as ToCode writes it in production. The seed said
                // "Confirmed" and nothing noticed, because no test in this file
                // had ever read the case back through the Case query store.
                CustodyState = "confirmed",
                CustodyRootRemoteId = "case-root-id",
                OriginIntakeReceiptId = receiptId,
                CreatedAtUtc = occurredAtUtc,
                Version = 3,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CaseWorkflowEntity
            {
                CaseId = caseId,
                State = "NotReady",
                Version = 0,
                ConcurrencyToken = Guid.NewGuid()
            });
        await context.SaveChangesAsync();
        return caseId;
    }

    private static async Task<Guid> SeedCurrentDocumentAsync(
        LocalDbTestDatabase database,
        Guid caseId,
        int ordinal,
        string fileName,
        string? operationKey = null,
        DocumentSemanticRole role = DocumentSemanticRole.Instruction,
        string? sha256 = null)
    {
        await using var context = await database.CreateContextAsync();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        context.AddRange(
            new CaseDocumentEntity
            {
                Id = documentId,
                CaseId = caseId,
                Ordinal = ordinal,
                SourceOccurrenceIdentity = $"test-document:{occurrenceId:N}"
            },
            new DocumentVersionEntity
            {
                Id = versionId,
                DocumentId = documentId,
                Version = 1,
                FileName = fileName,
                MediaType = "application/pdf",
                ContentLength = 1,
                Sha256 = sha256 ?? new string('a', 64),
                CustodyStatus = DocumentCustodyStatus.Confirmed,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "Staff:test",
                IsCurrent = true
            },
            new DocumentOccurrenceEntity
            {
                Id = occurrenceId,
                CaseId = caseId,
                DocumentId = documentId,
                VersionId = versionId,
                Ordinal = ordinal,
                SemanticRole = role,
                Source = DocumentSource.StaffUpload,
                SourceOccurrenceIdentity = $"test-document:{occurrenceId:N}",
                RecordedAtUtc = DateTimeOffset.UtcNow,
                OperationKey = operationKey ?? $"seed-document:{occurrenceId:N}"
            });
        await context.SaveChangesAsync();
        return occurrenceId;
    }

    private static async Task<Guid> VersionIdAsync(LocalDbTestDatabase database, Guid occurrenceId)
    {
        await using var context = await database.CreateContextAsync();
        return await context.Set<DocumentOccurrenceEntity>()
            .Where(item => item.Id == occurrenceId)
            .Select(item => item.VersionId)
            .SingleAsync();
    }

    private static async Task<Guid> SeedCurrentImageAsync(LocalDbTestDatabase database, Guid caseId)
    {
        await using var context = await database.CreateContextAsync();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        // Each seeded image is the Case's next document.
        var ordinal = await context.Set<CaseDocumentEntity>().CountAsync(document => document.CaseId == caseId);
        context.AddRange(
            new CaseDocumentEntity
            {
                Id = documentId,
                CaseId = caseId,
                Ordinal = ordinal,
                SourceOccurrenceIdentity = $"test-image:{occurrenceId:N}"
            },
            new DocumentVersionEntity
            {
                Id = versionId,
                DocumentId = documentId,
                Version = 1,
                FileName = "third-party.jpg",
                MediaType = "image/jpeg",
                ContentLength = 1,
                Sha256 = new string('a', 64),
                CustodyStatus = DocumentCustodyStatus.Confirmed,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "Staff:test",
                IsCurrent = true
            },
            new DocumentOccurrenceEntity
            {
                Id = occurrenceId,
                CaseId = caseId,
                DocumentId = documentId,
                VersionId = versionId,
                SemanticRole = DocumentSemanticRole.Image,
                Source = DocumentSource.StaffUpload,
                SourceOccurrenceIdentity = $"test-image:{occurrenceId:N}",
                RecordedAtUtc = DateTimeOffset.UtcNow,
                OperationKey = $"seed-image:{occurrenceId:N}"
            });
        await context.SaveChangesAsync();
        return occurrenceId;
    }

    private sealed class FailNextDocumentSaveInterceptor : SaveChangesInterceptor
    {
        private int failNextDocumentSave;

        public void FailNextDocumentSave() =>
            Interlocked.Exchange(ref failNextDocumentSave, 1);

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref failNextDocumentSave) == 1
                && eventData.Context is not null
                && eventData.Context.ChangeTracker.Entries<DocumentVersionEntity>()
                    .Any(entry => entry.State == EntityState.Added)
                && Interlocked.Exchange(ref failNextDocumentSave, 0) == 1)
            {
                throw new DbUpdateException("Injected document database failure.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class ManagedOnlyDocumentContentStore(IDocumentContentStore inner)
        : IDocumentContentStore
    {
        public List<ManagedDocumentContentAddress> Addresses { get; } = [];

        public Task StoreAsync(
            Guid caseId,
            string caseReference,
            Guid versionId,
            ReadOnlyMemory<byte> content,
            string expectedSha256,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Managed custody addressing is required.");

        public async Task<DocumentContentWriteResult> StoreVersionAsync(
            ManagedDocumentContentAddress address,
            ReadOnlyMemory<byte> content,
            string expectedSha256,
            CancellationToken cancellationToken)
        {
            Addresses.Add(address);
            return await inner.StoreVersionAsync(
                address,
                content,
                expectedSha256,
                cancellationToken);
        }

        public async Task<DocumentContentWriteResult> StoreVersionAsync(
            ManagedDocumentContentAddress address,
            Stream content,
            long contentLength,
            string expectedSha256,
            CancellationToken cancellationToken)
        {
            Addresses.Add(address);
            return await inner.StoreVersionAsync(
                address,
                content,
                contentLength,
                expectedSha256,
                cancellationToken);
        }

        public Task<Stream> OpenReadAsync(
            Guid caseId,
            string caseReference,
            Guid versionId,
            string expectedSha256,
            long expectedLength,
            CancellationToken cancellationToken) =>
            inner.OpenReadAsync(
                caseId,
                caseReference,
                versionId,
                expectedSha256,
                expectedLength,
                cancellationToken);

        public Task DeleteAsync(
            Guid caseId,
            string caseReference,
            Guid versionId,
            CancellationToken cancellationToken) =>
            inner.DeleteAsync(caseId, caseReference, versionId, cancellationToken);
    }
}
