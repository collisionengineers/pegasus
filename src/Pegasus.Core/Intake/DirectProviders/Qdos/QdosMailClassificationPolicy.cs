using System.Text.RegularExpressions;
using Pegasus.Core.Cases;

namespace Pegasus.Core.Intake;

/// <summary>
/// QDOS message-type classification over the settled taxonomy, built only on the
/// operator-guaranteed generated tells: the two Triage tells live in the email itself and
/// the work-type notification titles live only inside the attached instruction letter. Body
/// keyword matching is deliberately absent — corpus evidence shows "audit" in a body
/// signals an existing case being chased, not a new instruction. When predicates for more
/// than one category match, the result is the recorded Ambiguous outcome, never an
/// invented winner; when none match, the message fails closed as Unclassified.
///
/// MAIL-011/MAIL-012: QDOS sends triage requests in two templates, and the corpus shows
/// them to be disjoint — seven messages carry the body phrase, five carry the subject
/// line, none carry both. Recognising only the phrase left the whole second family
/// unclassifiable, U34 included. Two tells, one triage candidate.
/// </summary>
public sealed partial class QdosMailClassificationPolicy : IMailClassificationPolicy
{
    public const string Key = "qdos_mail_classification";
    public const int Version = 4;

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
        // One candidate from two tells. Adding a second candidate for the same
        // category would resolve to Ambiguous, so a message carrying both
        // tells would classify worse than one carrying either.
        var isTriageRequest = hasTriagePhrase || hasTriageSubject;
        var hasAuditTitle = documentTexts.Any(text =>
            text.Contains(AuditNotificationTitle, StringComparison.Ordinal));
        var hasEngineerTitle = documentTexts.Any(text =>
            text.Contains(EngineerNotificationTitle, StringComparison.Ordinal));
        var hasReportPlusAudit = documentTexts.Any(text =>
            text.Contains(EngineerNotificationTitle, StringComparison.Ordinal)
            && text.Contains(ReportPlusAuditMarker, StringComparison.Ordinal));

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

        var candidates = new List<MailCategory>();
        if (isAutomaticReply)
        {
            candidates.Add(MailCategory.Received(ReceivedMailFamily.General, "autoreply"));
        }

        if (isTriageRequest)
        {
            candidates.Add(MailCategory.Received(
                ReceivedMailFamily.PreInstructionEmails,
                "triage-request",
                isReplyContext: isReplyPrefixed));
        }

        if (hasAuditTitle)
        {
            candidates.Add(MailCategory.Received(
                ReceivedMailFamily.NewInstructionReceived,
                "audit",
                isReplyContext: isReplyPrefixed));
        }

        if (hasEngineerTitle)
        {
            candidates.Add(MailCategory.Received(
                ReceivedMailFamily.NewInstructionReceived,
                "inspection",
                isReplyContext: isReplyPrefixed));
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
                    .Select(candidate => candidate.Subtype is null
                        ? candidate.Name
                        : $"{candidate.Name}/{candidate.Subtype}")
                    .ToArray(),
                predicates,
                "Predicates for more than one category matched simultaneously; no winner is invented (open decision: mailbox rule activation).",
                Key,
                Version);
        }

        var category = candidates[0];
        CaseType? caseType = category is
        {
            Direction: MailDirection.Received,
            ReceivedFamily: ReceivedMailFamily.NewInstructionReceived,
            Subtype: "audit"
        }
            ? CaseType.Audit
            : category is
            {
                Direction: MailDirection.Received,
                ReceivedFamily: ReceivedMailFamily.NewInstructionReceived,
                Subtype: "inspection"
            }
                ? hasReportPlusAudit
                    ? CaseType.InspectionAndAudit
                    : CaseType.Inspection
                : null;

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

    [GeneratedRegex(@"^\s*RE\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReplyPrefixRegex();

    // The forward and reply prefixes are matched case-insensitively because
    // mail clients disagree about them; "Engineer Triage" is not, because the
    // casing of a generated line is part of what makes it discriminating.
    [GeneratedRegex(
        @"^(?:\s*(?i:RE|FW|FWD)\s*:\s*)*Engineer Triage\b",
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
