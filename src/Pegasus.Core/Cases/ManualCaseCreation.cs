using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Cases;

public sealed record CreateManualCaseRequest(
    ActionActor Actor,
    string OperationKey,
    string PrincipalCode,
    CaseType CaseType,
    CaseEditableData Data);

public interface ICreateManualCase
{
    Task<CaseIdentity> ExecuteAsync(CreateManualCaseRequest request, CancellationToken cancellationToken);
}

public interface IManualCaseCreationStore
{
    Task<CaseIdentity> CreateAsync(CreateManualCaseRequest request, CancellationToken cancellationToken);
}

public sealed class CreateManualCase(IManualCaseCreationStore store) : ICreateManualCase
{
    public Task<CaseIdentity> ExecuteAsync(CreateManualCaseRequest request, CancellationToken cancellationToken)
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
            data.InstructionDate,
            data.InspectionAddress,
            data.InspectionDate);
        var missingIdentity = InstructionDraftCompleteness.MissingIdentityCriticalFieldNames(draft);
        if (missingIdentity.Count > 0)
        {
            throw new InvalidOperationException(
                $"A manual case needs {string.Join(", ", missingIdentity)}.");
        }
        if (request.CaseType == CaseType.Audit)
        {
            throw new InvalidOperationException(
                "An Audit needs its retained original-report evidence and cannot be created manually.");
        }

        return store.CreateAsync(request with
        {
            OperationKey = request.OperationKey.Trim(),
            PrincipalCode = CasePrincipalCode.Normalize(request.PrincipalCode),
            Data = data
        }, cancellationToken);
    }
}
