using System.Collections.Immutable;
using Pegasus.Core.Cases;

namespace Pegasus.Core.Intake;

/// <summary>
/// The settled mailbox taxonomy (requirements: settled mailbox taxonomy and correction).
/// A category is a classification fact only: it carries no application queue, Triage
/// routing, or Outlook folder destination, which are separate facts.
/// </summary>
public enum MailDirection
{
    Received,
    Sent
}

public enum ReceivedMailFamily
{
    General,
    Billing,
    NewInstructionReceived,
    NonClientRelated,
    InProgressCases,
    PostReportEmails,
    PreInstructionEmails,
    InternalCc
}

public enum SentMailFamily
{
    ReportSent,
    CaseRejected,
    QuerySent,
    AdditionalImageRequest
}

public static class MailTaxonomy
{
    public static readonly ImmutableDictionary<ReceivedMailFamily, ImmutableArray<string>> ConfirmedReceivedSubtypes =
        new Dictionary<ReceivedMailFamily, ImmutableArray<string>>
        {
            [ReceivedMailFamily.General] =
                ["autoreply", "undeliverable", "general-chase", "case-summary"],
            [ReceivedMailFamily.Billing] =
                ["billing-query", "general-billing"],
            [ReceivedMailFamily.NewInstructionReceived] =
                ["audit", "diminution", "inspection", "new-client", "website-enquiry"],
            [ReceivedMailFamily.NonClientRelated] = [],
            [ReceivedMailFamily.InProgressCases] =
                ["cancellation", "case-update", "client-chasing-for-update", "provider-chasing-for-update"],
            [ReceivedMailFamily.PostReportEmails] = [],
            [ReceivedMailFamily.PreInstructionEmails] = [],
            [ReceivedMailFamily.InternalCc] = []
        }.ToImmutableDictionary();

    public static string CategoryName(ReceivedMailFamily family) => family switch
    {
        ReceivedMailFamily.General => "General",
        ReceivedMailFamily.Billing => "billing",
        ReceivedMailFamily.NewInstructionReceived => "new-instruction-received",
        ReceivedMailFamily.NonClientRelated => "non-client-related",
        ReceivedMailFamily.InProgressCases => "in-progress-cases",
        ReceivedMailFamily.PostReportEmails => "post-report-emails",
        ReceivedMailFamily.PreInstructionEmails => "pre-instruction-emails",
        ReceivedMailFamily.InternalCc => "internal-cc",
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, null)
    };

    public static string CategoryName(SentMailFamily family) => family switch
    {
        SentMailFamily.ReportSent => "Report sent",
        SentMailFamily.CaseRejected => "case-rejected",
        SentMailFamily.QuerySent => "query-sent",
        SentMailFamily.AdditionalImageRequest => "additional-image-request",
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, null)
    };

    public static ReceivedMailFamily ParseReceivedFamily(string name) =>
        Enum.GetValues<ReceivedMailFamily>()
            .Cast<ReceivedMailFamily?>()
            .FirstOrDefault(family => CategoryName(family!.Value) == name)
        ?? throw new ArgumentException(
            $"'{name}' is not a settled Received family name.", nameof(name));

    public static SentMailFamily ParseSentFamily(string name) =>
        Enum.GetValues<SentMailFamily>()
            .Cast<SentMailFamily?>()
            .FirstOrDefault(family => CategoryName(family!.Value) == name)
        ?? throw new ArgumentException(
            $"'{name}' is not a settled Sent family name.", nameof(name));
}

/// <summary>
/// A validated category. Reply is never a standalone recorded type: a reply mirrors the
/// underlying Received or Sent category and carries reply context. `Other` requires both
/// a new category name and reasoning.
/// </summary>
public sealed record MailCategory
{
    private MailCategory(
        MailDirection direction,
        ReceivedMailFamily? receivedFamily,
        SentMailFamily? sentFamily,
        string? subtype,
        bool isReplyContext,
        string? otherName,
        string? otherReasoning)
    {
        Direction = direction;
        ReceivedFamily = receivedFamily;
        SentFamily = sentFamily;
        Subtype = subtype;
        IsReplyContext = isReplyContext;
        OtherName = otherName;
        OtherReasoning = otherReasoning;
    }

    public MailDirection Direction { get; }
    public ReceivedMailFamily? ReceivedFamily { get; }
    public SentMailFamily? SentFamily { get; }
    public string? Subtype { get; }
    public bool IsReplyContext { get; }
    public string? OtherName { get; }
    public string? OtherReasoning { get; }

    public bool IsOther => OtherName is not null;

    public string Name =>
        OtherName
        ?? (ReceivedFamily is { } received
            ? MailTaxonomy.CategoryName(received)
            : MailTaxonomy.CategoryName(SentFamily!.Value));

    public static MailCategory Received(
        ReceivedMailFamily family,
        string? subtype = null,
        bool isReplyContext = false)
    {
        if (subtype is not null
            && !MailTaxonomy.ConfirmedReceivedSubtypes[family].Contains(subtype, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"'{subtype}' is not a confirmed subtype of the '{MailTaxonomy.CategoryName(family)}' family.",
                nameof(subtype));
        }

        return new(MailDirection.Received, family, null, subtype, isReplyContext, null, null);
    }

    public static MailCategory Sent(SentMailFamily family, bool isReplyContext = false) =>
        new(MailDirection.Sent, null, family, null, isReplyContext, null, null);

    public static MailCategory Other(
        MailDirection direction,
        string name,
        string reasoning)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(reasoning);
        return new(direction, null, null, null, false, name.Trim(), reasoning.Trim());
    }
}

public enum MailClassificationOutcome
{
    Classified,
    Ambiguous,
    Unclassified
}

public sealed record MailClassificationPredicateResult(
    string Key,
    bool Matched,
    string Detail);

/// <summary>
/// The recorded outcome of one classification evaluation. Ambiguity is an explicit
/// fail-closed outcome: when predicates for more than one family match simultaneously,
/// the candidates are recorded and no winner is invented (open decision: mailbox rule
/// activation owns multi-rule precedence).
/// </summary>
public sealed record MailClassificationResult(
    MailClassificationOutcome Outcome,
    MailCategory? Category,
    IReadOnlyList<string> AmbiguousCandidates,
    IReadOnlyList<MailClassificationPredicateResult> Predicates,
    string Reason,
    string PolicyKey,
    int PolicyVersion,
    CaseType? CaseType = null)
{
    public static MailClassificationResult Classified(
        MailCategory category,
        IReadOnlyList<MailClassificationPredicateResult> predicates,
        string reason,
        string policyKey,
        int policyVersion,
        CaseType? caseType = null) =>
        new(
            MailClassificationOutcome.Classified,
            category,
            [],
            predicates,
            reason,
            policyKey,
            policyVersion,
            caseType);

    public static MailClassificationResult Ambiguous(
        IReadOnlyList<string> candidates,
        IReadOnlyList<MailClassificationPredicateResult> predicates,
        string reason,
        string policyKey,
        int policyVersion) =>
        new(MailClassificationOutcome.Ambiguous, null, candidates, predicates, reason, policyKey, policyVersion);

    public static MailClassificationResult Unclassified(
        IReadOnlyList<MailClassificationPredicateResult> predicates,
        string reason,
        string policyKey,
        int policyVersion) =>
        new(MailClassificationOutcome.Unclassified, null, [], predicates, reason, policyKey, policyVersion);
}

/// <summary>
/// A route-owned classification policy. The applicable route is the only policy owner for
/// message-type classification (ADR-0008), so a policy names the provider route it serves
/// and is selected by the accepted route's work-provider code.
/// </summary>
public interface IMailClassificationPolicy
{
    string WorkProviderCode { get; }
    string PolicyKey { get; }
    int PolicyVersion { get; }
    MailClassificationResult Classify(IntakeSourceReadResult readResult);
}
