using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Cases;

/// <param name="PrincipalCode">
/// The Principal's code, or null for a one-off Case for a private customer:
/// the Case belongs to the standing Private customer record and its Case/PO
/// is the registration (operator, 31 October 2026; R574).
/// </param>
public sealed record CreateManualCaseRequest(
    ActionActor Actor,
    string OperationKey,
    string? PrincipalCode,
    CaseType CaseType,
    CaseEditableData Data)
{
    public bool IsPrivateCustomer => PrincipalCode is null;
}

/// <summary>
/// What a Case created directly must carry before it can be created, by
/// Case type: a Triage its registration; a one-off for a private customer
/// the claimant's name and the registration, with no claim number
/// (operator, 31 October and 1 November 2026; R577, R578); every other Case
/// its identity-critical fields.
/// </summary>
public static class ManualCaseCreationPolicy
{
    public static IReadOnlyList<string> MissingFieldNames(
        CaseType caseType,
        bool privateCustomer,
        InstructionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (caseType == CaseType.Triage)
        {
            return string.IsNullOrWhiteSpace(draft.VehicleRegistration)
                ? ["Vehicle registration"]
                : [];
        }

        return InstructionDraftCompleteness.MissingIdentityCriticalFieldNames(draft, privateCustomer);
    }
}

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
        var missingIdentity = ManualCaseCreationPolicy.MissingFieldNames(
            request.CaseType, request.IsPrivateCustomer, draft);
        if (missingIdentity.Count > 0)
        {
            throw new InvalidOperationException(
                $"A manual case needs {string.Join(", ", missingIdentity)}.");
        }
        // An Audit is never created by hand; a one-off for a private customer
        // is an Inspection or an Inspection + Audit (R577).
        if (request.CaseType is CaseType.Audit
            || (request.IsPrivateCustomer && request.CaseType is CaseType.Triage))
        {
            throw new InvalidOperationException("The case type is invalid.");
        }

        var outcome = await store.CreateAsync(request with
        {
            OperationKey = request.OperationKey.Trim(),
            PrincipalCode = request.PrincipalCode is null
                ? null
                : CasePrincipalCode.Normalize(request.PrincipalCode),
            Data = data
        }, cancellationToken);
        if (outcome.VehicleLookupWorkId is { } vehicleLookupWorkId)
        {
            await committedExternalWorkPublisher.PublishAsync(vehicleLookupWorkId, cancellationToken);
        }

        return outcome.Identity;
    }
}
