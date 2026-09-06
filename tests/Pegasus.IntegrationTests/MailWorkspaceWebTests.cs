using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
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

namespace Pegasus.IntegrationTests;

/// <summary>
/// The mail workspace through the real page pipeline: what an operator sees, what
/// the filters carry, and the fact that nothing on it can change anything.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class MailWorkspaceWebTests
{
    private static readonly DateTimeOffset NowUtc = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    private const string FirstMailboxId = "instructions";
    private static string FirstMailboxFilter => TestMailboxId.From(FirstMailboxId).ToString("D");
    private const string FirstMailboxAddress = "instructions@collisionengineers.co.uk";
    private const string SecondMailboxId = "reports";
    private const string SecondMailboxAddress = "reports@collisionengineers.co.uk";

    [Fact]
    public async Task QuickPreviewIsAuthenticatedExactEvidenceAndDoesNotMutateMailState()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var messageId = Assert.Single(await SeedAsync(
            factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        using var client = CreateClient(factory);

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
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var preview = document.RootElement;
        Assert.Equal(messageId, preview.GetProperty("id").GetGuid());
        Assert.Equal("A Sender", preview.GetProperty("sender").GetString());
        Assert.Equal($"Message 0 from {FirstMailboxId}", preview.GetProperty("subject").GetString());
        Assert.Equal("Please inspect the vehicle at the address supplied.", preview.GetProperty("excerpt").GetString());
        Assert.Equal("Not yet processed", preview.GetProperty("classification").GetString());
        Assert.Equal("No case", preview.GetProperty("association").GetString());
        Assert.Equal("estimate.pdf", Assert.Single(preview.GetProperty("attachments").EnumerateArray()).GetString());

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
    /// C08: the workspace contract says mailbox/folder/search/queue/unread/
    /// sort/page are URL and retained-query state only — opening, previewing,
    /// filtering or changing the unread scope never reaches Outlook or writes
    /// a classification correction. <see cref="RecordingFolderMover"/> stands
    /// in for the one Graph-facing port the pages can reach
    /// (<see cref="MoveRetainedMailFolder"/>'s underlying mover) and
    /// <see cref="RecordingClassificationStore"/> for
    /// <see cref="CorrectRetainedMailClassification"/>'s store; both are
    /// asserted at zero after every read action below. The explicit staff
    /// <c>OnPostMoveToRecommendedFolderAsync</c> stays a write and is proved
    /// elsewhere (<see cref="AuthenticatedStaffConfirmsTheServerDerivedFolderWithoutPostingTransportIdentity"/>).
    /// </summary>
    [Fact]
    public async Task OpenPreviewFilterUnreadAndSortNeverWriteThroughTheRetainedMailPorts()
    {
        var mover = new RecordingFolderMover();
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
                    MailCategory.Received(ReceivedMailFamily.NewInstructionReceived, "inspection"),
                    [],
                    "A receiving-work message was recognised.",
                    "fixture",
                    1));
        }
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRetainedMailFolderMover>();
                services.AddSingleton<IRetainedMailFolderMover>(mover);
                services.RemoveAll<IRetainedMailClassificationStore>();
                services.AddScoped<IRetainedMailClassificationStore>(_ => classificationStore);
            }));
        using var client = CreateClient(factory);

        // Open the list, filter it every way the workspace contract names,
        // preview a row, then open and return from the full message —
        // carrying the exact same query string a real navigation would.
        // "queue" has no "all" sentinel: TryParseQueue accepts only an
        // AggregateViews key (e.g. "receiving-work") or a "classification:"
        // one, and treats anything else — "all" included — as invalid,
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
        var query = $"mailbox={FirstMailboxId}&folder=inbox&search=estimate&queue=receiving-work&unread=true&sort=oldest";
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
        Assert.Contains("unread=true", rowHref, StringComparison.Ordinal);
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

        Assert.Equal(0, mover.MoveCalls);
        Assert.Equal(0, classificationStore.CorrectionCalls);
    }

    /// <summary>
    /// INTK-029's operator-facing half. The unlink dialog warns, naming the
    /// case, only when unlinking actually cancels it — that is, when the
    /// receipt's current link is the case its own acceptance created. A receipt
    /// merely associated with some other case gets no warning, because
    /// unlinking it leaves that case alone.
    ///
    /// The flag is proved in CaseAcceptanceReplayTests. What is proved here is
    /// the rendering, which is the half an operator actually reads and the half
    /// nothing else covered — the same one-line-wiring gap that let CASE-017
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
        using var client = CreateClient(factory);

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
        using var client = CreateClient(factory);

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
        var messageId = Assert.Single(await SeedAsync(
            factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        await StoreMailClassificationAsync(
            factory,
            FirstMailboxId,
            FirstMailboxId + "-0",
            MailClassificationResult.Classified(
                MailCategory.Received(ReceivedMailFamily.PostReportEmails, "query"),
                [],
                "The retained message is a post-report query.",
                "mail-query-response-test",
                1));
        var receiptId = await ReceiptIdAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        var caseId = await ImageIntakeTestData.SeedCaseAsync(
            factory.Services,
            receiptId,
            "AUTO14001",
            nameof(CaseLifecycleState.PostReport));
        using var client = CreateClient(factory);

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

        var linked = await GetHtmlAsync(client, link.Headers.Location!.ToString());
        Assert.Contains("handler=CreateQueryResponse", linked, StringComparison.Ordinal);
        Assert.Contains(">Draft reply with AI</span>", linked, StringComparison.Ordinal);
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
        Assert.Contains("AI reply job created.", rendered, StringComparison.Ordinal);
        Assert.Contains("id=\"case-ai-jobs-title\"", rendered, StringComparison.Ordinal);
        Assert.Contains(">Query response</td>", rendered, StringComparison.Ordinal);
        Assert.Contains(">Queued</span>", rendered, StringComparison.Ordinal);
        var jobsPanel = Between(
            rendered,
            "<section class=\"panel\" aria-labelledby=\"case-ai-jobs-title\">",
            "</section>");
        Assert.DoesNotContain(messageId.ToString("D"), jobsPanel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryResponsePostRefusesAMessageOutsideTheLinkedPostReportSource()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var messageId = Assert.Single(await SeedAsync(
            factory, FirstMailboxId, FirstMailboxAddress, count: 1));
        using var client = CreateClient(factory);
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
        using var client = CreateClient(factory);
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
        using var client = CreateClient(factory);

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
        using var client = CreateClient(factory);
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
        using var client = CreateClient(factory);
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

        // A viewer changes nothing. The only POST form the screen carries is the
        // layout's sign-out.
        Assert.Equal(1, CountOccurrences(html, "method=\"post\""));
        Assert.Contains("/Account/SignOut", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The ported list's new surface: the scope rail with one count per scope,
    /// the Unread scope as a real query, the sort toggle as a server-side
    /// flip, and the preview pane rendered from the selected row.
    /// </summary>
    [Fact]
    public async Task TheScopeRailCountsEachScopeUnreadFiltersAndTheSortToggleFlipsOrder()
    {
        using var factory = new IntakeWebApplicationFactory();
        await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 3);
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Inbox");

        // Every drawn scope renders once, with its count.
        Assert.Contains(">All incoming</span>", html, StringComparison.Ordinal);
        Assert.Contains(">Unread</span>", html, StringComparison.Ordinal);
        Assert.Contains(">Receiving work</span>", html, StringComparison.Ordinal);
        Assert.Contains(">Case updates</span>", html, StringComparison.Ordinal);
        Assert.Contains(">Pre-instructions</span>", html, StringComparison.Ordinal);
        Assert.Contains(">Unidentified</span>", html, StringComparison.Ordinal);
        Assert.Contains(">Sent Items</span>", html, StringComparison.Ordinal);
        Assert.Equal(7, CountOccurrences(html, "class=\"scope-button\""));
        // Three retained inbox messages, none read.
        Assert.Contains("<span class=\"tabular\">3</span>", html, StringComparison.Ordinal);

        // The preview pane renders server-side for the newest row, and the
        // pane's full-detail entry carries the list context.
        Assert.Contains("data-mail-preview-facts", html, StringComparison.Ordinal);
        Assert.Contains("Message 2 from instructions", html, StringComparison.Ordinal);
        Assert.Contains("Open full message", html, StringComparison.Ordinal);

        // The Unread scope is a real query, not a client filter.
        var unread = await GetHtmlAsync(client, "/Inbox?unread=true");
        Assert.Contains("Message 2 from instructions", unread, StringComparison.Ordinal);
        Assert.Contains("aria-pressed=\"true\"", unread, StringComparison.Ordinal);

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

        // An unknown sort or unread value is refused, like an unknown folder.
        using var badSort = await client.GetAsync("/Inbox?sort=newest-first");
        using var badUnread = await client.GetAsync("/Inbox?folder=sent&unread=true");
        Assert.Equal(HttpStatusCode.NotFound, badSort.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, badUnread.StatusCode);
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

        // The Unread scope and the oldest-first order survive the message
        // round-trip: the pane's full-detail entry opens the message with
        // them, Back returns to the same scope and order, and so does every
        // section tab.
        var unreadOldest = await GetHtmlAsync(
            client,
            $"/Inbox?mailbox={FirstMailboxFilter}&unread=true&sort=oldest&pageNumber=2");
        Assert.Contains("unread=true&amp;sort=oldest&amp;pageNumber=2", unreadOldest, StringComparison.Ordinal);

        var detail = await GetHtmlAsync(
            client,
            $"/Inbox/{ids[0]:D}?mailbox={FirstMailboxFilter}&unread=true&sort=oldest&pageNumber=2");
        Assert.Contains(
            $"/Inbox?mailbox={FirstMailboxFilter}&amp;unread=true&amp;sort=oldest&amp;pageNumber=2",
            detail,
            StringComparison.Ordinal);
        Assert.Contains("unread=true&amp;sort=oldest&amp;section=attachments", detail, StringComparison.Ordinal);
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

        Assert.Contains("Never updated", html, StringComparison.Ordinal);
        Assert.Contains(">Unavailable<", html, StringComparison.Ordinal);
        Assert.Contains("No mail has been received.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SentNamesWhatIsNotKeptAndDeletedListsNothingUntilItIsSearched()
    {
        using var factory = new IntakeWebApplicationFactory();
        await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1);
        using var client = IntakeWebDriver.CreateClient(factory);

        var sent = await GetHtmlAsync(client, "/Inbox?folder=sent");
        var deleted = await GetHtmlAsync(client, "/Inbox?folder=deleted");

        Assert.Contains("Sent messages are not kept in Pegasus yet.", sent, StringComparison.Ordinal);
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
        using var client = CreateClient(factory);

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
        using var client = CreateClient(factory);

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
        using var client = CreateClient(factory);

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

        Assert.Contains("No retained mail in this mailbox matched", noMatch, StringComparison.Ordinal);
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
            MailClassificationResult.Classified(
                MailCategory.Received(ReceivedMailFamily.NewInstructionReceived, "inspection"), [], "fixture", "test", 1),
            MailClassificationResult.Classified(
                MailCategory.Received(ReceivedMailFamily.PostReportEmails, "query"), [], "fixture", "test", 1),
            MailClassificationResult.Classified(
                MailCategory.Other(MailDirection.Received, "supplier-newsletter", "No known class fits."), [], "fixture", "test", 1),
            MailClassificationResult.Unclassified([], "fixture", "test", 1),
            MailClassificationResult.Classified(
                MailCategory.Received(ReceivedMailFamily.PreInstructionEmails, "triage-request"), [], "fixture", "test", 1),
            MailClassificationResult.Classified(
                MailCategory.Received(ReceivedMailFamily.General, "autoreply"), [], "fixture", "test", 1)
        };
        for (var index = 0; index < classifications.Length; index++)
        {
            await StoreMailClassificationAsync(
                factory,
                FirstMailboxId,
                $"{FirstMailboxId}-{index}",
                classifications[index]);
        }
        using var client = CreateClient(factory);

        var receiving = await GetHtmlAsync(client, "/Inbox?queue=receiving-work");
        Assert.Contains("<label for=\"queue-filter\">Queue</label>", receiving, StringComparison.Ordinal);
        Assert.Contains("<optgroup label=\"Operational queues\">", receiving, StringComparison.Ordinal);
        Assert.Contains("<optgroup label=\"Detailed classifications\">", receiving, StringComparison.Ordinal);
        Assert.DoesNotContain("Current view:", receiving, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"field-hint\"", receiving, StringComparison.Ordinal);
        // One selected option per filter-bar select: mailbox, folder, queue.
        Assert.Equal(3, CountOccurrences(receiving, " selected=\"selected\""));
        Assert.Contains($"/Inbox/{ids[5]:D}?queue=receiving-work", receiving, StringComparison.Ordinal);
        Assert.Contains("New instruction &#xB7; Inspection", receiving, StringComparison.Ordinal);
        Assert.DoesNotContain("Message 1 from instructions", receiving, StringComparison.Ordinal);

        foreach (var (key, included, excluded) in new[]
        {
            ("queries", "Message 1 from instructions", "Message 0 from instructions"),
            ("other", "Message 2 from instructions", "Message 1 from instructions"),
            ("unidentified", "Message 3 from instructions", "Message 4 from instructions"),
            ("triage", "Message 4 from instructions", "Message 3 from instructions"),
            ("classification:received:General:autoreply", "Message 5 from instructions", "Message 0 from instructions")
        })
        {
            var html = await GetHtmlAsync(client, $"/Inbox?queue={Uri.EscapeDataString(key)}");
            Assert.Contains(included, html, StringComparison.Ordinal);
            Assert.DoesNotContain(excluded, html, StringComparison.Ordinal);
        }

        var detail = await GetHtmlAsync(
            client,
            $"/Inbox/{ids[5]:D}?queue=receiving-work&pageNumber=2");
        Assert.Contains("/Inbox?queue=receiving-work&amp;pageNumber=2", detail, StringComparison.Ordinal);
        // The Case tab carries the whole list context forward.
        Assert.Contains("queue=receiving-work&amp;section=case", detail, StringComparison.Ordinal);
        using var unknown = await client.GetAsync("/Inbox?queue=needs-sorting");
        using var deleted = await client.GetAsync("/Inbox?folder=deleted&queue=triage");
        using var deletedDetail = await client.GetAsync($"/Inbox/{ids[5]:D}?folder=deleted&queue=triage");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deletedDetail.StatusCode);
        Assert.DoesNotContain("Needs sorting", receiving, StringComparison.OrdinalIgnoreCase);

        var emptyView = await GetHtmlAsync(
            client,
            "/Inbox?queue=classification%3Asent%3AReportSent");
        Assert.DoesNotContain("No retained mail is currently in", emptyView, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidMailViewContextStopsEveryExactMessagePostBeforeMutation(
        bool deletedItemsContext)
    {
        var mover = new RecordingFolderMover();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        _ = await SeedAsync(baseFactory, FirstMailboxId, FirstMailboxAddress, count: 3);
        for (var index = 0; index < 3; index++)
        {
            await StoreClassifiedInstructionAsync(
                baseFactory,
                FirstMailboxId,
                $"{FirstMailboxId}-{index}");
        }
        await ConfigureFolderBindingAsync(
            baseFactory,
            FirstMailboxId,
            MailLogicalFolderType.Instructions,
            "outlook-folder-instructions");
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
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRetainedMailFolderMover>();
                services.AddSingleton<IRetainedMailFolderMover>(mover);
            }));
        using var client = CreateClient(factory);

        string Forge(string action)
        {
            Assert.Contains("queue=receiving-work", action, StringComparison.Ordinal);
            return deletedItemsContext
                ? $"{action}&folder=deleted"
                : action.Replace("queue=receiving-work", "queue=needs-sorting", StringComparison.Ordinal);
        }

        var linkTarget = await GetHtmlAsync(
            client,
            $"/Inbox/{linkMessageId:D}?queue=receiving-work&caseQuery=MAIL-CONTEXT-LINK&targetCaseId={linkCaseId:D}");
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
            $"/Inbox/{unlinkMessageId:D}?queue=receiving-work&caseQuery=MAIL-CONTEXT-UNLINK&targetCaseId={unlinkCaseId:D}");
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
            $"/Inbox/{unlinkMessageId:D}?queue=receiving-work");
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
            $"/Inbox/{actionMessageId:D}?queue=receiving-work");
        var correctionForm = AssociationForm(actionPage, "CorrectClassification");
        var correctionFields = HiddenFields(correctionForm);
        correctionFields["ClassificationKey"] = "received:General:autoreply";
        correctionFields["CorrectionReason"] = "Reviewed retained evidence.";
        await PostNotFoundAsync(
            client,
            Forge(AssociationAction(correctionForm, "CorrectClassification")),
            correctionFields);

        var moveForm = AssociationForm(actionPage, "MoveToRecommendedFolder");
        var moveFields = HiddenFields(moveForm);
        moveFields["Reason"] = "Confirmed after reviewing the message.";
        await PostNotFoundAsync(
            client,
            Forge(AssociationAction(moveForm, "MoveToRecommendedFolder")),
            moveFields);

        Assert.Equal(0, mover.MoveCalls);
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
        Assert.Contains(">No case</strong>", message, StringComparison.Ordinal);
        // Back reconstructs the exact list position.
        Assert.Contains($"/Inbox?mailbox={FirstMailboxFilter}", message, StringComparison.Ordinal);
        // A viewer: the layout's sign-out is still the only POST on the screen.
        Assert.Equal(1, CountOccurrences(message, "method=\"post\""));

        var attachments = await GetHtmlAsync(
            client,
            $"/Inbox/{ids[0]:D}{query}&section=attachments");
        Assert.Contains("estimate.pdf", attachments, StringComparison.Ordinal);
        Assert.Contains("Content unavailable for search", attachments, StringComparison.Ordinal);
        // Megabytes, never bytes.
        Assert.Contains("under 0.1 MB", attachments, StringComparison.Ordinal);
        Assert.DoesNotContain("2048", attachments, StringComparison.Ordinal);

        var thread = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}{query}&section=thread");
        Assert.Contains("Message 0 from instructions", thread, StringComparison.Ordinal);
        Assert.Contains("Message 1 from instructions", thread, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MessageDetailShowsUnavailableFolderRecommendationBeforeClassificationExists()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1);
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}");

        // An unavailable recommendation renders nothing: the Decision card
        // shows only populated rows, and no folder prose reaches the page.
        Assert.Contains("<h2 class=\"decision-head\">Decision</h2>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Folder recommendation", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Recommended Outlook folder", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Folder</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("no current classification decision", html, StringComparison.Ordinal);
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
        Assert.Contains("<span>Classification</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span>Destination</span>", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Unidentified</strong>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("shared-mail-policy", html, StringComparison.Ordinal);
        Assert.DoesNotContain("sender-domain", html, StringComparison.Ordinal);
        Assert.DoesNotContain("mail_operational_destination", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Recommended Outlook folder", html, StringComparison.Ordinal);
        Assert.DoesNotContain("absent or ambiguous", html, StringComparison.Ordinal);
        // The correction is a dialog on the card, posting the exact decision
        // version it corrects.
        Assert.Contains(">Save correction</button>", html, StringComparison.Ordinal);
        Assert.Contains("name=\"ExpectedClassificationVersion\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"1\"", html, StringComparison.Ordinal);
        // PLAT-011: the persisted "system-worker:..." actor resolves to the
        // operator-facing provenance word, never the raw stored value.
        Assert.Contains("data-word=\"Automatic\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("system-worker:approved-inbox-poller", html, StringComparison.Ordinal);
        // No corrections yet, so no Corrections card.
        Assert.DoesNotContain(">Corrections<", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MessageDetailShowsTheOperationalDestinationDerivedFromAClassifiedDecision()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1);
        await StoreClassifiedInstructionAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}");

        // The retained-mail viewer is the real production caller of
        // MailOperationalDestinationPolicy.Map: this must not silently
        // regress to no destination, or the wrong one, for a known category.
        Assert.Contains("<span>Destination</span>", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Receiving work</strong>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("mail_operational_destination", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MessageDetailShowsTheCurrentMailboxConfiguredFolderRecommendation()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1);
        await StoreClassifiedInstructionAsync(factory, FirstMailboxId, FirstMailboxId + "-0");
        await ConfigureFolderBindingAsync(
            factory,
            FirstMailboxId,
            MailLogicalFolderType.Instructions,
            "outlook-folder-instructions");
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}");

        Assert.Contains("<span>Folder</span>", html, StringComparison.Ordinal);
        Assert.Contains("Instructions — not moved", html, StringComparison.Ordinal);
        Assert.DoesNotContain("mail_logical_folder", html, StringComparison.Ordinal);
        Assert.DoesNotContain("outlook-folder-instructions", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Move to Instructions", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticatedStaffConfirmsTheServerDerivedFolderWithoutPostingTransportIdentity()
    {
        var mover = new RecordingFolderMover();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var ids = await SeedAsync(baseFactory, FirstMailboxId, FirstMailboxAddress, count: 1);
        await StoreClassifiedInstructionAsync(baseFactory, FirstMailboxId, FirstMailboxId + "-0");
        await ConfigureFolderBindingAsync(
            baseFactory,
            FirstMailboxId,
            MailLogicalFolderType.Instructions,
            "outlook-folder-instructions");
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRetainedMailFolderMover>();
                services.AddSingleton<IRetainedMailFolderMover>(mover);
            }));
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}?queue=receiving-work");

        Assert.Contains(">Move to Instructions</button>", html, StringComparison.Ordinal);
        Assert.Contains("id=\"moveFolderDialog\"", html, StringComparison.Ordinal);
        Assert.Contains("<dt>From</dt><dd>Inbox</dd>", html, StringComparison.Ordinal);
        Assert.Contains("<dt>To</dt><dd>Instructions</dd>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("outlook-folder-instructions", html, StringComparison.Ordinal);
        Assert.Equal(0, mover.MoveCalls);
        var action = Regex.Match(
            html,
            "<form method=\"post\" action=\"([^\"]*handler=MoveToRecommendedFolder[^\"]*)\"",
            RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.NotEmpty(action);
        action = WebUtility.HtmlDecode(action);
        using var response = await client.PostAsync(
            action,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = AntiforgeryToken(html),
                ["Reason"] = "Confirmed after reviewing the message."
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("queue=receiving-work", response.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, mover.MoveCalls);
        Assert.Equal(FirstMailboxId, mover.Coordinates!.MailboxId);
        Assert.Equal("inbox", mover.Coordinates.SourceFolderId);
        Assert.Equal("outlook-folder-instructions", mover.Coordinates.DestinationFolderId);
        await using var scope = baseFactory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IRetainedMailQueries>();
        Assert.Empty((await queries.ListAsync(
            new(null, MailFolderScope.Inbox), 1, 25, CancellationToken.None)).Items);
        // MAIL-010: the notice that narrated this is gone. The assertion below
        // is the one that proved it — a message moved out of the Inbox is still
        // found by search — so it now stands alone.
        var searchHtml = await GetHtmlAsync(client, "/Inbox?search=estimate");
        Assert.Contains($"Message 0 from {FirstMailboxId}", searchHtml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("outlook-folder-instructions", "Message moved to the recommended Outlook folder.", false, false)]
    [InlineData("inbox", "The message was not moved. You can retry with a new confirmation.", false, true)]
    [InlineData("unresolved-folder", "The move result is uncertain. Retry this same confirmation to check its current location.", true, false)]
    public async Task AuthenticatedUncertainMoveReusesTheSameConfirmationForExactRecovery(
        string recoveredParent,
        string expectedNotice,
        bool remainsUncertain,
        bool showsSuggestedMove)
    {
        var mover = new SequenceRecoveryFolderMover(recoveredParent);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var ids = await SeedAsync(baseFactory, FirstMailboxId, FirstMailboxAddress, count: 1);
        await StoreClassifiedInstructionAsync(baseFactory, FirstMailboxId, FirstMailboxId + "-0");
        await ConfigureFolderBindingAsync(
            baseFactory,
            FirstMailboxId,
            MailLogicalFolderType.Instructions,
            "outlook-folder-instructions");
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRetainedMailFolderMover>();
                services.AddSingleton<IRetainedMailFolderMover>(mover);
            }));
        using var client = CreateClient(factory);

        var initial = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}?queue=receiving-work");
        var confirmationAction = WebUtility.HtmlDecode(Regex.Match(
            initial,
            "<form method=\"post\" action=\"([^\"]*handler=MoveToRecommendedFolder[^\"]*)\"",
            RegexOptions.IgnoreCase).Groups[1].Value);
        using var confirmation = await client.PostAsync(
            confirmationAction,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = AntiforgeryToken(initial),
                ["Reason"] = "Confirmed after reviewing the message."
            }));
        Assert.Equal(HttpStatusCode.Redirect, confirmation.StatusCode);
        Assert.Contains("queue=receiving-work", confirmation.Headers.Location!.ToString(), StringComparison.Ordinal);

        var uncertain = await GetHtmlAsync(client, confirmation.Headers.Location!.ToString());
        Assert.Contains("Check move status", uncertain, StringComparison.Ordinal);
        Assert.Contains("Unconfirmed", uncertain, StringComparison.Ordinal);
        Assert.DoesNotContain("moveFolderDialog", uncertain, StringComparison.Ordinal);
        Assert.Contains("value=\"Confirmed after reviewing the message.\"", uncertain, StringComparison.Ordinal);
        Assert.DoesNotContain("outlook-folder-instructions", uncertain, StringComparison.Ordinal);
        var recoveryAction = WebUtility.HtmlDecode(Regex.Match(
            uncertain,
            "<form method=\"post\" action=\"([^\"]*handler=MoveToRecommendedFolder[^\"]*)\"",
            RegexOptions.IgnoreCase).Groups[1].Value);
        Assert.Contains("queue=receiving-work", recoveryAction, StringComparison.Ordinal);
        using var recovery = await client.PostAsync(
            recoveryAction,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = AntiforgeryToken(uncertain),
                ["ExpectedClassificationVersion"] = HiddenValue(uncertain, "ExpectedClassificationVersion"),
                ["ExpectedRecommendationPolicyKey"] = HiddenValue(uncertain, "ExpectedRecommendationPolicyKey"),
                ["ExpectedRecommendationPolicyVersion"] = HiddenValue(uncertain, "ExpectedRecommendationPolicyVersion"),
                ["ExpectedMailboxVersion"] = HiddenValue(uncertain, "ExpectedMailboxVersion"),
                ["MoveOperationKey"] = HiddenValue(uncertain, "MoveOperationKey"),
                ["Reason"] = HiddenValue(uncertain, "Reason")
            }));
        Assert.Equal(HttpStatusCode.Redirect, recovery.StatusCode);
        Assert.Contains("queue=receiving-work", recovery.Headers.Location!.ToString(), StringComparison.Ordinal);
        var final = await GetHtmlAsync(client, recovery.Headers.Location!.ToString());

        Assert.Contains(expectedNotice, final, StringComparison.Ordinal);
        Assert.Equal(1, mover.MoveCalls);
        Assert.Equal(remainsUncertain, final.Contains("Check move status", StringComparison.Ordinal));
        Assert.Equal(showsSuggestedMove, final.Contains("moveFolderDialog", StringComparison.Ordinal));
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
            new Dictionary<string, string> { ["ClassificationKey"] = "received:999" },
            new Dictionary<string, string> { ["ClassificationKey"] = "sent:999" },
            new Dictionary<string, string>
            {
                ["ClassificationKey"] = "other-received",
                ["OtherClassificationName"] = new string('n', MailCategory.OtherNameMaxLength + 1),
                ["OtherClassificationReasoning"] = "No existing category fits."
            },
            new Dictionary<string, string>
            {
                ["ClassificationKey"] = "other-received",
                ["OtherClassificationName"] = "New category",
                ["OtherClassificationReasoning"] = new string('r', MailCategory.OtherReasoningMaxLength + 1)
            }
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
                "Choose a valid classification and complete any Other details.",
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
                ["ClassificationKey"] = "received:999",
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
    public async Task AnUnknownMessageIsNotFound()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync($"/Inbox/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheNavigationSeparatesTheInboxFromTheReceivedItemRecord()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, "/Inbox");

        Assert.Contains("href=\"/Inbox\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/Operations\"", html, StringComparison.Ordinal);
        Assert.Contains(">Operations<", html, StringComparison.Ordinal);
    }

    private static async Task<string> GetHtmlAsync(HttpClient client, string route)
    {
        using var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
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

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

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
                    "Reviewed source evidence and confirmed the case intake.",
                    CaseType.Inspection,
                    QdosPrincipal.Code,
                    new(true, true, true, true)),
                CancellationToken.None);
    }

    /// <summary>
    /// QDOS is one of the shared foundation migration's seeded principals, so
    /// this resolves the seed rather than inserting a second one of its own
    /// (INTK-060).
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

    private static async Task<Guid[]> SeedAsync(
        IntakeWebApplicationFactory factory,
        string mailboxId,
        string mailboxAddress,
        int count,
        DateTimeOffset? lastCompletedAtUtc = null)
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
                        $"Message {index} from {mailboxId}",
                        "Please inspect the vehicle at the address supplied.",
                        [new("estimate.pdf", "application/pdf", 2048)],
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

    private static async Task StoreForwardedRouteAsync(
        IntakeWebApplicationFactory factory,
        string mailboxId,
        string messageId)
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
                MailRouteDecision: new(
                    MailRouteDisposition.Accepted,
                    new("QDOS", MailRouteKind.DirectProvider, "QDOS"),
                    [],
                    "Fixture accepted route.",
                    "qdos_mail_route",
                    1,
                    [new("desk@collisionengineers.co.uk", "transport")],
                    [new("original@qdosassist.co.uk", "inline forward")],
                    new("original@qdosassist.co.uk", "inline forward"))),
            CancellationToken.None);
    }

    private static async Task ConfigureFolderBindingAsync(
        IntakeWebApplicationFactory factory,
        string mailboxIdentity,
        MailLogicalFolderType folderType,
        string folderIdentity)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var mailbox = await context.ApprovedMailboxes
            .Include(item => item.FolderBindings)
            .SingleAsync(item => item.Address == FirstMailboxAddress);
        mailbox.MailboxIdentity = mailboxIdentity;
        mailbox.InboxFolderIdentity = "inbox-folder";
        mailbox.SentFolderIdentity = "sent-folder";
        mailbox.Version++;
        mailbox.FolderBindings.Add(new()
        {
            ApprovedMailboxId = mailbox.Id,
            FolderType = folderType.ToString(),
            FolderIdentity = folderIdentity
        });
        await context.SaveChangesAsync();
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
                    MailCategory.Received(ReceivedMailFamily.NewInstructionReceived, "inspection"),
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

    private sealed class RecordingFolderMover : IRetainedMailFolderMover
    {
        public bool IsAvailable => true;
        public int MoveCalls { get; private set; }
        public RetainedMailFolderMoveCoordinates? Coordinates { get; private set; }
        private bool moved;

        public Task MoveAsync(RetainedMailFolderMoveCoordinates coordinates, CancellationToken cancellationToken)
        {
            MoveCalls++;
            Coordinates = coordinates;
            moved = true;
            return Task.CompletedTask;
        }

        public Task<string?> GetParentFolderIdAsync(string mailboxId, string immutableMessageId, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(moved ? Coordinates?.DestinationFolderId : "inbox");
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

        public Task<MailClassificationDossier> AppendCorrectionAsync(
            Guid messageId,
            int expectedVersion,
            MailClassificationResult before,
            MailClassificationResult after,
            string actor,
            string reason,
            DateTimeOffset correctedAtUtc,
            CancellationToken cancellationToken)
        {
            CorrectionCalls++;
            throw new InvalidOperationException(
                "A read-only mail workspace action attempted to write a classification correction.");
        }
    }

    private sealed class SequenceRecoveryFolderMover(string recoveredParent) : IRetainedMailFolderMover
    {
        private readonly Queue<string> parents = new(["inbox", "unresolved-folder", recoveredParent]);

        public bool IsAvailable => true;
        public int MoveCalls { get; private set; }

        public Task MoveAsync(RetainedMailFolderMoveCoordinates coordinates, CancellationToken cancellationToken)
        {
            MoveCalls++;
            throw new InvalidOperationException("The provider response was interrupted.");
        }

        public Task<string?> GetParentFolderIdAsync(string mailboxId, string immutableMessageId, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(parents.Count == 0 ? recoveredParent : parents.Dequeue());
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
