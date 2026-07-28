using Pegasus.Core.Intake;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Pegasus.Web.Pages;

public class IndexModel(
    IIntakeReceiptQueries queries,
    IConfiguration configuration,
    IWebHostEnvironment environment) : PageModel
{
    public IntakeQueueCounts Counts { get; private set; } = new(0, 0);

    public bool LocalIntakeEnabled { get; private set; }

    public DateTimeOffset LoadedAtUtc { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        LocalIntakeEnabled = environment.IsDevelopment()
            && configuration.GetValue<bool>("Features:LocalIntake");
        if (LocalIntakeEnabled)
        {
            Counts = await queries.GetCountsAsync(cancellationToken);
        }

        LoadedAtUtc = DateTimeOffset.UtcNow;
    }
}
