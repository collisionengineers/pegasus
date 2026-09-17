using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Custody page: custody retry, logical removal, image tags, and the
/// request-scoped upload links.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseCustodyWebTests
{
    [Fact]
    public async Task CustodyPageBindsRetryRemovalImageTagsAndRequestLinks()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<IRetryCaseCustody>(services, store);
            Substitute<ILogicallyRemoveDocument>(services, store);
            Substitute<ITagCaseImage>(services, store);
            Substitute<IUntagCaseImage>(services, store);
            Substitute<ICreateRequestUploadLink>(services, store);
            Substitute<IRevokeRequestUploadLink>(services, store);
        });
        var occurrenceId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var tagId = ImageTagVocabulary.ThirdPartyId;

        using var retried = await workspace.PostAsync(
            "Custody?handler=RetryCustody",
            workspace.MutationForm("retry-custody", "Provider storage is back", ("targetKind", "CaseSource")));
        using var removed = await workspace.PostAsync(
            "Custody?handler=RemoveDocument",
            workspace.MutationForm("remove-document", "Duplicate scan", ("occurrenceId", occurrenceId.ToString("D"))));
        using var tagged = await workspace.PostAsync(
            "Custody?handler=TagImage",
            workspace.MutationForm(
                "tag-image",
                reason: string.Empty,
                ("occurrenceId", occurrenceId.ToString("D")),
                ("tagId", tagId.ToString("D"))));
        using var untagged = await workspace.PostAsync(
            "Custody?handler=UntagImage",
            workspace.MutationForm(
                "untag-image",
                reason: string.Empty,
                ("occurrenceId", occurrenceId.ToString("D")),
                ("tagId", tagId.ToString("D"))));
        using var linkCreated = await workspace.PostAsync(
            "Custody?handler=CreateRequestUploadLink",
            workspace.MutationForm("create-request-link", "Ask the claimant for images", ("recipient", "Claimant")));
        using var linkRevoked = await workspace.PostAsync(
            "Custody?handler=RevokeRequestUploadLink",
            workspace.MutationForm(
                "revoke-request-link",
                "Sent to the wrong address",
                ("requestId", requestId.ToString("D")),
                ("expectedRequestVersion", "2")));

        // v26: the Custody page returns to the Files section.
        foreach (var response in new[] { retried, removed, linkCreated, linkRevoked })
        {
            AssertPrg(response, store.CaseId, "?section=files");
        }

        // The tag and untag posts return to the Files section's Images tab —
        // not Overview — so the operator lands back on the tile they just
        // acted on (issue: the tab and the hash were dropped on every POST).
        AssertPrgToFilesImages(tagged, store.CaseId);
        AssertPrgToFilesImages(untagged, store.CaseId);

        var retry = Assert.Single(store.CustodyRetries);
        AssertClaimant(workspace, retry.Actor);
        Assert.Equal(store.CaseVersion, retry.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, retry.EditLeaseToken);
        Assert.Equal("retry-custody", retry.OperationKey);
        Assert.Equal("Provider storage is back", retry.Reason);
        Assert.Equal(CustodyTargetKind.CaseSource, retry.TargetKind);

        var removal = Assert.Single(store.DocumentRemovals);
        AssertClaimant(workspace, removal.Actor);
        Assert.Equal(occurrenceId, removal.OccurrenceId);
        Assert.Equal(store.CaseVersion, removal.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, removal.EditLeaseToken);
        Assert.Equal("remove-document", removal.OperationKey);
        Assert.Equal("Duplicate scan", removal.Reason);

        // A tag carries the same envelope the third-party confirmation it
        // replaced carried, and no reason: the tag is the statement.
        var applied = Assert.Single(store.ImageTagsApplied);
        AssertClaimant(workspace, applied.Actor);
        Assert.Equal(occurrenceId, applied.OccurrenceId);
        Assert.Equal(tagId, applied.TagId);
        Assert.Equal(store.CaseVersion, applied.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, applied.EditLeaseToken);
        Assert.Equal("tag-image", applied.OperationKey);

        var removedTag = Assert.Single(store.ImageTagsRemoved);
        AssertClaimant(workspace, removedTag.Actor);
        Assert.Equal(occurrenceId, removedTag.OccurrenceId);
        Assert.Equal(tagId, removedTag.TagId);
        Assert.Equal(store.CaseVersion, removedTag.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, removedTag.EditLeaseToken);
        Assert.Equal("untag-image", removedTag.OperationKey);

        var linkCreation = Assert.Single(store.RequestLinkCreations);
        AssertClaimant(workspace, linkCreation.Actor);
        Assert.Equal(store.CaseVersion, linkCreation.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, linkCreation.EditLeaseToken);
        Assert.Equal("create-request-link", linkCreation.OperationKey);

        var revocation = Assert.Single(store.RequestLinkRevocations);
        AssertClaimant(workspace, revocation.Actor);
        Assert.Equal(requestId, revocation.RequestId);
        Assert.Equal(2, revocation.ExpectedRequestVersion);
        Assert.Equal(store.CaseVersion, revocation.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, revocation.EditLeaseToken);
        Assert.Equal("revoke-request-link", revocation.OperationKey);
        Assert.Equal("Sent to the wrong address", revocation.Reason);

        // The one-time secret is shown once, as the absolute link the claimant will open; it
        // survives the revoke post because only the workspace reads it.
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains(
            $"https://localhost/Uploads/{Assert.Single(store.RequestLinkSecrets).Token}",
            html,
            StringComparison.Ordinal);
        Assert.Contains("Copy this secret now", html, StringComparison.Ordinal);

        await AssertRefusalKeepsEditModeAsync(
            workspace,
            "Custody?handler=RemoveDocument",
            workspace.MutationForm("remove-document-2", "Not this one", ("occurrenceId", occurrenceId.ToString("D"))));

        // The staff upload handler and its refusal path went with the "Retain
        // document" control: the file is already stored, so there was nothing
        // for a person to retain (DOCS-012).
    }

    /// <summary>
    /// EPIC-011 §1.8 Case Files: each live file is a row carrying its name, its
    /// type, size and source, and the two things an operator does with it —
    /// View, which is the viewer's trigger, and Save as, which is the same
    /// authorised route asked to save instead of display (DOCS-011). v26: the
    /// Case's custody is the head chip; a stored file wears no custody badge
    /// of its own.
    /// </summary>
    [Fact]
    public async Task CaseFilesSectionDrawsEachLiveFileWithItsCustodyPreviewAndSaveAs()
    {
        var occurrenceId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var store = new RecordingCaseDetailsStore
        {
            CaseDocuments = [Document(occurrenceId, versionId, "instruction.pdf", "application/pdf")]
        };
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=files");
        var download =
            $"/Cases/{store.CaseId:D}/Documents/{occurrenceId:D}/Download?versionId={versionId:D}";

        var row = DocumentRow(html, occurrenceId);

        Assert.Contains("instruction.pdf", row, StringComparison.Ordinal);
        Assert.Contains("<span>" + CaseWorkspaceLabels.Files.View + "</span>", row, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.CaseWorkspace.SaveAs, row, StringComparison.Ordinal);
        Assert.Contains($"data-download-href=\"{download}\"", row, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{download}&amp;inline=True", row, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-evidence-item", row, StringComparison.Ordinal);
        Assert.Contains("data-evidence-set", html, StringComparison.Ordinal);
        Assert.Contains("data-custody-chip", html, StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorLabels.CustodyState(DocumentCustodyStatus.Confirmed), row, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.CaseWorkspace.AddEvidence, html, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.CaseWorkspace.OpenOperations, html, StringComparison.Ordinal);
        Assert.Contains("href=\"/Operations\"", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The Files section is one panel with two tabs (issue 6). Documents lists
    /// every file as a row; Images lists the case's own image occurrences as
    /// tiles built from the case documents themselves — not from the intake
    /// receipts, which a manually created Case has none of, and which is why
    /// its photographs were invisible. The three galleries the section used to
    /// stack are gone, and so is the Third-party vehicle button the tags
    /// replaced (issue 5).
    /// </summary>
    [Fact]
    public async Task CaseFilesSectionDrawsOneDocumentsTabAndOneImagesTab()
    {
        var documentOccurrenceId = Guid.NewGuid();
        var documentVersionId = Guid.NewGuid();
        var imageOccurrenceId = Guid.NewGuid();
        var imageVersionId = Guid.NewGuid();
        var store = new RecordingCaseDetailsStore
        {
            CaseDocuments =
            [
                Document(documentOccurrenceId, documentVersionId, "instruction.pdf", "application/pdf"),
                Document(
                    imageOccurrenceId,
                    imageVersionId,
                    "offside-front.jpg",
                    "image/jpeg",
                    DocumentSemanticRole.Image,
                    [
                        new(
                            ImageTagVocabulary.ThirdPartyId,
                            ImageTagVocabulary.ThirdPartyName,
                            ImageTagColour.Amber,
                            IsBuiltIn: true,
                            new DateTimeOffset(2031, 5, 6, 9, 0, 0, TimeSpan.Zero))
                    ])
            ]
        };
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=files");

        Assert.Contains("data-file-tab=\"documents\"", html, StringComparison.Ordinal);
        Assert.Contains("data-file-tab=\"images\"", html, StringComparison.Ordinal);
        Assert.Contains("data-file-tab-panel=\"documents\"", html, StringComparison.Ordinal);
        Assert.Contains("data-file-tab-panel=\"images\"", html, StringComparison.Ordinal);
        // The image is a tile asking for the derived rendering, and it wears
        // its tag as a chip.
        Assert.Contains(
            $"/Cases/{store.CaseId:D}/Documents/{imageOccurrenceId:D}/Download?versionId={imageVersionId:D}&amp;inline=True&amp;size=thumb",
            html,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ImageTagVocabulary.ThirdPartyName, html, StringComparison.Ordinal);
        // One grid, not three: no instruction-photograph gallery, no report
        // cards and no per-intake gallery in this section.
        Assert.Equal(1, Occurrences(html, "data-image-grid>"));
        Assert.DoesNotContain("Instruction photographs", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-report-images=\"files\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Third-party vehicle", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfirmThirdPartyVehicleEvidence", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// docs/design/README.md "No explanatory copy and page economy": a
    /// read-only visit renders no empty-state panel and no prose about how the
    /// page works. Both sentences this section used to carry are gone, and a
    /// case with no upload request and no images draws neither panel.
    /// </summary>
    [Fact]
    public async Task CaseFilesSectionCarriesNoExplanatoryCopyOrEmptyStatePanels()
    {
        var store = new RecordingCaseDetailsStore();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=files");

        Assert.DoesNotContain("Availability is not assumed", html, StringComparison.Ordinal);
        Assert.DoesNotContain("No vehicle images", html, StringComparison.Ordinal);
        Assert.DoesNotContain(
            OperatorLabels.CaseWorkspace.UploadRequestsPanel,
            html,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The vocabulary picker's own create-tag post also returns to the Files section's Images
    /// tab, both when it succeeds and when it is refused for a blank name — an operator adding a
    /// word to the vocabulary never loses their place on the tile they were tagging.
    /// </summary>
    [Fact]
    public async Task CreateImageTagRedirectsToFilesImagesOnSuccessAndOnRefusal()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<ICreateImageTag>(services, store);
        });

        using var blank = await workspace.PostAsync(
            "Custody?handler=CreateImageTag",
            workspace.MutationForm("create-image-tag", string.Empty, ("name", string.Empty), ("colour", "Blue")));
        AssertPrgToFilesImages(blank, store.CaseId);

        using var created = await workspace.PostAsync(
            "Custody?handler=CreateImageTag",
            workspace.MutationForm("create-image-tag-2", string.Empty, ("name", "Underside"), ("colour", "Grey")));
        AssertPrgToFilesImages(created, store.CaseId);

        var creation = Assert.Single(store.ImageTagsCreated);
        Assert.Equal("Underside", creation.Name);
        Assert.Equal(ImageTagColour.Grey, creation.Colour);
    }

    /// <summary>One Documents-tab row (v26 `.doc-row[data-document-row]`).</summary>
    private static string DocumentRow(string html, Guid occurrenceId)
    {
        var marker = html.IndexOf($"data-document-row=\"{occurrenceId:D}\"", StringComparison.Ordinal);
        Assert.True(marker >= 0, "The document row is not rendered.");
        var start = html.LastIndexOf("<div", marker, StringComparison.Ordinal);
        // Up to the next row, or the Images panel that follows the list.
        var end = html.IndexOf("data-document-row=", marker + 1, StringComparison.Ordinal);
        if (end < 0)
        {
            end = html.IndexOf("data-file-tab-panel=\"images\"", marker, StringComparison.Ordinal);
        }
        Assert.True(end > start, "The document row is not closed.");
        return html[start..end];
    }

    private static void AssertPrgToFilesImages(HttpResponseMessage response, Guid caseId)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.StartsWith($"/Cases/{caseId:D}", location, StringComparison.Ordinal);
        Assert.Contains("section=files", location, StringComparison.Ordinal);
        Assert.EndsWith("#case-files-images", location, StringComparison.Ordinal);
    }


    [Fact]
    public async Task CaseFilesRendersQueriesTableForLinkedQueryMailAndNoManualControls()
    {
        var forwardedId = Guid.NewGuid();
        var senderlessId = Guid.NewGuid();
        var receivedAtUtc = new DateTimeOffset(2031, 5, 6, 9, 15, 0, TimeSpan.Zero);
        var store = new RecordingCaseDetailsStore
        {
            QueryEmails =
            [
                new(
                    forwardedId,
                    receivedAtUtc,
                    "original@qdosassist.co.uk",
                    "Forwarding Desk",
                    "desk@collisionengineers.co.uk",
                    "Repair query",
                    MailCategory.Received(ReceivedMailFamily.PostReportEmails, "query")),
                new(
                    senderlessId,
                    receivedAtUtc.AddMinutes(-1),
                    null,
                    null,
                    null,
                    "Sender unavailable",
                    MailCategory.Received(ReceivedMailFamily.PostReportEmails, "dispute"))
            ]
        };
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseVehicleSection>(services, store);
                Substitute<IGetCaseValuationSection>(services, store);
                Substitute<IGetCaseNotesSection>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=files");
        // v26: the retained query mail is the Files section's Correspondence
        // tab, counted on its tab and listed as a plain table.
        Assert.Contains("data-file-tab=\"correspondence\">Correspondence · 2<", html, StringComparison.Ordinal);
        var queries = Correspondence(html);
        var visible = WebUtility.HtmlDecode(VisibleText(queries));

        foreach (var heading in new[] { "Received", "Sender", "Subject", "Classification" })
        {
            Assert.Contains(heading, visible, StringComparison.Ordinal);
        }
        Assert.Contains("06 May 2031 10:15", visible, StringComparison.Ordinal);
        Assert.Contains("original@qdosassist.co.uk", visible, StringComparison.Ordinal);
        Assert.DoesNotContain("Forwarding Desk", visible, StringComparison.Ordinal);
        Assert.DoesNotContain("desk@collisionengineers.co.uk", visible, StringComparison.Ordinal);
        Assert.Contains("Repair query", visible, StringComparison.Ordinal);
        Assert.Contains("Post-report · Query", visible, StringComparison.Ordinal);
        Assert.Contains("Sender unavailable", visible, StringComparison.Ordinal);
        Assert.DoesNotContain("Sender not recorded", visible, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Inbox/{forwardedId:D}\"", queries, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"href=\"/Inbox/{senderlessId:D}\"", queries, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<form", queries, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<button", queries, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("disabled", queries, StringComparison.OrdinalIgnoreCase);
        var pageText = WebUtility.HtmlDecode(VisibleText(html));
        foreach (var control in new[] { "Raise a query", "Reply", "Resolve", "Mark resolved" })
        {
            Assert.DoesNotContain(control, pageText, StringComparison.OrdinalIgnoreCase);
        }
    }


    [Fact]
    public async Task CaseFilesOmitsQueriesWhenNoLinkedQueryMailExists()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseVehicleSection>(services, store);
                Substitute<IGetCaseValuationSection>(services, store);
                Substitute<IGetCaseNotesSection>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=files");

        Assert.Contains("data-file-tab=\"correspondence\">Correspondence · 0<", html, StringComparison.Ordinal);
        Assert.Contains("data-correspondence-empty", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-correspondence-row", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Raise a query", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// PR 670 port (B01): an upload request names who it was sent to and why,
    /// read from the request's own record; a request recorded before those
    /// facts existed shows the absent marker rather than an empty cell.
    /// </summary>

    [Fact]
    public async Task UploadRequestsListRecipientAndReasonFromTheRecord()
    {
        var createdAtUtc = new DateTimeOffset(2031, 5, 6, 9, 0, 0, TimeSpan.Zero);
        var store = new RecordingCaseDetailsStore
        {
            RequestUploadLinks =
            [
                new(
                    Guid.NewGuid(),
                    RequestUploadStatus.Active,
                    createdAtUtc,
                    createdAtUtc.AddDays(7),
                    null,
                    0,
                    0,
                    1,
                    "Provider claims team",
                    "Missing photographs of the rear damage"),
                new(
                    Guid.NewGuid(),
                    RequestUploadStatus.Expired,
                    createdAtUtc.AddDays(-14),
                    createdAtUtc.AddDays(-7),
                    null,
                    2,
                    4_096,
                    3)
            ]
        };
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseVehicleSection>(services, store);
                Substitute<IGetCaseValuationSection>(services, store);
                Substitute<IGetCaseNotesSection>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=files");
        var panel = UploadRequests(html);
        var visible = WebUtility.HtmlDecode(VisibleText(panel));

        foreach (var heading in new[] { "Recipient", "Reason", "State", "Created", "Expires", "Accepted" })
        {
            Assert.Contains(heading, visible, StringComparison.Ordinal);
        }
        Assert.Contains("Provider claims team", visible, StringComparison.Ordinal);
        Assert.Contains("Missing photographs of the rear damage", visible, StringComparison.Ordinal);
        Assert.Equal(2, Occurrences(visible, Pegasus.Web.Presentation.OperatorLabels.CaseWorkspace.AbsentValue));
        Assert.DoesNotContain("id=\"create-upload-request\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// PR 670 port (B01), the write side over the shared G17 contract: the
    /// create dialog requires a recipient and offers a reason; the handler
    /// forwards both unchanged, and an omitted reason reaches Core as null.
    /// </summary>

    [Fact]
    public async Task CreateUploadRequestDialogPostsRecipientAndReasonToTheCommand()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ICreateRequestUploadLink>(services, store));

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=files");
        // v26: the create dialog is a div-backdrop dialog opened from the
        // Files section's More menu; its form is what the test reads.
        Assert.Contains("data-dialog-open=\"create-upload-request\"", html, StringComparison.Ordinal);
        var dialog = html[html.IndexOf("id=\"create-upload-request\"", StringComparison.Ordinal)..];
        dialog = dialog[..dialog.IndexOf("</form>", StringComparison.Ordinal)];

        Assert.Contains($"/Cases/{store.CaseId:D}/Custody?handler=CreateRequestUploadLink", dialog, StringComparison.Ordinal);
        Assert.Matches("<input[^>]*name=\"recipient\"[^>]*required", dialog);
        Assert.Contains("name=\"reason\"", dialog, StringComparison.Ordinal);

        using var withReason = await workspace.PostAsync(
            "Custody?handler=CreateRequestUploadLink",
            workspace.MutationForm(
                "create-request-link-1",
                "  Please send the rear photographs  ",
                ("recipient", "Provider claims team")));
        using var withoutReason = await workspace.PostAsync(
            "Custody?handler=CreateRequestUploadLink",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", "create-request-link-2"),
                ("editLeaseToken", store.LeaseToken),
                ("recipient", "Claimant"),
                ("reason", "")));

        AssertPrg(withReason, store.CaseId);
        AssertPrg(withoutReason, store.CaseId);
        Assert.Equal(2, store.RequestLinkCreations.Count);
        var first = store.RequestLinkCreations[0];
        AssertClaimant(workspace, first.Actor);
        Assert.Equal(store.CaseVersion, first.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, first.EditLeaseToken);
        Assert.Equal("create-request-link-1", first.OperationKey);
        Assert.Equal("Provider claims team", first.Recipient);
        Assert.Equal("  Please send the rear photographs  ", first.Reason);
        var second = store.RequestLinkCreations[1];
        Assert.Equal("Claimant", second.Recipient);
        Assert.Null(second.Reason);
    }

    /// <summary>
    /// The create action requires the recipient server-side as well: a post
    /// without one, or with only whitespace, is refused before the command
    /// port is reached, and the editor keeps edit mode to correct it.
    /// </summary>

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateUploadRequestWithoutARecipientNeverReachesTheCommand(string? recipient)
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ICreateRequestUploadLink>(services, store));
        var fields = new List<(string Name, string Value)>
        {
            ("id", store.CaseId.ToString("D")),
            ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
            ("operationKey", "create-request-link-blank"),
            ("editLeaseToken", store.LeaseToken),
            ("reason", "Photographs of the rear damage")
        };
        if (recipient is not null)
        {
            fields.Add(("recipient", recipient));
        }

        using var refused = await workspace.PostAsync(
            "Custody?handler=CreateRequestUploadLink",
            Form(workspace.AntiforgeryToken, [.. fields]));

        AssertPrg(refused, store.CaseId);
        Assert.Empty(store.RequestLinkCreations);
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
        Assert.Equal(store.LeaseToken, InputValue(html, "editLeaseToken"));
    }

    /// <summary>
    /// The frame's fragment handler answers with one section body and nothing
    /// of the record around it, so a mounted section cannot replace the frame
    /// or another section.
    /// </summary>

    [Fact]
    public async Task TheLazyFilesFragmentDoesNotTouchCookieBackedEditState()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, _ => { });

        using var response = await workspace.Client.GetAsync(
            $"/Cases/{store.CaseId:D}/Section?section=files");
        response.EnsureSuccessStatusCode();
        var fragment = await response.Content.ReadAsStringAsync();

        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
        Assert.DoesNotContain("name=\"editLeaseToken\"", fragment, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=SaveAssetPreparation", fragment, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=CreateRequestUploadLink", fragment, StringComparison.Ordinal);
    }

    /// <summary>
    /// The browser repeats the token already rendered in its edit form only as
    /// section-rendering data. This retains supported Files controls without
    /// restoring or writing cookie-backed TempData from the async GET.
    /// </summary>

    [Fact]
    public async Task TheLazyFilesFragmentRendersExistingEditControlsFromItsHeaderWithoutSettingCookies()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, _ => { });
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Cases/{store.CaseId:D}/Section?section=files");
        request.Headers.Add("X-Pegasus-Edit-Lease", store.LeaseToken);

        using var response = await workspace.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var fragment = await response.Content.ReadAsStringAsync();

        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
        Assert.Equal(store.LeaseToken, InputValue(fragment, "editLeaseToken"));
        Assert.Contains("handler=CreateRequestUploadLink", fragment, StringComparison.Ordinal);
    }


    [Fact]
    public async Task TheLazyFilesFragmentRejectsARenderHeaderWhenAnotherActorHoldsTheCase()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, _ => { });
        store.LeaseHolder = Guid.NewGuid().ToString("D");
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Cases/{store.CaseId:D}/Section?section=files");
        request.Headers.Add("X-Pegasus-Edit-Lease", store.LeaseToken);

        using var response = await workspace.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var fragment = await response.Content.ReadAsStringAsync();

        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
        Assert.DoesNotContain("name=\"editLeaseToken\"", fragment, StringComparison.Ordinal);
    }


    [Fact]
    public async Task TheLazyFilesFragmentRejectsAWrongRenderLeaseTokenFromItsHolder()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, _ => { });
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Cases/{store.CaseId:D}/Section?section=files");
        request.Headers.Add("X-Pegasus-Edit-Lease", new string('b', CaseEditAuthority.LeaseTokenLength));

        using var response = await workspace.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var fragment = await response.Content.ReadAsStringAsync();

        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
        Assert.DoesNotContain("name=\"editLeaseToken\"", fragment, StringComparison.Ordinal);
    }


    [Fact]
    public async Task TheLazyFilesFragmentRejectsAStaleRenderLeaseToken()
    {
        var store = new RecordingCaseDetailsStore { RenderLeaseIsCurrent = false };
        using var workspace = await EnterEditModeAsync(store, _ => { });
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Cases/{store.CaseId:D}/Section?section=files");
        request.Headers.Add("X-Pegasus-Edit-Lease", store.LeaseToken);

        using var response = await workspace.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var fragment = await response.Content.ReadAsStringAsync();

        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
        Assert.DoesNotContain("name=\"editLeaseToken\"", fragment, StringComparison.Ordinal);
    }


    [Fact]
    public async Task CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        // Capture Review with an eligible native handoff and optional EVA
        // delivery. No external action is required to assign the Engineer.
        var store = new RecordingCaseDetailsStore
        {
            ExposeCustody = true,
            State = CaseLifecycleState.Review
        };
        var evaStores = new StubEvaSubmissionStores(
            new EvaSubmissionModes(PrincipalReportGenerationPolicy.EvaZip));
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                Substitute<IStaffAccountQueries>(services,
                    new StubStaffAccounts(Guid.NewGuid(), "Engineer", StaffRole.Engineer));
                Substitute<IEvaSubmissionQueries>(services, evaStores);
                Substitute<IEvaSubmissionModeStore>(services, evaStores);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        var initial = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        using var claim = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(initial),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", InputValue(initial, "operationKey"))));
        AssertPrg(claim, store.CaseId);

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Contains("name=\"expectedVersion\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"operationKey\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"editLeaseToken\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"reason\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain(store.CaseId.ToString("D"), VisibleText(html), StringComparison.OrdinalIgnoreCase);

        // ENG-016: the export must post, because it records the once-per-case
        // First sent to Engineer proxy and a prefetched or refreshed GET must
        // not be able to fire it.
        //
        // EXT-04 moved the control: the handoff dialog carries the export as a
        // posted form and no link to it exists anywhere, and the export route
        // answers a GET with a redirect rather than a package (asserted below).
        Assert.Contains("Send to EVA", VisibleText(html), StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"href=\"/Cases/{store.CaseId:D}/Documents/Export",
            html,
            StringComparison.Ordinal);

        foreach (var route in new[] { "Custody?handler=RetryCustody", "Documents/Export?handler=Bundle" })
        {
            using var denied = await client.PostAsync(
                $"/Cases/{store.CaseId:D}/{route}",
                new FormUrlEncodedContent([]));
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        }

        // The hand-off's own page is gone, not merely unlinked. 405 rather
        // than 404 is this app's existing answer to a POST at a path with no
        // page: the 404 is re-executed at /status/{code} by
        // UseStatusCodePagesWithReExecute, and that page has only an OnGet.
        using var downloadGone = await client.PostAsync(
            $"/Cases/{store.CaseId:D}/Eva/Download",
            Form(AntiforgeryValue(html)));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, downloadGone.StatusCode);

        // The generate handler is gone too, but its page survives for the
        // vehicle actions, and Razor Pages answers an unrecognised handler name
        // by running no handler at all rather than by refusing the request. So
        // the honest assertion is not 404: it is that a stale form or bookmark
        // now does nothing -- no redirect back to the workspace, which is what
        // every real handler on these pages ends with.
        using var handlerGone = await client.PostAsync(
            $"/Cases/{store.CaseId:D}/Vehicle?handler=GenerateEvaHandoff",
            Form(AntiforgeryValue(html)));
        Assert.NotEqual(HttpStatusCode.Redirect, handlerGone.StatusCode);
        Assert.Null(handlerGone.Headers.Location);

        // A GET on the export route cannot produce the package: there is no GET
        // that exports, only one that returns a stale bookmark to the case.
        using var prefetched = await client.GetAsync(
            $"/Cases/{store.CaseId:D}/Documents/Export");
        AssertPrg(prefetched, store.CaseId);
        Assert.NotEqual(
            "application/zip",
            prefetched.Content.Headers.ContentType?.MediaType);
    }


    private static string UploadRequests(string html)
    {
        var start = html.IndexOf("data-upload-requests", StringComparison.Ordinal);
        Assert.True(start >= 0, "The upload requests panel is not rendered.");
        var end = html.IndexOf("</table>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The upload requests table is not rendered.");
        return html[start..end];
    }

    /// <summary>The Files section's Correspondence tab body (v26): the table of retained query mail.</summary>

    private static string Correspondence(string html)
    {
        var start = html.IndexOf("data-correspondence>", StringComparison.Ordinal);
        Assert.True(start >= 0, "The Correspondence tab is not rendered.");
        var end = html.IndexOf("</table>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The Correspondence table is not rendered.");
        return html[start..end];
    }

}
