using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Cases;

/// <summary>The Queries section's posts: open, log, mark done, reopen and assign. No edit lease.</summary>
public sealed partial class DetailsModel
{
    /// <summary>Opens a query on a linked e-mail. No lease, version or reason.</summary>
    public Task<IActionResult> OnPostOpenQueryAsync(
        Guid id,
        Guid retainedMessageId,
        string operationKey,
        string? section,
        CancellationToken cancellationToken) =>
        PostQueryAsync(
            id,
            operationKey,
            section is "queries" ? "queries" : "files",
            actor => openCaseQuery.ExecuteAsync(
                new(id, actor, operationKey, retainedMessageId, null, null),
                cancellationToken),
            CaseWorkspaceLabels.Queries.Opened);

    /// <summary>Logs a query that reached the office outside Pegasus's mail.</summary>
    public Task<IActionResult> OnPostLogQueryAsync(
        Guid id,
        string operationKey,
        CaseQueryChannel channel,
        string? details,
        CancellationToken cancellationToken) =>
        PostQueryAsync(
            id,
            operationKey,
            "queries",
            actor => openCaseQuery.ExecuteAsync(
                new(id, actor, operationKey, null, channel, details),
                cancellationToken),
            CaseWorkspaceLabels.Queries.Logged);

    /// <summary>Marks one open query done.</summary>
    public Task<IActionResult> OnPostMarkQueryDoneAsync(
        Guid id,
        Guid queryId,
        string operationKey,
        CancellationToken cancellationToken) =>
        PostQueryAsync(
            id,
            operationKey,
            "queries",
            actor => markCaseQueryDone.ExecuteAsync(new(queryId, actor, operationKey), cancellationToken),
            CaseWorkspaceLabels.Queries.MarkedDone);

    /// <summary>Reopens one Done query.</summary>
    public Task<IActionResult> OnPostReopenQueryAsync(
        Guid id,
        Guid queryId,
        string operationKey,
        CancellationToken cancellationToken) =>
        PostQueryAsync(
            id,
            operationKey,
            "queries",
            actor => reopenCaseQuery.ExecuteAsync(new(queryId, actor, operationKey), cancellationToken),
            CaseWorkspaceLabels.Queries.Reopened);

    /// <summary>
    /// Assigns one open query. <paramref name="assignee"/> is a staff id or
    /// <c>unassigned</c>. Take posts the viewer's own id.
    /// </summary>
    public Task<IActionResult> OnPostAssignQueryAsync(
        Guid id,
        Guid queryId,
        string operationKey,
        string? assignee,
        CancellationToken cancellationToken)
    {
        Guid? assigneeId = null;
        if (!string.IsNullOrWhiteSpace(assignee) && !string.Equals(assignee, "unassigned", StringComparison.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(assignee, out var parsed) || parsed == Guid.Empty)
            {
                TempData["CaseError"] = CaseWorkspaceLabels.Queries.AssigneeRequired;
                return Task.FromResult<IActionResult>(RedirectToSection(id, "queries"));
            }

            assigneeId = parsed;
        }

        return PostQueryAsync(
            id,
            operationKey,
            "queries",
            actor => assignCaseQuery.ExecuteAsync(new(queryId, actor, operationKey, assigneeId), cancellationToken),
            CaseWorkspaceLabels.Queries.Assigned);
    }

    private async Task<IActionResult> PostQueryAsync(
        Guid id,
        string operationKey,
        string section,
        Func<ActionActor, Task> act,
        string done)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (!IsOperationKeyValid(operationKey))
        {
            TempData["CaseError"] = "The form has expired. Retry the operation.";
            return RedirectToSection(id, section);
        }

        try
        {
            await act(actor);
            TempData["CaseStatus"] = done;
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is
            ArgumentException or InvalidOperationException or KeyNotFoundException or CaseOperationConflictException)
        {
            TempData["CaseError"] = exception.Message;
        }

        return RedirectToSection(id, section);
    }
}
