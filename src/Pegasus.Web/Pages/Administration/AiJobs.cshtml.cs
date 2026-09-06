using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class AiJobsModel(
    GetAdministrationAiJobs getJobs,
    ICancelAiJob cancelJob,
    IServiceProvider services) : AdministrationPageModel
{
    public AdministrationAiJobPage Result { get; private set; } = new([], new(0, 0), false, false, false);
    public int CurrentPage { get; private set; } = 1;
    [TempData] public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int page, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        CurrentPage = page == 0 ? 1 : page;
        var transportComposed = services.GetService<IAiHandOffTransport>() is not null;
        try { Result = await getJobs.ExecuteAsync(actor, CurrentPage, transportComposed, cancellationToken); }
        catch (ArgumentOutOfRangeException) { return NotFound(); }
        return Page();
    }

    public async Task<IActionResult> OnPostStopAsync(
        Guid jobId, long expectedVersion, string reason, string operationKey, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (jobId == Guid.Empty || !IsOperationKeyValid(operationKey) || string.IsNullOrWhiteSpace(reason))
        {
            StatusMessage = "The AI job could not be stopped.";
            return RedirectToPage();
        }
        try
        {
            await cancelJob.ExecuteAsync(new(jobId, expectedVersion, actor, operationKey, reason.Trim()), cancellationToken);
            StatusMessage = "The AI job was stopped.";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            StatusMessage = "The AI job changed before it could be stopped.";
        }
        return RedirectToPage();
    }
}
