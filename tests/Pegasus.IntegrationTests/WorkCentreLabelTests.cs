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
    public void EveryOtherKindKeepsItsRecordedTitleAndDetail()
    {
        var item = NewItem(
            NeedsAttentionKind.HeldDecision,
            title: "Mr A Claimant",
            attempts: null,
            detail: "QDOS");

        Assert.Equal("Mr A Claimant", NeedsAttentionPresentation.TitleLabel(item));
        Assert.Equal("QDOS", NeedsAttentionPresentation.DetailLabel(item));
    }

    /// <summary>
    /// `asp-page` takes a Razor page name, not a route template.
    /// `Pages/Operations/Index.cshtml` declares `@page "/Operations"`, which
    /// sets its route but leaves its page name `/Operations/Index` — the
    /// spelling `_Layout.cshtml` uses. `RecordPage` returned the route, so the
    /// tag helper resolved nothing and every external-work row, the pane's
    /// Open-full-record and the next-action button rendered `href=""`.
    ///
    /// A dead link is valid HTML, so no gate caught it. This pins the page name
    /// itself; <see cref="TheWorkCentreRendersNoEmptyLink"/> catches the class.
    /// </summary>
    [Fact]
    public void AnAiDraftOpensThroughItsOwnRouteNotARecordPage()
    {
        Assert.Equal("/Operations/Index", NeedsAttentionPresentation.RecordPage(NeedsAttentionKind.AiDraft));
        Assert.Null(NeedsAttentionPresentation.RecordRouteId(NewItem(NeedsAttentionKind.AiDraft, "Estimate", null)));
    }

    /// <summary>
    /// Every other kind names a real page too, so none of them can regress the
    /// same way.
    /// </summary>
    [Theory]
    [InlineData(NeedsAttentionKind.CaseChase, "/Cases/Details")]
    [InlineData(NeedsAttentionKind.HeldDecision, "/Cases/Details")]
    [InlineData(NeedsAttentionKind.ReviewCase, "/Cases/Details")]
    [InlineData(NeedsAttentionKind.UnassignedEngineer, "/Cases/Details")]
    [InlineData(NeedsAttentionKind.Unidentified, "/Unidentified/Details")]
    [InlineData(NeedsAttentionKind.Triage, "/Triage/Details")]
    public void EveryRecordPageNamesARealPage(NeedsAttentionKind kind, string expected)
    {
        Assert.Equal(expected, NeedsAttentionPresentation.RecordPage(kind));
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
