using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration.Automation;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ActivityModel(IListAutomationActivity listActivity)
    : AdministrationPageModel
{
    private const int PageSize = 50;

    [BindProperty(SupportsGet = true, Name = "correlationId")]
    public string? CorrelationId { get; set; }

    public int CurrentPage { get; private set; } = 1;

    public ListAutomationActivityResult Result { get; private set; } =
        new([], null, 1, PageSize, false, false);

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "page")] int? pageNumber,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        CurrentPage = Math.Max(1, pageNumber ?? 1);
        var correlationId = CorrelationId?.Trim();
        if (correlationId is { Length: > 100 })
        {
            // A typo in a filter box is not a missing page. It used to return
            // a raw browser 404 for one character too many.
            ModelState.AddModelError(
                nameof(CorrelationId),
                "That reference is too long. Use 100 characters or fewer.");
            correlationId = null;
        }

        Result = await listActivity.ExecuteAsync(
            new(
                actor,
                string.IsNullOrEmpty(correlationId) ? null : correlationId,
                CurrentPage,
                PageSize),
            cancellationToken);
        return Page();
    }

    public static string RecordTypeLabel(AutomationActivityRecordType recordType) =>
        recordType switch
        {
            AutomationActivityRecordType.ActionHistory => "Action",
            AutomationActivityRecordType.SecurityEvent => "Security event",
            _ => throw new InvalidOperationException(
                $"Unknown automation activity record type '{(int)recordType}'.")
        };
}
