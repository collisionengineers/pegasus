using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The ports a Triage Case's record uses on <c>/Cases/{id}</c>, gathered so the
/// Case record's handlers take them as one <c>[FromServices]</c> argument
/// rather than widening the Case record's constructor. The optional ports are
/// the composition-gated mail seams the Triage chaser uses.
/// </summary>
public sealed record TriageCasePorts(
    IGetTriage GetTriage,
    IGetCaseHeader GetCaseHeader,
    CaseEditLeases CaseLeases,
    IAssignTriage Assign,
    UnassignTriage Unassign,
    IRecordTriageFinding RecordFinding,
    ISupersedeTriageFinding SupersedeFinding,
    ILinkTriageResponseEvidence LinkResponseEvidence,
    IUnlinkTriageResponseEvidence UnlinkResponseEvidence,
    ICompleteTriage Complete,
    ICancelTriage Cancel,
    IReopenTriage Reopen,
    ISearchCases SearchCases,
    ILinkTriageCase LinkCase,
    IUnlinkTriageCase UnlinkCase,
    IEditScopeLeases EditScopes,
    Pegasus.Web.Presentation.OneCommandEdit OneCommandEdit,
    IGetIntake GetIntake,
    IDescribeCaseEditAuthorityHolder DescribeEditAuthorityHolder,
    ICaseEngineerChoices EngineerChoices,
    AddTriageNote AddNote,
    RenderEmailTemplate RenderTemplate,
    GetRetainedMail? RetainedMail = null,
    IStaffMailSend? StaffMailSend = null,
    IApprovedMailboxStore? ApprovedMailboxes = null,
    IStaffMailAttachmentResolver? AttachmentResolver = null);
