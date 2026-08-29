using Pegasus.Core.Operations;
using Pegasus.Web.Pages;

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
        Assert.Equal("Document custody", IndexModel.TitleLabel(item));
    }

    [Fact]
    public void ExternalWorkDetailReadsTheRecordedAttemptCount()
    {
        var item = NewItem(
            NeedsAttentionKind.ExternalWork,
            title: "document_custody",
            attempts: 2);

        Assert.Equal("2 attempts", IndexModel.DetailLabel(item));
    }

    [Fact]
    public void EveryOtherKindKeepsItsRecordedTitleAndDetail()
    {
        var item = NewItem(
            NeedsAttentionKind.HeldDecision,
            title: "Mr A Claimant",
            attempts: null,
            detail: "QDOS");

        Assert.Equal("Mr A Claimant", IndexModel.TitleLabel(item));
        Assert.Equal("QDOS", IndexModel.DetailLabel(item));
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
