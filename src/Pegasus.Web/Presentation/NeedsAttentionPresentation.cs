using Pegasus.Core.Operations;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The one mapping from a needs-attention row to what an operator surface
/// shows for it (C08): which record page it opens, its next-permitted-action
/// words, its title, its second line and its fact-grid values. The Work
/// Centre (<see cref="Pegasus.Web.Pages.IndexModel"/>) and the shell
/// notifications menu both read the same rows, so this is the one place that
/// turns a row into words rather than each surface writing its own copy.
/// </summary>
public static class NeedsAttentionPresentation
{
    /// <summary>
    /// The page behind a work item: the row, the pane's Open-full-record and
    /// the next-permitted-action button all open the same record.
    /// </summary>
    public static string RecordPage(NeedsAttentionKind kind) => kind switch
    {
        NeedsAttentionKind.CaseChase or NeedsAttentionKind.HeldDecision
            or NeedsAttentionKind.ReviewCase or NeedsAttentionKind.UnassignedEngineer => "/Cases/Details",
        NeedsAttentionKind.Unidentified => "/Unidentified/Details",
        NeedsAttentionKind.Triage => "/Triage/Details",
        _ => "/Operations/Index"
    };

    /// <summary>
    /// The route id for that page: an AI draft opens through its own route
    /// (<see cref="NeedsAttentionItem.Route"/>), so it has no record id here.
    /// </summary>
    public static Guid? RecordRouteId(NeedsAttentionItem item) =>
        item.Kind == NeedsAttentionKind.AiDraft ? null : item.Id;

    /// <summary>The next permitted action's words, per the Work Centre contract.</summary>
    public static string ActionLabel(NeedsAttentionKind kind) => kind switch
    {
        NeedsAttentionKind.Triage => "Open Triage",
        NeedsAttentionKind.Unidentified => "Review source",
        NeedsAttentionKind.AiDraft => "Review draft",
        NeedsAttentionKind.ReviewCase => "Review Case",
        NeedsAttentionKind.UnassignedEngineer => "Assign Engineer",
        _ => "Open Case"
    };

    /// <summary>
    /// The row's reference. A queue pass names no record, so its Core subject
    /// token is printed as the operator's words for the queue.
    /// </summary>
    public static string ReferenceLabel(NeedsAttentionItem item) =>
        item.Reference == Pegasus.Core.AiWork.AiJobPolicy.QueueSubjectReference
            ? OperatorLabels.AiJobs.QueueRecord
            : item.Reference;

    /// <summary>
    /// The row's title. External work records its kind as the persisted
    /// snake_case code, so it is labelled through the same helper the
    /// Operations table's Work column already uses; every other kind's title
    /// is a reference or a recorded name that is already operator text.
    /// </summary>
    public static string TitleLabel(NeedsAttentionItem item) =>
        item.Kind == NeedsAttentionKind.AiDraft
            ? OperatorLabels.Humanise(item.Title)
            : item.Title;

    /// <summary>
    /// The row's second line. External work records a try count rather than a
    /// name, so the words that read it live here with the rest of the page's
    /// copy — Core carries the number.
    /// </summary>
    public static string? DetailLabel(NeedsAttentionItem item) =>
        item.Attempts is { } attempts ? $"{attempts} attempts" : item.Detail;

    /// <summary>
    /// The notice's value: the recorded reason, labelled through the one map
    /// that owns each kind's vocabulary.
    /// </summary>
    public static string ReasonLabel(NeedsAttentionItem item) => item.Kind switch
    {
        NeedsAttentionKind.CaseChase => OperatorLabels.ChaseState(item.Reason),
        NeedsAttentionKind.ReviewCase => "Case needs review",
        NeedsAttentionKind.UnassignedEngineer => "Engineer assignment is required",
        NeedsAttentionKind.Unidentified => OperatorLabels.UnidentifiedReason(item.Reason),
        NeedsAttentionKind.Triage => OperatorLabels.Humanise(item.Reason),
        _ => item.Reason
    };

    /// <summary>The fact grid's Source value: the recorded origin, labelled per kind.</summary>
    public static string? SourceLabel(NeedsAttentionItem item) => item.Kind switch
    {
        NeedsAttentionKind.Unidentified => OperatorLabels.UnidentifiedMediaKind(item.Source),
        _ => item.Source
    };
}
