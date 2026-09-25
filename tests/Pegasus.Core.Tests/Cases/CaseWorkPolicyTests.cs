using Pegasus.Core.Cases;

namespace Pegasus.Core.Tests.Cases;

public sealed class CaseWorkPolicyTests
{
    private static readonly Guid CaseId = Guid.Parse("5d2f0d8e-2a6b-4a8f-9d61-0c6f2a7e1b01");
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 24, 9, 0, 0, TimeSpan.Zero);

    private static CaseWork Primary() => new(CaseId, CaseId, CaseWorkKind.Primary, CreatedAt);

    private static CaseWork Audit() =>
        new(Guid.Parse("9c1e3f55-7b4d-4c38-8f0a-2e5d6b7c8a90"), CaseId, CaseWorkKind.Audit, CreatedAt.AddDays(3));

    [Fact]
    public void WithoutAnAuditThePrimaryWorkIsCurrent()
    {
        var works = new CaseWorkSet(Primary(), null);

        Assert.False(works.HasAudit);
        Assert.Same(works.Primary, works.Current);
        Assert.Same(works.Primary, works.Select(CaseWorkSelector.Current));
        Assert.Same(works.Primary, works.Select(CaseWorkSelector.Primary));
    }

    [Fact]
    public void OnceAnAuditExistsItIsCurrentAndThePrimaryIsStillSelectable()
    {
        var works = new CaseWorkSet(Primary(), Audit());

        Assert.True(works.HasAudit);
        Assert.Same(works.Audit, works.Current);
        Assert.Same(works.Audit, works.Select(CaseWorkSelector.Current));
        Assert.Same(works.Primary, works.Select(CaseWorkSelector.Primary));
    }

    [Fact]
    public void TheCurrentSelectorIsTheDefault()
    {
        Assert.Equal(CaseWorkSelector.Current, default(CaseWorkSelector));
    }

    [Theory]
    [InlineData(CaseType.Audit, CaseWorkKind.Primary, true)]
    [InlineData(CaseType.InspectionAndAudit, CaseWorkKind.Audit, true)]
    [InlineData(CaseType.InspectionAndAudit, CaseWorkKind.Primary, false)]
    [InlineData(CaseType.Inspection, CaseWorkKind.Primary, false)]
    public void AnAuditReportIsAStandaloneAuditOrTheAuditWorksReport(
        CaseType caseType,
        CaseWorkKind workKind,
        bool expected)
    {
        Assert.Equal(expected, CaseWorkPolicy.IsAuditReport(caseType, workKind));
    }

    [Fact]
    public void ThePrimaryWorksReportCarriesTheCasePo()
    {
        var identity = new CaseIdentity(CaseId, "QDOS", 2026, 1, "QDOS26001", "a.QDOS26001");

        Assert.Equal("QDOS26001", CaseReferenceFormat.ReportReference(identity, CaseWorkKind.Primary));
    }

    [Fact]
    public void TheAuditWorksReportCarriesTheAuditReference()
    {
        var recorded = new CaseIdentity(CaseId, "QDOS", 2026, 1, "QDOS26001", "a.QDOS26001");
        var unrecorded = new CaseIdentity(CaseId, "QDOS", 2026, 1, "QDOS26001");

        Assert.Equal("a.QDOS26001", CaseReferenceFormat.ReportReference(recorded, CaseWorkKind.Audit));
        Assert.Equal("a.QDOS26001", CaseReferenceFormat.ReportReference(unrecorded, CaseWorkKind.Audit));
    }

    [Fact]
    public void AStandaloneAuditsReportCarriesItsOwnPrefixedCasePo()
    {
        var identity = new CaseIdentity(CaseId, "QDOS", 2026, 1, "a.QDOS26001");

        Assert.Equal("a.QDOS26001", CaseReferenceFormat.ReportReference(identity, CaseWorkKind.Primary));
    }
}
