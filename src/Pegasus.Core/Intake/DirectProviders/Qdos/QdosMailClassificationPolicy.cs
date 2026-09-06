using System.Text.RegularExpressions;
using Pegasus.Core.Cases;

namespace Pegasus.Core.Intake;

/// <summary>
/// QDOS message-type classification over the settled taxonomy, built only on the
/// operator-guaranteed generated tells: Triage tells live in the email or its attached letter and
/// the work-type notification titles live only inside the attached instruction letter. Body
/// keyword matching is deliberately absent — corpus evidence shows "audit" in a body
/// signals an existing case being chased, not a new instruction. When predicates for more
/// than one category match, the result is the recorded Ambiguous outcome, never an
/// invented winner; when none match, the message fails closed as Unclassified.
///
/// MAIL-011/MAIL-012: QDOS sends triage requests in two templates, and the reviewed
/// corpus shows the body-phrase and subject-line templates to be disjoint. Current
/// cohort counts belong to the versioned evidence/evaluation output rather than this
/// policy comment. All supported tells produce one triage candidate.
/// </summary>
public sealed partial class QdosMailClassificationPolicy : IMailClassificationPolicy
{
    public const string Key = "qdos_mail_classification";
    public const int Version = 6;

    private const string TriagePhrase = "Triage Only Request";
    private const string TriageSubjectPrefix = "Engineer Triage";
    private const string AuditNotificationTitle = "AUDIT REPORT NOTIFICATION";
    private const string EngineerNotificationTitle = "ENGINEER NOTIFICATION";
    private const string ReportPlusAuditMarker = "REPORT + AUDIT REPORT";

    public string WorkProviderCode => "QDOS";
    public string PolicyKey => Key;
    public int PolicyVersion => Version;

    public MailClassificationResult Classify(IntakeSourceReadResult readResult)
    {
        ArgumentNullException.ThrowIfNull(readResult);

        var subject = readResult.TransportEvidence
            .FirstOrDefault(item => item.Source == IntakeEvidenceSource.Subject)?.Value ?? string.Empty;
        var bodyTexts = Texts(readResult, IntakeEvidenceSource.EmailBody);
        var documentTexts = readResult.Content
            .Where(fragment => fragment.Source
                is IntakeEvidenceSource.DocumentContent
                or IntakeEvidenceSource.PdfContent)
            .Where(fragment => !IsNestedMessageContent(fragment))
            .Select(fragment => fragment.Text)
            .ToArray();

        var isAutomaticReply = AutomaticReplyRegex().IsMatch(subject);
        var isReplyPrefixed = ReplyPrefixRegex().IsMatch(subject);
        // The tells are generated text with one recorded casing each; the
        // casing is part of what makes them discriminating (a human sentence
        // mentioning "this was a triage only request" is not the tell).
        var hasTriagePhrase = bodyTexts.Any(text =>
            text.Contains(TriagePhrase, StringComparison.Ordinal));
        // The second template's tell is its generated subject line. Anchored
        // past any forward or reply prefix, because every QDOS message reaches
        // us as a staff forward — and because a human writing "about your
        // Engineer Triage query" mid-subject is not the tell, exactly as a
        // human sentence mentioning the body phrase is not.
        var hasTriageSubject = TriageSubjectRegex().IsMatch(subject);
        var hasAttachmentTriage = documentTexts.Any(text =>
            text.Contains(TriagePhrase, StringComparison.Ordinal));
        // One candidate from all accepted tells. Adding a second candidate for the same
        // category would resolve to Ambiguous, so a message carrying both
        // tells would classify worse than one carrying either.
        var isTriageRequest = hasTriagePhrase || hasTriageSubject || hasAttachmentTriage;
        var hasAuditTitle = documentTexts.Any(text =>
            text.Contains(AuditNotificationTitle, StringComparison.Ordinal));
        var hasReportPlusAudit = documentTexts.Any(text =>
            text.Contains(EngineerNotificationTitle, StringComparison.Ordinal)
            && text.Contains(ReportPlusAuditMarker, StringComparison.Ordinal));
        var hasPlainEngineerTitle = documentTexts.Any(text =>
            text.Contains(EngineerNotificationTitle, StringComparison.Ordinal)
            && !text.Contains(ReportPlusAuditMarker, StringComparison.Ordinal));
        var hasEngineerTitle = hasPlainEngineerTitle || hasReportPlusAudit;

        MailClassificationPredicateResult[] predicates =
        [
            new(
                "subject.automatic-reply",
                isAutomaticReply,
                isAutomaticReply
                    ? "The subject carries the generated 'Automatic reply:' prefix."
                    : "The subject carries no 'Automatic reply:' prefix."),
            new(
                "subject.reply-prefix",
                isReplyPrefixed,
                isReplyPrefixed
                    ? "The subject carries a reply prefix; the classification mirrors the underlying category with reply context."
                    : "The subject carries no reply prefix."),
            new(
                "body.triage-only-request",
                hasTriagePhrase,
                hasTriagePhrase
                    ? $"An email body contains the operator-guaranteed phrase '{TriagePhrase}'."
                    : $"No email body contains the phrase '{TriagePhrase}'."),
            new(
                "subject.engineer-triage",
                hasTriageSubject,
                hasTriageSubject
                    ? $"The subject opens with the generated Triage line '{TriageSubjectPrefix}'."
                    : $"The subject does not open with '{TriageSubjectPrefix}'."),
            new(
                "attachment.triage-only-request",
                hasAttachmentTriage,
                hasAttachmentTriage
                    ? $"An attached document contains the generated title '{TriagePhrase}'."
                    : $"No attached document contains the title '{TriagePhrase}'."),
            new(
                "attachment.audit-report-notification",
                hasAuditTitle,
                hasAuditTitle
                    ? $"An attached document contains the generated title '{AuditNotificationTitle}'."
                    : $"No attached document contains the title '{AuditNotificationTitle}'."),
            new(
                "attachment.engineer-notification",
                hasEngineerTitle,
                hasEngineerTitle
                    ? hasReportPlusAudit
                        ? $"An attached document contains the generated title '{EngineerNotificationTitle} ({ReportPlusAuditMarker})'."
                        : $"An attached document contains the generated title '{EngineerNotificationTitle}' without the '{ReportPlusAuditMarker}' marker."
                    : $"No attached document contains the title '{EngineerNotificationTitle}'.")
        ];

        var candidates = new List<ClassificationCandidate>();
        if (isAutomaticReply)
        {
            candidates.Add(new(
                MailCategory.Received(ReceivedMailFamily.General, "autoreply"),
                null));
        }

        if (isTriageRequest)
        {
            candidates.Add(new(
                MailCategory.Received(
                    ReceivedMailFamily.PreInstructionEmails,
                    MailCategory.TriageRequestSubtype,
                    isReplyContext: isReplyPrefixed),
                null));
        }

        if (hasAuditTitle)
        {
            candidates.Add(new(
                MailCategory.Received(
                    ReceivedMailFamily.NewInstructionReceived,
                    "audit",
                    isReplyContext: isReplyPrefixed),
                CaseType.Audit));
        }

        if (hasPlainEngineerTitle)
        {
            candidates.Add(new(
                MailCategory.Received(
                    ReceivedMailFamily.NewInstructionReceived,
                    "inspection",
                    isReplyContext: isReplyPrefixed),
                CaseType.Inspection));
        }

        if (hasReportPlusAudit)
        {
            candidates.Add(new(
                MailCategory.Received(
                    ReceivedMailFamily.NewInstructionReceived,
                    "inspection",
                    isReplyContext: isReplyPrefixed),
                CaseType.InspectionAndAudit));
        }

        if (candidates.Count == 0)
        {
            return MailClassificationResult.Unclassified(
                predicates,
                "No accepted classification predicate matched; the message fails closed for staff review.",
                Key,
                Version);
        }

        if (candidates.Count > 1)
        {
            return MailClassificationResult.Ambiguous(
                candidates
                    .Select(candidate => CandidateName(candidate, candidates))
                    .ToArray(),
                predicates,
                "Predicates for more than one category matched simultaneously; no winner is invented (open decision: mailbox rule activation).",
                Key,
                Version);
        }

        var candidate = candidates[0];
        var category = candidate.Category;
        var caseType = candidate.CaseType;

        var standaloneAuditReport = caseType == CaseType.Audit
            ? EvaluateStandaloneAuditReport(readResult)
            : null;

        return MailClassificationResult.Classified(
            category,
            predicates,
            "Exactly one accepted classification predicate family matched.",
            Key,
            Version,
            caseType,
            standaloneAuditReport);
    }

    private static string CandidateName(
        ClassificationCandidate candidate,
        IReadOnlyList<ClassificationCandidate> candidates)
    {
        var name = candidate.Category.Subtype is null
            ? candidate.Category.Name
            : $"{candidate.Category.Name}/{candidate.Category.Subtype}";
        var sameCategoryCount = candidates.Count(item => item.Category == candidate.Category);
        return sameCategoryCount > 1 && candidate.CaseType is not null
            ? $"{name}/{candidate.CaseType}"
            : name;
    }

    private sealed record ClassificationCandidate(MailCategory Category, CaseType? CaseType);

    private static StandaloneAuditReportEvaluation? EvaluateStandaloneAuditReport(
        IntakeSourceReadResult readResult)
    {
        var attachments = readResult.Content
            .Where(fragment => fragment.Source is IntakeEvidenceSource.DocumentContent or IntakeEvidenceSource.PdfContent)
            .Where(fragment => !IsNestedMessageContent(fragment))
            .Where(fragment => fragment.SourceLabel.Contains(", attachment ", StringComparison.Ordinal))
            .GroupBy(fragment => AssetSourceLabel(fragment.SourceLabel), StringComparer.Ordinal)
            .Select(group => new
            {
                AssetSourceLabel = group.Key,
                HasInstruction = group.Any(fragment => fragment.Text.Contains(AuditNotificationTitle, StringComparison.Ordinal)),
                HasRepairable = group.Any(fragment => ContainsRepairable(fragment.Text)),
                HasTotalLoss = group.Any(fragment => ContainsTotalLoss(fragment.Text))
            })
            .ToArray();

        // An Audit is not inferred from the email body or from a lone
        // notification.  It requires two distinct document attachments: the
        // generated Audit instruction and the original report being audited.
        // The report itself must say one, and only one, of the two outcomes.
        if (attachments.Length < 2 || attachments.Count(group => group.HasInstruction) != 1)
        {
            return null;
        }

        var outcomes = attachments
            .Where(group => !group.HasInstruction && group.HasRepairable != group.HasTotalLoss)
            .ToArray();

        return outcomes.Length == 1
            ? new(
                outcomes[0].AssetSourceLabel,
                outcomes[0].HasRepairable ? AuditAssessment.Repairable : AuditAssessment.TotalLoss)
            : null;
    }

    private static string AssetSourceLabel(string sourceLabel)
    {
        var pageIndex = sourceLabel.IndexOf(", page ", StringComparison.Ordinal);
        return pageIndex < 0 ? sourceLabel : sourceLabel[..pageIndex];
    }

    private static bool ContainsRepairable(string text) =>
        RepairableLiteralRegex().IsMatch(text)
        && !NegatedRepairableLiteralRegex().IsMatch(text);

    private static bool ContainsTotalLoss(string text) =>
        TotalLossLiteralRegex().IsMatch(text)
        && !NegatedTotalLossLiteralRegex().IsMatch(text);

    private static string[] Texts(
        IntakeSourceReadResult readResult,
        IntakeEvidenceSource source) =>
        readResult.Content
            .Where(fragment => fragment.Source == source)
            .Where(fragment => !IsNestedMessageContent(fragment))
            .Select(fragment => fragment.Text)
            .ToArray();

    /// <summary>
    /// A tell counts only in the received message itself. The reader labels
    /// every fragment that came out of an attached message — and everything
    /// beneath it — with an ", attached email N" segment, so a forwarded or
    /// quoted original instruction inside a chaser never re-classifies the
    /// chaser as a new instruction.
    /// </summary>
    private static bool IsNestedMessageContent(IntakeContentFragment fragment) =>
        fragment.SourceLabel.Contains(", attached email ", StringComparison.Ordinal);

    [GeneratedRegex(@"^\s*Automatic reply\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AutomaticReplyRegex();

    // A reply reaches us behind the forward, not in front of it. The comment
    // on TriageSubjectRegex already records why — "every QDOS message reaches
    // us as a staff forward" — so the ordinary shape of a reply in this
    // mailbox is "FW: RE: ...", and an anchor that only accepts a leading
    // "RE:" reads that as a brand-new message. For a Triage request the
    // consequence was a second Triage opened for ordinary thread
    // correspondence, which is the duplicate the reply-context gate exists to
    // prevent (INTK-033 review).
    //
    // Matching a reply anywhere in the chain needs no new anchor shape: any
    // chain containing "RE:" either opens with it, or reaches it past
    // forwards. Written with the same must-consume-a-literal iteration as
    // TriageSubjectRegex, so it inherits that regex's linear-time argument
    // rather than restating it.
    [GeneratedRegex(
        @"^\s*(?:(?i:FW|FWD)\s*:\s*)*(?i:RE)\s*:",
        RegexOptions.CultureInvariant)]
    private static partial Regex ReplyPrefixRegex();

    // The forward and reply prefixes are matched case-insensitively because
    // mail clients disagree about them; "Engineer Triage" is not, because the
    // casing of a generated line is part of what makes it discriminating.
    //
    // Leading whitespace is consumed once, before the group, and never inside
    // it. Written as `(?:\s*(?:RE|FW|FWD)\s*:\s*)*` the trailing \s* of one
    // iteration and the leading \s* of the next match the same spaces, so a
    // subject that ultimately fails to match has exponentially many parses to
    // enumerate: "Re:  " twenty times — an 85-character subject — took over
    // five seconds here, against zero for this form. Every iteration must now
    // consume a literal prefix, so the match is linear. The subject is
    // third-party input from an approved mailbox, and this runs on every
    // received message.
    [GeneratedRegex(
        @"^\s*(?:(?i:RE|FW|FWD)\s*:\s*)*Engineer Triage\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex TriageSubjectRegex();

    // A word occurrence is not automatically a report outcome: "unrepairable",
    // "not repairable", and "not a total loss" must never allocate a permanent
    // Audit identity. The report is accepted only on an unnegated literal.
    [GeneratedRegex(@"\brepairable\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RepairableLiteralRegex();

    [GeneratedRegex(@"\b(?:not|no)\b(?:\s+(?:a|the))?[\s-]+repairable\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NegatedRepairableLiteralRegex();

    [GeneratedRegex(@"\btotal[\s-]+loss\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TotalLossLiteralRegex();

    [GeneratedRegex(@"\b(?:not|no)\b(?:\s+(?:a|the))?[\s-]+total[\s-]+loss\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NegatedTotalLossLiteralRegex();
}
