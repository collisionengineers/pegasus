using Pegasus.Core.Cases;

namespace Pegasus.Core.Intake;

/// <summary>
/// What a received message is: one of four categories (operator, 27 October
/// 2026; Pre-Pegasus case added 30 October 2026, R550). Every other message
/// has no category: linked, it is Case correspondence; unlinked, it waits in
/// No Case Found. A message nothing has decided yet is Unclassified, not a
/// category. Sent mail carries no category. FRD-08 owns what each one means.
/// </summary>
public enum MailCategory
{
    NewInstruction,
    TriageRequest,
    Cancellation,

    /// <summary>
    /// Mail about a case still run in the old system, which has no Case in
    /// Pegasus. No predicate produces it: staff, the Automation actor or the
    /// classifier agent set it by correction. It leaves No Case Found.
    /// </summary>
    PrePegasusCase
}

/// <summary>
/// The one stored and keyed form of each category. The same code is the
/// persisted value, the Inbox filter key and the MCP correction value.
/// </summary>
public static class MailCategories
{
    public static IReadOnlyList<MailCategory> All { get; } = Enum.GetValues<MailCategory>();

    public static string Code(MailCategory category) => category switch
    {
        MailCategory.NewInstruction => "new-instruction",
        MailCategory.TriageRequest => "triage-request",
        MailCategory.Cancellation => "cancellation",
        MailCategory.PrePegasusCase => "pre-pegasus-case",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };

    public static bool TryParse(string? code, out MailCategory category)
    {
        foreach (var candidate in All)
        {
            if (string.Equals(Code(candidate), code, StringComparison.Ordinal))
            {
                category = candidate;
                return true;
            }
        }
        category = default;
        return false;
    }

    public static MailCategory Parse(string code) =>
        TryParse(code, out var category)
            ? category
            : throw new ArgumentException($"'{code}' is not a mail category.", nameof(code));
}

public enum MailClassificationOutcome
{
    Classified,
    Unclassified
}

public sealed record MailClassificationPredicateResult(
    string Key,
    bool Matched,
    string Detail);

public sealed record StandaloneAuditReportEvaluation(
    string AssetSourceLabel,
    AuditAssessment Assessment);

/// <summary>
/// The recorded outcome of one classification evaluation. When predicates for
/// more than one category match, the outcome is Unclassified and the reason
/// names the candidates: no winner is invented. A reply carries the category of
/// what it replies about, with <paramref name="IsReplyContext"/> set.
/// <paramref name="OpensQuery"/> is a deterministic query criterion, not a
/// category: linking the message to a Case whose report is sent opens a query.
/// </summary>
public sealed record MailClassificationResult(
    MailClassificationOutcome Outcome,
    MailCategory? Category,
    IReadOnlyList<MailClassificationPredicateResult> Predicates,
    string Reason,
    string PolicyKey,
    int PolicyVersion,
    CaseType? CaseType = null,
    StandaloneAuditReportEvaluation? StandaloneAuditReport = null,
    bool IsReplyContext = false,
    bool OpensQuery = false)
{
    public bool IsClassified => Outcome == MailClassificationOutcome.Classified && Category is not null;

    public bool Is(MailCategory category) => IsClassified && Category == category;

    public bool IsTriageRequest => Is(MailCategory.TriageRequest);

    public static MailClassificationResult Classified(
        MailCategory category,
        IReadOnlyList<MailClassificationPredicateResult> predicates,
        string reason,
        string policyKey,
        int policyVersion,
        CaseType? caseType = null,
        StandaloneAuditReportEvaluation? standaloneAuditReport = null,
        bool isReplyContext = false,
        bool opensQuery = false)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category), category, "The mail category is not recognized.");
        }
        return new(
            MailClassificationOutcome.Classified,
            category,
            predicates,
            reason,
            policyKey,
            policyVersion,
            caseType,
            standaloneAuditReport,
            isReplyContext,
            opensQuery);
    }

    public static MailClassificationResult Unclassified(
        IReadOnlyList<MailClassificationPredicateResult> predicates,
        string reason,
        string policyKey,
        int policyVersion,
        bool opensQuery = false) =>
        new(
            MailClassificationOutcome.Unclassified,
            null,
            predicates,
            reason,
            policyKey,
            policyVersion,
            OpensQuery: opensQuery);

    /// <summary>
    /// The decision for a received mail message no Principal policy classified:
    /// its sender matched no accepted route, or the route had no policy. Nothing
    /// is classified, but the message still carries a decision, so staff can
    /// correct it and the workspace can list it (FRD-08).
    /// </summary>
    public const string NoPolicyKey = "no_mail_classification_policy";

    public static MailClassificationResult NoPolicy(string reason) =>
        Unclassified([], reason, NoPolicyKey, 1);
}

/// <summary>
/// A route-owned classification policy. The applicable route is the only policy owner for
/// message-type classification (ADR-0008), so a policy names the Principal route it serves
/// and is selected by the accepted route's Principal code.
/// </summary>
public interface IMailClassificationPolicy
{
    string PrincipalCode { get; }
    string PolicyKey { get; }
    int PolicyVersion { get; }
    MailClassificationResult Classify(
        IntakeSourceReadResult readResult,
        IReadOnlyList<IntakeContentFragment>? instructionContent = null);
}
