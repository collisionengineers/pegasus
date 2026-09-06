using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages;

/// <summary>
/// The Work Centre: office-wide work behind five queried metrics and the
/// needs-attention list (FRD-12 § Work Centre).
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public class IndexModel(IGetOperationsSnapshot getOperationsSnapshot) : StaffPageModel
{
    public IntakeQueueCounts Counts { get; private set; } = new(0, 0);

    public CaseStageCounts CaseStages { get; private set; } = new(0, 0, 0, 0);

    public int UnidentifiedCount { get; private set; }

    public IReadOnlyList<NeedsAttentionItem> NeedsAttention { get; private set; } = [];

    /// <summary>
    /// The item whose detail the right pane shows: the row the address names,
    /// else the first row — the operator's next work — else none.
    /// </summary>
    public NeedsAttentionItem? Selected { get; private set; }

    public DateTimeOffset LoadedAtUtc { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? selected, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        var snapshot = await getOperationsSnapshot.ExecuteAsync(actor, cancellationToken);
        LoadedAtUtc = snapshot.AsOfUtc;
        Counts = snapshot.Intake;
        CaseStages = snapshot.CaseStages;
        UnidentifiedCount = snapshot.UnidentifiedCount;
        NeedsAttention = snapshot.NeedsAttention;
        Selected = Guid.TryParse(selected, out var selectedId)
            ? NeedsAttention.FirstOrDefault(item => item.Id == selectedId)
            : null;
        Selected ??= NeedsAttention.Count > 0 ? NeedsAttention[0] : null;
        return Page();
    }

    /// <summary>
    /// The page behind a work item: the row, the pane's Open-full-record and
    /// the next-permitted-action button all open the same record.
    /// </summary>
    public static string RecordPage(NeedsAttentionKind kind) => kind switch
    {
        NeedsAttentionKind.Case or NeedsAttentionKind.HeldDecision => "/Cases/Details",
        NeedsAttentionKind.Mail => "/Unidentified/Details",
        NeedsAttentionKind.Triage => "/Triage/Details",
        _ => "/Operations/Index"
    };

    /// <summary>
    /// The route id for that page: external work opens Operations, which has
    /// no record id of its own — a null omits the route value entirely.
    /// </summary>
    public static Guid? RecordRouteId(NeedsAttentionItem item) =>
        item.Kind == NeedsAttentionKind.ExternalWork ? null : item.Id;

    /// <summary>The next permitted action's words, per the Work Centre contract.</summary>
    public static string ActionLabel(NeedsAttentionKind kind) => kind switch
    {
        NeedsAttentionKind.Triage => "Open Triage",
        NeedsAttentionKind.Mail => "Review source",
        NeedsAttentionKind.ExternalWork => "Open Operations",
        _ => "Open Case"
    };

    /// <summary>
    /// The row's title. External work records its kind as the persisted
    /// snake_case code, so it is labelled through the same helper the
    /// Operations table's Work column already uses; every other kind's title
    /// is a reference or a recorded name that is already operator text.
    /// </summary>
    public static string TitleLabel(NeedsAttentionItem item) =>
        item.Kind == NeedsAttentionKind.ExternalWork
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
        NeedsAttentionKind.Case => OperatorLabels.ChaseState(item.Reason),
        NeedsAttentionKind.Mail => OperatorLabels.UnidentifiedReason(item.Reason),
        NeedsAttentionKind.Triage => OperatorLabels.Humanise(item.Reason),
        _ => item.Reason
    };

    /// <summary>The fact grid's Source value: the recorded origin, labelled per kind.</summary>
    public static string? SourceLabel(NeedsAttentionItem item) => item.Kind switch
    {
        NeedsAttentionKind.Mail => OperatorLabels.UnidentifiedMediaKind(item.Source),
        _ => item.Source
    };
}
