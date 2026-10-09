using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

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
        CaseListFacts.ClaimData | CaseListFacts.Assessment | CaseListFacts.ReportFigures | CaseListFacts.Sends | CaseListFacts.Activity,
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
            auditWork = Guid.NewGuid();
            context.CaseWorks.Add(new CaseWorkEntity { Id = auditWork, CaseId = inspectionAndAudit, Kind = CaseWorkKinds.Audit, CreatedAtUtc = Received.AddDays(2) });

            // The origin receipt is the received date, not the Case's creation.
            context.CaseDataSnapshots.Add(new CaseDataSnapshotEntity
            {
                WorkId = inspection,
                OriginReceivedAtUtc = Received,
                CompletenessPolicyKey = "test",
                CompletenessPolicyVersion = 1,
                AcceptedAtUtc = Received,
                Fields =
                [
                    new CaseDataFieldEntity
                    {
                        WorkId = inspection,
                        FieldName = CaseDataFieldNames.VehicleRegistration,
                        ValueKind = "fact",
                        ValueType = "text",
                        Value = "AB12 CDE",
                        SourceKind = "intake_evidence",
                        SourceIdentity = "test",
                        SourceLabel = "test",
                        PolicyKey = "test",
                        PolicyVersion = 1
                    }
                ]
            });
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
                new CaseTaskEntity { Id = Guid.NewGuid(), CaseId = inspection, Description = "Chase images", State = "Open", Version = 0, ConcurrencyToken = Guid.NewGuid() },
                new CaseTaskEntity { Id = Guid.NewGuid(), CaseId = inspection, Description = "Done", State = "Completed", Version = 0, ConcurrencyToken = Guid.NewGuid() },
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
        Assert.Equal("AB12 CDE", inspectionRecord.ClaimData[CaseDataFieldNames.VehicleRegistration]);
        Assert.Equal("repairable", inspectionRecord.Primary.Assessment[AssessmentVocabulary.Outcome]);
        // The fee is the first report's; the repair cost and signatory the latest's.
        Assert.Equal(180.00m, inspectionRecord.Primary.FirstReportAgreedFee);
        Assert.Equal(1234.56m, inspectionRecord.Primary.LatestReportRepairCost);
        Assert.Null(inspectionRecord.Primary.LatestReportSignatoryId);
        Assert.Equal(Received.AddDays(2), inspectionRecord.Primary.FirstSentAtUtc);
        Assert.Equal(ActorKind.Staff, inspectionRecord.Primary.FirstSentByKind);
        Assert.Equal(sender.ToString("D"), inspectionRecord.Primary.FirstSentBySubjectId);
        Assert.Null(inspectionRecord.AuditWork);
        Assert.Equal(new CaseListActivity(2, 1, 1, 0, 0, 2, 0, 1, 1), inspectionRecord.Activity);

        var auditRecord = all.Single(record => record.CaseId == audit);
        Assert.Equal("Smith Assessors", auditRecord.Primary.Assessment[AssessmentVocabulary.OriginalReportAssessor]);
        Assert.Null(auditRecord.Primary.FirstReportAgreedFee);

        var bothRecord = all.Single(record => record.CaseId == inspectionAndAudit);
        Assert.Equal("a.QDOS31003", bothRecord.AuditReference);
        Assert.NotNull(bothRecord.AuditWork);
        Assert.Equal(95.00m, bothRecord.AuditWork!.FirstReportAgreedFee);
        Assert.Equal(Received.AddDays(6), bothRecord.AuditWork.FirstSentAtUtc);
        Assert.Null(bothRecord.Primary.FirstReportAgreedFee);

        var withTriage = await queries.GetAsync(Everything with { IncludeTriage = true }, CancellationToken.None);
        Assert.Contains(withTriage, record => record.CaseId == triage && record.TriageState is not null && record.State is null);

        var june = await queries.GetAsync(
            Everything with { ReceivedFrom = new DateOnly(2031, 6, 1), ReceivedTo = new DateOnly(2031, 6, 10) },
            CancellationToken.None);
        Assert.Equal(new[] { inspection, audit, inspectionAndAudit }.Order(), june.Select(record => record.CaseId).Order());
        Assert.DoesNotContain(june, record => record.CaseId == outside);
        Assert.Equal(3, june.Count);
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

        var created = await store.SaveAsync(new(id, "Invoicing", ["case.reference", "agreed_fee.inspection"], 0, actor, createKey), CancellationToken.None);
        var replayed = await store.SaveAsync(new(id, "Invoicing", ["case.reference", "agreed_fee.inspection"], 0, actor, createKey), CancellationToken.None);

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

    private static StaffMailSendOperationEntity Send(Guid generation, Guid sender, DateTimeOffset sentAtUtc)
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
