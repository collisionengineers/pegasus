using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;

namespace Pegasus.Web.Pages;

public class IndexModel(
    IIntakeReceiptQueries intakeQueries,
    ITriageQueries triageQueries,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    TimeProvider timeProvider) : PageModel
{
    public IntakeQueueCounts Counts { get; private set; } = new(0, 0);
    public int TriageCount { get; private set; }

    public bool LocalIntakeEnabled { get; private set; }

    public DateTimeOffset LoadedAtUtc { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        LocalIntakeEnabled = environment.IsDevelopment()
            && configuration.GetValue<bool>("Features:LocalIntake");
        if (LocalIntakeEnabled)
        {
            Counts = await intakeQueries.GetCountsAsync(cancellationToken);
        }

        TriageCount = (await triageQueries.ListAsync(state: null, cancellationToken)).Count;
        LoadedAtUtc = timeProvider.GetUtcNow();
    }
}
