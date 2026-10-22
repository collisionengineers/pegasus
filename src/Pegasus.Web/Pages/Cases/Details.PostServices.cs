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
    // resolved from the request's own scope on first use, so a POST gets the
    // same scoped instance the constructor once handed it.
    private T PostService<T>() where T : notnull =>
        HttpContext.RequestServices.GetRequiredService<T>();

    private IGetCaseHeader getCaseHeader => PostService<IGetCaseHeader>();
    private IGetCaseEditBasis getCaseEditBasis => PostService<IGetCaseEditBasis>();
    private ICaseWorkflowQueries caseWorkflows => PostService<ICaseWorkflowQueries>();
    private ICreateCaseTask createCaseTask => PostService<ICreateCaseTask>();
    private IAssignCaseTask assignCaseTask => PostService<IAssignCaseTask>();
    private ICompleteCaseTask completeCaseTask => PostService<ICompleteCaseTask>();
    private ICancelCaseTask cancelCaseTask => PostService<ICancelCaseTask>();
    private ICaseDocumentQueries caseDocuments => PostService<ICaseDocumentQueries>();
    private ICreateAiJob createAiJob => PostService<ICreateAiJob>();
    private ISendCaseReport sendCaseReport => PostService<ISendCaseReport>();
    private IDuplicateEstimate duplicateEstimate => PostService<IDuplicateEstimate>();
    private IDiscardEstimate discardEstimate => PostService<IDiscardEstimate>();
    private ISetCurrentEstimate setCurrentEstimate => PostService<ISetCurrentEstimate>();
    private IRepairSpecificationStore repairSpecifications => PostService<IRepairSpecificationStore>();
    private IImportRawEstimate importRawEstimate => PostService<IImportRawEstimate>();
    private ISaveUnroadworthyReason saveUnroadworthyReasonAction => PostService<ISaveUnroadworthyReason>();
    private IScaleRepairSpecification scaleRepairSpecification => PostService<IScaleRepairSpecification>();
    private IRemoveRepairSpecificationScaling removeRepairSpecificationScaling => PostService<IRemoveRepairSpecificationScaling>();
    private IRestoreRepairSpecificationSnapshot restoreRepairSpecificationSnapshot => PostService<IRestoreRepairSpecificationSnapshot>();
    private IAddCaseDocument addCaseDocument => PostService<IAddCaseDocument>();
    private IAcquireCaseEditLease acquireLease => PostService<IAcquireCaseEditLease>();
    private IHeartbeatCaseEditLease heartbeatLease => PostService<IHeartbeatCaseEditLease>();
    private IReleaseCaseEditLease releaseLease => PostService<IReleaseCaseEditLease>();
    private ISaveCaseWorkspace saveCaseWorkspace => PostService<ISaveCaseWorkspace>();
    private ICreateAudit createAudit => PostService<ICreateAudit>();
    private IStartQueryResponse startQueryResponse => PostService<IStartQueryResponse>();
    private IStartMarketResearch startMarketResearch => PostService<IStartMarketResearch>();
    private IPreviewValuationCalculation previewValuation => PostService<IPreviewValuationCalculation>();
    private IGetCaseHasAuditWork getCaseHasAuditWork => PostService<IGetCaseHasAuditWork>();
}
