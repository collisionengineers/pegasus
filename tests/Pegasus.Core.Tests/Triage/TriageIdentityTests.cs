using Pegasus.Core.Cases;

namespace Pegasus.Core.Tests.Triage;

public sealed class TriageIdentityTests
{
    [Theory]
    [InlineData("QDOS26001", "t.QDOS26001")]
    [InlineData("ABC27142", "t.ABC27142")]
    public void CreatesTriageReferenceFromAllocatedCaseReference(string caseReference, string expected) =>
        Assert.Equal(expected, TriageIdentity.Create(caseReference));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsMissingAllocatedCaseReference(string? caseReference) =>
        Assert.ThrowsAny<ArgumentException>(() => TriageIdentity.Create(caseReference!));
}
