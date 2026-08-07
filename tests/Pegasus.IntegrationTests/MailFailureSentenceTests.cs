using Pegasus.Core.Intake;
using Pegasus.Web.Pages.Intake;

namespace Pegasus.IntegrationTests;

/// <summary>
/// What the Failed view tells an operator about a message that did not arrive.
/// </summary>
/// <remarks>
/// The 2026-08-05 QDOS forward was refused, retained, and listed here — and
/// the row said only "The last message from this mailbox could not be
/// processed." That sentence is about the mailbox, not about the message, and
/// it named neither the reason nor the size. Nobody could tell from the screen
/// what had happened, which is why the failure had to be diagnosed from the
/// database.
/// </remarks>
public sealed class MailFailureSentenceTests
{
    [Fact]
    public void ARefusedMessageNamesTheLimitAndItsOwnSize()
    {
        var sentence = IndexModel.MailFailureSentence("message_too_large", 17_496_501);

        Assert.Contains("larger than", sentence, StringComparison.Ordinal);
        Assert.Contains("16.7 MB", sentence, StringComparison.Ordinal);
        Assert.DoesNotContain("last message from this mailbox", sentence, StringComparison.Ordinal);
    }

    [Fact]
    public void ARefusedMessageIsNeverDescribedAsTheMailboxsLastMessage()
    {
        string[] refusalCodes =
        [
            "message_too_large",
            "empty_message",
            "missing_message_identity",
            "message_identity_too_long",
            "missing_message_file_name",
            "invalid_message_file_name",
            "message_file_name_too_long",
            "immutable_source_changed",
            "immutable_source_missing",
            "source_identity_conflict",
            "artifact_retention_failure"
        ];

        foreach (var code in refusalCodes)
        {
            var sentence = IndexModel.MailFailureSentence(code);
            Assert.DoesNotContain(
                "last message from this mailbox",
                sentence,
                StringComparison.Ordinal);
            Assert.EndsWith(".", sentence, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void APollingFailureStillReadsAsOneAboutTheMailbox()
    {
        Assert.Equal(
            "The last message from this mailbox could not be processed.",
            IndexModel.MailFailureSentence("mailbox_poll_failure"));
    }

    [Fact]
    public void TheSentenceQuotesTheShippedMailboxLimit()
    {
        var sentence = IndexModel.MailFailureSentence(
            "message_too_large",
            IntakeEnvelopeLimits.MaximumMailboxContentLength + 1);

        Assert.Contains("750.0 MB", sentence, StringComparison.Ordinal);
    }
}
