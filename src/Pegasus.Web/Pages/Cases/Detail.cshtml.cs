using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Cases;

[Authorize(Roles = "Administrator,Engineer,User")]
public sealed class DetailModel(
    ICaseWorkflowQueries queries,
    ILeaseCaseForEdit leases,
    IPutCaseOnHold hold,
    IReturnCaseToReview returnToReview,
    IAssignCaseEngineer assign,
    IStartCaseWork start,
    IRecordCaseReportApproval approve,
    IRecordCaseReportSent sent,
    ICloseCase close,
    IReopenCase reopen,
    TimeProvider timeProvider) : PageModel
{
    public CaseWorkflowRecord Workflow { get; private set; } = null!;
    public string? LeaseToken { get; private set; }
    public string OperationKey { get; private set; } = Guid.NewGuid().ToString("N");
    public Guid ApprovalId { get; private set; } = Guid.NewGuid();
    public Guid SentEvidenceId { get; private set; } = Guid.NewGuid();
    public DateTimeOffset ActionAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public string? Message { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken) =>
        await LoadAsync(id, cancellationToken) ? Page() : NotFound();

    public async Task<IActionResult> OnPostClaimLeaseAsync(Guid id, long expectedVersion, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        try
        {
            var lease = await leases.ClaimAsync(new(id, expectedVersion, actor, Guid.NewGuid().ToString("N")), cancellationToken);
            LeaseToken = lease.Token;
            Message = $"Edit lease claimed until {lease.ExpiresAtUtc:u}.";
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            Message = exception.Message;
        }
        return await LoadAsync(id, cancellationToken) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostActionAsync(
        Guid id,
        string actionName,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        Guid? engineerId,
        Guid approvalId,
        Guid sentEvidenceId,
        DateTimeOffset actionAtUtc,
        string? artifactIdentity,
        string? artifactSha256,
        string? mailboxIdentity,
        string? sentFolderIdentity,
        string? immutableItemIdentity,
        string? conversationIdentity,
        string? replyChainIdentity,
        DateTimeOffset? sentAtUtc,
        CaseClosureOutcome? outcome,
        Guid? replacementCaseId,
        CaseReopenDestination? destination,
        bool instructionsComplete,
        bool imagesComplete,
        bool instructionsReviewedByStaff,
        bool imagesReviewedByStaff,
        string? evidenceReference,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        OperationKey = operationKey;
        ApprovalId = approvalId;
        SentEvidenceId = sentEvidenceId;
        ActionAtUtc = actionAtUtc;
        try
        {
            var readiness = new CaseReadinessEvidence(
                instructionsComplete,
                imagesComplete,
                instructionsReviewedByStaff,
                imagesReviewedByStaff,
                evidenceReference ?? string.Empty);
            Workflow = actionName switch
            {
                "hold" => await hold.ExecuteAsync(new(id, expectedVersion, actor, operationKey, reason, editLeaseToken, actionAtUtc), cancellationToken),
                "return" => await returnToReview.ExecuteAsync(new(id, expectedVersion, actor, operationKey, reason, editLeaseToken, readiness), cancellationToken),
                "assign" => await assign.ExecuteAsync(new(id, expectedVersion, actor, operationKey, reason, editLeaseToken, engineerId ?? Guid.Empty, readiness), cancellationToken),
                "start" => await start.ExecuteAsync(new ChangeCaseStateRequest(id, expectedVersion, actor, operationKey, reason, editLeaseToken), cancellationToken),
                "approve" => await approve.ExecuteAsync(new(id, expectedVersion, actor, operationKey, reason, editLeaseToken,
                    new ReportApprovalEvidence(approvalId, artifactIdentity ?? string.Empty, artifactSha256 ?? string.Empty, actor, actionAtUtc)), cancellationToken),
                "sent" => await sent.ExecuteAsync(new(id, expectedVersion, actor, operationKey, reason, editLeaseToken,
                    new ApprovedMailboxReportSentEvidence(sentEvidenceId, mailboxIdentity ?? string.Empty, sentFolderIdentity ?? string.Empty,
                        immutableItemIdentity ?? string.Empty, conversationIdentity ?? string.Empty, replyChainIdentity ?? string.Empty,
                        sentAtUtc ?? default, actionAtUtc, actor)), cancellationToken),
                "close" => await close.ExecuteAsync(new(id, expectedVersion, actor, operationKey, reason, editLeaseToken,
                    outcome ?? throw new ArgumentException("A closure outcome is required."), replacementCaseId), cancellationToken),
                "reopen" => await reopen.ExecuteAsync(new(id, expectedVersion, actor, operationKey, reason, editLeaseToken,
                    destination ?? throw new ArgumentException("A reopen destination is required."),
                    destination is CaseReopenDestination.Review or CaseReopenDestination.Active ? readiness : null), cancellationToken),
                _ => throw new ArgumentException("The requested case action is not supported.")
            };
            Message = "Case workflow updated.";
            OperationKey = Guid.NewGuid().ToString("N");
            ApprovalId = Guid.NewGuid();
            SentEvidenceId = Guid.NewGuid();
            ActionAtUtc = timeProvider.GetUtcNow();
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            LeaseToken = editLeaseToken;
            Message = exception.Message;
        }

        if (Workflow is null && !await LoadAsync(id, cancellationToken)) return NotFound();
        return Page();
    }

    private async Task<bool> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var workflow = await queries.GetAsync(id, cancellationToken);
        if (workflow is null) return false;
        Workflow = workflow;
        return true;
    }

    private bool TryGetActor(out ActionActor actor)
    {
        if (StaffActorFactory.TryCreate(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var resolved))
        {
            actor = resolved;
            return true;
        }

        actor = null!;
        return false;
    }

    private static bool IsExpected(Exception exception) => exception is
        ArgumentException or InvalidOperationException or KeyNotFoundException;
}
