using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Cases;

public sealed record CreateManualCaseRequest(
    ActionActor Actor,
    string OperationKey,
    string PrincipalCode,
    CaseType CaseType,
    CaseEditableData Data);

/// <summary>
/// What the creation transaction committed: the new Case's identity and, when
/// the Case was created with an unambiguous registration and lookups are
/// composed, the automatic vehicle-lookup work item that same transaction
/// enqueued. The work item is published after the commit so the Worker starts
/// the lookup immediately instead of waiting for the reconciliation sweep.
/// </summary>
public sealed record ManualCaseCreationOutcome(
    CaseIdentity Identity,
    Guid? VehicleLookupWorkId);

public interface ICreateManualCase
{
    Task<CaseIdentity> ExecuteAsync(CreateManualCaseRequest request, CancellationToken cancellationToken);
}

public interface IManualCaseCreationStore
{
    Task<ManualCaseCreationOutcome> CreateAsync(
        CreateManualCaseRequest request,
        CancellationToken cancellationToken);
}

public sealed class CreateManualCase(
    IManualCaseCreationStore store,
    ICommittedExternalWorkPublisher committedExternalWorkPublisher) : ICreateManualCase
{
    public async Task<CaseIdentity> ExecuteAsync(
        CreateManualCaseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Data);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        if (request.Actor.Kind != ActorKind.Staff) throw new StaffAuthorizationException(StaffAccessRight.PerformCasework);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        if (request.OperationKey.Trim().Length > 100) throw new ArgumentOutOfRangeException(nameof(request));
        if (!Enum.IsDefined(request.CaseType)) throw new ArgumentOutOfRangeException(nameof(request));
        var data = CaseDataPolicy.Normalize(request.Data);
        var draft = new InstructionDraft(
            request.PrincipalCode,
            data.ClaimantName,
            data.ClaimNumber,
            data.VehicleRegistration,
            data.VehicleMake,
            data.VehicleModel,
            data.VehicleMileage,
            data.AccidentCircumstances,
            data.IncidentDate,
            data.InspectionAddress,
            data.InspectionDate);
        // A Triage Case asks only for its Principal and the vehicle
        // registration; every other Case needs the identity-critical fields.
        IReadOnlyList<string> missingIdentity;
        if (request.CaseType == CaseType.Triage)
        {
            missingIdentity = string.IsNullOrWhiteSpace(data.VehicleRegistration)
                ? ["Vehicle registration"]
                : [];
        }
        else
        {
            missingIdentity = InstructionDraftCompleteness.MissingIdentityCriticalFieldNames(draft);
        }
        if (missingIdentity.Count > 0)
        {
            throw new InvalidOperationException(
                $"A manual case needs {string.Join(", ", missingIdentity)}.");
        }
        if (request.CaseType is CaseType.Audit)
        {
            throw new InvalidOperationException("The case type is invalid.");
        }

        var outcome = await store.CreateAsync(request with
        {
            OperationKey = request.OperationKey.Trim(),
            PrincipalCode = CasePrincipalCode.Normalize(request.PrincipalCode),
            Data = data
        }, cancellationToken);
        if (outcome.VehicleLookupWorkId is { } vehicleLookupWorkId)
        {
            await committedExternalWorkPublisher.PublishAsync(vehicleLookupWorkId, cancellationToken);
        }

        return outcome.Identity;
    }
}
