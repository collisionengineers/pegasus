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
/// How a retention's recorded custody state is allowed to move.
/// </summary>
/// <remarks>
/// Confirmation is monotonic. A record only ever moves towards an answer
/// custody actually gave, so a recorder that knows less than the record
/// already does - a Pending or an Unknown arriving late, after custody
/// finished - cannot undo what is known. It is the same reason the states
/// exist at all: nothing may render as less certain than custody has been.
/// </remarks>
public static class IncomingArtifactCustodyProgress
{
    /// <summary>
    /// Whether <paramref name="next"/> is a forward move from
    /// <paramref name="current"/>. Unknown is the least that can be said -
    /// custody may hold this, and nothing more is known - Pending is more, and
    /// Confirmed and Failed are both final answers, so neither overwrites the
    /// other and nothing overwrites either.
    /// </summary>
    public static bool MovesForward(
        IncomingArtifactCustodyState current,
        IncomingArtifactCustodyState next) => Rank(next) > Rank(current);

    private static int Rank(IncomingArtifactCustodyState state) => state switch
    {
        IncomingArtifactCustodyState.Unknown => 0,
        IncomingArtifactCustodyState.Pending => 1,
        IncomingArtifactCustodyState.Confirmed or IncomingArtifactCustodyState.Failed => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };
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
/// <param name="Sha256">
/// The digest of the bytes this arrival was committed with, where its store
/// records one. It is what a hand-over offered under an operation key that
/// already has an arrival is checked against, so an operation key can never
/// come to name two different files.
/// </param>
/// <param name="ContentLength">
/// The length those same bytes were validated at, checked with the digest for
/// the same reason.
/// </param>
public sealed record RetainedIncomingArtifact(
    Guid OccurrenceId,
    string OperationKey,
    IncomingArtifactCustodyState State,
    Guid? CaseId = null,
    Guid? DocumentId = null,
    Guid? DocumentVersionId = null,
    string? BoxFileId = null,
    string? BoxVersionId = null,
    string? FailureCode = null,
    string? Sha256 = null,
    long? ContentLength = null)
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
    /// The durable retention record this operation key already has, if any.
    /// This is what makes a confirmed replay return the same logical document
    /// and version instead of retaining a second copy.
    /// </summary>
    /// <remarks>
    /// Every committed record is returned, including one custody has said
    /// nothing about yet: an arrival a store commits before the hand-over
    /// reads as <see cref="IncomingArtifactCustodyState.Unknown"/>, because
    /// that is exactly what is known about it. Reporting it as no record at
    /// all is what let two callers of one operation key both reach custody.
    /// Null therefore means only that this store holds nothing under the key.
    /// </remarks>
    Task<RetainedIncomingArtifact?> FindAsync(
        string operationKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Claims the committed arrival this occurrence addresses for the one
    /// hand-over it is allowed, and returns whether this caller won it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The claim is one conditional write - the arrival moves out of its
    /// pre-custody state, and only from it - so exactly one of any number of
    /// simultaneous callers can win. It is committed before the possibly
    /// accepting call, which is what makes a crash, a lost response or a
    /// failed recording of the result unable to reopen the hand-over: what is
    /// left behind is a claimed arrival that is reconciled, never one that is
    /// offered again.
    /// </para>
    /// <para>
    /// False is not a failure. It means another caller is holding the
    /// hand-over, or custody has already answered, and the losing caller must
    /// ask what became of the operation key rather than offer the bytes.
    /// </para>
    /// </remarks>
    Task<bool> TryClaimHandOverAsync(
        Guid occurrenceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the state of one occurrence. Called for every disposition, so a
    /// pending, failed or uncertain hand-over is as durable as a confirmed one
    /// and never silently disappears.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The write is monotonic, by
    /// <see cref="IncomingArtifactCustodyProgress.MovesForward"/>: a recorder
    /// that arrives after a further answer keeps what is recorded and its
    /// identities rather than replacing them with what it knew. Identities are
    /// filled in where they are missing and never erased, because the same
    /// logical document and version are what a later reconciliation asks
    /// about.
    /// </para>
    /// <para>
    /// The rule has to hold <em>between</em> recorders and not merely inside
    /// one, because two of them on one occurrence is an ordinary state of the
    /// world: a caller that lost the claim reconciles while the winner is
    /// still inside its hand-over. So a store applies the test and the write
    /// as one conditional operation the database decides, rather than reading
    /// the current state, judging it in memory and writing back - which is how
    /// a recorder that knows less wins a race against one that knows more.
    /// </para>
    /// </remarks>
    Task RecordAsync(
        RetainedIncomingArtifact artifact,
        CancellationToken cancellationToken);
}

/// <summary>
/// Raised when an incoming artifact is offered to custody without the
/// committed arrival every hand-over runs through.
/// </summary>
/// <remarks>
/// It is a caller defect, not an outcome: the bytes were never offered,
/// nothing was claimed and nothing was recorded, so there is no uncertainty to
/// reconcile and a retry that commits its arrival first is safe. It is typed
/// so a caller can tell it from the uncertain hand-over it is emphatically
/// not, and it carries no operation key, occurrence or Case, because the
/// public page logs what it catches.
/// </remarks>
public sealed class UnclaimedHandOverException()
    : InvalidOperationException(
        "An incoming artifact reaches custody only from an arrival its store "
        + "has already committed and this caller has claimed.");

/// <summary>
/// Raised when the bytes offered under an operation key are not the bytes the
/// arrival that key names was committed with.
/// </summary>
/// <remarks>
/// An operation key names one deliberate submission of one exact file, and a
/// retry under it re-offers that file or nothing. Bytes that differ are a
/// different submission and belong to a different key - never to this one, and
/// never handed to custody under it, because custody's own rule for a repeated
/// key is to return the intent it already has for it.
/// </remarks>
public sealed class HandOverContentMismatchException()
    : InvalidOperationException(
        "The bytes offered under this operation key are not the bytes its "
        + "committed arrival was validated with.");

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
/// <para>
/// One arrival is offered exactly once, and the store's claim is what makes
/// that true rather than a hope about timing. Every path to custody runs
/// through a claim this caller won and committed first, so simultaneous
/// callers of one operation key produce one hand-over and the rest reconcile,
/// and a crash between custody answering and the answer being written leaves a
/// claimed arrival to ask about rather than an arrival to offer again.
/// </para>
/// <para>
/// That is enforced, not assumed. A caller whose operation key names no
/// committed arrival is refused before custody is reached: there would be
/// nothing to claim, nothing to record the answer on, and nothing for a retry
/// to reconcile against, so offering the bytes would be exactly the unclaimed
/// hand-over the claim exists to prevent. Every caller commits its arrival
/// first - the public upload path does, and the holding destination this
/// command already carries must too. A hand-over is also only ever offered
/// the bytes its arrival was committed with, so one operation key can never
/// come to name two different files.
/// </para>
/// <para>
/// There is one hand-over a claim this caller did not win still permits, and
/// it is what keeps a claim from becoming a dead end. A claimed arrival that
/// names no document, and that custody owns up to nothing for, may never have
/// been offered at all: the process that took the claim can die between the
/// claim and the call. So the same validated bytes are offered again under the
/// same operation key - never a fresh one - and custody's own rule for a
/// repeated key is what keeps that one retention rather than two: a same-key
/// call after an intent exists returns that intent, and only the call that
/// creates it initiates storage (Stream A, PR 673 comment 5561151076, which
/// supersedes its earlier one-call rule). The invariant is one durable intent,
/// not one invocation.
/// </para>
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

    /// <summary>
    /// What a retention records when custody declined the authority. It is a
    /// refusal of that attempted acceptance and of nothing else, so it says
    /// only that, and never why.
    /// </summary>
    private const string RefusedFailureCode = "custody_refused";

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

        // No committed arrival, no hand-over. The bytes would reach custody
        // outside the one lifecycle that makes a retry safe, and the refusal
        // that closes a claim would have nothing to close.
        var existing = await store.FindAsync(occurrence.OperationKey, cancellationToken)
            ?? throw new UnclaimedHandOverException();
        RequireSameContent(existing, occurrence);

        // The two answers custody actually gave. A confirmed retention returns
        // the same logical document and version, and a refusal is final for
        // the acceptance it refused; neither is ever offered a second time
        // under this key, and only a new deliberate submission earns a new one.
        if (existing.State is IncomingArtifactCustodyState.Confirmed
            or IncomingArtifactCustodyState.Failed)
        {
            return existing;
        }

        // A Pending is custody stating that it has these bytes. It is asked
        // about, never repeated - whatever the answer, including none.
        if (existing.State is IncomingArtifactCustodyState.Pending)
        {
            return await ReconcileAsync(actor, existing, cancellationToken) ?? existing;
        }

        // Unknown is both the arrival nobody has offered yet and the hand-over
        // whose outcome was lost, because from here they are the same thing:
        // custody may hold these bytes. The store's conditional claim -
        // committed before the possibly accepting call - decides who offers
        // them first, and anyone else asks about the same operation key
        // before it does anything at all.
        if (!await store.TryClaimHandOverAsync(occurrence.OccurrenceId, cancellationToken))
        {
            if (await ReconcileAsync(actor, existing, cancellationToken) is { } asked)
            {
                return asked;
            }

            // Asked, and custody owns up to nothing for a claim that names no
            // document. That is not proof it holds nothing - a winner still
            // inside its call has committed nothing yet either - so what
            // follows must be safe in both worlds, and offering the same bytes
            // under the same key is: custody converges a repeated key on one
            // intent. It is the only way a claim whose holder died before it
            // ever called is ever resolved, and no fresh key is minted for it.
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
        catch (StaffAuthorizationException)
        {
            // A definite refusal of this attempted acceptance. Custody
            // committed no accepted intent, and bytes it read or staged on the
            // way to refusing are not one, so the claim this attempt holds is
            // closed as the refusal it is rather than left uncertain - which
            // is what lets the sender make a new deliberate submission under a
            // new key. There is always a claim to close, because nothing
            // reaches custody without one.
            await store.RecordAsync(
                new(
                    occurrence.OccurrenceId,
                    occurrence.OperationKey,
                    IncomingArtifactCustodyState.Failed,
                    occurrence.CaseId,
                    FailureCode: RefusedFailureCode,
                    Sha256: occurrence.Sha256,
                    ContentLength: occurrence.ContentLength),
                CancellationToken.None);
            throw;
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
                occurrence.CaseId,
                Sha256: occurrence.Sha256,
                ContentLength: occurrence.ContentLength);

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
    /// bytes. Everything does, except the one refusal custody states as a
    /// refusal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="StaffAuthorizationException"/> is custody declining the
    /// authority, which it settles before it commits an accepted intent
    /// whether or not it has read anything by then; it is handled as the
    /// definite refusal it is rather than as an uncertainty. Every other
    /// exception is uncertain - a dependency an adapter could not reach, a
    /// timeout, a database fault, a cancelled request, or a type this command
    /// has never heard of - because the type of a fault raised mid-call is not
    /// evidence about what custody kept, and the only safe reading of "the
    /// call did not return" is "custody may hold these bytes".
    /// </para>
    /// <para>
    /// <see cref="ArgumentException"/> is among the uncertain ones,
    /// deliberately. An adapter can raise one after it has committed as easily
    /// as before, so reading it as a refusal would license offering bytes
    /// custody already holds. Malformed input is refused by this command's own
    /// validation instead, which runs before anything is claimed or offered.
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
            or OutOfMemoryException
            or AccessViolationException);

    /// <summary>
    /// Asks custody what became of a hand-over that is neither confirmed nor
    /// refused - a Pending one it has not finished, an Unknown one it may
    /// never have received, or one another caller is holding the claim on
    /// right now. Without a status port, without a Case to ask about, or
    /// without the authority to ask, the retention keeps the state it had,
    /// which is honest and still never renders as success.
    /// </summary>
    /// <returns>
    /// The reconciled retention, or <see langword="null"/> for the one answer
    /// that is not a retention at all: nothing committed was observed for a
    /// claimed arrival that names no document, which leaves the caller free to
    /// offer the same bytes again under the same key. Every other outcome -
    /// including a lookup that observed nothing about an arrival custody has
    /// called Pending - is a record.
    /// </returns>
    /// <remarks>
    /// <para>
    /// There are two ways to ask, and which one is available is decided by
    /// what the record already knows. A retention that names a document and a
    /// version asks about that exact version. One that names neither - the
    /// hand-over whose response was lost before its identities could be
    /// written down - asks by the operation key it was accepted under, which
    /// is the only identity both sides still share. Recovered identities are
    /// copied onto the record, so the next question can be the precise one.
    /// </para>
    /// <para>
    /// A null lookup is not permission to start again. It says only that no
    /// committed intent was observed, which is exactly what a winner still
    /// inside its hand-over looks like, so the retention stays uncertain and
    /// the bytes are still never offered a second time under a fresh key.
    /// </para>
    /// <para>
    /// A refusal is not an error to report either: the retention stays exactly
    /// where it was, and a staff or system-worker retry - or custody finishing
    /// the Pending itself - still converges it.
    /// </para>
    /// </remarks>
    private async Task<RetainedIncomingArtifact?> ReconcileAsync(
        ActionActor actor,
        RetainedIncomingArtifact existing,
        CancellationToken cancellationToken)
    {
        // Nothing was observed because nothing could be asked. That is not the
        // observation a re-offer rests on, so the retention keeps the state it
        // had rather than being offered again on the strength of a question
        // that was never put.
        if (custodyStatus is null || existing.CaseId is not { } caseId)
        {
            return existing;
        }

        CaseArtifactCustodyResult? status;
        try
        {
            status = existing.DocumentId is { } documentId
                && existing.DocumentVersionId is { } versionId
                    ? await custodyStatus.GetAsync(
                        actor,
                        caseId,
                        documentId,
                        versionId,
                        Guid.Empty, // LOCAL VERIFICATION SHIM ONLY (C owns the G24 adaptation); never published
                        cancellationToken)
                    : await custodyStatus.FindByOperationKeyAsync(
                        actor,
                        caseId,
                        existing.OperationKey,
                        cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            // This actor may hand bytes over but may not read custody state.
            // Keeping what we already recorded is the honest answer.
            return existing;
        }

        if (status is null)
        {
            // No committed intent was observed. That is not evidence that none
            // will be, so the claim stands and the retention stays uncertain -
            // and for the identityless claim it is also the one observation
            // that lets the same bytes be offered again under the same key,
            // which the caller decides and this reports by answering nothing.
            // A Pending is custody's own word that it has the bytes, so it is
            // never re-offered however little a lookup can see.
            return existing.State == IncomingArtifactCustodyState.Unknown
                ? null
                : existing;
        }

        var state = ToState(status.Disposition);
        var confirmed = state == IncomingArtifactCustodyState.Confirmed;
        var reconciled = existing with
        {
            State = state,
            // Recovered identities are what make the next reconciliation the
            // precise one, and they are never unlearned once known.
            DocumentId = status.DocumentId ?? existing.DocumentId,
            DocumentVersionId = status.VersionId ?? existing.DocumentVersionId,
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
            result.FailureCode,
            occurrence.Sha256,
            occurrence.ContentLength);
    }

    /// <summary>
    /// Whether these are the bytes the arrival was committed with. A retry
    /// under an operation key re-offers that key's own file or nothing, so a
    /// digest or a length that does not match is refused here - before a claim
    /// is taken and before custody is asked - rather than handed over to
    /// become a second file under one key.
    /// </summary>
    /// <remarks>
    /// A store that records neither has nothing to check, and says so by
    /// carrying neither. It is not treated as a match to be lenient about: a
    /// store either knows what it committed or does not.
    /// </remarks>
    private static void RequireSameContent(
        RetainedIncomingArtifact existing,
        IncomingArtifactOccurrence occurrence)
    {
        if ((existing.Sha256 is { } sha256
                && !string.Equals(sha256, occurrence.Sha256, StringComparison.OrdinalIgnoreCase))
            || (existing.ContentLength is { } contentLength
                && contentLength != occurrence.ContentLength))
        {
            throw new HandOverContentMismatchException();
        }
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
