using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

/// <summary>What a Principal's reports are called, as its report settings save them (R740).</summary>
public sealed class PrincipalReportNamesTests
{
    [Fact]
    public void NamesAreTrimmedAndAnEmptyOneIsTheStandardName()
    {
        var normalized = PrincipalReportNames.Normalize(new(" {REG} Initial ", "  ", null, "{reg}_{case ref}_AUDIT"));

        Assert.Equal(new PrincipalReportNames("{REG} Initial", null, null, "{reg}_{case ref}_AUDIT"), normalized);
        Assert.Equal(PrincipalReportNames.None, PrincipalReportNames.Normalize(null));
        Assert.Equal(normalized, PrincipalReportNames.Normalize(normalized));
    }

    [Theory]
    [InlineData("{ref} Initial")]
    [InlineData("{VRM}")]
    [InlineData("{CASEREF}")]
    public void AnUnknownTokenIsRefusedNamingItsReport(string name)
    {
        var inspection = Assert.Throws<ReportNamesException>(() => PrincipalReportNames.Normalize(new(name, null, null, null)));
        Assert.Equal(ReportNamesRule.UnknownToken, inspection.Rule);
        Assert.False(inspection.AuditReport);

        var audit = Assert.Throws<ReportNamesException>(() => PrincipalReportNames.Normalize(new(null, null, null, name)));
        Assert.True(audit.AuditReport);
    }

    /// <summary>
    /// A name is refused when its longest result could not be filed: its
    /// text with each token at its longest value, and the extension, within
    /// custody's 120 characters.
    /// </summary>
    [Fact]
    public void ANameTooLongToFileIsRefused()
    {
        // 43 (reference) + 1 + 20 (registration) + 1 + 15 (outcome) + 4 (.pdf) = 84.
        const string tokens = "{CASE REF} {REG} {OUTCOME}";
        Assert.NotNull(PrincipalReportNames.Normalize(new(tokens + new string('x', 36), null, null, null)).InspectionFirst);

        var refused = Assert.Throws<ReportNamesException>(() =>
            PrincipalReportNames.Normalize(new(null, tokens + new string('x', 37), null, null)));
        Assert.Equal(ReportNamesRule.TooLong, refused.Rule);
        Assert.False(refused.AuditReport);
    }
}
