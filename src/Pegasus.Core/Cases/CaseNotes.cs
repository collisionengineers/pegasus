using Pegasus.Core.Identity;

namespace Pegasus.Core.Cases;

/// <summary>
/// A note an operator writes on a case. It joins the same timeline as what
/// Pegasus itself did — the vehicle lookup, custody, allocation, lifecycle
/// changes — because an operator reading a case wants one account of it, not
/// two lists to reconcile (CASE-017).
///
/// A note is a material action and is recorded as one: append-only, attributed,
/// and never editable afterwards. It is not a way to revise the record.
/// </summary>
public sealed record AddCaseNoteRequest(
    Guid CaseId,
    ActionActor Actor,
    string OperationKey,
    string Note);

public interface IAddCaseNote
{
    Task ExecuteAsync(AddCaseNoteRequest request, CancellationToken cancellationToken);
}

public interface ICaseNoteStore
{
    Task AddAsync(
        AddCaseNoteRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken);
}

public sealed class AddCaseNote(ICaseNoteStore store, TimeProvider timeProvider) : IAddCaseNote
{
    /// <summary>
    /// The history event type an operator note is recorded under. Every other
    /// entry on the timeline names something the system did; this one names
    /// something a person wrote.
    /// </summary>
    public const string EventType = "operator_note";

    public const int MaximumLength = 2000;

    public Task ExecuteAsync(AddCaseNoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        // The operator asked for a note a *user* writes. The Automation Actor
        // holds casework rights and already records what it does on this same
        // timeline under its own events; letting it author a note as well would
        // put machine text where a colleague's words are expected.
        if (request.Actor.Kind != ActorKind.Staff)
        {
            throw new StaffAuthorizationException(StaffAccessRight.PerformCasework);
        }

        if (request.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(request));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
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
            request with { Note = note, OperationKey = request.OperationKey.Trim() },
            timeProvider.GetUtcNow(),
            cancellationToken);
    }
}
