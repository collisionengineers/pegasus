using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Intake;

/// <summary>
/// Validates the caller-independent acceptance boundary before the persistence implementation
/// executes its single case, intake-link, history and custody-outbox transaction.
/// </summary>
public sealed class AcceptIntake(
    ICaseAcceptanceStore acceptanceStore,
    ICaseWorkflowConfiguration configuration,
    IProviderInspectionModeStore inspectionModeStore,
    ICommittedExternalWorkPublisher committedExternalWorkPublisher,
    IImageIntakeCasePairing? imageIntakeCasePairing = null) : IAcceptIntake
{
    public async Task<CaseAcceptanceOutcome> ExecuteAsync(
        AcceptIntakeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(acceptanceStore);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(request.Completeness);
        ArgumentNullException.ThrowIfNull(request.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PrincipalCode);
        if (request.Actor.Kind is not (ActorKind.Staff or ActorKind.SystemWorker))
        {
            throw new ArgumentException(
                "Intake acceptance requires a staff or system-worker actor.",
                nameof(request));
        }
        var reason = request.Reason.Trim();
        if (reason.Length > 500)
        {
            throw new ArgumentException(
                "The intake acceptance reason must be 500 characters or fewer.",
                nameof(request));
        }

        if (request.ReceiptId == Guid.Empty)
        {
            throw new ArgumentException("An intake receipt is required for case acceptance.", nameof(request));
        }

        if (request.ExpectedVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The expected intake version cannot be negative.");
        }

        if (!Enum.IsDefined(request.CaseType))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The case type is invalid.");
        }

        var principalCode = CasePrincipalCode.Normalize(request.PrincipalCode);
        if (request.CaseType == CaseType.Audit
            && request.StandaloneAuditEvidenceId is null)
        {
            throw new ArgumentException(
                "A standalone Audit requires its retained original-report evidence before identity allocation.",
                nameof(request));
        }
        if (request.StandaloneAuditEvidenceId == Guid.Empty)
        {
            throw new ArgumentException(
                "The standalone Audit evidence identity is invalid.",
                nameof(request));
        }
        if (request.CaseType != CaseType.Audit && request.StandaloneAuditEvidenceId is not null)
        {
            throw new ArgumentException(
                "Standalone Audit evidence can be linked only to a standalone Audit case.",
                nameof(request));
        }
        if (request.AllocationAttemptId.HasValue != request.AllocationCompletedAtUtc.HasValue
            || request.AllocationAttemptId == Guid.Empty)
        {
            throw new ArgumentException(
                "Allocation outcome identity and completion time must be supplied together.",
                nameof(request));
        }

        // CASE-013: the pipeline's own allocation runs under a system-worker
        // actor and only for a receipt already decided definitive, which is
        // what "automatically definitive" means. Staff acceptance is the other
        // caller and is never exempt.
        var automaticallyDefinitive = request.Actor.Kind == ActorKind.SystemWorker;
        var completenessEvaluation = CaseCompletenessPolicy.EvaluateAcceptanceCommand(
            request.Completeness,
            await configuration.GetCurrentAsync(cancellationToken),
            automaticallyDefinitive);
        var providerInspectionMode = await inspectionModeStore.GetForPrincipalAsync(
                principalCode,
                cancellationToken)
            ?? CaseInspectionMode.PhysicalAddress;

        var acceptance = new CaseAcceptanceRequest(
            request.ReceiptId,
            request.ExpectedVersion,
            request.Actor,
            request.OperationKey.Trim(),
            reason,
            request.CaseType,
            principalCode,
            request.Completeness,
            completenessEvaluation,
            providerInspectionMode,
            request.StandaloneAuditEvidenceId,
            request.AcceptedInspectionDeadline,
            request.AllocationAttemptId,
            request.AllocationCompletedAtUtc);

        var outcome = await acceptanceStore.AcceptAsync(acceptance, cancellationToken);
        _ = CaseInitialWorkflowState.From(outcome.InitialState);
        if (!outcome.IsDuplicate)
        {
            await committedExternalWorkPublisher.PublishAsync(
                outcome.CustodyWorkId,
                cancellationToken);
        }
        if (!outcome.IsDuplicate && imageIntakeCasePairing is not null)
        {
            try
            {
                await imageIntakeCasePairing.PairAcceptedCaseAsync(
                    outcome.Identity.CaseId,
                    cancellationToken);
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                // Reverse image-intake pairing is advisory: the accepted case
                // stands and staff pairing remains available.
            }
        }

        return outcome;
    }
}
