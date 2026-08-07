using System.Net;
using System.Text;
using Azure.Core;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Email;

namespace Pegasus.IntegrationTests;

public sealed class ProductionGraphSourceTests
{
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
}
