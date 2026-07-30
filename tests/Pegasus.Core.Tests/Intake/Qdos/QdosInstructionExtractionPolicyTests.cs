using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Qdos;

public sealed class QdosInstructionExtractionPolicyTests
{
    private static readonly DateTimeOffset ProcessedAtUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ReadableNonQdosContentIsNotApplicableAndHasNoDraftOrSuggestion()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(content: "General correspondence without an instruction marker."),
            ProcessedAtUtc);

        Assert.Equal(InstructionPolicyApplicability.NotApplicable, result.Applicability);
        Assert.Null(result.InstructionDraft);
        Assert.Empty(result.Fields);
    }

    [Theory]
    [InlineData(IntakeEvidenceSource.FileName)]
    [InlineData(IntakeEvidenceSource.Sender)]
    public void QdosTransportMetadataAloneIsIndeterminateAndHasNoPrincipalSuggestion(
        IntakeEvidenceSource source)
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(transport: [new(source, "forwarded-qdos@example.test")]),
            ProcessedAtUtc);

        Assert.Equal(InstructionPolicyApplicability.Indeterminate, result.Applicability);
        Assert.Null(result.InstructionDraft);
        Assert.Contains(result.Evidence, item =>
            item.Strength == IntakeEvidenceStrength.Weak
            && item.Finding == IntakeEvidenceFinding.SupportsPrincipal);
    }

    [Fact]
    public void StrongQdosContentFromStaffForwardingSenderIsApplicableAndPreservesContradiction()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(
                "QDOS instruction\nClaimant Name: Review Claimant\nClaim Number: Q-1",
                [new(IntakeEvidenceSource.Sender, "staff@collision-engineers.test")]),
            ProcessedAtUtc);

        Assert.Equal(InstructionPolicyApplicability.Applicable, result.Applicability);
        Assert.Equal("QDOS", Assert.IsType<InstructionDraft>(result.InstructionDraft).SuggestedPrincipalCode);
        Assert.Contains(result.Evidence, item =>
            item.Finding == IntakeEvidenceFinding.ContradictsTransport
            && item.Signal == "forwarded-sender");
    }
    [Theory]
    [InlineData("QDOS instruction")]
    [InlineData("QDOS instruction\nClaimant Name: Review Claimant")]
    public void QdosMarkerWithoutTwoInstructionLabelsCannotProduceDraft(string content)
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            Readable(content),
            ProcessedAtUtc);

        Assert.Equal(InstructionPolicyApplicability.Indeterminate, result.Applicability);
        Assert.Null(result.InstructionDraft);
        Assert.Empty(result.Fields);
        Assert.Empty(result.MissingFields);
    }

    [Fact]
    public void ProofCannotBeAssembledAcrossSeparateContentFragments()
    {
        var result = new QdosInstructionExtractionPolicy().Extract(
            new(
                IntakeSourceReadStatus.Readable,
                [
                    new(
                        IntakeEvidenceSource.EmailBody,
                        "untrusted message body",
                        "QDOS instruction\nClaimant Name: Review Claimant"),
                    new(
                        IntakeEvidenceSource.DocumentContent,
                        "separate attachment",
                        "QDOS instruction\nClaim Number: Q-423")
                ],
                [],
                [],
                false),
            ProcessedAtUtc);

        Assert.Equal(InstructionPolicyApplicability.Indeterminate, result.Applicability);
        Assert.Null(result.InstructionDraft);
        Assert.Empty(result.Fields);
        Assert.Empty(result.MissingFields);
    }


    [Fact]
    public void IncompleteResultCannotCrossPolicyBoundary()
    {
        var readResult = Readable("QDOS instruction\nClaimant Name: A\nClaim Number: B") with
        {
            IsIncomplete = true
        };

        Assert.Throws<ArgumentException>(() =>
            new QdosInstructionExtractionPolicy().Extract(readResult, ProcessedAtUtc));
    }

    private static IntakeSourceReadResult Readable(
        string? content = null,
        IReadOnlyList<IntakeTransportEvidence>? transport = null) =>
        new(
            IntakeSourceReadStatus.Readable,
            content is null
                ? []
                : [new(IntakeEvidenceSource.DocumentContent, "controlled policy fixture", content)],
            transport ?? [],
            [],
            false);
}
