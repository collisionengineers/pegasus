using Pegasus.Core.Assessment;

namespace Pegasus.Core.Tests.Assessment;

public sealed class RepairSpecificationPolicyTests
{
    /// <summary>
    /// Report-generation snapshots store the state and the route as numbers,
    /// so the members that remain keep the numbers they always had.
    /// </summary>
    [Fact]
    public void TheStatesAndRoutesKeepTheirStoredNumbers()
    {
        Assert.Equal(0, (int)RepairSpecificationState.Draft);
        Assert.Equal(3, (int)RepairSpecificationState.Discarded);
        Assert.Equal(
            [RepairSpecificationState.Draft, RepairSpecificationState.Discarded],
            Enum.GetValues<RepairSpecificationState>());

        Assert.Equal(1, (int)RepairSpecificationSourceRoute.Manual);
        Assert.Equal(2, (int)RepairSpecificationSourceRoute.Glasses);
        Assert.Equal(3, (int)RepairSpecificationSourceRoute.AudatexPdf);
        Assert.Equal(5, (int)RepairSpecificationSourceRoute.Json);
        Assert.Equal(6, (int)RepairSpecificationSourceRoute.AiDraft);
        Assert.Equal(5, Enum.GetValues<RepairSpecificationSourceRoute>().Length);
    }

    [Fact]
    public void AnUndefinedRouteIsNotASource() =>
        Assert.Throws<InvalidOperationException>(() => RepairSpecificationPolicy.ValidateSource(
            new((RepairSpecificationSourceRoute)99, null, null, null)));

    [Fact]
    public void OnlyDocumentRoutesRequireArtifactEvidence()
    {
        var manual = RepairSpecificationPolicy.ValidateSource(
            new(RepairSpecificationSourceRoute.Manual, null, null, null));
        Assert.Null(manual.Sha256);
        Assert.Throws<InvalidOperationException>(() => RepairSpecificationPolicy.ValidateSource(
            new(RepairSpecificationSourceRoute.AudatexPdf, "estimate-import:1", "v1", null)));
        Assert.Throws<InvalidOperationException>(() => RepairSpecificationPolicy.ValidateSource(
            new(RepairSpecificationSourceRoute.Json, "estimate-import:1", "v1", null)));
        var typed = RepairSpecificationPolicy.ValidateSource(
            new(RepairSpecificationSourceRoute.AiDraft, null, null, null));
        Assert.Null(typed.ArtifactReference);

        Assert.True(RepairSpecificationPolicy.IsDocumentRoute(RepairSpecificationSourceRoute.Glasses));
        Assert.True(RepairSpecificationPolicy.IsDocumentRoute(RepairSpecificationSourceRoute.AudatexPdf));
        Assert.True(RepairSpecificationPolicy.IsDocumentRoute(RepairSpecificationSourceRoute.Json));
        Assert.False(RepairSpecificationPolicy.IsDocumentRoute(RepairSpecificationSourceRoute.Manual));
        Assert.False(RepairSpecificationPolicy.IsDocumentRoute(RepairSpecificationSourceRoute.AiDraft));
    }
}
