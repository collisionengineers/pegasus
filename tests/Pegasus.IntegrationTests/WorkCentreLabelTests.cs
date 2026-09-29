using Pegasus.Core.Operations;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The needs-attention row carries recorded facts and Core enum
/// names, and the Work Centre labels them. An AI draft records its kind as the
/// enum name, so it may not reach the operator as it is stored. Failed
/// external work is no longer a row (Work Centre D1).
/// </summary>
public sealed class WorkCentreLabelTests
{
    [Fact]
    public void AiDraftTitleRendersThroughTheOperatorLabelMap()
    {
        var item = NewItem(
            NeedsAttentionKind.AiDraft,
            title: "QueryResponse",
            attempts: null);

        Assert.Equal("Query response", NeedsAttentionPresentation.TitleLabel(item));
    }

    [Fact]
    public void EveryOtherKindKeepsItsRecordedTitle()
    {
        var item = NewItem(
            NeedsAttentionKind.HeldDecision,
            title: "Mr A Claimant",
            attempts: null,
            detail: "QDOS");

        Assert.Equal("Mr A Claimant", NeedsAttentionPresentation.TitleLabel(item));
    }

    [Fact]
    public void PairedVehicleImagesReadAsTheirImageReferenceAndPrincipal()
    {
        var item = NewItem(
            NeedsAttentionKind.VehicleImagesPaired,
            title: "GJ13EVC-01",
            attempts: null,
            detail: "QDOS");

        Assert.Equal("Vehicle images paired", OperatorLabels.WorkCentre.KindChip(item.Kind));
        Assert.Equal("Vehicle images paired", NeedsAttentionPresentation.RowTitle(item));
        Assert.Equal("images", NeedsAttentionPresentation.KindSlug(item.Kind));
        Assert.Equal("Open Case", NeedsAttentionPresentation.ActionLabel(item));
        var facts = NeedsAttentionPresentation.Facts(item, DateTimeOffset.UtcNow);
        Assert.Equal(new WorkCentreFact("Image reference", "GJ13EVC-01", Mono: true), facts[1]);
        Assert.Equal(new WorkCentreFact("Principal", "QDOS"), facts[2]);
    }

    private static NeedsAttentionItem NewItem(
        NeedsAttentionKind kind,
        string title,
        int? attempts,
        string? detail = null) => new(
        kind,
        Guid.NewGuid(),
        "C/2026/009",
        title,
        detail,
        "custody_failed",
        NeedsAttentionPriority.Today,
        Owner: null,
        Due: null,
        LastOutcome: null,
        Source: null,
        attempts);
}
