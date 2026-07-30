using Pegasus.Core.Intake;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Pegasus.Web.Pages;

public class IndexModel(
    IIntakeReceiptQueries queries,
    Pegasus.Core.Cases.ICaseQueries caseQueries,
    Pegasus.Core.Triage.ITriageQueries triageQueries,
    Pegasus.Core.Access.IStaffActorAccessor actorAccessor,
    IConfiguration configuration,
    IWebHostEnvironment environment) : PageModel
{
    public IntakeQueueCounts Counts { get; private set; } = new(0, 0);
    public Pegasus.Core.Cases.CaseQueueCounts CaseCounts { get; private set; } = new(0, 0, 0, 0, 0, 0, 0, DateTimeOffset.UtcNow);
    public int TriageOpenCount { get; private set; }
    public string? Error { get; private set; }

    public bool LocalIntakeEnabled { get; private set; }

    public DateTimeOffset LoadedAtUtc { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        LocalIntakeEnabled = environment.IsDevelopment()
            && configuration.GetValue<bool>("Features:LocalIntake");
        var actor = actorAccessor.Current;
        if (actor is null)
        {
            Error = "Authentication is required.";
            LoadedAtUtc = DateTimeOffset.UtcNow;
            return;
        }
        try
        {
            if (LocalIntakeEnabled) Counts = await queries.GetCountsAsync(cancellationToken);
            CaseCounts = await caseQueries.GetQueueCountsAsync(actor, cancellationToken);
            TriageOpenCount = await triageQueries.GetOpenCountAsync(actor, cancellationToken);
        }
        catch (Exception)
        {
            Error = "Operational counts are temporarily unavailable. Retry the page.";
        }
        LoadedAtUtc = DateTimeOffset.UtcNow;
    }
}
