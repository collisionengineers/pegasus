using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Classification;

public sealed class MailCategoryTests
{
    [Fact]
    public void ExactlyTheElevenCategoriesExist()
    {
        string[] expected =
        [
            "new-instruction",
            "triage-request",
            "pre-instruction",
            "images-received",
            "case-update",
            "cancellation",
            "query",
            "billing",
            "general",
            "not-client-related",
            "internal-cc"
        ];
        Assert.Equal(expected, MailCategories.All.Select(MailCategories.Code).ToArray());
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
        Assert.False(result.Is(MailCategory.General));
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
