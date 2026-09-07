using Pegasus.Core.Documents;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// The intake size bounds are separate facts and must not converge, and the
/// four channel limits C07 item 5 (residual INTK-052) set are exact values
/// rather than arithmetic anyone may re-derive.
/// </summary>
public sealed class IntakeEnvelopeLimitsTests
{
    /// <summary>
    /// The byte length of the QDOS instruction forwarded to the instructions
    /// mailbox on 2026-08-05, which was refused as <c>message_too_large</c>,
    /// quarantined, and never read.
    /// </summary>
    private const long RefusedQdosForwardLength = 17_496_501;

    /// <summary>
    /// The one-file upload bound in force on 2026-08-05, when the mailbox
    /// envelope was still bounded by it. C07 item 5 raised the per-file cap,
    /// so the incident is recorded against the bound that actually refused it
    /// rather than against a constant that has since moved.
    /// </summary>
    private const long OneFileBoundAtTheRefusal = 10L * 1024 * 1024;

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
            RefusedQdosForwardLength > OneFileBoundAtTheRefusal,
            "The refused instruction must still be larger than the bound in "
                + "force at the refusal, or this test no longer describes the "
                + "incident.");
        Assert.True(
            RefusedQdosForwardLength <= IntakeEnvelopeLimits.MaximumMailboxContentLength,
            "The instruction refused on 2026-08-05 must now be admitted for "
                + "reading and decided on its content.");
    }

    [Fact]
    public void TheFourChannelLimitsAreTheExactValuesTheyWereSetTo()
    {
        Assert.Equal(104_857_600, IntakeEnvelopeLimits.MaximumContentLength);
        Assert.Equal(209_715_200L + 65_536L, IntakeEnvelopeLimits.MaximumBatchContentLength);
        Assert.Equal(20, IntakeEnvelopeLimits.MaximumBatchFileCount);
        Assert.Equal(31_457_280, IntakeEnvelopeLimits.MaximumProviderApiEnvelopeLength);
        Assert.Equal(209_715_200L, IntakeEnvelopeLimits.MaximumPublicAggregateContentLength);
    }

    [Fact]
    public void TheBatchBudgetIsPinnedRatherThanDerivedFromTheFileCount()
    {
        Assert.Equal(65_536L, IntakeEnvelopeLimits.MultipartOverhead);
        Assert.Equal(
            (200L * 1024 * 1024) + IntakeEnvelopeLimits.MultipartOverhead,
            IntakeEnvelopeLimits.MaximumBatchContentLength);
        Assert.True(
            IntakeEnvelopeLimits.MaximumBatchContentLength
                < IntakeEnvelopeLimits.MaximumBatchFileCount
                    * (long)IntakeEnvelopeLimits.MaximumContentLength,
            "The multipart body budget is a pinned figure, not the file count "
                + "times the per-file cap: deriving it would grant one request "
                + "far more body than the Web instance can hold.");
    }

    [Fact]
    public void ThePublicAggregateBudgetExcludesMultipartOverhead()
    {
        Assert.Equal(209_715_200L, IntakeEnvelopeLimits.MaximumPublicAggregateContentLength);
        Assert.Equal(
            IntakeEnvelopeLimits.MaximumBatchContentLength - IntakeEnvelopeLimits.MultipartOverhead,
            IntakeEnvelopeLimits.MaximumPublicAggregateContentLength);
    }

    [Fact]
    public void RequestUploadLimitsEnforcesCoreLimitsAndRejectsOneOver()
    {
        var exact = new RequestUploadLimits(
            "v1",
            TimeSpan.FromDays(7),
            IntakeEnvelopeLimits.MaximumBatchFileCount,
            IntakeEnvelopeLimits.MaximumContentLength,
            IntakeEnvelopeLimits.MaximumPublicAggregateContentLength,
            ["image/jpeg"],
            10,
            TimeSpan.FromMinutes(1));
        Assert.Equal(IntakeEnvelopeLimits.MaximumBatchFileCount, exact.MaximumFileCount);
        Assert.Equal(IntakeEnvelopeLimits.MaximumContentLength, exact.MaximumFileBytes);
        Assert.Equal(IntakeEnvelopeLimits.MaximumPublicAggregateContentLength, exact.MaximumRequestBytes);

        var lower = new RequestUploadLimits(
            "v1",
            TimeSpan.FromDays(7),
            10,
            10L * 1024 * 1024,
            20L * 1024 * 1024,
            ["image/jpeg"],
            10,
            TimeSpan.FromMinutes(1));
        Assert.Equal(10, lower.MaximumFileCount);
        Assert.Equal(10L * 1024 * 1024, lower.MaximumFileBytes);
        Assert.Equal(20L * 1024 * 1024, lower.MaximumRequestBytes);

        Assert.Throws<ArgumentOutOfRangeException>(() => new RequestUploadLimits(
            "v1",
            TimeSpan.FromDays(7),
            IntakeEnvelopeLimits.MaximumBatchFileCount + 1,
            IntakeEnvelopeLimits.MaximumContentLength,
            IntakeEnvelopeLimits.MaximumPublicAggregateContentLength,
            ["image/jpeg"],
            10,
            TimeSpan.FromMinutes(1)));

        Assert.Throws<ArgumentOutOfRangeException>(() => new RequestUploadLimits(
            "v1",
            TimeSpan.FromDays(7),
            IntakeEnvelopeLimits.MaximumBatchFileCount,
            IntakeEnvelopeLimits.MaximumContentLength + 1,
            IntakeEnvelopeLimits.MaximumPublicAggregateContentLength,
            ["image/jpeg"],
            10,
            TimeSpan.FromMinutes(1)));

        Assert.Throws<ArgumentOutOfRangeException>(() => new RequestUploadLimits(
            "v1",
            TimeSpan.FromDays(7),
            IntakeEnvelopeLimits.MaximumBatchFileCount,
            IntakeEnvelopeLimits.MaximumContentLength,
            IntakeEnvelopeLimits.MaximumPublicAggregateContentLength + 1,
            ["image/jpeg"],
            10,
            TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void AProviderApiFileIsNeverAllowedPastTheEnvelopeThatCarriesIt()
    {
        Assert.True(
            IntakeEnvelopeLimits.MaximumProviderApiFileLength
                <= IntakeEnvelopeLimits.MaximumProviderApiEnvelopeLength,
            "The Provider API's per-file bound is its decoded envelope, so it "
                + "can never exceed it.");
        Assert.True(
            IntakeEnvelopeLimits.MaximumProviderApiFileLength
                < IntakeEnvelopeLimits.MaximumContentLength,
            "The Provider API must not inherit the manual channel's per-file "
                + "cap; its files arrive inline in one bounded request body.");
    }
}
