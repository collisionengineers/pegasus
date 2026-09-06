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

        // The shell notifications menu shares this page's own snapshot rather
        // than paying for a second IGetAttentionRows call — RailCountsPageFilter
        // skips this page for exactly that reason (C08).
        ViewData["AttentionRows"] = NeedsAttention
            .Take(GetOperationsSnapshot.MaximumAttentionRows)
            .ToArray();

        return Page();
    }
}
