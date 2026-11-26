using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// One kept e-mail as it was sent (R1277, R1279, R1280): the original is read
/// from the receipt's source or, for a Sent item, from the kept original by
/// its hash, and rendered; a message known to have no HTML part reads nothing.
/// </summary>
public sealed class GetMailAsSentTests
{
    private const string ReceiptHash = "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789";
    private const string SentHash = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
    private static readonly byte[] Mime = "Message-Id: <m@example.test>\r\n\r\nBody"u8.ToArray();

    [Fact]
    public async Task AMessageThatIsNotKeptIsNull()
    {
        var fixture = new Fixture(original: null);

        Assert.Null(await fixture.Use.ExecuteAsync(Caseworker(), Guid.NewGuid()));
        Assert.Null(await fixture.Use.ExecuteAsync(Caseworker(), Guid.Empty));
        Assert.Equal(0, fixture.Sources.Reads);
    }

    [Fact]
    public async Task AMessageKnownToHaveNoHtmlHasNoFormattingAndNothingIsRead()
    {
        var fixture = new Fixture(new(Guid.NewGuid(), ReceiptHash, MailFolderScope.Inbox, false, HasHtmlBody: false));

        var result = await fixture.Use.ExecuteAsync(Caseworker(), Guid.NewGuid());

        Assert.Equal(new MailAsSent(MailAsSentStatus.NoFormatting, null, null), result);
        Assert.Equal(0, fixture.Sources.Reads);
        Assert.Equal(0, fixture.Artifacts.Reads);
        Assert.Empty(fixture.Renderer.Calls);
    }

    [Fact]
    public async Task AReceivedMessageRendersItsReceiptsSourceAddressedByItsHash()
    {
        var receiptId = Guid.NewGuid();
        var fixture = new Fixture(new(receiptId, ReceiptHash, MailFolderScope.Inbox, true, HasHtmlBody: null));
        fixture.Sources.Download = new IntakeSourceDownload(Mime, "message.eml", "message/rfc822", Mime.Length, ReceiptHash);

        var result = await fixture.Use.ExecuteAsync(Caseworker(), Guid.NewGuid());

        Assert.Equal(
            new MailAsSent(MailAsSentStatus.Available, "<p>as sent</p>", "abcdef0123456789-r1"),
            result);
        Assert.Equal(receiptId, Assert.Single(fixture.Sources.ReceiptIds));
        var call = Assert.Single(fixture.Renderer.Calls);
        Assert.Equal(Mime, call.Original);
        Assert.True(call.IsStaffForward);
        Assert.Equal(0, fixture.Artifacts.Reads);
    }

    [Fact]
    public async Task AReceiptWhoseCustodyIsNotConfirmedIsUnavailable()
    {
        var fixture = new Fixture(new(Guid.NewGuid(), ReceiptHash, MailFolderScope.Inbox, false, HasHtmlBody: true));
        fixture.Sources.Failure = new IntakeCustodyUnavailableException("The source is not yet in custody.");

        var result = await fixture.Use.ExecuteAsync(Caseworker(), Guid.NewGuid());

        Assert.Equal(MailAsSentStatus.Unavailable, result!.Status);
        Assert.Null(result.Html);
        Assert.Empty(fixture.Renderer.Calls);
    }

    [Fact]
    public async Task AReceiptWhoseSourceFailsItsIntegrityCheckIsUnavailable()
    {
        var fixture = new Fixture(new(Guid.NewGuid(), ReceiptHash, MailFolderScope.Inbox, false, HasHtmlBody: true));
        fixture.Sources.Failure = new IntakeArtifactIntegrityException();

        var result = await fixture.Use.ExecuteAsync(Caseworker(), Guid.NewGuid());

        Assert.Equal(MailAsSentStatus.Unavailable, result!.Status);
    }

    [Fact]
    public async Task AnUnknownReceiptIsUnavailable()
    {
        var fixture = new Fixture(new(Guid.NewGuid(), ReceiptHash, MailFolderScope.Inbox, false, HasHtmlBody: true));

        var result = await fixture.Use.ExecuteAsync(Caseworker(), Guid.NewGuid());

        Assert.Equal(MailAsSentStatus.Unavailable, result!.Status);
        Assert.Empty(fixture.Renderer.Calls);
    }

    [Fact]
    public async Task ASentMessageRendersItsKeptOriginal()
    {
        var fixture = new Fixture(new(null, SentHash, MailFolderScope.Sent, false, HasHtmlBody: true));
        fixture.Artifacts.Kept[SentHash] = Mime;

        var result = await fixture.Use.ExecuteAsync(Caseworker(), Guid.NewGuid());

        Assert.Equal(
            new MailAsSent(MailAsSentStatus.Available, "<p>as sent</p>", "0123456789abcdef-r1"),
            result);
        Assert.Equal(Mime, Assert.Single(fixture.Renderer.Calls).Original);
        Assert.Equal(0, fixture.Sources.Reads);
    }

    [Fact]
    public async Task ASentMessageWhoseOriginalWasNotKeptIsUnavailable()
    {
        var fixture = new Fixture(new(null, SentHash, MailFolderScope.Sent, false, HasHtmlBody: true));

        var result = await fixture.Use.ExecuteAsync(Caseworker(), Guid.NewGuid());

        Assert.Equal(MailAsSentStatus.Unavailable, result!.Status);
        Assert.Equal(1, fixture.Artifacts.Reads);
        Assert.Empty(fixture.Renderer.Calls);
    }

    [Fact]
    public async Task AnOriginalTheRendererFindsNoHtmlInHasNoFormatting()
    {
        var fixture = new Fixture(new(null, SentHash, MailFolderScope.Sent, false, HasHtmlBody: true));
        fixture.Artifacts.Kept[SentHash] = Mime;
        fixture.Renderer.Rendering = null;

        var result = await fixture.Use.ExecuteAsync(Caseworker(), Guid.NewGuid());

        Assert.Equal(new MailAsSent(MailAsSentStatus.NoFormatting, null, null), result);
    }

    [Fact]
    public async Task AnActorWithoutCaseworkIsRefusedBeforeAnythingIsRead()
    {
        var fixture = new Fixture(new(Guid.NewGuid(), ReceiptHash, MailFolderScope.Inbox, false, HasHtmlBody: true));

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            fixture.Use.ExecuteAsync(ActionActor.Principal(Guid.NewGuid()), Guid.NewGuid()));

        Assert.Equal(0, fixture.Queries.Reads);
        Assert.Equal(0, fixture.Sources.Reads);
    }

    [Fact]
    public void TheVersionIsTheHashsOpeningInLowerCaseAndTheRenderersVersion() =>
        Assert.Equal("abcdef0123456789-2", GetMailAsSent.VersionOf(ReceiptHash, "2"));

    private static ActionActor Caseworker() =>
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    private sealed class Fixture
    {
        public Fixture(RetainedMailOriginal? original)
        {
            Queries = new OriginalQueries(original);
            Use = new GetMailAsSent(Queries, Sources, Artifacts, Renderer);
        }

        public OriginalQueries Queries { get; }

        public Downloads Sources { get; } = new();

        public KeptOriginals Artifacts { get; } = new();

        public RecordingRenderer Renderer { get; } = new();

        public GetMailAsSent Use { get; }
    }

    private sealed class OriginalQueries(RetainedMailOriginal? original) : IRetainedMailQueries
    {
        public int Reads { get; private set; }

        public Task<RetainedMailOriginal?> GetOriginalAsync(Guid id, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(original);
        }

        public Task<RetainedMailPage> ListAsync(MailWorkspaceScope scope, int page, int pageSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RetainedMailCursorPage> ListByCursorAsync(
            MailWorkspaceScope scope, DateTimeOffset? beforeReceivedAtUtc, Guid? beforeId, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<int>> CountManyAsync(IReadOnlyList<MailWorkspaceScope> scopes, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RetainedMailDetail?> GetAsync(
            Guid id, CancellationToken cancellationToken, string? searchTerm = null, bool withConversation = false) =>
            throw new NotSupportedException();

        public Task<RetainedMailPreview?> GetPreviewAsync(Guid id, RetainedMailSummary? summary, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RetainedMailDetail?> GetByOriginReceiptAsync(Guid originReceiptId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Guid?> FindIdByExternalReceiptTokenAsync(string externalReceiptToken, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RetainedMailMailbox>> ListMailboxesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<string>> ListPrincipalsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<MailPollHealth>> ListPollHealthAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RetainedMailAttachmentTextRead?> ReadAttachmentTextAsync(
            Guid id, int attachmentOrdinal, int maximumCharacters, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> CountAwaitingAutomationDecisionAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class Downloads : IDownloadIntakeSource
    {
        public IntakeSourceDownload? Download { get; set; }

        public Exception? Failure { get; set; }

        public List<Guid> ReceiptIds { get; } = [];

        public int Reads => ReceiptIds.Count;

        public Task<IntakeSourceDownload?> ExecuteAsync(
            DownloadIntakeSourceQuery query,
            CancellationToken cancellationToken = default)
        {
            ReceiptIds.Add(query.ReceiptId);
            return Failure is null
                ? Task.FromResult(Download)
                : Task.FromException<IntakeSourceDownload?>(Failure);
        }

        public Task<IntakeSourceDownload> ExecuteAsync(
            AuthorizedIntakeSource authorized,
            ActionActor actor,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class KeptOriginals : IIntakeArtifactStore
    {
        public Dictionary<string, byte[]> Kept { get; } = new(StringComparer.Ordinal);

        public int Reads { get; private set; }

        public Task<ReadOnlyMemory<byte>?> ReadByHashAsync(string contentHash, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(Kept.TryGetValue(contentHash, out var content) ? (ReadOnlyMemory<byte>?)content : null);
        }

        public Task<string> StoreAsync(string contentHash, ReadOnlyMemory<byte> content, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StagedArtifactInventoryItem> StageAsync(
            Guid stagedReceiptId, string contentHash, Stream content, long contentLength,
            DateTimeOffset firstSeenAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ReadOnlyMemory<byte>?> ReadAsync(string storageKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingRenderer : IRenderMailAsSent
    {
        public MailAsSentRendering? Rendering { get; set; } = new("<p>as sent</p>");

        public List<(byte[] Original, bool IsStaffForward)> Calls { get; } = [];

        public string Version => "r1";

        public MailAsSentRendering? Render(ReadOnlyMemory<byte> original, bool isStaffForward)
        {
            Calls.Add((original.ToArray(), isStaffForward));
            return Rendering;
        }
    }
}
