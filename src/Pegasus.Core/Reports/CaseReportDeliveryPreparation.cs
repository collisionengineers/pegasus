using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;

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
    DateTimeOffset PreparedAtUtc);
public sealed record PrepareCaseReportDeliveryRequest(
    ActionActor Actor, Guid CaseId, long ExpectedCaseVersion, string LeaseToken,
    Guid GenerationId, long ExpectedGenerationVersion, string OperationKey);
public interface IPrepareCaseReportDelivery
{
    Task<CaseReportDeliveryPreparation> ExecuteAsync(
        PrepareCaseReportDeliveryRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// The delivery intent as it is addressed: the structured case contacts the
/// report goes to and the subject it goes under. Nothing here is typed by an
/// operator or parsed from a note; it is resolved from the Case's own
/// structured contact facts by <see cref="CaseReportDeliveryPolicy"/>.
/// </summary>
public sealed record CaseReportDeliveryAddressing(
    IReadOnlyList<StaffMailRecipient> To,
    IReadOnlyList<StaffMailRecipient> Cc,
    string Subject);

/// <summary>
/// One persisted preparation read back with the current facts the send
/// boundary re-checks it against: the Case version now, the generation's
/// state and version now, and the attachments the confirmed artifact rows
/// describe now. The preparation itself never changes; what changes around
/// it is what makes it unsendable.
/// </summary>
public sealed record CaseReportDeliveryPreparationRecord(
    CaseReportDeliveryPreparation Preparation,
    CaseReportDeliveryAddressing Addressing,
    long CurrentCaseVersion,
    CaseReportGenerationState GenerationState,
    bool GenerationIsCurrent,
    long CurrentGenerationVersion,
    IReadOnlyList<StaffMailAttachment> ConfirmedArtifacts);

/// <summary>
/// The store-side input of one preparation: the guarded request plus the
/// addressing Core already resolved from structured contacts.
/// </summary>
public sealed record PrepareCaseReportDeliveryCommand(
    PrepareCaseReportDeliveryRequest Request,
    CaseReportDeliveryAddressing Addressing);

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
    /// Resolves the recipients from the Case's structured contacts: the file
    /// handler Pegasus corresponds with about the case is addressed, and the
    /// recorded claim source contact is copied when it is a different
    /// address. The subject is the Case reference. A Case with no contact
    /// address cannot be prepared.
    /// </summary>
    public static CaseReportDeliveryAddressing Address(CaseDataProjection data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var contact = Recipient(data.Contact.EmailAddress.Current?.Value, data.Contact.Name.Current?.Value)
            ?? throw new InvalidOperationException(
                $"Case '{data.Identity.CaseId}' has no contact e-mail address to deliver the report to.");
        var claimSource = data.Workspace?.ClaimSource is { } source
            ? Recipient(source.ContactEmailAddress, source.ContactName ?? source.Name)
            : null;
        IReadOnlyList<StaffMailRecipient> cc = claimSource is not null && !SameAddress(claimSource, contact)
            ? [claimSource]
            : [];
        return new([contact], cc, data.Identity.Reference);
    }

    /// <summary>
    /// The prepared addressing must still resolve from the Case's structured
    /// contacts at send time: a contact edited after preparation changes the
    /// delivery intent, so the preparation is refused rather than sent to
    /// the address it no longer names.
    /// </summary>
    public static void RequireAddressingCurrent(
        CaseReportDeliveryAddressing prepared, CaseReportDeliveryAddressing current)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(current);
        if (!SameRecipients(prepared.To, current.To)
            || !SameRecipients(prepared.Cc, current.Cc)
            || !string.Equals(prepared.Subject, current.Subject, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The Case's contacts changed after the report was prepared; prepare it again.");
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
    /// The send boundary's re-check: a staff actor, the Case and generation
    /// versions the preparation was made at, the generation still current
    /// and confirmed, the preparation's own version, and every requested
    /// attachment byte-identical to both the preparation and the confirmed
    /// artifact rows as they are now.
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

        if (request.Artifacts.Count == 0
            || request.Artifacts.Count != preparation.Artifacts.Count
            || request.Artifacts.Any(attachment => !preparation.Artifacts.Contains(attachment)))
        {
            throw new InvalidOperationException(
                $"The attachments to send are not the ones report delivery preparation '{preparation.Id}' pinned.");
        }

        if (request.Artifacts.Any(attachment => !record.ConfirmedArtifacts.Contains(attachment)))
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
        if (string.IsNullOrEmpty(trimmed))
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
}

/// <summary>
/// Prepares one generation for delivery: a signed-in staff actor, the Case's
/// current version and edit lease, the generation at its expected version
/// with every artifact confirmed, and the recipients resolved from the Case's
/// structured contacts. Replays by operation key. Nothing is sent.
/// </summary>
public sealed class PrepareCaseReportDelivery(
    ICaseReportDeliveryPreparationStore store,
    ICaseDataQueries caseData) : IPrepareCaseReportDelivery
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
        var data = await caseData.GetAsync(request.CaseId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        var addressing = CaseReportDeliveryPolicy.Address(data);

        var record = await store
            .PrepareAsync(new(request, addressing), cancellationToken)
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
    ICaseDataQueries caseData,
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
        var data = await caseData.GetAsync(request.CaseId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        CaseReportDeliveryPolicy.RequireAddressingCurrent(
            record.Addressing, CaseReportDeliveryPolicy.Address(data));

        var preparation = record.Preparation;
        var report = new ReportSendReadinessRequest(
            request.Actor,
            request.CaseId,
            record.CurrentCaseVersion,
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
            mailbox.Version,
            StaffMailPurpose.CaseReport,
            request.CaseId,
            record.CurrentCaseVersion,
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
    /// The approved mailbox reports leave from is the one whose Sent items
    /// A reads for report-sent evidence. Exactly one identified, Approved
    /// mailbox may hold that scope; none or several fails closed.
    /// </summary>
    private async Task<ApprovedMailbox> SendingMailboxAsync(CancellationToken cancellationToken)
    {
        var candidates = (await mailboxes.ListAsync(cancellationToken).ConfigureAwait(false))
            .Where(mailbox => mailbox.State == ApprovedMailboxState.Approved
                && mailbox.IdentityIsBound
                && mailbox.RouteScopes.Contains(ApprovedMailboxRouteScope.SentEvidence))
            .ToArray();
        return candidates.Length == 1
            ? candidates[0]
            : throw new InvalidOperationException(
                candidates.Length == 0
                    ? "No approved mailbox is bound for report-sent evidence, so no report can be sent."
                    : "More than one approved mailbox is bound for report-sent evidence.");
    }
}
