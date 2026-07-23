using CollisionSpike.Core.Intake.Qdos;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CollisionSpike.Web.Pages;

public class IndexModel(
    IQdosIntakeQueries queries,
    IConfiguration configuration,
    IWebHostEnvironment environment) : PageModel
{
    public QdosQueueCounts Counts { get; private set; } = new(0, 0);

    public bool LocalQdosIntakeEnabled { get; private set; }

    public DateTimeOffset LoadedAtUtc { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        LocalQdosIntakeEnabled = environment.IsDevelopment()
            && configuration.GetValue<bool>("Features:LocalQdosIntake");
        if (LocalQdosIntakeEnabled)
        {
            Counts = await queries.GetCountsAsync(cancellationToken);
        }

        LoadedAtUtc = DateTimeOffset.UtcNow;
    }
}
