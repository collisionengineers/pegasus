using System.Globalization;
using System.Net;
using System.Text;
using Azure.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Pegasus.Infrastructure.Brego;
using Pegasus.Infrastructure.Email;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Brego's sign-in mail read from the Brego account's own mailbox over a
/// stand-in Graph (R1294), with synthetic messages only: the link from the
/// HTML-encoded body, never the click-tracking link; the one six-digit code
/// from the visible text; only Brego's authenticated sender, the exact
/// subject and mail received since the sign-in was asked for; and a mailbox
/// the host may not read answered as "cannot read", with nothing changed.
/// </summary>
public sealed class GraphBregoSignInMailReaderTests
{
    private const string Account = "valuations@example.test";
    private const string Token = "synthetic-magic-token-5170";
    private const string Code = "482913";
    private const string Passing =
        "spf=pass (sender IP is 192.0.2.10) smtp.mailfrom=mail.brego.io; dkim=pass (signature was verified) header.d=brego.io;dmarc=pass action=none header.from=brego.io;compauth=pass reason=100";

    private static readonly Uri BaseUri = new("https://graph.microsoft.com/v1.0/");
    private static readonly DateTimeOffset Asked = new(2031, 4, 15, 10, 0, 0, TimeSpan.Zero);

    private static readonly string LinkHtml =
        "<html><head><style>.button{color:#123456}</style></head><body>"
        + "<a href=\"https://abc.r.eu-west-1.awstrack.me/L0/https:%2F%2Fwww.brego.ai%2Flogin%3Fclient=collision-engineers%23magic-link=tracked-token/1/0102/xyz=\">Sign in</a>"
        + "<p>Or copy this link: https://www.brego.ai/login?application=platform&amp;client=collision-engineers&amp;path=%2F#magic-link="
        + Token + "&amp;email=valuations%40example.test</p>"
        + "<a href=\"https://www.brego.ai/login?application=platform&amp;client=collision-engineers&amp;path=%2F#magic-link="
        + Token + "&amp;email=valuations%40example.test\">Open</a>"
        + "</body></html>";

    private static readonly string CodeHtml =
        "<html><head><style>.code{color:#654321} @media (max-width:600px){.x{width:100px}}</style></head><body>"
        + "<img src=\"https://assets.brego.io/mail/logo-998877.png\" alt=\"Brego\"/>"
        + "<p>Use this code to verify this browser:</p><p style=\"font-size:24px\">" + Code + "</p>"
        + "<p>The code expires in 15 minutes. Sent 15/04/2031.</p>"
        + "<p>Help: https://www.brego.ai/help?ticket=777777</p>"
        + "</body></html>";

    [Fact]
    public async Task TheLinkIsReadFromTheEncodedBodyWithItsExtraParametersAndNeverFromTheTracker()
    {
        var graph = new StubGraph();
        graph.Add("m-1", Asked.AddSeconds(3), Mime(BregoSignInMail.LinkSubject, LinkHtml));

        var link = await Reader(graph).ReadLinkAsync(Account, Asked, default);

        Assert.Equal(
            "https://www.brego.ai/login?application=platform&client=collision-engineers&path=%2F#magic-link="
            + Token + "&email=valuations%40example.test",
            link);
        Assert.All(graph.Requests, request => Assert.StartsWith("GET ", request, StringComparison.Ordinal));
        Assert.DoesNotContain(graph.Requests, request => request.Contains("awstrack", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("before since")]
    [InlineData("wrong sender")]
    [InlineData("wrong subject")]
    [InlineData("dmarc fail")]
    [InlineData("other account")]
    [InlineData("tracker only")]
    public async Task AMessageThatIsNotTheAccountsSignInMailIsIgnored(string row)
    {
        var graph = new StubGraph();
        var received = row == "before since" ? Asked.AddSeconds(-30) : Asked.AddSeconds(3);
        var html = row switch
        {
            "other account" => LinkHtml.Replace("valuations%40example.test", "someone%40example.test", StringComparison.Ordinal),
            "tracker only" => "<html><body><a href=\"https://abc.r.eu-west-1.awstrack.me/L0/https:%2F%2Fwww.brego.ai%2Flogin%23magic-link="
                + Token + "%26email=valuations%2540example.test/1/0102/xyz=\">Sign in</a></body></html>",
            _ => LinkHtml,
        };
        graph.Add(
            "m-1",
            received,
            Mime(
                row == "wrong subject" ? "Brego - Your sign-in link (2)" : BregoSignInMail.LinkSubject,
                html,
                from: row == "wrong sender" ? "noreply@brego.example" : BregoSignInMail.Sender,
                authenticationResults: row == "dmarc fail" ? Passing.Replace("dmarc=pass", "dmarc=fail", StringComparison.Ordinal) : Passing));

        Assert.Null(await Reader(graph).ReadLinkAsync(Account, Asked, default));
    }

    [Fact]
    public async Task TheNewestSignInMailWins()
    {
        var graph = new StubGraph();
        graph.Add("m-2", Asked.AddSeconds(9), Mime(BregoSignInMail.LinkSubject, LinkHtml.Replace(Token, "newer-token", StringComparison.Ordinal)));
        graph.Add("m-1", Asked.AddSeconds(3), Mime(BregoSignInMail.LinkSubject, LinkHtml));

        var link = await Reader(graph).ReadLinkAsync(Account, Asked, default);

        Assert.Contains("#magic-link=newer-token&", link, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheCodeIsTheOneSixDigitNumberInTheVisibleText()
    {
        var graph = new StubGraph();
        graph.Add("m-1", Asked.AddSeconds(4), Mime(BregoSignInMail.CodeSubject, CodeHtml));

        Assert.Equal(Code, await Reader(graph).ReadCodeAsync(Account, Asked, default));
    }

    [Fact]
    public async Task TwoDifferentSixDigitNumbersInTheVisibleTextAreNoCode()
    {
        var graph = new StubGraph();
        graph.Add(
            "m-1",
            Asked.AddSeconds(4),
            Mime(BregoSignInMail.CodeSubject, CodeHtml.Replace("Sent 15/04/2031.", "Reference 135790.", StringComparison.Ordinal)));

        Assert.Null(await Reader(graph).ReadCodeAsync(Account, Asked, default));
    }

    [Fact]
    public async Task AMailboxTheHostMayNotReadCannotBeReadAndIsNotAnError()
    {
        var graph = new StubGraph { MessagesStatus = HttpStatusCode.Forbidden };
        var reader = Reader(graph);

        Assert.False(await reader.CanReadAsync(Account, default));
        await Assert.ThrowsAsync<BregoSignInMailUnreadableException>(() => reader.ReadLinkAsync(Account, Asked, default));
        Assert.All(graph.Requests, request => Assert.StartsWith("GET ", request, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AReadableMailboxCanBeRead()
    {
        Assert.True(await Reader(new StubGraph()).CanReadAsync(Account, default));
    }

    private static GraphBregoSignInMailReader Reader(StubGraph graph)
    {
        var http = new HttpClient(graph);
        var credential = new FixedCredential();
        return new GraphBregoSignInMailReader(
            new GraphApprovedMailboxResolver(credential, BaseUri, http, NullLogger<GraphApprovedMailboxResolver>.Instance),
            new GraphMailClient(credential, BaseUri, http));
    }

    private static string Mime(
        string subject,
        string html,
        string from = BregoSignInMail.Sender,
        string authenticationResults = Passing) =>
        "Authentication-Results: " + authenticationResults + "\r\n"
        + "From: Brego <" + from + ">\r\n"
        + "To: " + Account + "\r\n"
        + "Subject: " + subject + "\r\n"
        + "Date: Tue, 15 Apr 2031 10:00:03 +0000\r\n"
        + "Message-ID: <synthetic-" + Guid.NewGuid().ToString("N") + "@brego.io>\r\n"
        + "MIME-Version: 1.0\r\n"
        + "Content-Type: text/html; charset=utf-8\r\n"
        + "Content-Transfer-Encoding: 8bit\r\n"
        + "\r\n"
        + html + "\r\n";

    /// <summary>The Graph endpoints the reader uses, over one mailbox whose Inbox holds the added messages.</summary>
    private sealed class StubGraph : HttpMessageHandler
    {
        private readonly List<(string Id, DateTimeOffset Received, string Mime)> messages = [];

        public HttpStatusCode MessagesStatus { get; init; } = HttpStatusCode.OK;

        public List<string> Requests { get; } = [];

        public void Add(string id, DateTimeOffset received, string mime) => messages.Add((id, received, mime));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath);
            Requests.Add(request.Method + " " + request.RequestUri.AbsoluteUri);
            const string inbox = "/v1.0/users/mailbox-1/mailFolders/inbox-1/messages";
            HttpResponseMessage response;
            if (path == "/v1.0/users/" + Account)
            {
                response = Json(HttpStatusCode.OK, """{"id":"mailbox-1"}""");
            }
            else if (path == "/v1.0/users/mailbox-1/mailFolders/inbox")
            {
                response = Json(HttpStatusCode.OK, """{"id":"inbox-1"}""");
            }
            else if (path == "/v1.0/users/mailbox-1/mailFolders/sentitems")
            {
                response = Json(HttpStatusCode.OK, """{"id":"sent-1"}""");
            }
            else if (path == inbox)
            {
                response = MessagesStatus == HttpStatusCode.OK
                    ? Json(HttpStatusCode.OK, Listing())
                    : Json(MessagesStatus, """{"error":{"code":"ErrorAccessDenied"}}""");
            }
            else if (path.StartsWith(inbox + "/", StringComparison.Ordinal) && path.EndsWith("/$value", StringComparison.Ordinal))
            {
                var id = path[(inbox.Length + 1)..^"/$value".Length];
                var found = messages.Single(message => message.Id == id);
                response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(found.Mime, Encoding.UTF8, "message/rfc822")
                };
            }
            else
            {
                response = Json(HttpStatusCode.NotFound, "{}");
            }

            return Task.FromResult(response);
        }

        private string Listing() =>
            "{\"value\":["
            + string.Join(
                ",",
                messages
                    .OrderByDescending(message => message.Received)
                    .Select(message =>
                        "{\"id\":\"" + message.Id + "\",\"parentFolderId\":\"inbox-1\",\"receivedDateTime\":\""
                        + message.Received.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
                        + "\",\"isRead\":false}"))
            + "]}";

        private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private sealed class FixedCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }
}
