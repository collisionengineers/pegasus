using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.Address;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Tasks;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class CaseDetailsWebTests
{
    /// <summary>
    /// D30: the Engineer's work is Case sections, so the record carries no
    /// Open Assessment action and no assessment gate — neither enabled nor
    /// drawn disabled — whatever the shared access decision says.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheRecordOffersNoAssessmentAction(bool canOpen)
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
                services.RemoveAll<IGetAssessmentAccess>();
                services.AddSingleton<IGetAssessmentAccess>(new FakeGetAssessmentAccess(canOpen));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");

        Assert.DoesNotContain("Open Assessment", html, StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"/Cases/{store.CaseId:D}/Assessment",
            html,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// EPIC-011 §1.8 and FRD-07: the EVA handoff is a Review act. Outside
    /// Review the workspace offers no EVA control and draws no handoff, rather
    /// than drawing a disabled one.
    /// </summary>
    [Theory]
    [InlineData(CaseLifecycleState.NotReady, false)]
    [InlineData(CaseLifecycleState.ReportPreparation, true)]
    [InlineData(CaseLifecycleState.PostReport, true)]
    [InlineData(CaseLifecycleState.Review, true)]
    public async Task SendToEvaRendersInReviewAndWithEngineer(CaseLifecycleState state, bool offersHandoff)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore { State = state };
        var evaStores = new StubEvaSubmissionStores(
            new EvaSubmissionModes(PrincipalReportGenerationPolicy.EvaZip));
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
                Substitute<IEvaSubmissionQueries>(services, evaStores);
                Substitute<IEvaSubmissionModeStore>(services, evaStores);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");

        Assert.Equal(offersHandoff, RecordBar(html).Contains("Send to EVA", StringComparison.Ordinal));
        Assert.Equal(
            offersHandoff,
            html.Contains("data-dialog=\"eva-handoff-dialog\"", StringComparison.Ordinal));
        // TICK-223: the trigger is a real link to the fallback page the
        // dialog's own form posts to, so the handoff stays reachable without
        // JavaScript rather than being a dead button with no static target.
        Assert.Equal(
            offersHandoff,
            html.Contains(
                $"href=\"/Cases/{store.CaseId:D}/Eva/Send\"",
                StringComparison.OrdinalIgnoreCase));
        // The handoff's own routes come with it: the export posts from the
        // dialog, so the route is present exactly when the control is.
        Assert.Equal(
            offersHandoff,
            html.Contains(
                $"/Cases/{store.CaseId:D}/Documents/Export",
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// D10: "report sent" is confirmed from detected Sent evidence and is never
    /// asserted by hand, so the action renders only while the case is With
    /// Engineer, this browser holds the edit authority, and retained evidence
    /// exists. The evidence is named by mailbox and time; the transport handles
    /// and hashes it also carries stay internal.
    /// </summary>
    [Theory]
    [InlineData(CaseLifecycleState.ReportPreparation, true, true)]
    [InlineData(CaseLifecycleState.ReportPreparation, false, false)]
    [InlineData(CaseLifecycleState.Review, true, false)]
    public async Task ReportSentRendersOnlyWithDetectedEvidenceWhileWithEngineer(
        CaseLifecycleState state,
        bool hasEvidence,
        bool offersConfirmation)
    {
        var evidence = new RetainedApprovedMailboxReportSentEvidence(
            Guid.NewGuid(),
            "reports@collisionengineers.example",
            "sent-folder-handle",
            "immutable-item-handle",
            "internet-message-handle",
            "conversation-handle",
            "reply-chain-handle",
            "source-occurrence-handle",
            new string('b', 64),
            new string('c', 64),
            new DateTimeOffset(2031, 5, 6, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2031, 5, 6, 9, 5, 0, TimeSpan.Zero),
            ActionActor.SystemWorker("sent-mail-worker"));
        var store = new RecordingCaseDetailsStore
        {
            State = state,
            AvailableReportSentEvidence = hasEvidence ? [evidence] : []
        };
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = await workspace.GetWorkspaceAsync();

        Assert.Equal(
            offersConfirmation,
            RecordBar(html).Contains("Mark report sent", StringComparison.Ordinal));
        Assert.Equal(
            offersConfirmation,
            html.Contains("handler=LinkReportEvidence", StringComparison.Ordinal));
        if (offersConfirmation)
        {
            var visible = VisibleText(html);
            Assert.Contains("reports@collisionengineers.example", visible, StringComparison.Ordinal);
            Assert.DoesNotContain("immutable-item-handle", visible, StringComparison.Ordinal);
            Assert.DoesNotContain("internet-message-handle", visible, StringComparison.Ordinal);
            Assert.DoesNotContain(new string('b', 64), visible, StringComparison.Ordinal);
            Assert.DoesNotContain(new string('c', 64), visible, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// D29/D30: the record is one scrolling page of ten sections in a fixed
    /// order. Every section has its stable host and its jump link, in that
    /// order, on every response.
    /// </summary>
    [Fact]
    public async Task TheRecordRendersTenOrderedSectionHostsAndJumpLinks()
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

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");

        Assert.Equal(CaseSectionKeys, HostOrder(html));
        Assert.Equal(CaseSectionKeys, JumpLinkOrder(html));

        // The four sections that have a body below the fold are served as
        // fragments; every other host, including the Engineer shells,
        // renders with the page.
        Assert.Equal(
            ["vehicle", "valuation", "files", "notes"],
            DeferredSections(html));
    }

    /// <summary>
    /// <c>?section=</c> is a jump target, not an alternative: the addressed
    /// section is rendered by the first response, so the link works with no
    /// script, and it is the entry the jump-nav marks current. A key the record
    /// does not own — including the deleted pre-redesign keys, which are not
    /// aliased — selects Overview rather than nothing.
    /// </summary>
    [Theory]
    [InlineData("", "overview")]
    [InlineData("?section=overview", "overview")]
    [InlineData("?section=engineer-notes", "overview")]
    [InlineData("?section=vehicle", "vehicle")]
    [InlineData("?section=estimate", "estimate")]
    [InlineData("?section=files", "files")]
    [InlineData("?section=notes", "notes")]
    [InlineData("?section=valuations", "overview")]
    [InlineData("?section=inspection-address", "overview")]
    [InlineData("?section=case-files", "overview")]
    [InlineData("?section=evidence", "overview")]
    [InlineData("?tab=files", "overview")]
    public async Task TheAddressedSectionIsRenderedAndMarkedCurrent(
        string query,
        string currentSection)
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

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}{query}");

        Assert.Equal(currentSection, CurrentSectionKey(html));
        Assert.DoesNotContain(
            $"data-lazy=\"{currentSection}\"",
            html,
            StringComparison.Ordinal);
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
    [Theory]
    [InlineData("files")]
    [InlineData("notes")]
    [InlineData("vehicle")]
    [InlineData("valuation")]
    public async Task TheSectionFragmentReturnsOnlyThatSectionBody(string key)
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
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var fragment = await GetHtmlAsync(
            client,
            $"/Cases/{store.CaseId:D}/Section?section={key}");

        Assert.DoesNotContain("case-sticky", fragment, StringComparison.Ordinal);
        Assert.DoesNotContain("section-nav", fragment, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"case-main\"", fragment, StringComparison.Ordinal);
        Assert.DoesNotContain("<html", fragment, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A section the frame renders itself, and a key the record does not own,
    /// are not fragments at all — the deleted keys are refused rather than
    /// aliased.
    /// </summary>
    [Theory]
    [InlineData("overview")]
    [InlineData("inspection")]
    [InlineData("case-files")]
    [InlineData("engineer-notes")]
    [InlineData("nonsense")]
    public async Task TheSectionFragmentRefusesKeysItDoesNotServe(string key)
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

        using var response = await client.GetAsync(
            new Uri($"/Cases/{store.CaseId:D}/Section?section={key}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// The record has exactly one editor. Every section that contributes fields
    /// to its Save form renders at once while the lease is held; Files has no
    /// such fields and mounts separately. A second form posting the whole
    /// record would write stored values over another section's unsaved input,
    /// so Inspection contributes to the one record form instead. Editing one
    /// section and saving therefore cannot discard an unsaved edit in another.
    /// </summary>
    [Fact]
    public async Task TheRecordRendersOneEditorForEverySection()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = await workspace.GetWorkspaceAsync();

        Assert.Equal(1, Occurrences(html, $"/Cases/{store.CaseId:D}?handler=Save"));
        Assert.Equal(1, Occurrences(html, "id=\"case-edit-form\""));
        Assert.Equal(1, Occurrences(html, "data-edit-save"));
        // Each editable value SaveCase writes appears once across the
        // record, so no control is shadowed by a stale copy of itself.
        foreach (var field in new[]
        {
            "claimantName",
            "claimantContactNumber",
            "claimantAddress",
            "claimNumber",
            "vehicleRegistration",
            "vehicleMake",
            "vehicleModel",
            "vehicleMileage",
            "vehicleMileageUnit",
            "accidentCircumstances",
            "incidentDate",
            "contactName",
            "contactEmailAddress",
            "contactPhoneNumber",
            "instructionDate",
            "vatStatus",
            "inspectionDate",
            "inspectionDeadline",
            "inspectionAddress",
            "inspectionMode",
            "storageLocation"
        })
        {
            Assert.Equal(1, Occurrences(html, $"name=\"{field}\""));
        }
        // The Inspection section's control is the record form's entry for the
        // address, wherever it renders on the page.
        Assert.Contains(
            "id=\"inspection-address\" class=\"fi\" name=\"inspectionAddress\" form=\"case-edit-form\"",
            html,
            StringComparison.Ordinal);
        // WP6: the vehicle's own identity is edited in the Vehicle section,
        // between that section's host and the next one, and still posts
        // through the record's one form.
        Assert.Contains(
            "id=\"edit-registration\" class=\"fi mono\" name=\"vehicleRegistration\" form=\"case-edit-form\"",
            html,
            StringComparison.Ordinal);
        foreach (var control in new[] { "edit-registration", "edit-make", "edit-model" })
        {
            Assert.InRange(
                html.IndexOf($"id=\"{control}\"", StringComparison.Ordinal),
                html.IndexOf("id=\"section-vehicle\"", StringComparison.Ordinal),
                html.IndexOf("id=\"section-damage\"", StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// WP6 (issue 1): the record's frame is three sticky rows — the identity
    /// ribbon carrying the record's own two controls, one action row, and the
    /// section nav. Nothing sits above them to scroll away, and the edit state
    /// is a badge beside Cancel and Save rather than a fourth row.
    /// </summary>
    [Fact]
    public async Task TheCaseFrameIsThreeStickyRowsWithNoPageHeaderOrEditBar()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = await workspace.GetWorkspaceAsync();

        Assert.DoesNotContain("page-header", html, StringComparison.Ordinal);
        Assert.DoesNotContain("edit-bar", html, StringComparison.Ordinal);
        // v26: one measured sticky block — the 56px ribbon and the 40px
        // section row — and nothing else travels with the record.
        Assert.Equal(1, Occurrences(html, "data-sticky-block"));
        Assert.Equal(1, Occurrences(html, "class=\"ribbon\""));
        Assert.Equal(1, Occurrences(html, "class=\"section-row\""));

        // The identity row: the eyebrow with the registration, the reference
        // as the page's one heading, Claimant, Principal, Engineer, the
        // state chip and the record's two controls. Back to Cases and the
        // presence strip are gone (v25 decisions 1 and C).
        Assert.Contains(
            "<div class=\"ribbon-label\">Case workspace · AB12CDE</div>",
            html,
            StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(html, "<h1 class=\"ribbon-value\">"));
        Assert.DoesNotContain("Back to Cases", html, StringComparison.Ordinal);
        Assert.DoesNotContain("presence-strip", html, StringComparison.Ordinal);
        var refresh = RefreshForm(html);
        Assert.Contains("name=\"section\"", refresh, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Refresh\"", refresh, StringComparison.Ordinal);

        // The ribbon's actions: Cancel and Save beside the Editing badge and
        // the one Actions menu; no reason dialog stands between Save and the
        // record (v25 decision A).
        var actions = StickyActionRow(html);
        Assert.Contains("form=\"case-finish-editing-form\"", actions, StringComparison.Ordinal);
        Assert.Contains("data-case-cancel-form", actions, StringComparison.Ordinal);
        Assert.Contains("form=\"case-edit-form\"", actions, StringComparison.Ordinal);
        Assert.DoesNotContain("data-case-save-reason", html, StringComparison.Ordinal);
        Assert.DoesNotContain("case-save-reason-dialog", html, StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(actions, ">Cancel</span>"));
        Assert.Equal(1, Occurrences(actions, ">Save</span>"));
        Assert.Contains("case-edit-badge", actions, StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(actions, "data-case-actions"));
        Assert.DoesNotContain("You are editing this case", html, StringComparison.Ordinal);

        // The second row is the section nav, with its Scroll/Tabs switch.
        Assert.Equal(1, Occurrences(html, "data-section-nav"));
        Assert.Equal(1, Occurrences(html, "data-case-layout-switch"));
    }

    /// <summary>
    /// WP6 (issue 2): the Inspection panel prints each fact once. The chosen
    /// source and the mode restated the address itself on an image-based
    /// Case, which read as the same value four times; the Principal's default
    /// is named only where the Case holds something else.
    /// </summary>
    [Fact]
    public async Task TheInspectionPanelPrintsEachFactOnceAndNamesTheDefaultOnlyWhenItDiffers()
    {
        var store = new RecordingCaseDetailsStore();
        var panel = InspectionPanel(await ReadCaseAsync(store));

        // v26: one geometry of labelled cells — the address, then the Repairer
        // and Storage sub-panels (storage money moved here from Settlement).
        Assert.Contains("1 Depot Road", panel, StringComparison.Ordinal);
        Assert.Contains(">Storage location<", panel, StringComparison.Ordinal);
        Assert.Contains("14 Storage Lane", panel, StringComparison.Ordinal);
        Assert.Contains("data-inspection-repairer", panel, StringComparison.Ordinal);
        Assert.Contains(">Repairer<", panel, StringComparison.Ordinal);
        Assert.Contains("data-inspection-storage", panel, StringComparison.Ordinal);
        Assert.Contains(CaseWorkspaceLabels.Inspection.StoragePerDay, panel, StringComparison.Ordinal);
        // Read mode: the storage money reads; its control joins only the edit session.
        Assert.DoesNotContain("name=\"storagePerDay\"", panel, StringComparison.Ordinal);
        Assert.DoesNotContain(">Source<", panel, StringComparison.Ordinal);
        // The Principal's default is its own cell only where the Case holds
        // something else; here the Case holds a physical address of its own.
        Assert.Contains("data-inspection-provider-default hidden", panel, StringComparison.Ordinal);
        // A physical address says something the address itself does not, so
        // the mode still rides beside it.
        Assert.Contains("Physical address", panel, StringComparison.Ordinal);

        // The Principal's own setting, recorded on the Case unchanged: the
        // value carries its provenance word, no default cell is shown, and no
        // mode chip repeats the value.
        var imageBased = new RecordingCaseDetailsStore();
        imageBased.DataOverride = await InspectionOverrideAsync(imageBased, null);
        var imagePanel = InspectionPanel(await ReadCaseAsync(imageBased));
        var addressCell = AddressCell(imagePanel);
        Assert.Equal(1, Occurrences(addressCell, "Image Based Assessment"));
        Assert.Contains("data-provenance-word=\"Principal\"", addressCell, StringComparison.Ordinal);
        Assert.DoesNotContain("status--navy", addressCell, StringComparison.Ordinal);
        Assert.Contains("data-inspection-provider-default hidden", imagePanel, StringComparison.Ordinal);

        // The same Principal setting where staff recorded somewhere else: the
        // default is a fact the operator cannot read off the value.
        var corrected = new RecordingCaseDetailsStore();
        corrected.DataOverride = await InspectionOverrideAsync(corrected, "9 Other Road");
        var correctedPanel = InspectionPanel(await ReadCaseAsync(corrected));
        Assert.DoesNotContain("data-inspection-provider-default hidden", correctedPanel, StringComparison.Ordinal);
        Assert.Contains("data-inspection-provider-default", correctedPanel, StringComparison.Ordinal);
        Assert.Contains("Principal default", correctedPanel, StringComparison.Ordinal);
        Assert.Contains("9 Other Road", AddressCell(correctedPanel), StringComparison.Ordinal);
        Assert.Contains("Image Based Assessment", correctedPanel, StringComparison.Ordinal);
    }

    /// <summary>The Inspection section's recorded-address cell (v26 `[data-inspection-address]`).</summary>
    private static string AddressCell(string panel)
    {
        var start = panel.IndexOf("data-inspection-address>", StringComparison.Ordinal);
        Assert.True(start >= 0, "The inspection address cell must render.");
        var end = panel.IndexOf("data-inspection-provider-default", start, StringComparison.Ordinal);
        Assert.True(end > start, "The inspection address cell must end before the default cell.");
        return panel[start..end];
    }

    /// <summary>The Case as an operator who holds no edit lease reads it.</summary>
    private static async Task<string> ReadCaseAsync(RecordingCaseDetailsStore store)
    {
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

        return await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
    }

    /// <summary>
    /// The Case data of a Principal whose inspection setting is image-based:
    /// the address and the mode both reach the Case from that setting, and
    /// <paramref name="recordedAddress"/> is what staff recorded instead.
    /// </summary>
    private static async Task<CaseDataProjection> InspectionOverrideAsync(
        RecordingCaseDetailsStore store,
        string? recordedAddress)
    {
        var data = await store.GetAsync(store.CaseId, CancellationToken.None)
            ?? throw new InvalidOperationException("The case fixture returned no data.");
        var setting = new CaseDataSource(
            CaseDataSourceKind.ProviderSetting, "QDOS", "Principal setting", "provider-inspection", 1);
        var staff = new CaseDataSource(
            CaseDataSourceKind.StaffCorrection, "staff", "Staff correction", "case-edit", 1);
        return data with
        {
            Inspection = data.Inspection with
            {
                Address = new(
                    new("Image Based Assessment", CaseDataValueKind.Fact, setting),
                    null,
                    recordedAddress is null
                        ? null
                        : new(recordedAddress, CaseDataValueKind.Confirmed, staff)),
                Mode = new(
                    new(CaseInspectionMode.ImageBasedAssessment, CaseDataValueKind.Fact, setting),
                    null,
                    null)
            }
        };
    }

    /// <summary>The Inspection section's body, between its host and the next.</summary>
    private static string InspectionPanel(string html)
    {
        var start = html.IndexOf("id=\"section-inspection\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The Inspection section must render.");
        var end = html.IndexOf("id=\"section-vehicle\"", start, StringComparison.Ordinal);
        Assert.True(end > start, "The Inspection section must end before the Vehicle section.");
        return html[start..end];
    }

    /// <summary>The ribbon's actions cluster (v26), before the section row.</summary>
    private static string StickyActionRow(string html) => RecordBar(html);

    /// <summary>The record's Refresh control, with the section it reruns.</summary>
    private static string RefreshForm(string html)
    {
        var start = html.IndexOf("<form method=\"get\" data-refresh-form", StringComparison.Ordinal);
        Assert.True(start >= 0, "The record must offer Refresh.");
        var end = html.IndexOf("</form>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The Refresh form must close.");
        return html[start..end];
    }

    /// <summary>
    /// Files remains deferred while editing because it contributes no fields to
    /// the record's one Save form. The editable record sections still render
    /// together, so mounting Files cannot replace entered values.
    /// </summary>
    [Fact]
    public async Task HoldingTheEditLeaseDefersOnlyFilesAndKeepsTheSingleEditorComplete()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = await workspace.GetWorkspaceAsync();

        Assert.Equal(["files"], DeferredSections(html));
        Assert.Contains("id=\"section-files\"", html, StringComparison.Ordinal);
        Assert.Contains("section-placeholder", html, StringComparison.Ordinal);
        Assert.Equal(CaseSectionKeys, HostOrder(html));
        Assert.Equal(store.LeaseToken, InputValue(html, "editLeaseToken"));
        Assert.Equal(1, Occurrences(html, "id=\"case-edit-form\""));
        foreach (var field in new[]
        {
            "vehicleRegistration",
            "vehicleMake",
            "vehicleModel",
            "vehicleMileage",
            "inspectionAddress",
            "claimantName"
        })
        {
            Assert.Equal(1, Occurrences(html, $"name=\"{field}\""));
        }
    }

    /// <summary>
    /// A lazy Files request is an asynchronous read that can run beside a
    /// Claim, Save or release redirect. Without the page's render-only header,
    /// it does not restore edit state or reissue the cookie-backed TempData
    /// lease token.
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
    public async Task FocusedVehicleAndValuationReadsReuseOneDirectWorkspaceAndMatchLazyAssessmentProvenance()
    {
        var store = new RecordingCaseDetailsStore { ThrowOnBroadCaseRead = true };
        var assessment = new CaseAssessmentProjection(
            store.CaseId,
            "QDOS3100042",
            store.CaseVersion,
            store.State,
            null,
            [new(
                AssessmentVocabulary.VehicleFuel,
                "diesel",
                ActorKind.Automation,
                "vehicle-lookup",
                new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero),
                "vehicle-lookup",
                new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero))],
            [],
            new("AB12CDE", null, null, null, null, null, "tbc", null, null, null, null));
        store.FocusedAssessment = assessment;
        var assessmentWorkspace = new CountingAssessmentWorkspace(
            AssessmentWorkspaceTestData.Create(assessment));
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
                Substitute<IGetAssessmentAccess>(services, new FakeGetAssessmentAccess(canOpen: false));
                Substitute<IGetAssessmentWorkspace>(services, assessmentWorkspace);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var initial = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=vehicle");
        Assert.Equal(1, assessmentWorkspace.ReadCount);
        Assert.Single(store.VehicleSectionQueries);

        using (var claim = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(initial),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", InputValue(initial, "operationKey")))))
        {
            AssertPrg(claim, store.CaseId);
        }

        assessmentWorkspace.Reset();
        store.VehicleSectionQueries.Clear();
        store.VehicleSectionAssessments.Clear();
        var directVehicle = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=vehicle");
        var directVehicleQuery = Assert.Single(store.VehicleSectionQueries);
        Assert.Equal(1, assessmentWorkspace.ReadCount);
        Assert.True(directVehicleQuery.HasAssessmentWorkspace);
        Assert.Same(assessmentWorkspace.Workspace, directVehicleQuery.AssessmentWorkspace);
        Assert.Same(assessment, Assert.Single(store.VehicleSectionAssessments));
        Assert.Contains("Diesel", directVehicle, StringComparison.Ordinal);
        Assert.Contains("src-tag--lookup", directVehicle, StringComparison.Ordinal);

        var lazyVehicle = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}/Section?section=vehicle");
        var lazyVehicleQuery = store.VehicleSectionQueries.Last();
        Assert.Equal(1, assessmentWorkspace.ReadCount);
        Assert.False(lazyVehicleQuery.HasAssessmentWorkspace);
        Assert.Null(lazyVehicleQuery.AssessmentWorkspace);
        Assert.Same(assessment, store.VehicleSectionAssessments.Last());
        Assert.Contains("Diesel", lazyVehicle, StringComparison.Ordinal);
        Assert.Contains("src-tag--lookup", lazyVehicle, StringComparison.Ordinal);

        assessmentWorkspace.Reset();
        store.ValuationSectionQueries.Clear();
        store.ValuationSectionAssessments.Clear();
        await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=valuation");
        var directValuationQuery = Assert.Single(store.ValuationSectionQueries);
        Assert.Equal(1, assessmentWorkspace.ReadCount);
        Assert.True(directValuationQuery.HasAssessmentWorkspace);
        Assert.Same(assessmentWorkspace.Workspace, directValuationQuery.AssessmentWorkspace);
        Assert.Same(assessment, Assert.Single(store.ValuationSectionAssessments));

        await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}/Section?section=valuation");
        var lazyValuationQuery = store.ValuationSectionQueries.Last();
        Assert.Equal(1, assessmentWorkspace.ReadCount);
        Assert.False(lazyValuationQuery.HasAssessmentWorkspace);
        Assert.Null(lazyValuationQuery.AssessmentWorkspace);
        Assert.Same(assessment, store.ValuationSectionAssessments.Last());
    }

    /// <summary>
    /// WP7 moved report composition off the Files section entirely — the
    /// Report section is now the only place a report's image set is chosen —
    /// and moved image preparation's gate from assessment access to
    /// <c>CanEditCaseData</c> (lease held, state not PostReportComplete or
    /// Query, not archived). So a visit with assessment access denied but the
    /// Case lease held still sees the Images tab's tiles with their crop
    /// controls: assessment access no longer has a say in this surface.
    /// </summary>
    [Fact]
    public async Task TheLazyFilesFragmentOffersImagePreparationFromTheHeldLeaseNotAssessmentAccess()
    {
        var store = new PreparedImages().Store();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<ICaseAssetPreparationQueries>(services, store);
            services.RemoveAll<IGetAssessmentAccess>();
            services.AddSingleton<IGetAssessmentAccess>(new FakeGetAssessmentAccess(canOpen: false));
        });
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Cases/{store.CaseId:D}/Section?section=files");
        request.Headers.Add("X-Pegasus-Edit-Lease", store.LeaseToken);

        using var response = await workspace.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var fragment = await response.Content.ReadAsStringAsync();

        Assert.Contains("image-tile", fragment, StringComparison.Ordinal);
        Assert.Contains("data-preparation-crop", fragment, StringComparison.Ordinal);
        Assert.DoesNotContain("report-images", fragment, StringComparison.Ordinal);
    }

    /// <summary>
    /// PLAT-011: the case history table shows the resolved actor name, never the
    /// raw actor subject id (docs/design/README.md:168) — a Staff row shows its
    /// username and an Automation row shows the client label, not either GUID.
    /// </summary>
    [Fact]
    public async Task CaseHistoryShowsResolvedActorNamesAndNeverARawSubjectId()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var staffSubjectId = Guid.NewGuid().ToString("D");
        var automationSubjectId = Guid.NewGuid().ToString("D");
        var store = new RecordingCaseDetailsStore
        {
            HistoryEntries =
            [
                new(
                    "case_returned_to_review",
                    staffSubjectId,
                    nameof(ActorKind.Staff),
                    new(2031, 5, 6, 9, 0, 0, TimeSpan.Zero),
                    "Missing instructions.",
                    3,
                    4)
                {
                    ActorDisplayName = "alex"
                },
                new(
                    "case_created",
                    automationSubjectId,
                    nameof(ActorKind.Automation),
                    new(2031, 5, 5, 9, 0, 0, TimeSpan.Zero),
                    "Automated intake.",
                    0,
                    1)
                {
                    ActorDisplayName = "Automation"
                }
            ]
        };
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

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=notes");

        Assert.Contains("alex", html, StringComparison.Ordinal);
        Assert.Contains("Automation", html, StringComparison.Ordinal);
        Assert.DoesNotContain(staffSubjectId, html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(automationSubjectId, html, StringComparison.OrdinalIgnoreCase);
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

    [Theory]
    [InlineData(CaseLifecycleState.Review, PrincipalReportGenerationPolicy.EvaManualApi, "Send via API")]
    [InlineData(CaseLifecycleState.ReportPreparation, PrincipalReportGenerationPolicy.EvaManualApi, "Send via API")]
    [InlineData(CaseLifecycleState.Review, PrincipalReportGenerationPolicy.EvaZip, "Export EVA ZIP")]
    [InlineData(CaseLifecycleState.ReportPreparation, PrincipalReportGenerationPolicy.EvaZip, "Export EVA ZIP")]
    public async Task SendPageRendersItsChoiceInReviewAndWithEngineer(
        CaseLifecycleState state,
        PrincipalReportGenerationPolicy policy,
        string expectedAction)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore { CaseState = state, State = state };
        var evaStores = new StubEvaSubmissionStores(
            new EvaSubmissionModes(policy));
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<ICaseDataQueries>(services, store);
                Substitute<ICaseWorkflowQueries>(services, store);
                Substitute<IEvaSubmissionQueries>(services, evaStores);
                Substitute<IEvaSubmissionModeStore>(services, evaStores);
                // A composed transport is required for the manual API policy.
                Substitute<ISubmitCaseToEva>(services, new StubSubmitCaseToEva());
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        // EXT-04: the send page for a case still in Review — the one place the
        // operator gets the principal's configured EVA route.
        var html = await IntakeWebDriver.GetHtmlAsync(client, $"/Cases/{store.CaseId:D}/Eva/Send");

        // The page's own copy, as EPIC-011 restyled it: the handoff heading,
        // the case it is for, and its configured route out.
        Assert.Contains("<h1>EVA handoff</h1>", html, StringComparison.Ordinal);
        Assert.Contains(
            "<h2 id=\"eva-handoff-title\">QDOS3100042</h2>",
            html,
            StringComparison.Ordinal);
        Assert.Contains($"<span>{expectedAction}</span>", html, StringComparison.Ordinal);
        if (policy == PrincipalReportGenerationPolicy.EvaManualApi)
        {
            Assert.Contains($"/Cases/{store.CaseId:D}/Eva/Send", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Export EVA ZIP", html, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains($"/Cases/{store.CaseId:D}/Documents/Export", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Send via API", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ManualChasePostUsesAntiforgeryServerActorLiveLeaseVersionAndReplayKey()
    {
        // The attempt time is the server's clock at the post, never a value
        // the form carried (PR 670 port), so the host's clock is pinned here.
        var attemptedAtUtc = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        using var baseFactory = new IntakeWebApplicationFactory(
            new CaseWorkflowPersistenceTests.MutableTimeProvider(attemptedAtUtc));
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.RemoveAll<IRecordManualCaseChase>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<IRecordManualCaseChase>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var initialHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        var claimOperationKey = InputValue(initialHtml, "operationKey");
        using var claimResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(initialHtml),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", claimOperationKey)));
        AssertPrg(claimResponse, store.CaseId);
        Assert.Single(store.Claims);
        Assert.Equal(claimOperationKey, store.Claims[0].OperationKey);

        using (var recoveryClient = factory.CreateClient(new WebApplicationFactoryClientOptions
               {
                   AllowAutoRedirect = false,
                   BaseAddress = new Uri("https://localhost")
               }))
        {
            var recoveryHtml = await GetHtmlAsync(recoveryClient, $"/Cases/{store.CaseId:D}");
            // The holder returning to their own case in a second window gets
            // the one control, reading Take over (v26 § R): the claim replays
            // their retained lease, so nothing is "recovered".
            Assert.Contains("Take over", RecordBar(recoveryHtml), StringComparison.Ordinal);
            Assert.Contains("data-case-edit", RecordBar(recoveryHtml), StringComparison.Ordinal);
            Assert.Equal(claimOperationKey, InputValue(recoveryHtml, "operationKey"));
            using var recoveryResponse = await recoveryClient.PostAsync(
                $"/Cases/{store.CaseId:D}?handler=ClaimLease",
                Form(
                    AntiforgeryValue(recoveryHtml),
                    ("id", store.CaseId.ToString("D")),
                    ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                    ("operationKey", claimOperationKey)));
            AssertPrg(recoveryResponse, store.CaseId);
        }
        Assert.Equal(2, store.Claims.Count);
        Assert.Equal(store.Claims[0].OperationKey, store.Claims[1].OperationKey);

        var leasedHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=notes");
        var refreshedHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=notes");
        Assert.Equal(
            InputValue(leasedHtml, "editLeaseToken"),
            InputValue(refreshedHtml, "editLeaseToken"));
        leasedHtml = refreshedHtml;
        // The Record chase control lives on the Notes section and renders in
        // edit context while a chase is scheduled.
        Assert.Contains("Record chase", leasedHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"recipient\"", leasedHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"content\"", leasedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"attemptedAtUtc\"", leasedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"targetPartyOrAddress\"", leasedHtml, StringComparison.Ordinal);
        var chaseForm = ManualChaseMarkup(leasedHtml);
        Assert.DoesNotContain("name=\"reason\"", chaseForm, StringComparison.Ordinal);
        var operationKey = "manual-chase-replay";
        using var firstResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}/Tasks?handler=RecordManualChase",
            ManualChaseForm(AntiforgeryValue(leasedHtml), store, operationKey));
        AssertPrg(firstResponse, store.CaseId);

        var currentHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.DoesNotContain("name=\"editLeaseToken\"", currentHtml, StringComparison.Ordinal);
        using var replayResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}/Tasks?handler=RecordManualChase",
            ManualChaseForm(AntiforgeryValue(currentHtml), store, operationKey));
        AssertPrg(replayResponse, store.CaseId);

        Assert.Equal(2, store.ManualChases.Count);
        var command = store.ManualChases[0];
        var replay = store.ManualChases[1];
        Assert.Equal(command with { Actor = replay.Actor }, replay);
        Assert.Equal(command.Actor.Kind, replay.Actor.Kind);
        Assert.Equal(command.Actor.SubjectId, replay.Actor.SubjectId);
        Assert.Equal(command.Actor.Roles, replay.Actor.Roles);
        Assert.Equal(store.CaseId, command.CaseId);
        Assert.Equal(store.CaseVersion, command.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, command.EditLeaseToken);
        Assert.Equal(operationKey, command.OperationKey);
        Assert.Equal(ActorKind.Staff, command.Actor.Kind);
        Assert.Equal(attemptedAtUtc, command.AttemptedAtUtc);
        Assert.NotEmpty(command.Actor.Roles);
        Assert.Equal("Telephone", command.Channel);
        Assert.Equal("Provider claims team", command.TargetPartyOrAddress);
        Assert.Equal("Awaiting requested photographs", command.Outcome);
        Assert.Equal("Asked provider for missing images", command.Note);
    }

    [Fact]
    public async Task LifecyclePostsBindHoldReleaseAndNativeHandoffToAuthenticatedLease()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.RemoveAll<IHoldCase>();
                services.RemoveAll<IReleaseCase>();
                services.RemoveAll<ITransitionCase>();
                services.RemoveAll<IAssignCaseEngineer>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<IHoldCase>(store);
                services.AddSingleton<IReleaseCase>(store);
                services.AddSingleton<ITransitionCase>(store);
                services.AddSingleton<IAssignCaseEngineer>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var initialHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        using var claimResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(initialHtml),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", InputValue(initialHtml, "operationKey"))));
        AssertPrg(claimResponse, store.CaseId);

        var leasedHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        // The hold control is state-gated on the bar and its reason dialog
        // carries the lease envelope; the posts below exercise every route.
        Assert.Contains("Place on Hold", leasedHtml, StringComparison.Ordinal);
        Assert.Contains("data-dialog=\"case-hold-dialog\"", leasedHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"reason\"", leasedHtml, StringComparison.Ordinal);
        var antiforgeryToken = AntiforgeryValue(leasedHtml);
        using var holdResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}/Workflow?handler=Hold",
            LifecycleForm(antiforgeryToken, store, "hold-case", "Awaiting provider"));
        using var releaseResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}/Workflow?handler=ReleaseHold",
            LifecycleForm(antiforgeryToken, store, "release-case", "Provider replied"));
        var engineerId = Guid.NewGuid();
        using var handoffResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}/Workflow?handler=AssignEngineer",
            Form(antiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", "native-handoff"),
                ("editLeaseToken", store.LeaseToken),
                ("engineerId", engineerId.ToString("D")),
                ("instructionsComplete", "true"),
                ("imagesComplete", "true"),
                ("evidenceReference", "case-completeness-projection")));

        AssertPrg(holdResponse, store.CaseId);
        AssertPrg(releaseResponse, store.CaseId);
        AssertPrg(handoffResponse, store.CaseId);
        var actorSubjectId = Assert.Single(store.Claims).Actor.SubjectId;
        var hold = Assert.Single(store.Holds);
        var release = Assert.Single(store.Releases);
        var handoff = Assert.Single(store.EngineerAssignments);
        Assert.Equal(actorSubjectId, hold.Actor.SubjectId);
        Assert.Equal(actorSubjectId, release.Actor.SubjectId);
        Assert.Equal(actorSubjectId, handoff.Actor.SubjectId);
        Assert.Equal(store.CaseVersion, hold.ExpectedVersion);
        Assert.Equal(store.CaseVersion, release.ExpectedVersion);
        Assert.Equal(store.CaseVersion, handoff.ExpectedVersion);
        Assert.Equal(store.LeaseToken, hold.EditLeaseToken);
        Assert.Equal(store.LeaseToken, release.EditLeaseToken);
        Assert.Equal(store.LeaseToken, handoff.EditLeaseToken);
        Assert.Equal("hold-case", hold.OperationKey);
        Assert.Equal("release-case", release.OperationKey);
        Assert.Equal("native-handoff", handoff.OperationKey);
        Assert.Equal("Hand to Engineer", handoff.Reason);
        Assert.Equal(engineerId, handoff.EngineerId);
        Assert.Empty(store.Transitions);
    }

    [Fact]
    public async Task WrongHolderProjectionClearsProtectedLeaseAuthorityAndFallsBackToRecovery()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var initialHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        var claimOperationKey = InputValue(initialHtml, "operationKey");
        using var claimResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(initialHtml),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", claimOperationKey)));
        AssertPrg(claimResponse, store.CaseId);

        var claimant = Assert.Single(store.Claims).Actor.SubjectId;

        // A staff subject identifier is always a GUID; a holder that is not one is the Automation
        // Actor, so a staff holder has to be shaped like one here to test the staff disclosure.
        var otherStaffId = Guid.NewGuid().ToString("D");
        store.LeaseHolder = otherStaffId;
        var wrongHolderHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Contains(
            "Another member of staff is editing",
            wrongHolderHtml,
            StringComparison.Ordinal);
        Assert.DoesNotContain(otherStaffId, wrongHolderHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"editLeaseToken\"", wrongHolderHtml, StringComparison.Ordinal);

        store.LeaseHolder = claimant;
        var recoveryHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        // The one control, reading Take over for the holder's own other
        // window (v26 § R): the claim replays this holder's retained lease.
        Assert.Contains("Take over", RecordBar(recoveryHtml), StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"editLeaseToken\"", recoveryHtml, StringComparison.Ordinal);
        Assert.Equal(claimOperationKey, InputValue(recoveryHtml, "operationKey"));
    }

    private static FormUrlEncodedContent ManualChaseForm(
        string antiforgeryToken,
        RecordingCaseDetailsStore store,
        string operationKey) => Form(
            antiforgeryToken,
            ("id", store.CaseId.ToString("D")),
            ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
            ("operationKey", operationKey),
            ("editLeaseToken", store.LeaseToken),
            ("channel", "Telephone"),
            ("recipient", "Provider claims team"),
            ("outcome", "Awaiting requested photographs"),
            ("content", "Asked provider for missing images"));

    private static string ManualChaseMarkup(string html)
    {
        var start = html.IndexOf("handler=RecordManualChase", StringComparison.Ordinal);
        Assert.True(start >= 0, "The manual chase form is not rendered.");
        var end = html.IndexOf("</form>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The manual chase form is not closed.");
        return html[start..end];
    }

    /// <summary>
    /// What each reasoned lifecycle mutation posts from the leased workspace — the case id, its
    /// version, operation key, lease token, reason, and action-specific fields.
    /// </summary>
    private static FormUrlEncodedContent LifecycleForm(
        string antiforgeryToken,
        RecordingCaseDetailsStore store,
        string operationKey,
        string reason,
        params (string Name, string Value)[] fields) => Form(
            antiforgeryToken,
            [
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", operationKey),
                ("editLeaseToken", store.LeaseToken),
                ("reason", reason),
                .. fields
            ]);

    private static async Task<string> GetHtmlAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static FormUrlEncodedContent Form(
        string antiforgeryToken,
        params (string Name, string Value)[] values)
    {
        var fields = values.ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
        fields["__RequestVerificationToken"] = antiforgeryToken;
        return new(fields);
    }

    /// <summary>
    /// A form post that may repeat a field name, which a checkbox and its hidden false companion
    /// always do. <see cref="Form"/> cannot express that because it keys by name.
    /// </summary>
    private static FormUrlEncodedContent RepeatableForm(
        string antiforgeryToken,
        params (string Name, string Value)[] values) =>
        new(values
            .Select(item => KeyValuePair.Create(item.Name, item.Value))
            .Append(KeyValuePair.Create("__RequestVerificationToken", antiforgeryToken)));

    private static string InputValue(string html, string name)
    {
        var tag = Regex.Match(
            html,
            $"<input[^>]*name=\\\"{Regex.Escape(name)}\\\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(tag.Success, $"The case action must render '{name}'.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, $"The case field '{name}' must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    private static string AntiforgeryValue(string html)
    {
        var tag = AntiforgeryTagRegex().Match(html);
        Assert.True(tag.Success, "The case action must render an antiforgery token.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, "The case antiforgery token must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    /// <summary>
    /// The original CASE-027 regression dropped claimant contact/address.
    /// Keep its actual caller assertion when moving the single Save to the
    /// workspace command, whose submitted sections replace all their members.
    /// </summary>
    [Fact]
    public async Task ASaveCarriesTheClaimantContactNumberAndAddressThroughToTheCommand()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.RemoveAll<ISaveCaseWorkspace>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<ISaveCaseWorkspace>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var initialHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        using var saveResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            Form(
                AntiforgeryValue(initialHtml),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", DetailsModelOperationKey),
                ("editLeaseToken", store.LeaseToken),
                ("reason", "Corrected the registration"),
                ("claimantName", "Rebecca Claimant"),
                ("claimantContactNumber", "07700 900123"),
                ("claimantAddress", "12 Example Street, Leeds, LS1 1AA"),
                ("inspectionAddress", "7 No Script Road"),
                ("storageLocation", "14 Storage Lane")));
        AssertPrg(saveResponse, store.CaseId);

        var saved = Assert.Single(store.Saves);
        Assert.Equal("07700 900123", saved.Overview!.ClaimantContactNumber);
        Assert.Equal("12 Example Street, Leeds, LS1 1AA", saved.Overview!.ClaimantAddress);
        Assert.Equal("14 Storage Lane", saved.Inspection!.StorageLocation);
        Assert.Equal("7 No Script Road", saved.Inspection!.Address);
        Assert.Equal(CaseReportAddressTreatment.PhysicalVehicleLocation, saved.Inspection!.AddressTreatment);

        // A submitted section still contains the other accepted members.
        Assert.Equal("Rebecca Claimant", saved.Overview!.ClaimantName);
        Assert.Equal("CLM-42", saved.Overview.ClaimNumber);
        Assert.Equal("Case contact", saved.Overview.ContactName);
        Assert.Null(saved.Vehicle);
    }

    [Fact]
    public async Task ASaveCarriesTheDamageWorkbenchFieldsThroughToTheWorkspaceCommand()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            CaseState = CaseLifecycleState.ReportPreparation
        };
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.RemoveAll<ISaveCaseWorkspace>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<ISaveCaseWorkspace>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var initialHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        using var saveResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            Form(
                AntiforgeryValue(initialHtml),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", DetailsModelOperationKey),
                ("editLeaseToken", store.LeaseToken),
                ("reason", "Recorded damage observations"),
                ("damageImpacts", "[{\"zone\":\"front\",\"severity\":\"light\",\"note\":\"Scuffed\"}]"),
                (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.DamageTyreRightFront), "damaged"),
                (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.DamageBeltLeftRear), "deployed"),
                (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.DamageUnrelated), "Old rear bumper scrape"),
                (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.DamageUnrelatedDeduction), "125.50"),
                (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.DamageMaterialTransfer), "White paint transfer")));
        AssertPrg(saveResponse, store.CaseId);

        var damage = Assert.Single(store.Saves).Damage;
        Assert.NotNull(damage);
        Assert.Equal(new AssessmentImpact("front", "light", "Scuffed"), Assert.Single(damage.Impacts!));
        Assert.Equal("damaged", damage.AssessmentFields![AssessmentVocabulary.DamageTyreRightFront]);
        Assert.Equal("deployed", damage.AssessmentFields[AssessmentVocabulary.DamageBeltLeftRear]);
        Assert.Equal("Old rear bumper scrape", damage.AssessmentFields[AssessmentVocabulary.DamageUnrelated]);
        Assert.Equal("125.50", damage.AssessmentFields[AssessmentVocabulary.DamageUnrelatedDeduction]);
        Assert.Equal("White paint transfer", damage.AssessmentFields[AssessmentVocabulary.DamageMaterialTransfer]);
    }

    [Fact]
    public async Task ARefusedSaveKeepsTheProposedValuesForComparisonAndOffersNoApplyControl()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.RemoveAll<ISaveCaseWorkspace>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<ISaveCaseWorkspace>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var initialHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        using var saveResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            Form(
                AntiforgeryValue(initialHtml),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", DetailsModelOperationKey),
                ("editLeaseToken", store.LeaseToken),
                ("reason", "Corrected claimant spelling"),
                ("claimantName", "Rebecca Proposed"),
                ("claimNumber", "CLM-99")));
        AssertPrg(saveResponse, store.CaseId);
        Assert.Single(store.Saves);

        var refusedHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");

        Assert.Contains("Your change was not applied", refusedHtml, StringComparison.Ordinal);
        Assert.Contains("You proposed", refusedHtml, StringComparison.Ordinal);
        Assert.Contains("The case now holds", refusedHtml, StringComparison.Ordinal);
        Assert.Contains("Rebecca Proposed", refusedHtml, StringComparison.Ordinal);
        Assert.Contains("CLM-99", refusedHtml, StringComparison.Ordinal);
        Assert.Contains("Corrected claimant spelling", refusedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(store.LeaseToken, refusedHtml, StringComparison.Ordinal);

        // Structural, not phrase-matching: the comparison panel is a table of values with no way
        // to put them back. Any form, button, or input inside it would be an apply/force path.
        var panel = ProposedValuesPanel(refusedHtml);
        Assert.DoesNotContain("<form", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<button", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<input", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<textarea", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<select", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("handler=", panel, StringComparison.OrdinalIgnoreCase);

        var clearedHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.DoesNotContain("Your change was not applied", clearedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AutomaticReadinessRendersWithoutManualCompletenessControls()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.Review,
            CaseState = CaseLifecycleState.Review
        };
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");

        Assert.Contains("Case workflow", html, StringComparison.Ordinal);
        Assert.Contains("Review", html, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=ConfirmCompleteness", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Confirm completeness", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"instructionComplete\"", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A stale-version refusal is not lease loss, but the requirement still makes the rejected editor
    /// "reload and reacquire rather than merge or force the save", so the edit forms must not come
    /// back under the same edit authority.
    /// </summary>
    [Fact]
    public async Task AStaleVersionRefusalRequiresEditModeToBeEnteredAgain()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.RemoveAll<ISaveCaseWorkspace>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<ISaveCaseWorkspace>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var initialHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        using (var claimResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(initialHtml),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", InputValue(initialHtml, "operationKey")))))
        {
            AssertPrg(claimResponse, store.CaseId);
        }

        // Edit mode is genuinely active before the refusal, or the assertion below proves nothing.
        var editingHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Contains("name=\"editLeaseToken\"", editingHtml, StringComparison.Ordinal);

        using (var saveResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            Form(
                AntiforgeryValue(editingHtml),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", DetailsModelOperationKey),
                ("editLeaseToken", store.LeaseToken),
                ("reason", "Corrected claimant spelling"),
                ("claimantName", "Rebecca Proposed"))))
        {
            AssertPrg(saveResponse, store.CaseId);
        }

        var refusedHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");

        Assert.Contains("Your change was not applied", refusedHtml, StringComparison.Ordinal);
        Assert.Contains("Rebecca Proposed", refusedHtml, StringComparison.Ordinal);

        // The authority is still this editor's on the server, so recovery is offered rather than
        // the case being handed to anyone else — but no edit form is live until it is retaken.
        // v26 names that one control Take over (the holder's own lease, not this browser's).
        Assert.DoesNotContain("name=\"editLeaseToken\"", refusedHtml, StringComparison.Ordinal);
        Assert.Contains("Take over", RecordBar(refusedHtml), StringComparison.Ordinal);
        Assert.Contains("handler=ClaimLease", refusedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARefusalOnOneCaseSurvivesAVisitToAnother()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<ISaveCaseWorkspace>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<ISaveCaseWorkspace>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var initialHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        using var saveResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            Form(
                AntiforgeryValue(initialHtml),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", DetailsModelOperationKey),
                ("editLeaseToken", store.LeaseToken),
                ("reason", "Corrected claimant spelling"),
                ("claimantName", "Rebecca Proposed")));
        AssertPrg(saveResponse, store.CaseId);

        // Another case is visited before the refused editor returns to theirs.
        using var otherCaseResponse = await client.GetAsync($"/Cases/{Guid.NewGuid():D}");
        Assert.Equal(HttpStatusCode.OK, otherCaseResponse.StatusCode);

        var refusedHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Contains("Your change was not applied", refusedHtml, StringComparison.Ordinal);
        Assert.Contains("Rebecca Proposed", refusedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefusedRetentionKeepsEditorialValuesAndNeverIdentifiersOrRoutingFields()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<ISaveCaseWorkspace>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<ISaveCaseWorkspace>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var taskId = Guid.NewGuid();
        var initialHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        // A long circumstances value is kept far past the old 300-character trim, and the
        // identifier and routing fields posted alongside it are never retained.
        var circumstances = new string('c', 1500);
        using var saveResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            Form(
                AntiforgeryValue(initialHtml),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", DetailsModelOperationKey),
                ("editLeaseToken", store.LeaseToken),
                ("reason", "Long circumstances"),
                ("accidentCircumstances", circumstances),
                ("taskId", taskId.ToString("D")),
                ("actionName", "release"),
                ("destination", "Review")));
        AssertPrg(saveResponse, store.CaseId);

        var refusedHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        var panel = ProposedValuesPanel(refusedHtml);

        Assert.Contains(circumstances, panel, StringComparison.Ordinal);
        Assert.DoesNotContain("Some values were too long", panel, StringComparison.Ordinal);
        Assert.DoesNotContain(taskId.ToString("D"), panel, StringComparison.Ordinal);
        Assert.DoesNotContain("Task id", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Action name", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("release", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Destination", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(GuidRegex(), VisibleText(panel));
    }

    [Fact]
    public async Task ARetainedValueTooLongToKeepIsReportedRatherThanTrimmedQuietly()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<ISaveCaseWorkspace>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<ISaveCaseWorkspace>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var initialHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        using var saveResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            Form(
                AntiforgeryValue(initialHtml),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", DetailsModelOperationKey),
                ("editLeaseToken", store.LeaseToken),
                ("accidentCircumstances", new string('c', 2500))));
        AssertPrg(saveResponse, store.CaseId);

        var refusedHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");

        Assert.Contains("Some values were too long to keep in full", refusedHtml, StringComparison.Ordinal);
        Assert.Contains("re-enter those in full", refusedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANonHolderSeesTheEditingStaffAccountByNameAndNeverItsIdentifier()
    {
        var holderId = Guid.Parse("0d3b5a41-6f3f-4a1e-9f0b-2c5d7e8a9b01");
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore { LeaseHolder = holderId.ToString("D") };
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IDescribeCaseEditAuthorityHolder>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IDescribeCaseEditAuthorityHolder>(
                    new StubEditAuthorityHolders("r.hughes"));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        var note = EditAuthorityNote(html);

        Assert.Contains("r.hughes is editing", note, StringComparison.Ordinal);
        // CASE-024: an open editor keeps its own lease alive, so no moment when editing
        // becomes available is knowable here, and naming one would be a broken promise.
        Assert.DoesNotContain("Editing becomes available", note, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=ClaimLease", html, StringComparison.Ordinal);
        Assert.DoesNotContain(holderId.ToString("D"), html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(GuidRegex(), VisibleText(note));
    }

    [Fact]
    public async Task AnUnresolvableHolderIsStillDisclosedWithoutAnIdentifier()
    {
        var holderId = Guid.Parse("0d3b5a41-6f3f-4a1e-9f0b-2c5d7e8a9b01");
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore { LeaseHolder = holderId.ToString("D") };
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IDescribeCaseEditAuthorityHolder>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IDescribeCaseEditAuthorityHolder>(
                    new StubEditAuthorityHolders(displayName: null));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        var note = EditAuthorityNote(html);

        Assert.Contains(
            "Another member of staff is editing",
            note,
            StringComparison.Ordinal);
        // CASE-024: an open editor keeps its own lease alive, so no moment when editing
        // becomes available is knowable here, and naming one would be a broken promise.
        Assert.DoesNotContain("Editing becomes available", note, StringComparison.Ordinal);
        Assert.DoesNotContain(holderId.ToString("D"), html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(GuidRegex(), VisibleText(note));
    }

    /// <summary>
    /// ADR-0011 requires the Automation Actor to stay attributable without impersonating staff, so a
    /// case it holds must never be reported as held by a member of staff.
    /// </summary>
    [Fact]
    public async Task AnAutomationHolderIsNamedAsAiAndNeverAsAMemberOfStaff()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore
        {
            LeaseHolder = "pegasus-automation",
            LeaseHolderKind = ActorKind.Automation
        };
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IDescribeCaseEditAuthorityHolder>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IDescribeCaseEditAuthorityHolder>(
                    new StubEditAuthorityHolders(displayName: null, isAutomation: true));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        var note = EditAuthorityNote(html);

        Assert.Contains("AI is editing", note, StringComparison.Ordinal);
        // CASE-024: an open editor keeps its own lease alive, so no moment when editing
        // becomes available is knowable here, and naming one would be a broken promise.
        Assert.DoesNotContain("Editing becomes available", note, StringComparison.Ordinal);
        Assert.DoesNotContain("member of staff", note, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pegasus-automation", html, StringComparison.OrdinalIgnoreCase);
        AssertNoBannedVocabulary(note);
    }

    [Fact]
    public async Task EditModeCopyAvoidsBannedOperatorVocabularyInEveryState()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.RemoveAll<ISaveCaseWorkspace>();
                services.RemoveAll<IDescribeCaseEditAuthorityHolder>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<ISaveCaseWorkspace>(store);
                services.AddSingleton<IDescribeCaseEditAuthorityHolder>(
                    new StubEditAuthorityHolders("r.hughes"));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        // Available: nobody is editing.
        AssertNoBannedVocabulary(RecordBar(await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}")));

        // Holder: this staff member is editing, with the edit forms rendered.
        var availableHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        using (var claimResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(availableHtml),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", InputValue(availableHtml, "operationKey")))))
        {
            AssertPrg(claimResponse, store.CaseId);
        }

        var holderHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        var holderRecord = RecordBar(holderHtml);
        Assert.Contains("id=\"case-finish-editing-form\"", holderRecord, StringComparison.Ordinal);
        Assert.Contains("form=\"case-finish-editing-form\"", holderRecord, StringComparison.Ordinal);
        Assert.Contains("form=\"case-edit-form\"", holderRecord, StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(holderRecord, ">Cancel</span>"));
        Assert.Equal(1, Occurrences(holderRecord, ">Save</span>"));
        // WP6: both ways out of an edit are on the one action row.
        Assert.Equal(1, Occurrences(StickyActionRow(holderHtml), ">Cancel</span>"));
        Assert.Equal(1, Occurrences(StickyActionRow(holderHtml), ">Save</span>"));
        Assert.DoesNotContain("Finish editing", holderRecord, StringComparison.Ordinal);
        Assert.DoesNotContain("Save case data", holderRecord, StringComparison.Ordinal);
        AssertNoBannedVocabulary(holderRecord);

        // Recover: the same holder without the protected browser state.
        using (var recoveryClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        }))
        {
            var recoverHtml = await GetHtmlAsync(recoveryClient, $"/Cases/{store.CaseId:D}");
            Assert.Contains("Take over", RecordBar(recoverHtml), StringComparison.Ordinal);
            AssertNoBannedVocabulary(RecordBar(recoverHtml));
        }

        // Non-holder: someone else is editing.
        store.LeaseHolder = Guid.NewGuid().ToString("D");
        using (var otherClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        }))
        {
            var nonHolderHtml = await GetHtmlAsync(otherClient, $"/Cases/{store.CaseId:D}");
            Assert.Contains(
                "r.hughes is editing",
                EditAuthorityNote(nonHolderHtml),
                StringComparison.Ordinal);
            AssertNoBannedVocabulary(EditAuthorityNote(nonHolderHtml));
        }
    }

    /// <summary>
    /// The vocabulary `docs/ui-work/ui-standards-and-review.md` bans from operator copy, including
    /// identifiers. Applied to this feature's own panels; GUID debt elsewhere on the case page
    /// (the Engineer field) predates this work and belongs to the queued case-container rework.
    /// </summary>
    private static void AssertNoBannedVocabulary(string sectionHtml)
    {
        var visible = VisibleText(sectionHtml);
        foreach (var banned in new[]
        {
            "lease", "opaque", "token", "expiry", "projection", "ingress", "bounded",
            "artifact", "durable", "aggregate", "caller", "composed", "composition",
            "bytes", "hash", "operation key", "correlation"
        })
        {
            Assert.DoesNotContain(banned, visible, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotMatch(GuidRegex(), visible);
    }

    private const string DetailsModelOperationKey = "3f2504e04f8911d39a0c0305e82c3301";

    /// <summary>
    /// v26: the record's actions live in the ribbon's own actions cluster —
    /// the primary (Edit Case / Cancel + Save) and the one Actions menu —
    /// which the section row closes. Nothing below the sticky block is an
    /// action of the record itself.
    /// </summary>
    private static string RecordBar(string html)
    {
        var start = html.IndexOf("data-case-ribbon-actions", StringComparison.Ordinal);
        Assert.True(start >= 0, "The record bar is not rendered.");
        var end = html.IndexOf("class=\"section-row\"", start, StringComparison.Ordinal);
        Assert.True(end > start, "The record bar is not closed before the section row.");
        return html[start..end];
    }

    /// <summary>
    /// The ten Case sections in the order D30 fixes, from the frame's one
    /// section list.
    /// </summary>
    private static readonly string[] CaseSectionKeys =
        [.. Pegasus.Web.Presentation.OperatorLabels.CaseWorkspace.Sections
            .Select(section => section.Key)];

    /// <summary>The hosts the first response leaves for the frame to fetch.</summary>
    private static string[] DeferredSections(string html) =>
        [.. DeferredSectionRegex().Matches(html).Select(match => match.Groups[1].Value)];

    /// <summary>The record's section hosts, in the order they render.</summary>
    private static string[] HostOrder(string html) =>
        [.. SectionHostRegex().Matches(html).Select(match => match.Groups[1].Value)];

    /// <summary>The jump-nav's links, in the order they render.</summary>
    private static string[] JumpLinkOrder(string html) =>
        [.. JumpLinkRegex().Matches(JumpNav(html)).Select(match => match.Groups[1].Value)];

    /// <summary>
    /// The key of the jump-nav entry marked current. Scoped to the jump-nav so
    /// the shell rail's own current link cannot answer for it.
    /// </summary>
    private static string CurrentSectionKey(string html)
    {
        var current = CurrentSectionRegex().Match(JumpNav(html));
        Assert.True(current.Success, "No section is marked current.");
        return current.Groups[1].Value;
    }

    private static string JumpNav(string html)
    {
        var marker = html.IndexOf("data-section-nav", StringComparison.Ordinal);
        Assert.True(marker >= 0, "The section jump-nav is not rendered.");
        var start = html.LastIndexOf("<nav", marker, StringComparison.Ordinal);
        Assert.True(start >= 0, "The section jump-nav is not a nav element.");
        var end = html.IndexOf("</nav>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The jump-nav is not closed.");
        return html[start..end];
    }

    private static int Occurrences(string html, string value) =>
        html.Split(value, StringSplitOptions.None).Length - 1;

    [GeneratedRegex(
        "<section class=\"record-section[^\"]*\" id=\"section-([a-z-]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex SectionHostRegex();

    [GeneratedRegex(
        "data-section-link=\"([a-z-]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex JumpLinkRegex();

    [GeneratedRegex(
        "data-lazy=\"([a-z-]+)\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex DeferredSectionRegex();

    [GeneratedRegex(
        "data-section-link=\"([a-z-]+)\"\\s+aria-current=\"true\"",
        RegexOptions.CultureInvariant)]
    private static partial Regex CurrentSectionRegex();

    private static string EditAuthorityNote(string html)
    {
        var start = html.IndexOf("data-edit-authority", StringComparison.Ordinal);
        Assert.True(start >= 0, "The edit-authority note is not rendered.");
        var end = html.IndexOf("</span>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The edit-authority note is not closed.");
        return html[start..end];
    }

    /// <summary>
    /// v26: the refused editor's proposed values are a sub-panel of Overview
    /// (`[data-proposed-values]`) rather than a section of their own.
    /// </summary>
    private static string ProposedValuesPanel(string html)
    {
        var start = html.IndexOf("data-proposed-values", StringComparison.Ordinal);
        Assert.True(start >= 0, "The proposed-values panel is not rendered.");
        var end = html.IndexOf("</table>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The proposed-values panel is not closed.");
        return html[start..end];
    }

    /// <summary>The Files section's upload-requests sub-panel (v26): the table of links.</summary>
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

    private static string Section(string html, string labelledBy)
    {
        var start = html.IndexOf($"aria-labelledby=\"{labelledBy}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"The '{labelledBy}' section is not rendered.");
        var open = html.LastIndexOf("<section", start, StringComparison.Ordinal);
        var end = html.IndexOf("</section>", start, StringComparison.Ordinal);
        Assert.True(end > open, $"The '{labelledBy}' section is not closed.");
        return html[open..(end + "</section>".Length)];
    }

    /// <summary>
    /// Operator copy only: markup, attribute values, and script are removed so the banned-vocabulary
    /// assertion reads what a member of staff reads.
    /// </summary>
    private static string VisibleText(string html) =>
        MarkupRegex().Replace(html, " ");

    [GeneratedRegex("<(script|style)[^>]*>.*?</\\1>|<[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex MarkupRegex();

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.CultureInvariant)]
    private static partial Regex GuidRegex();

    /// <summary>
    /// KANMER-005: while the Automation Actor holds the lease, the workspace is read-only to
    /// staff — the holder is disclosed from its retained kind through the real descriptor, no
    /// claim control is rendered, and a claim posted anyway is refused without the page
    /// pretending edit mode was entered. The refusal is the shared owner's own conflict.
    /// </summary>
    [Fact]
    public async Task AnAutomationHeldCaseIsReadOnlyToStaffAndAPostedClaimIsRefused()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore
        {
            LeaseHolder = "pegasus-automation",
            LeaseHolderKind = ActorKind.Automation
        };
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Contains("AI is editing", EditAuthorityNote(html), StringComparison.Ordinal);
        Assert.DoesNotContain("handler=ClaimLease", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Edit case<", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"editLeaseToken\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("pegasus-automation", html, StringComparison.OrdinalIgnoreCase);

        store.NextFailure = new CaseEditLeaseConflictException(store.CaseId, store.CaseVersion);
        using var claimResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(html),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", Guid.NewGuid().ToString("N"))));
        AssertPrg(claimResponse, store.CaseId);

        Assert.Empty(store.Claims);
        Assert.Equal("pegasus-automation", store.LeaseHolder);
        Assert.Equal(ActorKind.Automation, store.LeaseHolderKind);
        var afterRefusal = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Contains("This case is already being edited.", afterRefusal, StringComparison.Ordinal);
        Assert.Contains("AI is editing", EditAuthorityNote(afterRefusal), StringComparison.Ordinal);
        Assert.DoesNotContain("handler=ClaimLease", afterRefusal, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"editLeaseToken\"", afterRefusal, StringComparison.Ordinal);
    }

    /// <summary>
    /// Review point 12: the adverse disposition is not a step in the workflow.
    /// It stands alone at the end of the record bar, away from the progression
    /// actions, and offers only the outcomes Core will actually accept from
    /// the Case's current state. Created in error and E-mail unlinked each
    /// have their own action, so neither is ever in the chooser, and post-report
    /// completion is progression rather than an adverse disposition.
    /// </summary>
    [Theory]
    [InlineData(CaseLifecycleState.NotReady, true)]
    [InlineData(CaseLifecycleState.Review, true)]
    [InlineData(CaseLifecycleState.ReportPreparation, true)]
    [InlineData(CaseLifecycleState.PostReport, true)]
    [InlineData(CaseLifecycleState.PostReportComplete, true)]
    [InlineData(CaseLifecycleState.Query, true)]
    [InlineData(CaseLifecycleState.ProviderCancelled, false)]
    [InlineData(CaseLifecycleState.CollisionEngineersRejected, false)]
    [InlineData(CaseLifecycleState.CreatedInError, false)]
    [InlineData(CaseLifecycleState.SourceEmailUnlinked, false)]
    public async Task TheAdverseCloseActionStandsApartAndOffersOnlyThePermittedOutcomes(
        CaseLifecycleState state,
        bool offersClosure)
    {
        var store = new RecordingCaseDetailsStore { State = state };
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ICloseCase>(services, new CoreCheckedCloseCase(store)));

        var html = await workspace.GetWorkspaceAsync();
        var bar = RecordBar(html);

        // v26 (decision C2): Close case is the last item of the one Actions
        // menu, in red below a divider.
        Assert.Equal(offersClosure, bar.Contains("data-dialog-open=\"case-close-dialog\"", StringComparison.Ordinal));
        Assert.Equal(
            offersClosure,
            html.Contains("data-dialog=\"case-close-dialog\"", StringComparison.Ordinal));
        if (!offersClosure)
        {
            // A closed Case is read: it keeps its reference and reads its
            // recorded outcome, and offers no way to close it a second time.
            Assert.DoesNotContain("data-dialog-open=\"case-close-dialog\"", html, StringComparison.Ordinal);
            Assert.Contains("QDOS3100042", html, StringComparison.Ordinal);
            Assert.Contains(EncodedStage(state), html, StringComparison.Ordinal);
            return;
        }

        // Separation: below the menu's last divider there is the one Close
        // control, in the danger style, and none of the progression actions.
        var adverse = AdverseGroup(html);
        Assert.Contains("data-dialog-open=\"case-close-dialog\"", adverse, StringComparison.Ordinal);
        Assert.Contains("btn--danger", adverse, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.CaseWorkspace.CloseCase, adverse, StringComparison.Ordinal);
        foreach (var progression in new[] { "Hand to Engineer", "Mark completed", "Return to Engineer", "Place on Hold", "Correct principal" })
        {
            Assert.DoesNotContain(progression, adverse, StringComparison.Ordinal);
        }

        var dialog = CloseDialog(html);
        Assert.Contains(
            $"/Cases/{store.CaseId:D}/Closure?handler=Close",
            dialog,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"ProviderCancelled\"", dialog, StringComparison.Ordinal);
        Assert.Contains("value=\"CollisionEngineersRejected\"", dialog, StringComparison.Ordinal);
        foreach (var unavailable in new[] { "CreatedInError", "SourceEmailUnlinked", "PostReportComplete" })
        {
            Assert.DoesNotContain($"value=\"{unavailable}\"", dialog, StringComparison.Ordinal);
        }

        Assert.Matches("<select[^>]*name=\"outcome\"[^>]*required", dialog);
        Assert.Matches("<textarea[^>]*name=\"reason\"[^>]*rows=\"3\"", dialog);
        Assert.Contains("name=\"reason\"", dialog, StringComparison.Ordinal);
        Assert.Contains("required", dialog, StringComparison.Ordinal);
        Assert.Contains($"value=\"{store.LeaseToken}\"", dialog, StringComparison.Ordinal);
        Assert.Contains(
            $"value=\"{store.CaseVersion.ToString(CultureInfo.InvariantCulture)}\"",
            dialog,
            StringComparison.Ordinal);
        // Closing is never deletion: the workspace offers no such action.
        Assert.DoesNotContain("Delete case", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("handler=DeleteCase", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The progression actions stay where they were: the adverse group is an
    /// addition beside them, not a replacement for them.
    /// </summary>
    [Fact]
    public async Task TheCloseActionDoesNotDisplaceTheProgressionActions()
    {
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.PostReport };
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ICloseCase>(services, new CoreCheckedCloseCase(store)));

        var bar = RecordBar(await workspace.GetWorkspaceAsync());
        var adverseStart = bar.IndexOf("data-dialog-open=\"case-close-dialog\"", StringComparison.Ordinal);

        Assert.Contains("Mark completed", bar, StringComparison.Ordinal);
        Assert.Contains("Place on Hold", bar, StringComparison.Ordinal);
        Assert.True(adverseStart > 0, "The Close item is not rendered.");
        // The progression items and the hold group are drawn before the
        // divider that sets Close apart at the end of the menu.
        Assert.InRange(bar.IndexOf("Mark completed", StringComparison.Ordinal), 0, adverseStart);
        Assert.InRange(bar.IndexOf("Place on Hold", StringComparison.Ordinal), 0, adverseStart);
        Assert.InRange(bar.LastIndexOf("menu-sep", StringComparison.Ordinal), 0, adverseStart);
    }

    /// <summary>
    /// A complete disposition reaches Core with the workspace's own reasoned
    /// envelope — actor, version, lease, operation key and reason — carrying
    /// the chosen outcome, and the Case is still there afterwards, reading the
    /// outcome it was closed with. Closing never deletes a Case.
    /// </summary>
    [Fact]
    public async Task ClosingRecordsTheChosenAdverseOutcomeAndKeepsTheCase()
    {
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.Review };
        var closeCase = new CoreCheckedCloseCase(store);
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ICloseCase>(services, closeCase));

        using var closed = await workspace.PostAsync(
            "Closure?handler=Close",
            workspace.MutationForm(
                "close-provider-cancelled",
                "Provider withdrew the instruction",
                ("outcome", "ProviderCancelled")));

        AssertPrg(closed, store.CaseId);
        var closure = Assert.Single(closeCase.Closures);
        AssertLeasedMutation(workspace, closure, "close-provider-cancelled", "Provider withdrew the instruction");
        Assert.Equal(CaseClosureOutcome.ProviderCancelled, closure.Outcome);

        // The recorded transition, as the projection then reports it.
        store.State = CaseLifecycleState.ProviderCancelled;
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains("The selected terminal outcome was recorded.", html, StringComparison.Ordinal);
        Assert.Contains("QDOS3100042", html, StringComparison.Ordinal);
        Assert.Contains(
            EncodedStage(CaseLifecycleState.ProviderCancelled),
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain("data-dialog-open=\"case-close-dialog\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The chooser and the reason are both required, and an outcome the Case's
    /// current state does not permit is not a closure. Every refusal stops
    /// before Core's store, keeps this browser in edit mode, and hands the
    /// operator back what they submitted.
    /// </summary>
    [Theory]
    // The chooser was not answered, or was answered with something that is not
    // one of the named outcomes: neither may fall through to the enum default.
    [InlineData(CaseLifecycleState.Review, null, "Provider withdrew the instruction")]
    [InlineData(CaseLifecycleState.Review, "", "Provider withdrew the instruction")]
    [InlineData(CaseLifecycleState.Review, "99", "Provider withdrew the instruction")]
    // Each of these is reached through its own action, never through Close.
    [InlineData(CaseLifecycleState.Review, "CreatedInError", "The principal was wrong")]
    [InlineData(CaseLifecycleState.Review, "SourceEmailUnlinked", "The e-mail was unlinked")]
    // Progression, and not from this state.
    [InlineData(CaseLifecycleState.Review, "PostReportComplete", "Post-report work is done")]
    // A closed Case cannot be closed again.
    [InlineData(CaseLifecycleState.ProviderCancelled, "CollisionEngineersRejected", "Rejected after all")]
    // The reason is required.
    [InlineData(CaseLifecycleState.Review, "ProviderCancelled", "")]
    [InlineData(CaseLifecycleState.Review, "ProviderCancelled", "   ")]
    public async Task AnIncompleteOrUnavailableClosureNeverReachesTheCommand(
        CaseLifecycleState state,
        string? outcome,
        string reason)
    {
        var store = new RecordingCaseDetailsStore { State = state };
        var closeCase = new CoreCheckedCloseCase(store);
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ICloseCase>(services, closeCase));
        var fields = new List<(string Name, string Value)>
        {
            ("id", store.CaseId.ToString("D")),
            ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
            ("operationKey", "close-refused"),
            ("editLeaseToken", store.LeaseToken),
            ("reason", reason)
        };
        if (outcome is not null)
        {
            fields.Add(("outcome", outcome));
        }

        using var refused = await workspace.PostAsync(
            "Closure?handler=Close",
            Form(workspace.AntiforgeryToken, [.. fields]));

        AssertPrg(refused, store.CaseId);
        Assert.Empty(closeCase.Closures);
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
        // Edit mode survives, and what was submitted comes back with it.
        Assert.Equal(store.LeaseToken, InputValue(html, "editLeaseToken"));
        Assert.Contains("Your change was not applied", html, StringComparison.Ordinal);
        // The Case itself is untouched and still readable.
        Assert.Contains("QDOS3100042", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A stage name as the response actually carries it: the framework's HTML
    /// encoder writes the chip's separator as a numeric reference.
    /// </summary>
    private static string EncodedStage(CaseLifecycleState state) =>
        System.Text.Encodings.Web.HtmlEncoder.Default.Encode(OperatorLabels.CaseStage(state));

    /// <summary>The Actions menu's tail below its last divider (v26): the Close item alone.</summary>
    private static string AdverseGroup(string html)
    {
        var bar = RecordBar(html);
        var menuEnd = bar.IndexOf("</details>", StringComparison.Ordinal);
        Assert.True(menuEnd > 0, "The Actions menu is not rendered.");
        var start = bar.LastIndexOf("class=\"menu-sep\"", menuEnd, StringComparison.Ordinal);
        Assert.True(start >= 0, "The adverse divider is not rendered.");
        return bar[start..menuEnd];
    }

    private static string CloseDialog(string html)
    {
        var start = html.IndexOf("data-dialog=\"case-close-dialog\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The close dialog is not rendered.");
        var end = html.IndexOf("</section>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The close dialog is not closed.");
        return html[start..end];
    }

    /// <summary>
    /// Stands in for Core's <c>CloseCase</c> over the recording projection: it
    /// applies exactly the rules that use case applies, in its order, and
    /// replaces only the persistence. A closure the real command would refuse
    /// is refused here too, so the page's own refusal path is exercised
    /// against the real policy rather than against a permissive double.
    /// </summary>
    private sealed class CoreCheckedCloseCase(RecordingCaseDetailsStore store) : ICloseCase
    {
        public List<CloseCaseRequest> Closures { get; } = [];

        public async Task<CaseWorkflowRecord> ExecuteAsync(
            CloseCaseRequest request,
            CancellationToken cancellationToken)
        {
            CaseLifecycleRules.ValidateClose(request);
            var current = await CaseLifecycleRules.GetRequiredAsync(store, request.CaseId, cancellationToken);
            CaseLifecycleRules.RequireClosureIsAllowed(current, request);
            Closures.Add(request);
            return current with
            {
                State = Enum.Parse<CaseLifecycleState>(request.Outcome.ToString()),
                ClosureOutcome = request.Outcome
            };
        }
    }

    private sealed class StubEditAuthorityHolders(string? displayName, bool isAutomation = false)
        : IDescribeCaseEditAuthorityHolder
    {
        public Task<CaseEditAuthorityHolder> ExecuteAsync(
            ActorKind? holderKind,
            string holderSubjectId,
            ActionActor actor,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CaseEditAuthorityHolder(displayName, isAutomation));
        }
    }

    /// <summary>
    /// The EVA stores the send page reads: the case has never been sent, and its
    /// principal carries the modes the test states.
    /// </summary>
    private sealed class StubEvaSubmissionStores(EvaSubmissionModes modes) :
        IEvaSubmissionQueries,
        IEvaSubmissionModeStore
    {
        Task<EvaSubmissionRecord?> IEvaSubmissionQueries.GetLatestAsync(
            Guid caseId,
            CancellationToken cancellationToken) => Task.FromResult<EvaSubmissionRecord?>(null);

        Task<IReadOnlyList<EvaSubmissionFailure>> IEvaSubmissionQueries.GetRecentFailuresAsync(
            DateTimeOffset sinceUtc,
            int maximumResults,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EvaSubmissionFailure>>([]);

        Task<EvaSubmissionActivity> IEvaSubmissionQueries.GetActivityAsync(
            CancellationToken cancellationToken) => Task.FromResult(new EvaSubmissionActivity(null));

        Task<EvaSubmissionModes> IEvaSubmissionModeStore.GetForPrincipalAsync(
            string principalCode,
            CancellationToken cancellationToken) => Task.FromResult(modes);
    }

    /// <summary>
    /// In-memory stand-in so the page sees a composed transport and applies
    /// the principal's manual toggle. No request is ever sent anywhere: the
    /// send-page test is a GET, and a POST would only record here and read
    /// back as "nothing was submitted".
    /// </summary>
    private sealed class StubSubmitCaseToEva : ISubmitCaseToEva
    {
        public List<SubmitCaseToEvaRequest> Requests { get; } = [];

        public Task<SubmitCaseToEvaResult?> ExecuteAsync(
            SubmitCaseToEvaRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult<SubmitCaseToEvaResult?>(null);
        }
    }

    private sealed class StubStaffAccounts(Guid staffId, string userName, StaffRole role = StaffRole.User)
        : IStaffAccountQueries, IStaffHeldCaseEditLeaseQueries
    {
        private readonly StaffAccountSummary account =
            new(staffId, userName, true, false, role);

        public Task<StaffAccountQuerySlice> ListAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult(new StaffAccountQuerySlice([account], false));

        public Task<StaffAccountSummary?> GetAsync(
            Guid requestedStaffId,
            CancellationToken cancellationToken) =>
            Task.FromResult<StaffAccountSummary?>(requestedStaffId == staffId ? account : null);

        public Task<IReadOnlyList<StaffHeldCaseEditLease>> ListHeldCaseEditLeasesAsync(
            Guid requestedStaffId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StaffHeldCaseEditLease>>([]);

        public Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SignOffEngineerProfile>>([]);

        public Task<SignOffEngineerProfile?> GetSignOffEngineerAsync(
            Guid requestedStaffId,
            CancellationToken cancellationToken) =>
            Task.FromResult<SignOffEngineerProfile?>(null);
    }

    /// <summary>
    /// The post-redirect-get lands on the Case record, carrying at most the
    /// section it returns to (v26: <c>?section=</c> plus a fragment). Name the
    /// expected query to pin the section.
    /// </summary>
    private static void AssertPrg(HttpResponseMessage response, Guid caseId, string? expectedQuery = null)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        var path = location.Split('?', '#')[0];
        Assert.Equal($"/Cases/{caseId:D}", path);
        var query = location.Contains('?', StringComparison.Ordinal) ? location.Split('?')[1].Split('#')[0] : string.Empty;
        Assert.True(
            query.Length == 0 || Regex.IsMatch(query, "^section=[a-z]+$"),
            $"The redirect carries more than a section: {location}");
        if (expectedQuery is not null)
        {
            Assert.Equal(expectedQuery.TrimStart('?'), query);
        }
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValueRegex();

    private static void SubstituteDetailsPageReaders(
        IServiceCollection services,
        RecordingCaseDetailsStore store)
    {
        Substitute<IGetCasePageFrame>(services, store);
        Substitute<IGetCaseVehicleSection>(services, store);
        Substitute<IGetCaseValuationSection>(services, store);
        Substitute<IGetCaseNotesSection>(services, store);
        Substitute<IGetCaseFilesSection>(services, store);
        Substitute<IGetAssessmentWorkspace>(services, store);
    }
    private sealed class CountingAssessmentWorkspace(AssessmentWorkspace workspace) : IGetAssessmentWorkspace
    {
        public AssessmentWorkspace Workspace { get; } = workspace;
        public int ReadCount { get; private set; }

        public Task<AssessmentWorkspace?> ExecuteAsync(
            GetAssessmentWorkspaceQuery query,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult<AssessmentWorkspace?>(Workspace);
        }

        public void Reset() => ReadCount = 0;
    }

    private sealed partial class RecordingCaseDetailsStore :
        IGetCase,
        IGetCasePageFrame,
        ICaseDataQueries,
        IInspectionAddressChoicesQueries,
        IAcquireCaseEditLease,
        IRecordManualCaseChase,
        IHoldCase,
        IReleaseCase,
        ITransitionCase,
        ICaseWorkflowQueries,
        ISaveCaseWorkspace,
        IGetCaseVehicleSection,
        IGetCaseValuationSection,
        IGetCaseNotesSection,
        IGetCaseFilesSection,
        IValidateCaseRenderLease
    {
        private readonly DateTimeOffset _now = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        private CaseDueWork _dueWork;
        private string? _leaseHolder;
        private ActorKind? _leaseHolderKind = ActorKind.Staff;
        private string? _leaseOperationKey;

        public RecordingCaseDetailsStore()
        {
            _dueWork = new(
                CaseId,
                "QDOS26001",
                "Vehicle images",
                new DateOnly(2031, 5, 10),
                CaseDueWorkState.Scheduled,
                _now.AddDays(1),
                null,
                null,
                null,
                null,
                null,
                3);
        }

        public Guid CaseId { get; } = Guid.NewGuid();

        public long CaseVersion { get; private set; } = 7;
        public bool AcceptWorkspaceSaves { get; init; }

        /// <summary>The workflow state the projection reports; Not ready unless a test says otherwise.</summary>
        public CaseLifecycleState State { get; set; } = CaseLifecycleState.NotReady;

        public bool ExposeCustody { get; init; }

        /// <summary>
        /// The lifecycle state the store's case data reports. The default keeps the
        /// workflow surface's NotReady answer; a page that acts on a particular
        /// state sets the state it needs.
        /// </summary>
        public CaseLifecycleState CaseState { get; init; } = CaseLifecycleState.NotReady;

        /// <summary>The detected Sent evidence the projection offers for confirmation (D10).</summary>
        public IReadOnlyList<RetainedApprovedMailboxReportSentEvidence> AvailableReportSentEvidence
        {
            get;
            init;
        } = [];

        public IReadOnlyList<CaseHistoryEntry> HistoryEntries { get; init; } = [];

        public IReadOnlyList<CaseQueryEmail> QueryEmails { get; init; } = [];

        public string LeaseToken { get; } = new('a', CaseEditAuthority.LeaseTokenLength);

        public bool RenderLeaseIsCurrent { get; set; } = true;

        /// <summary>Fails a test if a focused page path falls back to the legacy full Case read.</summary>
        public bool ThrowOnBroadCaseRead { get; init; }

        public CaseAssessmentProjection? FocusedAssessment { get; set; }
        public List<GetCaseSectionQuery> VehicleSectionQueries { get; } = [];
        public List<GetCaseSectionQuery> ValuationSectionQueries { get; } = [];
        public List<CaseAssessmentProjection?> VehicleSectionAssessments { get; } = [];
        public List<CaseAssessmentProjection?> ValuationSectionAssessments { get; } = [];

        public List<ClaimCaseEditLeaseRequest> Claims { get; } = [];
        public string? LeaseHolder
        {
            get => _leaseHolder;
            set => _leaseHolder = value;
        }

        public ActorKind? LeaseHolderKind
        {
            get => _leaseHolderKind;
            set => _leaseHolderKind = value;
        }

        public List<SaveCaseWorkspaceRequest> Saves { get; } = [];
        public CaseDataProjection? DataOverride { get; set; }
        public List<ManualChaseRecord> ManualChases { get; } = [];
        public List<PutCaseOnHoldRequest> Holds { get; } = [];
        public List<CaseMutationRequest> Releases { get; } = [];
        public List<TransitionCaseRequest> Transitions { get; } = [];
        public InspectionAddressChoicesData InspectionChoices { get; init; } = new(
            "8 Claimant Street",
            RepairerAddress: null,
            "14 Storage Lane",
            ["2 Previous Street", "1 Older Avenue"]);

        public Task<CaseDetails?> ExecuteAsync(GetCaseQuery query, CancellationToken cancellationToken)
        {
            if (ThrowOnBroadCaseRead)
            {
                throw new InvalidOperationException("A focused Case page read used IGetCase.");
            }

            var workflow = CreateWorkflow();
            var summary = CreateSummary(workflow);
            CaseDetails details = new(
                summary,
                workflow,
                ActiveLease(),
                CaseDocuments,
                null,
                CaseCustodyState.Pending,
                RequestUploadLinks,
                AvailableReportSentEvidence,
                HistoryEntries)
            {
                Data = DataOverride ?? CreateData(),
                VehicleEvidence = VehicleLookupEvidence,
                QueryEmails = QueryEmails,
                RecordNotes = RecordNotes,
                Custody = ExposeCustody
                    ? [new(CaseId, CaseVersion, CustodyTargetKind.CaseSource, "Failed", "Provider storage was unavailable.", 1, true)]
                    : []
            };
            return Task.FromResult<CaseDetails?>(details);
        }

        Task<CasePageFrame?> IGetCasePageFrame.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken)
        {
            if (query.CaseId != CaseId)
            {
                return Task.FromResult<CasePageFrame?>(null);
            }

            return Task.FromResult<CasePageFrame?>(new(
                FocusedFrame(),
                CaseDocuments,
                AvailableReportSentEvidence,
                RecordNotes,
                DataOverride ?? CreateData()));
        }

        Task<CaseVehicleSection?> IGetCaseVehicleSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken)
        {
            VehicleSectionQueries.Add(query);
            if (query.CaseId != CaseId)
            {
                return Task.FromResult<CaseVehicleSection?>(null);
            }

            var sectionAssessment = query.AssessmentWorkspace?.Assessment ?? FocusedAssessment ?? EngineeringAssessment();
            VehicleSectionAssessments.Add(sectionAssessment);
            return Task.FromResult<CaseVehicleSection?>(new(
                FocusedFrame(),
                query.AssessmentWorkspace?.Data ?? query.Data ?? DataOverride ?? CreateData(),
                query.AssessmentWorkspace?.LatestVehicleObservation ?? VehicleLookupEvidence?.LatestObservation,
                sectionAssessment));
        }

        Task<CaseValuationSection?> IGetCaseValuationSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken)
        {
            ValuationSectionQueries.Add(query);
            if (query.CaseId != CaseId)
            {
                return Task.FromResult<CaseValuationSection?>(null);
            }

            var sectionAssessment = query.AssessmentWorkspace?.Assessment ?? FocusedAssessment ?? EngineeringAssessment();
            ValuationSectionAssessments.Add(sectionAssessment);
            return Task.FromResult<CaseValuationSection?>(new(
                FocusedFrame(),
                query.AssessmentWorkspace?.Data ?? query.Data ?? DataOverride ?? CreateData(),
                sectionAssessment));
        }

        Task<CaseNotesSection?> IGetCaseNotesSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<CaseNotesSection?>(query.CaseId == CaseId
                ? new(FocusedFrame(), HistoryEntries)
                : null);
        }

        Task<CaseFilesSection?> IGetCaseFilesSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<CaseFilesSection?>(query.CaseId == CaseId
                ? new(
                    FocusedFrame(),
                    query.Documents ?? CaseDocuments,
                    null,
                    CaseCustodyState.Pending,
                    RequestUploadLinks,
                    QueryEmails)
                : null);
        }

        Task<bool> IValidateCaseRenderLease.ExecuteAsync(
            ValidateCaseRenderLeaseQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                RenderLeaseIsCurrent
                && string.Equals(query.Token, LeaseToken, StringComparison.Ordinal)
                && string.Equals(query.Actor.SubjectId, LeaseHolder, StringComparison.Ordinal));

        private CaseSearchItem CreateSummary(CaseWorkflowRecord workflow) => new(
            CaseId,
            workflow.Identity.Reference,
            null,
            SummaryCaseType,
            workflow.Identity.PrincipalCode,
            workflow.State,
            null,
            OmitVehicleValues ? null : "AB12CDE",
            "Case claimant",
            "CLM-42",
            _now.AddDays(-2),
            new DateOnly(2031, 5, 5),
            "Email",
            _now.AddDays(-2));

        private CaseEditLeaseSnapshot? ActiveLease() => _leaseHolder is null
            ? null
            : new(_leaseHolder, _leaseHolderKind, _now.AddMinutes(5), _leaseOperationKey!);

        private CaseSectionFrame FocusedFrame()
        {
            var workflow = CreateWorkflow();
            return new(CreateSummary(workflow), workflow, ActiveLease());
        }

        /// <summary>
        /// The same case the details surface serves, through the port the data-reading
        /// case pages (the EVA send page) use.
        /// </summary>
        public Task<CaseDataProjection?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseDataProjection?>(caseId == CaseId ? DataOverride ?? CreateData() : null);

        Task<CaseWorkflowRecord?> ICaseWorkflowQueries.GetAsync(
            Guid caseId,
            CancellationToken cancellationToken) => Task.FromResult<CaseWorkflowRecord?>(
                caseId == CaseId ? CreateWorkflow() : null);

        Task<bool> ICaseWorkflowQueries.HasOperationAsync(
            Guid caseId,
            string operationKey,
            CancellationToken cancellationToken) => Task.FromResult(false);

        Task<InspectionAddressChoicesData?> IInspectionAddressChoicesQueries.GetAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult<InspectionAddressChoicesData?>(
                caseId == CaseId ? InspectionChoices : null);

        /// <summary>
        /// The case as it currently stands, so a refused editor's proposed values have something to
        /// be compared against rather than an empty "the case now holds" column.
        /// </summary>
        private CaseDataProjection CreateData() =>
            new(
                new(CaseId, "QDOS", 2031, 42, "QDOS3100042"),
                new(
                    Guid.NewGuid(),
                    IntakeSourceChannel.Mailbox,
                    "receipt-token",
                    "source-hash",
                    _now.AddDays(-2),
                    "reader",
                    "1",
                    null,
                    null),
                _now.AddDays(-2),
                CaseVersion,
                CaseState,
                new(
                    new(
                        InstructionComplete: true,
                        ImagesComplete: true),
                    new(false, "case-completeness", 1)),
                new(Confirmed("QDOS")),
                new(Confirmed("Case claimant"), Empty<string>(), Empty<string>()),
                new(Confirmed("CLM-42")),
                VehicleFields(),
                new(Empty<DateOnly>(), Confirmed("Rear impact")),
                new(Confirmed("Case contact"), Empty<string>(), Empty<string>()),
                new(Empty<DateOnly>(), Confirmed("Standard")),
                new(
                    Empty<DateOnly>(),
                    Empty<DateOnly>(),
                    Confirmed("1 Depot Road"),
                    Confirmed(CaseInspectionMode.PhysicalAddress),
                    Confirmed("14 Storage Lane"),
                    Empty<string>()));

        /// <summary>
        /// The vehicle as the case holds it. A lookup now fills empty fields as
        /// working values rather than suggesting beside them, so there is no
        /// suggestion shape to build.
        /// </summary>
        private CaseVehicleData VehicleFields() => OmitVehicleValues
            ? new(Empty<string>(), Empty<string>(), Empty<string>(), Empty<string>(), Empty<long>(), Empty<string>())
            : new(
                Confirmed("AB12CDE"),
                Confirmed("Ford"),
                Confirmed("Transit"),
                Confirmed("2019"),
                Confirmed(42_000L),
                Confirmed("miles"));

        private static readonly CaseDataSource StaffCorrection =
            new(CaseDataSourceKind.StaffCorrection, "staff", "Staff correction", "case-edit", 1);

        private CaseField<T> Confirmed<T>(T value)
            where T : notnull =>
            new(
                null,
                null,
                new(value, CaseDataValueKind.Confirmed, StaffCorrection, "staff", _now));

        private static CaseField<T> Empty<T>()
            where T : notnull =>
            new(null, null, null);

        Task<CaseEditLease> IAcquireCaseEditLease.ExecuteAsync(
            ClaimCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            _leaseHolder = request.Actor.SubjectId;
            _leaseHolderKind = request.Actor.Kind;
            _leaseOperationKey = request.OperationKey;
            Claims.Add(request);
            return Task.FromResult(
                new CaseEditLease(
                    request.CaseId,
                    LeaseToken,
                    request.Actor.SubjectId,
                    request.ExpectedVersion,
                    _now.AddMinutes(5)));
        }


        Task<SaveCaseWorkspaceResult> ISaveCaseWorkspace.ExecuteAsync(
            SaveCaseWorkspaceRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowNextFailure();
            Saves.Add(request);
            if (AcceptWorkspaceSaves)
            {
                CaseVersion++;
                return Task.FromResult(new SaveCaseWorkspaceResult(CreateData(), EngineeringAssessment(), null, false));
            }
            throw new CaseVersionConflictException(CaseId, request.ExpectedVersion, CaseVersion + 1);
        }

        Task<CaseWorkflowRecord> IHoldCase.ExecuteAsync(
            PutCaseOnHoldRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowNextFailure();
            Holds.Add(request);
            return Task.FromResult(CreateWorkflow() with { State = CaseLifecycleState.Held });
        }

        Task<CaseWorkflowRecord> IReleaseCase.ExecuteAsync(
            CaseMutationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowNextFailure();
            Releases.Add(request);
            return Task.FromResult(CreateWorkflow() with { State = CaseLifecycleState.Review });
        }

        Task<CaseWorkflowRecord> ITransitionCase.ExecuteAsync(
            TransitionCaseRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowNextFailure();
            Transitions.Add(request);
            return Task.FromResult(CreateWorkflow() with
            {
                State = request.Destination == CaseTransitionDestination.ReportPreparation
                    ? CaseLifecycleState.ReportPreparation
                    : CaseLifecycleState.Review
            });
        }
        private CaseWorkflowRecord CreateWorkflow() =>
            new CaseWorkflowRecord(
                CaseId,
                new(CaseId, "QDOS", 2031, 42, "QDOS3100042"),
                State,
                null,
                null,
                null,
                _dueWork,
                null,
                null,
                null,
                CaseVersion) with { HoldReviewOn = HoldReviewOn };

        Task<CaseDueWork> IRecordManualCaseChase.ExecuteAsync(
            ManualChaseRecord request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            ManualChases.Add(request);
            _dueWork = _dueWork with
            {
                NextChaseAtUtc = _now.AddDays(7),
                MostRecentChannel = request.Channel,
                MostRecentOutcome = request.Outcome,
                MostRecentNote = request.Note,
                Version = _dueWork.Version + 1
            };
            _leaseHolder = null;
            _leaseOperationKey = null;
            return Task.FromResult(_dueWork);
        }
    }
}
