using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Classification;

public sealed class MailCategoryTests
{
    [Fact]
    public void ExactlyTheFourCategoriesExist()
    {
        string[] expected = ["new-instruction", "triage-request", "cancellation", "pre-pegasus-case"];
        Assert.Equal(expected, MailCategories.All.Select(MailCategories.Code).ToArray());
    }

    /// <summary>
    /// Pre-Pegasus case (operator, 30 October 2026, R561) is a classified
    /// category with no work type, set only by correction.
    /// </summary>
    [Fact]
    public void PrePegasusCaseIsAClassifiedCategoryWithNoCaseType()
    {
        var result = MailClassificationResult.Classified(
            MailCategory.PrePegasusCase, [], "reason", "policy", 1);

        Assert.True(result.IsClassified);
        Assert.True(result.Is(MailCategory.PrePegasusCase));
        Assert.Null(result.CaseType);
        Assert.Equal("pre-pegasus-case", MailCategories.Code(MailCategory.PrePegasusCase));
        Assert.True(MailCategories.TryParse("pre-pegasus-case", out var parsed));
        Assert.Equal(MailCategory.PrePegasusCase, parsed);
    }

    [Fact]
    public void EveryCodeRoundTrips()
    {
        foreach (var category in MailCategories.All)
        {
            var code = MailCategories.Code(category);
            Assert.Equal(category, MailCategories.Parse(code));
            Assert.True(MailCategories.TryParse(code, out var parsed));
            Assert.Equal(category, parsed);
        }
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("post-report-emails")]
    [InlineData("query")]
    [InlineData("case-update")]
    [InlineData("pre-instruction")]
    [InlineData("images-received")]
    [InlineData("billing")]
    [InlineData("general")]
    [InlineData("not-client-related")]
    [InlineData("internal-cc")]
    [InlineData("Query")]
    [InlineData("")]
    [InlineData(null)]
    public void UnknownCodeDoesNotParse(string? code)
    {
        Assert.False(MailCategories.TryParse(code, out _));
    }

    [Fact]
    public void ReplyContextLivesOnTheResult()
    {
        var reply = MailClassificationResult.Classified(
            MailCategory.NewInstruction,
            [],
            "reason",
            "policy",
            1,
            isReplyContext: true);

        Assert.True(reply.IsClassified);
        Assert.True(reply.Is(MailCategory.NewInstruction));
        Assert.True(reply.IsReplyContext);
    }

    [Fact]
    public void UnclassifiedHasNoCategory()
    {
        var result = MailClassificationResult.Unclassified([], "reason", "policy", 1);

        Assert.False(result.IsClassified);
        Assert.Null(result.Category);
        Assert.False(result.Is(MailCategory.Cancellation));
    }

    [Fact]
    public void ResultCarriesNoQueueTriageRoutingOrFolderDestination()
    {
        string[] separatedConcerns = ["queue", "folder", "destination", "triageroute", "outlook"];
        foreach (var property in typeof(MailClassificationResult).GetProperties())
        {
            Assert.DoesNotContain(
                separatedConcerns,
                concern => property.Name.Contains(concern, StringComparison.OrdinalIgnoreCase));
        }
    }
}
