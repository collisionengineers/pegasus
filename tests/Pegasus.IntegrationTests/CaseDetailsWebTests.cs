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
using Pegasus.Core.Tasks;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

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
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => Substitute<IGetCase>(services, store)));
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
    /// D29/D30: the record is one scrolling page of eleven sections in a fixed
    /// order. Every section has its stable host and its jump link, in that
    /// order, on every response.
    /// </summary>
    [Fact]
    public async Task TheRecordRendersElevenOrderedSectionHostsAndJumpLinks()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => Substitute<IGetCase>(services, store)));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");

        Assert.Equal(CaseSectionKeys, HostOrder(html));
        Assert.Equal(CaseSectionKeys, JumpLinkOrder(html));

        // The five sections that have a body below the fold are served as
        // fragments; every other host, including the Engineer shells,
        // renders with the page.
        Assert.Equal(
            ["engineer-notes", "vehicle", "valuation", "files", "notes"],
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
    [InlineData("?section=engineer-notes", "engineer-notes")]
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
            builder.ConfigureServices(services => Substitute<IGetCase>(services, store)));
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
            builder.ConfigureServices(services => Substitute<IGetCase>(services, store)));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=files");
        var queries = Section(html, "case-queries-title");
        var visible = WebUtility.HtmlDecode(VisibleText(queries));

        Assert.Contains("Queries", visible, StringComparison.Ordinal);
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
            builder.ConfigureServices(services => Substitute<IGetCase>(services, store)));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=files");

        Assert.DoesNotContain("case-queries-title", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Queries<", html, StringComparison.Ordinal);
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
            builder.ConfigureServices(services => Substitute<IGetCase>(services, store)));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=files");
        var panel = Section(html, "case-upload-requests-title");
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
        var dialog = html[html.IndexOf("id=\"create-upload-request\"", StringComparison.Ordinal)..];
        dialog = dialog[..dialog.IndexOf("</dialog>", StringComparison.Ordinal)];

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
    [InlineData("engineer-notes")]
    [InlineData("notes")]
    [InlineData("vehicle")]
    [InlineData("valuation")]
    public async Task TheSectionFragmentReturnsOnlyThatSectionBody(string key)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => Substitute<IGetCase>(services, store)));
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
    [InlineData("nonsense")]
    public async Task TheSectionFragmentRefusesKeysItDoesNotServe(string key)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => Substitute<IGetCase>(services, store)));
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
    /// The record has exactly one editor. Every section renders at once while
    /// the lease is held, so a second form posting the whole record would write
    /// the case's stored values over whatever another section is holding
    /// unsaved; the Inspection section contributes its control to the one
    /// record form instead. Editing one section and saving therefore cannot
    /// discard an unsaved edit in another: there is only one form to save.
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
            "id=\"inspection-address\" name=\"inspectionAddress\" form=\"case-edit-form\"",
            html,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// While this browser holds the edit lease the whole record is rendered: no
    /// body is deferred, so nothing being typed can be replaced by a section
    /// mounting under it.
    /// </summary>
    [Fact]
    public async Task HoldingTheEditLeaseRendersEverySectionAndDefersNone()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = await workspace.GetWorkspaceAsync();

        Assert.DoesNotContain("data-lazy=", html, StringComparison.Ordinal);
        Assert.DoesNotContain("section-placeholder", html, StringComparison.Ordinal);
        Assert.Equal(CaseSectionKeys, HostOrder(html));
        Assert.Equal(store.LeaseToken, InputValue(html, "editLeaseToken"));
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
            builder.ConfigureServices(services => Substitute<IGetCase>(services, store)));
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
    public async Task EngineerNotesRenderAttributedAndSeparateWithoutEditOrDeleteAffordances()
    {
        var staffId = Guid.NewGuid();
        var store = new RecordingCaseDetailsStore();
        store.EngineerNoteEntries =
        [
            new(
                Guid.NewGuid(),
                store.CaseId,
                staffId,
                "Check the nearside sill.",
                new DateTimeOffset(2031, 5, 6, 9, 15, 0, TimeSpan.Zero))
        ];
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IEngineerNoteQueries>(services, store);
                Substitute<IStaffAccountQueries>(services, new StubStaffAccounts(staffId, "alex"));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=engineer-notes");
        var section = Section(html, "case-engineer-notes-title");
        var visible = VisibleText(section);

        Assert.Contains("Engineer notes", visible, StringComparison.Ordinal);
        Assert.Contains("alex", visible, StringComparison.Ordinal);
        Assert.Contains("Check the nearside sill.", visible, StringComparison.Ordinal);
        Assert.DoesNotContain(staffId.ToString("D"), section, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No notes", visible, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<form", section, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("edit", visible, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("delete", visible, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, Occurrences(html, "Check the nearside sill."));
    }

    [Fact]
    public async Task EngineerNotesEmptyReadOnlySectionHasNoEmptyStateProse()
    {
        var store = new RecordingCaseDetailsStore();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IEngineerNoteQueries>(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=engineer-notes");
        var section = Section(html, "case-engineer-notes-title");

        Assert.DoesNotContain("empty", section, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No note", VisibleText(section), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EngineerNotePostCarriesTheLeasedStaffMutationEnvelope()
    {
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.PostReportComplete
        };
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<IEngineerNoteQueries>(services, store);
            Substitute<IAddEngineerNote>(services, store);
        });
        var html = await workspace.GetWorkspaceAsync();
        var section = Section(html, "case-engineer-notes-title");
        var operationKey = InputValue(section, "operationKey");

        Assert.Contains("handler=AddEngineerNote", section, StringComparison.Ordinal);
        Assert.Contains("name=\"expectedVersion\"", section, StringComparison.Ordinal);
        Assert.Contains("name=\"editLeaseToken\"", section, StringComparison.Ordinal);
        Assert.DoesNotContain("disabled", section, StringComparison.OrdinalIgnoreCase);

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=AddEngineerNote",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", operationKey),
                ("note", "  Check the chassis leg.  "),
                ("editLeaseToken", store.LeaseToken)));

        AssertPrg(response, store.CaseId);
        var command = Assert.Single(store.EngineerNoteAdds);
        AssertClaimant(workspace, command.Actor);
        Assert.Equal(store.CaseId, command.CaseId);
        Assert.Equal(store.CaseVersion, command.ExpectedVersion);
        Assert.Equal(operationKey, command.OperationKey);
        Assert.Equal("  Check the chassis leg.  ", command.Note);
        Assert.Equal(store.LeaseToken, command.EditLeaseToken);
    }

    [Fact]
    public async Task CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        // The EVA control is a Review act (FRD-07): the workspace offers the
        // handoff only in Review, so the store stands there.
        var store = new RecordingCaseDetailsStore
        {
            ExposeCustody = true,
            State = CaseLifecycleState.Review
        };
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.AddSingleton<IGetCase>(store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
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
    [InlineData(CaseLifecycleState.Review)]
    [InlineData(CaseLifecycleState.ReportPreparation)]
    public async Task SendPageRendersItsChoiceInReviewAndWithEngineer(CaseLifecycleState state)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore { CaseState = state, State = state };
        var evaStores = new StubEvaSubmissionStores(new EvaSubmissionModes(Manual: true, Automatic: false));
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<ICaseDataQueries>(services, store);
                Substitute<ICaseWorkflowQueries>(services, store);
                Substitute<IEvaSubmissionQueries>(services, evaStores);
                Substitute<IEvaSubmissionModeStore>(services, evaStores);
                // The page treats an uncomposed transport as "no API route":
                // a non-null submitter is what makes the manual toggle apply.
                Substitute<ISubmitCaseToEva>(services, new StubSubmitCaseToEva());
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        // EXT-04: the send page for a case still in Review — the one place the
        // operator chooses between the API submission and the export. The
        // capture-aware fetch so a Test UI capture records it.
        var html = await IntakeWebDriver.GetHtmlAsync(client, $"/Cases/{store.CaseId:D}/Eva/Send");

        // The page's own copy, as EPIC-011 restyled it: the handoff heading,
        // the case it is for, and both routes out.
        Assert.Contains("<h1>EVA handoff</h1>", html, StringComparison.Ordinal);
        Assert.Contains(
            "<h2 id=\"eva-handoff-title\">QDOS3100042</h2>",
            html,
            StringComparison.Ordinal);
        Assert.Contains("<span>Send via API</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span>Download ZIP</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Download EVA package", html, StringComparison.Ordinal);
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
            Assert.Contains("Recover editing", recoveryHtml, StringComparison.Ordinal);
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
        Assert.Equal("Missing evidence follow-up", command.Reason);
    }

    [Fact]
    public async Task LifecyclePostsBindHoldReleaseAndReportPreparationToAuthenticatedLease()
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
                services.AddSingleton<IGetCase>(store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<IHoldCase>(store);
                services.AddSingleton<IReleaseCase>(store);
                services.AddSingleton<ITransitionCase>(store);
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
        using var startResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}/Workflow?handler=StartWork",
            LifecycleForm(antiforgeryToken, store, "start-report-preparation", "Engineer work started"));

        AssertPrg(holdResponse, store.CaseId);
        AssertPrg(releaseResponse, store.CaseId);
        AssertPrg(startResponse, store.CaseId);
        var actorSubjectId = Assert.Single(store.Claims).Actor.SubjectId;
        var hold = Assert.Single(store.Holds);
        var release = Assert.Single(store.Releases);
        var transition = Assert.Single(store.Transitions);
        Assert.Equal(actorSubjectId, hold.Actor.SubjectId);
        Assert.Equal(actorSubjectId, release.Actor.SubjectId);
        Assert.Equal(actorSubjectId, transition.Actor.SubjectId);
        Assert.Equal(store.CaseVersion, hold.ExpectedVersion);
        Assert.Equal(store.CaseVersion, release.ExpectedVersion);
        Assert.Equal(store.CaseVersion, transition.ExpectedVersion);
        Assert.Equal(store.LeaseToken, hold.EditLeaseToken);
        Assert.Equal(store.LeaseToken, release.EditLeaseToken);
        Assert.Equal(store.LeaseToken, transition.EditLeaseToken);
        Assert.Equal("hold-case", hold.OperationKey);
        Assert.Equal("release-case", release.OperationKey);
        Assert.Equal("start-report-preparation", transition.OperationKey);
        Assert.Equal(CaseTransitionDestination.ReportPreparation, transition.Destination);
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
            "Case locked - another member of staff is editing",
            wrongHolderHtml,
            StringComparison.Ordinal);
        Assert.DoesNotContain(otherStaffId, wrongHolderHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"editLeaseToken\"", wrongHolderHtml, StringComparison.Ordinal);

        store.LeaseHolder = claimant;
        var recoveryHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Contains("Recover editing", recoveryHtml, StringComparison.Ordinal);
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
            ("reason", "Missing evidence follow-up"),
            ("channel", "Telephone"),
            ("recipient", "Provider claims team"),
            ("outcome", "Awaiting requested photographs"),
            ("content", "Asked provider for missing images"));

    /// <summary>
    /// What every case mutation posts from the leased workspace — the case id, its version, the
    /// operation key, the lease token, and the reason — plus the fields the action adds.
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
    /// SaveCase writes every member of <c>CaseEditableData</c>, so
    /// a value the handler does not bind is written as null and clears the
    /// confirmed field. The claimant's own contact number and address were
    /// omitted from both the form and the handler, so every Overview save
    /// silently discarded them (CASE-027).
    ///
    /// This asserts the values reach <c>SaveCase</c>, not merely that the inputs
    /// render: rendering them while the handler ignores them is exactly the
    /// half-fix this test exists to refuse.
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
                services.RemoveAll<ISaveCase>();
                services.AddSingleton<IGetCase>(store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<ISaveCase>(store);
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
        Assert.Equal("07700 900123", saved.Data.ClaimantContactNumber);
        Assert.Equal("12 Example Street, Leeds, LS1 1AA", saved.Data.ClaimantAddress);
        Assert.Equal("14 Storage Lane", saved.Data.StorageLocation);
        Assert.Equal("7 No Script Road", saved.Data.InspectionAddress);
        Assert.Equal(CaseInspectionMode.PhysicalAddress, saved.Data.InspectionMode);

        // The values the operator did not touch still travel, because SaveCase
        // nulls anything absent — the same defect one field over.
        Assert.Equal("Rebecca Claimant", saved.Data.ClaimantName);
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
                services.RemoveAll<ISaveCase>();
                services.AddSingleton<IGetCase>(store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<ISaveCase>(store);
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

    /// <summary>
    /// An unchecked box is absent from the post unless the form carries a hidden false, so without
    /// one a proposed "no" silently disappears from the comparison — the very defect the retention
    /// work exists to remove. The current column must be populated too, or there is nothing to
    /// compare against.
    /// </summary>
    [Fact]
    public async Task ARefusedCompletenessChangeKeepsUncheckedProposalsBesideTheCurrentValues()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.RemoveAll<IConfirmCompleteness>();
                services.AddSingleton<IGetCase>(store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<IConfirmCompleteness>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var initialHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");

        // Exactly what the browser posts: a checked box sends "true" then its hidden "false"; an
        // unchecked box sends the hidden "false" alone.
        using var response = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ConfirmCompleteness",
            RepeatableForm(
                AntiforgeryValue(initialHtml),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", DetailsModelOperationKey),
                ("editLeaseToken", store.LeaseToken),
                ("reason", "Images turned out to be incomplete"),
                ("instructionComplete", "true"),
                ("instructionComplete", "false"),
                ("imagesComplete", "false"),
                ("instructionConfirmedByStaff", "false"),
                ("imagesConfirmedByStaff", "false")));
        AssertPrg(response, store.CaseId);

        // The command really did receive false, so the panel must not claim otherwise.
        var confirmation = Assert.Single(store.CompletenessConfirmations);
        Assert.True(confirmation.Completeness.InstructionComplete);
        Assert.False(confirmation.Completeness.ImagesComplete);

        var panel = ProposedValuesPanel(await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}"));

        Assert.Contains("Images complete", panel, StringComparison.Ordinal);
        Assert.Contains("Instructions complete", panel, StringComparison.Ordinal);
        Assert.Contains(">No<", panel, StringComparison.Ordinal);
        Assert.Contains(">Yes<", panel, StringComparison.Ordinal);

        // Raw booleans never reach operator copy, and the current column is not the em-dash blank.
        Assert.DoesNotContain(">true<", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">false<", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("true, false", panel, StringComparison.OrdinalIgnoreCase);
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
                services.RemoveAll<ISaveCase>();
                services.AddSingleton<IGetCase>(store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<ISaveCase>(store);
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
        Assert.DoesNotContain("name=\"editLeaseToken\"", refusedHtml, StringComparison.Ordinal);
        Assert.Contains("Recover editing", refusedHtml, StringComparison.Ordinal);
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
                services.RemoveAll<ISaveCase>();
                services.AddSingleton<IGetCase>(store);
                services.AddSingleton<ISaveCase>(store);
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
                services.RemoveAll<ISaveCase>();
                services.AddSingleton<IGetCase>(store);
                services.AddSingleton<ISaveCase>(store);
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
                services.RemoveAll<ISaveCase>();
                services.AddSingleton<IGetCase>(store);
                services.AddSingleton<ISaveCase>(store);
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

        Assert.Contains("Case locked - r.hughes is editing", note, StringComparison.Ordinal);
        // CASE-024: an open editor keeps its own lease alive, so no moment when editing
        // becomes available is knowable here, and naming one would be a broken promise.
        Assert.DoesNotContain("Editing becomes available", note, StringComparison.Ordinal);
        Assert.Contains("Editing cannot be taken over", note, StringComparison.Ordinal);
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
            "Case locked - another member of staff is editing",
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

        Assert.Contains("Case locked - AI is editing", note, StringComparison.Ordinal);
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
                services.RemoveAll<ISaveCase>();
                services.RemoveAll<IDescribeCaseEditAuthorityHolder>();
                services.AddSingleton<IGetCase>(store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<ISaveCase>(store);
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
        Assert.Contains("Finish editing", RecordBar(holderHtml), StringComparison.Ordinal);
        AssertNoBannedVocabulary(RecordBar(holderHtml));

        // Recover: the same holder without the protected browser state.
        using (var recoveryClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        }))
        {
            var recoverHtml = await GetHtmlAsync(recoveryClient, $"/Cases/{store.CaseId:D}");
            Assert.Contains("Recover editing", RecordBar(recoverHtml), StringComparison.Ordinal);
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
                "Case locked - ",
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

    private static string RecordBar(string html)
    {
        var start = html.IndexOf("class=\"record-bar\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The record bar is not rendered.");
        // CASE-012 round 2: the workspace side nav is no longer a <nav>, so
        // the record's own closing tag bounds the bar and everything under
        // it (edit bar, workspace, context column).
        var end = html.IndexOf("</article>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The record bar is not closed before the record ends.");
        return html[start..end];
    }

    /// <summary>
    /// The eleven Case sections in the order D30 fixes, from the frame's one
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
        var start = html.IndexOf("class=\"section-nav\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The section jump-nav is not rendered.");
        var end = html.IndexOf("</nav>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The jump-nav is not closed.");
        return html[start..end];
    }

    private static int Occurrences(string html, string value) =>
        html.Split(value, StringSplitOptions.None).Length - 1;

    [GeneratedRegex(
        "<section class=\"case-section[^\"]*\" id=\"section-([a-z-]+)\"",
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

    private static string ProposedValuesPanel(string html) =>
        Section(html, "case-proposed-values-title");

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
                services.AddSingleton<IAcquireCaseEditLease>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Contains("Case locked - AI is editing", EditAuthorityNote(html), StringComparison.Ordinal);
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
        Assert.Contains("Edit mode could not be entered", afterRefusal, StringComparison.Ordinal);
        Assert.Contains("Case locked - AI is editing", EditAuthorityNote(afterRefusal), StringComparison.Ordinal);
        Assert.DoesNotContain("handler=ClaimLease", afterRefusal, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"editLeaseToken\"", afterRefusal, StringComparison.Ordinal);
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
            CancellationToken cancellationToken) => Task.FromResult(new EvaSubmissionActivity(0, null));

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

    private sealed class StubStaffAccounts(Guid staffId, string userName) : IStaffAccountQueries
    {
        private readonly StaffAccountSummary account =
            new(staffId, userName, true, false, [StaffRole.User], null);

        public Task<StaffAccountQuerySlice> ListAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult(new StaffAccountQuerySlice([account], false));

        public Task<StaffAccountSummary?> GetAsync(
            Guid requestedStaffId,
            CancellationToken cancellationToken) =>
            Task.FromResult<StaffAccountSummary?>(requestedStaffId == staffId ? account : null);

        public Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SignOffEngineerProfile>>([]);

        public Task<SignOffEngineerProfile?> GetSignOffEngineerAsync(
            Guid requestedStaffId,
            CancellationToken cancellationToken) =>
            Task.FromResult<SignOffEngineerProfile?>(null);
    }

    private static void AssertPrg(HttpResponseMessage response, Guid caseId)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Cases/{caseId:D}", response.Headers.Location?.OriginalString);
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValueRegex();

    private sealed partial class RecordingCaseDetailsStore :
        IGetCase,
        ICaseDataQueries,
        IInspectionAddressChoicesQueries,
        IAcquireCaseEditLease,
        IRecordManualCaseChase,
        IHoldCase,
        IReleaseCase,
        ITransitionCase,
        ICaseWorkflowQueries,
        IConfirmCompleteness,
        ISaveCase,
        IEngineerNoteQueries,
        IAddEngineerNote
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

        public long CaseVersion { get; } = 7;

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

        public IReadOnlyList<EngineerNote> EngineerNoteEntries { get; set; } = [];

        public string LeaseToken { get; } = "opaque-live-case-lease";

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

        public List<SaveCaseRequest> Saves { get; } = [];
        public List<ConfirmCompletenessRequest> CompletenessConfirmations { get; } = [];
        public List<ManualChaseRecord> ManualChases { get; } = [];
        public List<PutCaseOnHoldRequest> Holds { get; } = [];
        public List<CaseMutationRequest> Releases { get; } = [];
        public List<TransitionCaseRequest> Transitions { get; } = [];
        public List<AddEngineerNoteRequest> EngineerNoteAdds { get; } = [];

        public InspectionAddressChoicesData InspectionChoices { get; init; } = new(
            "8 Claimant Street",
            RepairerAddress: null,
            "14 Storage Lane",
            ["2 Previous Street", "1 Older Avenue"]);

        public Task<CaseDetails?> ExecuteAsync(GetCaseQuery query, CancellationToken cancellationToken)
        {
            var workflow = CreateWorkflow();
            var summary = new CaseSearchItem(
                CaseId,
                workflow.Identity.Reference,
                null,
                CaseType.Inspection,
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
            CaseDetails details = new(
                summary,
                workflow,
                _leaseHolder is null
                    ? null
                    : new(_leaseHolder, _leaseHolderKind, _now.AddMinutes(5), _leaseOperationKey!),
                CaseDocuments,
                null,
                CaseCustodyState.Pending,
                RequestUploadLinks,
                AvailableReportSentEvidence,
                HistoryEntries)
            {
                Data = CreateData(),
                VehicleEvidence = VehicleLookupEvidence,
                QueryEmails = QueryEmails,
                Custody = ExposeCustody
                    ? [new(CaseId, CaseVersion, CustodyTargetKind.CaseSource, "Failed", "Provider storage was unavailable.", 1, true)]
                    : []
            };
            return Task.FromResult<CaseDetails?>(details);
        }

        /// <summary>
        /// The same case the details surface serves, through the port the data-reading
        /// case pages (the EVA send page) use.
        /// </summary>
        public Task<CaseDataProjection?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseDataProjection?>(caseId == CaseId ? CreateData() : null);

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

        Task<IReadOnlyList<EngineerNote>> IEngineerNoteQueries.ListNewestFirstAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EngineerNote>>(
                caseId == CaseId ? EngineerNoteEntries : []);

        Task IAddEngineerNote.ExecuteAsync(
            AddEngineerNoteRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EngineerNoteAdds.Add(request);
            _leaseHolder = null;
            _leaseHolderKind = null;
            _leaseOperationKey = null;
            return Task.CompletedTask;
        }

        Task<CaseDataProjection> IConfirmCompleteness.ExecuteAsync(
            ConfirmCompletenessRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CompletenessConfirmations.Add(request);
            throw new CaseVersionConflictException(CaseId, request.ExpectedVersion, CaseVersion + 1);
        }

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
                        ImagesComplete: true,
                        InstructionConfirmedByStaff: false,
                        ImagesConfirmedByStaff: false),
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
        /// The vehicle as the case holds it. With
        /// <see cref="IncludeVehicleSuggestions"/> the latest answered lookup
        /// also suggests a different make, model and mileage, each cited to
        /// that observation, which is the shape the section's per-field
        /// acceptance controls render from (PR 670 port).
        /// </summary>
        private CaseVehicleData VehicleFields()
        {
            if (OmitVehicleValues)
            {
                return new(Empty<string>(), Empty<string>(), Empty<string>(), Empty<long>(), Empty<string>());
            }
            if (!IncludeVehicleSuggestions)
            {
                return new(
                    Confirmed("AB12CDE"),
                    Confirmed("Ford"),
                    Confirmed("Transit"),
                    Confirmed(42_000L),
                    Confirmed("miles"));
            }

            var lookup = VehicleLookupEvidence?.LatestObservation
                ?? throw new InvalidOperationException(
                    "Vehicle suggestions cite a recorded lookup observation; supply VehicleLookupEvidence.");
            return new(
                Confirmed("AB12CDE"),
                WithSuggestion(Confirmed("Ford"), "Ford Motor Company", lookup),
                WithSuggestion(Confirmed("Transit"), "Transit Custom", lookup),
                WithSuggestion(Confirmed(42_000L), 43_210L, lookup),
                WithSuggestion(Confirmed("miles"), "miles", lookup));
        }

        private static CaseField<T> WithSuggestion<T>(
            CaseField<T> field,
            T suggested,
            VehicleLookupObservation lookup)
            where T : notnull =>
            field with
            {
                Suggestion = new(
                    suggested,
                    CaseDataValueKind.Suggestion,
                    new(
                        CaseDataSourceKind.VehicleLookup,
                        lookup.Id.ToString("D"),
                        "DVLA lookup",
                        "vehicle-lookup",
                        1))
            };

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


        Task<CaseDataProjection> ISaveCase.ExecuteAsync(
            SaveCaseRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Saves.Add(request);
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
            new(
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
                CaseVersion);

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
