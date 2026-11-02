using System.Globalization;
using Pegasus.Core.Cases;

namespace Pegasus.Core.Tests.Cases;

public sealed class CaseReferenceFormatTests
{
    [Theory]
    [InlineData(CaseType.Inspection, "QDOS26001")]
    [InlineData(CaseType.InspectionAndAudit, "QDOS26001")]
    [InlineData(CaseType.Audit, "a.QDOS26001")]
    [InlineData(CaseType.Triage, "t.QDOS26001")]
    public void EachCaseTypeTakesItsOwnPrefix(CaseType caseType, string expected)
    {
        var baseReference = CaseReferenceFormat.Base("QDOS", 2026, 1);

        Assert.Equal(expected, CaseReferenceFormat.CasePo(caseType, baseReference));
    }

    [Fact]
    public void TheSequenceWidensPastNineHundredAndNinetyNine()
    {
        Assert.Equal("QDOS261000", CaseReferenceFormat.Base("QDOS", 2026, 1000));
        Assert.Equal("QDOS2610000", CaseReferenceFormat.Base("QDOS", 2026, 10000));
    }

    [Fact]
    public void TheBaseReferenceIgnoresTheCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            Assert.Equal("QDOS26007", CaseReferenceFormat.Base("QDOS", 2026, 7));
            Assert.Equal("t.QDOS26007", CaseReferenceFormat.CasePo(CaseType.Triage, "QDOS26007"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void TheAuditReportOfAnInspectionAndAuditPrefixesItsCasePo()
    {
        Assert.Equal("a.QDOS26001", CaseReferenceFormat.AuditReport("QDOS26001"));
    }

    [Fact]
    public void AnUndefinedCaseTypeIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CaseReferenceFormat.CasePo((CaseType)99, "QDOS26001"));
    }

    /// <summary>
    /// A one-off Case for a private customer is referenced by its registration,
    /// and a later one-off of the same vehicle by <c>.2</c>, <c>.3</c>
    /// (operator, 31 October 2026; R574, R575).
    /// </summary>
    [Theory]
    [InlineData(1, "AB12CDE")]
    [InlineData(2, "AB12CDE.2")]
    [InlineData(10, "AB12CDE.10")]
    public void AOneOffIsReferencedByItsRegistrationAndItsRepeat(int repeat, string expected)
    {
        Assert.Equal(expected, CaseReferenceFormat.Registration("AB12CDE", repeat));
    }

    [Theory]
    [InlineData("ab12cde")]
    [InlineData("AB12 CDE")]
    [InlineData("AB12CDE.2")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")]
    public void AOneOffReferenceTakesOnlyANormalisedRegistration(string registration)
    {
        Assert.Throws<ArgumentException>(() => CaseReferenceFormat.Registration(registration, 1));
    }

    [Fact]
    public void AOneOffRepeatBelowOneIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CaseReferenceFormat.Registration("AB12CDE", 0));
    }

    /// <summary>
    /// The next repeat is one past the highest held, so a gap never hands a
    /// reference out twice; other vehicles' references and the Image
    /// reference shape <c>AB12CDE-01</c> are not counted.
    /// </summary>
    [Fact]
    public void TheNextRepeatFollowsTheHighestReferenceAlreadyHeld()
    {
        Assert.Equal(1, CaseReferenceFormat.NextRegistrationRepeat("AB12CDE", []));
        Assert.Equal(2, CaseReferenceFormat.NextRegistrationRepeat("AB12CDE", ["AB12CDE"]));
        Assert.Equal(4, CaseReferenceFormat.NextRegistrationRepeat("AB12CDE", ["AB12CDE", "AB12CDE.3"]));
        Assert.Equal(
            1,
            CaseReferenceFormat.NextRegistrationRepeat(
                "AB12CDE", ["AB12CDEF", "AB12CDE-01", "AB12CDE.x", "QDOS26001"]));
    }
}
