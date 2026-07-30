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
    public void EvaProductionMappingUsesOnlyAcceptedVersionedEvidence()
    {
        var mapping = CaseEvaMapping.MapForProduction(
            AcceptedEvaEvidence(),
            AcceptedEvaMapping());

        Assert.True(mapping.IsReady);
        Assert.NotNull(mapping.Fields);
        Assert.Equal("AB12CDE", mapping.Fields.Vrm);
        Assert.Equal(CaseEvaMapping.ImageBasedAssessment, mapping.Fields.InspectionAddress);
        Assert.Equal(13, mapping.Provenance.Count);
        Assert.All(mapping.Provenance, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Source));
            Assert.False(string.IsNullOrWhiteSpace(item.SourceVersion));
        });
    }

    [Fact]
    public void EvaProductionMappingFailsClosedWithoutAcceptedMappingVersion()
    {
        var mapping = CaseEvaMapping.MapForProduction(
            AcceptedEvaEvidence(),
            EvaMappingAcceptance.Unaccepted);

        Assert.False(mapping.IsReady);
        Assert.Null(mapping.Source);
        Assert.Equal(CaseEvaMapping.ActivationGateReason, mapping.BlockingReasons[0]);
    }

    [Fact]
    public void EvaProductionMappingBlocksMissingReadinessAndUnacceptedAddress()
    {
        var accepted = AcceptedEvaEvidence();
        var mapping = CaseEvaMapping.MapForProduction(accepted with
        {
            InstructionComplete = false,
            Inspection = accepted.Inspection with
            {
                Evidence = accepted.Inspection.Evidence with
                {
                    Status = EvaEvidenceStatus.Suggested
                }
            }
        }, AcceptedEvaMapping());

        Assert.False(mapping.IsReady);
        Assert.Null(mapping.Source);
        Assert.Contains(
            "Instruction and image completeness must both be confirmed.",
            mapping.BlockingReasons);
        Assert.Contains(
            "The inspection address or exact Image Based Assessment mode is unresolved.",
            mapping.BlockingReasons);
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

    private static EvaMappingAcceptance AcceptedEvaMapping() => new(
        CaseEvaMapping.MappingKey,
        CaseEvaMapping.MappingVersion,
        "accepted-evidence:test");

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
