using Pegasus.Core.Custody;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

/// <summary>
/// The durable custody state of one incoming artifact. Only
/// <see cref="Confirmed"/> is success; the other three are all "not yet, and
/// not a failure the operator can be told is finished".
/// </summary>
public enum IncomingArtifactCustodyState
{
    /// <summary>Accepted and handed over; custody has not confirmed it yet.</summary>
    Pending,

    /// <summary>Custody holds the exact bytes under a known identity.</summary>
    Confirmed,

    /// <summary>Custody refused it for a recorded reason.</summary>
    Failed,

    /// <summary>
    /// The hand-over neither confirmed nor refused — a timeout, a lost
    /// connection, a restart mid-call. The artifact may or may not be held, so
    /// it is reconciled against custody and never blindly resubmitted.
    /// </summary>
    Unknown
}

/// <summary>
/// One immutable incoming artifact offered to custody. The occurrence identity
/// is server-issued and addresses this arrival — two arrivals with the same
/// proposed name are two occurrences and never overwrite one another. The
/// operation key is what makes a retry the same retention rather than a second
/// one.
/// </summary>
public sealed record IncomingArtifactOccurrence(
    Guid OccurrenceId,
    Guid? CaseId,
    Guid? IntakeReceiptId,
    string OperationKey,
    string ProposedFileName,
    string MediaType,
    long ContentLength,
    string Sha256);

/// <summary>
/// What the retention record holds after a hand-over. The logical document and
/// version identities are kept for every state, not just
/// <see cref="IncomingArtifactCustodyState.Confirmed"/>, because they are what
/// a later reconciliation asks custody about.
/// </summary>
public sealed record RetainedIncomingArtifact(
    Guid OccurrenceId,
    string OperationKey,
    IncomingArtifactCustodyState State,
    Guid? CaseId = null,
    Guid? DocumentId = null,
    Guid? DocumentVersionId = null,
    string? BoxFileId = null,
    string? BoxVersionId = null,
    string? FailureCode = null)
{
    /// <summary>
    /// The one place "did this succeed" is decided. Nothing renders success,
    /// counts towards a session's completion, or lets a submission be
    /// finalized on anything but a confirmed retention.
    /// </summary>
    public bool IsConfirmed => State == IncomingArtifactCustodyState.Confirmed;
}

/// <summary>
/// Where a retained occurrence's custody state lives. Implemented once per
/// retained record shape — the public-upload occurrence and the intake asset —
/// so <see cref="RetainIncomingArtifact"/> stays the only command that talks to
/// custody.
/// </summary>
public interface IIncomingArtifactRetentionStore
{
    /// <summary>
    /// The retention this operation key already produced, if any. This is what
    /// makes a confirmed replay return the same logical document and version
    /// instead of retaining a second copy.
    /// </summary>
    Task<RetainedIncomingArtifact?> FindAsync(
        string operationKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the state of one occurrence. Called for every disposition, so a
    /// pending, failed or uncertain hand-over is as durable as a confirmed one
    /// and never silently disappears.
    /// </summary>
    Task RecordAsync(
        RetainedIncomingArtifact artifact,
        CancellationToken cancellationToken);
}

/// <summary>
/// The one Core command that hands an incoming artifact to custody.
/// </summary>
/// <remarks>
/// Every incoming channel — the public upload link now, manual and mailbox
/// retention once their surfaces land — retains through here, so there is one
/// place that decides what "retained" means and one place that records it.
/// The command never invents success: a disposition custody did not give is
/// never upgraded, and a hand-over custody has not finished - a Pending one as
/// much as an Unknown one - is reconciled through
/// <see cref="ICaseArtifactCustodyStatus"/> under the same operation key rather
/// than resubmitted, because resubmitting bytes custody may already hold is how
/// duplicates are made.
/// </remarks>
public sealed class RetainIncomingArtifact(
    ICaseArtifactCustody custody,
    IIncomingArtifactRetentionStore store,
    ICaseArtifactCustodyStatus? custodyStatus = null)
{
    private readonly ICaseArtifactCustody custody =
        custody ?? throw new ArgumentNullException(nameof(custody));

    private readonly IIncomingArtifactRetentionStore store =
        store ?? throw new ArgumentNullException(nameof(store));

    public async Task<RetainedIncomingArtifact> ExecuteAsync(
        ActionActor actor,
        IncomingArtifactOccurrence occurrence,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(occurrence);
        ArgumentNullException.ThrowIfNull(content);
        Validate(actor, occurrence);

        var existing = await store.FindAsync(occurrence.OperationKey, cancellationToken);
        if (existing is not null)
        {
            // A confirmed retention is final: the same operation key returns
            // the same logical document and version, and the bytes are not
            // offered a second time.
            if (existing.IsConfirmed)
            {
                return existing;
            }

            // Pending and Unknown both mean custody may already hold these
            // bytes, so both are asked about rather than repeated. Only a
            // Failed retention - custody said no, and said so - is offered
            // again under the same key.
            if (existing.State is IncomingArtifactCustodyState.Pending
                or IncomingArtifactCustodyState.Unknown)
            {
                return await ReconcileAsync(actor, existing, cancellationToken);
            }
        }

        CaseArtifactCustodyResult result;
        try
        {
            result = await custody.RetainAsync(
                new(
                    actor,
                    occurrence.CaseId,
                    occurrence.IntakeReceiptId,
                    occurrence.OccurrenceId.ToString("N"),
                    occurrence.OperationKey,
                    occurrence.ProposedFileName,
                    occurrence.MediaType,
                    occurrence.ContentLength,
                    occurrence.Sha256,
                    content),
                cancellationToken);
        }
        catch (Exception exception) when (IsUncertainHandOver(exception))
        {
            // The call neither confirmed nor refused, so custody may be
            // holding these exact bytes. Recording Unknown - rather than
            // leaving the arrival in whatever state it was offered from - is
            // what makes the next attempt ask about it instead of offering
            // the same bytes a second time.
            var uncertain = new RetainedIncomingArtifact(
                occurrence.OccurrenceId,
                occurrence.OperationKey,
                IncomingArtifactCustodyState.Unknown,
                occurrence.CaseId);

            // Written on a fresh token on purpose. A hand-over cancelled by
            // the sender disconnecting is exactly the case that must still be
            // written down, and the cancelled token it arrived on would refuse
            // the write and leave the arrival re-offerable.
            await store.RecordAsync(uncertain, CancellationToken.None);
            return uncertain;
        }

        var retained = Project(occurrence, result);
        await store.RecordAsync(retained, cancellationToken);
        return retained;
    }

    /// <summary>
    /// Whether a thrown hand-over leaves it uncertain that custody took the
    /// bytes. Everything does, except the two refusals custody raises before
    /// it has read anything.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Custody refuses an authority it does not accept, and a request it
    /// cannot parse, <em>before</em> the content stream is touched. Those two
    /// are named here so they surface: nothing was offered, so nothing about
    /// it is uncertain, and recording an arrival nothing can reconcile would
    /// be a lie. Every other exception is uncertain - a dependency an adapter
    /// could not reach, a timeout, a database fault, a cancelled request, or a
    /// type this command has never heard of - because the type of a fault
    /// raised mid-call is not evidence about what custody kept, and the only
    /// safe reading of "the call did not return" is "custody may hold these
    /// bytes".
    /// </para>
    /// <para>
    /// No transport type is named, deliberately. Core does not know what
    /// transport an adapter speaks, and naming one would make Core reference
    /// <c>System.Net.Http</c>, which the dependency direction forbids; an
    /// adapter translates its transport faults to
    /// <see cref="IntakeDependencyUnavailableException"/>, which is uncertain
    /// here like everything else. The two process-fatal faults
    /// <see cref="IntakeExceptionPolicy.IsRecoverable"/> also excludes are
    /// left to propagate, because there is no database write to be made on
    /// the way down.
    /// </para>
    /// </remarks>
    private static bool IsUncertainHandOver(Exception exception) =>
        exception is not (StaffAuthorizationException
            or ArgumentException
            or OutOfMemoryException
            or AccessViolationException);

    /// <summary>
    /// Asks custody what became of a hand-over that is neither confirmed nor
    /// refused - a Pending one it has not finished, or an Unknown one it may
    /// never have received. Without a status port, or without the identities
    /// to ask about, the retention keeps the state it had, which is honest and
    /// still never renders as success.
    /// </summary>
    private async Task<RetainedIncomingArtifact> ReconcileAsync(
        ActionActor actor,
        RetainedIncomingArtifact existing,
        CancellationToken cancellationToken)
    {
        if (custodyStatus is null
            || existing.CaseId is not { } caseId
            || existing.DocumentId is not { } documentId
            || existing.DocumentVersionId is not { } versionId)
        {
            return existing;
        }

        var status = await custodyStatus.GetAsync(
            actor,
            caseId,
            documentId,
            versionId,
            cancellationToken);
        var state = ToState(status.Disposition);
        var confirmed = state == IncomingArtifactCustodyState.Confirmed;
        var reconciled = existing with
        {
            State = state,
            // The same rule a first hand-over follows: only a confirmed
            // retention says where custody holds the bytes, so a reconciliation
            // that comes back Pending or Failed carries no remote identity.
            BoxFileId = confirmed ? status.BoxFileId ?? existing.BoxFileId : null,
            BoxVersionId = confirmed ? status.BoxVersionId ?? existing.BoxVersionId : null,
            FailureCode = status.FailureCode ?? existing.FailureCode
        };
        if (reconciled != existing)
        {
            await store.RecordAsync(reconciled, cancellationToken);
        }

        return reconciled;
    }

    private static RetainedIncomingArtifact Project(
        IncomingArtifactOccurrence occurrence,
        CaseArtifactCustodyResult result)
    {
        var state = ToState(result.Disposition);
        var confirmed = state == IncomingArtifactCustodyState.Confirmed;
        return new(
            occurrence.OccurrenceId,
            occurrence.OperationKey,
            state,
            occurrence.CaseId,
            result.DocumentId,
            result.VersionId,
            // The remote identities are only meaningful for a confirmed
            // retention; carrying them for any other disposition would state
            // that custody holds something it has not said it holds.
            confirmed ? result.BoxFileId : null,
            confirmed ? result.BoxVersionId : null,
            result.FailureCode);
    }

    private static IncomingArtifactCustodyState ToState(
        CaseArtifactCustodyDisposition disposition) => disposition switch
        {
            CaseArtifactCustodyDisposition.Confirmed => IncomingArtifactCustodyState.Confirmed,
            CaseArtifactCustodyDisposition.Pending => IncomingArtifactCustodyState.Pending,
            CaseArtifactCustodyDisposition.Failed => IncomingArtifactCustodyState.Failed,
            CaseArtifactCustodyDisposition.Unknown => IncomingArtifactCustodyState.Unknown,
            _ => throw new ArgumentOutOfRangeException(
                nameof(disposition),
                "The custody disposition is not recognized.")
        };

    private static void Validate(ActionActor actor, IncomingArtifactOccurrence occurrence)
    {
        // Public submission is a request-link actor; staff and the system
        // worker retain through the same command on their own rights.
        if (!StaffAuthorization.IsAuthorized(actor, StaffAccessRight.SubmitRequestUpload)
            && !StaffAuthorization.IsAuthorized(actor, StaffAccessRight.PerformCasework)
            && !StaffAuthorization.IsAuthorized(actor, StaffAccessRight.ExecuteSystemWork))
        {
            throw new StaffAuthorizationException(StaffAccessRight.SubmitRequestUpload);
        }
        if (occurrence.OccurrenceId == Guid.Empty)
        {
            throw new ArgumentException(
                "A server-issued occurrence identity is required.",
                nameof(occurrence));
        }
        if (occurrence.CaseId is null && occurrence.IntakeReceiptId is null)
        {
            throw new ArgumentException(
                "An incoming artifact is retained against a Case or a holding receipt.",
                nameof(occurrence));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(occurrence.OperationKey, nameof(occurrence));
        ArgumentException.ThrowIfNullOrWhiteSpace(occurrence.ProposedFileName, nameof(occurrence));
        ArgumentException.ThrowIfNullOrWhiteSpace(occurrence.MediaType, nameof(occurrence));
        ArgumentException.ThrowIfNullOrWhiteSpace(occurrence.Sha256, nameof(occurrence));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(occurrence.ContentLength, nameof(occurrence));
    }
}
