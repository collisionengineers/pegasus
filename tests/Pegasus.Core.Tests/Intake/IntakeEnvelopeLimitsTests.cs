using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// The two intake size bounds are different facts and must not converge.
/// </summary>
public sealed class IntakeEnvelopeLimitsTests
{
    /// <summary>
    /// The byte length of the QDOS instruction forwarded to the instructions
    /// mailbox on 2026-08-05, which was refused as <c>message_too_large</c>,
    /// quarantined, and never read.
    /// </summary>
    private const long RefusedQdosForwardLength = 17_496_501;

    [Fact]
    public void AReceivedInstructionIsNotBoundedByTheOneFileUploadLimit()
    {
        Assert.True(
            IntakeEnvelopeLimits.MaximumMailboxContentLength
                > IntakeEnvelopeLimits.MaximumContentLength,
            "A mailbox message carries a covering message plus every document "
                + "and photograph of the job, so it cannot share the bound that "
                + "exists for one uploaded file.");
    }

    [Fact]
    public void TheMailboxBoundAdmitsTheInstructionThatWasRefused()
    {
        Assert.True(
            RefusedQdosForwardLength > IntakeEnvelopeLimits.MaximumContentLength,
            "The refused instruction must still be larger than the upload "
                + "bound, or this test no longer describes the incident.");
        Assert.True(
            RefusedQdosForwardLength <= IntakeEnvelopeLimits.MaximumMailboxContentLength,
            "The instruction refused on 2026-08-05 must now be admitted for "
                + "reading and decided on its content.");
    }

    [Fact]
    public void TheUploadFormKeepsItsOwnBound()
    {
        Assert.Equal(10 * 1024 * 1024, IntakeEnvelopeLimits.MaximumContentLength);
    }
}
