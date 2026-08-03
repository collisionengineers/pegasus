using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

/// <summary>
/// QDOS message-type classification over the settled taxonomy, built only on the
/// operator-guaranteed generated tells: the Triage phrase lives in the email body and the
/// work-type notification titles live only inside the attached instruction letter. Body
/// keyword matching is deliberately absent — corpus evidence shows "audit" in a body
/// signals an existing case being chased, not a new instruction. When predicates for more
/// than one category match, the result is the recorded Ambiguous outcome, never an
/// invented winner; when none match, the message fails closed as Unclassified.
/// </summary>
public sealed partial class QdosMailClassificationPolicy : IMailClassificationPolicy
{
    public const string Key = "qdos_mail_classification";
    public const int Version = 1;

    private const string TriagePhrase = "Triage Only Request";
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
        var documentTexts = Texts(readResult, IntakeEvidenceSource.DocumentContent);

        var isAutomaticReply = AutomaticReplyRegex().IsMatch(subject);
        var isReplyPrefixed = ReplyPrefixRegex().IsMatch(subject);
        var hasTriagePhrase = bodyTexts.Any(text =>
            text.Contains(TriagePhrase, StringComparison.OrdinalIgnoreCase));
        var hasAuditTitle = documentTexts.Any(text =>
            text.Contains(AuditNotificationTitle, StringComparison.OrdinalIgnoreCase));
        var hasEngineerTitle = documentTexts.Any(text =>
            text.Contains(EngineerNotificationTitle, StringComparison.OrdinalIgnoreCase));
        var hasReportPlusAudit = hasEngineerTitle && documentTexts.Any(text =>
            text.Contains(ReportPlusAuditMarker, StringComparison.OrdinalIgnoreCase));

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

        if (hasTriagePhrase)
        {
            candidates.Add(MailCategory.Received(
                ReceivedMailFamily.PreInstructionEmails,
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

        return MailClassificationResult.Classified(
            candidates[0],
            predicates,
            "Exactly one accepted classification predicate family matched.",
            Key,
            Version);
    }

    private static string[] Texts(
        IntakeSourceReadResult readResult,
        IntakeEvidenceSource source) =>
        readResult.Content
            .Where(fragment => fragment.Source == source)
            .Select(fragment => fragment.Text)
            .ToArray();

    [GeneratedRegex(@"^\s*Automatic reply\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AutomaticReplyRegex();

    [GeneratedRegex(@"^\s*RE\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReplyPrefixRegex();
}
