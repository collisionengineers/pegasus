using Pegasus.Core.Workflow;

namespace Pegasus.Core.Cases;

/// <summary>
/// Which work of a Case its per-work data belongs to. Every Case has its
/// primary work; an Inspection + Audit Case gains an Audit work at Create
/// audit, which then drives the Case.
/// </summary>
public enum CaseWorkKind
{
    Primary,
    Audit
}

/// <summary>
/// Which work a read addresses. <see cref="Current"/> (the default) is the
/// work the Case is on — the Audit once one exists; <see cref="Primary"/>
/// reads the Inspection's own data for the read-only Inspection view.
/// </summary>
public enum CaseWorkSelector
{
    Current = 0,
    Primary = 1
}

/// <summary>
/// One work of a Case. The primary work's id is the Case id. The primary work
/// keeps the Inspection report's approval and sent evidence once an Audit
/// work takes over the workflow.
/// </summary>
public sealed record CaseWork(
    Guid Id,
    Guid CaseId,
    CaseWorkKind Kind,
    DateTimeOffset CreatedAtUtc,
    ReportApprovalEvidence? ReportApproval = null,
    ApprovedMailboxReportSentEvidence? ReportSentEvidence = null);

/// <summary>The works of one Case: its primary work and, at most, one Audit work.</summary>
public sealed record CaseWorkSet(CaseWork Primary, CaseWork? Audit)
{
    public CaseWork Current => Audit ?? Primary;

    public bool HasAudit => Audit is not null;

    public CaseWork Select(CaseWorkSelector selector) =>
        selector == CaseWorkSelector.Primary ? Primary : Current;
}

public static class CaseWorkPolicy
{
    /// <summary>
    /// A report is an Audit report when it is made on a standalone Audit Case
    /// or from the Audit work of an Inspection + Audit Case.
    /// </summary>
    public static bool IsAuditReport(CaseType caseType, CaseWorkKind workKind) =>
        caseType == CaseType.Audit || workKind == CaseWorkKind.Audit;
}
