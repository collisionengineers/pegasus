using Pegasus.Core.Identity;
using System.Security.Cryptography;
using System.Text;

namespace Pegasus.Core.ReleaseNotes;

/// <summary>
/// The build the application is running: its product version and the source
/// SHA stamped into the assembly. Publishing a release note records both, so
/// a note is tied to the deployment it describes.
/// </summary>
public sealed record ApplicationBuild(string Version, string SourceSha);

public enum ReleaseNoteStatus
{
    Draft,
    Published
}

/// <summary>
/// One release note: what changed in a deployment, written by an Administrator
/// in the application and published by their press. A published note is
/// immutable; every staff member sees the newest published note once, in the
/// shell's What's new dialog, until they acknowledge it.
/// </summary>
public sealed record ReleaseNote(
    Guid Id,
    string Title,
    string Body,
    ReleaseNoteStatus Status,
    string? Version,
    string? SourceSha,
    Guid CreatedByStaffId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    Guid? PublishedByStaffId,
    DateTimeOffset? PublishedAtUtc,
    long RowVersion)
{
    public bool IsPublished => Status == ReleaseNoteStatus.Published;
}

public sealed record NewReleaseNote(
    string Title, string Body, Guid CreatedByStaffId, DateTimeOffset AtUtc,
    string OperationKey, string RequestHash);

/// <summary>Thrown when a note changed under the Administrator's edit, or is not a draft any more.</summary>
public sealed class ReleaseNoteConflictException(Guid noteId)
    : InvalidOperationException($"Release note {noteId:D} changed before this edit was saved.");

public interface IReleaseNoteStore
{
    Task<ReleaseNote?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Every note, newest first by its last change.</summary>
    Task<IReadOnlyList<ReleaseNote>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Published notes, newest published first.</summary>
    Task<IReadOnlyList<ReleaseNote>> ListPublishedAsync(CancellationToken cancellationToken);

    Task<ReleaseNote> AddAsync(NewReleaseNote note, CancellationToken cancellationToken);

    /// <summary>Rewrites a draft's title and body; a stale row version or a published note throws <see cref="ReleaseNoteConflictException"/>.</summary>
    Task<ReleaseNote> UpdateDraftAsync(
        Guid id,
        long expectedRowVersion,
        string title,
        string body,
        DateTimeOffset atUtc,
        CancellationToken cancellationToken);

    /// <summary>Publishes a draft, stamping the build; a stale row version or an already published note throws <see cref="ReleaseNoteConflictException"/>.</summary>
    Task<ReleaseNote> PublishAsync(
        Guid id,
        long expectedRowVersion,
        string title,
        string body,
        ApplicationBuild build,
        Guid publishedByStaffId,
        DateTimeOffset atUtc,
        CancellationToken cancellationToken);

    /// <summary>The newest published note the person has not acknowledged, or null.</summary>
    Task<ReleaseNote?> GetUnacknowledgedAsync(Guid staffId, CancellationToken cancellationToken);

    /// <summary>Records that the person has seen the note; a second acknowledgement of the same note changes nothing.</summary>
    Task AcknowledgeAsync(Guid staffId, Guid noteId, DateTimeOffset atUtc, CancellationToken cancellationToken);
}

/// <summary>
/// What a release note may hold. The words are the Administrator's; nothing
/// here writes or rewrites them.
/// </summary>
public static class ReleaseNotePolicy
{
    public const int MaximumTitleLength = 120;
    public const int MaximumBodyLength = 8000;

    public static string NormalizeOperationKey(string? key) =>
        Guid.TryParse(key, out var value) && value != Guid.Empty
            ? value.ToString("N")
            : throw new ArgumentException("The release note form has expired. Open it again.", nameof(key));

    public static string RequestHash(Guid staffId, string title, string body) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{staffId:D}\n{title.Length}:{title}\n{body.Length}:{body}")));

    public static string ValidateTitle(string? title)
    {
        var value = (title ?? string.Empty).Trim();
        if (value.Length == 0 || value.Length > MaximumTitleLength || value.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"A release note title is 1 to {MaximumTitleLength} characters on one line.", nameof(title));
        }

        return value;
    }

    public static string ValidateBody(string? body)
    {
        var value = (body ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (value.Length == 0 || value.Length > MaximumBodyLength
            || value.Any(character => char.IsControl(character) && character != '\n'))
        {
            throw new ArgumentException(
                $"A release note body is 1 to {MaximumBodyLength} characters.", nameof(body));
        }

        return value;
    }

    public static Guid RequireStaff(ActionActor actor, StaffAccessRight right)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, right);
        return actor.Kind == ActorKind.Staff && Guid.TryParse(actor.SubjectId, out var id) && id != Guid.Empty
            ? id
            : throw new StaffAuthorizationException(right);
    }
}

/// <summary>
/// The Administrator's side: drafts are written and rewritten, then published.
/// Only a signed-in Administrator holds <see cref="StaffAccessRight.PublishReleaseNotes"/>;
/// the Automation Actor and every other actor kind are refused, so no note
/// reaches staff without an Administrator's press.
/// </summary>
public sealed class ReleaseNoteAdministration(
    IReleaseNoteStore store,
    ApplicationBuild build,
    TimeProvider timeProvider)
{
    private readonly IReleaseNoteStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ApplicationBuild _build = build ?? throw new ArgumentNullException(nameof(build));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public Task<IReadOnlyList<ReleaseNote>> ListAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        ReleaseNotePolicy.RequireStaff(actor, StaffAccessRight.PublishReleaseNotes);
        return _store.ListAsync(cancellationToken);
    }

    public Task<ReleaseNote?> GetAsync(ActionActor actor, Guid id, CancellationToken cancellationToken)
    {
        ReleaseNotePolicy.RequireStaff(actor, StaffAccessRight.PublishReleaseNotes);
        return _store.GetAsync(id, cancellationToken);
    }

    /// <summary>Creates a draft, or rewrites the draft <paramref name="id"/> names at <paramref name="expectedRowVersion"/>.</summary>
    public async Task<ReleaseNote> SaveDraftAsync(
        ActionActor actor,
        Guid? id,
        long? expectedRowVersion,
        string? title,
        string? body,
        string operationKey,
        CancellationToken cancellationToken)
    {
        var staffId = ReleaseNotePolicy.RequireStaff(actor, StaffAccessRight.PublishReleaseNotes);
        var validTitle = ReleaseNotePolicy.ValidateTitle(title);
        var validBody = ReleaseNotePolicy.ValidateBody(body);
        var now = _timeProvider.GetUtcNow();
        if (id is not { } noteId || noteId == Guid.Empty)
        {
            var key = ReleaseNotePolicy.NormalizeOperationKey(operationKey);
            var hash = ReleaseNotePolicy.RequestHash(staffId, validTitle, validBody);
            return await _store.AddAsync(
                new NewReleaseNote(validTitle, validBody, staffId, now, key, hash), cancellationToken);
        }

        if (expectedRowVersion is not { } rowVersion)
        {
            throw new ArgumentException("An existing draft is saved with its row version.", nameof(expectedRowVersion));
        }

        return await _store.UpdateDraftAsync(noteId, rowVersion, validTitle, validBody, now, cancellationToken);
    }

    /// <summary>Publishes the draft as it stands, stamped with the running build.</summary>
    public async Task<ReleaseNote> PublishAsync(
        ActionActor actor,
        Guid id,
        long expectedRowVersion,
        string? title,
        string? body,
        CancellationToken cancellationToken)
    {
        var staffId = ReleaseNotePolicy.RequireStaff(actor, StaffAccessRight.PublishReleaseNotes);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A release note identifier is required.", nameof(id));
        }

        var validTitle = ReleaseNotePolicy.ValidateTitle(title);
        var validBody = ReleaseNotePolicy.ValidateBody(body);
        return await _store.PublishAsync(
            id, expectedRowVersion, validTitle, validBody, _build, staffId,
            _timeProvider.GetUtcNow(), cancellationToken);
    }
}

/// <summary>The signed-in person's side: the note to show them, their acknowledgement, and the history.</summary>
public interface IMyReleaseNotes
{
    /// <summary>The newest published note this person has not yet acknowledged, or null.</summary>
    Task<ReleaseNote?> GetUnacknowledgedAsync(ActionActor actor, CancellationToken cancellationToken);

    Task AcknowledgeAsync(ActionActor actor, Guid noteId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReleaseNote>> ListPublishedAsync(ActionActor actor, CancellationToken cancellationToken);
}

public sealed class MyReleaseNotes(IReleaseNoteStore store, TimeProvider timeProvider) : IMyReleaseNotes
{
    private readonly IReleaseNoteStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public Task<ReleaseNote?> GetUnacknowledgedAsync(ActionActor actor, CancellationToken cancellationToken) =>
        _store.GetUnacknowledgedAsync(
            ReleaseNotePolicy.RequireStaff(actor, StaffAccessRight.AccessStaffApplication), cancellationToken);

    public Task AcknowledgeAsync(ActionActor actor, Guid noteId, CancellationToken cancellationToken)
    {
        var staffId = ReleaseNotePolicy.RequireStaff(actor, StaffAccessRight.AccessStaffApplication);
        if (noteId == Guid.Empty)
        {
            throw new ArgumentException("A release note identifier is required.", nameof(noteId));
        }

        return _store.AcknowledgeAsync(staffId, noteId, _timeProvider.GetUtcNow(), cancellationToken);
    }

    public Task<IReadOnlyList<ReleaseNote>> ListPublishedAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        ReleaseNotePolicy.RequireStaff(actor, StaffAccessRight.AccessStaffApplication);
        return _store.ListPublishedAsync(cancellationToken);
    }
}
