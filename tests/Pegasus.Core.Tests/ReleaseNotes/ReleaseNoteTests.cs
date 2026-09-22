using Pegasus.Core.Identity;
using Pegasus.Core.ReleaseNotes;

namespace Pegasus.Core.Tests.ReleaseNotes;

public sealed class ReleaseNoteTests
{
    private static readonly Guid AdministratorId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly ApplicationBuild Build = new("0.1.0-alpha.1", new string('a', 40));
    private static readonly TestClock Clock = new(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));

    private static ActionActor Administrator() => ActionActor.Staff(AdministratorId, [StaffRole.Administrator]);
    private static string Key() => Guid.NewGuid().ToString("N");
    private static ActionActor User() => ActionActor.Staff(UserId, [StaffRole.User]);
    private static ActionActor Engineer() => ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

    [Fact]
    public async Task AnAdministratorWritesADraftAndPublishesItStampedWithTheBuild()
    {
        var store = new FakeStore();
        var administration = new ReleaseNoteAdministration(store, Build, Clock);

        var draft = await administration.SaveDraftAsync(Administrator(), null, null, "  Repair Spec  ", "Line one\r\n\r\n- a\r\n- b\r\n", Key(), default);
        Assert.Equal(ReleaseNoteStatus.Draft, draft.Status);
        Assert.Equal("Repair Spec", draft.Title);
        Assert.Equal("Line one\n\n- a\n- b", draft.Body);
        Assert.Null(draft.Version);

        var rewritten = await administration.SaveDraftAsync(Administrator(), draft.Id, draft.RowVersion, "Repair Spec and images", draft.Body, Key(), default);
        Assert.Equal(draft.RowVersion + 1, rewritten.RowVersion);

        var published = await administration.PublishAsync(Administrator(), rewritten.Id, rewritten.RowVersion, rewritten.Title, rewritten.Body, default);
        Assert.Equal(ReleaseNoteStatus.Published, published.Status);
        Assert.Equal(Build.Version, published.Version);
        Assert.Equal(Build.SourceSha, published.SourceSha);
        Assert.Equal(AdministratorId, published.PublishedByStaffId);
        Assert.Equal(Clock.GetUtcNow(), published.PublishedAtUtc);
    }

    [Fact]
    public async Task CreateAndPublishReplaysReturnTheSameNoteAndChangedCreateInputConflicts()
    {
        var store = new FakeStore();
        var administration = new ReleaseNoteAdministration(store, Build, Clock);
        var key = Key();

        var draft = await administration.SaveDraftAsync(
            Administrator(), null, null, "First", "Body", key, default);
        var replay = await administration.SaveDraftAsync(
            Administrator(), null, null, "First", "Body", key, default);
        Assert.Equal(draft.Id, replay.Id);
        Assert.Single(store.Notes);
        await Assert.ThrowsAsync<ReleaseNoteConflictException>(() =>
            administration.SaveDraftAsync(
                Administrator(), null, null, "Changed", "Body", key, default));

        var published = await administration.PublishAsync(
            Administrator(), draft.Id, draft.RowVersion, "Edited on publish", "Final body", default);
        var publishedReplay = await administration.PublishAsync(
            Administrator(), draft.Id, draft.RowVersion, "Edited on publish", "Final body", default);
        Assert.Equal(published.Id, publishedReplay.Id);
        Assert.Equal(published.RowVersion, publishedReplay.RowVersion);
        Assert.Equal("Edited on publish", published.Title);
        Assert.Equal("Final body", published.Body);
        Assert.Single(store.Notes);
    }

    [Fact]
    public async Task OnlyASignedInAdministratorMayWriteOrPublish()
    {
        var store = new FakeStore();
        var administration = new ReleaseNoteAdministration(store, Build, Clock);
        var automation = ActionActor.Automation("automation-client");

        foreach (var actor in new[] { User(), Engineer(), automation })
        {
            await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
                administration.SaveDraftAsync(actor, null, null, "Title", "Body", Key(), default));
            await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
                administration.PublishAsync(actor, Guid.NewGuid(), 1, "Title", "Body", default));
            await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
                administration.ListAsync(actor, default));
        }

        Assert.Empty(store.Notes);
        Assert.False(StaffAuthorization.IsAuthorized(automation, StaffAccessRight.PublishReleaseNotes));
        Assert.True(StaffAuthorization.IsAuthorized(Administrator(), StaffAccessRight.PublishReleaseNotes));
    }

    [Fact]
    public async Task AnEmptyOrOversizedNoteIsRefusedBeforeItIsStored()
    {
        var store = new FakeStore();
        var administration = new ReleaseNoteAdministration(store, Build, Clock);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            administration.SaveDraftAsync(Administrator(), null, null, "   ", "Body", Key(), default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            administration.SaveDraftAsync(Administrator(), null, null, "Title", "", Key(), default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            administration.SaveDraftAsync(Administrator(), null, null, new string('t', ReleaseNotePolicy.MaximumTitleLength + 1), "Body", Key(), default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            administration.SaveDraftAsync(Administrator(), null, null, "Title", new string('b', ReleaseNotePolicy.MaximumBodyLength + 1), Key(), default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            administration.SaveDraftAsync(Administrator(), null, null, "Two\nlines", "Body", Key(), default));
        Assert.Empty(store.Notes);
    }

    [Fact]
    public async Task APublishedNoteCannotBeRewrittenAndAStaleDraftIsRefused()
    {
        var store = new FakeStore();
        var administration = new ReleaseNoteAdministration(store, Build, Clock);
        var draft = await administration.SaveDraftAsync(Administrator(), null, null, "Title", "Body", Key(), default);

        await Assert.ThrowsAsync<ReleaseNoteConflictException>(() =>
            administration.SaveDraftAsync(Administrator(), draft.Id, draft.RowVersion + 5, "Other", "Body", Key(), default));

        var published = await administration.PublishAsync(Administrator(), draft.Id, draft.RowVersion, draft.Title, draft.Body, default);
        await Assert.ThrowsAsync<ReleaseNoteConflictException>(() =>
            administration.SaveDraftAsync(Administrator(), published.Id, published.RowVersion, "Other", "Body", Key(), default));
        await Assert.ThrowsAsync<ReleaseNoteConflictException>(() =>
            administration.PublishAsync(Administrator(), published.Id, published.RowVersion, published.Title, published.Body, default));
    }

    [Fact]
    public async Task EachPersonSeesTheNewestPublishedNoteOnceUntilTheyAcknowledgeIt()
    {
        var store = new FakeStore();
        var administration = new ReleaseNoteAdministration(store, Build, Clock);
        var mine = new MyReleaseNotes(store, Clock);

        Assert.Null(await mine.GetUnacknowledgedAsync(User(), default));
        var draft = await administration.SaveDraftAsync(Administrator(), null, null, "Unpublished", "Body", Key(), default);
        Assert.Null(await mine.GetUnacknowledgedAsync(User(), default)); // A draft is nobody's news.

        var first = await administration.PublishAsync(Administrator(), draft.Id, draft.RowVersion, draft.Title, draft.Body, default);
        Assert.Equal(first.Id, (await mine.GetUnacknowledgedAsync(User(), default))?.Id);
        Assert.Equal(first.Id, (await mine.GetUnacknowledgedAsync(Administrator(), default))?.Id);

        await mine.AcknowledgeAsync(User(), first.Id, default);
        await mine.AcknowledgeAsync(User(), first.Id, default); // Idempotent.
        Assert.Null(await mine.GetUnacknowledgedAsync(User(), default));
        Assert.Equal(first.Id, (await mine.GetUnacknowledgedAsync(Administrator(), default))?.Id);

        Clock.Advance(TimeSpan.FromMinutes(1));
        var next = await administration.SaveDraftAsync(Administrator(), null, null, "Second", "Body", Key(), default);
        var second = await administration.PublishAsync(Administrator(), next.Id, next.RowVersion, next.Title, next.Body, default);
        Assert.Equal(second.Id, (await mine.GetUnacknowledgedAsync(User(), default))?.Id);
        Assert.Equal([second.Id, first.Id], (await mine.ListPublishedAsync(User(), default)).Select(note => note.Id));
    }

    [Fact]
    public async Task TheAutomationActorCannotReadOrAcknowledgeNotes()
    {
        var mine = new MyReleaseNotes(new FakeStore(), Clock);
        var automation = ActionActor.Automation("automation-client");

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => mine.GetUnacknowledgedAsync(automation, default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => mine.AcknowledgeAsync(automation, Guid.NewGuid(), default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => mine.ListPublishedAsync(automation, default));
    }

    private sealed class TestClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class FakeStore : IReleaseNoteStore
    {
        public List<ReleaseNote> Notes { get; } = [];
        private readonly Dictionary<string, (string Hash, Guid Id)> _creations = [];
        private readonly HashSet<(Guid Staff, Guid Note)> _acknowledged = [];

        public Task<ReleaseNote?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Notes.SingleOrDefault(note => note.Id == id));

        public Task<IReadOnlyList<ReleaseNote>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReleaseNote>>(Notes.OrderByDescending(note => note.UpdatedAtUtc).ToArray());

        public Task<IReadOnlyList<ReleaseNote>> ListPublishedAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReleaseNote>>(Published().ToArray());

        public Task<ReleaseNote> AddAsync(NewReleaseNote note, CancellationToken cancellationToken)
        {
            if (_creations.TryGetValue(note.OperationKey, out var existing))
            {
                if (existing.Hash != note.RequestHash)
                    throw new ReleaseNoteConflictException(existing.Id);
                return Task.FromResult(Notes.Single(item => item.Id == existing.Id));
            }
            var added = new ReleaseNote(Guid.NewGuid(), note.Title, note.Body, ReleaseNoteStatus.Draft, null, null,
                note.CreatedByStaffId, note.AtUtc, note.AtUtc, null, null, 1);
            Notes.Add(added);
            _creations.Add(note.OperationKey, (note.RequestHash, added.Id));
            return Task.FromResult(added);
        }

        public Task<ReleaseNote> UpdateDraftAsync(Guid id, long expectedRowVersion, string title, string body, DateTimeOffset atUtc, CancellationToken cancellationToken)
        {
            var current = Draft(id, expectedRowVersion);
            var updated = current with { Title = title, Body = body, UpdatedAtUtc = atUtc, RowVersion = expectedRowVersion + 1 };
            Notes[Notes.IndexOf(current)] = updated;
            return Task.FromResult(updated);
        }

        public Task<ReleaseNote> PublishAsync(Guid id, long expectedRowVersion, string title, string body, ApplicationBuild build, Guid publishedByStaffId, DateTimeOffset atUtc, CancellationToken cancellationToken)
        {
            var previous = Notes.SingleOrDefault(note => note.Id == id);
            if (previous is { Status: ReleaseNoteStatus.Published }
                && previous.RowVersion == expectedRowVersion + 1
                && previous.PublishedByStaffId == publishedByStaffId
                && previous.Title == title && previous.Body == body
                && previous.Version == build.Version && previous.SourceSha == build.SourceSha)
                return Task.FromResult(previous);
            var current = Draft(id, expectedRowVersion);
            var published = current with
            {
                Title = title,
                Body = body,
                Status = ReleaseNoteStatus.Published,
                Version = build.Version,
                SourceSha = build.SourceSha,
                PublishedByStaffId = publishedByStaffId,
                PublishedAtUtc = atUtc,
                UpdatedAtUtc = atUtc,
                RowVersion = expectedRowVersion + 1
            };
            Notes[Notes.IndexOf(current)] = published;
            return Task.FromResult(published);
        }

        public Task<ReleaseNote?> GetUnacknowledgedAsync(Guid staffId, CancellationToken cancellationToken)
        {
            var newest = Published().FirstOrDefault();
            return Task.FromResult(newest is not null && !_acknowledged.Contains((staffId, newest.Id)) ? newest : null);
        }

        public Task AcknowledgeAsync(Guid staffId, Guid noteId, DateTimeOffset atUtc, CancellationToken cancellationToken)
        {
            if (Published().Any(note => note.Id == noteId))
            {
                _acknowledged.Add((staffId, noteId));
            }

            return Task.CompletedTask;
        }

        private IEnumerable<ReleaseNote> Published() =>
            Notes.Where(note => note.IsPublished).OrderByDescending(note => note.PublishedAtUtc);

        private ReleaseNote Draft(Guid id, long expectedRowVersion)
        {
            var current = Notes.SingleOrDefault(note => note.Id == id) ?? throw new ReleaseNoteConflictException(id);
            return current.Status == ReleaseNoteStatus.Draft && current.RowVersion == expectedRowVersion
                ? current
                : throw new ReleaseNoteConflictException(id);
        }
    }
}
