using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// MAIL-008: every mail category renders through the one operator label map —
/// no category code, no fallback — in the wording the
/// operator ruled (12 October 2026).
/// </summary>
public sealed class MailClassificationLabelTests
{
    [Fact]
    public void EveryCategoryHasAnOperatorLabel()
    {
        foreach (var category in MailCategories.All)
        {
            var label = OperatorLabels.MailClassification(category);
            Assert.False(string.IsNullOrWhiteSpace(label));
            Assert.NotEqual(MailCategories.Code(category), label);
            Assert.True(char.IsUpper(label[0]), $"'{label}' does not start with a capital.");
        }
    }

    [Theory]
    [InlineData("new-instruction", "New instruction")]
    [InlineData("triage-request", "Triage request")]
    [InlineData("cancellation", "Cancellation")]
    [InlineData("pre-pegasus-case", "Pre-Pegasus case")]
    public void LabelsUseTheRuledWording(string code, string label) =>
        Assert.Equal(label, OperatorLabels.MailClassification(MailCategories.Parse(code)));

    [Fact]
    public void CorrespondenceIsNotACategoryLabel()
    {
        Assert.Equal(OperatorLabels.Inbox.Correspondence, "Correspondence");
        Assert.Equal(4, MailCategories.All.Count);
    }
}
