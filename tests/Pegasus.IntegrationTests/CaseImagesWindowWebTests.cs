using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Pop out (operator, 9 October 2026): the Case's images in their own window,
/// <c>/Cases/{id}/Images</c>. The Files head and the viewer offer it; the
/// window is the read-mode tiles under the same viewer, opened on the image
/// Pop out was pressed on, and holds no lease.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseImagesWindowWebTests
{
    [Fact]
    public async Task TheImagesWindowRendersTheReadableImagesUnderTheViewerWithoutALease()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store(CaseLifecycleState.ReportPreparation);
        using var workspace = await OpenEngineerWorkspaceAsync(
            store,
            services => Substitute<ICaseAssetPreparationQueries>(services, store));

        var html = await GetHtmlAsync(
            workspace.Client,
            $"/Cases/{store.CaseId:D}/Images?image={fixture.OverviewOccurrenceId:D}");

        // The window: no shell, no Case record, no form, no lease controls.
        Assert.Contains("class=\"case-images-window\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-case-record", html, StringComparison.Ordinal);
        Assert.DoesNotContain("case-edit-form", html, StringComparison.Ordinal);
        Assert.DoesNotContain("editLeaseToken", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"rail", html, StringComparison.Ordinal);

        // Every readable image is a read-mode tile: a link to its preview
        // naming its occurrence, no preparation and no tools.
        Assert.Equal(5, Regex.Count(html, "data-evidence-item"));
        Assert.Contains($"data-evidence-occurrence=\"{fixture.CloseUpOccurrenceId:D}\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-preparation-card", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-evidence-preparation-occurrence=\"" + fixture.OverviewOccurrenceId.ToString("D"), html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-image-in-report", html, StringComparison.Ordinal);
        Assert.DoesNotContain("tag-picker", html, StringComparison.Ordinal);
        // The report's order: the Overview holds place 1 by its tag, then the
        // Close-up (operator, 7 October 2026), then the Supporting images.
        Assert.True(
            html.IndexOf(fixture.OverviewOccurrenceId.ToString("D"), StringComparison.Ordinal)
            < html.IndexOf(fixture.CloseUpOccurrenceId.ToString("D"), StringComparison.Ordinal));
        Assert.True(
            html.IndexOf(fixture.CloseUpOccurrenceId.ToString("D"), StringComparison.Ordinal)
            < html.IndexOf(fixture.FirstSupportingOccurrenceId.ToString("D"), StringComparison.Ordinal));

        // The same viewer, standalone: no Pop out of its own, and it opens on
        // the image the address names.
        Assert.Contains("data-case-viewer data-viewer-standalone=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("data-viewer-download", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-viewer-popout", html, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(html, "data-evidence-start=\"true\""));
        var start = html.IndexOf("data-evidence-start=\"true\"", StringComparison.Ordinal);
        Assert.Contains(
            $"data-evidence-occurrence=\"{fixture.OverviewOccurrenceId:D}\"",
            html[start..html.IndexOf("</a>", start, StringComparison.Ordinal)],
            StringComparison.Ordinal);

        // An image the address does not name opens the window on the first.
        var unnamed = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}/Images?image={Guid.NewGuid():D}");
        Assert.DoesNotContain("data-evidence-start=\"true\"", unnamed, StringComparison.Ordinal);

        // Another Case is not this store's: not found. Anonymous is sent to
        // sign in.
        using var other = await workspace.Client.GetAsync($"/Cases/{Guid.NewGuid():D}/Images");
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        using var anonymousRequest = new HttpRequestMessage(HttpMethod.Get, $"/Cases/{store.CaseId:D}/Images");
        anonymousRequest.Headers.Add("X-Test-Anonymous", "1");
        using var anonymous = await workspace.Client.SendAsync(anonymousRequest);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.Contains("/Account/SignIn", anonymous.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATriageCaseHasNoImagesWindow()
    {
        // The route filter answers before the page: the ports the page would
        // read are the store's, and the Case's kind is Triage.
        var store = new PreparedImages().Store();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<ICaseAssetPreparationQueries>(services, store);
                Substitute<IGetCaseKind>(services, new TriageKind());
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Engineer");

        using var response = await client.GetAsync($"/Cases/{store.CaseId:D}/Images");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheCasePageOffersPopOutInTheFilesHeadAndTheViewerOnceAnImageCanBeRead()
    {
        var store = new PreparedImages().Store();
        using var workspace = await OpenEngineerWorkspaceAsync(
            store,
            services => Substitute<ICaseAssetPreparationQueries>(services, store));

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=files");
        var files = Section(html, "section-files-title");
        var head = files[..files.IndexOf("class=\"panel-body", StringComparison.Ordinal)];
        var popout = $"<a class=\"btn btn--small\" href=\"/Cases/{store.CaseId:D}/Images\" target=\"_blank\" rel=\"noopener\" data-images-popout>";
        Assert.Contains(popout, head, StringComparison.Ordinal);
        // After the one primary, Add evidence; a secondary button, not a menu item.
        Assert.True(head.IndexOf("data-add-evidence", StringComparison.Ordinal) < head.IndexOf("data-images-popout", StringComparison.Ordinal));
        Assert.Contains(">Pop out<", head, StringComparison.Ordinal);
        // Every tile names its occurrence for the viewer's Pop out, in read mode too.
        Assert.Contains($"data-evidence-occurrence=\"{store.CaseDocuments[0].Occurrences[0].Id:D}\"", files, StringComparison.Ordinal);

        // The viewer's own Pop out: a link to the window that the script
        // points at the image in view; hidden until one is.
        Assert.Contains("data-case-viewer data-viewer-standalone=\"false\" hidden", html, StringComparison.Ordinal);
        Assert.Contains(
            $"<a class=\"btn btn--small\" href=\"/Cases/{store.CaseId:D}/Images\" target=\"_blank\" rel=\"noopener\" data-viewer-popout data-images-popout hidden>",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheFilesHeadHasNoPopOutWhileNoImageCanBeRead()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await OpenEngineerWorkspaceAsync(store, _ => { });

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=files");
        var files = Section(html, "section-files-title");
        Assert.DoesNotContain("data-images-popout", files, StringComparison.Ordinal);
        Assert.Contains("data-add-evidence", files, StringComparison.Ordinal);
    }

    private sealed class TriageKind : IGetCaseKind
    {
        public Task<CaseType?> ExecuteAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseType?>(CaseType.Triage);
    }
}
