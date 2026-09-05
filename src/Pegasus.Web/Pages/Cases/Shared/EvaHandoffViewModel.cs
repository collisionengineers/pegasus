using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Cases;

public sealed record EvaHandoffViewModel(
    Guid CaseId,
    long ExpectedVersion,
    CaseLifecycleState State,
    string? EditLeaseToken,
    string EngineerDisplayName,
    IReadOnlyList<EvaHandoffEngineerOption> EngineerOptions,
    string SignOffEngineerDisplayName,
    Guid? SignOffEngineerId,
    IReadOnlyList<EvaHandoffEngineerOption> SignOffEngineerOptions,
    bool InstructionsComplete,
    bool ImagesComplete,
    bool ApiComposed,
    bool ApiEnabled,
    string ExportOperationKey,
    string SubmitOperationKey);

public sealed record EvaHandoffEngineerOption(Guid Id, string Name);
