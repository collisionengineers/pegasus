using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.AiWork;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Queries section's AI draft members (FRD-27): the Case's AI jobs, each
/// open mail query's live Query response job, whether a row may start one, and
/// Draft reply with AI on a row, which calls the one Core owner of starting a
/// Query response job (<see cref="IStartQueryResponse"/>).
/// </summary>
public sealed partial class DetailsModel
{
    /// <summary>
    /// The Case's AI jobs, read once for the Next action's drafts, the
    /// Valuation section's pending research and the Queries rows' live
    /// Query response jobs.
    /// </summary>
    public IReadOnlyList<AiJobRecord> CaseAiJobs { get; private set; } = [];

    /// <summary>
    /// Why a Queries row withholds **Draft reply with AI**, or null when the
    /// row offers it: the same answer the message record gives.
    /// </summary>
    public string? QueryDraftCondition { get; private set; }

    /// <summary>The live Query response job for one open mail query's message, as the Queries row draws it.</summary>
    public QueryDraft? QueryDraftFor(Guid retainedMessageId) =>
        QueryDraft.For(CaseAiJobs, retainedMessageId, clock.GetUtcNow());

    /// <summary>
    /// What the Queries rows need (FRD-27): the Case's jobs for each open mail
    /// query's live Query response job, and whether a row may start one.
    /// </summary>
    private async Task LoadQueryDraftsAsync(Guid id, CaseQueriesSection section, CancellationToken cancellationToken)
    {
        CaseAiJobs = await aiJobs.ListForSubjectAsync(id, cancellationToken);
        QueryDraftCondition = await QueryDraftConditionAsync(section, cancellationToken);
    }

    // The Send to AI switch is read only when some open mail query has no live
    // job, so a row would offer Draft reply with AI; otherwise no row asks.
    private Task<string?> QueryDraftConditionAsync(CaseQueriesSection section, CancellationToken cancellationToken) =>
        section.Queries.Any(query =>
            query.IsOpen && query.Message is { } message && QueryDraftFor(message.RetainedMessageId) is null)
            ? QueryResponseAvailability.ConditionAsync(section.Frame.Workflow, sendToAiControl, cancellationToken)
            : Task.FromResult<string?>(null);

    /// <summary>
    /// Draft reply with AI on a Queries row (FRD-27): queues a Query response
    /// job for that row's retained message through the same Core command as
    /// the message record's own button. The message must be a Query linked to
    /// this Case; nothing is sent.
    /// </summary>
    public async Task<IActionResult> OnPostDraftQueryReplyAsync(
        Guid id,
        Guid messageId,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!IsOperationKeyValid(operationKey))
        {
            TempData["CaseError"] = "The form has expired. Retry the operation.";
            return RedirectToSection(id, "queries");
        }

        try
        {
            await startQueryResponse.ExecuteAsync(
                new(actor, messageId, operationKey, CaseId: id),
                cancellationToken);
            TempData["CaseStatus"] = Pegasus.Web.Presentation.CaseWorkspaceLabels.Queries.DraftStarted;
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is
            ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            TempData["CaseError"] = exception.Message;
        }

        return RedirectToSection(id, "queries");
    }
}
