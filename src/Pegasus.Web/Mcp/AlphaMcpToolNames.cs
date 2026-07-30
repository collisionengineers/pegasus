namespace Pegasus.Web.Mcp;

internal static class AlphaMcpToolNames
{
    public const string OperationsGet = "operations.get";
    public const string IntakeList = "intake.list";
    public const string IntakeGet = "intake.get";
    public const string CasesSearch = "cases.search";
    public const string CasesGet = "cases.get";
    public const string TriageList = "triage.list";
    public const string TriageGet = "triage.get";

    public const string IntakeResolve = "intake.resolve";
    public const string IntakeReevaluate = "intake.reevaluate";
    public const string CasesSave = "cases.save";
    public const string CasesAcquireEditLease = "cases.acquire_edit_lease";
    public const string CasesRenewEditLease = "cases.renew_edit_lease";
    public const string CasesReleaseEditLease = "cases.release_edit_lease";
    public const string CasesCreateTask = "cases.create_task";
    public const string CasesAssignTask = "cases.assign_task";
    public const string TriageAssign = "triage.assign";
    public const string TriageUnassign = "triage.unassign";
    public const string TriageRecordFinding = "triage.record_finding";
    public const string TriageSupersedeFinding = "triage.supersede_finding";
    public const string TriageLinkResponse = "triage.link_response";
    public const string TriageUnlinkResponse = "triage.unlink_response";
    public const string TriageLinkCase = "triage.link_case";
    public const string TriageUnlinkCase = "triage.unlink_case";

    public const string IntakeAccept = "intake.accept";
    public const string IntakeLinkCase = "intake.link_case";
    public const string IntakeUnlinkCase = "intake.unlink_case";
    public const string CasesConfirmCompleteness = "cases.confirm_completeness";
    public const string CasesHold = "cases.hold";
    public const string CasesReleaseHold = "cases.release_hold";
    public const string CasesTransition = "cases.transition";
    public const string CasesClose = "cases.close";
    public const string CasesReopen = "cases.reopen";
    public const string CasesArchive = "cases.archive";
    public const string CasesCreateLinkedReplacement = "cases.create_linked_replacement";
    public const string CasesCompleteTask = "cases.complete_task";
    public const string CasesCancelTask = "cases.cancel_task";
    public const string CasesRecordEngineerFinding = "cases.record_engineer_finding";
    public const string TriageComplete = "triage.complete";
    public const string TriageCancel = "triage.cancel";
    public const string TriageReopen = "triage.reopen";
    public const string DocumentsLogicalRemove = "documents.logical_remove";

    public const string DocumentsDownload = "documents.download";
    public const string DocumentsExport = "documents.export";

    public const string RequestsCreateBox = "requests.create_box";
    public const string RequestsRevokeBox = "requests.revoke_box";
    public const string RequestsCreateUpload = "requests.create_upload";
    public const string RequestsRevokeUpload = "requests.revoke_upload";
    public const string VehicleRequestLookup = "vehicle.request_lookup";
    public const string VehicleAcceptSuggestion = "vehicle.accept_suggestion";
    public const string ReportsGenerateEva = "reports.generate_eva";
    public const string ReportsLinkEvidence = "reports.link_evidence";
    public const string ReportsUnlinkEvidence = "reports.unlink_evidence";
}
