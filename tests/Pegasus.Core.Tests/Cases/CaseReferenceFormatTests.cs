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
}
