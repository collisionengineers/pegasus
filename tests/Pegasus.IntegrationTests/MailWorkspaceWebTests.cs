using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.AiWork;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Email;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.Presentation;
using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The mail workspace through the real page pipeline: what an operator sees, what
/// the filters carry, and the fact that nothing on it can change anything.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class MailWorkspaceWebTests
{
    private static readonly DateTimeOffset NowUtc = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    // The Category select holds the eleven categories only: No Case Found is a scope (v41 I1).
    private static readonly string[] CategoryFilterValues =
        ["", .. MailCategories.All.Select(MailCategories.Code)];
    // The record's Classification chips: the eleven, then Unclassified (R179).
    private static readonly string[] ChipValues =
        [.. MailCategories.All.Select(MailCategories.Code), "unclassified"];

    private const string FirstMailboxId = "instructions";
    private static string FirstMailboxFilter => TestMailboxId.From(FirstMailboxId).ToString("D");
    private const string FirstMailboxAddress = "instructions@collisionengineers.co.uk";
    private const string SecondMailboxId = "reports";
    private const string SecondMailboxAddress = "reports@collisionengineers.co.uk";

    /// <summary>The Inbox rows as a row list; the shell's notification list carries the same hook on every page.</summary>
    private const string InboxRowList = "<div data-row-list>";

    /// <summary>
    /// The Inbox's message dialog is the record's Dialog handler: the same
    /// exact-message read, actor and refusals as the record page, drawn as a
    /// fragment, and like it a read that changes nothing (v41 I2).
    /// </summary>
    [Fact]
    public async Task TheInboxDialogFragmentIsAuthenticatedExactEvidenceAndChangesNothing()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var messageId = Assert.Single(await SeedAsync(
            factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        using var client = IntakeWebDriver.CreateClient(factory);

        // The list draws the mount for the dialog and links each subject to the record.
        var list = await GetHtmlAsync(client, "/Inbox");
        Assert.Contains("data-mail-workspace", list, StringComparison.Ordinal);
        Assert.Contains("data-mail-dialog-root", list, StringComparison.Ordinal);
        Assert.Contains("data-mail-open", MailRow(list, messageId), StringComparison.Ordinal);

        bool readBefore;
        int classificationHistoryBefore;
        int associationHistoryBefore;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            readBefore = await context.RetainedMailboxMessages
                .Where(item => item.Id == messageId)
                .Select(item => item.IsRead)
                .SingleAsync();
            classificationHistoryBefore = await context.IntakeMailClassificationHistory.CountAsync();
            associationHistoryBefore = await context.IntakeMutationHistory.CountAsync();
        }

        using var response = await client.GetAsync($"/Inbox/{messageId:D}?handler=Dialog&mailbox={FirstMailboxFilter}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var fragment = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<html", fragment, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-dialog=\"mailMessageDialog\"", fragment, StringComparison.Ordinal);
        Assert.Contains($"data-mail-message=\"{messageId:D}\"", fragment, StringComparison.Ordinal);
        Assert.Contains($"Message 0 from {FirstMailboxId}", fragment, StringComparison.Ordinal);
        Assert.Contains("Please inspect the vehicle at the address supplied.", fragment, StringComparison.Ordinal);
        // The head carries Previous and Next (v41 I6); the list's context rides every link.
        Assert.Contains($"aria-label=\"{OperatorLabels.Inbox.PreviousMessage}\"", fragment, StringComparison.Ordinal);
        Assert.Contains($"aria-label=\"{OperatorLabels.Inbox.NextMessage}\"", fragment, StringComparison.Ordinal);
        Assert.Contains($"mailbox={FirstMailboxFilter}", fragment, StringComparison.Ordinal);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            Assert.Equal(readBefore, await context.RetainedMailboxMessages
                .Where(item => item.Id == messageId)
                .Select(item => item.IsRead)
                .SingleAsync());
            Assert.Equal(classificationHistoryBefore, await context.IntakeMailClassificationHistory.CountAsync());
            Assert.Equal(associationHistoryBefore, await context.IntakeMutationHistory.CountAsync());
        }

        using var unknown = await client.GetAsync($"/Inbox/{Guid.NewGuid():D}?handler=Dialog");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        using var rolelessRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Inbox/{messageId:D}?handler=Dialog");
        rolelessRequest.Headers.Add("X-Test-Roleless", "1");
        using var roleless = await client.SendAsync(rolelessRequest);
        Assert.Equal(HttpStatusCode.Forbidden, roleless.StatusCode);
    }

    /// <summary>
    /// The record's Content handler is the Case Correspondence dialog's body:
    /// the message's head, text and attachment names as a fragment, read the
    /// way the record reads it, with nothing to act on and nothing changed.
    /// </summary>
    [Fact]
    public async Task MessageContentIsAReadOnlyFragmentOfTheRecord()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var messageId = Assert.Single(await SeedAsync(
            factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        using var client = IntakeWebDriver.CreateClient(factory);

        bool readBefore;
        int classificationHistoryBefore;
        int associationHistoryBefore;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            readBefore = await context.RetainedMailboxMessages
                .Where(item => item.Id == messageId)
                .Select(item => item.IsRead)
                .SingleAsync();
            classificationHistoryBefore = await context.IntakeMailClassificationHistory.CountAsync();
            associationHistoryBefore = await context.IntakeMutationHistory.CountAsync();
        }

        using var response = await client.GetAsync($"/Inbox/{messageId:D}?handler=Content");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var fragment = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-message-content", fragment, StringComparison.Ordinal);
        Assert.Contains("From A Sender", fragment, StringComparison.Ordinal);
        Assert.Contains("To intake@collisionengineers.co.uk", fragment, StringComparison.Ordinal);
        Assert.Contains("Please inspect the vehicle at the address supplied.", fragment, StringComparison.Ordinal);
        Assert.Contains("<li>estimate.pdf</li>", fragment, StringComparison.Ordinal);
        Assert.DoesNotContain("<html", fragment, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<form", fragment, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<button", fragment, StringComparison.OrdinalIgnoreCase);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            Assert.Equal(readBefore, await context.RetainedMailboxMessages
                .Where(item => item.Id == messageId)
                .Select(item => item.IsRead)
                .SingleAsync());
            Assert.Equal(classificationHistoryBefore, await context.IntakeMailClassificationHistory.CountAsync());
            Assert.Equal(associationHistoryBefore, await context.IntakeMutationHistory.CountAsync());
        }

        using var unknown = await client.GetAsync($"/Inbox/{Guid.NewGuid():D}?handler=Content");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        using var rolelessRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Inbox/{messageId:D}?handler=Content");
        rolelessRequest.Headers.Add("X-Test-Roleless", "1");
        using var roleless = await client.SendAsync(rolelessRequest);
        Assert.Equal(HttpStatusCode.Forbidden, roleless.StatusCode);
    }

    /// <summary>
    /// C08: the workspace contract says mailbox/folder/search/queue/unread/
    /// sort/page are URL and retained-query state only — opening, filtering or
    /// changing the unread scope never reaches Outlook or writes
    /// a classification correction. <see cref="RecordingClassificationStore"/>
    /// stands in for <see cref="CorrectRetainedMailClassification"/>'s store
    /// and is asserted at zero after every read action below.
    /// </summary>
    [Fact]
    public async Task OpenFilterUnreadAndSortNeverWriteThroughTheRetainedMailPorts()
    {
        var classificationStore = new RecordingClassificationStore();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var ids = await SeedAsync(baseFactory, FirstMailboxId, FirstMailboxAddress, count: 3);
        for (var index = 0; index < ids.Length; index++)
        {
            await StoreMailClassificationAsync(
                baseFactory,
                FirstMailboxId,
                $"{FirstMailboxId}-{index}",
                MailClassificationResult.Classified(
                    MailCategory.NewInstruction,
                    [],
                    "A new instruction was recognised.",
                    "fixture",
                    1));
        }
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRetainedMailClassificationStore>();
                services.AddScoped<IRetainedMailClassificationStore>(_ => classificationStore);
            }));
        using var client = IntakeWebDriver.CreateClient(factory);

        // Open the list, filter it every way the workspace contract names,
        // then open and return from the full message —
        // carrying the exact same query string a real navigation would.
        // "queue" has no "all" sentinel: TryParseQueue accepts only a
        // category code (e.g. "new-instruction") or "no_case_found", and
        // treats anything else — "all" included — as invalid,
        // returning NotFound. The unfiltered scope is the absent parameter,
        // not a literal "all"; a real queue key exercises the same
        // round-trip without hitting that 404.
        // "sort" has no "asc"/"desc" sentinel either: TryParseSort accepts
        // only the absent value (newest) or "oldest" — the same two states
        // the sort toggle link itself draws (Index.cshtml.cs OldestFirst /
        // RefreshFields), so "oldest" is the value that exercises the
        // round-trip instead of hitting that same 404.
        // "search" matches against the attachment file name or an
        // IntakeReceipts search document (BuildMatches) — never the seeded
        // subject/body text, and SeedAsync writes no IntakeReceipts row — so
        // "estimate" (the attachment every seeded row carries) is the term
        // that actually leaves rows in the filtered list; a term that
        // matches nothing would render zero rows and no row link at all.
        var query = $"mailbox={FirstMailboxId}&folder=inbox&search=estimate&queue=new-instruction&sort=oldest";
        await GetHtmlAsync(client, "/Inbox");
        var listHtml = await GetHtmlAsync(client, $"/Inbox?{query}");

        // Prove a row actually rendered under this filtered query before
        // reading anything off it, then read the "unread"/"sort" tokens off
        // that rendered row's own link (Index.cshtml's row <a>) rather than
        // asserting a guessed ordered substring — the sort *toggle* link
        // deliberately carries the opposite value, so only the row link
        // proves what this query round-trips.
        var triggerIndex = listHtml.IndexOf("data-mail-open", StringComparison.Ordinal);
        Assert.True(triggerIndex >= 0, "expected at least one rendered row (data-mail-open) for this query.");
        var rowAnchorStart = listHtml.LastIndexOf("<a", triggerIndex, StringComparison.Ordinal);
        var rowAnchorEnd = listHtml.IndexOf('>', triggerIndex);
        Assert.True(rowAnchorStart >= 0 && rowAnchorEnd > triggerIndex,
            "expected data-mail-open inside a complete row anchor.");
        var rowAnchor = listHtml[rowAnchorStart..(rowAnchorEnd + 1)];
        var rowHrefMatch = Regex.Match(rowAnchor, "href=\"(?<href>[^\"]+)\"", RegexOptions.IgnoreCase);
        Assert.True(rowHrefMatch.Success, $"expected the rendered row anchor to carry an href: {rowAnchor}");
        var rowHref = WebUtility.HtmlDecode(rowHrefMatch.Groups["href"].Value);
        // No Unread scope (Inbox, 13 September): the row link never carries one.
        Assert.DoesNotContain("unread=", rowHref, StringComparison.Ordinal);
        Assert.Contains("sort=oldest", rowHref, StringComparison.Ordinal);

        // The dialog's fragment reads the same exact message and writes nothing.
        using (var dialogResponse = await client.GetAsync($"/Inbox/{ids[0]:D}?{query}&handler=Dialog"))
        {
            Assert.Equal(HttpStatusCode.OK, dialogResponse.StatusCode);
        }

        var messageHtml = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}?{query}");
        // "Back to Inbox" carries the mailbox the operator navigated with —
        // the query state is preserved, not reset to the default. The
        // anchor tag helper resolves asp-page/asp-route-* into a plain
        // href before the response is ever rendered, so match the rendered
        // form, not the source-only tag-helper attribute.
        var backLinkMarkup = Between(messageHtml, "<a class=\"btn\" href=\"/Inbox", "Back to Inbox");
        Assert.Contains($"mailbox={FirstMailboxId}", backLinkMarkup, StringComparison.Ordinal);

        // Preview -> full message -> back preserves the query string:
        // the message page carries it forward on every internal link it
        // renders (the message tabs use the same asp-route-* set).
        Assert.Contains($"mailbox={FirstMailboxId}", messageHtml, StringComparison.Ordinal);

        Assert.Equal(0, classificationStore.CorrectionCalls);
    }

    /// <summary>
    /// The unlink-cancels-case rule's operator-facing half. The unlink dialog warns, naming the
    /// case, only when unlinking actually cancels it — that is, when the
    /// receipt's current link is the case its own acceptance created. A receipt
    /// merely associated with some other case gets no warning, because
    /// unlinking it leaves that case alone.
    ///
    /// The flag is proved in CaseAcceptanceReplayTests. What is proved here is
    /// the rendering, which is the half an operator actually reads and the half
    /// nothing else covered — the same one-line-wiring gap that let case notes
    /// ship a note nobody could see.
    /// </summary>
    [Fact]
    public async Task TheUnlinkDialogWarnsOnlyWhenUnlinkingCancelsTheCase()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var messageId = Assert.Single(await SeedAsync(
            factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        await StoreClassificationAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var receiptId = await ReceiptIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        using var client = IntakeWebDriver.CreateClient(factory);

        // This receipt's own acceptance creates the case, so unlinking it takes
        // the case's only source away.
        var outcome = await AcceptReceiptAsync(factory, receiptId);

        var page = await GetHtmlAsync(
            client, $"/Inbox/{messageId:D}?mailbox={FirstMailboxFilter}");
        Assert.Contains(
            $"Unlinking this email cancels case {outcome.Identity.Reference}.",
            page,
            StringComparison.Ordinal);

        var submission = DialogSubmission(
            page,
            "UnlinkCase",
            "The email that created this case was unlinked.");
        using var unlink = await client.PostAsync(
            submission.Action,
            new FormUrlEncodedContent(submission.Fields));
        Assert.Equal(HttpStatusCode.Redirect, unlink.StatusCode);

        // The case is cancelled, and the receipt is no longer any case's source,
        // so nothing later claims an unlink would cancel anything.
        Assert.Equal(
            CaseLifecycleState.SourceEmailUnlinked,
            await ReadCaseStateAsync(factory.Services, outcome.Identity.CaseId));
        var unlinked = await GetHtmlAsync(
            client, $"/Inbox/{messageId:D}?mailbox={FirstMailboxFilter}");
        Assert.DoesNotContain("cancels case", unlinked, StringComparison.Ordinal);
    }

    /// <summary>
    /// What the Inbox pages cost in SQL commands against the real stores
    /// (Roadmap Lane D, parts D1 and D3): the message page of a message linked to
    /// a Case, the list, and the message dialog's fragment, which is the same
    /// record. The message page reads the Case header, not the whole Case; the
    /// list reads only its rows and counts, nothing per message. All three counts
    /// are ceilings, so growth in one is seen, and a failure lists every command
    /// sent.
    /// </summary>
    [Fact]
    public async Task TheInboxPagesSendTheirPinnedStatements()
    {
        var counter = new CommandCountingInterceptor();
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, useIntegrationTestAuthentication: true, commandInterceptor: counter);
        var messageId = Assert.Single(await SeedAsync(
            factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        await StoreClassificationAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var receiptId = await ReceiptIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        _ = await AcceptReceiptAsync(factory, receiptId);
        using var client = IntakeWebDriver.CreateClient(factory);
        // The first request of each page pays one-off work; the count is the second.
        _ = await GetHtmlAsync(client, $"/Inbox/{messageId:D}");
        _ = await GetHtmlAsync(client, "/Inbox");
        _ = await GetHtmlAsync(client, $"/Inbox/{messageId:D}?handler=Dialog");

        counter.Reset();
        var message = await GetHtmlAsync(client, $"/Inbox/{messageId:D}");
        var messageCommands = counter.Count;
        var messageDescription = counter.Describe();
        // An intake read is its receipt statement and its Assets statement: the receipt statement alone.
        var classificationDecisionReads = counter.CountMentioning(
            "[IntakeMailClassificationDecisions]", without: "[IntakeAssets]");
        Assert.Contains(messageId.ToString("D"), message, StringComparison.Ordinal);

        counter.Reset();
        var list = await GetHtmlAsync(client, "/Inbox");
        var listCommands = counter.Count;
        var listDescription = counter.Describe();
        Assert.Contains($"data-mail-row=\"{messageId:D}\"", list, StringComparison.OrdinalIgnoreCase);

        counter.Reset();
        _ = await GetHtmlAsync(client, $"/Inbox/{messageId:D}?handler=Dialog");
        var dialogCommands = counter.Count;
        var dialogDescription = counter.Describe();

        Assert.True(
            messageCommands <= InboxMessageCommands,
            $"The Inbox message page sent {messageCommands} SQL commands; at most {InboxMessageCommands} are allowed."
            + Environment.NewLine + messageDescription);
        Assert.Equal(2, classificationDecisionReads);
        Assert.True(
            listCommands <= InboxListCommands,
            $"The Inbox list sent {listCommands} SQL commands; at most {InboxListCommands} are allowed."
            + Environment.NewLine + listDescription);
        // The dialog is the record, so it holds to the record's ceiling.
        Assert.True(
            dialogCommands <= InboxMessageCommands,
            $"The Inbox message dialog sent {dialogCommands} SQL commands; at most {InboxMessageCommands} are allowed."
            + Environment.NewLine + dialogDescription);
    }

    private const int InboxMessageCommands = 18;

    private const int InboxListCommands = 10;

    /// <summary>
    /// The Inbox list reads nothing once per row: the same request over six
    /// messages sends the same commands as over three. Every message is
    /// retained, classified and linked to a Case, so each row carries the
    /// facts the list looks up for it.
    /// </summary>
    [Fact]
    public async Task TheInboxListSendsTheSameStatementsForSixMessagesAsForThree()
    {
        var counter = new CommandCountingInterceptor();
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, useIntegrationTestAuthentication: true, commandInterceptor: counter);
        var selectedId = (await SeedAcceptedMessagesAsync(
            factory, FirstMailboxId, FirstMailboxAddress, count: 3))[0];
        using var client = IntakeWebDriver.CreateClient(factory);
        var route = $"/Inbox?selected={selectedId:D}";
        // The first request pays one-off work; the counts are later ones.
        _ = await GetHtmlAsync(client, route);

        counter.Reset();
        _ = await GetHtmlAsync(client, route);
        var withThreeMessages = counter.Count;
        var withThreeDescription = counter.Describe();

        // Three more messages, in the second mailbox, same request.
        _ = await SeedAcceptedMessagesAsync(
            factory, SecondMailboxId, SecondMailboxAddress, count: 3);
        counter.Reset();
        var html = await GetHtmlAsync(client, route);
        var withSixMessages = counter.Count;
        var withSixDescription = counter.Describe();

        Assert.True(withThreeMessages > 0, "The interceptor observed no commands at all.");
        for (var index = 0; index < 3; index++)
        {
            Assert.Contains($"Message {index} from {SecondMailboxId}", html, StringComparison.Ordinal);
        }

        Assert.True(
            withThreeMessages == withSixMessages,
            $"The Inbox list grew with the messages: {withThreeMessages} commands for three and {withSixMessages} for six."
            + Environment.NewLine + "Three messages:" + Environment.NewLine + withThreeDescription
            + Environment.NewLine + "Six messages:" + Environment.NewLine + withSixDescription);
    }

    /// <summary>
    /// The Case / PO picker's search reads each destination's workflow state,
    /// version, archived flag and Triage version from the search rows
    /// themselves: no statement reads the workflow or Triage table without the
    /// Case search's own join.
    /// </summary>
    [Fact]
    public async Task TheCaseOptionsSearchReadsNoWorkflowOrTriageRowOfItsOwn()
    {
        var counter = new CommandCountingInterceptor();
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, useIntegrationTestAuthentication: true, commandInterceptor: counter);
        var messageId = Assert.Single(await SeedAsync(
            factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        await StoreClassificationAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var receiptId = await ReceiptIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        _ = await ImageIntakeTestData.SeedCaseAsync(
            factory.Services, receiptId, "MAIL35001", nameof(CaseLifecycleState.Review));
        using var client = IntakeWebDriver.CreateClient(factory);
        var route = $"/Inbox/{messageId:D}?handler=LinkCaseOptions&q=MAIL35001";
        // The first request pays one-off work; the count is the second.
        _ = await client.GetAsync(route);

        counter.Reset();
        using var options = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        Assert.Contains("value=\"MAIL35001\"", await options.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var ownReads = counter.CountMentioning("[CaseWorkflows]", without: "[Cases]")
            + counter.CountMentioning("[Triage]", without: "[Cases]");
        Assert.True(
            ownReads == 0,
            $"The Case options search sent {ownReads} workflow or Triage statements of its own."
            + Environment.NewLine + counter.Describe());
    }

    [Fact]
    public async Task ExactMessageCanBeLinkedUnlinkedAndLinkedToAReplacementInOneStepEach()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var messageId = Assert.Single(await SeedAsync(
            factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        await StoreClassificationAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        _ = Assert.Single(await SeedAsync(
            factory, SecondMailboxId, SecondMailboxAddress, count: 1));
        await StoreClassificationAsync(factory, SecondMailboxId, SecondMailboxId + "-0");
        var receiptId = await ReceiptIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var replacementOriginId = await ReceiptIdAsync(factory, SecondMailboxId, SecondMailboxId + "-0");
        var firstCaseId = await ImageIntakeTestData.SeedCaseAsync(
            factory.Services, receiptId, "MAIL31001", nameof(Pegasus.Core.Workflow.CaseLifecycleState.Review));
        var replacementCaseId = await ImageIntakeTestData.SeedCaseAsync(
            factory.Services, replacementOriginId, "MAIL31002", nameof(Pegasus.Core.Workflow.CaseLifecycleState.Review));
        using var client = IntakeWebDriver.CreateClient(factory);

        // The record carries the Case / PO picker and its Link to Case button; typing
        // fetches the Cases the receipt may be linked to, and choosing one writes nothing.
        var record = await GetHtmlAsync(
            client,
            $"/Inbox/{messageId:D}?mailbox={FirstMailboxFilter}&pageNumber=2");
        Assert.Contains($">{OperatorLabels.Inbox.LinkToCase}</button>", record, StringComparison.Ordinal);
        Assert.Contains(
            $"data-case-picker-url=\"/Inbox/{messageId:D}?handler=LinkCaseOptions\"",
            record,
            StringComparison.Ordinal);
        using (var options = await client.GetAsync($"/Inbox/{messageId:D}?handler=LinkCaseOptions&q=MAIL31001"))
        {
            Assert.Equal(HttpStatusCode.OK, options.StatusCode);
            var optionsHtml = await options.Content.ReadAsStringAsync();
            Assert.Contains("role=\"listbox\"", optionsHtml, StringComparison.Ordinal);
            Assert.Contains("value=\"MAIL31001\"", optionsHtml, StringComparison.Ordinal);
        }
        await AssertAssociationStateAsync(factory, receiptId, expectedCaseId: null, expectedHistoryCount: 0);

        // One POST: the Case / PO and the reason arrive together.
        var linkSubmission = DialogSubmission(
            record,
            "LinkCase",
            "The retained message names this exact Case/PO.",
            "MAIL31001");
        using var link = await client.PostAsync(
            linkSubmission.Action,
            new FormUrlEncodedContent(linkSubmission.Fields));
        Assert.Equal(HttpStatusCode.Redirect, link.StatusCode);
        Assert.Contains($"mailbox={FirstMailboxFilter}", link.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Contains("pageNumber=2", link.Headers.Location.ToString(), StringComparison.Ordinal);

        var linked = await GetHtmlAsync(client, link.Headers.Location.ToString());
        Assert.Contains("Message linked to the confirmed case.", linked, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.Inbox.Unlink}</button>", linked, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.Inbox.ChangeLinkedCase}</button>", linked, StringComparison.Ordinal);
        await AssertAssociationStateAsync(factory, receiptId, firstCaseId, expectedHistoryCount: 1);

        // The same post again finds the receipt moved on and writes nothing.
        using (var linkReplay = await client.PostAsync(
            linkSubmission.Action,
            new FormUrlEncodedContent(linkSubmission.Fields)))
        {
            Assert.Equal(HttpStatusCode.OK, linkReplay.StatusCode);
            Assert.Contains(
                "The message or case changed. Reload it, review the current target, and try again.",
                await linkReplay.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }
        await AssertAssociationStateAsync(factory, receiptId, firstCaseId, expectedHistoryCount: 1);

        var unlinkSubmission = DialogSubmission(
            linked,
            "UnlinkCase",
            "The message belongs to a different Case/PO.");
        using var unlink = await client.PostAsync(
            unlinkSubmission.Action,
            new FormUrlEncodedContent(unlinkSubmission.Fields));
        Assert.Equal(HttpStatusCode.Redirect, unlink.StatusCode);

        var unlinked = await GetHtmlAsync(client, unlink.Headers.Location!.ToString());
        Assert.Contains("Message unlinked from the confirmed case.", unlinked, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.Inbox.LinkToCase}</button>", unlinked, StringComparison.Ordinal);

        using (var unlinkReplay = await client.PostAsync(
            unlinkSubmission.Action,
            new FormUrlEncodedContent(unlinkSubmission.Fields)))
        {
            Assert.Equal(HttpStatusCode.OK, unlinkReplay.StatusCode);
        }
        await AssertAssociationStateAsync(factory, receiptId, expectedCaseId: null, expectedHistoryCount: 2);

        var replacement = await GetHtmlAsync(client, $"/Inbox/{messageId:D}");
        var relinkSubmission = DialogSubmission(
            replacement,
            "LinkCase",
            "The replacement Case/PO was separately searched and confirmed.",
            "MAIL31002");
        using var relink = await client.PostAsync(
            relinkSubmission.Action,
            new FormUrlEncodedContent(relinkSubmission.Fields));
        Assert.Equal(HttpStatusCode.Redirect, relink.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var association = await context.IntakeManualAssociations.SingleAsync(item => item.IntakeReceiptId == receiptId);
        Assert.True(association.IsActive);
        Assert.Equal(replacementCaseId, association.CaseId);
        Assert.Equal(3, await context.IntakeMutationHistory.CountAsync(item => item.IntakeReceiptId == receiptId));
        Assert.Null((await context.CaseWorkflows.SingleAsync(item => item.CaseId == firstCaseId)).EditLeaseToken);
        Assert.Null((await context.CaseWorkflows.SingleAsync(item => item.CaseId == replacementCaseId)).EditLeaseToken);
    }

    /// <summary>
    /// Change linked Case (v41 I9, I13) is the existing Unlink and the existing
    /// Link to Case in one post with one reason: two history entries, each under
    /// its own edit authority. A Case / PO that names no Case, or a Case a
    /// colleague is editing, stops it before anything is unlinked.
    /// </summary>
    [Fact]
    public async Task ChangeLinkedCaseUnlinksAndLinksInOnePostWithOneReasonAndTwoHistoryEntries()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var messageId = Assert.Single(await SeedAsync(
            factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        await StoreClassificationAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        _ = Assert.Single(await SeedAsync(
            factory, SecondMailboxId, SecondMailboxAddress, count: 1));
        await StoreClassificationAsync(factory, SecondMailboxId, SecondMailboxId + "-0");
        var receiptId = await ReceiptIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var otherOriginId = await ReceiptIdAsync(factory, SecondMailboxId, SecondMailboxId + "-0");
        var firstCaseId = await ImageIntakeTestData.SeedCaseAsync(
            factory.Services, receiptId, "MAIL33001", nameof(CaseLifecycleState.Review));
        var newCaseId = await ImageIntakeTestData.SeedCaseAsync(
            factory.Services, otherOriginId, "MAIL33002", nameof(CaseLifecycleState.Review));
        using var client = IntakeWebDriver.CreateClient(factory);

        var record = await GetHtmlAsync(client, $"/Inbox/{messageId:D}");
        var link = DialogSubmission(record, "LinkCase", "The message names this Case/PO.", "MAIL33001");
        using (var linked = await client.PostAsync(link.Action, new FormUrlEncodedContent(link.Fields)))
        {
            Assert.Equal(HttpStatusCode.Redirect, linked.StatusCode);
        }
        await AssertAssociationStateAsync(factory, receiptId, firstCaseId, expectedHistoryCount: 1);

        var page = await GetHtmlAsync(client, $"/Inbox/{messageId:D}");

        // A reference that names no Case is refused before the message is unlinked.
        var unknown = DialogSubmission(page, "ChangeLinkedCase", "Moved to the right Case.", "NO-SUCH-CASE");
        using (var refused = await client.PostAsync(unknown.Action, new FormUrlEncodedContent(unknown.Fields)))
        {
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            Assert.Contains(
                "Choose one Case by its Case / PO reference.",
                await refused.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }
        await AssertAssociationStateAsync(factory, receiptId, firstCaseId, expectedHistoryCount: 1);

        // A colleague is editing the new Case: its authority is claimed before the
        // message is unlinked, so the message stays linked to the first Case.
        await using (var colleagueScope = factory.Services.CreateAsyncScope())
        {
            var colleague = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
            var held = await colleagueScope.ServiceProvider
                .GetRequiredService<Pegasus.Core.Workflow.IAcquireCaseEditLease>()
                .ExecuteAsync(
                    new(newCaseId, 0, colleague, $"mail-test-colleague:{Guid.NewGuid():N}"),
                    CancellationToken.None);
            var busy = DialogSubmission(page, "ChangeLinkedCase", "Moved to the right Case.", "MAIL33002");
            using (var refusedBusy = await client.PostAsync(busy.Action, new FormUrlEncodedContent(busy.Fields)))
            {
                Assert.Equal(HttpStatusCode.OK, refusedBusy.StatusCode);
                Assert.Contains(
                    "This case is currently being edited. Reload and try again later.",
                    await refusedBusy.Content.ReadAsStringAsync(),
                    StringComparison.Ordinal);
            }
            await AssertAssociationStateAsync(factory, receiptId, firstCaseId, expectedHistoryCount: 1);
            Assert.Null(await CaseLeaseTokenAsync(factory, firstCaseId));
            await colleagueScope.ServiceProvider
                .GetRequiredService<Pegasus.Core.Workflow.IReleaseCaseEditLease>()
                .ExecuteAsync(
                    new(newCaseId, colleague, $"mail-test-colleague-release:{Guid.NewGuid():N}", held.Token),
                    CancellationToken.None);
        }

        // The record is drawn again, so the retry carries fresh confirmation keys.
        var change = DialogSubmission(
            await GetHtmlAsync(client, $"/Inbox/{messageId:D}"),
            "ChangeLinkedCase",
            "Moved to the right Case.",
            "MAIL33002");
        using (var changed = await client.PostAsync(change.Action, new FormUrlEncodedContent(change.Fields)))
        {
            Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        }

        await AssertAssociationStateAsync(factory, receiptId, newCaseId, expectedHistoryCount: 3);
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.Null((await context.CaseWorkflows.SingleAsync(item => item.CaseId == firstCaseId)).EditLeaseToken);
        Assert.Null((await context.CaseWorkflows.SingleAsync(item => item.CaseId == newCaseId)).EditLeaseToken);
    }

    /// <summary>
    /// A linked message's Case / PO picker lists Cases (v41 I13): typing finds
    /// another Case, never the one the message is linked to, and the reason
    /// dialog then re-links the message to the chosen Case in one step.
    /// </summary>
    [Fact]
    public async Task ALinkedMessagesPickerListsCasesAndChangeLinkedCaseRelinksItInOneStep()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var messageId = Assert.Single(await SeedAsync(
            factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        await StoreClassificationAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        _ = Assert.Single(await SeedAsync(
            factory, SecondMailboxId, SecondMailboxAddress, count: 1));
        await StoreClassificationAsync(factory, SecondMailboxId, SecondMailboxId + "-0");
        var receiptId = await ReceiptIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var otherOriginId = await ReceiptIdAsync(factory, SecondMailboxId, SecondMailboxId + "-0");
        var firstCaseId = await ImageIntakeTestData.SeedCaseAsync(
            factory.Services, receiptId, "MAIL34001", nameof(CaseLifecycleState.Review));
        var newCaseId = await ImageIntakeTestData.SeedCaseAsync(
            factory.Services, otherOriginId, "MAIL34002", nameof(CaseLifecycleState.Review));
        using var client = IntakeWebDriver.CreateClient(factory);

        var link = DialogSubmission(
            await GetHtmlAsync(client, $"/Inbox/{messageId:D}"),
            "LinkCase",
            "The message names this Case/PO.",
            "MAIL34001");
        using (var linked = await client.PostAsync(link.Action, new FormUrlEncodedContent(link.Fields)))
        {
            Assert.Equal(HttpStatusCode.Redirect, linked.StatusCode);
        }
        await AssertAssociationStateAsync(factory, receiptId, firstCaseId, expectedHistoryCount: 1);

        // The linked record's picker belongs to the Change linked Case dialog; typing lists another Case.
        var page = await GetHtmlAsync(client, $"/Inbox/{messageId:D}");
        Assert.Contains("form=\"changeLinkedCaseForm\"", page, StringComparison.Ordinal);
        using (var options = await client.GetAsync($"/Inbox/{messageId:D}?handler=LinkCaseOptions&q=MAIL34002"))
        {
            Assert.Equal(HttpStatusCode.OK, options.StatusCode);
            var optionsHtml = await options.Content.ReadAsStringAsync();
            Assert.Contains("role=\"listbox\"", optionsHtml, StringComparison.Ordinal);
            Assert.Contains("value=\"MAIL34002\"", optionsHtml, StringComparison.Ordinal);
        }
        // The Case the message is linked to is not offered as the Case to change to.
        using (var own = await client.GetAsync($"/Inbox/{messageId:D}?handler=LinkCaseOptions&q=MAIL34001"))
        {
            Assert.Equal(HttpStatusCode.OK, own.StatusCode);
            Assert.DoesNotContain("value=\"MAIL34001\"", await own.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        await AssertAssociationStateAsync(factory, receiptId, firstCaseId, expectedHistoryCount: 1);

        // Choosing the option only fills the field; the reason dialog's one post re-links.
        var change = DialogSubmission(page, "ChangeLinkedCase", "Moved to the right Case.", "MAIL34002");
        using (var changed = await client.PostAsync(change.Action, new FormUrlEncodedContent(change.Fields)))
        {
            Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        }
        await AssertAssociationStateAsync(factory, receiptId, newCaseId, expectedHistoryCount: 3);
    }

    /// <summary>
    /// Automatic association links a message without asking its decision, so a
    /// linked message can carry one that leaves nothing to link (Unsupported). It
    /// is offered no Change linked Case, and a post that names a Case anyway is
    /// refused with Core's sentence before the new Case is claimed or the
    /// message is unlinked: it stays linked and its Case is not cancelled.
    /// </summary>
    [Fact]
    public async Task ALinkedMessageWhoseDecisionCannotBeLinkedOffersNoChangeAndARefusedPostWritesNothing()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var messageId = Assert.Single(await SeedAsync(
            factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        await StoreOutcomeReceiptAsync(
            factory, FirstMailboxId, FirstMailboxId + "-0", IntakeDecision.Unsupported);
        _ = Assert.Single(await SeedAsync(
            factory, SecondMailboxId, SecondMailboxAddress, count: 1));
        await StoreClassificationAsync(factory, SecondMailboxId, SecondMailboxId + "-0");
        var receiptId = await ReceiptIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var otherOriginId = await ReceiptIdAsync(factory, SecondMailboxId, SecondMailboxId + "-0");
        var linkedCaseId = await ImageIntakeTestData.SeedCaseAsync(
            factory.Services, receiptId, "MAIL35001", nameof(CaseLifecycleState.Review));
        var otherCaseId = await ImageIntakeTestData.SeedCaseAsync(
            factory.Services, otherOriginId, "MAIL35002", nameof(CaseLifecycleState.Review));
        await MailboxIntakeTestData.LinkReceiptAsync(factory.Services, receiptId, linkedCaseId);
        using var client = IntakeWebDriver.CreateClient(factory);

        var page = await GetHtmlAsync(client, $"/Inbox/{messageId:D}");
        Assert.Contains($">{OperatorLabels.Inbox.Unlink}</button>", page, StringComparison.Ordinal);
        Assert.DoesNotContain($">{OperatorLabels.Inbox.ChangeLinkedCase}</button>", page, StringComparison.Ordinal);
        Assert.DoesNotContain("changeLinkedCaseForm", page, StringComparison.Ordinal);

        // A stale page or another client can still post it: the Unlink dialog's
        // keys, to the Change handler, with the Case typed.
        var unlink = DialogSubmission(page, "UnlinkCase", "Moved to the right Case.");
        var change = new AssociationPost(
            unlink.Action.Replace("handler=UnlinkCase", "handler=ChangeLinkedCase", StringComparison.Ordinal),
            new(unlink.Fields)
            {
                ["caseReference"] = "MAIL35002",
                ["linkLeaseOperationKey"] = Guid.NewGuid().ToString("N"),
                ["linkOperationKey"] = Guid.NewGuid().ToString("D")
            });
        using (var refused = await client.PostAsync(change.Action, new FormUrlEncodedContent(change.Fields)))
        {
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            Assert.Contains(
                OperatorLabels.IntakeCannotBecomeCaseReason(IntakeDecision.Unsupported),
                await refused.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }

        await AssertAssociationStateAsync(factory, receiptId, linkedCaseId, expectedHistoryCount: 0);
        Assert.Equal(CaseLifecycleState.Review, await ReadCaseStateAsync(factory.Services, linkedCaseId));
        Assert.Null(await CaseLeaseTokenAsync(factory, linkedCaseId));
        Assert.Null(await CaseLeaseTokenAsync(factory, otherCaseId));
    }

    /// <summary>
    /// The same decision on a message no Case holds: Link to Case, its reason
    /// dialog and the Case / PO picker are not drawn, and a post that names a
    /// Case anyway is refused with Core's sentence before any Case is searched
    /// or claimed, leaving the message unlinked.
    /// </summary>
    [Fact]
    public async Task AnUnlinkedMessageWhoseDecisionCannotBeLinkedOffersNoLinkAndARefusedPostWritesNothing()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var messageId = Assert.Single(await SeedAsync(
            factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        await StoreOutcomeReceiptAsync(
            factory, FirstMailboxId, FirstMailboxId + "-0", IntakeDecision.Unsupported);
        _ = Assert.Single(await SeedAsync(
            factory, SecondMailboxId, SecondMailboxAddress, count: 1));
        await StoreClassificationAsync(factory, SecondMailboxId, SecondMailboxId + "-0");
        var receiptId = await ReceiptIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var otherOriginId = await ReceiptIdAsync(factory, SecondMailboxId, SecondMailboxId + "-0");
        var otherCaseId = await ImageIntakeTestData.SeedCaseAsync(
            factory.Services, otherOriginId, "MAIL36001", nameof(CaseLifecycleState.Review));
        using var client = IntakeWebDriver.CreateClient(factory);

        var page = await GetHtmlAsync(client, $"/Inbox/{messageId:D}");
        Assert.DoesNotContain($">{OperatorLabels.Inbox.LinkToCase}</button>", page, StringComparison.Ordinal);
        Assert.DoesNotContain("linkCaseDialog", page, StringComparison.Ordinal);
        Assert.DoesNotContain("linkCaseForm", page, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"mailLinkCaseReference\"", page, StringComparison.Ordinal);

        // A stale page or another client can still post it: the dialog's keys, with the Case typed.
        long receiptVersion;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            receiptVersion = await context.IntakeReceipts
                .Where(item => item.Id == receiptId)
                .Select(item => item.Version)
                .SingleAsync();
        }
        using (var refused = await client.PostAsync(
            $"/Inbox/{messageId:D}?handler=LinkCase",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = AntiforgeryToken(page),
                ["expectedIntakeVersion"] = receiptVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["leaseOperationKey"] = Guid.NewGuid().ToString("N"),
                ["operationKey"] = Guid.NewGuid().ToString("D"),
                ["Reason"] = "The message names this Case/PO.",
                ["caseReference"] = "MAIL36001"
            })))
        {
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            Assert.Contains(
                OperatorLabels.IntakeCannotBecomeCaseReason(IntakeDecision.Unsupported),
                await refused.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }

        await AssertAssociationStateAsync(factory, receiptId, expectedCaseId: null, expectedHistoryCount: 0);
        Assert.Null(await CaseLeaseTokenAsync(factory, otherCaseId));
    }

    [Fact]
    public async Task LinkedPostReportMessageCreatesAQueryResponseJobFromTheRecord()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        var (messageId, caseId, linked) = await SeedLinkedPostReportQueryAsync(factory, client);
        Assert.Contains("handler=CreateQueryResponse", linked, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.QueryResponseJobs.Create}</span>", linked, StringComparison.Ordinal);
        var createForm = AssociationForm(linked, "CreateQueryResponse");
        using var create = await client.PostAsync(
            AssociationAction(createForm, "CreateQueryResponse"),
            new FormUrlEncodedContent(HiddenFields(createForm)));
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var jobs = await scope.ServiceProvider
                .GetRequiredService<IAiJobQueries>()
                .ListOpenForSubjectAsync(caseId, CancellationToken.None);
            var job = Assert.Single(jobs);
            Assert.Equal(AiJobKind.QueryResponse, job.Kind);
            Assert.Equal(AiJobSubjectKind.Case, job.SubjectKind);
            Assert.Equal(caseId, job.SubjectId);
            Assert.Equal("AUTO14001", job.SubjectReference);
            Assert.Equal(messageId.ToString("D"), job.Instruction);
            Assert.Equal(ActorKind.Staff, job.CreatedByKind);
            Assert.Equal(DevelopmentOfflineIdentity.AdministratorId.ToString("D"), job.CreatedBy);
            Assert.Equal(AiJobState.Queued, job.State);
        }

        var rendered = await GetHtmlAsync(client, create.Headers.Location!.ToString());
        Assert.Contains(OperatorLabels.QueryResponseJobs.Created, rendered, StringComparison.Ordinal);

        // The foot reads the live job in place of the start button (FRD-27).
        Guid jobId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            jobId = Assert.Single(await scope.ServiceProvider
                .GetRequiredService<IAiJobQueries>()
                .ListOpenForSubjectAsync(caseId, CancellationToken.None)).JobId;
        }
        Assert.DoesNotContain("handler=CreateQueryResponse", rendered, StringComparison.Ordinal);
        Assert.Contains($"data-query-ai-pending=\"{jobId:D}\"", rendered, StringComparison.Ordinal);
        Assert.Contains("Queued</span>", Between(rendered, $"data-query-ai-pending=\"{jobId:D}\"", "</span>") + "</span>", StringComparison.Ordinal);

        await WriteDraftReplyAsync(factory, jobId, messageId, "Dear Ms Patel, thank you for your e-mail.");
        // Without staff mail the draft is reported as ready; the composer is not offered.
        var ready = await GetHtmlAsync(client, $"/Inbox/{messageId:D}");
        Assert.Contains($"data-query-ai-pending=\"{jobId:D}\"", ready, StringComparison.Ordinal);
        Assert.Contains("Draft ready</span>", Between(ready, $"data-query-ai-pending=\"{jobId:D}\"", "</span>") + "</span>", StringComparison.Ordinal);
        Assert.DoesNotContain("data-query-ai-draft=", ready, StringComparison.Ordinal);
    }

    /// <summary>
    /// One retained message classified Query, linked by staff to a Report sent
    /// Case: the Query response job's source. Returns the record as rendered
    /// after the link.
    /// </summary>
    private static async Task<(Guid MessageId, Guid CaseId, string Linked)> SeedLinkedPostReportQueryAsync(
        IntakeWebApplicationFactory factory, HttpClient client)
    {
        var messageId = Assert.Single(await SeedAsync(
            factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        await StoreMailClassificationAsync(
            factory,
            FirstMailboxId,
            FirstMailboxId + "-0",
            MailClassificationResult.Classified(
                MailCategory.Query,
                [],
                "The retained message is a post-report query.",
                "mail-query-response-test",
                1));
        var receiptId = await ReceiptIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var caseId = await ImageIntakeTestData.SeedCaseAsync(
            factory.Services,
            receiptId,
            "AUTO14001",
            nameof(CaseLifecycleState.ReportSent));

        var linked = await LinkThroughTheRecordAsync(
            client, messageId, "AUTO14001", "The retained post-report query names this Case/PO.");
        return (messageId, caseId, linked);
    }

    /// <summary>
    /// Links a message to a Case in one step, as the record's Link to Case does
    /// (v41 I13): the Case / PO and the reason in one post. Returns the record it lands on.
    /// </summary>
    private static async Task<string> LinkThroughTheRecordAsync(
        HttpClient client,
        Guid messageId,
        string caseReference,
        string reason,
        string context = "")
    {
        var record = await GetHtmlAsync(client, $"/Inbox/{messageId:D}{context}");
        var submission = DialogSubmission(record, "LinkCase", reason, caseReference);
        using var link = await client.PostAsync(
            submission.Action,
            new FormUrlEncodedContent(submission.Fields));
        Assert.Equal(HttpStatusCode.Redirect, link.StatusCode);
        return await GetHtmlAsync(client, link.Headers.Location!.ToString());
    }

    /// <summary>The external client takes the job and writes its draft reply, as the Automation Actor's tools do.</summary>
    private static async Task WriteDraftReplyAsync(
        IntakeWebApplicationFactory factory, Guid jobId, Guid messageId, string text)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IAiJobStore>();
        // The lease is read against the host's clock, which the test host fixes.
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
        var client = ActionActor.Automation("draft-client");
        var job = (await store.GetAsync(jobId, CancellationToken.None))!;
        var taken = await store.TransitionAsync(
            new AiJobTransition(
                job.JobId, job.Version, AiJobState.Taken, client, $"mcp:{Guid.NewGuid():N}",
                LeaseExpiresAtUtc: now + AiJobPolicy.LeaseDuration),
            CancellationToken.None);
        await store.TransitionAsync(
            new AiJobTransition(
                taken.JobId, taken.Version, AiJobState.DraftReady, client, $"mcp:{Guid.NewGuid():N}",
                Result: new AiJobResult(AiJobResultKind.DraftReply, messageId.ToString("D"), text)),
            CancellationToken.None);
    }

    [Fact]
    public async Task QueryResponsePostRefusesAMessageOutsideTheLinkedPostReportSource()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var messageId = Assert.Single(await SeedAsync(
            factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        using var client = IntakeWebDriver.CreateClient(factory);
        var page = await GetHtmlAsync(client, $"/Inbox/{messageId:D}");

        Assert.DoesNotContain("handler=CreateQueryResponse", page, StringComparison.Ordinal);
        using var response = await client.PostAsync(
            $"/Inbox/{messageId:D}?handler=CreateQueryResponse",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = AntiforgeryToken(page),
                ["operationKey"] = $"query-response:{Guid.NewGuid():N}"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "This message is not a linked post-report message.",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.Empty(await context.AiJobs.ToListAsync());
    }

    [Fact]
    public async Task AssociationPostRefusesRolelessAndStaleReviewedStateWithoutWriting()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var messageId = Assert.Single(await SeedAsync(
            factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        await StoreClassificationAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var receiptId = await ReceiptIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var caseId = await ImageIntakeTestData.SeedCaseAsync(
            factory.Services, receiptId, "MAIL31999", nameof(Pegasus.Core.Workflow.CaseLifecycleState.Review));
        using var client = IntakeWebDriver.CreateClient(factory);
        var record = await GetHtmlAsync(client, $"/Inbox/{messageId:D}");
        var submission = DialogSubmission(
            record,
            "LinkCase",
            "Reviewed exact retained-message evidence.",
            "MAIL31999");
        using var rolelessRequest = new HttpRequestMessage(HttpMethod.Post, submission.Action)
        {
            Content = new FormUrlEncodedContent(submission.Fields)
        };
        rolelessRequest.Headers.Add("X-Test-Roleless", "1");
        using var roleless = await client.SendAsync(rolelessRequest);
        Assert.Equal(HttpStatusCode.Forbidden, roleless.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            await context.IntakeReceipts
                .Where(item => item.Id == receiptId)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.Version, item => item.Version + 1));
        }

        using var stale = await client.PostAsync(
            submission.Action,
            new FormUrlEncodedContent(submission.Fields));
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        Assert.Contains(
            "The message or case changed. Reload it, review the current target, and try again.",
            await stale.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationFactory = verificationScope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var verification = await verificationFactory.CreateDbContextAsync();
        Assert.Empty(await verification.IntakeManualAssociations.Where(item => item.IntakeReceiptId == receiptId).ToListAsync());
        Assert.Empty(await verification.IntakeMutationHistory.Where(item => item.IntakeReceiptId == receiptId).ToListAsync());
        Assert.Null((await verification.CaseWorkflows.SingleAsync(item => item.CaseId == caseId)).EditLeaseToken);

        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        var retryLease = await verificationScope.ServiceProvider
            .GetRequiredService<Pegasus.Core.Workflow.IAcquireCaseEditLease>()
            .ExecuteAsync(
                new(caseId, 0, actor, $"mail-test-retry:{Guid.NewGuid():N}"),
                CancellationToken.None);
        Assert.Equal(caseId, retryLease.CaseId);
    }

    [Fact]
    public async Task AnAssociationPostNamesItsMessageItsVersionAndItsAction()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        _ = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 2);
        await StoreClassificationAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        await StoreClassificationAsync(factory, FirstMailboxId, FirstMailboxId + "-1");
        var messageA = await MessageIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var messageB = await MessageIdAsync(factory, FirstMailboxId, FirstMailboxId + "-1");
        var receiptA = await ReceiptIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var receiptB = await ReceiptIdAsync(factory, FirstMailboxId, FirstMailboxId + "-1");
        var caseId = await ImageIntakeTestData.SeedCaseAsync(
            factory.Services, receiptA, "MAIL-BIND", nameof(CaseLifecycleState.Review));
        using var client = IntakeWebDriver.CreateClient(factory);

        var recordA = await GetHtmlAsync(client, $"/Inbox/{messageA:D}");
        var linkA = DialogSubmission(
            recordA,
            "LinkCase",
            "The exact retained message belongs to this Case/PO.",
            "MAIL-BIND");

        // Message B stands at another version, so A's confirmation moved onto B writes nothing.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            await context.IntakeReceipts
                .Where(item => item.Id == receiptB)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.Version, item => item.Version + 1));
        }
        using var crossMessage = await client.PostAsync(
            linkA.Action.Replace(messageA.ToString("D"), messageB.ToString("D"), StringComparison.Ordinal),
            new FormUrlEncodedContent(linkA.Fields));
        Assert.Equal(HttpStatusCode.OK, crossMessage.StatusCode);
        Assert.Contains(
            "The message or case changed",
            await crossMessage.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        await AssertAssociationStateAsync(factory, receiptA, expectedCaseId: null, expectedHistoryCount: 0);
        await AssertAssociationStateAsync(factory, receiptB, expectedCaseId: null, expectedHistoryCount: 0);

        using var linked = await client.PostAsync(
            linkA.Action,
            new FormUrlEncodedContent(linkA.Fields));
        Assert.Equal(HttpStatusCode.Redirect, linked.StatusCode);

        // A link's fields posted to Unlink, the other action, name no linked Case.
        using var linkAsUnlink = await client.PostAsync(
            linkA.Action.Replace("handler=LinkCase", "handler=UnlinkCase", StringComparison.Ordinal),
            new FormUrlEncodedContent(linkA.Fields));
        Assert.Equal(HttpStatusCode.OK, linkAsUnlink.StatusCode);
        Assert.Contains(
            "The message or case changed",
            await linkAsUnlink.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        await AssertAssociationStateAsync(factory, receiptA, caseId, expectedHistoryCount: 1);

        // An unlink's fields posted to Link: the message already holds a Case.
        var linkedPage = await GetHtmlAsync(client, linked.Headers.Location!.ToString());
        var unlink = DialogSubmission(
            linkedPage,
            "UnlinkCase",
            "The exact retained message must be unlinked from this Case/PO.");
        using var unlinkAsLink = await client.PostAsync(
            unlink.Action.Replace("handler=UnlinkCase", "handler=LinkCase", StringComparison.Ordinal),
            new FormUrlEncodedContent(unlink.Fields));
        Assert.Equal(HttpStatusCode.OK, unlinkAsLink.StatusCode);
        Assert.Contains(
            "The message or case changed",
            await unlinkAsLink.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        await AssertAssociationStateAsync(factory, receiptA, caseId, expectedHistoryCount: 1);
    }

    /// <summary>
    /// A link that fails for good after its edit authority was claimed gives the
    /// authority back; when that release itself fails, the form comes back with
    /// its confirmation, so the retry is the same confirmation and settles it.
    /// </summary>
    [Fact]
    public async Task FailedLeaseReleaseRetainsTheExactConfirmationUntilTheRetrySettlesIt()
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var releaseFailures = new ReleaseFailureGate();
        var linkFailures = new ReleaseFailureGate();
        var messageId = Assert.Single(await SeedAsync(
            baseFactory, FirstMailboxId, FirstMailboxAddress, count: 1));
        await StoreClassificationAsync(baseFactory, FirstMailboxId, FirstMailboxId + "-0");
        var receiptId = await ReceiptIdAsync(baseFactory, FirstMailboxId, FirstMailboxId + "-0");
        var caseId = await ImageIntakeTestData.SeedCaseAsync(
            baseFactory.Services, receiptId, "MAIL-RELEASE", nameof(CaseLifecycleState.Review));
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IReleaseCaseEditLease>();
                services.AddScoped<IReleaseCaseEditLease>(services =>
                    new FailOnceReleaseCaseEditLease(
                        services.GetRequiredService<ILeaseCaseForEdit>(),
                        releaseFailures));
                services.RemoveAll<ILinkIntake>();
                services.AddScoped<ILinkIntake>(services =>
                    new FailingFirstLinkIntake(
                        ActivatorUtilities.CreateInstance<LinkIntake>(services),
                        linkFailures));
            }));
        using var client = IntakeWebDriver.CreateClient(factory);
        var record = await GetHtmlAsync(client, $"/Inbox/{messageId:D}");
        var submission = DialogSubmission(
            record,
            "LinkCase",
            "Reviewed exact retained-message evidence.",
            "MAIL-RELEASE");

        using var failedRelease = await client.PostAsync(
            submission.Action,
            new FormUrlEncodedContent(submission.Fields));
        Assert.Equal(HttpStatusCode.OK, failedRelease.StatusCode);
        var retryPage = await failedRelease.Content.ReadAsStringAsync();
        Assert.Contains("edit authority could not be released", retryPage, StringComparison.Ordinal);
        Assert.Equal(1, releaseFailures.AttemptCount);
        Assert.NotNull(await CaseLeaseTokenAsync(baseFactory, caseId));
        await AssertAssociationStateAsync(baseFactory, receiptId, expectedCaseId: null, expectedHistoryCount: 0);

        // The form came back with the same confirmation, so its retry claims the same authority.
        var retry = DialogSubmission(
            retryPage,
            "LinkCase",
            "Reviewed exact retained-message evidence.",
            "MAIL-RELEASE");
        Assert.Equal(submission.Fields["leaseOperationKey"], retry.Fields["leaseOperationKey"]);
        Assert.Equal(submission.Fields["operationKey"], retry.Fields["operationKey"]);
        using var settled = await client.PostAsync(
            retry.Action,
            new FormUrlEncodedContent(retry.Fields));
        Assert.Equal(HttpStatusCode.Redirect, settled.StatusCode);

        await AssertAssociationStateAsync(baseFactory, receiptId, caseId, expectedHistoryCount: 1);
        Assert.Null(await CaseLeaseTokenAsync(baseFactory, caseId));
        Assert.Equal(1, releaseFailures.AttemptCount);
    }

    /// <summary>
    /// The Link to Case options are one button each, whose accessible name holds
    /// the Case / PO, the registration and the claimant, so nothing but the name
    /// is needed to choose. Choosing one writes nothing.
    /// </summary>
    [Fact]
    public async Task TheLinkOptionsAreOneButtonEachWhoseNameContainsEveryVisibleIdentityFact()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "AB12 CDE", "MAIL-A11Y");
        var caseReference = await CaseReferenceAsync(factory, caseId);
        var messageId = Assert.Single(await SeedAsync(
            factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        await StoreClassificationAsync(factory, FirstMailboxId, FirstMailboxId + "-0");

        var html = await GetHtmlAsync(client, $"/Inbox/{messageId:D}?handler=LinkCaseOptions&q=AB12%20CDE");
        var option = Regex.Match(
            html,
            $"<button[^>]*role=\"option\"[^>]*value=\"{Regex.Escape(caseReference)}\"[^>]*>(.*?)</button>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        Assert.True(option.Success, "The matching Case must be one option button.");
        Assert.Single(
            Regex.Matches(html, "role=\"option\"", RegexOptions.CultureInvariant).Cast<Match>());
        var accessibleName = Regex.Replace(
            WebUtility.HtmlDecode(option.Groups[1].Value),
            "<[^>]+>",
            string.Empty,
            RegexOptions.CultureInvariant);
        Assert.Contains(caseReference, accessibleName, StringComparison.Ordinal);
        Assert.Contains("AB12CDE", accessibleName.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("Fixture Claimant", accessibleName, StringComparison.Ordinal);
        await AssertAssociationStateAsync(
            factory,
            await ReceiptIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0"),
            expectedCaseId: null,
            expectedHistoryCount: 0);
    }

    [Fact]
    public async Task TheDefaultViewIsEveryMailboxNewestFirstWithExcerptsAndNoMutation()
    {
        using var factory = new IntakeWebApplicationFactory();
        await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 2);
        await SeedAsync(factory, SecondMailboxId, SecondMailboxAddress, count: 1);
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Inbox");

        Assert.Contains("instructions@collisionengineers.co.uk", html, StringComparison.Ordinal);
        Assert.Contains("reports@collisionengineers.co.uk", html, StringComparison.Ordinal);
        Assert.Contains("Message 1 from instructions", html, StringComparison.Ordinal);
        // The excerpt sits beneath the sender and the subject.
        Assert.Contains("Please inspect the vehicle", html, StringComparison.Ordinal);
        // Unread is a word, not only a weight.
        Assert.Contains(">Unread<", html, StringComparison.Ordinal);

        // Newest first: the second message of the first mailbox is the newest.
        var newest = html.IndexOf("Message 1 from instructions", StringComparison.Ordinal);
        var oldest = html.IndexOf("Message 0 from instructions", StringComparison.Ordinal);
        Assert.True(newest < oldest, "The list must default to newest received first.");

        // Reading changes nothing. The only POST forms are the rows' Dismiss
        // actions (Inbox, 13 September), one per row, and the one bulk form
        // above the list (v41 I18); the shell's dialogs arrive when they open.
        Assert.Equal(3, CountOccurrences(html, "data-mail-row-action=\"dismiss\""));
        // The bulk form and the three dismissals.
        Assert.Equal(1 + 3, CountOccurrences(html, "method=\"post\""));
    }

    /// <summary>
    /// Operator, 12 October 2026: a received message whose mailbox can send
    /// offers Reply on its row, opening the message record's own reply; a message
    /// in a mailbox that cannot send offers none.
    /// </summary>
    [Fact]
    public async Task AMessageInASendingMailboxOffersReplyOnItsRow()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var sendingId = Assert.Single(await SeedAsync(baseFactory, FirstMailboxId, FirstMailboxAddress, count: 1));
        var receivingId = Assert.Single(await SeedAsync(baseFactory, SecondMailboxId, SecondMailboxAddress, count: 1));
        await MakeSendReadyAsync(baseFactory, FirstMailboxId);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<Pegasus.Core.Operations.IStaffMailSend>();
                services.AddSingleton<Pegasus.Core.Operations.IStaffMailSend>(
                    new CaseWebTestSupport.OfferedStaffMailSend());
            }));
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Inbox?selected={sendingId:D}");

        var sendingRow = MailRow(html, sendingId);
        Assert.Contains("data-mail-row-reply", sendingRow, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Inbox/{sendingId:D}?", sendingRow, StringComparison.Ordinal);
        Assert.Contains("compose=reply", sendingRow, StringComparison.Ordinal);
        // The row's Reply opens the composer in the message dialog (script); its address is the record's own.
        Assert.Contains("data-mail-open", Between(sendingRow, "data-mail-row-reply", ">"), StringComparison.Ordinal);
        Assert.Contains("data-mail-row-action=\"dismiss\"", sendingRow, StringComparison.Ordinal);
        var receivingRow = MailRow(html, receivingId);
        Assert.DoesNotContain("data-mail-row-reply", receivingRow, StringComparison.Ordinal);
        Assert.Contains("data-mail-row-action=\"dismiss\"", receivingRow, StringComparison.Ordinal);
    }

    /// <summary>Without staff mail the Inbox offers no Reply, even for a mailbox that can send.</summary>
    [Fact]
    public async Task WithoutStaffMailTheInboxOffersNoReply()
    {
        using var factory = new IntakeWebApplicationFactory();
        var messageId = Assert.Single(await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        await MakeSendReadyAsync(factory, FirstMailboxId);
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Inbox?selected={messageId:D}");

        Assert.DoesNotContain("data-mail-row-reply", MailRow(html, messageId), StringComparison.Ordinal);
    }

    /// <summary>
    /// The row (26 September 2026): the subject opens the message with the
    /// list's query values, the selection mark sits on the row where the shared
    /// highlight rule reads it, and the list carries the row-list keyboard hook.
    /// </summary>
    [Fact]
    public async Task TheSubjectOpensTheMessageAndTheSelectedRowCarriesTheMark()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 2);
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(
            client,
            $"/Inbox?mailbox={FirstMailboxFilter}&sort=oldest&selected={ids[1]:D}");

        Assert.Contains(InboxRowList, html, StringComparison.Ordinal);
        var selectedRow = MailRow(html, ids[1]);
        var otherRow = MailRow(html, ids[0]);
        Assert.Contains("aria-current=\"true\"", selectedRow, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-current", otherRow, StringComparison.Ordinal);

        // The subject anchor targets the message and carries the list's
        // values; it no longer carries a selection or a tooltip of itself.
        var subject = Between(selectedRow, "<a class=\"row-title\"", "</a>");
        Assert.Contains($"href=\"/Inbox/{ids[1]:D}?", subject, StringComparison.Ordinal);
        Assert.Contains($"mailbox={FirstMailboxFilter}", subject, StringComparison.Ordinal);
        Assert.Contains("sort=oldest", subject, StringComparison.Ordinal);
        Assert.DoesNotContain("selected=", subject, StringComparison.Ordinal);
        Assert.DoesNotContain("title=", subject, StringComparison.Ordinal);
        // Script opens it in the dialog; the link is the record page, as it always was.
        Assert.Contains("data-mail-open", subject, StringComparison.Ordinal);
    }

    /// <summary>
    /// The row's excerpt (26 September 2026): the sender's line breaks are
    /// kept, runs of spaces collapse, and it stops after eight lines.
    /// </summary>
    [Fact]
    public async Task TheRowExcerptKeepsTheSendersLineBreaksAndStopsAfterEightLines()
    {
        using var factory = new IntakeWebApplicationFactory();
        await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1);
        var body = string.Join(
            "\n\n",
            Enumerable.Range(1, 10).Select(line => $"Line  {line}   of the message."));
        await StoreSearchProjectionAsync(factory, FirstMailboxId, FirstMailboxId + "-0", body);
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Inbox");
        // The HTML encoder writes each line break and the ellipsis as a
        // character reference; the browser reads them back.
        var excerpt = WebUtility.HtmlDecode(
            Between(html, "<span class=\"row-excerpt\">", "</span>"));

        Assert.Contains("Line 1 of the message.\nLine 2 of the message.", excerpt, StringComparison.Ordinal);
        Assert.EndsWith("Line 8 of the message.…", excerpt, StringComparison.Ordinal);
        Assert.DoesNotContain("Line 9", excerpt, StringComparison.Ordinal);
    }

    /// <summary>
    /// A long single paragraph: the excerpt reads enough of the body to cut
    /// it, so the text stops at a word boundary with an ellipsis rather than
    /// mid-word where the read ran out.
    /// </summary>
    [Fact]
    public async Task ALongParagraphIsCutAtAWordBoundaryWithAnEllipsis()
    {
        using var factory = new IntakeWebApplicationFactory();
        await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1);
        var body = string.Join(' ', Enumerable.Range(1, 300).Select(word => $"word{word:D3}"));
        await StoreSearchProjectionAsync(factory, FirstMailboxId, FirstMailboxId + "-0", body);
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Inbox");
        const string excerptStart = "<span class=\"row-excerpt\">";
        var excerpt = WebUtility.HtmlDecode(Between(html, excerptStart, "</span>")[excerptStart.Length..]);

        Assert.EndsWith("…", excerpt, StringComparison.Ordinal);
        Assert.True(excerpt.Length <= 600, $"The excerpt is {excerpt.Length} characters: {excerpt}");
        var kept = excerpt[..^1];
        Assert.StartsWith(kept, body, StringComparison.Ordinal);
        Assert.Equal(' ', body[kept.Length]);
    }

    /// <summary>
    /// The row's attachments (26 September 2026): one count, no names. The full
    /// message still names every file.
    /// </summary>
    [Fact]
    public async Task TheRowCountsAttachmentsWhileTheMessageNamesThem()
    {
        using var factory = new IntakeWebApplicationFactory();
        var messageId = Assert.Single(await SeedAsync(
            factory,
            FirstMailboxId,
            FirstMailboxAddress,
            count: 1,
            attachments:
            [
                new("front.jpg", "image/jpeg", 4096),
                new("rear.png", "image/png", 4096),
                new("estimate.pdf", "application/pdf", 2048)
            ]));
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Inbox");
        var row = MailRow(html, messageId);
        Assert.Contains("3 attachments", row, StringComparison.Ordinal);
        Assert.DoesNotContain("front.jpg", row, StringComparison.Ordinal);
        Assert.DoesNotContain("estimate.pdf", row, StringComparison.Ordinal);

        var attachments = await GetHtmlAsync(client, $"/Inbox/{messageId:D}?section=attachments");
        Assert.Contains("front.jpg", attachments, StringComparison.Ordinal);
        Assert.Contains("rear.png", attachments, StringComparison.Ordinal);
        Assert.Contains("estimate.pdf", attachments, StringComparison.Ordinal);
    }

    /// <summary>
    /// A message that opened a Triage (26 September 2026): the chip reads
    /// Triage and the Case cell carries the t. reference, on the row and, once,
    /// in the record's Decision.
    /// </summary>
    [Fact]
    public async Task AMessageThatOpenedATriageShowsItsReference()
    {
        using var factory = new IntakeWebApplicationFactory();
        var messageId = Assert.Single(await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        await StoreOutcomeReceiptAsync(factory, FirstMailboxId, FirstMailboxId + "-0", IntakeDecision.NeedsSorting);
        var receiptId = await ReceiptIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var triageCaseId = await MailboxIntakeTestData.SeedTriageCaseAsync(factory.Services, receiptId, "t.QDOS31007");
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Inbox");

        var row = MailRow(html, messageId);
        Assert.Contains(">Triage</span>", row, StringComparison.Ordinal);
        Assert.Contains($"<a href=\"/Cases/{triageCaseId:D}\">t.QDOS31007</a>", row, StringComparison.Ordinal);

        var record = await GetHtmlAsync(client, $"/Inbox/{messageId:D}");
        Assert.Contains($"<span>{OperatorLabels.Inbox.Case}</span>", record, StringComparison.Ordinal);
        Assert.Contains($"<strong><a href=\"/Cases/{triageCaseId:D}\">t.QDOS31007</a></strong>", record, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Triage flag follows the Case's own type, not how the message came
    /// to it: a message staff linked to a Triage Case it did not open shows
    /// the Triage chip too, and its record offers Unlink and Change linked Case.
    /// </summary>
    [Fact]
    public async Task AMessageLinkedToATriageCaseShowsItAsATriage()
    {
        using var factory = new IntakeWebApplicationFactory();
        var messageId = Assert.Single(await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        await StoreOutcomeReceiptAsync(factory, FirstMailboxId, FirstMailboxId + "-0", IntakeDecision.NeedsSorting);
        var receiptId = await ReceiptIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var triageCaseId = await MailboxIntakeTestData.SeedTriageCaseAsync(factory.Services, originReceiptId: null, "t.QDOS31008");
        await MailboxIntakeTestData.LinkReceiptAsync(factory.Services, receiptId, triageCaseId);
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Inbox");
        Assert.Contains(">Triage</span>", MailRow(html, messageId), StringComparison.Ordinal);

        var record = await GetHtmlAsync(client, $"/Inbox/{messageId:D}");
        Assert.Contains($"<strong><a href=\"/Cases/{triageCaseId:D}\">t.QDOS31008</a></strong>", record, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.Inbox.Unlink}</button>", record, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.Inbox.ChangeLinkedCase}</button>", record, StringComparison.Ordinal);
    }

    /// <summary>
    /// The full message agrees with itself: a message that opened a Triage and
    /// is linked to no Case shows that Triage as its Case, with its t.
    /// reference, and Link to Case stays available because linking to an
    /// instruction Case is a separate act.
    /// </summary>
    [Fact]
    public async Task TheRecordShowsTheTriageTheMessageOpenedAndStillOffersLinkToCase()
    {
        using var factory = new IntakeWebApplicationFactory();
        var messageId = Assert.Single(await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        await StoreOutcomeReceiptAsync(factory, FirstMailboxId, FirstMailboxId + "-0", IntakeDecision.NeedsSorting);
        var receiptId = await ReceiptIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var triageCaseId = await MailboxIntakeTestData.SeedTriageCaseAsync(factory.Services, receiptId, "t.QDOS31010");
        using var client = IntakeWebDriver.CreateClient(factory);

        var record = await GetHtmlAsync(client, $"/Inbox/{messageId:D}");

        Assert.Contains($"<a href=\"/Cases/{triageCaseId:D}\">t.QDOS31010</a>", record, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.Inbox.LinkToCase}</button>", record, StringComparison.Ordinal);
        Assert.Contains("id=\"mailLinkCaseReference\"", record, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompoundOutcomeLabelsStayExactAndGreenInRows()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 2);
        await StoreOutcomeReceiptAsync(factory, FirstMailboxId, FirstMailboxId + "-0", IntakeDecision.CaseCreated);
        await StoreOutcomeReceiptAsync(factory, FirstMailboxId, FirstMailboxId + "-1", IntakeDecision.ImageIntakeRegistered);
        using var client = IntakeWebDriver.CreateClient(factory);

        var outcomes = new[]
        {
            (MessageId: ids[1], Label: "Ready for case allocation"),
            (MessageId: ids[0], Label: "Vehicle images registered")
        };
        foreach (var outcome in outcomes)
        {
            var html = await GetHtmlAsync(client, $"/Inbox?selected={outcome.MessageId:D}");
            var row = MailRow(html, outcome.MessageId);
            Assert.Contains($">{outcome.Label}<", row, StringComparison.Ordinal);
            Assert.Contains("class=\"status status--green\"", row, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AMessageThatFoundNoCaseReadsNoCaseFoundInAmber()
    {
        using var factory = new IntakeWebApplicationFactory();
        var messageId = Assert.Single(await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        await StoreOutcomeReceiptAsync(factory, FirstMailboxId, FirstMailboxId + "-0", IntakeDecision.NeedsSorting);
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Inbox?selected={messageId:D}");

        var row = MailRow(html, messageId);
        Assert.Contains($">{OperatorLabels.Inbox.NoCaseFound}<", row, StringComparison.Ordinal);
        Assert.Contains("class=\"status status--amber\"", row, StringComparison.Ordinal);
        Assert.DoesNotContain(">Unidentified<", row, StringComparison.Ordinal);
    }

    /// <summary>
    /// The list's scope rail (Inbox, 13 September; v41 I1): one count per scope,
    /// no Unread scope, the five scopes with Deleted Items last, and the sort
    /// toggle as a server-side flip.
    /// </summary>
    [Fact]
    public async Task TheScopeRailCountsEachScopeHasNoUnreadScopeAndTheSortToggleFlipsOrder()
    {
        using var factory = new IntakeWebApplicationFactory();
        await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 3);
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Inbox");

        // Every planned scope renders once, with its count; Unread is not a scope.
        Assert.Contains(">All incoming</span>", html, StringComparison.Ordinal);
        // The scope label renders as <span>Label</span>; an unread row's sr-only
        // "Unread" word is not a scope.
        Assert.DoesNotContain("<span>Unread</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Receiving work</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Case updates</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Pre-instructions</span>", html, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.Inbox.NoCaseFound}</span>", html, StringComparison.Ordinal);
        Assert.Contains(">Sent Items</span>", html, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.Inbox.DismissedScope}</span>", html, StringComparison.Ordinal);
        // Deleted Items is the fifth scope (v41 I1); there is no Folder select beside the rail.
        Assert.Contains($">{OperatorLabels.Inbox.DeletedItemsScope}</span>", html, StringComparison.Ordinal);
        Assert.Equal(5, CountOccurrences(html, "class=\"scope-button\""));
        // Three retained inbox messages, none read.
        Assert.Contains("<span class=\"tabular\">3</span>", html, StringComparison.Ordinal);
        Assert.Contains("Message 2 from instructions", html, StringComparison.Ordinal);

        // The Dismissed scope is a real query: nothing dismissed yet, so it is empty
        // and pressed, and the incoming rows are not in it.
        var dismissed = await GetHtmlAsync(client, "/Inbox?folder=dismissed");
        Assert.Contains("No dismissed messages.", dismissed, StringComparison.Ordinal);
        Assert.DoesNotContain("Message 2 from instructions", dismissed, StringComparison.Ordinal);
        Assert.Contains("aria-pressed=\"true\"", dismissed, StringComparison.Ordinal);

        // The sort toggle flips the received order server-side. The arrow is
        // rendered through the HTML encoder (an expression, not markup text),
        // so the page carries the entity form of the glyph; the literal
        // chunk is "Received " followed by that entity.
        var oldest = await GetHtmlAsync(client, "/Inbox?sort=oldest");
        var newestIndex = oldest.IndexOf("Message 2 from instructions", StringComparison.Ordinal);
        var middleIndex = oldest.IndexOf("Message 1 from instructions", StringComparison.Ordinal);
        Assert.True(newestIndex > middleIndex, "sort=oldest must list the newest message last.");
        Assert.Contains("Received &#x2191;", oldest, StringComparison.Ordinal);
        Assert.DoesNotContain("&#x2193;", oldest, StringComparison.Ordinal);
        Assert.Contains("sort=oldest", oldest, StringComparison.Ordinal);
        var newest = await GetHtmlAsync(client, "/Inbox");
        Assert.Contains("Received &#x2193;", newest, StringComparison.Ordinal);

        // An unknown sort or folder value is refused.
        using var badSort = await client.GetAsync("/Inbox?sort=newest-first");
        using var badFolder = await client.GetAsync("/Inbox?folder=unread");
        Assert.Equal(HttpStatusCode.NotFound, badSort.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, badFolder.StatusCode);
    }

    [Fact]
    public async Task ScopingAndPagingCarryTheMailboxFolderAndPageForward()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 30);
        using var client = IntakeWebDriver.CreateClient(factory);

        var scoped = await GetHtmlAsync(client, $"/Inbox?mailbox={FirstMailboxFilter}");

        Assert.Contains($"/Inbox?mailbox={FirstMailboxFilter}&amp;pageNumber=2", scoped, StringComparison.Ordinal);
        Assert.Contains("Page 1 of 2", scoped, StringComparison.Ordinal);

        var secondPage = await GetHtmlAsync(client, $"/Inbox?mailbox={FirstMailboxFilter}&pageNumber=2");
        Assert.Contains("Page 2 of 2", secondPage, StringComparison.Ordinal);
        // The row link carries the exact list position back into detail.
        Assert.Contains($"mailbox={FirstMailboxFilter}&amp;pageNumber=2", secondPage, StringComparison.Ordinal);

        // The oldest-first order survives the message round-trip: the pane's
        // full-detail entry opens the message with it, Back returns to the same
        // scope and order, and so does every section tab.
        var oldest = await GetHtmlAsync(
            client,
            $"/Inbox?mailbox={FirstMailboxFilter}&sort=oldest&pageNumber=2");
        Assert.Contains("sort=oldest&amp;pageNumber=2", oldest, StringComparison.Ordinal);

        var detail = await GetHtmlAsync(
            client,
            $"/Inbox/{ids[0]:D}?mailbox={FirstMailboxFilter}&sort=oldest&pageNumber=2");
        Assert.Contains(
            $"/Inbox?mailbox={FirstMailboxFilter}&amp;sort=oldest&amp;pageNumber=2",
            detail,
            StringComparison.Ordinal);
        Assert.Contains("sort=oldest&amp;section=attachments", detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheRefreshFormCarriesTheActiveFilterAndPage()
    {
        using var factory = new IntakeWebApplicationFactory();
        await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 30);
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Inbox?mailbox={FirstMailboxFilter}&pageNumber=2");

        // Refresh reruns the query the operator is looking at. A bare GET form
        // submits nothing and silently resets the screen to page one of
        // everything, which the requirement forbids.
        var form = Between(html, "<form method=\"get\" data-refresh-form>", "</form>");
        Assert.Contains(
            $"name=\"mailbox\" value=\"{FirstMailboxFilter}\"",
            form,
            StringComparison.Ordinal);
        Assert.Contains("name=\"pageNumber\" value=\"2\"", form, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FreshnessReportsTheLastSuccessfulPollAndTurnsStale()
    {
        var clock = new MovableTimeProvider(NowUtc);
        using var factory = new IntakeWebApplicationFactory(clock);
        await SeedAsync(
            factory,
            FirstMailboxId,
            FirstMailboxAddress,
            count: 1,
            lastCompletedAtUtc: NowUtc.AddMinutes(-1));
        using var client = IntakeWebDriver.CreateClient(factory);

        var current = await GetHtmlAsync(client, "/Inbox");
        Assert.Contains("Current ·", current, StringComparison.Ordinal);
        Assert.DoesNotContain(">Stale<", current, StringComparison.Ordinal);

        clock.Advance(GetRetainedMailFreshness.StaleAfter + TimeSpan.FromMinutes(1));
        var stale = await GetHtmlAsync(client, "/Inbox");

        Assert.Contains(">Stale<", stale, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMailboxThatHasNeverPolledReportsUnavailableRatherThanAnOldTime()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Inbox");

        Assert.Contains(OperatorLabels.Freshness.Never, html, StringComparison.Ordinal);
        Assert.Contains(">Unavailable<", html, StringComparison.Ordinal);
        Assert.Contains("No mail has been received.", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Sent Items lists the Sent items the Sent poll retained (FRD-20), and
    /// only those; a received message stays out of it. Deleted Items is still
    /// not kept.
    /// </summary>
    [Fact]
    public async Task SentListsTheRetainedSentItemsAndDeletedListsNothingUntilItIsSearched()
    {
        using var factory = new IntakeWebApplicationFactory();
        await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<EfRetainedMailboxMessageStore>().RetainAsync(
                new(
                    TestMailboxId.From(FirstMailboxId),
                    FirstMailboxAddress,
                    "sent-item-1",
                    $"{FirstMailboxId.Length}:{FirstMailboxId}sent-item-1",
                    NowUtc.AddMinutes(-2),
                    1024,
                    new string('B', 64),
                    new(
                        "sent-items",
                        $"conversation-{FirstMailboxId}-sent",
                        "<e101bc7b9d6b4c519594a4c74b4c1e91@pegasus.invalid>",
                        [],
                        FirstMailboxAddress,
                        null,
                        ["claimant@example.invalid"],
                        [],
                        [],
                        "Following up on your claim",
                        "Please find the update below.",
                        [],
                        IsRead: true),
                    NowUtc,
                    MailFolderScope.Sent),
                CancellationToken.None);
            // The Sent copy of a message the mailbox also received (here, Inbox
            // message 0) is its own Sent row, not a contradiction of the Inbox row.
            await scope.ServiceProvider.GetRequiredService<EfRetainedMailboxMessageStore>().RetainAsync(
                new(
                    TestMailboxId.From(FirstMailboxId),
                    FirstMailboxAddress,
                    "sent-copy-of-inbox-0",
                    $"{FirstMailboxId.Length}:{FirstMailboxId}sent-copy-of-inbox-0",
                    NowUtc.AddMinutes(-3),
                    1024,
                    new string('C', 64),
                    new(
                        "sent-items",
                        $"conversation-{FirstMailboxId}",
                        $"<{FirstMailboxId}-0@example.invalid>",
                        [],
                        FirstMailboxAddress,
                        null,
                        ["intake@collisionengineers.co.uk"],
                        [],
                        [],
                        "Sent copy of the received message",
                        "Please inspect the vehicle at the address supplied.",
                        [],
                        IsRead: true),
                    NowUtc,
                    MailFolderScope.Sent),
                CancellationToken.None);
        }
        using var client = IntakeWebDriver.CreateClient(factory);

        var sent = await GetHtmlAsync(client, "/Inbox?folder=sent");
        var inbox = await GetHtmlAsync(client, "/Inbox");
        var deleted = await GetHtmlAsync(client, "/Inbox?folder=deleted");

        Assert.Contains("Sent copy of the received message", sent, StringComparison.Ordinal);
        Assert.Contains($"Message 0 from {FirstMailboxId}", inbox, StringComparison.Ordinal);
        // Sent search reads the retained subject, sender and text; Sent has no
        // categories, as Deleted Items has none.
        var searched = await GetHtmlAsync(client, "/Inbox?folder=sent&search=Following");
        Assert.Contains("Following up on your claim", searched, StringComparison.Ordinal);
        Assert.DoesNotContain("Sent copy of the received message", searched, StringComparison.Ordinal);
        Assert.Matches("<select id=\"queue-filter\" name=\"queue\" disabled=\"disabled\"", sent);
        using var categorised = await client.GetAsync("/Inbox?folder=sent&queue=triage-request");
        Assert.Equal(HttpStatusCode.NotFound, categorised.StatusCode);

        Assert.Contains("Following up on your claim", sent, StringComparison.Ordinal);
        Assert.DoesNotContain($"Message 0 from {FirstMailboxId}", sent, StringComparison.Ordinal);
        Assert.DoesNotContain("not kept in Pegasus yet", sent, StringComparison.Ordinal);
        // A Sent item is not received work: no processing chip on the row.
        Assert.DoesNotContain("Not yet processed", sent, StringComparison.Ordinal);
        Assert.Contains("Deleted items messages are not kept in Pegasus yet.", deleted, StringComparison.Ordinal);
        // MAIL-010: this used to assert the sentence that told the operator to
        // search. The sentence was a field hint and is gone; what mattered was
        // always the behaviour it described, so assert that instead — Deleted
        // Items lists nothing until a search term is given.
        Assert.DoesNotContain($"Message 0 from {FirstMailboxId}", deleted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticatedDeletedSearchUsesTheSelectedApprovedMailboxAndRendersBoundsPagingAndUnavailableState()
    {
        var source = new RecordingDeletedMailSearchSource();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDeletedMailSearchSource>();
                services.AddSingleton<IDeletedMailSearchSource>(source);
            }));
        using var client = IntakeWebDriver.CreateClient(factory);

        var firstPage = await GetHtmlAsync(
            client,
            "/Inbox?folder=deleted&mailbox=11111111-1111-1111-1111-111111111111&search=needle");

        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), source.MailboxId);
        Assert.Equal("needle", source.SearchTerm);
        Assert.Equal(100, source.MaximumMessages);
        Assert.Contains("empty@example.invalid", firstPage, StringComparison.Ordinal);
        Assert.Contains("Deleted match 25", firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Deleted match 0", firstPage, StringComparison.Ordinal);
        Assert.Contains("Attachment content: proof.pdf (attachment 1)", firstPage, StringComparison.Ordinal);
        Assert.Contains("checked the 100 newest Deleted Items", firstPage, StringComparison.Ordinal);
        Assert.Contains("pageNumber=2", firstPage, StringComparison.Ordinal);
        // Deleted Items rows have no link to take focus, so the pane is not a
        // row list: the arrow keys scroll it.
        Assert.DoesNotContain(InboxRowList, firstPage, StringComparison.Ordinal);

        var secondPage = await GetHtmlAsync(
            client,
            "/Inbox?folder=deleted&mailbox=11111111-1111-1111-1111-111111111111&search=needle&paged=true&pageNumber=2");
        Assert.Contains("Deleted match 0", secondPage, StringComparison.Ordinal);
        Assert.Contains("Page 2 of 2", secondPage, StringComparison.Ordinal);
        // Next carries the paging marker and reused the kept result: one search for both pages.
        Assert.Contains("paged=true", firstPage, StringComparison.Ordinal);
        Assert.Equal(1, source.SearchCount);

        // Refresh on page 2 carries the page but not the marker: it reads the source again.
        var refreshed = await GetHtmlAsync(
            client,
            "/Inbox?folder=deleted&mailbox=11111111-1111-1111-1111-111111111111&search=needle&pageNumber=2");
        Assert.Contains("Page 2 of 2", refreshed, StringComparison.Ordinal);
        Assert.Equal(2, source.SearchCount);

        // The same search again, once the source is unavailable, shows the notice.
        source.Result = new([], false, DeletedMailSearchState.Unavailable);
        var unavailable = await GetHtmlAsync(
            client,
            "/Inbox?folder=deleted&mailbox=11111111-1111-1111-1111-111111111111&search=needle");
        Assert.Equal(3, source.SearchCount);
        Assert.Contains(
            "Deleted Items search is unavailable. Retained Inbox mail remains available.",
            unavailable,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticatedDeletedSearchRendersUnavailableWhenCredentialAcquisitionFails()
    {
        var source = new GraphDeletedMailSearchSource(
            new GraphMailClient(
                new FailingCredential(),
                new Uri("https://graph.microsoft.com/v1.0/"),
                new HttpClient(new UnexpectedHttpHandler())),
            new ApprovedMailboxEstate(
                [new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "empty-mailbox", "empty@example.invalid", "inbox-folder", DateTimeOffset.MinValue)]),
            new Pegasus.Infrastructure.Intake.MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System));
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDeletedMailSearchSource>();
                services.AddSingleton<IDeletedMailSearchSource>(source);
            }));
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(
            client,
            "/Inbox?folder=deleted&mailbox=11111111-1111-1111-1111-111111111111&search=needle");

        Assert.Contains(
            "Deleted Items search is unavailable. Retained Inbox mail remains available.",
            html,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("folder-root")]
    [InlineData("missing-value")]
    [InlineData("relative-next-link")]
    public async Task AuthenticatedDeletedSearchRendersUnavailableForMalformedGraphShapes(
        string responseCase)
    {
        var source = new GraphDeletedMailSearchSource(
            new GraphMailClient(
                new SuccessfulCredential(),
                new Uri("https://graph.microsoft.com/v1.0/"),
                new HttpClient(new MalformedDeletedGraphHandler(responseCase))),
            new ApprovedMailboxEstate(
                [new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "empty-mailbox", "empty@example.invalid", "inbox-folder", DateTimeOffset.MinValue)]),
            new Pegasus.Infrastructure.Intake.MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System));
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDeletedMailSearchSource>();
                services.AddSingleton<IDeletedMailSearchSource>(source);
            }));
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(
            client,
            "/Inbox?folder=deleted&mailbox=11111111-1111-1111-1111-111111111111&search=needle");

        Assert.Contains(
            "Deleted Items search is unavailable. Retained Inbox mail remains available.",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchFiltersRetainedRowsAndCarriesTheTermThroughPagingAndDetail()
    {
        using var factory = new IntakeWebApplicationFactory();
        await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 30);
        for (var index = 0; index < 30; index++)
        {
            await StoreSearchProjectionAsync(
                factory,
                FirstMailboxId,
                $"{FirstMailboxId}-{index}",
                "Please inspect the vehicle at the address supplied.");
        }
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Inbox?search=inspect");

        Assert.Contains("name=\"search\" value=\"inspect\"", html, StringComparison.Ordinal);
        Assert.Contains("search=inspect&amp;pageNumber=2", html, StringComparison.Ordinal);
        Assert.Contains("Matched in: Message body", html, StringComparison.Ordinal);
        Assert.Contains("search=inspect", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchDistinguishesNoMatchAndInvalidInputFromNoReceivedMail()
    {
        using var factory = new IntakeWebApplicationFactory();
        await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1);
        using var client = IntakeWebDriver.CreateClient(factory);

        var noMatch = await GetHtmlAsync(client, "/Inbox?search=definitely-not-present");
        await GetHtmlAsync(client, "/Inbox?search=%20%20%20");
        var overlong = await GetHtmlAsync(client, $"/Inbox?search={new string('x', 201)}");

        Assert.Contains("No emails in this mailbox matched", noMatch, StringComparison.Ordinal);
        Assert.DoesNotContain("No mail has been received.", noMatch, StringComparison.Ordinal);
        Assert.Contains("Search terms must be 200 characters or fewer.", overlong, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticatedMailViewsAreDistinctAccessibleAndPreservedThroughDetail()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 6);
        var classifications = new[]
        {
            MailClassificationResult.Classified(MailCategory.NewInstruction, [], "fixture", "test", 1),
            MailClassificationResult.Classified(MailCategory.Query, [], "fixture", "test", 1),
            MailClassificationResult.Classified(MailCategory.Billing, [], "fixture", "test", 1),
            MailClassificationResult.Unclassified([], "fixture", "test", 1),
            MailClassificationResult.Classified(MailCategory.TriageRequest, [], "fixture", "test", 1),
            MailClassificationResult.Classified(MailCategory.General, [], "fixture", "test", 1)
        };
        for (var index = 0; index < classifications.Length; index++)
        {
            await StoreMailClassificationAsync(
                factory,
                FirstMailboxId,
                $"{FirstMailboxId}-{index}",
                classifications[index]);
        }
        // Message 0 stands linked to a Case, so No Case Found leaves it out.
        var linkedReceiptId = await ReceiptIdAsync(factory, FirstMailboxId, $"{FirstMailboxId}-0");
        var linkedCaseId = await ImageIntakeTestData.SeedCaseAsync(
            factory.Services,
            linkedReceiptId,
            "MAIL-VIEWS-LINK",
            nameof(CaseLifecycleState.Review));
        await MailboxIntakeTestData.LinkReceiptAsync(factory.Services, linkedReceiptId, linkedCaseId);
        using var client = IntakeWebDriver.CreateClient(factory);

        var receiving = await GetHtmlAsync(client, "/Inbox?queue=new-instruction");
        // Category, not Queue (Inbox, 13 September).
        Assert.Contains($"<label for=\"queue-filter\">{OperatorLabels.Inbox.Category}</label>", receiving, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.Inbox.AllCategories}</option>", receiving, StringComparison.Ordinal);
        Assert.DoesNotContain(">Queue</label>", receiving, StringComparison.Ordinal);
        // One flat list: All categories, then the eleven categories in order. No Case
        // Found is a scope on the rail, not a category (v41 I1).
        Assert.DoesNotContain("<optgroup", receiving, StringComparison.Ordinal);
        var categorySelect = Between(receiving, "<select id=\"queue-filter\"", "</select>");
        Assert.Equal(
            CategoryFilterValues,
            Regex.Matches(categorySelect, "<option value=\"(?<value>[^\"]*)\"")
                .Select(match => match.Groups["value"].Value)
                .ToArray());
        Assert.Contains(">Not client related</option>", categorySelect, StringComparison.Ordinal);
        Assert.DoesNotContain("Current view:", receiving, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"field-hint\"", receiving, StringComparison.Ordinal);
        // One selected option per filter-bar select: mailbox and category.
        Assert.Equal(2, CountOccurrences(receiving, " selected=\"selected\""));
        Assert.Contains($"/Inbox/{ids[5]:D}?queue=new-instruction", receiving, StringComparison.Ordinal);
        Assert.Contains("<span class=\"row-meta\">New instruction</span>", receiving, StringComparison.Ordinal);
        Assert.DoesNotContain("Message 1 from instructions", receiving, StringComparison.Ordinal);

        foreach (var (key, included, excluded) in new[]
        {
            ("query", "Message 1 from instructions", "Message 0 from instructions"),
            ("billing", "Message 2 from instructions", "Message 1 from instructions"),
            ("no_case_found", "Message 3 from instructions", "Message 0 from instructions"),
            ("triage-request", "Message 4 from instructions", "Message 3 from instructions"),
            ("general", "Message 5 from instructions", "Message 0 from instructions")
        })
        {
            var html = await GetHtmlAsync(client, $"/Inbox?queue={Uri.EscapeDataString(key)}");
            Assert.Contains(included, html, StringComparison.Ordinal);
            Assert.DoesNotContain(excluded, html, StringComparison.Ordinal);
        }
        // No Case Found reads the link, not the category: every unlinked
        // message is there, classified or not.
        var noCaseFound = await GetHtmlAsync(client, "/Inbox?queue=no_case_found");
        foreach (var unlinked in new[] { 1, 2, 4, 5 })
        {
            Assert.Contains($"Message {unlinked} from instructions", noCaseFound, StringComparison.Ordinal);
        }
        // The Category select is drawn enabled in No Case Found (v41 inbox-no-case-found):
        // its All categories option holds the scope's own key, so a search stays in the
        // scope, and the eleven categories follow.
        Assert.Contains("<select id=\"queue-filter\" name=\"queue\">", noCaseFound, StringComparison.Ordinal);
        string[] noCaseFoundValues = ["no_case_found", .. CategoryFilterValues.Skip(1)];
        Assert.Equal(
            noCaseFoundValues,
            Regex.Matches(Between(noCaseFound, "<select id=\"queue-filter\"", "</select>"), "<option value=\"(?<value>[^\"]*)\"")
                .Select(match => match.Groups["value"].Value)
                .ToArray());
        Assert.Contains(
            $"<option value=\"no_case_found\" selected=\"selected\">{OperatorLabels.Inbox.AllCategories}</option>",
            noCaseFound,
            StringComparison.Ordinal);

        var detail = await GetHtmlAsync(
            client,
            $"/Inbox/{ids[5]:D}?queue=new-instruction&pageNumber=2");
        Assert.Contains("/Inbox?queue=new-instruction&amp;pageNumber=2", detail, StringComparison.Ordinal);
        // Every section tab carries the whole list context forward.
        Assert.Contains("queue=new-instruction&amp;section=attachments", detail, StringComparison.Ordinal);
        using var deleted = await client.GetAsync("/Inbox?folder=deleted&queue=triage-request");
        using var deletedDetail = await client.GetAsync($"/Inbox/{ids[5]:D}?folder=deleted&queue=triage-request");
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deletedDetail.StatusCode);
        Assert.DoesNotContain("Needs sorting", receiving, StringComparison.OrdinalIgnoreCase);

        var emptyView = await GetHtmlAsync(
            client,
            "/Inbox?queue=cancellation");
        Assert.DoesNotContain("No retained mail is currently in", emptyView, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidMailViewContextStopsEveryExactMessagePostBeforeMutation(
        bool deletedItemsContext)
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var baseFactory = factory;
        _ = await SeedAsync(baseFactory, FirstMailboxId, FirstMailboxAddress, count: 3);
        for (var index = 0; index < 3; index++)
        {
            await StoreClassifiedInstructionAsync(
                baseFactory,
                FirstMailboxId,
                $"{FirstMailboxId}-{index}");
        }
        var linkMessageId = await MessageIdAsync(baseFactory, FirstMailboxId, FirstMailboxId + "-0");
        var unlinkMessageId = await MessageIdAsync(baseFactory, FirstMailboxId, FirstMailboxId + "-1");
        var actionMessageId = await MessageIdAsync(baseFactory, FirstMailboxId, FirstMailboxId + "-2");
        var linkReceiptId = await ReceiptIdAsync(baseFactory, FirstMailboxId, FirstMailboxId + "-0");
        var unlinkReceiptId = await ReceiptIdAsync(baseFactory, FirstMailboxId, FirstMailboxId + "-1");
        var actionReceiptId = await ReceiptIdAsync(baseFactory, FirstMailboxId, FirstMailboxId + "-2");
        var linkCaseId = await ImageIntakeTestData.SeedCaseAsync(
            baseFactory.Services,
            linkReceiptId,
            "MAIL-CONTEXT-LINK",
            nameof(CaseLifecycleState.Review));
        var unlinkCaseId = await ImageIntakeTestData.SeedCaseAsync(
            baseFactory.Services,
            unlinkReceiptId,
            "MAIL-CONTEXT-UNLINK",
            nameof(CaseLifecycleState.Review));
        using var client = IntakeWebDriver.CreateClient(factory);

        string Forge(string action)
        {
            Assert.Contains("queue=new-instruction", action, StringComparison.Ordinal);
            return deletedItemsContext
                ? $"{action}&folder=deleted"
                : action.Replace("queue=new-instruction", "queue=needs-sorting", StringComparison.Ordinal);
        }

        // One post names its context: a forged one stops before the Case's authority
        // is claimed, so nothing is written and nothing is left held.
        var linkRecord = await GetHtmlAsync(client, $"/Inbox/{linkMessageId:D}?queue=new-instruction");
        var linkSubmission = DialogSubmission(
            linkRecord,
            "LinkCase",
            "The exact retained message belongs to this Case/PO.",
            "MAIL-CONTEXT-LINK");
        await PostNotFoundAsync(
            client,
            Forge(linkSubmission.Action),
            linkSubmission.Fields);
        await AssertAssociationStateAsync(baseFactory, linkReceiptId, expectedCaseId: null, expectedHistoryCount: 0);
        Assert.Null(await CaseLeaseTokenAsync(baseFactory, linkCaseId));

        var linkedPage = await LinkThroughTheRecordAsync(
            client,
            unlinkMessageId,
            "MAIL-CONTEXT-UNLINK",
            "The exact retained message belongs to this Case/PO.",
            "?queue=new-instruction");
        var unlinkSubmission = DialogSubmission(
            linkedPage,
            "UnlinkCase",
            "The message belongs to a different Case/PO.");
        await PostNotFoundAsync(
            client,
            Forge(unlinkSubmission.Action),
            unlinkSubmission.Fields);
        await AssertAssociationStateAsync(baseFactory, unlinkReceiptId, unlinkCaseId, expectedHistoryCount: 1);
        Assert.Null(await CaseLeaseTokenAsync(baseFactory, unlinkCaseId));

        var actionPage = await GetHtmlAsync(
            client,
            $"/Inbox/{actionMessageId:D}?queue=new-instruction");
        var correctionFields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiforgeryToken(actionPage),
            ["ExpectedClassificationVersion"] = "1",
            ["ClassificationKey"] = "general",
            ["CorrectionReason"] = MailClassificationSelection.CorrectedFromInboxReason
        };
        await PostNotFoundAsync(
            client,
            Forge(ChipFormAction(actionPage)),
            correctionFields);

        await using var scope = baseFactory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.Equal(
            1,
            await context.IntakeMailClassificationDecisions
                .Where(item => item.IntakeReceiptId == actionReceiptId)
                .Select(item => item.Version)
                .SingleAsync());
        Assert.Empty(await context.IntakeMailClassificationHistory
            .Where(item => item.IntakeReceiptId == actionReceiptId)
            .ToListAsync());
    }

    /// <summary>
    /// Back to Inbox carries the message it leaves as the selection, so the
    /// list returns with that row pinned rather than its first row.
    /// </summary>
    [Fact]
    public async Task BackToInboxPinsTheMessageItLeft()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 2);
        using var client = IntakeWebDriver.CreateClient(factory);
        var list = await GetHtmlAsync(client, $"/Inbox?mailbox={FirstMailboxFilter}");
        var left = MailRow(list, ids[0]).Contains("aria-current", StringComparison.Ordinal) ? ids[1] : ids[0];
        var other = left == ids[0] ? ids[1] : ids[0];

        var message = await GetHtmlAsync(client, $"/Inbox/{left:D}?mailbox={FirstMailboxFilter}");
        var back = WebUtility.HtmlDecode(Regex.Match(
            message,
            "<a class=\"btn\" href=\"(?<href>/Inbox\\?[^\"]*)\">\\s*<svg class=\"icon\" aria-hidden=\"true\"><use href=\"[^\"]*#icon-arrow-right\" />").Groups["href"].Value);
        Assert.Contains($"mailbox={FirstMailboxFilter}", back, StringComparison.Ordinal);
        Assert.Contains($"selected={left:D}", back, StringComparison.Ordinal);

        var returned = await GetHtmlAsync(client, back);
        Assert.Contains("aria-current=\"true\"", MailRow(returned, left), StringComparison.Ordinal);
        Assert.DoesNotContain("aria-current", MailRow(returned, other), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownFolderScopeIsNotFound()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/Inbox?folder=archive");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MessageDetailShowsTheBodyAttachmentsThreadOutcomeAndTheWayBack()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 2);
        using var client = IntakeWebDriver.CreateClient(factory);

        var query = $"?mailbox={FirstMailboxFilter}&pageNumber=1";
        var message = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}{query}");

        Assert.Contains("Please inspect the vehicle", message, StringComparison.Ordinal);
        Assert.Contains("intake@collisionengineers.co.uk", message, StringComparison.Ordinal);
        // Nothing was processed, so the state strip says so rather than blanking.
        Assert.Contains("Not yet processed", message, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.Inbox.NoCase}</strong>", message, StringComparison.Ordinal);
        // Back reconstructs the exact list position.
        Assert.Contains($"/Inbox?mailbox={FirstMailboxFilter}", message, StringComparison.Ordinal);
        // The record's only POST is Dismiss (Inbox, 13 September); the shell's dialogs arrive when they open.
        Assert.Equal(1, CountOccurrences(message, "method=\"post\""));
        Assert.Contains("data-message-dismissal=\"dismiss\"", message, StringComparison.Ordinal);

        var attachments = await GetHtmlAsync(
            client,
            $"/Inbox/{ids[0]:D}{query}&section=attachments");
        Assert.Contains("estimate.pdf", attachments, StringComparison.Ordinal);
        // The v26 attachments table states each file's outcome, not its searchability.
        Assert.Contains("data-attachment-outcome=", attachments, StringComparison.Ordinal);
        // Megabytes, never bytes.
        Assert.Contains("under 0.1 MB", attachments, StringComparison.Ordinal);
        // The byte count standing alone, not inside a random key or identifier.
        Assert.DoesNotMatch(@"(?<![0-9A-Za-z-])2048(?![0-9A-Za-z-])", attachments);

        var thread = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}{query}&section=thread");
        Assert.Contains("Message 0 from instructions", thread, StringComparison.Ordinal);
        Assert.Contains("Message 1 from instructions", thread, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MessageDetailBeforeClassificationExistsShowsOnlyPopulatedDecisionRows()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1);
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}");

        // The Decision card shows only populated rows.
        Assert.Contains("<h2 class=\"decision-head\">Decision</h2>", html, StringComparison.Ordinal);
        // The record's Dismiss.
        Assert.Equal(1, CountOccurrences(html, "method=\"post\""));
    }

    [Fact]
    public async Task MessageDetailExplainsTheVersionedDecisionAndOffersExactMessageCorrection()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1);
        await StoreClassificationAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}");

        // The Decision card carries the decision in operator words; policy
        // keys, versions, predicate rows and prose never reach the page.
        Assert.Contains("<h2 class=\"decision-head\">Decision</h2>", html, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.Inbox.Classification}</span>", html, StringComparison.Ordinal);
        // The Classification chips: the decision is the one pressed, and the twelve are the
        // eleven categories then Unclassified (v41 I9, R179).
        var chips = Between(html, "id=\"mailClassificationLabel\"", "</form>");
        Assert.Equal(
            ChipValues.Length,
            CountOccurrences(chips, "aria-pressed=\"false\"") + CountOccurrences(chips, "aria-pressed=\"true\""));
        Assert.Contains(MailClassificationSelection.UnclassifiedLabel, Between(chips, "aria-pressed=\"true\"", "</button>"), StringComparison.Ordinal);
        // An unlinked message has no Destination row.
        Assert.DoesNotContain("<span>Destination</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("shared-mail-policy", html, StringComparison.Ordinal);
        Assert.DoesNotContain("sender-domain", html, StringComparison.Ordinal);
        Assert.DoesNotContain("mail_operational_destination", html, StringComparison.Ordinal);
        // A chip applies at once: it posts the exact decision version it corrects and
        // the fixed reason, so no reason is typed.
        Assert.Contains("name=\"ExpectedClassificationVersion\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"1\"", html, StringComparison.Ordinal);
        Assert.Contains(
            $"name=\"CorrectionReason\" value=\"{MailClassificationSelection.CorrectedFromInboxReason}\"",
            html,
            StringComparison.Ordinal);
        // The persisted "system-worker:..." actor resolves to the
        // operator-facing word beside the time, never the raw stored value.
        Assert.Contains("· Automatic", html, StringComparison.Ordinal);
        Assert.DoesNotContain("system-worker:approved-inbox-poller", html, StringComparison.Ordinal);
        // No corrections yet, so no Corrections card.
        Assert.DoesNotContain(">Corrections<", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LinkedMessageTakesTheCaseStateClassificationWhileAnUnlinkedMessageShowsNone()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 2);
        await StoreClassificationAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        await StoreClassificationAsync(factory, FirstMailboxId, FirstMailboxId + "-1");
        var linkedMessageId = await MessageIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var unlinkedMessageId = await MessageIdAsync(factory, FirstMailboxId, FirstMailboxId + "-1");
        var receiptId = await ReceiptIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var caseId = await ImageIntakeTestData.SeedCaseAsync(
            factory.Services, receiptId, "MAIL-DESTINATION", nameof(CaseLifecycleState.Review));
        using var client = IntakeWebDriver.CreateClient(factory);

        _ = await LinkThroughTheRecordAsync(
            client,
            linkedMessageId,
            "MAIL-DESTINATION",
            "The retained message belongs to this Case/PO.");

        // A fresh viewer checks the persisted message destination independently.
        using var viewer = IntakeWebDriver.CreateClient(factory);
        var linked = await GetHtmlAsync(viewer, $"/Inbox/{linkedMessageId:D}");
        // FRD-08 case-state classification: linked to a Review Case, the
        // unclassified message becomes a Case update. The Case is shown once.
        Assert.Contains("Case update", PressedChip(linked), StringComparison.Ordinal);
        Assert.DoesNotContain(MailClassificationSelection.UnclassifiedLabel, PressedChip(linked), StringComparison.Ordinal);
        Assert.Contains(
            $"<strong><a href=\"/Cases/{caseId:D}\">MAIL-DESTINATION</a></strong>",
            linked,
            StringComparison.Ordinal);
        Assert.DoesNotContain($"<strong>{OperatorLabels.Inbox.NoCaseFound}</strong>", linked, StringComparison.Ordinal);

        var listPage = await GetHtmlAsync(viewer, $"/Inbox?selected={linkedMessageId:D}");
        var linkedRow = MailRow(listPage, linkedMessageId);
        Assert.Contains("Case update", linkedRow, StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorLabels.Inbox.NoCaseFound, linkedRow, StringComparison.Ordinal);

        var unlinked = await GetHtmlAsync(viewer, $"/Inbox/{unlinkedMessageId:D}");
        Assert.Contains(MailClassificationSelection.UnclassifiedLabel, PressedChip(unlinked), StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.Inbox.NoCase}</strong>", unlinked, StringComparison.Ordinal);
    }

    /// <summary>The one pressed Classification chip of a record: the message's decision.</summary>
    private static string PressedChip(string html) =>
        Between(Between(html, "id=\"mailClassificationLabel\"", "</form>"), "aria-pressed=\"true\"", "</button>");

    /// <summary>The Classification chips are one form; its action, as rendered.</summary>
    private static string ChipFormAction(string html)
    {
        var form = Regex.Match(html, "<form[^>]*id=\"mailClassificationForm\"[^>]*>", RegexOptions.CultureInvariant);
        Assert.True(form.Success, "The Classification chips were not rendered.");
        return FormAction(form.Value);
    }

    [Fact]
    public async Task MessageDetailShowsTheCategoryOfAClassifiedDecision()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1);
        await StoreClassifiedInstructionAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}");

        // The category alone: no destination, folder recommendation or move.
        Assert.Contains("New instruction", PressedChip(html), StringComparison.Ordinal);
        Assert.DoesNotContain("<span>Destination</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Folder</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Move to", html, StringComparison.Ordinal);
        Assert.DoesNotContain("moveFolderDialog", html, StringComparison.Ordinal);
        Assert.DoesNotContain("MoveToRecommendedFolder", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CraftedOrOversizedCorrectionsFailClosedWithoutHistoryWrites()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1);
        await StoreClassificationAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        using var client = IntakeWebDriver.CreateClient(factory);
        var route = $"/Inbox/{ids[0]:D}?handler=CorrectClassification";
        var attempts = new[]
        {
            new Dictionary<string, string> { ["ClassificationKey"] = "received:General:autoreply" },
            new Dictionary<string, string> { ["ClassificationKey"] = "other-received" },
            new Dictionary<string, string> { ["ClassificationKey"] = "Query" },
            new Dictionary<string, string> { ["ClassificationKey"] = "unidentified" }
        };

        foreach (var attempt in attempts)
        {
            var page = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}");
            attempt["__RequestVerificationToken"] = AntiforgeryToken(page);
            attempt["ExpectedClassificationVersion"] = "1";
            attempt["CorrectionReason"] = "Reviewed retained evidence.";
            using var response = await client.PostAsync(route, new FormUrlEncodedContent(attempt));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(
                "Choose a valid classification.",
                await response.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.Equal(1, await context.IntakeMailClassificationDecisions.Select(item => item.Version).SingleAsync());
        Assert.Empty(await context.IntakeMailClassificationHistory.ToListAsync());
    }

    [Fact]
    public async Task TheChipsOfferEveryCategoryAndUnclassifiedAndANewInstructionStoresItsCaseTypeAndTheFixedReason()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1);
        await StoreClassificationAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        using var client = IntakeWebDriver.CreateClient(factory);
        var route = $"/Inbox/{ids[0]:D}?handler=CorrectClassification";

        var page = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}");
        // The chips are one flat set: every category, then Unclassified, which is the
        // one pressed here (the message is Unclassified), and a New instruction asks for the case type.
        var chips = Between(page, "id=\"mailClassificationLabel\"", "</form>");
        Assert.Equal(
            ChipValues[..^1],
            Regex.Matches(chips, "name=\"ClassificationKey\" value=\"(?<value>[^\"]*)\"")
                .Select(match => match.Groups["value"].Value)
                .ToArray());
        Assert.Contains(MailClassificationSelection.UnclassifiedLabel, PressedChip(page), StringComparison.Ordinal);
        Assert.Contains("data-work-type-field", page, StringComparison.Ordinal);

        // A New instruction without a case type is refused before the command.
        using (var missing = await client.PostAsync(route, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiforgeryToken(page),
            ["ExpectedClassificationVersion"] = "1",
            ["ClassificationKey"] = "new-instruction",
            ["CorrectionReason"] = MailClassificationSelection.CorrectedFromInboxReason
        })))
        {
            Assert.Equal(HttpStatusCode.OK, missing.StatusCode);
            Assert.Contains("Choose a valid case type.", await missing.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        page = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}");
        using (var corrected = await client.PostAsync(route, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiforgeryToken(page),
            ["ExpectedClassificationVersion"] = "1",
            ["ClassificationKey"] = "new-instruction",
            ["WorkType"] = nameof(CaseType.InspectionAndAudit),
            ["CorrectionReason"] = MailClassificationSelection.CorrectedFromInboxReason
        })))
        {
            Assert.Equal(HttpStatusCode.Redirect, corrected.StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var decision = await context.IntakeMailClassificationDecisions.SingleAsync();
        Assert.Equal(2, decision.Version);
        Assert.Equal(CaseTypeCodes.ToCode(CaseType.InspectionAndAudit), decision.CaseType);
        // A chip records the fixed reason, as Tag as query records its own (R179).
        Assert.Equal(
            MailClassificationSelection.CorrectedFromInboxReason,
            (await context.IntakeMailClassificationHistory.SingleAsync()).Reason);

        // The corrected classification offers its one next action, Create case,
        // against the message's receipt; nothing ran on its own.
        var offered = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}");
        Assert.Contains("data-offered-action=\"CreateCase\"", offered, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Cases/Create?receiptId={decision.IntakeReceiptId:D}\"", offered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidSearchContextOnACorrectionReloadReturnsASupportedResponseWithoutWrites()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1);
        await StoreClassificationAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        using var client = IntakeWebDriver.CreateClient(factory);

        foreach (var (search, expectedStatus) in new[]
        {
            ("   ", HttpStatusCode.OK),
            (new string('s', 201), HttpStatusCode.NotFound)
        })
        {
            var page = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}");
            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = AntiforgeryToken(page),
                ["ExpectedClassificationVersion"] = "1",
                ["ClassificationKey"] = "not-a-category",
                ["CorrectionReason"] = "Reviewed retained evidence."
            };
            var route = $"/Inbox/{ids[0]:D}?handler=CorrectClassification&search={Uri.EscapeDataString(search)}";

            using var response = await client.PostAsync(route, new FormUrlEncodedContent(form));

            Assert.Equal(expectedStatus, response.StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.Equal(1, await context.IntakeMailClassificationDecisions.Select(item => item.Version).SingleAsync());
        Assert.Empty(await context.IntakeMailClassificationHistory.ToListAsync());
    }

    [Fact]
    public async Task AForwardedMessageShowsTheProvenOriginalSenderAndItsForwarder()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1);
        await StoreForwardedRouteAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        using var client = IntakeWebDriver.CreateClient(factory);

        var inbox = await GetHtmlAsync(client, "/Inbox");
        var detail = await GetHtmlAsync(client, "/Inbox/" + ids[0].ToString("D"));

        Assert.Contains("original@qdosassist.co.uk", inbox, StringComparison.Ordinal);
        Assert.Contains("Forwarded by", inbox, StringComparison.Ordinal);
        Assert.Contains("A Sender", inbox, StringComparison.Ordinal);
        Assert.Contains("original@qdosassist.co.uk", detail, StringComparison.Ordinal);
        Assert.Contains("Forwarded by A Sender", detail, StringComparison.Ordinal);
        Assert.Contains("sender@example.invalid", detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMessageOpenedFromAScopeItIsNotInStillRendersWithTheWayBack()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1);
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(
            client,
            $"/Inbox/{ids[0]:D}?mailbox={TestMailboxId.From(SecondMailboxId):D}");

        Assert.Contains(
            "This message is no longer in the view you opened it from.",
            html,
            StringComparison.Ordinal);
        Assert.Contains("Back to Inbox", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMessageOpenedFromAnotherCategoryIsOutsideTheView()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1);
        await StoreClassifiedInstructionAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        using var client = IntakeWebDriver.CreateClient(factory);

        var other = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}?queue=query");
        var own = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}?queue=new-instruction");
        // No Case Found reads the link, not the category: the classified but
        // unlinked instruction is in it.
        var noCaseFound = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}?queue=no_case_found");

        Assert.Contains("This message is no longer in the view you opened it from.", other, StringComparison.Ordinal);
        Assert.DoesNotContain("This message is no longer in the view you opened it from.", own, StringComparison.Ordinal);
        Assert.DoesNotContain("This message is no longer in the view you opened it from.", noCaseFound, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ALinkedOrUnreceiptedMessageIsOutsideTheNoCaseFoundViewWhileAnUnlinkedMessageRemains()
    {
        using var factory = new IntakeWebApplicationFactory();
        await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 3);
        await StoreClassificationAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        await StoreClassificationAsync(factory, FirstMailboxId, FirstMailboxId + "-1");
        var linkedMessageId = await MessageIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var openMessageId = await MessageIdAsync(factory, FirstMailboxId, FirstMailboxId + "-1");
        var unreceiptedMessageId = await MessageIdAsync(factory, FirstMailboxId, FirstMailboxId + "-2");
        var linkedReceiptId = await ReceiptIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var caseId = await ImageIntakeTestData.SeedCaseAsync(
            factory.Services, linkedReceiptId, "MAIL-NO-CASE-FOUND", nameof(CaseLifecycleState.Review));
        await MailboxIntakeTestData.LinkReceiptAsync(factory.Services, linkedReceiptId, caseId);

        using var client = IntakeWebDriver.CreateClient(factory);
        var linked = await GetHtmlAsync(
            client,
            $"/Inbox/{linkedMessageId:D}?queue=no_case_found");
        var unreceipted = await GetHtmlAsync(
            client,
            $"/Inbox/{unreceiptedMessageId:D}?queue=no_case_found");
        var open = await GetHtmlAsync(
            client,
            $"/Inbox/{openMessageId:D}?queue=no_case_found");

        Assert.Contains(
            "This message is no longer in the view you opened it from.",
            linked,
            StringComparison.Ordinal);
        Assert.Contains(
            "This message is no longer in the view you opened it from.",
            unreceipted,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "This message is no longer in the view you opened it from.",
            open,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownMessageIsNotFound()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync($"/Inbox/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Dismiss on the row (Inbox, 13 September): the message leaves every incoming
    /// scope and the counts, appears only under Dismissed, and Restore brings it
    /// back. Nothing is deleted.
    /// </summary>
    [Fact]
    public async Task DismissFromTheRowMovesTheMessageToDismissedAndRestoreBringsItBack()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 2);
        using var client = IntakeWebDriver.CreateClient(factory);
        var dismissedId = ids[0];

        var list = await GetHtmlAsync(client, "/Inbox");
        var dismissForm = RowActionForm(list, "dismiss", dismissedId);
        using (var dismiss = await client.PostAsync(
            FormAction(dismissForm),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = FormInput(list, "__RequestVerificationToken"),
                ["id"] = dismissedId.ToString("D"),
                ["operationKey"] = FormInput(dismissForm, "operationKey")
            })))
        {
            Assert.Equal(HttpStatusCode.Redirect, dismiss.StatusCode);
            Assert.Equal("/Inbox", dismiss.Headers.Location?.OriginalString);
        }

        var incoming = await GetHtmlAsync(client, "/Inbox");
        Assert.DoesNotContain($"data-mail-row=\"{dismissedId:D}\"", incoming, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"data-mail-row=\"{ids[1]:D}\"", incoming, StringComparison.OrdinalIgnoreCase);

        var dismissed = await GetHtmlAsync(client, "/Inbox?folder=dismissed");
        Assert.Contains($"data-mail-row=\"{dismissedId:D}\"", dismissed, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"data-mail-row=\"{ids[1]:D}\"", dismissed, StringComparison.OrdinalIgnoreCase);
        var restoreForm = RowActionForm(dismissed, "restore", dismissedId);
        Assert.Contains("folder=dismissed", WebUtility.HtmlDecode(FormAction(restoreForm)), StringComparison.Ordinal);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            Assert.NotNull(await context.RetainedMailboxMessages
                .Where(item => item.Id == dismissedId)
                .Select(item => item.DismissedAtUtc)
                .SingleAsync());
        }

        using (var restore = await client.PostAsync(
            WebUtility.HtmlDecode(FormAction(restoreForm)),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = FormInput(dismissed, "__RequestVerificationToken"),
                ["id"] = dismissedId.ToString("D"),
                ["operationKey"] = FormInput(restoreForm, "operationKey")
            })))
        {
            Assert.Equal(HttpStatusCode.Redirect, restore.StatusCode);
            Assert.Equal("/Inbox?folder=dismissed", restore.Headers.Location?.OriginalString);
        }

        Assert.Contains("No dismissed messages.", await GetHtmlAsync(client, "/Inbox?folder=dismissed"), StringComparison.Ordinal);
        Assert.Contains($"data-mail-row=\"{dismissedId:D}\"", await GetHtmlAsync(client, "/Inbox"), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ticked rows (operator, 15 October 2026): the row's own Dismiss, and
    /// Restore from the Dismissed scope, once per ticked message, each with its
    /// own history entry; an unticked message is untouched.
    /// </summary>
    [Fact]
    public async Task BulkDismissAndRestoreApplyTheSingleCommandToEachTickedMessage()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 3);
        using var client = IntakeWebDriver.CreateClient(factory);
        var token = AntiforgeryToken(await GetHtmlAsync(client, "/Inbox"));
        Guid[] ticked = [ids[0], ids[1]];

        using (var dismiss = await client.PostAsync(
            "/Inbox?handler=BulkDismiss",
            BulkForm(token, ticked, ("operationKey", Guid.NewGuid().ToString("N")))))
        {
            Assert.Equal(HttpStatusCode.Redirect, dismiss.StatusCode);
            Assert.Equal("/Inbox", dismiss.Headers.Location?.OriginalString);
        }

        Assert.Equal(OperatorLabels.Inbox.DismissedNotice, Confirmation(await GetHtmlAsync(client, "/Inbox")));
        Assert.Equal(ticked.Order(), await DismissedMessagesAsync(factory));
        Assert.Equal(2, await DismissalHistoryCountAsync(factory, "retained_mail_dismissed"));

        using (var restore = await client.PostAsync(
            "/Inbox?folder=dismissed&handler=BulkRestore",
            BulkForm(token, ticked, ("operationKey", Guid.NewGuid().ToString("N")))))
        {
            Assert.Equal(HttpStatusCode.Redirect, restore.StatusCode);
            Assert.Equal("/Inbox?folder=dismissed", restore.Headers.Location?.OriginalString);
        }

        Assert.Equal(OperatorLabels.Inbox.RestoredNotice, Confirmation(await GetHtmlAsync(client, "/Inbox?folder=dismissed")));
        Assert.Empty(await DismissedMessagesAsync(factory));
        Assert.Equal(2, await DismissalHistoryCountAsync(factory, "retained_mail_restored"));
    }

    /// <summary>
    /// The same bulk form posted twice: Dismiss replays each message's keyed
    /// act, and the correction's second post finds the version it named
    /// already moved on, so neither writes a second history entry.
    /// </summary>
    [Fact]
    public async Task ABulkPostIsReplayedNotRepeated()
    {
        using var factory = new IntakeWebApplicationFactory();
        await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 3);
        var messages = new Guid[3];
        for (var index = 0; index < messages.Length; index++)
        {
            await StoreClassificationAsync(factory, FirstMailboxId, $"{FirstMailboxId}-{index}");
            messages[index] = await MessageIdAsync(factory, FirstMailboxId, $"{FirstMailboxId}-{index}");
        }
        using var client = IntakeWebDriver.CreateClient(factory);
        var token = AntiforgeryToken(await GetHtmlAsync(client, "/Inbox"));
        var operationKey = Guid.NewGuid().ToString("N");

        for (var post = 0; post < 2; post++)
        {
            using var dismiss = await client.PostAsync(
                "/Inbox?handler=BulkDismiss",
                BulkForm(token, messages[..2], ("operationKey", operationKey)));
            Assert.Equal(HttpStatusCode.Redirect, dismiss.StatusCode);

            using var classify = await client.PostAsync(
                "/Inbox?handler=BulkClassify",
                BulkForm(
                    token,
                    [messages[2]],
                    ($"versions[{messages[2]:D}]", "1"),
                    ("ClassificationKey", MailCategories.Code(MailCategory.General)),
                    ("CorrectionReason", "An acknowledgement.")));
            Assert.Equal(HttpStatusCode.Redirect, classify.StatusCode);
        }

        Assert.Equal(2, await DismissalHistoryCountAsync(factory, "retained_mail_dismissed"));
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.Single(await context.IntakeMailClassificationHistory.ToListAsync());
        Assert.Equal(
            [1, 1, 2],
            await context.IntakeMailClassificationDecisions
                .OrderBy(item => item.Version)
                .Select(item => item.Version)
                .ToListAsync());
    }

    /// <summary>
    /// One category, case type and reason for every ticked message; each
    /// message is corrected on its own against the version its row named, with
    /// its own history entry carrying the shared reason (R140).
    /// </summary>
    [Fact]
    public async Task BulkClassifyCorrectsEachTickedMessageWithItsOwnHistoryAndReason()
    {
        using var factory = new IntakeWebApplicationFactory();
        await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 3);
        var messages = new Guid[3];
        var receipts = new Guid[3];
        for (var index = 0; index < messages.Length; index++)
        {
            await StoreClassificationAsync(factory, FirstMailboxId, $"{FirstMailboxId}-{index}");
            messages[index] = await MessageIdAsync(factory, FirstMailboxId, $"{FirstMailboxId}-{index}");
            receipts[index] = await ReceiptIdAsync(factory, FirstMailboxId, $"{FirstMailboxId}-{index}");
        }
        using var client = IntakeWebDriver.CreateClient(factory);
        var token = AntiforgeryToken(await GetHtmlAsync(client, "/Inbox"));
        const string reason = "Instructions read from the retained messages.";

        using (var response = await client.PostAsync(
            "/Inbox?handler=BulkClassify",
            BulkForm(
                token,
                messages[..2],
                ($"versions[{messages[0]:D}]", "1"),
                ($"versions[{messages[1]:D}]", "1"),
                ("ClassificationKey", MailCategories.Code(MailCategory.NewInstruction)),
                ("WorkType", nameof(CaseType.Inspection)),
                ("CorrectionReason", reason))))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Inbox", response.Headers.Location?.OriginalString);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        foreach (var receiptId in receipts[..2])
        {
            var decision = await context.IntakeMailClassificationDecisions.SingleAsync(item => item.IntakeReceiptId == receiptId);
            Assert.Equal(2, decision.Version);
            Assert.Equal(MailCategories.Code(MailCategory.NewInstruction), decision.Category);
            Assert.Equal(CaseTypeCodes.ToCode(CaseType.Inspection), decision.CaseType);
            var entry = await context.IntakeMailClassificationHistory.SingleAsync(item => item.IntakeReceiptId == receiptId);
            Assert.Equal(reason, entry.Reason);
        }
        Assert.Equal(
            1,
            await context.IntakeMailClassificationDecisions
                .Where(item => item.IntakeReceiptId == receipts[2])
                .Select(item => item.Version)
                .SingleAsync());
        Assert.Equal(2, await context.IntakeMailClassificationHistory.CountAsync());
    }

    /// <summary>
    /// A ticked message a colleague corrected after the list was drawn, and
    /// one with no classification decision, are refused on their own; the
    /// messages after them are still corrected.
    /// </summary>
    [Fact]
    public async Task BulkClassifyRefusesAStaleMessageAndStillCorrectsTheOthers()
    {
        using var factory = new IntakeWebApplicationFactory();
        await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 4);
        var messages = new Guid[4];
        var receipts = new Guid[3];
        for (var index = 0; index < messages.Length; index++)
        {
            messages[index] = await MessageIdAsync(factory, FirstMailboxId, $"{FirstMailboxId}-{index}");
        }
        for (var index = 0; index < receipts.Length; index++)
        {
            await StoreClassificationAsync(factory, FirstMailboxId, $"{FirstMailboxId}-{index}");
            receipts[index] = await ReceiptIdAsync(factory, FirstMailboxId, $"{FirstMailboxId}-{index}");
        }
        await using (var colleague = factory.Services.CreateAsyncScope())
        {
            await colleague.ServiceProvider.GetRequiredService<CorrectRetainedMailClassification>().ExecuteAsync(
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
                new(messages[0], 1, MailCategory.Billing, "A colleague's correction."));
        }
        using var client = IntakeWebDriver.CreateClient(factory);
        var token = AntiforgeryToken(await GetHtmlAsync(client, "/Inbox"));

        using (var response = await client.PostAsync(
            "/Inbox?handler=BulkClassify",
            BulkForm(
                token,
                messages,
                ($"versions[{messages[0]:D}]", "1"),
                ($"versions[{messages[1]:D}]", "1"),
                ($"versions[{messages[2]:D}]", "1"),
                ("ClassificationKey", MailCategories.Code(MailCategory.General)),
                ("CorrectionReason", "Acknowledgements."))))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Inbox", response.Headers.Location?.OriginalString);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var stale = await context.IntakeMailClassificationDecisions.SingleAsync(item => item.IntakeReceiptId == receipts[0]);
        Assert.Equal(2, stale.Version);
        Assert.Equal(MailCategories.Code(MailCategory.Billing), stale.Category);
        Assert.Equal(
            "A colleague's correction.",
            (await context.IntakeMailClassificationHistory.SingleAsync(item => item.IntakeReceiptId == receipts[0])).Reason);
        foreach (var receiptId in receipts[1..])
        {
            var decision = await context.IntakeMailClassificationDecisions.SingleAsync(item => item.IntakeReceiptId == receiptId);
            Assert.Equal(2, decision.Version);
            Assert.Equal(MailCategories.Code(MailCategory.General), decision.Category);
        }
        Assert.Equal(3, await context.IntakeMailClassificationDecisions.CountAsync());
        Assert.Equal(3, await context.IntakeMailClassificationHistory.CountAsync());
    }

    /// <summary>
    /// Every bulk post stops before a write on an invalid list context, outside
    /// the scope its row act belongs to, for an actor without casework, and,
    /// for the correction, on a refused category, case type or reason.
    /// </summary>
    [Fact]
    public async Task BulkPostsFailClosedOnAnInvalidListContextAndWithoutCaseworkRight()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 2);
        var messages = new Guid[2];
        for (var index = 0; index < messages.Length; index++)
        {
            await StoreClassificationAsync(factory, FirstMailboxId, $"{FirstMailboxId}-{index}");
            messages[index] = await MessageIdAsync(factory, FirstMailboxId, $"{FirstMailboxId}-{index}");
        }
        using var client = IntakeWebDriver.CreateClient(factory);
        var token = AntiforgeryToken(await GetHtmlAsync(client, "/Inbox"));
        (string Name, string Value)[] correction =
        [
            ($"versions[{messages[0]:D}]", "1"),
            ($"versions[{messages[1]:D}]", "1"),
            ("ClassificationKey", MailCategories.Code(MailCategory.General)),
            ("CorrectionReason", "Reviewed retained evidence."),
            ("operationKey", Guid.NewGuid().ToString("N"))
        ];

        foreach (var route in new[]
        {
            "/Inbox?queue=not-a-category&handler=BulkDismiss",
            "/Inbox?sort=newest&handler=BulkClassify",
            "/Inbox?folder=elsewhere&handler=BulkRestore",
            "/Inbox?folder=dismissed&handler=BulkDismiss",
            "/Inbox?folder=dismissed&handler=BulkClassify",
            "/Inbox?handler=BulkRestore",
            "/Inbox?folder=sent&handler=BulkDismiss",
            "/Inbox?folder=deleted&handler=BulkClassify"
        })
        {
            using var response = await client.PostAsync(route, BulkForm(token, messages, correction));
            Assert.True(HttpStatusCode.NotFound == response.StatusCode, $"{route} answered {response.StatusCode}.");
        }

        foreach (var route in new[]
        {
            "/Inbox?handler=BulkDismiss",
            "/Inbox?folder=dismissed&handler=BulkRestore",
            "/Inbox?handler=BulkClassify"
        })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, route)
            {
                Content = BulkForm(token, messages, correction)
            };
            request.Headers.Add("X-Test-Roleless", "1");
            using var roleless = await client.SendAsync(request);
            Assert.True(HttpStatusCode.Forbidden == roleless.StatusCode, $"{route} answered {roleless.StatusCode}.");
        }

        // The correction's refusals come once, before any message is touched.
        foreach (var refused in new (string Name, string Value)[][]
        {
            [($"versions[{messages[0]:D}]", "1"), ("ClassificationKey", "received:General:autoreply"), ("CorrectionReason", "Reviewed.")],
            [($"versions[{messages[0]:D}]", "1"), ("ClassificationKey", MailCategories.Code(MailCategory.NewInstruction)), ("CorrectionReason", "Reviewed.")],
            [($"versions[{messages[0]:D}]", "1"), ("ClassificationKey", MailCategories.Code(MailCategory.General)), ("CorrectionReason", " ")]
        })
        {
            using var response = await client.PostAsync("/Inbox?handler=BulkClassify", BulkForm(token, messages, refused));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.Empty(await DismissedMessagesAsync(factory));
        Assert.Equal(0, await DismissalHistoryCountAsync(factory, "retained_mail_dismissed"));
        Assert.Equal(0, await DismissalHistoryCountAsync(factory, "retained_mail_restored"));
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.All(await context.IntakeMailClassificationDecisions.ToListAsync(), item => Assert.Equal(1, item.Version));
        Assert.Empty(await context.IntakeMailClassificationHistory.ToListAsync());
    }

    /// <summary>
    /// Row ticks and one bulk form (v41 I18): a tick in the gutter of every row in
    /// All incoming, No Case Found and Dismissed, each named by its message's subject;
    /// one form above the list with a bare count and Dismiss and Correct classification,
    /// or Restore in Dismissed; none in Sent Items or Deleted Items. The rendered form
    /// posts to each handler, and each applies to the messages it names.
    /// </summary>
    [Fact]
    public async Task TheListOffersTickboxesAndOneBulkFormInTheScopesTheDrawingShows()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 3);
        for (var index = 0; index < ids.Length; index++)
        {
            await StoreClassificationAsync(factory, FirstMailboxId, $"{FirstMailboxId}-{index}");
        }
        using var client = IntakeWebDriver.CreateClient(factory);

        var incoming = await GetHtmlAsync(client, "/Inbox");
        Assert.Equal(1, CountOccurrences(incoming, "id=\"mail-bulk\""));
        Assert.Equal(3, CountOccurrences(incoming, "data-mail-tick"));
        Assert.Contains("<span class=\"inbox-bulk-count tabular\" data-bulk-count>0</span>", incoming, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.Inbox.CorrectClassification}</button>", incoming, StringComparison.Ordinal);
        foreach (var (id, index) in ids.Select((id, index) => (id, ids.Length - 1 - index)))
        {
            var row = MailRow(incoming, id);
            Assert.Contains($"aria-label=\"Message {index} from {FirstMailboxId}\"", row, StringComparison.Ordinal);
            Assert.Contains($"name=\"versions[{id:D}]\"", row, StringComparison.Ordinal);
        }
        var noCaseFound = await GetHtmlAsync(client, "/Inbox?queue=no_case_found");
        Assert.Equal(3, CountOccurrences(noCaseFound, "data-mail-tick"));
        foreach (var scope in new[] { "folder=sent", "folder=deleted" })
        {
            var html = await GetHtmlAsync(client, $"/Inbox?{scope}");
            Assert.Equal(0, CountOccurrences(html, "data-mail-tick"));
            Assert.Equal(0, CountOccurrences(html, "id=\"mail-bulk\""));
        }

        // The rendered form posts: Dismiss for two of the three ticked, then Restore from Dismissed.
        var token = AntiforgeryToken(incoming);
        var operationKey = HiddenValue(Between(incoming, "id=\"mail-bulk\"", "</form>"), "operationKey");
        using (var dismiss = await client.PostAsync(
            BulkAction(incoming, "BulkDismiss"),
            BulkForm(token, ids[..2], ("operationKey", operationKey))))
        {
            Assert.Equal(HttpStatusCode.Redirect, dismiss.StatusCode);
        }
        Assert.Equal(ids[..2].Order(), await DismissedMessagesAsync(factory));

        var dismissed = await GetHtmlAsync(client, "/Inbox?folder=dismissed");
        Assert.Equal(2, CountOccurrences(dismissed, "data-mail-tick"));
        Assert.Contains($">{OperatorLabels.Inbox.Restore}</span>", Between(dismissed, "id=\"mail-bulk\"", "</form>"), StringComparison.Ordinal);
        using (var restore = await client.PostAsync(
            BulkAction(dismissed, "BulkRestore"),
            BulkForm(
                AntiforgeryToken(dismissed),
                ids[..2],
                ("operationKey", HiddenValue(Between(dismissed, "id=\"mail-bulk\"", "</form>"), "operationKey")))))
        {
            Assert.Equal(HttpStatusCode.Redirect, restore.StatusCode);
        }
        Assert.Empty(await DismissedMessagesAsync(factory));

        // Correct classification: the dialog's fields and each row's version join the one form.
        var again = await GetHtmlAsync(client, "/Inbox");
        var versions = Regex.Matches(again, "name=\"versions\\[(?<id>[^\\]]+)\\]\" value=\"(?<version>\\d+)\"")
            .ToDictionary(match => match.Groups["id"].Value, match => match.Groups["version"].Value);
        Assert.Equal(3, versions.Count);
        using (var classify = await client.PostAsync(
            BulkAction(again, "BulkClassify"),
            BulkForm(
                AntiforgeryToken(again),
                ids[1..],
                ($"versions[{ids[1]:D}]", versions[ids[1].ToString("D")]),
                ($"versions[{ids[2]:D}]", versions[ids[2].ToString("D")]),
                ("ClassificationKey", MailCategories.Code(MailCategory.General)),
                ("CorrectionReason", "Acknowledgements."))))
        {
            Assert.Equal(HttpStatusCode.Redirect, classify.StatusCode);
        }
        await using var verification = factory.Services.CreateAsyncScope();
        var contextFactory = verification.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.Equal(2, await context.IntakeMailClassificationHistory.CountAsync());
    }

    /// <summary>
    /// A bulk post that could not change some ticked messages (R180): one line above
    /// the list says how many were not changed, once, and those rows stay ticked so they
    /// can be retried; the others are changed.
    /// </summary>
    [Fact]
    public async Task ABulkPostThatCouldNotChangeAMessageSaysSoOnceAndKeepsItTicked()
    {
        using var factory = new IntakeWebApplicationFactory();
        await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 3);
        var messages = new Guid[3];
        for (var index = 0; index < messages.Length; index++)
        {
            await StoreClassificationAsync(factory, FirstMailboxId, $"{FirstMailboxId}-{index}");
            messages[index] = await MessageIdAsync(factory, FirstMailboxId, $"{FirstMailboxId}-{index}");
        }
        await using (var colleague = factory.Services.CreateAsyncScope())
        {
            await colleague.ServiceProvider.GetRequiredService<CorrectRetainedMailClassification>().ExecuteAsync(
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
                new(messages[0], 1, MailCategory.Billing, "A colleague's correction."));
        }
        using var client = IntakeWebDriver.CreateClient(factory);
        var token = AntiforgeryToken(await GetHtmlAsync(client, "/Inbox"));

        using (var response = await client.PostAsync(
            "/Inbox?handler=BulkClassify",
            BulkForm(
                token,
                messages,
                ($"versions[{messages[0]:D}]", "1"),
                ($"versions[{messages[1]:D}]", "1"),
                ($"versions[{messages[2]:D}]", "1"),
                ("ClassificationKey", MailCategories.Code(MailCategory.General)),
                ("CorrectionReason", "Acknowledgements."))))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var list = await GetHtmlAsync(client, "/Inbox");
        Assert.Equal(1, CountOccurrences(list, "data-bulk-not-changed"));
        Assert.Contains($">{OperatorLabels.Inbox.NotChanged(1)}</span>", list, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(list, " checked=\"checked\""));
        Assert.Contains(" checked=\"checked\"", MailRow(list, messages[0]), StringComparison.Ordinal);
        Assert.Equal(0, CountOccurrences(await GetHtmlAsync(client, "/Inbox"), "data-bulk-not-changed"));
    }

    /// <summary>
    /// A refused correction shows its sentences where the operator sees them: on the
    /// record, beside the Case type a New instruction asks for, and on the list, in the
    /// dialog it came from, which opens again over the rows that were ticked.
    /// </summary>
    [Fact]
    public async Task ARefusedCorrectionIsVisibleOnTheRecordAndOnTheList()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 2);
        for (var index = 0; index < ids.Length; index++)
        {
            await StoreClassificationAsync(factory, FirstMailboxId, $"{FirstMailboxId}-{index}");
        }
        using var client = IntakeWebDriver.CreateClient(factory);

        var record = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}");
        using (var refused = await client.PostAsync(
            ChipFormAction(record),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = AntiforgeryToken(record),
                ["ExpectedClassificationVersion"] = "1",
                ["ClassificationKey"] = MailCategories.Code(MailCategory.NewInstruction),
                ["CorrectionReason"] = MailClassificationSelection.CorrectedFromInboxReason
            })))
        {
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            var html = await refused.Content.ReadAsStringAsync();
            var caseTypeField = Between(html, "class=\"field mail-case-type\"", "</div>");
            Assert.Contains(OperatorLabels.Inbox.ChooseCaseType, caseTypeField, StringComparison.Ordinal);
            Assert.DoesNotContain("hidden", Between(html, "class=\"field mail-case-type\"", ">"), StringComparison.Ordinal);
        }

        var list = await GetHtmlAsync(client, "/Inbox");
        using var listRefusal = await client.PostAsync(
            "/Inbox?handler=BulkClassify",
            BulkForm(
                AntiforgeryToken(list),
                ids,
                ($"versions[{ids[0]:D}]", "1"),
                ($"versions[{ids[1]:D}]", "1"),
                ("ClassificationKey", MailCategories.Code(MailCategory.General)),
                ("CorrectionReason", " ")));
        Assert.Equal(HttpStatusCode.OK, listRefusal.StatusCode);
        var refusedList = await listRefusal.Content.ReadAsStringAsync();
        Assert.Contains("data-dialog-open-on-load=\"true\"", Between(refusedList, "id=\"bulkClassifyDialog\"", ">"), StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.Inbox.CorrectionReasonRequired, refusedList, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(refusedList, " checked=\"checked\""));
    }

    /// <summary>
    /// Dismiss in the message dialog (v41 I10): the post names the message the list
    /// reads after this one, and the Inbox opens it; with none, the Inbox shows the list.
    /// </summary>
    [Fact]
    public async Task DismissInTheDialogLandsOnTheFollowingMessage()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 3);
        using var client = IntakeWebDriver.CreateClient(factory);

        var dialog = await GetHtmlAsync(
            client, $"/Inbox/{ids[1]:D}?handler=Dialog&mailbox={FirstMailboxFilter}");
        var form = Regex.Match(
            dialog,
            "<form[^>]*data-message-dismissal=\"dismiss\"[^>]*>[\\s\\S]*?</form>").Value;
        var action = FormAction(form);
        Assert.Contains("dialog=true", action, StringComparison.Ordinal);
        Assert.Contains("name=\"next\"", form, StringComparison.Ordinal);

        using (var dismiss = await client.PostAsync(
            action,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = FormInput(dialog, "__RequestVerificationToken"),
                ["operationKey"] = FormInput(form, "operationKey"),
                ["next"] = ids[2].ToString("D")
            })))
        {
            Assert.Equal(HttpStatusCode.Redirect, dismiss.StatusCode);
            var location = dismiss.Headers.Location!.ToString();
            Assert.StartsWith("/Inbox?", location, StringComparison.Ordinal);
            Assert.Contains($"open={ids[2]:D}", location, StringComparison.Ordinal);
            Assert.Contains($"selected={ids[2]:D}", location, StringComparison.Ordinal);
            Assert.Contains($"mailbox={FirstMailboxFilter}", location, StringComparison.Ordinal);
        }
        Assert.Equal([ids[1]], await DismissedMessagesAsync(factory));

        // A message with no following one and none before it: the Inbox shows the list.
        var last = await GetHtmlAsync(client, $"/Inbox/{ids[2]:D}?handler=Dialog");
        var lastForm = Regex.Match(
            last,
            "<form[^>]*data-message-dismissal=\"dismiss\"[^>]*>[\\s\\S]*?</form>").Value;
        using (var dismissLast = await client.PostAsync(
            FormAction(lastForm),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = FormInput(last, "__RequestVerificationToken"),
                ["operationKey"] = FormInput(lastForm, "operationKey"),
                ["next"] = string.Empty
            })))
        {
            Assert.Equal(HttpStatusCode.Redirect, dismissLast.StatusCode);
            Assert.Equal("/Inbox", dismissLast.Headers.Location?.OriginalString);
        }
    }

    /// <summary>
    /// Register images (v41 I17): the offer sits in the record's foot and its dialog
    /// posts from the record. The message's image-only receipt becomes a
    /// vehicle-images record under the registration staff give.
    /// </summary>
    [Fact]
    public async Task RegisterImagesPostsFromTheRecord()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(
            factory.Services,
            "vehicle.png",
            "image/png",
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64));
        var messageId = await TriageFromIntakeIntegrationTests.RetainAsThePollWouldAsync(
            factory, receiptId, "register-images", "Photographs of the vehicle");

        // The route read no category: staff correct the message to Images received,
        // and the record then offers Register images against the same receipt.
        using (var correction = await client.PostAsync(
            $"/Inbox/{messageId:D}?handler=CorrectClassification",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = await IntakeWebDriver.GetAntiforgeryTokenAsync(client),
                ["ExpectedClassificationVersion"] = "1",
                ["ClassificationKey"] = "images-received",
                ["CorrectionReason"] = MailClassificationSelection.CorrectedFromInboxReason
            })))
        {
            Assert.Equal(HttpStatusCode.Redirect, correction.StatusCode);
        }

        var record = await GetHtmlAsync(client, $"/Inbox/{messageId:D}");
        Assert.Contains("data-offered-action=\"RegisterImages\"", record, StringComparison.Ordinal);
        var form = AssociationForm(record, "RegisterImages");
        var fields = HiddenFields(form);
        fields["vehicleRegistration"] = "ab12 cde";
        fields["reason"] = "Staff read the registration from the retained image.";
        using (var register = await client.PostAsync(
            AssociationAction(form, "RegisterImages"),
            new FormUrlEncodedContent(fields)))
        {
            Assert.Equal(HttpStatusCode.Redirect, register.StatusCode);
            var landed = await GetHtmlAsync(client, register.Headers.Location!.ToString());
            Assert.Contains("Registered as vehicle images AB12CDE-01.", landed, StringComparison.Ordinal);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var receipt = await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        Assert.Equal(IntakeDecision.ImageIntakeRegistered, receipt!.Decision);
    }

    /// <summary>The action the rendered bulk form's control for a handler posts to.</summary>
    private static string BulkAction(string html, string handler)
    {
        var match = Regex.Match(html, $"formaction=\"(?<action>[^\"]*handler={handler}[^\"]*)\"");
        Assert.True(match.Success, $"The bulk control for {handler} was not rendered.");
        return WebUtility.HtmlDecode(match.Groups["action"].Value);
    }

    private static FormUrlEncodedContent BulkForm(
        string token,
        IEnumerable<Guid> ticked,
        params (string Name, string Value)[] fields)
    {
        var form = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token) };
        form.AddRange(ticked.Select(id => new KeyValuePair<string, string>("ids", id.ToString("D"))));
        form.AddRange(fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value)));
        return new FormUrlEncodedContent(form);
    }

    /// <summary>The shell's one-time confirmation notice, or null when the page carries none.</summary>
    private static string? Confirmation(string html)
    {
        var match = Regex.Match(html, "data-confirmation>[\\s\\S]*?<span>(?<text>[^<]*)</span>");
        return match.Success ? WebUtility.HtmlDecode(match.Groups["text"].Value) : null;
    }

    private static async Task<Guid[]> DismissedMessagesAsync(IntakeWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        return (await context.RetainedMailboxMessages
            .Where(item => item.DismissedAtUtc != null)
            .Select(item => item.Id)
            .ToArrayAsync())
            .Order()
            .ToArray();
    }

    private static async Task<int> DismissalHistoryCountAsync(IntakeWebApplicationFactory factory, string eventKind)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.ActionHistory
            .CountAsync(item => item.AggregateType == "retained_mail" && item.EventKind == eventKind);
    }

    /// <summary>
    /// The message record carries Dismiss (Restore once dismissed), and its
    /// Attachments tab states an outcome per attachment in operator words with
    /// nothing linking to a receipt.
    /// </summary>
    [Fact]
    public async Task TheMessageRecordDismissesAndStatesAttachmentOutcomes()
    {
        using var factory = new IntakeWebApplicationFactory();
        var messageId = Assert.Single(await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        using var client = IntakeWebDriver.CreateClient(factory);

        var record = await GetHtmlAsync(client, $"/Inbox/{messageId:D}");
        Assert.Contains("data-message-dismissal=\"dismiss\"", record, StringComparison.Ordinal);
        Assert.DoesNotContain("data-record-kind", record, StringComparison.Ordinal);

        var attachments = await GetHtmlAsync(client, $"/Inbox/{messageId:D}?section=attachments");
        var table = Between(attachments, "data-message-attachments", "</table>");
        Assert.Contains("<th scope=\"col\">Outcome</th>", table, StringComparison.Ordinal);
        Assert.Contains("estimate.pdf", table, StringComparison.Ordinal);
        // The seeded message has no intake receipt yet, so its outcome is stated plainly.
        Assert.Contains("data-attachment-outcome=\"Not yet processed\"", table, StringComparison.Ordinal);
        Assert.DoesNotContain("Retained", table, StringComparison.Ordinal);

        var dismissForm = Regex.Match(record, "<form[^>]*data-message-dismissal=\"dismiss\"[^>]*>[\\s\\S]*?</form>").Value;
        using (var dismiss = await client.PostAsync(
            WebUtility.HtmlDecode(FormAction(dismissForm)),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = FormInput(record, "__RequestVerificationToken"),
                ["operationKey"] = FormInput(dismissForm, "operationKey")
            })))
        {
            Assert.Equal(HttpStatusCode.Redirect, dismiss.StatusCode);
            Assert.StartsWith($"/Inbox/{messageId:D}", dismiss.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        var dismissedRecord = await GetHtmlAsync(client, $"/Inbox/{messageId:D}?folder=dismissed");
        Assert.Contains("data-message-dismissal=\"restore\"", dismissedRecord, StringComparison.Ordinal);
        Assert.DoesNotContain("This message is no longer in the view you opened it from.", dismissedRecord, StringComparison.Ordinal);
    }

    private static string RowActionForm(string html, string action, Guid messageId)
    {
        var row = Regex.Match(
            html,
            $"<div[^>]*data-mail-row=\"{messageId:D}\"[\\s\\S]*?<form[^>]*data-mail-row-action=\"{action}\"[^>]*>[\\s\\S]*?</form>",
            RegexOptions.IgnoreCase);
        Assert.True(row.Success, $"The {action} row action for {messageId:D} was not rendered.");
        return row.Value[row.Value.LastIndexOf("<form", StringComparison.Ordinal)..];
    }

    private static string FormAction(string form)
    {
        // Anchored on a preceding space: data-mail-row-action="…" also ends in action=".
        var match = Regex.Match(form, "\\saction=\"(?<action>[^\"]+)\"");
        Assert.True(match.Success, "The form has no action.");
        return WebUtility.HtmlDecode(match.Groups["action"].Value);
    }

    private static string FormInput(string html, string name)
    {
        var tag = Regex.Match(html, $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*>");
        Assert.True(tag.Success, $"The input '{name}' was not rendered.");
        return WebUtility.HtmlDecode(Regex.Match(tag.Value, "value=\"(?<value>[^\"]*)\"").Groups["value"].Value);
    }

    private static async Task PostNotFoundAsync(
        HttpClient client,
        string route,
        IReadOnlyDictionary<string, string> fields)
    {
        using var response = await client.PostAsync(route, new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ANonmatchingThreadMemberIsMarkedOutsideTheActiveSearchView()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 2);
        await StoreSearchProjectionAsync(
            factory,
            FirstMailboxId,
            FirstMailboxId + "-1",
            "Needle appears in the matching message.");
        await StoreSearchProjectionAsync(
            factory,
            FirstMailboxId,
            FirstMailboxId + "-0",
            "Different thread message body.");
        using var client = IntakeWebDriver.CreateClient(factory);

        var matching = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}?search=needle&section=thread");
        var nonmatching = await GetHtmlAsync(client, $"/Inbox/{ids[1]:D}?search=needle&section=thread");

        Assert.Contains("search=needle", matching, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "This message is no longer in the view you opened it from.",
            matching,
            StringComparison.Ordinal);
        Assert.Contains(
            "This message is no longer in the view you opened it from.",
            nonmatching,
            StringComparison.Ordinal);
        Assert.Contains("search=needle", nonmatching, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = text.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }

    private static string Between(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"'{start}' was not rendered.");
        var to = text.IndexOf(end, from, StringComparison.Ordinal);
        Assert.True(to > from, $"'{end}' was not rendered after '{start}'.");
        return text[from..to];
    }

    private static string MailRow(string html, Guid messageId)
    {
        var marker = $"data-mail-row=\"{messageId:D}\"";
        var markerIndex = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, $"The retained message row {messageId:D} was not rendered.");
        var start = html.LastIndexOf("<div class=\"row-button", markerIndex, StringComparison.Ordinal);
        var end = html.IndexOf("</div>", markerIndex, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"The retained message row {messageId:D} was incomplete.");
        return html[start..end];
    }

    private static string AssociationAction(string html, string handler)
    {
        var match = Regex.Match(
            html,
            $"<form method=\"post\" action=\"([^\"]*handler={Regex.Escape(handler)}[^\"]*)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"The {handler} confirmation action was not rendered.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    /// <summary>
    /// The one post a record's reason dialog makes (v41 I13): its rendered action and
    /// hidden fields, the reason, and the Case / PO the picker held, for a link.
    /// </summary>
    private static AssociationPost DialogSubmission(
        string html,
        string handler,
        string reason,
        string? caseReference = null)
    {
        var form = AssociationForm(html, handler);
        var fields = HiddenFields(form);
        fields["Reason"] = reason;
        if (caseReference is not null)
        {
            fields["caseReference"] = caseReference;
        }
        return new(AssociationAction(form, handler), fields);
    }

    private static string AssociationForm(string html, string handler)
    {
        var match = Regex.Match(
            html,
            $"<form method=\"post\" action=\"[^\"]*handler={Regex.Escape(handler)}[^\"]*\"[^>]*>.*?</form>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"The {handler} form was not rendered.");
        return match.Value;
    }

    private static Dictionary<string, string> HiddenFields(string form) => Regex.Matches(
            form,
            "<input[^>]*name=\"([^\"]+)\"[^>]*value=\"([^\"]*)\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
        .Cast<Match>()
        .ToDictionary(
            match => WebUtility.HtmlDecode(match.Groups[1].Value),
            match => WebUtility.HtmlDecode(match.Groups[2].Value),
            StringComparer.Ordinal);

    private static async Task AssertAssociationStateAsync(
        IntakeWebApplicationFactory factory,
        Guid receiptId,
        Guid? expectedCaseId,
        int expectedHistoryCount)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var association = await context.IntakeManualAssociations
            .SingleOrDefaultAsync(item => item.IntakeReceiptId == receiptId && item.IsActive);
        Assert.Equal(expectedCaseId, association?.CaseId);
        Assert.Equal(
            expectedHistoryCount,
            await context.IntakeMutationHistory.CountAsync(item => item.IntakeReceiptId == receiptId));
    }

    private static async Task<string?> CaseLeaseTokenAsync(
        IntakeWebApplicationFactory factory,
        Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.CaseWorkflows
            .Where(item => item.CaseId == caseId)
            .Select(item => item.EditLeaseToken)
            .SingleAsync();
    }

    private sealed record AssociationPost(
        string Action,
        Dictionary<string, string> Fields);

    private sealed class ReleaseFailureGate
    {
        private int attempts;

        public int AttemptCount => Volatile.Read(ref attempts);

        public bool FailThisAttempt() => Interlocked.Increment(ref attempts) == 1;
    }

    /// <summary>The first link fails for good, as a Core refusal does; every later one is the real command.</summary>
    private sealed class FailingFirstLinkIntake(ILinkIntake inner, ReleaseFailureGate gate) : ILinkIntake
    {
        public Task ExecuteAsync(
            LinkIntakeRequest request,
            CancellationToken cancellationToken = default) =>
            gate.FailThisAttempt()
                ? Task.FromException(new InvalidOperationException("Fixture link failure."))
                : inner.ExecuteAsync(request, cancellationToken);
    }

    private sealed class FailOnceReleaseCaseEditLease(
        ILeaseCaseForEdit leases,
        ReleaseFailureGate failures) : IReleaseCaseEditLease
    {
        private readonly ReleaseCaseEditLease release = new(leases);

        public Task ExecuteAsync(
            ReleaseCaseEditLeaseRequest request,
            CancellationToken cancellationToken) =>
            failures.FailThisAttempt()
                ? Task.FromException(new TimeoutException("Fixture release timeout."))
                : release.ExecuteAsync(request, cancellationToken);
    }

    /// <summary>
    /// Retains <paramref name="count"/> messages in a mailbox, classifies each and
    /// accepts its receipt into a Case, the way <c>TheInboxPagesSendTheirPinnedStatements</c> seeds its one.
    /// </summary>
    private static async Task<Guid[]> SeedAcceptedMessagesAsync(
        IntakeWebApplicationFactory factory,
        string mailboxId,
        string mailboxAddress,
        int count)
    {
        var messageIds = await SeedAsync(factory, mailboxId, mailboxAddress, count);
        for (var index = 0; index < count; index++)
        {
            var immutableMessageId = $"{mailboxId}-{index}";
            await StoreClassificationAsync(factory, mailboxId, immutableMessageId);
            _ = await AcceptReceiptAsync(
                factory, await ReceiptIdAsync(factory, mailboxId, immutableMessageId));
        }

        return messageIds;
    }

    /// <summary>
    /// Accepts a seeded mailbox receipt through the real acceptance command, so
    /// the resulting case is genuinely this receipt's own.
    /// </summary>
    private static async Task<CaseAcceptanceOutcome> AcceptReceiptAsync(
        IntakeWebApplicationFactory factory,
        Guid receiptId)
    {
        await SeedPrincipalAsync(factory.Services);
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        long version;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            version = await context.IntakeReceipts
                .AsNoTracking()
                .Where(item => item.Id == receiptId)
                .Select(item => item.Version)
                .SingleAsync();
        }

        return await scope.ServiceProvider.GetRequiredService<IAcceptIntake>()
            .ExecuteAsync(
                new(
                    receiptId,
                    version,
                    ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
                    $"mail-unlink-accept:{Guid.NewGuid():N}",
                    CaseType.Inspection,
                    QdosPrincipal.Code,
                    new(true, true)),
                CancellationToken.None);
    }

    /// <summary>
    /// QDOS is one of the shared foundation migration's seeded principals, so
    /// this resolves the seed rather than inserting a second one of its own.
    /// </summary>
    private static async Task SeedPrincipalAsync(IServiceProvider services) =>
        await SeededPrincipals.QdosAsync(services);

    private static async Task<CaseLifecycleState> ReadCaseStateAsync(
        IServiceProvider services,
        Guid caseId)
    {
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var state = await context.CaseWorkflows
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .Select(item => item.State)
            .SingleAsync();
        return Enum.Parse<CaseLifecycleState>(state);
    }

    private static async Task<Guid> ReceiptIdAsync(
        IntakeWebApplicationFactory factory,
        string mailboxId,
        string messageId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var externalToken = $"{mailboxId.Length}:{mailboxId}{messageId}";
        return await context.IntakeReceipts
            .Where(item => item.SourceChannel == "mailbox" && item.ExternalReceiptToken == externalToken)
            .Select(item => item.Id)
            .SingleAsync();
    }

    private static async Task<Guid> MessageIdAsync(
        IntakeWebApplicationFactory factory,
        string mailboxId,
        string immutableMessageId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.RetainedMailboxMessages
            .Where(item => item.MailboxId == TestMailboxId.From(mailboxId) && item.ImmutableMessageId == immutableMessageId)
            .Select(item => item.Id)
            .SingleAsync();
    }

    private static async Task<string> CaseReferenceAsync(
        IntakeWebApplicationFactory factory,
        Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Cases
            .Where(item => item.Id == caseId)
            .Select(item => item.Reference)
            .SingleAsync();
    }

    private static string AntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(match.Success, "The antiforgery token was not rendered.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static string HiddenValue(string html, string name)
    {
        var match = Regex.Match(
            html,
            $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"The hidden input '{name}' was not rendered.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    /// <summary>Makes a seeded mailbox able to send: Outgoing, with its Sent Items and a generation.</summary>
    private static async Task MakeSendReadyAsync(IntakeWebApplicationFactory factory, string mailboxId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var mailbox = await context.ApprovedMailboxes.SingleAsync(item => item.Id == TestMailboxId.From(mailboxId));
        mailbox.AllowOutgoing = true;
        mailbox.SentFolderIdentity = "sent-items";
        mailbox.MailboxGeneration = Math.Max(1, mailbox.MailboxGeneration);
        await context.SaveChangesAsync();
    }

    private static async Task<Guid[]> SeedAsync(
        IntakeWebApplicationFactory factory,
        string mailboxId,
        string mailboxAddress,
        int count,
        DateTimeOffset? lastCompletedAtUtc = null,
        IReadOnlyList<RetainedMailboxAttachment>? attachments = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var approvedMailboxId = await TestMailboxId.EnsureApprovedAsync(
                context, mailboxId, mailboxAddress, NowUtc.AddDays(-1));
            if (!await context.ApprovedInboxPollStates.AnyAsync(item => item.ApprovedMailboxId == approvedMailboxId))
            {
                context.ApprovedInboxPollStates.Add(new()
                {
                    ApprovedMailboxId = approvedMailboxId,
                    MailboxAddress = mailboxAddress,
                    ScopeFingerprint = new string('A', 64),
                    ActivatedAtUtc = NowUtc.AddDays(-1),
                    DueAtUtc = NowUtc,
                    LastCompletedAtUtc = lastCompletedAtUtc ?? NowUtc.AddMinutes(-1)
                });
                await context.SaveChangesAsync();
            }
        }

        var store = scope.ServiceProvider.GetRequiredService<EfRetainedMailboxMessageStore>();
        for (var index = 0; index < count; index++)
        {
            var identity = $"{mailboxId}-{index}";
            await store.RetainAsync(
                new(
                    TestMailboxId.From(mailboxId),
                    mailboxAddress,
                    identity,
                    $"{mailboxId.Length}:{mailboxId}{identity}",
                    NowUtc.AddMinutes(-count + index),
                    1024,
                    new string('A', 64),
                    new(
                        "inbox",
                        $"conversation-{mailboxId}",
                        $"<{identity}@example.invalid>",
                        [],
                        "sender@example.invalid",
                        "A Sender",
                        ["intake@collisionengineers.co.uk"],
                        [],
                        [],
                        $"Message {index} from {mailboxId}",
                        "Please inspect the vehicle at the address supplied.",
                        attachments ?? [new("estimate.pdf", "application/pdf", 2048)],
                        IsRead: false),
                    NowUtc),
                CancellationToken.None);
        }

        await using var readContext = await contextFactory.CreateDbContextAsync();
        return await readContext.RetainedMailboxMessages
            .AsNoTracking()
            .Where(item => item.MailboxId == TestMailboxId.From(mailboxId))
            .OrderByDescending(item => item.ReceivedAtUtc)
            .Select(item => item.Id)
            .ToArrayAsync();
    }

    private static Task StoreForwardedRouteAsync(
        IntakeWebApplicationFactory factory,
        string mailboxId,
        string messageId) =>
        StoreOutcomeReceiptAsync(factory, mailboxId, messageId, IntakeDecision.NeedsSorting);

    private static async Task StoreOutcomeReceiptAsync(
        IntakeWebApplicationFactory factory,
        string mailboxId,
        string messageId,
        IntakeDecision decision)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>().StoreAsync(
            new(
                SourceFileName: "forwarded.eml",
                MediaType: "message/rfc822",
                SourceLength: 1,
                SourceHash: new string('B', 64),
                SourceIdentity: new(IntakeSourceChannel.Mailbox, mailboxId.Length + ":" + mailboxId + messageId),
                ReceivedAtUtc: NowUtc,
                ProcessedAtUtc: NowUtc,
                Actor: "system-worker:approved-inbox-poller",
                Decision: decision,
                DecisionReason: "Fixture evaluation.",
                Evidence: [],
                Fields: [],
                InstructionDraft: null,
                MissingFields: [],
                FailureCode: null,
                FailureReason: null,
                SourceReaderKey: "protocol_reader",
                SourceReaderVersion: "1",
                ExtractionPolicyKey: "protocol_policy",
                ExtractionPolicyVersion: 1,
                Assets: [],
                MailRouteDecision: new(
                    MailRouteDisposition.Accepted,
                    new("QDOS", MailRouteKind.DirectPrincipal, "QDOS"),
                    [],
                    "Fixture accepted route.",
                    "qdos_mail_route",
                    1,
                    [new("desk@collisionengineers.co.uk", "transport")],
                    [new("original@qdosassist.co.uk", "inline forward")],
                    new("original@qdosassist.co.uk", "inline forward"))),
            CancellationToken.None);
    }

    private static async Task StoreClassificationAsync(
        IntakeWebApplicationFactory factory,
        string mailboxId,
        string messageId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>().StoreAsync(
            new(
                SourceFileName: "classified.eml",
                MediaType: "message/rfc822",
                SourceLength: 1,
                SourceHash: new string('D', 64),
                SourceIdentity: new(IntakeSourceChannel.Mailbox, mailboxId.Length + ":" + mailboxId + messageId),
                ReceivedAtUtc: NowUtc,
                ProcessedAtUtc: NowUtc,
                Actor: "system-worker:approved-inbox-poller",
                Decision: IntakeDecision.NeedsSorting,
                DecisionReason: "Fixture evaluation.",
                Evidence: [],
                Fields: [],
                InstructionDraft: null,
                MissingFields: [],
                FailureCode: null,
                FailureReason: null,
                SourceReaderKey: "protocol_reader",
                SourceReaderVersion: "1",
                ExtractionPolicyKey: "protocol_policy",
                ExtractionPolicyVersion: 1,
                Assets: [],
                MailClassificationDecision: MailClassificationResult.Unclassified(
                    [new("sender-domain", false, "The sender domain is not recognized.")],
                    "No supported category matched.",
                    "shared-mail-policy",
                    3)),
            CancellationToken.None);
    }

    private static async Task StoreMailClassificationAsync(
        IntakeWebApplicationFactory factory,
        string mailboxId,
        string messageId,
        MailClassificationResult classification)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>().StoreAsync(
            new(
                SourceFileName: "mail-view.eml",
                MediaType: "message/rfc822",
                SourceLength: 1,
                SourceHash: new string('F', 64),
                SourceIdentity: new(
                    IntakeSourceChannel.Mailbox,
                    mailboxId.Length + ":" + mailboxId + messageId),
                ReceivedAtUtc: NowUtc,
                ProcessedAtUtc: NowUtc,
                Actor: "system-worker:approved-inbox-poller",
                Decision: IntakeDecision.NeedsSorting,
                DecisionReason: "Fixture evaluation.",
                Evidence: [],
                Fields: [],
                InstructionDraft: null,
                MissingFields: [],
                FailureCode: null,
                FailureReason: null,
                SourceReaderKey: "protocol_reader",
                SourceReaderVersion: "1",
                ExtractionPolicyKey: "protocol_policy",
                ExtractionPolicyVersion: 1,
                Assets: [],
                MailClassificationDecision: classification),
            CancellationToken.None);
    }

    private static async Task StoreClassifiedInstructionAsync(
        IntakeWebApplicationFactory factory,
        string mailboxId,
        string messageId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>().StoreAsync(
            new(
                SourceFileName: "classified-instruction.eml",
                MediaType: "message/rfc822",
                SourceLength: 1,
                SourceHash: new string('E', 64),
                SourceIdentity: new(IntakeSourceChannel.Mailbox, mailboxId.Length + ":" + mailboxId + messageId),
                ReceivedAtUtc: NowUtc,
                ProcessedAtUtc: NowUtc,
                Actor: "system-worker:approved-inbox-poller",
                Decision: IntakeDecision.NeedsSorting,
                DecisionReason: "Fixture evaluation.",
                Evidence: [],
                Fields: [],
                InstructionDraft: null,
                MissingFields: [],
                FailureCode: null,
                FailureReason: null,
                SourceReaderKey: "protocol_reader",
                SourceReaderVersion: "1",
                ExtractionPolicyKey: "protocol_policy",
                ExtractionPolicyVersion: 1,
                Assets: [],
                MailClassificationDecision: MailClassificationResult.Classified(
                    MailCategory.NewInstruction,
                    [new("attachment.engineer-notification", true, "An attached document contains the generated title.")],
                    "An accepted Inspection instruction was recognised.",
                    "qdos_mail_classification",
                    3)),
            CancellationToken.None);
    }

    private sealed class MovableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset now = utcNow;

        internal void Advance(TimeSpan amount) => now = now.Add(amount);

        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>
    /// C08: <see cref="CorrectRetainedMailClassification"/>'s store, recorded
    /// rather than backed by real persistence — no read/open/preview/filter
    /// path calls it, so a call here is itself the proof of an accidental
    /// write.
    /// </summary>
    private sealed class RecordingClassificationStore : IRetainedMailClassificationStore
    {
        public int CorrectionCalls { get; private set; }

        public Task<MailClassificationDossier?> GetClassificationAsync(
            Guid messageId, CancellationToken cancellationToken) =>
            Task.FromResult<MailClassificationDossier?>(null);

        public Task<MailClassificationCorrectionResult> AppendCorrectionAsync(
            Guid messageId,
            int expectedVersion,
            MailClassificationResult before,
            MailClassificationResult after,
            ActionActor actor,
            string reason,
            DateTimeOffset correctedAtUtc,
            CancellationToken cancellationToken)
        {
            CorrectionCalls++;
            throw new InvalidOperationException(
                "A read-only mail workspace action attempted to write a classification correction.");
        }
    }

    private static async Task StoreSearchProjectionAsync(
        IntakeWebApplicationFactory factory,
        string mailboxId,
        string messageId,
        string body)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>().StoreAsync(
            new(
                SourceFileName: "search.eml",
                MediaType: "message/rfc822",
                SourceLength: 1,
                SourceHash: new string('F', 64),
                SourceIdentity: new(
                    IntakeSourceChannel.Mailbox,
                    mailboxId.Length + ":" + mailboxId + messageId),
                ReceivedAtUtc: NowUtc,
                ProcessedAtUtc: NowUtc,
                Actor: "system-worker:approved-inbox-poller",
                Decision: IntakeDecision.NeedsSorting,
                DecisionReason: "Fixture evaluation.",
                Evidence: [],
                Fields: [],
                InstructionDraft: null,
                MissingFields: [],
                FailureCode: null,
                FailureReason: null,
                SourceReaderKey: "protocol_reader",
                SourceReaderVersion: "1",
                ExtractionPolicyKey: "protocol_policy",
                ExtractionPolicyVersion: 1,
                Assets: [],
                SearchDocuments: [new("message body", null, body)]),
            CancellationToken.None);
    }

    private sealed class RecordingDeletedMailSearchSource : IDeletedMailSearchSource
    {
        internal Guid? MailboxId { get; private set; }

        internal string? SearchTerm { get; private set; }

        internal int MaximumMessages { get; private set; }

        internal int SearchCount { get; private set; }

        internal DeletedMailSourceResult Result { get; set; } = new(
            Enumerable.Range(0, 26)
                .Select(index => new DeletedMailSearchItem(
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    "empty@example.invalid",
                    $"deleted-{index}",
                    "sender@example.invalid",
                    "A Sender",
                    $"Deleted match {index}",
                    "The visible body also contains needle.",
                    NowUtc.AddMinutes(index),
                    IsRead: false,
                    [new("proof.pdf", "application/pdf", 1024, IsSearchable: true)],
                    [new(MailSearchMatchKind.AttachmentContent, "proof.pdf", 0)]))
                .ToArray(),
            IsTruncated: true);

        public Task<IReadOnlyList<RetainedMailMailbox>> ListMailboxesAsync(
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<RetainedMailMailbox>>(
                [
                    new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "empty@example.invalid", IsPolled: true),
                    new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "other@example.invalid", IsPolled: true)
                ]);

        public Task<DeletedMailSourceResult> SearchAsync(
            Guid? mailboxId,
            string searchTerm,
            int maximumMessages,
            CancellationToken cancellationToken)
        {
            MailboxId = mailboxId;
            SearchTerm = searchTerm;
            MaximumMessages = maximumMessages;
            SearchCount++;
            return Task.FromResult(Result);
        }
    }

    private sealed class FailingCredential : TokenCredential
    {
        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) => throw new AuthenticationFailedException("Credential unavailable.");

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) => ValueTask.FromException<AccessToken>(
                new AuthenticationFailedException("Credential unavailable."));
    }

    private sealed class SuccessfulCredential : TokenCredential
    {
        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            new("token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) => ValueTask.FromResult(
                GetToken(requestContext, cancellationToken));
    }

    private sealed class MalformedDeletedGraphHandler(string responseCase) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var isFolder = request.RequestUri!.AbsolutePath.EndsWith(
                "/mailFolders/deleteditems",
                StringComparison.Ordinal);
            var body = (responseCase, isFolder) switch
            {
                ("folder-root", true) => "[]",
                (_, true) => """{"id":"deleted-folder"}""",
                ("missing-value", false) => "{}",
                ("relative-next-link", false) =>
                    """{"value":[],"@odata.nextLink":"/v1.0/users/empty-mailbox/mailFolders/deleted-folder/messages?$top=100"}""",
                _ => throw new InvalidOperationException("Unknown malformed Graph response case.")
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class UnexpectedHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => throw new InvalidOperationException(
                "HTTP must not be called after credential acquisition fails.");
    }

    private sealed class ApprovedMailboxEstate(IReadOnlyList<ApprovedIntakeMailbox> mailboxes)
        : IApprovedIntakeMailboxes
    {
        public Task<IReadOnlyList<ApprovedIntakeMailbox>> ListPollableAsync(
            CancellationToken cancellationToken) => Task.FromResult(mailboxes);
    }
}
