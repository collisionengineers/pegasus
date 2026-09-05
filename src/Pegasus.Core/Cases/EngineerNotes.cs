using Pegasus.Core.Identity;

namespace Pegasus.Core.Cases;

/// <summary>An attributed, append-only note addressed to the Engineer.</summary>
public sealed record AddEngineerNoteRequest(
    Guid CaseId,
    ActionActor Actor,
    long ExpectedVersion,
    string OperationKey,
    string Note,
    string EditLeaseToken);

public sealed record EngineerNote(
    Guid Id,
    Guid CaseId,
    Guid RecordedByStaffId,
    string Note,
    DateTimeOffset RecordedAtUtc);

public interface IAddEngineerNote
{
    Task ExecuteAsync(AddEngineerNoteRequest request, CancellationToken cancellationToken);
}

public interface IEngineerNoteStore
{
    Task AddAsync(
        AddEngineerNoteRequest request,
        DateTimeOffset recordedAtUtc,
        CancellationToken cancellationToken);
}

public interface IEngineerNoteQueries
{
    Task<IReadOnlyList<EngineerNote>> ListNewestFirstAsync(
        Guid caseId,
        CancellationToken cancellationToken);
}

public sealed class AddEngineerNote(
    IEngineerNoteStore store,
    TimeProvider timeProvider) : IAddEngineerNote
{
    public const int MaximumLength = 2000;

    public Task ExecuteAsync(
        AddEngineerNoteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        if (request.Actor.Kind != ActorKind.Staff)
        {
            throw new StaffAuthorizationException(StaffAccessRight.PerformCasework);
        }

        if (request.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(request));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.EditLeaseToken);
        var note = request.Note?.Trim();
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new ArgumentException("A note is required.", nameof(request));
        }
        if (note.Length > MaximumLength)
        {
            throw new ArgumentException(
                $"A note cannot exceed {MaximumLength} characters.",
                nameof(request));
        }

        return store.AddAsync(
            request with
            {
                OperationKey = request.OperationKey.Trim(),
                Note = note,
                EditLeaseToken = request.EditLeaseToken.Trim()
            },
            timeProvider.GetUtcNow(),
            cancellationToken);
    }
}
