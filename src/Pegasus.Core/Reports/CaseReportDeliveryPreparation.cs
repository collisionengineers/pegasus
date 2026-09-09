using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace Pegasus.Core.Reports;

public sealed record ReportSendReadinessRequest(
    ActionActor Actor, Guid CaseId, long ExpectedCaseVersion, Guid GenerationId,
    long ExpectedGenerationVersion, Guid PreparationId, long ExpectedPreparationVersion,
    IReadOnlyList<StaffMailAttachment> Artifacts);
public interface IReportSendReadiness
{
    Task RequireReadyAsync(ReportSendReadinessRequest request, CancellationToken cancellationToken);
}
public sealed record CaseReportDeliveryPreparation(
    Guid Id, Guid CaseId, Guid GenerationId, long GenerationVersion, long Version,
    IReadOnlyList<StaffMailAttachment> Artifacts, ActionActor PreparedBy,
    DateTimeOffset PreparedAtUtc, string RecipientSuggestionFingerprint);
public sealed record PrepareCaseReportDeliveryRequest(
    ActionActor Actor, Guid CaseId, long ExpectedCaseVersion, string LeaseToken,
    Guid GenerationId, long ExpectedGenerationVersion, string OperationKey,
    ReportRecipientReview? ReviewedRecipients = null);
public interface IPrepareCaseReportDelivery
{
    Task<CaseReportDeliveryPreparation> ExecuteAsync(
        PrepareCaseReportDeliveryRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// The immutable delivery intent addressing: staff review its To and Cc
/// recipients before preparation freezes them with the report subject.
/// </summary>
public sealed record CaseReportDeliveryAddressing(
    IReadOnlyList<StaffMailRecipient> To,
    IReadOnlyList<StaffMailRecipient> Cc,
    string Subject);

/// <summary>The staff-reviewed recipients to freeze in one delivery preparation.</summary>
public sealed record ReportRecipientReview(
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc);

public sealed record ReportRecipientSuggestions(
    string CaseReference,
    PrincipalReportRecipientSettings Settings,
    string? OriginalInstructionSender)
{
    public string Fingerprint => CaseReportDeliveryPolicy.SuggestionFingerprint(this);
}

public interface IReportRecipientSuggestionQueries
{
    Task<ReportRecipientSuggestions?> GetAsync(Guid caseId, CancellationToken cancellationToken);
}

/// <summary>
/// One persisted preparation read back with the current facts the send
/// boundary re-checks it against: the Case version the preparation froze,
/// the Case version now, the generation's state and version now, and the
/// attachments the confirmed artifact rows describe now. The preparation
/// itself never changes; what changes around it is what makes it unsendable.
/// </summary>
public sealed record CaseReportDeliveryPreparationRecord(
    CaseReportDeliveryPreparation Preparation,
    CaseReportDeliveryAddressing Addressing,
    long FrozenCaseVersion,
    long CurrentCaseVersion,
    CaseReportGenerationState GenerationState,
    bool GenerationIsCurrent,
    long CurrentGenerationVersion,
    IReadOnlyList<StaffMailAttachment> ConfirmedArtifacts);

/// <summary>
/// The store-side input of one preparation: the guarded request plus the
/// staff-reviewed addressing and the Principal suggestion fingerprint.
/// </summary>
public sealed record PrepareCaseReportDeliveryCommand(
    PrepareCaseReportDeliveryRequest Request,
    CaseReportDeliveryAddressing Addressing,
    string RecipientSuggestionFingerprint);

public interface ICaseReportDeliveryPreparationStore
{
    /// <summary>
    /// Reloads permission, lease and expected Case version, requires the
    /// named generation to be current, fully confirmed and at the expected
    /// version, and writes one preparation pinning every confirmed artifact
    /// by exact document, version, hash and length. Replays by operation key.
    /// </summary>
    Task<CaseReportDeliveryPreparationRecord> PrepareAsync(
        PrepareCaseReportDeliveryCommand command, CancellationToken cancellationToken);

    Task<CaseReportDeliveryPreparationRecord?> GetAsync(
        ActionActor actor, Guid caseId, Guid preparationId, CancellationToken cancellationToken);

    /// <summary>The latest preparation of the Case's current generation, if any.</summary>
    Task<CaseReportDeliveryPreparationRecord?> GetCurrentAsync(
        ActionActor actor, Guid caseId, CancellationToken cancellationToken);
}

public sealed record SendPreparedCaseReportRequest(
    ActionActor Actor, Guid CaseId, Guid PreparationId, long ExpectedPreparationVersion,
    string OperationKey);
public interface ISendPreparedCaseReport
{
    Task<StaffMailOperation> ExecuteAsync(
        SendPreparedCaseReportRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// The one owner of how a generated report is addressed, which artifacts go,
/// and when a preparation is still sendable. Every rule reads persisted facts
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
    /// Validates staff-reviewed To and Cc fields before freezing them. The
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

    public static string SuggestionFingerprint(ReportRecipientSuggestions suggestions)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        var material = string.Join("\u001f",
            suggestions.Settings.IncludeOriginalInstructionSender,
            suggestions.OriginalInstructionSender?.Trim().ToUpperInvariant() ?? "",
            string.Join("\u001e", suggestions.Settings.AdditionalAddresses
                .Select(address => address.Trim().ToUpperInvariant())));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    /// <summary>
    /// The Principal recipient suggestions must remain unchanged after
    /// preparation. The reviewed To/Cc list itself is frozen and may differ
    /// from those suggestions.
    /// </summary>
    public static void RequireSuggestionCurrent(string preparedFingerprint, string currentFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preparedFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentFingerprint);
        if (!string.Equals(preparedFingerprint, currentFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The Principal's recipient suggestions changed after the report was prepared; prepare it again.");
        }
    }

    /// <summary>
    /// A generation is deliverable only while it is the Case's current one,
    /// fully confirmed, and at the version the caller last saw.
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
    /// The attachments one generation delivers: every artifact it was asked
    /// for, each Confirmed, taken exactly from the confirmed rows. A partly
    /// confirmed generation yields nothing.
    /// </summary>
    public static IReadOnlyList<StaffMailAttachment> Attachments(
        Guid generationId, IReadOnlyList<CaseReportArtifactRecord> artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        if (artifacts.Count == 0
            || artifacts.Any(artifact => artifact.Status != CaseReportArtifactStatus.Confirmed))
        {
            throw new InvalidOperationException(
                $"Case report generation '{generationId}' has no fully confirmed artifacts to deliver.");
        }

        return artifacts.OrderBy(artifact => artifact.Kind).Select(AttachmentOf).ToArray();
    }

    /// <summary>
    /// The send boundary's re-check: a staff actor, the preparation's frozen
    /// Case version against the Case version now, the generation still
    /// current and confirmed at the version the preparation pinned, the
    /// preparation's own version, and every requested attachment
    /// byte-identical to both the preparation and the confirmed artifact
    /// rows as they are now.
    /// </summary>
    public static void RequireReady(
        ReportSendReadinessRequest request, CaseReportDeliveryPreparationRecord record)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(record);
        RequireStaff(request.Actor);
        var preparation = record.Preparation;
        if (request.CaseId != preparation.CaseId || request.PreparationId != preparation.Id)
        {
            throw new InvalidOperationException(
                "The report delivery preparation does not belong to the named Case.");
        }

        // The expected version is the one frozen into the preparation: any
        // Case mutation since — even one that left the addressing intact —
        // refuses the send instead of delivering stale bytes.
        CaseEditAuthority.RequireVersion(
            request.CaseId, record.CurrentCaseVersion, request.ExpectedCaseVersion);
        if (request.GenerationId != preparation.GenerationId)
        {
            throw new InvalidOperationException(
                "The report delivery preparation was made for a different generation.");
        }

        if (request.ExpectedGenerationVersion != preparation.GenerationVersion)
        {
            throw new InvalidOperationException(
                $"Case report generation '{preparation.GenerationId}' was prepared at version {preparation.GenerationVersion}, not expected version {request.ExpectedGenerationVersion}.");
        }

        RequireDeliverable(
            preparation.GenerationId,
            record.GenerationState,
            record.GenerationIsCurrent,
            record.CurrentGenerationVersion,
            request.ExpectedGenerationVersion);
        if (request.ExpectedPreparationVersion != preparation.Version)
        {
            throw new InvalidOperationException(
                $"Report delivery preparation '{preparation.Id}' is at version {preparation.Version}, not expected version {request.ExpectedPreparationVersion}.");
        }

        // Exact identity equality against the frozen pin — same count, same
        // items, same order. A count-plus-contains check would let a
        // duplicated artifact omit another (Stream A review, multiplicity).
        if (!request.Artifacts.SequenceEqual(preparation.Artifacts))
        {
            throw new InvalidOperationException(
                $"The attachments to send are not the ones report delivery preparation '{preparation.Id}' pinned.");
        }

        if (!request.Artifacts.SequenceEqual(record.ConfirmedArtifacts))
        {
            throw new InvalidOperationException(
                $"An attachment of report delivery preparation '{preparation.Id}' no longer matches its confirmed artifact's hash, length or identity.");
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

    private static bool SameRecipients(
        IReadOnlyList<StaffMailRecipient> left, IReadOnlyList<StaffMailRecipient> right) =>
        left.Count == right.Count
        && left.Zip(right).All(pair =>
            SameAddress(pair.First, pair.Second)
            && string.Equals(pair.First.DisplayName, pair.Second.DisplayName, StringComparison.Ordinal));

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
/// Prepares one generation for delivery: a signed-in staff actor, the Case's
/// current version and edit lease, the generation at its expected version
/// with every artifact confirmed, and the recipients resolved from the Case's
/// structured contacts. Replays by operation key. Nothing is sent.
/// </summary>
public sealed class PrepareCaseReportDelivery(
    ICaseReportDeliveryPreparationStore store,
    IReportRecipientSuggestionQueries recipientSuggestions) : IPrepareCaseReportDelivery
{
    public async Task<CaseReportDeliveryPreparation> ExecuteAsync(
        PrepareCaseReportDeliveryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.LeaseToken);
        if (request.CaseId == Guid.Empty || request.GenerationId == Guid.Empty)
        {
            throw new ArgumentException("A Case and a generation are required.", nameof(request));
        }

        CaseReportDeliveryPolicy.RequireStaff(request.Actor);

        // The structured contacts are read outside the store's transaction:
        // they are a Case-data read, never something the operator posts.
        var suggestions = await recipientSuggestions.GetAsync(request.CaseId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        var review = request.ReviewedRecipients
            ?? CaseReportDeliveryPolicy.SuggestedReview(suggestions);
        var addressing = CaseReportDeliveryPolicy.ReviewedAddress(suggestions, review);

        var record = await store
            .PrepareAsync(new(request, addressing, suggestions.Fingerprint), cancellationToken)
            .ConfigureAwait(false);
        return record.Preparation;
    }
}

/// <summary>
/// B's half of the send boundary contract: A invokes it with the persisted
/// actor, Case, generation and preparation versions and the exact attachment
/// identities it is about to send. It reloads and refuses; it never sends.
/// </summary>
public sealed class ReportSendReadiness(ICaseReportDeliveryPreparationStore store) : IReportSendReadiness
{
    public async Task RequireReadyAsync(
        ReportSendReadinessRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        CaseReportDeliveryPolicy.RequireStaff(request.Actor);
        var record = await store
            .GetAsync(request.Actor, request.CaseId, request.PreparationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Report delivery preparation '{request.PreparationId}' is unavailable on case '{request.CaseId}'.");
        CaseReportDeliveryPolicy.RequireReady(request, record);
    }
}

/// <summary>
/// The one production caller of A's staff report send. It re-checks what
/// only B can — that the prepared recipients still resolve from the Case's
/// structured contacts — then the shared readiness, and hands A one command
/// under the caller's operation key. A's returned state is the outcome; an
/// Unknown or pending result is returned as such and never retried here.
/// </summary>
public sealed class SendPreparedCaseReport(
    ICaseReportDeliveryPreparationStore store,
    IReportRecipientSuggestionQueries recipientSuggestions,
    IApprovedMailboxStore mailboxes,
    IReportSendReadiness readiness,
    IStaffReportSend send) : ISendPreparedCaseReport
{
    public async Task<StaffMailOperation> ExecuteAsync(
        SendPreparedCaseReportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        CaseReportDeliveryPolicy.RequireStaff(request.Actor);

        var record = await store
            .GetAsync(request.Actor, request.CaseId, request.PreparationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Report delivery preparation '{request.PreparationId}' is unavailable on case '{request.CaseId}'.");
        var suggestions = await recipientSuggestions.GetAsync(request.CaseId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        CaseReportDeliveryPolicy.RequireSuggestionCurrent(
            record.Preparation.RecipientSuggestionFingerprint,
            suggestions.Fingerprint);

        var preparation = record.Preparation;
        var report = new ReportSendReadinessRequest(
            request.Actor,
            request.CaseId,
            record.FrozenCaseVersion,
            preparation.GenerationId,
            preparation.GenerationVersion,
            preparation.Id,
            request.ExpectedPreparationVersion,
            preparation.Artifacts);
        await readiness.RequireReadyAsync(report, cancellationToken).ConfigureAwait(false);

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
            preparation.GenerationId,
            preparation.GenerationVersion,
            StaffMailComposeMode.New,
            OriginalMessage: null,
            record.Addressing.To,
            record.Addressing.Cc,
            record.Addressing.Subject,
            Body: string.Empty,
            preparation.Artifacts,
            request.OperationKey);
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
