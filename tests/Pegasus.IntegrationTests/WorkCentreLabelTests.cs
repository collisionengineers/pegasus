using Pegasus.Core.Operations;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// UIIMP-008: the needs-attention row carries recorded facts and Core enum
/// names, and the Work Centre labels them. External work records its kind as
/// the persisted snake_case code and its tries as a number, so neither may
/// reach the operator as it is stored.
/// </summary>
public sealed class WorkCentreLabelTests
{
    [Fact]
    public void ExternalWorkTitleRendersThroughTheOperatorLabelMap()
    {
        var item = NewItem(
            NeedsAttentionKind.ExternalWork,
            title: "document_custody",
            attempts: 2);

        // The same words the Operations table's Work column already shows for
        // this exact field — one list per concept, not a second map.
        Assert.Equal("Document custody", NeedsAttentionPresentation.TitleLabel(item));
    }

    [Fact]
    public void ExternalWorkDetailReadsTheRecordedAttemptCount()
    {
        var item = NewItem(
            NeedsAttentionKind.ExternalWork,
            title: "document_custody",
            attempts: 2);

        Assert.Equal("2 attempts", NeedsAttentionPresentation.DetailLabel(item));
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
    /// UIIMP-008: `asp-page` takes a Razor page name, not a route template.
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
    public void ExternalWorkOpensTheOperationsPageByItsPageName()
    {
        Assert.Equal("/Operations/Index", NeedsAttentionPresentation.RecordPage(NeedsAttentionKind.ExternalWork));
    }

    /// <summary>
    /// Every other kind names a real page too, so none of them can regress the
    /// same way.
    /// </summary>
    [Theory]
    [InlineData(NeedsAttentionKind.Case, "/Cases/Details")]
    [InlineData(NeedsAttentionKind.HeldDecision, "/Cases/Details")]
    [InlineData(NeedsAttentionKind.Mail, "/Unidentified/Details")]
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
        NeedsAttentionPriority.High,
        Owner: null,
        Due: null,
        LastOutcome: null,
        Source: null,
        attempts);
}
