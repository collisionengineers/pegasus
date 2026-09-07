using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Web.Pages.Uploads;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class QdosCustodialWebTests
{
    [Fact]
    public async Task PublicRequestUploadWithNoMatchingTokenReturnsNoRequestOrCaseDisclosure()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var token = RequestUploadToken.Create().Secret.Token;

        using var response = await client.GetAsync($"/Uploads/{Uri.EscapeDataString(token)}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(token, body, StringComparison.Ordinal);
        Assert.DoesNotContain("case", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("request", body, StringComparison.OrdinalIgnoreCase);
    }
    /// <summary>
    /// INTK-051: opening the composition gate made the anonymous upload page
    /// reachable, and with it a bound that did not previously exist.
    ///
    /// <see cref="RequestUploadAttemptLimiter"/> partitions on the token digest
    /// and <see cref="Pegasus.Web.Pages.Uploads.RequestModel.OnPostAsync"/>
    /// answers <c>NotFound</c> for an unknown token before the limiter is
    /// consulted, so a caller holding no token spends nothing there. While the
    /// gate was closed the middleware short-circuited every <c>/Uploads</c>
    /// request to 404 before a body was read; once composed, Razor Pages'
    /// antiforgery filter buffers the whole multipart body first.
    ///
    /// The per-address policy is what closes that, so it has to be proven to
    /// actually refuse — a bound nobody has seen reject is not a bound.
    /// </summary>
    [Fact]
    public async Task PublicRequestUploadRefusesAnAddressThatHoldsNoToken()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0;
            attempt < PublicUploadLink.RequestsPerClientPerMinute + 5;
            attempt++)
        {
            // A fresh unknown token each time, so nothing is shared with the
            // per-token limiter and only the address bound can refuse this.
            var token = RequestUploadToken.Create().Secret.Token;
            using var response = await client.GetAsync(
                $"/Uploads/{Uri.EscapeDataString(token)}");
            statuses.Add(response.StatusCode);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);

        // And it refuses only after the allowance, not from the first request.
        var refusedAt = statuses.IndexOf(HttpStatusCode.TooManyRequests);
        Assert.Equal(PublicUploadLink.RequestsPerClientPerMinute, refusedAt);
        Assert.All(
            statuses.Take(refusedAt),
            status => Assert.Equal(HttpStatusCode.NotFound, status));
    }

    [Fact]
    public async Task PublicRequestUploadUsesOneCoreCommandAndPrgWithGenericCompletion()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var handler = new RecordingRequestUploadHandler();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetRequestUpload>();
                services.RemoveAll<IUploadToRequest>();
                services.AddSingleton<IGetRequestUpload>(handler);
                services.AddSingleton<IUploadToRequest>(handler);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        var route = $"/Uploads/{Uri.EscapeDataString(handler.Token)}";

        // The page's forms name their handler, and the page has no unnamed
        // one to fall back to, so the suite posts where the sender's browser
        // posts.
        var uploadRoute = $"{route}?handler=Upload";

        using var formResponse = await client.GetAsync(route);
        formResponse.EnsureSuccessStatusCode();
        var formHtml = await formResponse.Content.ReadAsStringAsync();
        using (var invalidForm = new MultipartFormDataContent())
        {
            invalidForm.Add(new StringContent(AntiforgeryValue(formHtml)), "__RequestVerificationToken");
            invalidForm.Add(new StringContent(InputValue(formHtml, "Token")), "Token");
            invalidForm.Add(new StringContent(InputValue(formHtml, "OperationKey")), "OperationKey");
            using var invalid = await client.PostAsync(uploadRoute, invalidForm);
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
            Assert.Contains("Choose a document to upload.", await invalid.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(AntiforgeryValue(formHtml)), "__RequestVerificationToken");
        form.Add(new StringContent(InputValue(formHtml, "Token")), "Token");
        form.Add(new StringContent(InputValue(formHtml, "OperationKey")), "OperationKey");
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("public upload proof"));
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
        form.Add(file, "Upload", "public-proof.txt");

        using var post = await client.PostAsync(uploadRoute, form);

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Equal(route, post.Headers.Location?.OriginalString);
        var command = Assert.Single(handler.Commands);
        Assert.Equal(handler.Token, command.Token);
        Assert.Equal("public-proof.txt", command.File.FileName);
        Assert.Equal("text/plain", command.File.MediaType);
        Assert.True(Guid.TryParseExact(command.File.OperationKey, "N", out _));

        using var completion = await client.GetAsync(post.Headers.Location!);
        var completionHtml = await completion.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, completion.StatusCode);
        Assert.Contains(
            "Your document was received and retained securely.",
            completionHtml,
            StringComparison.Ordinal);
        Assert.DoesNotContain(handler.Token, completionHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("public-proof.txt", completionHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Upload\"", completionHtml, StringComparison.Ordinal);
        Assert.Equal(4, handler.QueryCount);
    }


    [Fact]
    public async Task CanonicalCaseWorkspaceUsesTheAuthenticatedOfflineStaffSession()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/Search");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Filter cases", body, StringComparison.Ordinal);
    }
    [Fact]
    public async Task CanonicalDownloadOwnerCallsCoreAndReturnsVerifiedSafeMetadata()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var handlers = new RecordingDocumentHandlers();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDownloadCaseDocument>();
                services.AddSingleton<IDownloadCaseDocument>(handlers);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.GetAsync(
            $"/Cases/{handlers.CaseId:D}/Documents/{handlers.OccurrenceId:D}/Download?versionId={handlers.VersionId:D}");
        var content = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(handlers.Payload, content);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("engineer-report.pdf", response.Content.Headers.ContentDisposition?.ToString(), StringComparison.Ordinal);
        // DOCS-011 added an inline disposition to this route. Until then no test
        // pinned the disposition *type*, so a default flipped to inline would
        // have shipped green. This is the assertion that makes the additive
        // claim checkable.
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.True(response.Headers.TryGetValues("X-Content-SHA256", out var hashes));
        Assert.Equal(handlers.Sha256, Assert.Single(hashes));
        var query = Assert.Single(handlers.Downloads);
        Assert.Equal(handlers.CaseId, query.CaseId);
        Assert.Equal(handlers.OccurrenceId, query.OccurrenceId);
        Assert.Equal(handlers.VersionId, query.VersionId);
        Assert.Equal(ActorKind.Staff, query.Actor.Kind);
        Assert.Equal(StaffRole.Administrator, Assert.Single(query.Actor.Roles));
        Assert.True(StaffAuthorization.IsAuthorized(query.Actor, StaffAccessRight.PerformCasework));
        const string downloadOperationPrefix = "web-download:";
        Assert.StartsWith(downloadOperationPrefix, query.OperationKey, StringComparison.Ordinal);
        Assert.True(Guid.TryParseExact(
            query.OperationKey[downloadOperationPrefix.Length..],
            "N",
            out _));

        var wrongCaseId = Guid.NewGuid();
        using var denied = await client.GetAsync(
            $"/Cases/{wrongCaseId:D}/Documents/{handlers.OccurrenceId:D}/Download?versionId={handlers.VersionId:D}");
        var deniedBody = await denied.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.DoesNotContain(handlers.OccurrenceId.ToString("D"), deniedBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(handlers.VersionId.ToString("D"), deniedBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InlineFlagPreviewsOnlyContentABrowserWillNotExecute()
    {
        // DOCS-011. The preview and the download are the same authorised read
        // through the same use case; only the disposition differs. A case
        // document is arbitrary operator-supplied content, so retained HTML
        // served inline from this origin would execute as same-origin script --
        // the flag therefore allowlists what a browser renders inertly. One
        // route observed under four media types, so one host and one restored
        // database serve them all.
        using var baseFactory = new IntakeWebApplicationFactory();
        var handlers = new RecordingDocumentHandlers();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDownloadCaseDocument>();
                services.AddSingleton<IDownloadCaseDocument>(handlers);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        var route = $"/Cases/{handlers.CaseId:D}/Documents/{handlers.OccurrenceId:D}/Download"
            + $"?versionId={handlers.VersionId:D}";

        using (var preview = await client.GetAsync(route + "&inline=true"))
        {
            Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
            Assert.Equal("inline", preview.Content.Headers.ContentDisposition?.DispositionType);
            Assert.Contains(
                "engineer-report.pdf",
                preview.Content.Headers.ContentDisposition?.ToString(),
                StringComparison.Ordinal);
            Assert.Equal("application/pdf", preview.Content.Headers.ContentType?.MediaType);
            Assert.Equal(handlers.Payload, await preview.Content.ReadAsByteArrayAsync());
            Assert.Equal("nosniff", Assert.Single(preview.Headers.GetValues("X-Content-Type-Options")));
        }

        handlers.MediaType = "image/jpeg";
        handlers.FileName = "damage.jpg";
        using (var preview = await client.GetAsync(route + "&inline=true"))
        {
            Assert.Equal("inline", preview.Content.Headers.ContentDisposition?.DispositionType);
        }

        // Not on the allowlist: the flag is ignored and the save disposition
        // stands, so retained markup can never render from this origin.
        handlers.MediaType = "text/html";
        handlers.FileName = "statement.html";
        using (var refused = await client.GetAsync(route + "&inline=true"))
        {
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            Assert.Equal("attachment", refused.Content.Headers.ContentDisposition?.DispositionType);
        }

        // SVG is image/*, but it executes script when navigated to -- and the
        // document link this route serves is navigable with no script or on a
        // middle-click. It is excluded from the allowlist for that reason, so
        // an operator-supplied SVG can never render from this origin.
        handlers.MediaType = "image/svg+xml";
        handlers.FileName = "diagram.svg";
        using (var refused = await client.GetAsync(route + "&inline=true"))
        {
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            Assert.Equal("attachment", refused.Content.Headers.ContentDisposition?.DispositionType);
        }

        // And the flag is opt-in: absent, a previewable type still downloads.
        handlers.MediaType = "application/pdf";
        handlers.FileName = "engineer-report.pdf";
        using (var plain = await client.GetAsync(route))
        {
            Assert.Equal("attachment", plain.Content.Headers.ContentDisposition?.DispositionType);
        }
    }

    private static string AntiforgeryValue(string html)
    {
        var tag = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(tag.Success, "The authenticated form must render an antiforgery token.");
        var value = Regex.Match(
            tag.Value,
            "value=\"(?<value>[^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(value.Success, "The antiforgery token must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }
    private static string InputValue(string html, string name)
    {
        var tag = Regex.Match(
            html,
            $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(tag.Success, $"The form must render '{name}'.");
        var value = Regex.Match(
            tag.Value,
            "value=\"(?<value>[^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(value.Success, $"The form field '{name}' must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }


    private sealed class RecordingRequestUploadHandler : IGetRequestUpload, IUploadToRequest
    {
        private bool completed;

        public string Token { get; } = RequestUploadToken.Create().Secret.Token;

        public int QueryCount { get; private set; }

        public List<UploadToRequestCommand> Commands { get; } = [];

        public Task<RequestUploadPublicView?> ExecuteAsync(
            string token,
            CancellationToken cancellationToken = default)
        {
            QueryCount++;
            return Task.FromResult<RequestUploadPublicView?>(
                token == Token && !completed
                    ? new RequestUploadPublicView(
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "text/plain" },
                        1024)
                    : null);
        }

        public Task<UploadToRequestResult> ExecuteAsync(
            UploadToRequestCommand command,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            completed = true;
            return Task.FromResult(
                new UploadToRequestResult(
                    RequestUploadDecision.Accepted,
                    Guid.NewGuid(),
                    false));
        }

        public Task<FinalizeRequestUploadResult> FinalizeAsync(
            string token,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new FinalizeRequestUploadResult(
                RequestUploadDecision.Unavailable,
                false));
    }

    private sealed class RecordingDocumentHandlers : IDownloadCaseDocument, IExportCaseDocuments
    {
        public string MediaType { get; set; } = "application/pdf";

        public string FileName { get; set; } = "engineer-report.pdf";

        public Guid CaseId { get; } = Guid.NewGuid();

        public Guid OccurrenceId { get; } = Guid.NewGuid();

        public Guid VersionId { get; } = Guid.NewGuid();

        public byte[] Payload { get; } = Encoding.UTF8.GetBytes("verified report content");

        public byte[] ExportPayload { get; } = Encoding.UTF8.GetBytes("deterministic archive bytes");

        public string Sha256 { get; } = new('a', 64);

        public List<DownloadCaseDocumentQuery> Downloads { get; } = [];

        public List<ExportCaseDocumentsCommand> Exports { get; } = [];

        public Task<DocumentDownload?> ExecuteAsync(
            DownloadCaseDocumentQuery query,
            CancellationToken cancellationToken = default)
        {
            Downloads.Add(query);
            if (query.CaseId != CaseId
                || query.OccurrenceId != OccurrenceId
                || query.VersionId != VersionId)
            {
                return Task.FromResult<DocumentDownload?>(null);
            }

            return Task.FromResult<DocumentDownload?>(
                new(
                    new MemoryStream(Payload, writable: false),
                    FileName,
                    MediaType,
                    Payload.Length,
                    Sha256));
        }

        public Task<DocumentExport> ExecuteAsync(
            ExportCaseDocumentsCommand command,
            CancellationToken cancellationToken = default)
        {
            Exports.Add(command);
            var selection = Assert.Single(command.Selections);
            return Task.FromResult(
                new DocumentExport(
                    new MemoryStream(ExportPayload, writable: false),
                    "case-export.zip",
                    [
                        new(
                            "engineer-report.pdf",
                            selection.OccurrenceId,
                            selection.VersionId,
                            DocumentSemanticRole.EngineerReport,
                            Payload.Length,
                            Sha256)
                    ]));
        }
    }

}
