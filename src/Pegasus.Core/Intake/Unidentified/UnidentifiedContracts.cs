using System.Globalization;
using System.Text.RegularExpressions;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Triage;

namespace Pegasus.Core.Intake.Unidentified;

/// <summary>One canonical reason taxonomy for safely retained unidentified material.</summary>
public enum UnidentifiedReasonCode
{
    UnreadableOrCorruptContent,
    UnsupportedContent,
    NoUsableIdentification,
    ConflictingIdentification,
    AmbiguousOwnershipOrDestination,
    TechnicalProcessingFailure
}

public enum UnidentifiedState
{
    Open,
    Resolved
}

public enum UnidentifiedOriginKind
{
    Receipt,
    SubmissionGroup
}

public enum UnidentifiedResolutionTargetKind
{
    InstructionCase,
    ImageIntake,
    Triage,
    BlockedIntake,
    ExternalReference
}

public sealed record UnidentifiedOrigin(UnidentifiedOriginKind Kind, Guid Id)
{
    public static UnidentifiedOrigin Receipt(Guid id) => new(UnidentifiedOriginKind.Receipt, id);

    public static UnidentifiedOrigin SubmissionGroup(Guid id) =>
        new(UnidentifiedOriginKind.SubmissionGroup, id);

    public static void Validate(UnidentifiedOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (origin.Id == Guid.Empty)
        {
            throw new ArgumentException("An Unidentified origin requires a non-empty identifier.", nameof(origin));
        }
        if (!Enum.IsDefined(origin.Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(origin), "The Unidentified origin kind is not recognised.");
        }
    }
}

public static class UnidentifiedReferenceFormat
{
    private static readonly Regex Canonical = new("^U[1-9][0-9]*$", RegexOptions.CultureInvariant);

    public static string Create(long sequence)
    {
        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "An Unidentified sequence must be positive.");
        }

        return $"U{sequence.ToString(CultureInfo.InvariantCulture)}";
    }

    public static bool TryParse(string? value, out long sequence)
    {
        sequence = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        if (!Canonical.IsMatch(candidate)
            || !long.TryParse(candidate.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out sequence)
            || sequence <= 0)
        {
            sequence = 0;
            return false;
        }

        return string.Equals(Create(sequence), candidate, StringComparison.Ordinal);
    }

    public static long Parse(string value) =>
        TryParse(value, out var sequence)
            ? sequence
            : throw new FormatException("The value is not a canonical Unidentified reference.");
}

public sealed record UnidentifiedItem(
    Guid Id,
    long Sequence,
    string Reference,
    UnidentifiedOrigin Origin,
    UnidentifiedReasonCode ReasonCode,
    string SafeDetail,
    UnidentifiedState State,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    ActionActor CreatedBy,
    ActionActor? ResolvedBy,
    string? ResolutionReason,
    UnidentifiedResolutionTargetKind? ResolutionTargetKind,
    string? ResolutionTargetId,
    string? ResolutionTargetReference,
    long Version)
{
    public static void Validate(UnidentifiedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        UnidentifiedOrigin.Validate(item.Origin);
        if (item.Id == Guid.Empty || item.Sequence <= 0 || !string.Equals(
                item.Reference,
                UnidentifiedReferenceFormat.Create(item.Sequence),
                StringComparison.Ordinal))
        {
            throw new ArgumentException("The Unidentified identity is invalid.", nameof(item));
        }
        if (!Enum.IsDefined(item.ReasonCode) || !Enum.IsDefined(item.State))
        {
            throw new ArgumentOutOfRangeException(nameof(item), "The Unidentified state or reason is not recognised.");
        }
        UnidentifiedValidation.RequireDetail(item.SafeDetail);
        if (item.Version < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(item), "The Unidentified version cannot be negative.");
        }
        if (item.State == UnidentifiedState.Open
            && (item.ResolvedAtUtc is not null
                || item.ResolvedBy is not null
                || item.ResolutionReason is not null
                || item.ResolutionTargetKind is not null
                || item.ResolutionTargetId is not null
                || item.ResolutionTargetReference is not null))
        {
            throw new ArgumentException("An open Unidentified item cannot contain resolution fields.", nameof(item));
        }
    }
}

public sealed record UnidentifiedHistoryEntry(
    Guid Id,
    Guid UnidentifiedItemId,
    UnidentifiedState PreviousState,
    UnidentifiedState NewState,
    ActionActor Actor,
    DateTimeOffset OccurredAtUtc,
    string Reason,
    string OperationKey,
    UnidentifiedResolutionTargetKind? TargetKind,
    string? TargetId,
    string? TargetReference);

public sealed record RegisterUnidentifiedRequest(
    UnidentifiedOrigin Origin,
    UnidentifiedReasonCode ReasonCode,
    string SafeDetail,
    ActionActor Actor,
    string OperationKey,
    DateTimeOffset CreatedAtUtc);

public sealed record ResolveUnidentifiedRequest(
    Guid UnidentifiedItemId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    UnidentifiedResolutionTargetKind TargetKind,
    string TargetId,
    string? TargetReference,
    DateTimeOffset ResolvedAtUtc);

public sealed record UnidentifiedRegisterResult(UnidentifiedItem Item, bool IsReplay);

public sealed record UnidentifiedResolveResult(UnidentifiedItem Item, UnidentifiedHistoryEntry History, bool IsReplay);

public interface IUnidentifiedStore
{
    Task<UnidentifiedRegisterResult> RegisterAsync(
        RegisterUnidentifiedRequest request,
        CancellationToken cancellationToken = default);

    Task<UnidentifiedRegisterResult?> ProbeRegisterReplayAsync(
        RegisterUnidentifiedRequest request,
        CancellationToken cancellationToken = default);

    Task<UnidentifiedResolveResult> ResolveAsync(
        ResolveUnidentifiedRequest request,
        CancellationToken cancellationToken = default);

    Task<UnidentifiedResolveResult?> ProbeResolveReplayAsync(
        ResolveUnidentifiedRequest request,
        CancellationToken cancellationToken = default);

    Task<UnidentifiedItem?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UnidentifiedItem?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>
    /// The item registered for this origin, if one exists, regardless of
    /// state. Backed by the unique OriginKind/OriginId index; used to
    /// reconcile a stale open item once its source receipt reaches a
    /// different, non-Unidentified outcome.
    /// </summary>
    Task<UnidentifiedItem?> GetByOriginAsync(
        UnidentifiedOrigin origin,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UnidentifiedItem>> ListAsync(
        UnidentifiedState? state = UnidentifiedState.Open,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UnidentifiedHistoryEntry>> HistoryAsync(
        Guid unidentifiedItemId,
        CancellationToken cancellationToken = default);
}

public interface IRegisterUnidentified
{
    Task<UnidentifiedRegisterResult> ExecuteAsync(
        RegisterUnidentifiedRequest request,
        CancellationToken cancellationToken = default);
}

public interface IResolveUnidentified
{
    Task<UnidentifiedResolveResult> ExecuteAsync(
        ResolveUnidentifiedRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class RegisterUnidentified(IUnidentifiedStore store) : IRegisterUnidentified
{
    public async Task<UnidentifiedRegisterResult> ExecuteAsync(
        RegisterUnidentifiedRequest request,
        CancellationToken cancellationToken = default)
    {
        UnidentifiedValidation.ValidateRegister(request);
        return await store.RegisterAsync(request, cancellationToken);
    }
}

/// <summary>
/// Resolving an Unidentified item requires the selected destination to
/// actually exist: a typo or fabricated <c>TargetId</c> must not be able to
/// permanently remove work from the open queue with no supported destination
/// behind it. Each destination port is optional so a deployment lacking one
/// of these capabilities still fails closed for that target kind rather than
/// throwing a missing-service error; <see cref="UnidentifiedResolutionTargetKind.ExternalReference"/>
/// is free-form by design and has no Core-owned port to validate against.
/// </summary>
public sealed class ResolveUnidentified(
    IUnidentifiedStore store,
    ICaseQueryStore? caseQueries = null,
    IImageIntakeQueries? imageIntakeQueries = null,
    ITriageQueries? triageQueries = null,
    IIntakeReceiptQueries? intakeReceiptQueries = null) : IResolveUnidentified
{
    public async Task<UnidentifiedResolveResult> ExecuteAsync(
        ResolveUnidentifiedRequest request,
        CancellationToken cancellationToken = default)
    {
        UnidentifiedValidation.ValidateResolve(request);
        await EnsureDestinationExistsAsync(request, cancellationToken);
        return await store.ResolveAsync(request, cancellationToken);
    }

    private async Task EnsureDestinationExistsAsync(
        ResolveUnidentifiedRequest request,
        CancellationToken cancellationToken)
    {
        var targetId = request.TargetId.Trim();
        var exists = request.TargetKind switch
        {
            UnidentifiedResolutionTargetKind.InstructionCase => Guid.TryParse(targetId, out var caseId)
                && caseQueries is not null
                && await caseQueries.GetAsync(new(caseId, request.Actor), cancellationToken) is not null,
            UnidentifiedResolutionTargetKind.ImageIntake => Guid.TryParse(targetId, out var imageIntakeId)
                && imageIntakeQueries is not null
                && await imageIntakeQueries.GetAsync(imageIntakeId, cancellationToken) is not null,
            UnidentifiedResolutionTargetKind.Triage => Guid.TryParse(targetId, out var triageId)
                && triageQueries is not null
                && await triageQueries.GetAsync(triageId, cancellationToken) is not null,
            UnidentifiedResolutionTargetKind.BlockedIntake => Guid.TryParse(targetId, out var receiptId)
                && intakeReceiptQueries is not null
                && await intakeReceiptQueries.GetAsync(receiptId, cancellationToken) is
                    { Decision: IntakeDecision.BlockedIntake },
            // Free-form external reference; no Core-owned destination to validate.
            UnidentifiedResolutionTargetKind.ExternalReference => true,
            _ => throw new ArgumentOutOfRangeException(
                nameof(request), "The resolution target is not recognised.")
        };
        if (!exists)
        {
            throw new UnidentifiedResolutionTargetNotFoundException(request.TargetKind, targetId);
        }
    }
}

public static class UnidentifiedValidation
{
    public const int MaximumDetailLength = 1000;
    public const int MaximumReasonLength = 500;
    public const int MaximumOperationKeyLength = 200;
    public const int MaximumTargetIdLength = 200;
    public const int MaximumTargetReferenceLength = 200;

    public static void ValidateRegister(RegisterUnidentifiedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        UnidentifiedOrigin.Validate(request.Origin);
        if (!Enum.IsDefined(request.ReasonCode))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The Unidentified reason is not recognised.");
        }
        RequireDetail(request.SafeDetail);
        RequireOperation(request.OperationKey);
        RequireActorForRegistration(request.Actor);
        RequireUtc(request.CreatedAtUtc, nameof(request.CreatedAtUtc));
    }

    public static void ValidateResolve(ResolveUnidentifiedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.UnidentifiedItemId == Guid.Empty || request.ExpectedVersion < 0)
        {
            throw new ArgumentException("A resolution requires a valid item and expected version.", nameof(request));
        }
        RequireStaffOrAutomation(request.Actor);
        RequireOperation(request.OperationKey);
        RequireText(request.Reason, MaximumReasonLength, nameof(request.Reason));
        RequireText(request.TargetId, MaximumTargetIdLength, nameof(request.TargetId));
        if (request.TargetReference is not null)
        {
            RequireText(request.TargetReference, MaximumTargetReferenceLength, nameof(request.TargetReference));
        }
        if (!Enum.IsDefined(request.TargetKind))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The resolution target is not recognised.");
        }
        RequireUtc(request.ResolvedAtUtc, nameof(request.ResolvedAtUtc));
    }

    public static void RequireDetail(string value) => RequireText(value, MaximumDetailLength, nameof(value));

    private static void RequireOperation(string value) => RequireText(value, MaximumOperationKeyLength, nameof(value));

    private static void RequireActorForRegistration(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Kind is not (ActorKind.Staff or ActorKind.SystemWorker or ActorKind.Automation))
        {
            throw new UnauthorizedAccessException("This actor cannot register Unidentified material.");
        }
    }

    private static void RequireStaffOrAutomation(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Kind is not (ActorKind.Staff or ActorKind.Automation))
        {
            throw new UnauthorizedAccessException("Only staff or authorised automation can resolve Unidentified material.");
        }
    }

    private static void RequireText(string value, int maximum, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Trim().Length > maximum)
        {
            throw new ArgumentException($"{parameterName} exceeds its maximum length of {maximum}.", parameterName);
        }
    }

    private static void RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamps must be UTC.", parameterName);
        }
    }
}

public sealed class UnidentifiedOperationConflictException() : InvalidOperationException(
    "The operation key was already used for a different Unidentified request.");

public sealed class UnidentifiedVersionConflictException() : InvalidOperationException(
    "The Unidentified item changed; reload it before resolving.");

/// <summary>
/// An <see cref="ArgumentException"/> so the existing Web resolve-form error
/// handling (which already surfaces any <c>ArgumentException</c> as a model
/// error) reports it without a separate catch clause.
/// </summary>
public sealed class UnidentifiedResolutionTargetNotFoundException(
    UnidentifiedResolutionTargetKind targetKind,
    string targetId)
    : ArgumentException(
        $"No {targetKind} destination exists for target '{targetId}'.",
        nameof(ResolveUnidentifiedRequest.TargetId));
