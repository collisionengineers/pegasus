using Pegasus.Core.Workflow;
using Pegasus.Core.Reports;

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
    PrincipalReportGenerationPolicy ReportGenerationPolicy,
    bool ApiComposed,
    bool ApiEnabled,
    string ExportOperationKey,
    string SubmitOperationKey,
    bool CanRetryAutomaticFailure = false,
    bool ShowAutomaticFailureNotice = false);

public sealed record EvaHandoffEngineerOption(Guid Id, string Name);
