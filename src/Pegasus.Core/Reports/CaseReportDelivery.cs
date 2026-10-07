using System.Globalization;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;
using System.Net.Mail;

namespace Pegasus.Core.Reports;

public sealed record ReportSendReadinessRequest(
    ActionActor Actor, Guid CaseId, Guid GenerationId,
    long ExpectedGenerationVersion, IReadOnlyList<StaffMailAttachment> Artifacts);
public interface IReportSendReadiness
{
    Task RequireReadyAsync(ReportSendReadinessRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// One report send as staff submit it (operator, 6 October 2026): the
/// generation, the reviewed recipients, the documents to attach and the
/// covering message, under the Case's edit lease.
/// </summary>
public sealed record SendCaseReportRequest(
    ActionActor Actor, Guid CaseId, long ExpectedCaseVersion, string LeaseToken,
    Guid GenerationId, long ExpectedGenerationVersion, string OperationKey,
    string CoveringMessage,
    ReportRecipientReview? ReviewedRecipients = null,
    IReadOnlyList<CaseReportArtifactKind>? Attach = null,
    CaseWorkSelector Work = CaseWorkSelector.Current);
public interface ISendCaseReport
{
    Task<StaffMailOperation> ExecuteAsync(
        SendCaseReportRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// One delivery's addressing: the To and Cc recipients staff reviewed, with
/// the report subject.
/// </summary>
public sealed record CaseReportDeliveryAddressing(
    IReadOnlyList<StaffMailRecipient> To,
    IReadOnlyList<StaffMailRecipient> Cc,
    string Subject);

/// <summary>The staff-reviewed recipients of one delivery.</summary>
public sealed record ReportRecipientReview(
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc);

public sealed record ReportRecipientSuggestions(
    string CaseReference,
    PrincipalReportRecipientSettings Settings,
    string? OriginalInstructionSender,
    string? PrincipalName = null);

public interface IReportRecipientSuggestionQueries
{
    /// <summary>The suggestions for the report of <paramref name="work"/>, whose reference names the email.</summary>
    Task<ReportRecipientSuggestions?> GetAsync(Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken);
}

/// <summary>One address the delivery form offers, and where it came from (v28 P21).</summary>
public sealed record ReportRecipientCandidate(string Address, string Source);

/// <summary>
/// What the Case's report has already been sent, for the naming a re-send
/// carries (v28 P23): how many times a report of this Case has been sent, and
/// the report date of the one sent last.
/// </summary>
public sealed record CaseReportSendHistory(int SentCount, DateOnly? LastSentReportDate)
{
    public static CaseReportSendHistory None { get; } = new(0, null);
}

public interface ICaseReportSendHistoryQueries
{
    /// <summary>The sends of <paramref name="work"/>'s reports; a work's re-send suffix counts its own sends only.</summary>
    Task<CaseReportSendHistory> GetAsync(Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken);
}

/// <summary>
/// What one delivery supplies to the Case report delivery template
/// (<see cref="EmailTemplatePurpose.CaseReportDelivery"/>). The template, its
/// placeholders and its built-in body belong to <see cref="EmailTemplates"/>.
/// </summary>
/// <remarks>
/// A fact that was not recorded has no value, so its line is left out of the
/// rendered body. The superseded report date has a value only when a report of
/// this Case has already been sent, so a first send carries no "supersedes"
/// line.
/// </remarks>
public sealed record CaseReportDeliveryFacts(
    string CaseReference,
    string? Registration,
    string? Outcome,
    string? PrincipalName,
    CaseReportSendHistory History)
{
    /// <summary>The placeholder values this delivery supplies, by placeholder name.</summary>
    public IReadOnlyDictionary<string, string?> Values() =>
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [EmailTemplates.CaseReference] = CaseReference,
            [EmailTemplates.Registration] = Registration,
            [EmailTemplates.Outcome] = Outcome,
            [EmailTemplates.PrincipalName] = PrincipalName,
            [EmailTemplates.SupersededReportDate] = History.SentCount > 0 && History.LastSentReportDate is { } superseded
                ? superseded.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("en-GB"))
                : null
        };
}

/// <summary>
/// How one delivery names what it sends (v28 P23). The attached report is
/// named for the people who read it — the Case's reference, the registration
/// and the outcome — and a re-issue adds one dot for each report of this Case
/// already sent, the way the firm's own files are named. The covering message
/// is not named here: it is the Case report delivery template
/// (<see cref="EmailTemplatePurpose.CaseReportDelivery"/>), rendered from
/// <see cref="CaseReportDeliveryFacts"/>, reviewed and edited by staff, and
/// sent as submitted.
/// </summary>
public static class CaseReportDeliveryNaming
{
    /// <summary>The outcome as the attached report's name reads it.</summary>
    public static string OutcomeWords(AssessmentReportOutcome outcome) => outcome switch
    {
        AssessmentReportOutcome.TotalLoss => "Total loss",
        AssessmentReportOutcome.CashInLieu => "Cash in lieu",
        AssessmentReportOutcome.ContractRepair => "Contract repair",
        _ => "Repairable",
    };

    /// <summary>The attached report's own name, without its extension.</summary>
    public static string ReportName(
        string caseReference, string? registration, string? outcome, int sentCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseReference);
        ArgumentOutOfRangeException.ThrowIfNegative(sentCount);
        var parts = new[]
        {
            caseReference.Trim(),
            registration?.Trim(),
            string.IsNullOrWhiteSpace(outcome) ? "report" : $"{outcome.Trim()} report",
        };
        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)))
            + new string('.', sentCount);
    }

    /// <summary>
    /// The attachments as the recipient sees them: the report under the name
    /// above, every companion document keeping the name custody gave it.
    /// </summary>
    public static IReadOnlyList<StaffMailAttachment> Named(
        IReadOnlyList<StaffMailAttachment> attachments, string reportName)
    {
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportName);
        return
        [
            .. attachments.Select((attachment, index) => index == 0
                ? attachment with { FileName = reportName + ".pdf" }
                : attachment)
        ];
    }
}

/// <summary>
/// The one owner of how a generated report is addressed, which artifacts go,
/// and when a generation is sendable. Every rule reads persisted facts
/// only. Generation is not delivery: nothing here records a Sent state, and
/// EVA is absent because the optional hand-off never gates the report.
/// </summary>
public static class CaseReportDeliveryPolicy
{
    /// <summary>
    /// Report delivery is a signed-in staff act. The Automation actor holds
    /// the ordinary casework right, but that right stops at transport.
    /// </summary>
    public static void RequireStaff(ActionActor actor)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.AccessStaffApplication);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
    }

    /// <summary>
    /// The covering message staff reviewed, as one delivery sends it: line
    /// endings made plain and trailing space trimmed. A blank message, or one
    /// past <see cref="EmailTemplates.MaximumBodyLength"/>, is refused.
    /// </summary>
    public static string CoveringMessage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("A covering message is required.", nameof(text));
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
        if (normalized.Length > EmailTemplates.MaximumBodyLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(text),
                $"A covering message cannot exceed {EmailTemplates.MaximumBodyLength} characters.");
        }

        return normalized;
    }

    /// <summary>
    /// Resolves only the Principal's configured recipient suggestions. The
    /// original sender comes from the Case's origin instruction, never a
    /// later reply, and Claim Source is never copied implicitly.
    /// </summary>
    public static CaseReportDeliveryAddressing Address(ReportRecipientSuggestions suggestions)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        ArgumentNullException.ThrowIfNull(suggestions.Settings);
        var recipients = suggestions.Settings.AdditionalAddresses
            .Select(address => Recipient(address, null))
            .OfType<StaffMailRecipient>()
            .ToList();
        if (suggestions.Settings.IncludeOriginalInstructionSender
            && Recipient(suggestions.OriginalInstructionSender, null) is { } original)
        {
            recipients.Insert(0, original);
        }
        var to = recipients
            .GroupBy(recipient => recipient.Address, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        if (to.Length == 0)
        {
            throw new InvalidOperationException(
                $"Case '{suggestions.CaseReference}' has no resolved report recipients.");
        }
        return new(to, [], suggestions.CaseReference);
    }

    /// <summary>
    /// Validates the staff-reviewed To and Cc fields of one delivery. The
    /// Principal suggestions seed the review only; they never overwrite the
    /// staff choice and Claim Source is not an implicit recipient.
    /// </summary>
    public static CaseReportDeliveryAddressing ReviewedAddress(
        ReportRecipientSuggestions suggestions,
        ReportRecipientReview review)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        ArgumentNullException.ThrowIfNull(review);
        var to = Recipients(review.To, nameof(review.To));
        if (to.Length == 0)
        {
            throw new InvalidOperationException(
                $"Case '{suggestions.CaseReference}' has no reviewed report recipients.");
        }
        var cc = Recipients(review.Cc, nameof(review.Cc))
            .Where(candidate => !to.Any(recipient => SameAddress(recipient, candidate)))
            .ToArray();
        return new(to, cc, suggestions.CaseReference);
    }

    /// <summary>
    /// The addresses the delivery form offers beside its recipient fields
    /// (v28 P21): the Principal's own report recipients and, when the
    /// Principal asks for it, the sender of the instruction that opened the
    /// Case. Nothing else is suggested, and the operator's typed address is
    /// never overwritten.
    /// </summary>
    public static IReadOnlyList<ReportRecipientCandidate> AddressBook(
        ReportRecipientSuggestions suggestions)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        ArgumentNullException.ThrowIfNull(suggestions.Settings);
        var candidates = new List<ReportRecipientCandidate>();
        if (suggestions.Settings.IncludeOriginalInstructionSender
            && Recipient(suggestions.OriginalInstructionSender, null) is { } original)
        {
            candidates.Add(new(original.Address, "This case"));
        }
        candidates.AddRange(suggestions.Settings.AdditionalAddresses
            .Select(address => Recipient(address, null))
            .OfType<StaffMailRecipient>()
            .Select(recipient => new ReportRecipientCandidate(recipient.Address, "Principal")));
        return
        [
            .. candidates
                .GroupBy(candidate => candidate.Address, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
        ];
    }

    public static ReportRecipientReview SuggestedReview(ReportRecipientSuggestions suggestions)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        ArgumentNullException.ThrowIfNull(suggestions.Settings);
        var recipients = suggestions.Settings.AdditionalAddresses
            .Select(address => Recipient(address, null))
            .OfType<StaffMailRecipient>();
        if (suggestions.Settings.IncludeOriginalInstructionSender
            && Recipient(suggestions.OriginalInstructionSender, null) is { } original)
        {
            recipients = new[] { original }.Concat(recipients);
        }
        return new(recipients
            .GroupBy(recipient => recipient.Address, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First().Address)
            .ToArray(), []);
    }

    /// <summary>
    /// A generation is deliverable only while it is its work's current one,
    /// fully confirmed, and at the version the caller last saw. The Inspection
    /// report of a Case that has its Audit stays deliverable on its own work
    /// (operator, 1 October 2026).
    /// </summary>
    public static void RequireDeliverable(
        Guid generationId,
        CaseReportGenerationState state,
        bool isCurrent,
        long version,
        long expectedVersion)
    {
        if (!isCurrent || state != CaseReportGenerationState.Confirmed)
        {
            throw new InvalidOperationException(
                $"Case report generation '{generationId}' is {(isCurrent ? state.ToString() : "superseded")} and cannot be delivered.");
        }

        if (version != expectedVersion)
        {
            throw new InvalidOperationException(
                $"Case report generation '{generationId}' is at version {version}, not expected version {expectedVersion}.");
        }
    }

    /// <summary>
    /// The attachments one generation delivers: the assessment report and the
    /// companion documents the operator chose to attach (v28 P22), each
    /// Confirmed, taken exactly from the confirmed rows. Without a choice every
    /// artifact the generation holds attaches. With a choice, the one
    /// assessment report is always included and exactly the chosen companion
    /// kinds must be present and Confirmed.
    /// </summary>
    public static IReadOnlyList<StaffMailAttachment> Attachments(
        Guid generationId,
        IReadOnlyList<CaseReportArtifactRecord> artifacts,
        IReadOnlyList<CaseReportArtifactKind>? attach = null)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        var reports = artifacts
            .Where(artifact => artifact.Kind == CaseReportArtifactKind.AssessmentReport)
            .ToArray();
        if (reports.Length != 1
            || reports[0].Status != CaseReportArtifactStatus.Confirmed)
        {
            throw new InvalidOperationException(
                $"Case report generation '{generationId}' must have exactly one confirmed assessment report to deliver.");
        }

        var chosen = attach is { Count: > 0 }
            ? artifacts.Where(artifact => artifact.Kind == CaseReportArtifactKind.AssessmentReport
                || attach.Contains(artifact.Kind)).ToArray()
            : artifacts;
        if (chosen.Count == 0
            || chosen.Any(artifact => artifact.Status != CaseReportArtifactStatus.Confirmed))
        {
            throw new InvalidOperationException(
                $"Case report generation '{generationId}' has no fully confirmed artifacts to deliver.");
        }
        if (attach is { Count: > 0 }
            && attach.Distinct().Any(kind => !chosen.Any(artifact => artifact.Kind == kind)))
        {
            throw new InvalidOperationException(
                $"Case report generation '{generationId}' does not hold every document the delivery attaches.");
        }

        return chosen.OrderBy(artifact => artifact.Kind).Select(AttachmentOf).ToArray();
    }

    /// <summary>
    /// The send boundary's re-check, made again just before the mail is
    /// submitted: a staff actor, the generation still its work's current one
    /// and confirmed at the version the send named, and every attachment
    /// byte-identical to a confirmed artifact row as it is now. A delivery may
    /// attach a subset of the generation's documents (v28 P22), so each
    /// attachment is matched, not the two lists compared.
    /// </summary>
    public static void RequireReady(
        ReportSendReadinessRequest request, CaseReportGenerationRecord generation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(generation);
        RequireStaff(request.Actor);
        if (request.CaseId != generation.CaseId || request.GenerationId != generation.Id)
        {
            throw new InvalidOperationException(
                "The case report generation does not belong to the named Case.");
        }

        RequireDeliverable(
            generation.Id,
            generation.State,
            generation.SupersededById is null,
            generation.Version,
            request.ExpectedGenerationVersion);
        var confirmed = generation.Artifacts
            .Where(artifact => artifact.Status == CaseReportArtifactStatus.Confirmed)
            .Select(AttachmentOf)
            .ToArray();
        if (request.Artifacts.Count == 0
            || request.Artifacts.Any(attachment => !confirmed.Contains(attachment)))
        {
            throw new InvalidOperationException(
                $"An attachment of case report generation '{generation.Id}' no longer matches its confirmed artifact's hash, length or identity.");
        }
    }

    public static StaffMailAttachment AttachmentOf(CaseReportArtifactRecord artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        if (artifact.Status != CaseReportArtifactStatus.Confirmed)
        {
            throw new InvalidOperationException(
                $"Generated artifact '{artifact.Id}' is {artifact.Status}, not Confirmed.");
        }

        return new(
            artifact.DocumentId ?? throw Missing(artifact, "document identity"),
            artifact.VersionId ?? throw Missing(artifact, "version identity"),
            artifact.Sha256 ?? throw Missing(artifact, "content hash"),
            artifact.ContentLength ?? throw Missing(artifact, "content length"),
            artifact.FileName ?? throw Missing(artifact, "file name"),
            artifact.MediaType ?? throw Missing(artifact, "media type"));
    }

    private static InvalidOperationException Missing(CaseReportArtifactRecord artifact, string fact) =>
        new($"Confirmed generated artifact '{artifact.Id}' carries no {fact}.");

    private static StaffMailRecipient? Recipient(string? address, string? displayName)
    {
        var trimmed = address?.Trim();
        if (string.IsNullOrEmpty(trimmed) || !MailAddress.TryCreate(trimmed, out _))
        {
            return null;
        }

        var name = displayName?.Trim();
        return new(trimmed, string.IsNullOrEmpty(name) ? null : name);
    }

    private static bool SameAddress(StaffMailRecipient left, StaffMailRecipient right) =>
        string.Equals(left.Address, right.Address, StringComparison.OrdinalIgnoreCase);

    private static StaffMailRecipient[] Recipients(
        IReadOnlyList<string>? values,
        string parameterName) => (values ?? [])
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => Recipient(value, null)
            ?? throw new ArgumentException("Each reviewed recipient must be a valid e-mail address.", parameterName))
        .GroupBy(recipient => recipient.Address, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .ToArray();
}

/// <summary>
/// B's half of the send boundary contract: A invokes it with the actor, Case
/// and generation versions and the exact attachment identities it is about to
/// send. It reloads the generation and refuses; it never sends.
/// </summary>
public sealed class ReportSendReadiness(ICaseReportGenerationStore generations) : IReportSendReadiness
{
    public async Task RequireReadyAsync(
        ReportSendReadinessRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        CaseReportDeliveryPolicy.RequireStaff(request.Actor);
        var generation = await generations
            .GetAsync(request.Actor, request.CaseId, request.GenerationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Case report generation '{request.GenerationId}' is unavailable on case '{request.CaseId}'.");
        CaseReportDeliveryPolicy.RequireReady(request, generation);
    }
}

/// <summary>
/// Sends one generation's report (operator, 6 October 2026): a signed-in
/// staff actor, the generation read under the Case's edit lease, the
/// recipients staff reviewed, the documents they chose and the message they
/// submitted. It is the one
/// production caller of A's staff report send and hands it one command under
/// the caller's operation key, so the same key replays the same send. A's
/// returned state is the outcome; an Unknown or pending result is returned as
/// such and never retried here.
/// </summary>
public sealed class SendCaseReport(
    ICaseReportGenerationStore generations,
    ICaseReportSendHistoryQueries sendHistory,
    IReportRecipientSuggestionQueries recipientSuggestions,
    IApprovedMailboxStore mailboxes,
    IStaffReportSend send) : ISendCaseReport
{
    public async Task<StaffMailOperation> ExecuteAsync(
        SendCaseReportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.LeaseToken);
        if (request.CaseId == Guid.Empty || request.GenerationId == Guid.Empty)
        {
            throw new ArgumentException("A Case and a generation are required.", nameof(request));
        }

        CaseReportDeliveryPolicy.RequireStaff(request.Actor);

        // The message staff reviewed and possibly edited is what is sent; the
        // template only pre-filled it.
        var message = CaseReportDeliveryPolicy.CoveringMessage(request.CoveringMessage);

        // The structured contacts seed the review only: what staff submitted
        // is what the delivery is addressed to.
        var suggestions = await recipientSuggestions.GetAsync(request.CaseId, request.Work, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        var addressing = CaseReportDeliveryPolicy.ReviewedAddress(
            suggestions,
            request.ReviewedRecipients ?? CaseReportDeliveryPolicy.SuggestedReview(suggestions));

        // The store reloads the lease and requires the generation deliverable.
        var generation = await generations.GetForDeliveryAsync(request, cancellationToken).ConfigureAwait(false);
        var attachments = CaseReportDeliveryPolicy.Attachments(
            generation.Id, generation.Artifacts, request.Attach);

        // v28 P23: what the work has already sent decides the report's name,
        // read with the generation this delivery sends.
        var history = await sendHistory.GetAsync(request.CaseId, request.Work, cancellationToken).ConfigureAwait(false);
        var reportName = CaseReportDeliveryNaming.ReportName(
            generation.Snapshot.CaseReference,
            generation.Snapshot.Report.Vehicle.Registration,
            CaseReportDeliveryNaming.OutcomeWords(generation.Snapshot.Report.Outcome),
            history.SentCount);

        var mailbox = await SendingMailboxAsync(cancellationToken).ConfigureAwait(false);
        var mail = new StaffMailSendCommand(
            request.Actor,
            mailbox.Id,
            // The transport's generation guard is the mailbox's Generation
            // (G14), never the administration row's concurrency Version.
            mailbox.Generation,
            StaffMailPurpose.CaseReport,
            // A03's report context is the immutable generation, not the Case:
            // the generation's version is what the transport re-checks.
            generation.Id,
            generation.Version,
            StaffMailComposeMode.New,
            OriginalMessage: null,
            addressing.To,
            addressing.Cc,
            addressing.Subject,
            Body: message,
            // Custody keeps its own name for the same bytes.
            CaseReportDeliveryNaming.Named(attachments, reportName),
            request.OperationKey);
        var report = new ReportSendReadinessRequest(
            request.Actor, request.CaseId, generation.Id, generation.Version, attachments);
        return await send.SendAsync(new(mail, report), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The approved mailbox reports leave from is the one bound for both
    /// directions of that journey: StaffSend, because a report send is a
    /// staff send, and SentEvidence, because A reads the same mailbox's Sent
    /// items for truthful report-sent evidence. Exactly one identified,
    /// Approved mailbox with a valid Generation may hold both scopes; none,
    /// a missing scope or several fails closed.
    /// </summary>
    private async Task<ApprovedMailbox> SendingMailboxAsync(CancellationToken cancellationToken)
    {
        var candidates = (await mailboxes.ListAsync(cancellationToken).ConfigureAwait(false))
            .Where(mailbox => mailbox.State == ApprovedMailboxState.Approved
                && mailbox.IdentityIsBound
                && mailbox.RouteScopes.Contains(ApprovedMailboxRouteScope.StaffSend)
                && mailbox.RouteScopes.Contains(ApprovedMailboxRouteScope.SentEvidence)
                && mailbox.Generation > 0)
            .ToArray();
        return candidates.Length == 1
            ? candidates[0]
            : throw new InvalidOperationException(
                candidates.Length == 0
                    ? "No approved mailbox is bound for both staff send and report-sent evidence, so no report can be sent."
                    : "More than one approved mailbox is bound for both staff send and report-sent evidence.");
    }
}
