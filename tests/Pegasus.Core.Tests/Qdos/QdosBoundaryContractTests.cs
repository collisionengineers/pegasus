using Pegasus.Core.Address;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;

namespace Pegasus.Core.Tests.Qdos;

public sealed class QdosBoundaryContractTests
{
    private static readonly DateTimeOffset Now = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void RequestUploadMetadataMayBeOmitted()
    {
        var command = CreateRequestUploadCommand(null, null);

        var normalized = RequestUploadPolicy.NormalizeCreate(command);

        Assert.Equal(command, normalized);
        Assert.Null(normalized.Recipient);
        Assert.Null(normalized.Reason);
    }

    [Fact]
    public void RequestUploadMetadataIsTrimmedWithoutChangingCommandContext()
    {
        var command = CreateRequestUploadCommand("  Workshop contact  ", "  Requested evidence  ");

        var normalized = RequestUploadPolicy.NormalizeCreate(command);

        Assert.Equal(command.CaseId, normalized.CaseId);
        Assert.Same(command.Actor, normalized.Actor);
        Assert.Equal(command.OperationKey, normalized.OperationKey);
        Assert.Equal(command.ExpectedCaseVersion, normalized.ExpectedCaseVersion);
        Assert.Equal(command.EditLeaseToken, normalized.EditLeaseToken);
        Assert.Equal("Workshop contact", normalized.Recipient);
        Assert.Equal("Requested evidence", normalized.Reason);
    }

    [Fact]
    public void RequestUploadMetadataAcceptsItsExactLimits()
    {
        var normalized = RequestUploadPolicy.NormalizeCreate(
            CreateRequestUploadCommand(new string('R', 500), new string('N', 1000)));

        Assert.Equal(500, normalized.Recipient!.Length);
        Assert.Equal(1000, normalized.Reason!.Length);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RequestUploadMetadataRejectsValuesOverTheirLimits(bool recipient)
    {
        var command = recipient
            ? CreateRequestUploadCommand(new string('R', 501), "Reason")
            : CreateRequestUploadCommand("Recipient", new string('N', 1001));

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => RequestUploadPolicy.NormalizeCreate(command));

        Assert.Equal(recipient ? "Recipient" : "Reason", exception.ParamName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RequestUploadMetadataRejectsWhitespaceOnlyValues(bool recipient)
    {
        var command = recipient
            ? CreateRequestUploadCommand(" \t ", "Reason")
            : CreateRequestUploadCommand("Recipient", " \r\n ");

        var exception = Assert.Throws<ArgumentException>(
            () => RequestUploadPolicy.NormalizeCreate(command));

        Assert.Equal(recipient ? "Recipient" : "Reason", exception.ParamName);
    }

    [Fact]
    public void RequestUploadRejectsRevokedLinkBeforeExposingFileDetails()
    {
        var issue = RequestUploadToken.Create();
        var authorization = CreateRequestUploadPolicy().Authorize(
            Link(issue, RequestUploadStatus.Revoked, revokedAtUtc: Now),
            new(issue.Secret.Token, File("case-notes.pdf", "application/pdf", "document"u8.ToArray(), "upload-1"), 0));

        Assert.Equal(RequestUploadDecision.Unavailable, authorization.Decision);
        Assert.Null(authorization.ContentHash);
        Assert.Null(authorization.SafeFileName);
        Assert.False(authorization.MayEnterCustody);
    }

    [Fact]
    public void RequestUploadAllowsOnlyExactSameOperationReplay()
    {
        var issue = RequestUploadToken.Create();
        var link = Link(issue, RequestUploadStatus.Active);
        var first = File("case-notes.pdf", "application/pdf", "document"u8.ToArray(), "upload-1");
        var firstAuthorization = CreateRequestUploadPolicy().Authorize(link, new(issue.Secret.Token, first, 0));
        var replay = CreateRequestUploadPolicy().Authorize(
            link,
            new(issue.Secret.Token, first, 0),
            firstAuthorization.ContentHash);
        var conflict = CreateRequestUploadPolicy().Authorize(
            link,
            new(issue.Secret.Token, File("case-notes.pdf", "application/pdf", "changed"u8.ToArray(), "upload-1"), 0),
            firstAuthorization.ContentHash);

        Assert.Equal(RequestUploadDecision.Accepted, firstAuthorization.Decision);
        Assert.True(firstAuthorization.MayEnterCustody);
        Assert.Equal(RequestUploadDecision.Replay, replay.Decision);
        Assert.True(replay.IsReplay);
        Assert.False(replay.MayEnterCustody);
        Assert.Equal(RequestUploadDecision.OperationConflict, conflict.Decision);
        Assert.Null(conflict.ContentHash);
        Assert.False(conflict.MayEnterCustody);
    }

    [Fact]
    public void InspectionAddressPolicyRejectsTransportMetadataAndRetainsContradictoryContent()
    {
        var transportOnly = Ext18InspectionAddressPolicy.Evaluate(
            new InstructionReviewField[] { Field(
                "Inspection address",
                false,
                new InstructionFieldCandidate[]
                {
                    new InstructionFieldCandidate("A workshop", IntakeEvidenceSource.Sender, "sender"),
                }) },
            QdosInstructionExtractionPolicy.Key,
            QdosInstructionExtractionPolicy.Version);
        var conflicting = Ext18InspectionAddressPolicy.Evaluate(
            [Field(
                "Inspection address",
                true,
                new InstructionFieldCandidate[]
                {
                    new InstructionFieldCandidate("One workshop", IntakeEvidenceSource.EmailBody, "body"),
                    new InstructionFieldCandidate("Two workshop", IntakeEvidenceSource.PdfContent, "attachment"),
                })],
            QdosInstructionExtractionPolicy.Key,
            QdosInstructionExtractionPolicy.Version);

        Assert.True(transportOnly.IsUnresolved);
        Assert.Empty(transportOnly.ConflictingEvidence);
        Assert.True(conflicting.IsUnresolved);
        Assert.Equal(["One workshop", "Two workshop"], conflicting.ConflictingEvidence.Select(item => item.Value));
    }

    [Fact]
    public void TheExportMappingCarriesVersionedProvenanceForEveryField()
    {
        var export = CaseEvaMapping.MapForOperatorExport(
            AcceptedEvaEvidence(),
            new DateOnly(2031, 5, 4));

        Assert.NotNull(export.Source);
        Assert.Equal("AB12CDE", export.Source.Fields.Vrm);
        Assert.Equal(
            $"{CaseEvaMapping.ImageBasedAssessmentExportValue}\n\n\n\n\n",
            export.Source.Fields.InspectionAddress);
        Assert.Equal(13, export.Source.Provenance.Count);
        Assert.All(export.Source.Provenance, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Source));
            Assert.False(string.IsNullOrWhiteSpace(item.SourceVersion));
        });
    }

    [Fact]
    public void ARealInspectionAddressPutsItsPostcodeOnTheSixthLine()
    {
        // ENG-015: the system EVA imports into requires six lines — five body
        // lines then the postcode — and rejects a bare string. The case stores
        // the address as one collapsed line, so commas separate lines here.
        var accepted = AcceptedEvaEvidence();
        var export = CaseEvaMapping.MapForOperatorExport(accepted with
        {
            Inspection = new(
                EvaInspectionMode.PhysicalAddress,
                accepted.Inspection.Evidence with { Value = "109 Valley View, Hoole, CH490DJ" })
        }, new DateOnly(2031, 5, 4));

        Assert.NotNull(export.Source);
        Assert.Equal(
            "109 Valley View\nHoole\n\n\n\nCH490DJ",
            export.Source.Fields.InspectionAddress);
        Assert.Equal(6, export.Source.Fields.InspectionAddress!.Split('\n').Length);
    }

    [Fact]
    public void SurplusInspectionAddressLinesJoinTheFifthRatherThanPushOutThePostcode()
    {
        var accepted = AcceptedEvaEvidence();
        var export = CaseEvaMapping.MapForOperatorExport(accepted with
        {
            Inspection = new(
                EvaInspectionMode.PhysicalAddress,
                accepted.Inspection.Evidence with
                {
                    Value = "One, Two, Three, Four, Five, Six, Seven, CH49 0DJ"
                })
        }, new DateOnly(2031, 5, 4));

        Assert.NotNull(export.Source);
        var lines = export.Source.Fields.InspectionAddress!.Split('\n');
        Assert.Equal(6, lines.Length);
        Assert.Equal("Five Six Seven", lines[4]);
        Assert.Equal("CH49 0DJ", lines[5]);
    }

    [Fact]
    public void AnAddressWithoutAPostcodeLeavesTheSixthLineBlank()
    {
        var accepted = AcceptedEvaEvidence();
        var export = CaseEvaMapping.MapForOperatorExport(accepted with
        {
            Inspection = new(
                EvaInspectionMode.PhysicalAddress,
                accepted.Inspection.Evidence with { Value = "Unit 4, Riverside Depot" })
        }, new DateOnly(2031, 5, 4));

        Assert.NotNull(export.Source);
        Assert.Equal("Unit 4\nRiverside Depot\n\n\n\n", export.Source.Fields.InspectionAddress);
    }

    [Fact]
    public void VatStatusStaysBlankForQdosRatherThanBeingDefaulted()
    {
        // ENG-015, pinned deliberately: QDOS's presence-check config in the
        // original extractor is empty, so this field is blank by design and
        // not by failure. Nothing should "fix" it with a default or a prompt.
        var accepted = AcceptedEvaEvidence();
        var export = CaseEvaMapping.MapForOperatorExport(
            accepted with
            {
                VatStatus = new(null, EvaEvidenceStatus.Unrecorded, "unrecorded", "unrecorded")
            },
            new DateOnly(2031, 5, 4));

        Assert.NotNull(export.Source);
        Assert.Null(export.Source.Fields.VatStatus);
        Assert.Contains("VAT Status", export.UnrecordedFields);
    }

    [Fact]
    public void ASuggestedMileageStillReachesAnOperatorExport()
    {
        // ENG-015, pinned deliberately: Pegasus fills mileage from the DVLA and
        // DVSA lookup (ENG-013) where the original extractor emitted "". That
        // divergence is what the operator asked for; nobody should "restore
        // parity" by dropping it.
        var accepted = AcceptedEvaEvidence();
        var export = CaseEvaMapping.MapForOperatorExport(
            accepted with
            {
                Mileage = new("208602", EvaEvidenceStatus.Suggested, "vehicle-lookup", "mot/v1")
            },
            new DateOnly(2031, 5, 4));

        Assert.NotNull(export.Source);
        Assert.Equal("208602", export.Source.Fields.Mileage);
        Assert.DoesNotContain("Mileage", export.UnrecordedFields);
        var mileage = Assert.Single(export.Source.Provenance, field => field.Name == "Mileage");
        Assert.Equal(EvaEvidenceStatus.Suggested, mileage.Status);
    }

    [Fact]
    public void CompletenessAndSuggestionStatusAreOwnedByReviewRatherThanTheMapper()
    {
        var accepted = AcceptedEvaEvidence();
        var export = CaseEvaMapping.MapForOperatorExport(accepted with
        {
            InstructionComplete = false,
            Inspection = accepted.Inspection with
            {
                Evidence = accepted.Inspection.Evidence with
                {
                    Status = EvaEvidenceStatus.Suggested
                }
            }
        }, new DateOnly(2031, 5, 4));

        Assert.NotNull(export.Source);
        Assert.Equal(EvaEvidenceStatus.Suggested, Assert.Single(
            export.Source.Provenance,
            field => field.Name == "Inspection Address").Status);
    }

    [Fact]
    public async Task SentEmailReplayRejectsNonWorkerActorBeforeAnyEvidenceIsRecorded()
    {
        var recorder = new RecordingSentEvidence();
        var replay = new ReplaySentEmailEvidence(recorder);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => replay.ExecuteAsync(
            new(
                "replay-1",
                Guid.NewGuid(),
                0,
                "message-1",
                "Case material",
                ["recipient@example.test"],
                new string('a', 64),
                Now,
                Now.AddDays(7)),
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User])));

        Assert.Null(recorder.Request);
    }

    private static RequestUploadPolicy CreateRequestUploadPolicy() => new(
        new("test-v1", TimeSpan.FromHours(1), 2, 1024, 2048, ["application/pdf"], 3, TimeSpan.FromMinutes(1)),
        new FixedTimeProvider(Now));

    private static RequestUploadLink Link(
        RequestUploadTokenIssue issue,
        RequestUploadStatus status,
        DateTimeOffset? revokedAtUtc = null) => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            issue.TokenDigest,
            status,
            Now,
            Now.AddHours(1),
            revokedAtUtc,
            0,
            0,
            "test-v1",
            0);

    private static RequestUploadFile File(string name, string mediaType, byte[] content, string operationKey) =>
        new(name, mediaType, content, operationKey);

    private static InstructionReviewField Field(
        string name,
        bool hasConflict,
        params InstructionFieldCandidate[] candidates)
    {
        var suggestedCandidate = candidates.FirstOrDefault();
        Assert.NotNull(suggestedCandidate);

        return new(name, suggestedCandidate.Value, candidates, false, hasConflict);
    }

    private static EvaAcceptedCaseEvidence AcceptedEvaEvidence()
    {
        var accepted = new EvaEvidenceValue("confirmed", EvaEvidenceStatus.Accepted, "staff", "1");
        return new(
            Guid.NewGuid(),
            1,
            true,
            true,
            true,
            accepted with { Value = "QDOS-001", Source = "case", SourceVersion = "1" },
            accepted,
            accepted with { Value = "AB12 CDE" },
            accepted with { Value = "Model" },
            accepted with { Value = "Claimant" },
            accepted with { Value = "2031-05-01" },
            accepted with { Value = "2031-05-02" },
            accepted with { Value = "2031-05-03" },
            new(
                EvaInspectionMode.ImageBasedAssessment,
                accepted with { Value = CaseEvaMapping.ImageBasedAssessment }),
            accepted with { Value = "Impact" },
            accepted with { Value = "VAT registered" },
            accepted with { Value = "12000" },
            accepted with { Value = "miles" });
    }

    private static CreateRequestUploadLinkCommand CreateRequestUploadCommand(
        string? recipient,
        string? reason) => new(
            Guid.NewGuid(),
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
            "request-upload:create",
            17,
            "lease-token",
            recipient,
            reason);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingSentEvidence : IRecordSentEmailEvidence
    {
        public RecordSentEmailEvidenceRequest? Request { get; private set; }

        public Task<SentEmailEvidence> ExecuteAsync(
            RecordSentEmailEvidenceRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            throw new InvalidOperationException("The caller should have been rejected before recording evidence.");
        }
    }
}
