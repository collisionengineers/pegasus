using System.Net;
using System.Text;
using Azure.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Email;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The mailbox-administration "add an address" resolve port (MAIL-002), fake-backed
/// against a stand-in Graph transport — the same pattern as
/// <see cref="ProductionGraphSourceTests"/>, but resolving an arbitrary address rather
/// than reading one fixed, already-approved mailbox's mail.
/// </summary>
public sealed class GraphApprovedMailboxResolverTests
{
    private static readonly Uri BaseUri = new("https://graph.microsoft.com/v1.0/");

    [Fact]
    public async Task ResolvesTheMailboxAndBothWellKnownFolderIdsForAnExistingAddress()
    {
        var requestedUris = new List<string>();
        var handler = new DelegateHandler(request =>
        {
            var path = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath);
            requestedUris.Add(request.RequestUri.AbsoluteUri);
            return path switch
            {
                "/v1.0/users/estate@collisionengineers.co.uk" =>
                    Response(HttpStatusCode.OK, """{"id":"mailbox-1"}"""),
                "/v1.0/users/mailbox-1/mailFolders/inbox" =>
                    Response(HttpStatusCode.OK, """{"id":"inbox-1"}"""),
                "/v1.0/users/mailbox-1/mailFolders/sentitems" =>
                    Response(HttpStatusCode.OK, """{"id":"sent-1"}"""),
                "/v1.0/users/mailbox-1/mailFolders" =>
                    Response(HttpStatusCode.OK, """{"value":[{"id":"instructions-1","displayName":"Instructions","childFolderCount":0},{"id":"billing-1","displayName":"Billing","childFolderCount":0}]}"""),
                _ => Response(HttpStatusCode.NotFound, "{}")
            };
        });
        var resolver = CreateResolver(handler);

        var resolution = await resolver.ResolveAsync(
            "estate@collisionengineers.co.uk",
            CancellationToken.None);

        Assert.NotNull(resolution);
        Assert.Equal("mailbox-1", resolution!.MailboxIdentity);
        Assert.Equal("inbox-1", resolution.InboxFolderIdentity);
        Assert.Equal("sent-1", resolution.SentFolderIdentity);
        Assert.Collection(
            resolution.FolderBindings!,
            item =>
            {
                Assert.Equal(MailLogicalFolderType.Instructions, item.FolderType);
                Assert.Equal("instructions-1", item.FolderIdentity);
            },
            item =>
            {
                Assert.Equal(MailLogicalFolderType.Billing, item.FolderType);
                Assert.Equal("billing-1", item.FolderIdentity);
            });
        Assert.Equal(4, requestedUris.Count);
        Assert.Contains(
            requestedUris,
            uri => Uri.UnescapeDataString(uri).Contains("/users/estate@collisionengineers.co.uk", StringComparison.Ordinal));
        Assert.Contains(requestedUris, uri => uri.Contains("/users/mailbox-1/mailFolders/inbox", StringComparison.Ordinal));
        Assert.Contains(requestedUris, uri => uri.Contains("/users/mailbox-1/mailFolders/sentitems", StringComparison.Ordinal));
        Assert.All(requestedUris, uri => Assert.StartsWith(BaseUri.AbsoluteUri, uri, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReturnsNullWithoutQueryingFoldersWhenTheAddressIsNotInTheTenant()
    {
        var calls = 0;
        var handler = new DelegateHandler(_ =>
        {
            calls++;
            return Response(HttpStatusCode.NotFound, "{}");
        });
        var resolver = CreateResolver(handler);

        var resolution = await resolver.ResolveAsync(
            "unknown@collisionengineers.co.uk",
            CancellationToken.None);

        Assert.Null(resolution);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ReturnsNullWhenAWellKnownFolderCannotBeResolved()
    {
        var handler = new DelegateHandler(request => Uri.UnescapeDataString(request.RequestUri!.AbsolutePath) switch
        {
            "/v1.0/users/estate@collisionengineers.co.uk" =>
                Response(HttpStatusCode.OK, """{"id":"mailbox-1"}"""),
            "/v1.0/users/mailbox-1/mailFolders/inbox" =>
                Response(HttpStatusCode.OK, """{"id":"inbox-1"}"""),
            _ => Response(HttpStatusCode.NotFound, "{}")
        });
        var resolver = CreateResolver(handler);

        var resolution = await resolver.ResolveAsync(
            "estate@collisionengineers.co.uk",
            CancellationToken.None);

        Assert.Null(resolution);
    }

    [Fact]
    public async Task RecursesReadOnlyAndLeavesDuplicateLogicalNamesUnconfigured()
    {
        var methods = new List<HttpMethod>();
        var handler = new DelegateHandler(request =>
        {
            methods.Add(request.Method);
            return Uri.UnescapeDataString(request.RequestUri!.AbsolutePath) switch
            {
                "/v1.0/users/estate@collisionengineers.co.uk" =>
                    Response(HttpStatusCode.OK, """{"id":"mailbox-1"}"""),
                "/v1.0/users/mailbox-1/mailFolders/inbox" =>
                    Response(HttpStatusCode.OK, """{"id":"inbox-1"}"""),
                "/v1.0/users/mailbox-1/mailFolders/sentitems" =>
                    Response(HttpStatusCode.OK, """{"id":"sent-1"}"""),
                "/v1.0/users/mailbox-1/mailFolders" =>
                    Response(HttpStatusCode.OK, """{"value":[{"id":"instructions-1","displayName":"Instructions","childFolderCount":0},{"id":"parent-1","displayName":"Cases","childFolderCount":1}]}"""),
                "/v1.0/users/mailbox-1/mailFolders/parent-1/childFolders" =>
                    Response(HttpStatusCode.OK, """{"value":[{"id":"instructions-2","displayName":"Instructions","childFolderCount":0},{"id":"billing-1","displayName":"Billing","childFolderCount":0}]}"""),
                _ => Response(HttpStatusCode.NotFound, "{}")
            };
        });

        var resolution = await CreateResolver(handler).ResolveAsync(
            "estate@collisionengineers.co.uk",
            CancellationToken.None);

        var binding = Assert.Single(resolution!.FolderBindings!);
        Assert.Equal(MailLogicalFolderType.Billing, binding.FolderType);
        Assert.Equal("billing-1", binding.FolderIdentity);
        Assert.All(methods, method => Assert.Equal(HttpMethod.Get, method));
    }

    [Fact]
    public async Task FailsClosedWhenGraphPagesOutsideTheApprovedMailbox()
    {
        var calls = 0;
        var handler = new DelegateHandler(request =>
        {
            calls++;
            return Uri.UnescapeDataString(request.RequestUri!.AbsolutePath) switch
            {
                "/v1.0/users/estate@collisionengineers.co.uk" =>
                    Response(HttpStatusCode.OK, """{"id":"mailbox-1"}"""),
                "/v1.0/users/mailbox-1/mailFolders/inbox" =>
                    Response(HttpStatusCode.OK, """{"id":"inbox-1"}"""),
                "/v1.0/users/mailbox-1/mailFolders/sentitems" =>
                    Response(HttpStatusCode.OK, """{"id":"sent-1"}"""),
                "/v1.0/users/mailbox-1/mailFolders" => Response(
                    HttpStatusCode.OK,
                    """{"value":[],"@odata.nextLink":"https://graph.microsoft.com/v1.0/users/other-mailbox/mailFolders?$skiptoken=hostile"}"""),
                _ => Response(HttpStatusCode.NotFound, "{}")
            };
        });

        var resolution = await CreateResolver(handler).ResolveAsync(
            "estate@collisionengineers.co.uk",
            CancellationToken.None);

        Assert.Null(resolution);
        Assert.Equal(4, calls);
    }

    [Fact]
    public async Task FailsClosedRatherThanThrowingWhenTheTransportItselfFails()
    {
        var handler = new DelegateHandler(_ => throw new HttpRequestException("simulated transport failure"));
        var resolver = CreateResolver(handler);

        var resolution = await resolver.ResolveAsync(
            "estate@collisionengineers.co.uk",
            CancellationToken.None);

        Assert.Null(resolution);
    }

    private static GraphApprovedMailboxResolver CreateResolver(HttpMessageHandler handler) => new(
        new FixedCredential(),
        BaseUri,
        new HttpClient(handler),
        NullLogger<GraphApprovedMailboxResolver>.Instance);

    private static HttpResponseMessage Response(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

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
