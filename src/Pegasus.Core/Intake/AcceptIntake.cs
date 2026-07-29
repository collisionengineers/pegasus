using Pegasus.Core.Cases;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Intake;

/// <summary>
/// Validates the caller-independent acceptance boundary before the persistence implementation
/// executes its single case, intake-link, history and custody-outbox transaction.
/// </summary>
public sealed class AcceptIntake(ICaseAcceptanceStore acceptanceStore) : IAcceptIntake
{
    public async Task<CaseAcceptanceOutcome> ExecuteAsync(
        AcceptIntakeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(acceptanceStore);
        ArgumentNullException.ThrowIfNull(request.Completeness);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PrincipalCode);

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

        if (request.StandaloneAuditAssessment is { } assessment && !Enum.IsDefined(assessment))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The audit assessment is invalid.");
        }

        if (request.CaseType == CaseType.Audit && request.StandaloneAuditAssessment is null)
        {
            throw new ArgumentException(
                "A standalone Audit requires an unambiguous assessment before identity allocation.",
                nameof(request));
        }

        if (request.CaseType != CaseType.Audit && request.StandaloneAuditAssessment is not null)
        {
            throw new ArgumentException(
                "Only a standalone Audit may supply an assessment during intake acceptance.",
                nameof(request));
        }

        var acceptance = new CaseAcceptanceRequest(
            request.ReceiptId,
            request.ExpectedVersion,
            request.Actor,
            request.OperationKey,
            request.CaseType,
            request.PrincipalCode,
            request.Completeness,
            request.StandaloneAuditAssessment);

        var outcome = await acceptanceStore.AcceptAsync(acceptance, cancellationToken);
        _ = CaseInitialWorkflowState.From(outcome.InitialState);
        return outcome;
    }
}
