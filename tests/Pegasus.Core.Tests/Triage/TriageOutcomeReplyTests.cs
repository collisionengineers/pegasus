using Pegasus.Core.Operations;
using Pegasus.Core.Triage;

namespace Pegasus.Core.Tests.Triage;

/// <summary>
/// Reply with outcome opens with the Triage outcome reply template rendered
/// from the registration and the current finding. A dimension that was not
/// recorded has no value, so its line is left out.
/// </summary>
public sealed class TriageOutcomeReplyTests
{
    private static readonly string DefaultBody =
        EmailTemplates.DefaultBody(EmailTemplatePurpose.TriageOutcomeReply);

    [Fact]
    public void TheDefaultBodyRendersTheRegistrationAndBothDimensions()
    {
        var body = EmailTemplates.Render(
            DefaultBody,
            new TriageOutcomeReply(
                "AB12CDE",
                Finding(RoadworthinessFinding.Unroadworthy, AssessmentFinding.TotalLoss)).Values());

        Assert.Equal(
            "Thank you for your triage request for AB12CDE.\n"
            + "\n"
            + "Roadworthiness: Unroadworthy\n"
            + "Repair outcome: Total loss\n"
            + "\n"
            + "Kind regards\n"
            + "Collision Engineers",
            body);
    }

    [Fact]
    public void EachValueComesFromTheTriage()
    {
        var values = new TriageOutcomeReply(
            "XY34ZZZ",
            Finding(RoadworthinessFinding.Roadworthy, AssessmentFinding.Repairable, "Minor panel damage.")).Values();

        Assert.Equal("XY34ZZZ", values[EmailTemplates.Registration]);
        Assert.Equal("Roadworthy", values[EmailTemplates.Roadworthiness]);
        Assert.Equal("Repairable", values[EmailTemplates.RepairOutcome]);
        Assert.Equal("Minor panel damage.", values[EmailTemplates.FindingReason]);
    }

    [Fact]
    public void AnUnrecordedDimensionLeavesItsLineOut()
    {
        var body = EmailTemplates.Render(
            DefaultBody,
            new TriageOutcomeReply("AB12CDE", Finding(RoadworthinessFinding.Roadworthy, assessment: null)).Values());

        Assert.Contains("Roadworthiness: Roadworthy", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Repair outcome", body, StringComparison.Ordinal);
        // Lines with no placeholder are kept, blank lines included.
        Assert.Contains("\n\nKind regards\n", body, StringComparison.Ordinal);
    }

    [Fact]
    public void NoFindingLeavesBothDimensionLinesOut()
    {
        var body = EmailTemplates.Render(DefaultBody, new TriageOutcomeReply("AB12CDE", Finding: null).Values());

        Assert.DoesNotContain("Roadworthiness", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Repair outcome", body, StringComparison.Ordinal);
        Assert.StartsWith("Thank you for your triage request for AB12CDE.", body, StringComparison.Ordinal);
    }

    private static TriageFinding Finding(
        RoadworthinessFinding? roadworthiness,
        AssessmentFinding? assessment,
        string reason = "Reviewed the request images.") => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        roadworthiness,
        assessment,
        null,
        "staff",
        "finding-op",
        reason,
        DateTimeOffset.UnixEpoch);
}
