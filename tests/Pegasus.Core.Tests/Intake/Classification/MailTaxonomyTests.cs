using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Classification;

public sealed class MailTaxonomyTests
{
    [Fact]
    public void ExactlyTheEightSettledReceivedFamiliesExist()
    {
        string[] expected =
        [
            "General",
            "billing",
            "new-instruction-received",
            "non-client-related",
            "in-progress-cases",
            "post-report-emails",
            "pre-instruction-emails",
            "internal-cc"
        ];
        Assert.Equal(
            expected,
            Enum.GetValues<ReceivedMailFamily>().Select(MailTaxonomy.CategoryName).ToArray());
    }

    [Theory]
    [InlineData(ReceivedMailFamily.General, new[] { "autoreply", "undeliverable", "acknowledgement", "general-chase", "case-summary" })]
    [InlineData(ReceivedMailFamily.Billing, new[] { "payment-notification", "remittance", "invoice-request", "billing-query", "general-billing" })]
    [InlineData(ReceivedMailFamily.NewInstructionReceived, new[] { "audit", "diminution", "inspection", "new-client", "website-enquiry" })]
    [InlineData(ReceivedMailFamily.NonClientRelated, new string[0])]
    [InlineData(ReceivedMailFamily.InProgressCases, new[] { "cancellation", "case-update", "chasing-for-update", "ongoing-correspondence" })]
    [InlineData(ReceivedMailFamily.PostReportEmails, new[] { "query", "dispute", "amendment-request" })]
    [InlineData(ReceivedMailFamily.PreInstructionEmails, new[] { "triage-request", "pre-formal-instruction-request", "images-received" })]
    [InlineData(ReceivedMailFamily.InternalCc, new string[0])]
    public void ConfirmedSubtypesMatchTheSettledTables(ReceivedMailFamily family, string[] expected)
    {
        Assert.Equal(expected, MailTaxonomy.ConfirmedReceivedSubtypes[family].ToArray());
    }

    [Fact]
    public void EveryReceivedFamilyHasAConfirmedSubtypeEntry()
    {
        Assert.Equal(
            Enum.GetValues<ReceivedMailFamily>().Length,
            MailTaxonomy.ConfirmedReceivedSubtypes.Count);
    }

    [Fact]
    public void UnconfirmedSubtypeIsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            MailCategory.Received(ReceivedMailFamily.General, "billing-query"));
        Assert.Throws<ArgumentException>(() =>
            MailCategory.Received(ReceivedMailFamily.PostReportEmails, "instruction"));
    }

    [Fact]
    public void ReplyMirrorsTheUnderlyingCategory()
    {
        var receivedReply = MailCategory.Received(
            ReceivedMailFamily.NewInstructionReceived,
            "audit",
            isReplyContext: true);
        Assert.Equal(ReceivedMailFamily.NewInstructionReceived, receivedReply.ReceivedFamily);
        Assert.Equal("audit", receivedReply.Subtype);
        Assert.True(receivedReply.IsReplyContext);
        Assert.Equal("new-instruction-received", receivedReply.Name);
    }

    [Fact]
    public void CategoryCarriesNoQueueTriageRoutingOrFolderDestination()
    {
        string[] separatedConcerns = ["queue", "folder", "destination", "triageroute", "outlook"];
        foreach (var type in new[] { typeof(MailCategory), typeof(MailClassificationResult) })
        {
            foreach (var property in type.GetProperties())
            {
                Assert.DoesNotContain(
                    separatedConcerns,
                    concern => property.Name.Contains(concern, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
