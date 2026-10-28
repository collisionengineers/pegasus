using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Cases;

public sealed partial class DetailsModel
{
    // The use cases only a POST handler runs. A GET never builds them: each is
    // resolved from the request's own scope on first use and kept on the page
    // model, so a POST gets the same scoped instance the constructor once
    // handed it and looks each service up once.
    private T PostService<T>() where T : notnull =>
        HttpContext.RequestServices.GetRequiredService<T>();

    private IGetCaseHeader getCaseHeader => field ??= PostService<IGetCaseHeader>();
    private IGetCaseEditBasis getCaseEditBasis => field ??= PostService<IGetCaseEditBasis>();
    private ICaseWorkflowQueries caseWorkflows => field ??= PostService<ICaseWorkflowQueries>();
    private ICreateCaseTask createCaseTask => field ??= PostService<ICreateCaseTask>();
    private IAssignCaseTask assignCaseTask => field ??= PostService<IAssignCaseTask>();
    private ICompleteCaseTask completeCaseTask => field ??= PostService<ICompleteCaseTask>();
    private ICancelCaseTask cancelCaseTask => field ??= PostService<ICancelCaseTask>();
    private ICaseDocumentQueries caseDocuments => field ??= PostService<ICaseDocumentQueries>();
    private ICreateAiJob createAiJob => field ??= PostService<ICreateAiJob>();
    private ISendCaseReport sendCaseReport => field ??= PostService<ISendCaseReport>();
    private IDuplicateEstimate duplicateEstimate => field ??= PostService<IDuplicateEstimate>();
    private IDiscardEstimate discardEstimate => field ??= PostService<IDiscardEstimate>();
    private ISetCurrentEstimate setCurrentEstimate => field ??= PostService<ISetCurrentEstimate>();
    private IRepairSpecificationStore repairSpecifications => field ??= PostService<IRepairSpecificationStore>();
    private IImportRawEstimate importRawEstimate => field ??= PostService<IImportRawEstimate>();
    private ISaveUnroadworthyReason saveUnroadworthyReasonAction => field ??= PostService<ISaveUnroadworthyReason>();
    private IScaleRepairSpecification scaleRepairSpecification => field ??= PostService<IScaleRepairSpecification>();
    private RemoveRepairSpecificationScaling removeRepairSpecificationScaling => field ??= PostService<RemoveRepairSpecificationScaling>();
    private RestoreRepairSpecificationSnapshot restoreRepairSpecificationSnapshot => field ??= PostService<RestoreRepairSpecificationSnapshot>();
    private IAddCaseDocument addCaseDocument => field ??= PostService<IAddCaseDocument>();
    private IAcquireCaseEditLease acquireLease => field ??= PostService<IAcquireCaseEditLease>();
    private IHeartbeatCaseEditLease heartbeatLease => field ??= PostService<IHeartbeatCaseEditLease>();
    private IReleaseCaseEditLease releaseLease => field ??= PostService<IReleaseCaseEditLease>();
    private ISaveCaseWorkspace saveCaseWorkspace => field ??= PostService<ISaveCaseWorkspace>();
    private ICreateAudit createAudit => field ??= PostService<ICreateAudit>();
    private IOpenCaseQuery openCaseQuery => field ??= PostService<IOpenCaseQuery>();
    private IMarkCaseQueryDone markCaseQueryDone => field ??= PostService<IMarkCaseQueryDone>();
    private IReopenCaseQuery reopenCaseQuery => field ??= PostService<IReopenCaseQuery>();
    private IAssignCaseQuery assignCaseQuery => field ??= PostService<IAssignCaseQuery>();
    private IStartMarketResearch startMarketResearch => field ??= PostService<IStartMarketResearch>();
    private IPreviewValuationCalculation previewValuation => field ??= PostService<IPreviewValuationCalculation>();
    private GetCaseHasAuditWork getCaseHasAuditWork => field ??= PostService<GetCaseHasAuditWork>();
}
