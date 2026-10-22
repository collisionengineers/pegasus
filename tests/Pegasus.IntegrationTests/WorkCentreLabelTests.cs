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

    [Fact]
    public void OpenTasksReadAsTheFirstTaskAndHowManyMore()
    {
        var item = NewItem(
            NeedsAttentionKind.OpenTasks,
            title: "Ford Focus GJ13EVC",
            attempts: null,
            detail: "QDOS",
            reason: "Chase the invoice");
        var more = item with { MoreCount = 2 };

        Assert.Equal("Open tasks", OperatorLabels.NeedsAttentionKind(item.Kind));
        Assert.Equal("Tasks", OperatorLabels.WorkCentre.KindChip(item.Kind));
        Assert.Equal("tasks", NeedsAttentionPresentation.KindSlug(item.Kind));
        Assert.Equal(NeedsAttentionKind.OpenTasks, Assert.Single(NeedsAttentionPresentation.ParseKinds(["tasks"])));
        Assert.Equal("Open tasks", NeedsAttentionPresentation.ActionLabel(item));
        Assert.Equal("Chase the invoice", NeedsAttentionPresentation.RowTitle(item));
        Assert.Equal("Chase the invoice (+2 more)", NeedsAttentionPresentation.RowTitle(more));
        Assert.Equal("Ford Focus GJ13EVC", NeedsAttentionPresentation.RowSubject(item));
        // After every Case kind, immediately before AI draft.
        var chips = NeedsAttentionPresentation.ChipOrder.ToList();
        Assert.Equal(chips.IndexOf(NeedsAttentionKind.AiDraft) - 1, chips.IndexOf(NeedsAttentionKind.OpenTasks));
        var facts = NeedsAttentionPresentation.Facts(item, DateTimeOffset.UtcNow);
        Assert.Equal(new WorkCentreFact("Vehicle", "Ford Focus GJ13EVC"), facts[1]);
        Assert.Equal(new WorkCentreFact("Principal", "QDOS"), facts[2]);
    }

    /// <summary>
    /// #1213 (v41 W8, signed off 19 October 2026): a With Engineer row reads
    /// the stage's own word as its kind, chip and bold line, opens the Case,
    /// and its chip sits beside Unassigned, drawn on Mine only.
    /// </summary>
    [Fact]
    public void AWithEngineerRowReadsItsStageAndOpensTheCase()
    {
        var item = NewItem(
            NeedsAttentionKind.WithEngineer,
            title: "Ford Focus GJ13EVC",
            attempts: null,
            detail: "QDOS",
            reason: "ReportPreparation");

        Assert.Equal(
            OperatorLabels.CaseStage(Pegasus.Core.Workflow.CaseLifecycleState.ReportPreparation),
            OperatorLabels.WorkCentre.WithEngineer);
        Assert.Equal(OperatorLabels.WorkCentre.WithEngineer, OperatorLabels.NeedsAttentionKind(item.Kind));
        Assert.Equal(OperatorLabels.WorkCentre.WithEngineer, OperatorLabels.WorkCentre.KindChip(item.Kind));
        Assert.Equal(OperatorLabels.WorkCentre.WithEngineer, NeedsAttentionPresentation.RowTitle(item));
        Assert.Equal(OperatorLabels.WorkCentre.OpenCase, NeedsAttentionPresentation.ActionLabel(item));
        Assert.Equal("with-engineer", NeedsAttentionPresentation.KindSlug(item.Kind));
        Assert.Equal(NeedsAttentionKind.WithEngineer, Assert.Single(NeedsAttentionPresentation.ParseKinds(["with-engineer"])));
        var chips = NeedsAttentionPresentation.ChipOrder.ToList();
        Assert.Equal(chips.IndexOf(NeedsAttentionKind.UnassignedEngineer) + 1, chips.IndexOf(NeedsAttentionKind.WithEngineer));
        Assert.Contains(NeedsAttentionKind.WithEngineer, NeedsAttentionPresentation.ChipsFor(NeedsAttentionScope.Mine));
        Assert.Equal(
            chips.Where(kind => kind != NeedsAttentionKind.WithEngineer),
            NeedsAttentionPresentation.ChipsFor(NeedsAttentionScope.Office));
        var facts = NeedsAttentionPresentation.Facts(item, DateTimeOffset.UtcNow);
        Assert.Equal(new WorkCentreFact("Vehicle", "Ford Focus GJ13EVC"), facts[1]);
        Assert.Equal(new WorkCentreFact("Principal", "QDOS"), facts[2]);
        Assert.Equal(new WorkCentreFact("Due", "No due date"), facts[4]);
    }

    private static NeedsAttentionItem NewItem(
        NeedsAttentionKind kind,
        string title,
        int? attempts,
        string? detail = null,
        string reason = "custody_failed") => new(
        kind,
        Guid.NewGuid(),
        "C/2026/009",
        title,
        detail,
        reason,
        NeedsAttentionPriority.Today,
        Owner: NeedsAttentionPolicy.NoPersonOwner,
        Due: null,
        LastOutcome: null,
        Source: null,
        attempts);
}
