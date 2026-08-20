using System.Net;
using System.Text;
using Azure.Core;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Email;
using Pegasus.Infrastructure.Intake;

namespace Pegasus.IntegrationTests;

public sealed class ProductionGraphSourceTests
{
    [Fact]
    public async Task DeletedSearchReadsOnlyTheResolvedFolderAndPassesMimeThroughTheCanonicalReader()
    {
        var requests = new List<(HttpMethod Method, string Path, string? Prefer)>();
        var handler = new DelegateHandler(request =>
        {
            requests.Add((
                request.Method,
                request.RequestUri!.AbsolutePath,
                request.Headers.TryGetValues("Prefer", out var values) ? values.Single() : null));
            if (request.RequestUri.AbsolutePath.EndsWith("/mailFolders/deleteditems", StringComparison.Ordinal))
            {
                return Response(HttpStatusCode.OK, """{"id":"deleted-folder"}""");
            }
            if (request.RequestUri.AbsolutePath.EndsWith("/$value", StringComparison.Ordinal))
            {
                return Response(
                    HttpStatusCode.OK,
                    "From: sender@example.test\r\nSubject: Deleted instruction\r\n"
                    + "MIME-Version: 1.0\r\nContent-Type: multipart/mixed; boundary=part\r\n\r\n"
                    + "--part\r\nContent-Type: text/plain\r\n\r\nThe searchable needle is here.\r\n"
                    + "--part\r\nContent-Type: application/octet-stream; name=source.bin\r\n"
                    + "Content-Disposition: attachment; filename=source.bin\r\n"
                    + "Content-Transfer-Encoding: base64\r\n\r\nAQID\r\n--part--\r\n",
                    "message/rfc822");
            }
            return Response(
                HttpStatusCode.OK,
                """{"value":[{"id":"deleted-1","parentFolderId":"deleted-folder","receivedDateTime":"2026-07-31T10:00:00Z","isRead":false}]}""");
        });
        var options = Options();
        var source = new GraphDeletedMailSearchSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)),
            new MailboxEstate([new(options.MailboxId, options.MailboxAddress, options.InboxFolderId)]),
            new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System));

        var result = await source.SearchAsync(
            options.MailboxId,
            "needle",
            100,
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("Deleted instruction", item.Subject);
        Assert.Equal("sender@example.test", item.SenderAddress);
        Assert.Equal("The searchable needle is here.", item.BodyPlainText);
        Assert.Contains(item.Matches, match => match.Kind == MailSearchMatchKind.MessageBody);
        Assert.False(Assert.Single(item.Attachments).IsSearchable);
        Assert.All(requests, request => Assert.Equal(HttpMethod.Get, request.Method));
        Assert.All(
            requests.Where(request => !request.Path.EndsWith("/mailFolders/deleteditems", StringComparison.Ordinal)),
            request => Assert.Equal("IdType=\"ImmutableId\"", request.Prefer));
        Assert.Contains(requests, request => request.Path.EndsWith("/mailFolders/deleteditems", StringComparison.Ordinal));
        Assert.Contains(requests, request => request.Path.Contains("/mailFolders/deleted-folder/messages", StringComparison.Ordinal));
        Assert.Contains(requests, request => request.Path.EndsWith(
            "/mailFolders/deleted-folder/messages/deleted-1/$value",
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeletedSearchBecomesUnavailableWhenTheMessageMovesBeforeItsFolderScopedMimeRead()
    {
        var handler = new DelegateHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/mailFolders/deleteditems", StringComparison.Ordinal))
            {
                return Response(HttpStatusCode.OK, """{"id":"deleted-folder"}""");
            }
            if (request.RequestUri.AbsolutePath.EndsWith("/$value", StringComparison.Ordinal))
            {
                return Response(HttpStatusCode.NotFound, "{}");
            }
            return Response(
                HttpStatusCode.OK,
                """{"value":[{"id":"moved-1","parentFolderId":"deleted-folder","receivedDateTime":"2026-07-31T10:00:00Z","isRead":false}]}""");
        });
        var options = Options();
        var source = new GraphDeletedMailSearchSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)),
            new MailboxEstate([new(options.MailboxId, options.MailboxAddress, options.InboxFolderId)]),
            new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System));

        var result = await source.SearchAsync(null, "needle", 100, CancellationToken.None);

        Assert.Equal(DeletedMailSearchState.Unavailable, result.State);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task DeletedSearchDoesNotCallGraphForAMailboxOutsideTheApprovedEstate()
    {
        var calls = 0;
        var handler = new DelegateHandler(_ =>
        {
            calls++;
            return Response(HttpStatusCode.OK, "{}");
        });
        var options = Options();
        var source = new GraphDeletedMailSearchSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)),
            new MailboxEstate([new(options.MailboxId, options.MailboxAddress, options.InboxFolderId)]),
            new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System));

        var result = await source.SearchAsync("not-approved", "needle", 100, CancellationToken.None);

        Assert.Equal(DeletedMailSearchState.Unavailable, result.State);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task DeletedSearchChoosesTheNewestBoundedMessagesAcrossApprovedMailboxes()
    {
        var mimePaths = new List<string>();
        var handler = new DelegateHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/mailFolders/deleteditems", StringComparison.Ordinal))
            {
                return Response(HttpStatusCode.OK, path.Contains("mailbox-two", StringComparison.Ordinal)
                    ? """{"id":"deleted-two"}"""
                    : """{"id":"deleted-one"}""");
            }
            if (path.EndsWith("/$value", StringComparison.Ordinal))
            {
                mimePaths.Add(path);
                return Response(HttpStatusCode.OK, "Subject: Match\r\n\r\nneedle", "message/rfc822");
            }
            var second = path.Contains("mailbox-two", StringComparison.Ordinal);
            return Response(HttpStatusCode.OK, second
                ? """{"value":[{"id":"newer","parentFolderId":"deleted-two","receivedDateTime":"2026-08-20T11:00:00Z","isRead":false}]}"""
                : """{"value":[{"id":"older","parentFolderId":"deleted-one","receivedDateTime":"2026-08-20T10:00:00Z","isRead":false}]}""");
        });
        var options = Options();
        var source = new GraphDeletedMailSearchSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)),
            new MailboxEstate([
                new("mailbox-id", "one@example.test", "inbox-one"),
                new("mailbox-two", "two@example.test", "inbox-two")]),
            new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System));

        var result = await source.SearchAsync(null, "needle", 1, CancellationToken.None);

        Assert.Equal("newer", Assert.Single(result.Items).ImmutableMessageId);
        Assert.Single(mimePaths);
        Assert.Contains(
            "mailbox-two/mailFolders/deleted-two/messages/newer",
            mimePaths[0],
            StringComparison.Ordinal);
        Assert.True(result.IsTruncated);
    }

    [Fact]
    public async Task DeletedSearchListsApprovedMailboxesWithoutRetainedRows()
    {
        var options = Options();
        var source = new GraphDeletedMailSearchSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(new DelegateHandler(_ => Response(HttpStatusCode.OK, "{}")))),
            new MailboxEstate([new("mailbox-zero", "zero@example.test", "inbox-zero")]),
            new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System));

        var mailbox = Assert.Single(await source.ListMailboxesAsync(CancellationToken.None));

        Assert.Equal("mailbox-zero", mailbox.MailboxId);
        Assert.True(mailbox.IsPolled);
    }

    [Fact]
    public async Task DeletedSearchTurnsHttpTimeoutIntoUnavailable()
    {
        var options = Options();
        var source = new GraphDeletedMailSearchSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(
                new DelegateHandler(_ => throw new TaskCanceledException("timeout")))),
            new MailboxEstate([new(options.MailboxId, options.MailboxAddress, options.InboxFolderId)]),
            new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System));

        var result = await source.SearchAsync(null, "needle", 100, CancellationToken.None);

        Assert.Equal(DeletedMailSearchState.Unavailable, result.State);
    }

    [Fact]
    public async Task DeletedSearchDoesNotTurnCallerCancellationIntoUnavailable()
    {
        var options = Options();
        var source = new GraphDeletedMailSearchSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(
                new DelegateHandler(_ => throw new TaskCanceledException("cancelled")))),
            new MailboxEstate([new(options.MailboxId, options.MailboxAddress, options.InboxFolderId)]),
            new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            source.SearchAsync(null, "needle", 100, cancellation.Token));
    }

    [Fact]
    public async Task InboxUsesImmutableIdsAndContinuesWithDeltaCursorWithoutMutation()
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Prefer)>();
        var handler = new DelegateHandler(request =>
        {
            requests.Add((request.Method, request.RequestUri!.AbsoluteUri,
                request.Headers.TryGetValues("Prefer", out var values) ? values.Single() : null));
            return request.RequestUri.AbsolutePath.EndsWith("/$value", StringComparison.Ordinal)
                ? Response(HttpStatusCode.OK, "From: sender@example.test\r\nMessage-Id: <one@example.test>\r\n\r\nBody", "message/rfc822")
                : Response(HttpStatusCode.OK,
                    """{"value":[{"id":"immutable-1","parentFolderId":"inbox-folder","receivedDateTime":"2026-07-31T10:00:00Z"}],"@odata.deltaLink":"https://graph.microsoft.com/v1.0/users/mailbox-id/mailFolders/inbox-folder/messages/delta?$deltatoken=final"}""");
        });
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        var page = await source.ReadAsync(
            new(options.MailboxId, options.MailboxAddress, options.InboxFolderId, null, "lease"),
            10,
            CancellationToken.None);

        var message = Assert.Single(page.Messages);
        Assert.Equal("immutable-1", message.ImmutableMessageId);
        Assert.Equal(page.NextCursor, message.NextCursor);
        Assert.All(requests, request => Assert.Equal(HttpMethod.Get, request.Method));
        Assert.All(requests, request => Assert.Equal("IdType=\"ImmutableId\"", request.Prefer));
    }

    [Fact]
    public async Task InboxAcceptsTheGraphCanonicalODataCursorForTheExactFolder()
    {
        var handler = new DelegateHandler(_ => Response(
            HttpStatusCode.OK,
            """{"value":[],"@odata.deltaLink":"https://graph.microsoft.com/v1.0/users/mailbox-id/mailFolders('inbox-folder==')/messages/delta?$deltatoken=final"}"""));
        var options = GraphApprovedMailboxOptions.Create(
            "https://graph.microsoft.com/v1.0/",
            "mailbox-id",
            "instructions@collisionengineers.co.uk",
            "inbox-folder==",
            "sent-folder==");
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        var page = await source.ReadAsync(
            new(options.MailboxId, options.MailboxAddress, options.InboxFolderId, null, "lease"),
            10,
            CancellationToken.None);

        Assert.Empty(page.Messages);
        var cursor = GraphCursor.Parse(page.NextCursor, new Uri("https://example.test"));
        Assert.Equal(
            "v1.0/users/mailbox-id/mailFolders('inbox-folder==')/messages/delta",
            cursor.PageUri.GetComponents(UriComponents.Path, UriFormat.Unescaped));
    }

    /// <summary>
    /// The lease's own folder is what bounds the read. A persisted cursor pointing at a
    /// different folder of the same mailbox is refused before Graph is called, so the
    /// estate cannot be widened by a stale cursor.
    /// </summary>
    [Fact]
    public async Task InboxRejectsACursorForAnotherFolderOfTheSameMailboxBeforeCallingGraph()
    {
        var calls = 0;
        var handler = new DelegateHandler(_ => { calls++; return Response(HttpStatusCode.OK, "{}"); });
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));
        var otherFolderCursor = GraphCursor.Serialize(
            new Uri("https://graph.microsoft.com/v1.0/users/mailbox-id/mailFolders/other-folder/messages/delta?$deltatoken=x"),
            0);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => source.ReadAsync(
            new(options.MailboxId, options.MailboxAddress, options.InboxFolderId, otherFolderCursor, "lease"),
            10,
            CancellationToken.None));

        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("has space", "inbox-folder")]
    [InlineData("mailbox-id", "has space")]
    [InlineData("mailbox-id", " ")]
    public async Task InboxRefusesALeaseWithoutAnExactMailboxAndFolderIdentity(
        string mailboxId,
        string inboxFolderIdentity)
    {
        var calls = 0;
        var handler = new DelegateHandler(_ => { calls++; return Response(HttpStatusCode.OK, "{}"); });
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => source.ReadAsync(
            new(mailboxId, options.MailboxAddress, inboxFolderIdentity, null, "lease"),
            10,
            CancellationToken.None));

        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task InboxReportsATenantThatHasNotAdmittedTheApplicationToThisMailbox(
        HttpStatusCode status)
    {
        var handler = new DelegateHandler(_ => Response(status, "{}"));
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        await Assert.ThrowsAsync<ApprovedMailboxAccessDeniedException>(() => source.ReadAsync(
            new(options.MailboxId, options.MailboxAddress, options.InboxFolderId, null, "lease"),
            10,
            CancellationToken.None));
    }

    /// <summary>
    /// The same singleton client reads a second mailbox in the same tick without being
    /// rebuilt, because it no longer closes over one mailbox.
    /// </summary>
    [Fact]
    public async Task OneClientReadsTwoMailboxesEachInsideItsOwnFolder()
    {
        var paths = new List<string>();
        var handler = new DelegateHandler(request =>
        {
            paths.Add(request.RequestUri!.AbsolutePath);
            var mailbox = request.RequestUri.AbsolutePath.Contains("mailbox-two", StringComparison.Ordinal)
                ? "mailbox-two"
                : "mailbox-id";
            var folder = string.Equals(mailbox, "mailbox-two", StringComparison.Ordinal)
                ? "inbox-two"
                : "inbox-folder";
            return Response(
                HttpStatusCode.OK,
                $$"""{"value":[],"@odata.deltaLink":"https://graph.microsoft.com/v1.0/users/{{mailbox}}/mailFolders/{{folder}}/messages/delta?$deltatoken=final"}""");
        });
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        await source.ReadAsync(
            new("mailbox-id", "a@collisionengineers.co.uk", "inbox-folder", null, "lease-1"),
            10,
            CancellationToken.None);
        await source.ReadAsync(
            new("mailbox-two", "b@collisionengineers.co.uk", "inbox-two", null, "lease-2"),
            10,
            CancellationToken.None);

        Assert.Contains(
            paths,
            path => path.Contains("mailbox-id/mailFolders/inbox-folder", StringComparison.Ordinal));
        Assert.Contains(
            paths,
            path => path.Contains("mailbox-two/mailFolders/inbox-two", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExpiredDeltaCursorResetsToTheExactApprovedFolder()
    {
        var calls = 0;
        var handler = new DelegateHandler(request =>
        {
            calls++;
            if (calls == 1)
            {
                return Response(HttpStatusCode.Gone, "{}");
            }
            Assert.Contains("mailFolders/inbox-folder/messages/delta", request.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
            return Response(HttpStatusCode.OK,
                """{"value":[],"@odata.deltaLink":"https://graph.microsoft.com/v1.0/users/mailbox-id/mailFolders/inbox-folder/messages/delta?$deltatoken=reset"}""");
        });
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));
        var staleCursor = GraphCursor.Serialize(
            new Uri("https://graph.microsoft.com/v1.0/users/mailbox-id/mailFolders/inbox-folder/messages/delta?$deltatoken=stale"),
            0);

        var page = await source.ReadAsync(
            new(options.MailboxId, options.MailboxAddress, options.InboxFolderId, staleCursor, "lease"),
            10,
            CancellationToken.None);

        Assert.Empty(page.Messages);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task InboxRejectsPersistedCursorForAnotherFolderBeforeCallingGraph()
    {
        var calls = 0;
        var handler = new DelegateHandler(_ => { calls++; return Response(HttpStatusCode.OK, "{}"); });
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));
        var escapedCursor = GraphCursor.Serialize(
            new Uri("https://graph.microsoft.com/v1.0/users/other/mailFolders/other/messages/delta?$deltatoken=x"),
            0);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => source.ReadAsync(
            new(options.MailboxId, options.MailboxAddress, options.InboxFolderId, escapedCursor, "lease"),
            10,
            CancellationToken.None));

        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("other-folder")]
    public async Task InboxRejectsAnItemOutsideTheExactApprovedFolderBeforeReadingMime(string? parentFolderId)
    {
        var mimeCalls = 0;
        var parentProperty = parentFolderId is null
            ? string.Empty
            : $",\"parentFolderId\":\"{parentFolderId}\"";
        var handler = new DelegateHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/$value", StringComparison.Ordinal))
            {
                mimeCalls++;
                return Response(HttpStatusCode.OK, "Body", "message/rfc822");
            }
            return Response(HttpStatusCode.OK,
                $"{{\"value\":[{{\"id\":\"immutable-1\"{parentProperty},\"receivedDateTime\":\"2026-07-31T10:00:00Z\"}}],\"@odata.deltaLink\":\"https://graph.microsoft.com/v1.0/users/mailbox-id/mailFolders/inbox-folder/messages/delta?$deltatoken=final\"}}");
        });
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => source.ReadAsync(
            new(options.MailboxId, options.MailboxAddress, options.InboxFolderId, null, "lease"),
            10,
            CancellationToken.None));

        Assert.Equal(0, mimeCalls);
    }

    [Fact]
    public async Task SentThrottlingRetainsTheProviderRetryDelay()
    {
        var handler = new DelegateHandler(_ =>
        {
            var response = Response(HttpStatusCode.TooManyRequests, "{}");
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(17));
            return response;
        });
        var options = Options();
        var source = new GraphApprovedSentSource(
            options,
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        var error = await Assert.ThrowsAsync<ApprovedSentSourceThrottledException>(() => source.ReadAsync(
            new(options.MailboxId, options.MailboxAddress, options.SentFolderId, null, "lease"),
            10,
            CancellationToken.None));

        Assert.Equal(TimeSpan.FromSeconds(17), error.RetryAfter);
    }

    [Fact]
    public async Task InboxCursorContinuesInsideAnOversizedGraphPageWithoutDroppingMessages()
    {
        var handler = new DelegateHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/$value", StringComparison.Ordinal)
            ? Response(HttpStatusCode.OK, $"Message-Id: <{request.RequestUri.Segments[^2]}@example.test>\r\n\r\nBody", "message/rfc822")
            : Response(HttpStatusCode.OK,
                """{"value":[{"id":"immutable-1","parentFolderId":"inbox-folder","receivedDateTime":"2026-07-31T10:00:00Z"},{"id":"immutable-2","parentFolderId":"inbox-folder","receivedDateTime":"2026-07-31T10:01:00Z"}],"@odata.deltaLink":"https://graph.microsoft.com/v1.0/users/mailbox-id/mailFolders/inbox-folder/messages/delta?$deltatoken=final"}"""));
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        var first = await source.ReadAsync(
            new(options.MailboxId, options.MailboxAddress, options.InboxFolderId, null, "lease-1"),
            1,
            CancellationToken.None);
        var second = await source.ReadAsync(
            new(options.MailboxId, options.MailboxAddress, options.InboxFolderId, first.NextCursor, "lease-2"),
            1,
            CancellationToken.None);

        Assert.Equal("immutable-1", Assert.Single(first.Messages).ImmutableMessageId);
        Assert.Equal("immutable-2", Assert.Single(second.Messages).ImmutableMessageId);
        Assert.NotEqual(first.NextCursor, second.NextCursor);
    }

    private static GraphApprovedMailboxOptions Options() => GraphApprovedMailboxOptions.Create(
        "https://graph.microsoft.com/v1.0/",
        "mailbox-id",
        "instructions@collisionengineers.co.uk",
        "inbox-folder",
        "sent-folder");

    private static HttpResponseMessage Response(HttpStatusCode status, string body, string mediaType = "application/json") =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) };

    private sealed class FixedCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }

    private sealed class MailboxEstate(IReadOnlyList<ApprovedIntakeMailbox> mailboxes)
        : IApprovedIntakeMailboxes
    {
        public Task<IReadOnlyList<ApprovedIntakeMailbox>> ListPollableAsync(
            CancellationToken cancellationToken) => Task.FromResult(mailboxes);
    }
}
