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
    private static readonly string[] CategoryFilterValues =
        ["", .. MailCategories.All.Select(MailCategories.Code), "no_case_found"];
    private static readonly string[] CorrectionValues =
        ["", .. MailCategories.All.Select(MailCategories.Code), "unclassified"];

    private const string FirstMailboxId = "instructions";
    private static string FirstMailboxFilter => TestMailboxId.From(FirstMailboxId).ToString("D");
    private const string FirstMailboxAddress = "instructions@collisionengineers.co.uk";
    private const string SecondMailboxId = "reports";
    private const string SecondMailboxAddress = "reports@collisionengineers.co.uk";

    /// <summary>The Inbox rows as a row list; the shell's notification list carries the same hook on every page.</summary>
    private const string InboxRowList = "<div data-row-list>";

    [Fact]
    public async Task QuickPreviewIsAuthenticatedExactEvidenceAndDoesNotMutateMailState()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var messageId = Assert.Single(await SeedAsync(
            factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Inbox");
        Assert.Contains("data-mail-preview-workspace", html, StringComparison.Ordinal);
        Assert.Contains("data-mail-preview-trigger", html, StringComparison.Ordinal);
        Assert.Contains("handler=Preview", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(messageId.ToString("D"), html, StringComparison.OrdinalIgnoreCase);
        var previewMarkup = Between(html, "<aside id=\"mail-quick-preview\"", "</aside>");
        Assert.DoesNotContain("<form", previewMarkup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<button", previewMarkup, StringComparison.OrdinalIgnoreCase);

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

        using var response = await client.GetAsync($"/Inbox?handler=Preview&id={messageId:D}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var preview = document.RootElement;
        Assert.Equal(messageId, preview.GetProperty("id").GetGuid());
        Assert.Equal("A Sender", preview.GetProperty("sender").GetString());
        Assert.Equal($"Message 0 from {FirstMailboxId}", preview.GetProperty("subject").GetString());
        // The pane's every cell: the chip with its tone, the mailbox, the
        // folder and one attachment line by kind, never the names.
        Assert.Equal(FirstMailboxAddress, preview.GetProperty("mailbox").GetString());
        Assert.Equal("Not yet processed", preview.GetProperty("state").GetString());
        Assert.Equal("amber", preview.GetProperty("stateTone").GetString());
        Assert.Equal("Please inspect the vehicle at the address supplied.", preview.GetProperty("excerpt").GetString());
        Assert.Equal("1 attachment · 1 document", preview.GetProperty("attachments").GetString());
        Assert.Equal("Not yet processed", preview.GetProperty("classification").GetString());
        Assert.Equal("None", preview.GetProperty("association").GetString());
        Assert.Equal("Inbox", preview.GetProperty("folder").GetString());
        Assert.DoesNotContain("estimate.pdf", json, StringComparison.Ordinal);
        // No Case, so a pinned row's pane offers no Case action.
        Assert.True(!preview.TryGetProperty("caseUrl", out var caseUrl) || caseUrl.ValueKind == JsonValueKind.Null);

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

        using var unknown = await client.GetAsync($"/Inbox?handler=Preview&id={Guid.NewGuid():D}");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        using var rolelessRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Inbox?handler=Preview&id={messageId:D}");
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
    /// sort/page are URL and retained-query state only — opening, previewing,
    /// filtering or changing the unread scope never reaches Outlook or writes
    /// a classification correction. <see cref="RecordingClassificationStore"/>
    /// stands in for <see cref="CorrectRetainedMailClassification"/>'s store
    /// and is asserted at zero after every read action below.
    /// </summary>
    [Fact]
    public async Task OpenPreviewFilterUnreadAndSortNeverWriteThroughTheRetainedMailPorts()
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
        // preview a row, then open and return from the full message —
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
        var triggerIndex = listHtml.IndexOf("data-mail-preview-trigger", StringComparison.Ordinal);
        Assert.True(triggerIndex >= 0, "expected at least one rendered row (data-mail-preview-trigger) for this query.");
        var rowAnchorStart = listHtml.LastIndexOf("<a", triggerIndex, StringComparison.Ordinal);
        var rowAnchorEnd = listHtml.IndexOf('>', triggerIndex);
        Assert.True(rowAnchorStart >= 0 && rowAnchorEnd > triggerIndex,
            "expected data-mail-preview-trigger inside a complete row anchor.");
        var rowAnchor = listHtml[rowAnchorStart..(rowAnchorEnd + 1)];
        var rowHrefMatch = Regex.Match(rowAnchor, "href=\"(?<href>[^\"]+)\"", RegexOptions.IgnoreCase);
        Assert.True(rowHrefMatch.Success, $"expected the rendered row anchor to carry an href: {rowAnchor}");
        var rowHref = WebUtility.HtmlDecode(rowHrefMatch.Groups["href"].Value);
        // No Unread scope (Inbox, 13 September): the row link never carries one.
        Assert.DoesNotContain("unread=", rowHref, StringComparison.Ordinal);
        Assert.Contains("sort=oldest", rowHref, StringComparison.Ordinal);

        // Diagnostic for a non-OK preview: confirm whether the seeded row is
        // still present (rules out a seeding/dedup gap) before asserting, so a
        // failure names the actual cause instead of only the status code.
        using (var previewResponse = await client.GetAsync($"/Inbox?handler=Preview&id={ids[0]:D}"))
        {
            if (previewResponse.StatusCode != HttpStatusCode.OK)
            {
                var previewBody = await previewResponse.Content.ReadAsStringAsync();
                await using var scope = factory.Services.CreateAsyncScope();
                var contextFactory = scope.ServiceProvider
                    .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
                await using var context = await contextFactory.CreateDbContextAsync();
                var rowExists = await context.RetainedMailboxMessages
                    .AsNoTracking()
                    .AnyAsync(item => item.Id == ids[0]);
                Assert.Fail(
                    $"Preview for {ids[0]:D} returned {(int)previewResponse.StatusCode} "
                    + $"{previewResponse.StatusCode} (row exists in DB: {rowExists}). Body: {previewBody}");
            }
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
            client, $"/Inbox/{messageId:D}?mailbox={FirstMailboxFilter}&section=case");
        var confirmation = await PrepareAssociationAsync(client, page, "PrepareUnlinkCase");
        Assert.Contains(
            $"Unlinking this email cancels case {outcome.Identity.Reference}.",
            confirmation,
            StringComparison.Ordinal);

        var submission = AssociationSubmission(
            confirmation,
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
            client, $"/Inbox/{messageId:D}?mailbox={FirstMailboxFilter}&section=case");
        Assert.DoesNotContain("cancels case", unlinked, StringComparison.Ordinal);
    }

    /// <summary>
    /// What the Inbox pages cost in SQL commands against the real stores
    /// (Roadmap Lane D, parts D1 and D3): the message page of a message linked to
    /// a Case, the list with its first row selected, and the list's Preview
    /// fragment. The message page reads the Case header, not the whole Case; the
    /// list draws its pane from its own row and reads only what the row lacks.
    /// All three counts are ceilings, so growth in one is seen, and a failure
    /// lists every command sent.
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
        _ = await GetHtmlAsync(client, $"/Inbox?handler=Preview&id={messageId:D}");

        counter.Reset();
        var message = await GetHtmlAsync(client, $"/Inbox/{messageId:D}");
        var messageCommands = counter.Count;
        var messageDescription = counter.Describe();
        var classificationDecisionReads = counter.CountMentioning("[IntakeMailClassificationDecisions]");
        Assert.Contains(messageId.ToString("D"), message, StringComparison.Ordinal);

        counter.Reset();
        var list = await GetHtmlAsync(client, "/Inbox");
        var listCommands = counter.Count;
        var listDescription = counter.Describe();
        Assert.Contains("data-mail-preview-classification", list, StringComparison.Ordinal);

        counter.Reset();
        _ = await GetHtmlAsync(client, $"/Inbox?handler=Preview&id={messageId:D}");
        var previewCommands = counter.Count;
        var previewDescription = counter.Describe();

        Assert.True(
            messageCommands <= InboxMessageCommands,
            $"The Inbox message page sent {messageCommands} SQL commands; at most {InboxMessageCommands} are allowed."
            + Environment.NewLine + messageDescription);
        Assert.Equal(2, classificationDecisionReads);
        Assert.True(
            listCommands <= InboxListCommands,
            $"The Inbox list sent {listCommands} SQL commands; at most {InboxListCommands} are allowed."
            + Environment.NewLine + listDescription);
        Assert.True(
            previewCommands <= InboxPreviewCommands,
            $"The Inbox preview sent {previewCommands} SQL commands; at most {InboxPreviewCommands} are allowed."
            + Environment.NewLine + previewDescription);
    }

    private const int InboxMessageCommands = 19;

    private const int InboxListCommands = 14;

    private const int InboxPreviewCommands = 6;

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
        using var client = CreateClient(factory);
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

    [Fact]
    public async Task ExactMessageCanBeSearchedLinkedUnlinkedAndLinkedToAReplacement()
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

        var search = await GetHtmlAsync(
            client,
            $"/Inbox/{messageId:D}?mailbox={FirstMailboxFilter}&pageNumber=2&caseQuery=MAIL31001");
        Assert.Contains(">MAIL31001</strong>", search, StringComparison.Ordinal);
        Assert.Contains($"mailbox={FirstMailboxFilter}", search, StringComparison.Ordinal);
        Assert.Contains("pageNumber=2", search, StringComparison.Ordinal);

        var target = await GetHtmlAsync(
            client,
            $"/Inbox/{messageId:D}?mailbox={FirstMailboxFilter}&pageNumber=2&caseQuery=MAIL31001&targetCaseId={firstCaseId:D}");
        Assert.Contains("Confirm target", target, StringComparison.Ordinal);
        Assert.Contains("MAIL31001", target, StringComparison.Ordinal);
        Assert.DoesNotContain("Confirm unlink", target, StringComparison.Ordinal);
        var linkConfirmation = await PrepareAssociationAsync(client, target, "PrepareLinkCase");
        var linkSubmission = AssociationSubmission(
            linkConfirmation,
            "LinkCase",
            "The retained message names this exact Case/PO.");
        using var link = await client.PostAsync(
            linkSubmission.Action,
            new FormUrlEncodedContent(linkSubmission.Fields));
        Assert.Equal(HttpStatusCode.Redirect, link.StatusCode);
        using var linkReplay = await client.PostAsync(
            linkSubmission.Action,
            new FormUrlEncodedContent(linkSubmission.Fields));
        Assert.Equal(HttpStatusCode.Redirect, linkReplay.StatusCode);

        var linked = await GetHtmlAsync(client, link.Headers.Location!.ToString());
        Assert.Contains("Message linked to the confirmed case.", linked, StringComparison.Ordinal);
        Assert.Contains("Linked case", linked, StringComparison.Ordinal);
        Assert.Contains(">Unlink</button>", linked, StringComparison.Ordinal);
        Assert.DoesNotContain("association-case-query", linked, StringComparison.Ordinal);
        await AssertAssociationStateAsync(factory, receiptId, firstCaseId, expectedHistoryCount: 1);

        var conflictingLinkFields = new Dictionary<string, string>(linkSubmission.Fields)
        {
            ["Reason"] = "Changed reason under the same operation key."
        };
        using var conflictingLink = await client.PostAsync(
            linkSubmission.Action,
            new FormUrlEncodedContent(conflictingLinkFields));
        Assert.Equal(HttpStatusCode.OK, conflictingLink.StatusCode);
        Assert.Contains(
            "confirmation identity was already used with different details",
            await conflictingLink.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        await AssertAssociationStateAsync(factory, receiptId, firstCaseId, expectedHistoryCount: 1);

        var unlinkConfirmation = await PrepareAssociationAsync(client, linked, "PrepareUnlinkCase");
        var unlinkSubmission = AssociationSubmission(
            unlinkConfirmation,
            "UnlinkCase",
            "The message belongs to a different Case/PO.");
        using var unlink = await client.PostAsync(
            unlinkSubmission.Action,
            new FormUrlEncodedContent(unlinkSubmission.Fields));
        Assert.Equal(HttpStatusCode.Redirect, unlink.StatusCode);
        using var unlinkReplay = await client.PostAsync(
            unlinkSubmission.Action,
            new FormUrlEncodedContent(unlinkSubmission.Fields));
        Assert.Equal(HttpStatusCode.Redirect, unlinkReplay.StatusCode);
        await AssertAssociationStateAsync(factory, receiptId, expectedCaseId: null, expectedHistoryCount: 2);

        var unlinked = await GetHtmlAsync(client, unlink.Headers.Location!.ToString());
        Assert.Contains("Message unlinked from the confirmed case.", unlinked, StringComparison.Ordinal);
        Assert.Contains("association-case-query", unlinked, StringComparison.Ordinal);
        Assert.DoesNotContain("Unlink from this case", unlinked, StringComparison.Ordinal);

        var replacement = await GetHtmlAsync(
            client,
            $"/Inbox/{messageId:D}?caseQuery=MAIL31002&targetCaseId={replacementCaseId:D}");
        var replacementConfirmation = await PrepareAssociationAsync(
            client, replacement, "PrepareLinkCase");
        var replacementSubmission = AssociationSubmission(
            replacementConfirmation,
            "LinkCase",
            "The replacement Case/PO was separately searched and confirmed.");
        using var relink = await client.PostAsync(
            replacementSubmission.Action,
            new FormUrlEncodedContent(replacementSubmission.Fields));
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

    [Fact]
    public async Task LinkedPostReportMessageCreatesAQueryResponseJobAndShowsCaseJobs()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        var (messageId, caseId, linked) = await SeedLinkedPostReportQueryAsync(factory, client);
        Assert.Contains("handler=CreateQueryResponse", linked, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.QueryResponseJobs.Create}</span>", linked, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"case-ai-jobs-title\"", linked, StringComparison.Ordinal);
        var createForm = AssociationForm(linked, "CreateQueryResponse");
        using var create = await client.PostAsync(
            AssociationAction(createForm, "CreateQueryResponse"),
            new FormUrlEncodedContent(HiddenFields(createForm)));
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var jobs = await scope.ServiceProvider
                .GetRequiredService<IAiJobQueries>()
                .ListForSubjectAsync(caseId, CancellationToken.None);
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
        Assert.Contains("id=\"case-ai-jobs-title\"", rendered, StringComparison.Ordinal);
        Assert.Contains(">Query response</td>", rendered, StringComparison.Ordinal);
        Assert.Contains(">Queued</span>", rendered, StringComparison.Ordinal);
        var jobsPanel = Between(
            rendered,
            "<section class=\"panel\" aria-labelledby=\"case-ai-jobs-title\">",
            "</section>");
        Assert.DoesNotContain(messageId.ToString("D"), jobsPanel, StringComparison.OrdinalIgnoreCase);

        // The record bar reads the live job in place of the start button (FRD-27).
        Guid jobId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            jobId = Assert.Single(await scope.ServiceProvider
                .GetRequiredService<IAiJobQueries>()
                .ListForSubjectAsync(caseId, CancellationToken.None)).JobId;
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
    /// The Case's Queries row starts the same job through the Case's own
    /// handler (FRD-27), then reads the job's state in place of the button:
    /// Queued once started, Draft ready once the client has written the draft.
    /// </summary>
    [Fact]
    public async Task TheCaseQueriesRowStartsAQueryResponseJobAndReadsItsState()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        var (messageId, caseId, _) = await SeedLinkedPostReportQueryAsync(factory, client);

        var queries = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=queries");
        var startForm = Regex.Match(
            queries,
            $"<form[^>]*data-query-ai-draft-start=\"{messageId:D}\"[^>]*>.*?</form>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(startForm.Success, "The Queries row offers no Draft reply with AI.");
        var action = WebUtility.HtmlDecode(Regex.Match(startForm.Value, "action=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value);
        using var start = await client.PostAsync(action, new FormUrlEncodedContent(HiddenFields(startForm.Value)));
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);

        Guid jobId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var job = Assert.Single(await scope.ServiceProvider
                .GetRequiredService<IAiJobQueries>()
                .ListForSubjectAsync(caseId, CancellationToken.None));
            Assert.Equal(AiJobKind.QueryResponse, job.Kind);
            Assert.Equal(caseId, job.SubjectId);
            Assert.Equal(messageId.ToString("D"), job.Instruction);
            Assert.Equal(AiJobState.Queued, job.State);
            jobId = job.JobId;
        }

        var pending = await GetHtmlAsync(client, start.Headers.Location!.ToString());
        Assert.Contains("AI reply job created.", pending, StringComparison.Ordinal);
        Assert.DoesNotContain("data-query-ai-draft-start", pending, StringComparison.Ordinal);
        Assert.Contains($"data-query-ai-pending=\"{jobId:D}\"", pending, StringComparison.Ordinal);
        Assert.Contains("Queued</span>", Between(pending, $"data-query-ai-pending=\"{jobId:D}\"", "</span>") + "</span>", StringComparison.Ordinal);

        await WriteDraftReplyAsync(factory, jobId, messageId, "Dear Ms Patel, thank you for your e-mail.");
        var ready = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=queries");
        Assert.Contains($"data-query-ai-pending=\"{jobId:D}\"", ready, StringComparison.Ordinal);
        Assert.Contains("Draft ready</span>", Between(ready, $"data-query-ai-pending=\"{jobId:D}\"", "</span>") + "</span>", StringComparison.Ordinal);
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

        var target = await GetHtmlAsync(
            client,
            $"/Inbox/{messageId:D}?caseQuery=AUTO14001&targetCaseId={caseId:D}");
        var linkConfirmation = await PrepareAssociationAsync(client, target, "PrepareLinkCase");
        var linkSubmission = AssociationSubmission(
            linkConfirmation,
            "LinkCase",
            "The retained post-report query names this Case/PO.");
        using var link = await client.PostAsync(
            linkSubmission.Action,
            new FormUrlEncodedContent(linkSubmission.Fields));
        Assert.Equal(HttpStatusCode.Redirect, link.StatusCode);

        return (messageId, caseId, await GetHtmlAsync(client, link.Headers.Location!.ToString()));
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
        var target = await GetHtmlAsync(
            client,
            $"/Inbox/{messageId:D}?caseQuery=MAIL31999&targetCaseId={caseId:D}");
        var confirmation = await PrepareAssociationAsync(client, target, "PrepareLinkCase");
        var submission = AssociationSubmission(
            confirmation,
            "LinkCase",
            "Reviewed exact retained-message evidence.");
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
    public async Task PreparedAssociationCannotMoveToAnotherMessageOrTheOtherAction()
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

        var targetA = await GetHtmlAsync(
            client,
            $"/Inbox/{messageA:D}?caseQuery=MAIL-BIND&targetCaseId={caseId:D}");
        var confirmationA = await PrepareAssociationAsync(client, targetA, "PrepareLinkCase");
        var linkA = AssociationSubmission(
            confirmationA,
            "LinkCase",
            "The exact retained message belongs to this Case/PO.");
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

        targetA = await GetHtmlAsync(
            client,
            $"/Inbox/{messageA:D}?caseQuery=MAIL-BIND&targetCaseId={caseId:D}");
        confirmationA = await PrepareAssociationAsync(client, targetA, "PrepareLinkCase");
        linkA = AssociationSubmission(
            confirmationA,
            "LinkCase",
            "The exact retained message belongs to this Case/PO.");
        using var linked = await client.PostAsync(
            linkA.Action,
            new FormUrlEncodedContent(linkA.Fields));
        Assert.Equal(HttpStatusCode.Redirect, linked.StatusCode);

        using var linkAsUnlink = await client.PostAsync(
            linkA.Action.Replace("handler=LinkCase", "handler=UnlinkCase", StringComparison.Ordinal),
            new FormUrlEncodedContent(linkA.Fields));
        Assert.Equal(HttpStatusCode.OK, linkAsUnlink.StatusCode);
        Assert.Contains(
            "The message or case changed",
            await linkAsUnlink.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        await AssertAssociationStateAsync(factory, receiptA, caseId, expectedHistoryCount: 1);

        var linkedPage = await GetHtmlAsync(client, linked.Headers.Location!.ToString());
        var unlinkConfirmation = await PrepareAssociationAsync(
            client, linkedPage, "PrepareUnlinkCase");
        var unlink = AssociationSubmission(
            unlinkConfirmation,
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

    [Fact]
    public async Task FailedLeaseReleaseRetainsTheExactConfirmationUntilCompensationSucceeds()
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var releaseFailures = new ReleaseFailureGate();
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
            }));
        using var client = IntakeWebDriver.CreateClient(factory);
        var target = await GetHtmlAsync(
            client,
            $"/Inbox/{messageId:D}?caseQuery=MAIL-RELEASE&targetCaseId={caseId:D}");
        var confirmation = await PrepareAssociationAsync(client, target, "PrepareLinkCase");
        var submission = AssociationSubmission(
            confirmation,
            "LinkCase",
            "Reviewed exact retained-message evidence.");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            await context.IntakeReceipts
                .Where(item => item.Id == receiptId)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.Version, item => item.Version + 1));
        }

        using var failedRelease = await client.PostAsync(
            submission.Action,
            new FormUrlEncodedContent(submission.Fields));
        Assert.Equal(HttpStatusCode.OK, failedRelease.StatusCode);
        var retryPage = await failedRelease.Content.ReadAsStringAsync();
        Assert.Contains("edit authority could not be released", retryPage, StringComparison.Ordinal);
        var retry = AssociationSubmission(
            retryPage,
            "LinkCase",
            "Reviewed exact retained-message evidence.");

        using var released = await client.PostAsync(
            retry.Action,
            new FormUrlEncodedContent(retry.Fields));
        Assert.Equal(HttpStatusCode.OK, released.StatusCode);
        var releasedPage = await released.Content.ReadAsStringAsync();
        Assert.Contains("The message or case changed", releasedPage, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"editLeaseToken\"", releasedPage, StringComparison.Ordinal);
        Assert.Equal(2, releaseFailures.AttemptCount);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationFactory = verificationScope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var verification = await verificationFactory.CreateDbContextAsync();
        Assert.Null((await verification.CaseWorkflows.SingleAsync(item => item.CaseId == caseId)).EditLeaseToken);
        Assert.Empty(await verification.IntakeMutationHistory.Where(item => item.IntakeReceiptId == receiptId).ToListAsync());
        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        var retryLease = await verificationScope.ServiceProvider
            .GetRequiredService<IAcquireCaseEditLease>()
            .ExecuteAsync(
                new(caseId, 0, actor, $"mail-release-retry:{Guid.NewGuid():N}"),
                CancellationToken.None);
        Assert.Equal(caseId, retryLease.CaseId);
    }

    [Fact]
    public async Task CaseSearchResultIsOneFocusableLinkWhoseNameContainsEveryVisibleIdentityFact()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "AB12 CDE", "MAIL-A11Y");
        var caseReference = await CaseReferenceAsync(factory, caseId);
        var messageId = Assert.Single(await SeedAsync(
            factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        await StoreClassificationAsync(factory, FirstMailboxId, FirstMailboxId + "-0");

        var html = await GetHtmlAsync(client, $"/Inbox/{messageId:D}?caseQuery=AB12%20CDE");
        var resultLink = Regex.Match(
            html,
            $"<a[^>]*targetCaseId={caseId:D}[^>]*>(.*?)</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        Assert.True(resultLink.Success, "The matching Case must be one focusable link.");
        Assert.Single(
            Regex.Matches(
                html,
                $"<a[^>]*targetCaseId={caseId:D}[^>]*>",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .Cast<Match>());
        var accessibleName = Regex.Replace(
            WebUtility.HtmlDecode(resultLink.Groups[1].Value),
            "<[^>]+>",
            string.Empty,
            RegexOptions.CultureInvariant);
        Assert.Contains(caseReference, accessibleName, StringComparison.Ordinal);
        Assert.Contains("AB12CDE", accessibleName.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("Fixture Claimant", accessibleName, StringComparison.Ordinal);
        Assert.Contains("Review", accessibleName, StringComparison.Ordinal);
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

        // Reading changes nothing. Beyond the layout's sign-out, the only POST
        // forms are the rows' Dismiss actions (Inbox, 13 September), one per row.
        Assert.Equal(3, CountOccurrences(html, "data-mail-row-action=\"dismiss\""));
        // Sign-out, the shell's Report a problem, and the three dismissals.
        Assert.Equal(2 + 3, CountOccurrences(html, "method=\"post\""));
        Assert.Contains("/Account/SignOut", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Operator, 12 October 2026: a received message whose mailbox can send
    /// offers Reply on its row and in the preview pane, opening the message
    /// record's own reply; a message in a mailbox that cannot send offers none.
    /// </summary>
    [Fact]
    public async Task AMessageInASendingMailboxOffersReplyOnItsRowAndInThePreview()
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
        Assert.Contains("data-mail-reply-url=", sendingRow, StringComparison.Ordinal);
        Assert.Contains("data-mail-row-action=\"dismiss\"", sendingRow, StringComparison.Ordinal);
        var receivingRow = MailRow(html, receivingId);
        Assert.DoesNotContain("data-mail-row-reply", receivingRow, StringComparison.Ordinal);
        // Razor keeps a data- attribute with a null value, so the row carries an empty one.
        Assert.Contains("data-mail-reply-url=\"\"", receivingRow, StringComparison.Ordinal);
        Assert.Contains("data-mail-row-action=\"dismiss\"", receivingRow, StringComparison.Ordinal);

        var preview = Between(html, "<aside id=\"mail-quick-preview\"", "</aside>");
        var previewReply = Regex.Match(preview, "<a[^>]*data-mail-preview-reply[^>]*>");
        Assert.True(previewReply.Success, "The preview pane has no Reply.");
        Assert.Contains($"href=\"/Inbox/{sendingId:D}?", previewReply.Value, StringComparison.Ordinal);
        Assert.Contains("compose=reply", previewReply.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden", previewReply.Value, StringComparison.Ordinal);

        var receivingHtml = await GetHtmlAsync(client, $"/Inbox?selected={receivingId:D}");
        var receivingPreview = Between(receivingHtml, "<aside id=\"mail-quick-preview\"", "</aside>");
        var hiddenReply = Regex.Match(receivingPreview, "<a[^>]*data-mail-preview-reply[^>]*>");
        Assert.True(hiddenReply.Success, "The preview pane keeps its Reply slot for a row that offers one.");
        Assert.Contains("hidden=\"hidden\"", hiddenReply.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("href=", hiddenReply.Value, StringComparison.Ordinal);
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
        Assert.Contains(
            "hidden=\"hidden\"",
            Regex.Match(html, "<a[^>]*data-mail-preview-reply[^>]*>").Value,
            StringComparison.Ordinal);
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
        Assert.Contains("data-mail-preview-trigger", subject, StringComparison.Ordinal);
    }

    /// <summary>
    /// The preview's text (26 September 2026): the sender's line breaks are
    /// kept, runs of spaces collapse, and it stops after eight lines.
    /// </summary>
    [Fact]
    public async Task ThePreviewKeepsTheSendersLineBreaksAndStopsAfterEightLines()
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
        // character reference; the browser reads them back for pre-line.
        var excerpt = WebUtility.HtmlDecode(
            Between(html, "<p class=\"mail-excerpt\" data-mail-preview-excerpt>", "</p>"));

        Assert.Contains("Line 1 of the message.\nLine 2 of the message.", excerpt, StringComparison.Ordinal);
        Assert.EndsWith("Line 8 of the message.…", excerpt, StringComparison.Ordinal);
        Assert.DoesNotContain("Line 9", excerpt, StringComparison.Ordinal);
    }

    /// <summary>
    /// A long single paragraph: the preview reads enough of the body to cut
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
        const string excerptStart = "<p class=\"mail-excerpt\" data-mail-preview-excerpt>";
        var excerpt = WebUtility.HtmlDecode(Between(html, excerptStart, "</p>")[excerptStart.Length..]);

        Assert.EndsWith("…", excerpt, StringComparison.Ordinal);
        Assert.True(excerpt.Length <= 600, $"The excerpt is {excerpt.Length} characters: {excerpt}");
        var kept = excerpt[..^1];
        Assert.StartsWith(kept, body, StringComparison.Ordinal);
        Assert.Equal(' ', body[kept.Length]);
    }

    /// <summary>
    /// The preview's attachments (26 September 2026): one line counting them
    /// by kind, no names. The row keeps its count and the full message still
    /// names every file.
    /// </summary>
    [Fact]
    public async Task ThePreviewCountsAttachmentsByKindWhileTheMessageNamesThem()
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
        var preview = Between(html, "<aside id=\"mail-quick-preview\"", "</aside>");
        Assert.Contains("data-mail-preview-attachments>3 attachments &#xB7; 2 images, 1 document</p>", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("front.jpg", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("estimate.pdf", preview, StringComparison.Ordinal);
        Assert.Contains("3 attachments", MailRow(html, messageId), StringComparison.Ordinal);

        var attachments = await GetHtmlAsync(client, $"/Inbox/{messageId:D}?section=attachments");
        Assert.Contains("front.jpg", attachments, StringComparison.Ordinal);
        Assert.Contains("rear.png", attachments, StringComparison.Ordinal);
        Assert.Contains("estimate.pdf", attachments, StringComparison.Ordinal);
    }

    /// <summary>
    /// A message that opened a Triage (26 September 2026): the chip reads
    /// Triage, the Case cell carries the t. reference, the row links it, and
    /// the pane offers Open Triage rather than saying there is no Case.
    /// </summary>
    [Fact]
    public async Task AMessageThatOpenedATriageShowsItsReferenceAndOpenTriage()
    {
        using var factory = new IntakeWebApplicationFactory();
        var messageId = Assert.Single(await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        await StoreOutcomeReceiptAsync(factory, FirstMailboxId, FirstMailboxId + "-0", IntakeDecision.NeedsSorting);
        var receiptId = await ReceiptIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var triageCaseId = await MailboxIntakeTestData.SeedTriageCaseAsync(factory.Services, receiptId, "t.QDOS31007");
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Inbox");

        var preview = Between(html, "<aside id=\"mail-quick-preview\"", "</aside>");
        Assert.Contains(">Triage</span>", preview, StringComparison.Ordinal);
        Assert.Contains($"<dt>{OperatorLabels.Inbox.Case}</dt>", preview, StringComparison.Ordinal);
        Assert.Contains("data-mail-preview-association>t.QDOS31007</dd>", preview, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Cases/{triageCaseId:D}\"", preview, StringComparison.Ordinal);
        Assert.Contains($"<span>{OperatorLabels.Inbox.OpenTriage}</span>", preview, StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorLabels.Inbox.OpenCase, preview, StringComparison.Ordinal);
        Assert.DoesNotContain(">None<", preview, StringComparison.Ordinal);

        var row = MailRow(html, messageId);
        Assert.Contains(">Triage</span>", row, StringComparison.Ordinal);
        Assert.Contains($"<a href=\"/Cases/{triageCaseId:D}\">t.QDOS31007</a>", row, StringComparison.Ordinal);

        // A pinned row's pane takes its Case action from the preview JSON.
        Assert.Contains("data-mail-preview-open", preview, StringComparison.Ordinal);
        Assert.Contains("data-mail-preview-case", preview, StringComparison.Ordinal);
        using var response = await client.GetAsync($"/Inbox?handler=Preview&id={messageId:D}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal($"/Cases/{triageCaseId:D}", json.RootElement.GetProperty("caseUrl").GetString());
        Assert.Equal("Open Triage", json.RootElement.GetProperty("caseAction").GetString());
    }

    /// <summary>
    /// The Triage flag follows the Case's own type, not how the message came
    /// to it: a message staff linked to a Triage Case it did not open shows
    /// the Triage chip and Open Triage too.
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

        var preview = Between(html, "<aside id=\"mail-quick-preview\"", "</aside>");
        Assert.Contains(">Triage</span>", preview, StringComparison.Ordinal);
        Assert.Contains("data-mail-preview-association>t.QDOS31008</dd>", preview, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Cases/{triageCaseId:D}\"", preview, StringComparison.Ordinal);
        Assert.Contains($"<span>{OperatorLabels.Inbox.OpenTriage}</span>", preview, StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorLabels.Inbox.OpenCase, preview, StringComparison.Ordinal);
        Assert.Contains(">Triage</span>", MailRow(html, messageId), StringComparison.Ordinal);
    }

    /// <summary>
    /// The full message agrees with itself: a message that opened a Triage and
    /// is linked to no Case shows that Triage on its Case tab, with its t.
    /// reference and Open Triage, and Link to Case stays available because
    /// linking to an instruction Case is a separate act.
    /// </summary>
    [Fact]
    public async Task TheCaseTabShowsTheTriageTheMessageOpened()
    {
        using var factory = new IntakeWebApplicationFactory();
        var messageId = Assert.Single(await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        await StoreOutcomeReceiptAsync(factory, FirstMailboxId, FirstMailboxId + "-0", IntakeDecision.NeedsSorting);
        var receiptId = await ReceiptIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var triageCaseId = await MailboxIntakeTestData.SeedTriageCaseAsync(factory.Services, receiptId, "t.QDOS31010");
        using var client = IntakeWebDriver.CreateClient(factory);

        var caseTab = await GetHtmlAsync(client, $"/Inbox/{messageId:D}?section=case");

        Assert.Contains($"<h2 class=\"decision-head\">{OperatorLabels.Inbox.Triage}</h2>", caseTab, StringComparison.Ordinal);
        Assert.Contains($"<a href=\"/Cases/{triageCaseId:D}\">t.QDOS31010</a>", caseTab, StringComparison.Ordinal);
        Assert.Contains($"<span>{OperatorLabels.Inbox.OpenTriage}</span>", caseTab, StringComparison.Ordinal);
        Assert.DoesNotContain("Linked case", caseTab, StringComparison.Ordinal);
        Assert.Contains("association-case-query", caseTab, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompoundOutcomeLabelsStayExactAndGreenInRowsAndSelectedPreview()
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

            var preview = Between(html, "<aside id=\"mail-quick-preview\"", "</aside>");
            Assert.Contains($">{outcome.Label}<", preview, StringComparison.Ordinal);
            Assert.Contains("class=\"status status--green\"", preview, StringComparison.Ordinal);
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
    /// The list's scope rail (Inbox, 13 September): one count per scope, no Unread
    /// scope and a Dismissed scope last, the sort toggle as a server-side flip, and
    /// the preview pane rendered from the selected row.
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
        Assert.Equal(4, CountOccurrences(html, "class=\"scope-button\""));
        // Three retained inbox messages, none read.
        Assert.Contains("<span class=\"tabular\">3</span>", html, StringComparison.Ordinal);

        // The preview pane renders server-side for the newest row, and the
        // pane's full-detail entry carries the list context.
        Assert.Contains("data-mail-preview-facts", html, StringComparison.Ordinal);
        Assert.Contains("Message 2 from instructions", html, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.Inbox.OpenFullMessage, html, StringComparison.Ordinal);

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
        // A Sent item is not received work: no processing chip on the row or
        // the pane, and its preview JSON carries no state to repaint one.
        Assert.DoesNotContain("Not yet processed", sent, StringComparison.Ordinal);
        var previewUrl = Regex.Match(sent, "data-mail-preview-url=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(previewUrl);
        using var previewResponse = await client.GetAsync(System.Net.WebUtility.HtmlDecode(previewUrl));
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        using var preview = JsonDocument.Parse(await previewResponse.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, preview.RootElement.GetProperty("state").ValueKind);
        Assert.Equal(Pegasus.Web.Presentation.OperatorLabels.CaseWorkspace.AbsentValue, preview.RootElement.GetProperty("classification").GetString());
        Assert.Equal("Sent", preview.RootElement.GetProperty("folder").GetString());
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
            "/Inbox?folder=deleted&mailbox=11111111-1111-1111-1111-111111111111&search=needle&pageNumber=2");
        Assert.Contains("Deleted match 0", secondPage, StringComparison.Ordinal);
        Assert.Contains("Page 2 of 2", secondPage, StringComparison.Ordinal);

        source.Result = new([], false, DeletedMailSearchState.Unavailable);
        var unavailable = await GetHtmlAsync(
            client,
            "/Inbox?folder=deleted&mailbox=11111111-1111-1111-1111-111111111111&search=needle");
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

        // The pane says where the term matched only while one is typed.
        var searched = Between(html, "<aside id=\"mail-quick-preview\"", "</aside>");
        Assert.Contains($"<dt>{OperatorLabels.Inbox.Matched}</dt>", searched, StringComparison.Ordinal);
        Assert.Contains("data-mail-preview-matched", searched, StringComparison.Ordinal);
        Assert.Contains("<dd>Message body</dd>", searched, StringComparison.Ordinal);
        var unsearched = Between(await GetHtmlAsync(client, "/Inbox"), "<aside id=\"mail-quick-preview\"", "</aside>");
        Assert.DoesNotContain(OperatorLabels.Inbox.Matched, unsearched, StringComparison.Ordinal);
        Assert.DoesNotContain("Search match", unsearched, StringComparison.Ordinal);
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
        // One flat list: All categories, every category in order, then No Case Found.
        Assert.DoesNotContain("<optgroup", receiving, StringComparison.Ordinal);
        var categorySelect = Between(receiving, "<select id=\"queue-filter\"", "</select>");
        Assert.Equal(
            CategoryFilterValues,
            Regex.Matches(categorySelect, "<option value=\"(?<value>[^\"]*)\"")
                .Select(match => match.Groups["value"].Value)
                .ToArray());
        Assert.Contains(">Not client related</option>", categorySelect, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.Inbox.NoCaseFound}</option>", categorySelect, StringComparison.Ordinal);
        Assert.DoesNotContain("Current view:", receiving, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"field-hint\"", receiving, StringComparison.Ordinal);
        // One selected option per filter-bar select: mailbox, folder, queue.
        Assert.Equal(3, CountOccurrences(receiving, " selected=\"selected\""));
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

        var detail = await GetHtmlAsync(
            client,
            $"/Inbox/{ids[5]:D}?queue=new-instruction&pageNumber=2");
        Assert.Contains("/Inbox?queue=new-instruction&amp;pageNumber=2", detail, StringComparison.Ordinal);
        // The Case tab carries the whole list context forward.
        Assert.Contains("queue=new-instruction&amp;section=case", detail, StringComparison.Ordinal);
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

        var linkTarget = await GetHtmlAsync(
            client,
            $"/Inbox/{linkMessageId:D}?queue=new-instruction&caseQuery=MAIL-CONTEXT-LINK&targetCaseId={linkCaseId:D}");
        var prepareLinkForm = AssociationForm(linkTarget, "PrepareLinkCase");
        await PostNotFoundAsync(
            client,
            Forge(AssociationAction(prepareLinkForm, "PrepareLinkCase")),
            HiddenFields(prepareLinkForm));
        Assert.Null(await CaseLeaseTokenAsync(baseFactory, linkCaseId));

        var linkConfirmation = await PrepareAssociationAsync(client, linkTarget, "PrepareLinkCase");
        var linkSubmission = AssociationSubmission(
            linkConfirmation,
            "LinkCase",
            "The exact retained message belongs to this Case/PO.");
        await PostNotFoundAsync(
            client,
            Forge(linkSubmission.Action),
            linkSubmission.Fields);
        await AssertAssociationStateAsync(baseFactory, linkReceiptId, expectedCaseId: null, expectedHistoryCount: 0);
        Assert.Equal(linkSubmission.Fields["editLeaseToken"], await CaseLeaseTokenAsync(baseFactory, linkCaseId));

        var unlinkTarget = await GetHtmlAsync(
            client,
            $"/Inbox/{unlinkMessageId:D}?queue=new-instruction&caseQuery=MAIL-CONTEXT-UNLINK&targetCaseId={unlinkCaseId:D}");
        var unlinkLinkConfirmation = await PrepareAssociationAsync(client, unlinkTarget, "PrepareLinkCase");
        var unlinkLinkSubmission = AssociationSubmission(
            unlinkLinkConfirmation,
            "LinkCase",
            "The exact retained message belongs to this Case/PO.");
        using (var linked = await client.PostAsync(
            unlinkLinkSubmission.Action,
            new FormUrlEncodedContent(unlinkLinkSubmission.Fields)))
        {
            Assert.Equal(HttpStatusCode.Redirect, linked.StatusCode);
        }
        var linkedPage = await GetHtmlAsync(
            client,
            $"/Inbox/{unlinkMessageId:D}?queue=new-instruction");
        var prepareUnlinkForm = AssociationForm(linkedPage, "PrepareUnlinkCase");
        await PostNotFoundAsync(
            client,
            Forge(AssociationAction(prepareUnlinkForm, "PrepareUnlinkCase")),
            HiddenFields(prepareUnlinkForm));
        Assert.Null(await CaseLeaseTokenAsync(baseFactory, unlinkCaseId));

        var unlinkConfirmation = await PrepareAssociationAsync(client, linkedPage, "PrepareUnlinkCase");
        var unlinkSubmission = AssociationSubmission(
            unlinkConfirmation,
            "UnlinkCase",
            "The message belongs to a different Case/PO.");
        await PostNotFoundAsync(
            client,
            Forge(unlinkSubmission.Action),
            unlinkSubmission.Fields);
        await AssertAssociationStateAsync(baseFactory, unlinkReceiptId, unlinkCaseId, expectedHistoryCount: 1);
        Assert.Equal(unlinkSubmission.Fields["editLeaseToken"], await CaseLeaseTokenAsync(baseFactory, unlinkCaseId));

        var actionPage = await GetHtmlAsync(
            client,
            $"/Inbox/{actionMessageId:D}?queue=new-instruction");
        var correctionForm = AssociationForm(actionPage, "CorrectClassification");
        var correctionFields = HiddenFields(correctionForm);
        correctionFields["ClassificationKey"] = "general";
        correctionFields["CorrectionReason"] = "Reviewed retained evidence.";
        await PostNotFoundAsync(
            client,
            Forge(AssociationAction(correctionForm, "CorrectClassification")),
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
            "<a class=\"btn\" href=\"(?<href>/Inbox\\?[^\"]*)\">\\s*<svg class=\"icon\" aria-hidden=\"true\"><use href=\"#icon-arrow-right\" />").Groups["href"].Value);
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
        // Beyond the layout's sign-out and Report a problem, the record's only POST is Dismiss (Inbox, 13 September).
        Assert.Equal(3, CountOccurrences(message, "method=\"post\""));
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
        // Sign-out, Report a problem and the record's Dismiss.
        Assert.Equal(3, CountOccurrences(html, "method=\"post\""));
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
        Assert.Contains("<span>Classification</span>", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Unclassified</strong>", html, StringComparison.Ordinal);
        // An unlinked message has no Destination row.
        Assert.DoesNotContain("<span>Destination</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("shared-mail-policy", html, StringComparison.Ordinal);
        Assert.DoesNotContain("sender-domain", html, StringComparison.Ordinal);
        Assert.DoesNotContain("mail_operational_destination", html, StringComparison.Ordinal);
        // The correction is a dialog on the card, posting the exact decision
        // version it corrects.
        Assert.Contains(">Save correction</button>", html, StringComparison.Ordinal);
        Assert.Contains("name=\"ExpectedClassificationVersion\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"1\"", html, StringComparison.Ordinal);
        // The persisted "system-worker:..." actor resolves to the
        // operator-facing provenance word, never the raw stored value.
        Assert.Contains("data-word=\"Automatic\"", html, StringComparison.Ordinal);
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

        var target = await GetHtmlAsync(
            client,
            $"/Inbox/{linkedMessageId:D}?caseQuery=MAIL-DESTINATION&targetCaseId={caseId:D}");
        var confirmation = await PrepareAssociationAsync(client, target, "PrepareLinkCase");
        var submission = AssociationSubmission(
            confirmation,
            "LinkCase",
            "The retained message belongs to this Case/PO.");
        using var linkedResponse = await client.PostAsync(
            submission.Action,
            new FormUrlEncodedContent(submission.Fields));
        Assert.Equal(HttpStatusCode.Redirect, linkedResponse.StatusCode);

        // The linking session retains its confirmation context on the Case tab.
        // A fresh viewer checks the persisted message destination independently.
        using var viewer = IntakeWebDriver.CreateClient(factory);
        var linked = await GetHtmlAsync(viewer, $"/Inbox/{linkedMessageId:D}");
        // FRD-08 case-state classification: linked to a Review Case, the
        // unclassified message becomes a Case update.
        Assert.Contains("<span>Classification</span>", linked, StringComparison.Ordinal);
        Assert.Contains("<strong>Case update</strong>", linked, StringComparison.Ordinal);
        Assert.DoesNotContain("<strong>Unclassified</strong>", linked, StringComparison.Ordinal);
        Assert.Contains("<span>Destination</span>", linked, StringComparison.Ordinal);
        Assert.Contains(
            $"<a href=\"/Cases/{caseId:D}\">MAIL-DESTINATION</a>",
            linked,
            StringComparison.Ordinal);
        Assert.DoesNotContain($"<strong>{OperatorLabels.Inbox.NoCaseFound}</strong>", linked, StringComparison.Ordinal);

        var previewPage = await GetHtmlAsync(viewer, $"/Inbox?selected={linkedMessageId:D}");
        var linkedRowStart = previewPage.IndexOf(
            $"data-mail-row=\"{linkedMessageId:D}\"",
            StringComparison.OrdinalIgnoreCase);
        Assert.True(linkedRowStart >= 0, "The linked message row was not rendered.");
        var linkedRowEnd = previewPage.IndexOf("</div>", linkedRowStart, StringComparison.Ordinal);
        Assert.True(linkedRowEnd > linkedRowStart, "The linked message row was not complete.");
        var linkedRow = previewPage[linkedRowStart..linkedRowEnd];
        Assert.Contains("Case update", linkedRow, StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorLabels.Inbox.NoCaseFound, linkedRow, StringComparison.Ordinal);

        var preview = Between(previewPage, "<aside id=\"mail-quick-preview\"", "</aside>");
        Assert.Contains("data-mail-preview-classification>Case update</dd>", preview, StringComparison.Ordinal);
        // An instruction Case: the Case cell names its Case/PO and the pane offers Open Case.
        Assert.Contains($"<dt>{OperatorLabels.Inbox.Case}</dt>", preview, StringComparison.Ordinal);
        Assert.Contains("data-mail-preview-association>MAIL-DESTINATION</dd>", preview, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Cases/{caseId:D}\"", preview, StringComparison.Ordinal);
        Assert.Contains($"<span>{OperatorLabels.Inbox.OpenCase}</span>", preview, StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorLabels.Inbox.OpenTriage, preview, StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorLabels.Inbox.NoCaseFound, preview, StringComparison.Ordinal);

        var unlinked = await GetHtmlAsync(viewer, $"/Inbox/{unlinkedMessageId:D}");
        Assert.Contains("<strong>Unclassified</strong>", unlinked, StringComparison.Ordinal);
        Assert.DoesNotContain("<span>Destination</span>", unlinked, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.Inbox.NoCase}</strong>", unlinked, StringComparison.Ordinal);
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
        Assert.Contains("<strong>New instruction</strong>", html, StringComparison.Ordinal);
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
    public async Task TheCorrectionOffersEveryCategoryAndUnclassifiedAndANewInstructionStoresItsCaseType()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1);
        await StoreClassificationAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        using var client = IntakeWebDriver.CreateClient(factory);
        var route = $"/Inbox/{ids[0]:D}?handler=CorrectClassification";

        var page = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}");
        // The picker is one flat list: every category, then Unclassified, and
        // it asks for the case type a New instruction names.
        var picker = Between(page, "id=\"ClassificationKey\"", "</select>");
        Assert.Equal(
            CorrectionValues,
            Regex.Matches(picker, "<option value=\"(?<value>[^\"]*)\"")
                .Select(match => match.Groups["value"].Value)
                .ToArray());
        Assert.Contains(">Unclassified</option>", picker, StringComparison.Ordinal);
        Assert.DoesNotContain("OtherClassification", page, StringComparison.Ordinal);
        Assert.Contains("data-work-type-field", page, StringComparison.Ordinal);

        // A New instruction without a case type is refused before the command.
        using (var missing = await client.PostAsync(route, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiforgeryToken(page),
            ["ExpectedClassificationVersion"] = "1",
            ["ClassificationKey"] = "new-instruction",
            ["CorrectionReason"] = "Instruction read from the retained message."
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
            ["CorrectionReason"] = "Instruction read from the retained message."
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

    private static async Task<string> PrepareAssociationAsync(
        HttpClient client,
        string html,
        string handler)
    {
        var form = AssociationForm(html, handler);
        using var response = await client.PostAsync(
            AssociationAction(form, handler),
            new FormUrlEncodedContent(HiddenFields(form)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        return await GetHtmlAsync(client, response.Headers.Location.ToString());
    }

    private static AssociationPost AssociationSubmission(
        string html,
        string handler,
        string reason)
    {
        var form = AssociationForm(html, handler);
        var action = AssociationAction(form, handler);
        var fields = HiddenFields(form);
        fields["Reason"] = reason;
        Assert.DoesNotContain(fields["editLeaseToken"], action, StringComparison.Ordinal);
        return new(action, fields);
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
