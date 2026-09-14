using Pegasus.Core.Operations;
using Labels = Pegasus.Web.Presentation.OperatorLabels.WorkCentre;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The one mapping from a needs-attention row to what the Work Centre shows for
/// it: the kind chip and its query slug, the row's title and second line, the
/// Today pane's facts and the next permitted action's words. Core carries the
/// recorded facts; this turns them into the v26 row (Work Centre D2, P1–P6).
/// </summary>
public static class NeedsAttentionPresentation
{
    /// <summary>The kind chips in the mockup's order (P3).</summary>
    public static readonly IReadOnlyList<NeedsAttentionKind> ChipOrder =
    [
        NeedsAttentionKind.CaseChase,
        NeedsAttentionKind.HeldDecision,
        NeedsAttentionKind.ReviewCase,
        NeedsAttentionKind.UnassignedEngineer,
        NeedsAttentionKind.Unidentified,
        NeedsAttentionKind.Triage,
        NeedsAttentionKind.AiDraft
    ];

    /// <summary>The due-day groups in list order (P1).</summary>
    public static readonly IReadOnlyList<NeedsAttentionPriority> Groups =
    [
        NeedsAttentionPriority.Overdue,
        NeedsAttentionPriority.Today,
        NeedsAttentionPriority.Normal
    ];

    public static string KindSlug(NeedsAttentionKind kind) => kind switch
    {
        NeedsAttentionKind.CaseChase => "case",
        NeedsAttentionKind.HeldDecision => "held",
        NeedsAttentionKind.ReviewCase => "review",
        NeedsAttentionKind.UnassignedEngineer => "unassigned",
        NeedsAttentionKind.Unidentified => "unidentified",
        NeedsAttentionKind.Triage => "triage",
        NeedsAttentionKind.AiDraft => "ai",
        _ => kind.ToString().ToLowerInvariant()
    };

    /// <summary>The kinds a query string names, in chip order; unknown slugs are ignored.</summary>
    public static IReadOnlyList<NeedsAttentionKind> ParseKinds(IEnumerable<string>? slugs)
    {
        var named = (slugs ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return ChipOrder.Where(kind => named.Contains(KindSlug(kind))).ToArray();
    }

    /// <summary>
    /// The page behind a work item when the row carries no route of its own.
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

    /// <summary>The next permitted action's words (P4).</summary>
    public static string ActionLabel(NeedsAttentionItem item) => item.Kind switch
    {
        NeedsAttentionKind.Triage => "Open Triage",
        NeedsAttentionKind.Unidentified => "Review source",
        NeedsAttentionKind.AiDraft => Labels.AiDraftAction(item.Source),
        NeedsAttentionKind.ReviewCase => "Review Case",
        NeedsAttentionKind.UnassignedEngineer => Labels.AssignEngineer,
        _ => Labels.OpenCase
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
    /// The recorded title. An AI draft records its kind as the Core enum name,
    /// so it is labelled; every other kind's title is already operator text.
    /// </summary>
    public static string TitleLabel(NeedsAttentionItem item) =>
        item.Kind == NeedsAttentionKind.AiDraft
            ? OperatorLabels.Humanise(item.Title)
            : item.Title;

    /// <summary>The recorded second fact (principal, sender or instruction).</summary>
    public static string? DetailLabel(NeedsAttentionItem item) =>
        item.Attempts is { } attempts ? $"{attempts} attempts" : item.Detail;

    /// <summary>What needs doing: the row's bold line.</summary>
    public static string RowTitle(NeedsAttentionItem item) => item.Kind switch
    {
        NeedsAttentionKind.CaseChase => string.IsNullOrWhiteSpace(item.Title)
            ? OperatorLabels.ChaseState(item.Reason)
            : item.Title,
        NeedsAttentionKind.HeldDecision => "Held decision",
        NeedsAttentionKind.ReviewCase => "Review Case",
        NeedsAttentionKind.UnassignedEngineer => Labels.AssignEngineer,
        NeedsAttentionKind.Unidentified => OperatorLabels.UnidentifiedReason(item.Reason),
        NeedsAttentionKind.Triage => Labels.TriageTitle(item.Reason),
        NeedsAttentionKind.AiDraft => Labels.AiDraftTitle(item.Title),
        _ => item.Title
    };

    /// <summary>"Kind · reference · subject": the row's second line.</summary>
    public static string RowDetail(NeedsAttentionItem item)
    {
        var parts = new List<string> { Labels.KindChip(item.Kind), ReferenceLabel(item) };
        var subject = item.Kind switch
        {
            NeedsAttentionKind.CaseChase => null,
            NeedsAttentionKind.AiDraft => item.Detail,
            _ => item.Title
        };
        if (!string.IsNullOrWhiteSpace(subject)
            && !string.Equals(subject, item.Reference, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(subject);
        }

        return string.Join(" · ", parts);
    }

    public static string OwnerLabel(NeedsAttentionItem item) =>
        item.Owner ?? (item.Kind is NeedsAttentionKind.Unidentified or NeedsAttentionKind.AiDraft
            ? Labels.NoOwner
            : Labels.NoEngineer);

    public static string DueTone(NeedsAttentionPriority priority) => priority switch
    {
        NeedsAttentionPriority.Overdue => "overdue",
        NeedsAttentionPriority.Today => "today",
        _ => "later"
    };

    /// <summary>The Today pane's six facts for the selected row.</summary>
    public static IReadOnlyList<WorkCentreFact> Facts(NeedsAttentionItem item, DateTimeOffset now)
    {
        var subject = item.Kind switch
        {
            NeedsAttentionKind.HeldDecision => new WorkCentreFact("Claimant", item.Title),
            NeedsAttentionKind.ReviewCase or NeedsAttentionKind.UnassignedEngineer => new WorkCentreFact("Vehicle", item.Title),
            NeedsAttentionKind.Unidentified => new WorkCentreFact("Source", item.Title),
            NeedsAttentionKind.Triage => new WorkCentreFact("Registration", item.Title, Mono: true),
            NeedsAttentionKind.AiDraft => new WorkCentreFact("Job", TitleLabel(item)),
            _ => new WorkCentreFact("Missing", item.Title)
        };
        var second = item.Kind switch
        {
            NeedsAttentionKind.HeldDecision or NeedsAttentionKind.ReviewCase or NeedsAttentionKind.UnassignedEngineer =>
                new WorkCentreFact("Principal", item.Detail),
            NeedsAttentionKind.Unidentified => new WorkCentreFact("Sender", item.Detail),
            NeedsAttentionKind.AiDraft => new WorkCentreFact("Instruction", item.Detail),
            NeedsAttentionKind.Triage => new WorkCentreFact("State", Labels.TriageTitle(item.Reason)),
            _ => new WorkCentreFact("Chase", OperatorLabels.ChaseState(item.Reason))
        };
        return
        [
            new WorkCentreFact("Reference", ReferenceLabel(item), Mono: true),
            subject,
            second,
            new WorkCentreFact("Owner", OwnerLabel(item)),
            new WorkCentreFact("Due", Labels.DueText(item.Due, now)),
            new WorkCentreFact("Received", Labels.ReceivedText(item.Received, now))
        ];
    }
}

/// <summary>One fact on the Today pane; an empty value reads "Not recorded".</summary>
public sealed record WorkCentreFact(string Label, string? Value, bool Mono = false);
