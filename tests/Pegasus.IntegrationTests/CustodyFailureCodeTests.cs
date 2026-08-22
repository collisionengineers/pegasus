using Pegasus.Core.Custody;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// DOCS-008: two production audits failed custody with an unclassified code and
/// nothing retained what threw, so diagnosis meant reading source and writing
/// reproductions. An unclassified failure now names its own exception type.
/// </summary>
public sealed class CustodyFailureCodeTests
{
    [Theory]
    [InlineData(typeof(FileNotFoundException), "source_unavailable")]
    [InlineData(typeof(InvalidDataException), "source_integrity_conflict")]
    [InlineData(typeof(UnauthorizedAccessException), "custody_scope_denied")]
    [InlineData(typeof(OperationCanceledException), "custody_cancelled")]
    [InlineData(typeof(IOException), "custody_dependency_failure")]
    public void AClassifiedFailureKeepsItsExactCode(Type exceptionType, string expected) =>
        Assert.Equal(
            expected,
            EfQueuedCustodyProcessor.GetFailureCode((Exception)Activator.CreateInstance(exceptionType)!));

    [Fact]
    public void AnUnclassifiedFailureNamesItsOwnType() =>
        Assert.Equal(
            "custody_unexpected_failure:InvalidOperationException",
            EfQueuedCustodyProcessor.GetFailureCode(new InvalidOperationException("anything")));

    [Fact]
    public void TheOperatorSafeReasonIsUnchangedByThatSuffix() =>
        Assert.Equal(
            "Case evidence could not be stored.",
            EfQueuedCustodyProcessor.GetFailureReason(new InvalidOperationException("anything")));

    [Fact]
    public void AClassifiedFailureStillGetsItsOwnReason() =>
        Assert.Equal(
            "Case evidence storage stopped because this processing attempt no longer owns the work.",
            EfQueuedCustodyProcessor.GetFailureReason(new CustodyProcessingLeaseLostException()));

    [Fact]
    public void ALongTypeNameCannotOverflowTheColumn() =>
        Assert.True(
            EfQueuedCustodyProcessor.GetFailureCode(new AVeryLongExceptionTypeNameThatWouldOtherwiseOverflowTheHundredCharacterFailureCodeColumn()).Length <= 100);

    private sealed class AVeryLongExceptionTypeNameThatWouldOtherwiseOverflowTheHundredCharacterFailureCodeColumn : Exception;
}
