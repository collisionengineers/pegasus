using Pegasus.Core.Triage;

namespace Pegasus.Core.Tests.Triage;

/// <summary>
/// Reply with outcome opens with the built-in body rendered from the
/// registration and the current finding. A placeholder with no value renders
/// nothing, and a line whose placeholders are all empty is left out.
/// </summary>
public sealed class TriageOutcomeReplyTests
{
    [Fact]
    public void TheDefaultBodyRendersTheRegistrationAndBothDimensions()
    {
        var body = TriageOutcomeReply.Render(
            "AB12CDE",
            Finding(RoadworthinessFinding.Unroadworthy, AssessmentFinding.TotalLoss));

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
    public void EachPlaceholderRendersItsValue()
    {
        var values = TriageOutcomeReply.Values(
            "XY34ZZZ",
            Finding(RoadworthinessFinding.Roadworthy, AssessmentFinding.Repairable, "Minor panel damage."));

        var rendered = TriageOutcomeReply.Render(
            "{registration}|{roadworthiness}|{repair outcome}|{reason}",
            values);

        Assert.Equal("XY34ZZZ|Roadworthy|Repairable|Minor panel damage.", rendered);
    }

    [Fact]
    public void AnUnrecordedDimensionLeavesItsLineOut()
    {
        var body = TriageOutcomeReply.Render(
            "AB12CDE",
            Finding(RoadworthinessFinding.Roadworthy, assessment: null));

        Assert.Contains("Roadworthiness: Roadworthy", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Repair outcome", body, StringComparison.Ordinal);
        // Lines with no placeholder are kept, blank lines included.
        Assert.Contains("\n\nKind regards\n", body, StringComparison.Ordinal);
    }

    [Fact]
    public void NoFindingLeavesBothDimensionLinesOut()
    {
        var body = TriageOutcomeReply.Render("AB12CDE", finding: null);

        Assert.DoesNotContain("Roadworthiness", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Repair outcome", body, StringComparison.Ordinal);
        Assert.StartsWith("Thank you for your triage request for AB12CDE.", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ALineWithOneFilledAndOneEmptyPlaceholderKeepsTheFilledOne()
    {
        var values = TriageOutcomeReply.Values(
            "AB12CDE",
            Finding(roadworthiness: null, AssessmentFinding.TotalLoss));

        var rendered = TriageOutcomeReply.Render(
            "Outcome: {roadworthiness}{repair outcome}",
            values);

        Assert.Equal("Outcome: Total loss", rendered);
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
