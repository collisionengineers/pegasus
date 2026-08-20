using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Email;
using Pegasus.Infrastructure.Persistence;

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
    private const string FirstMailboxAddress = "instructions@collisionengineers.co.uk";
    private const string SecondMailboxId = "reports";
    private const string SecondMailboxAddress = "reports@collisionengineers.co.uk";

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

    [Fact]
    public async Task ScopingAndPagingCarryTheMailboxFolderAndPageForward()
    {
        using var factory = new IntakeWebApplicationFactory();
        await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 30);
        using var client = IntakeWebDriver.CreateClient(factory);

        var scoped = await GetHtmlAsync(client, $"/Inbox?mailbox={FirstMailboxId}");

        Assert.Contains($"/Inbox?mailbox={FirstMailboxId}&amp;pageNumber=2", scoped, StringComparison.Ordinal);
        Assert.Contains("Page 1 of 2", scoped, StringComparison.Ordinal);

        var secondPage = await GetHtmlAsync(client, $"/Inbox?mailbox={FirstMailboxId}&pageNumber=2");
        Assert.Contains("Page 2 of 2", secondPage, StringComparison.Ordinal);
        // The row link carries the exact list position back into detail.
        Assert.Contains($"mailbox={FirstMailboxId}&amp;pageNumber=2", secondPage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheRefreshFormCarriesTheActiveFilterAndPage()
    {
        using var factory = new IntakeWebApplicationFactory();
        await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 30);
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Inbox?mailbox={FirstMailboxId}&pageNumber=2");

        // Refresh reruns the query the operator is looking at. A bare GET form
        // submits nothing and silently resets the screen to page one of
        // everything, which the requirement forbids.
        var form = Between(html, "<form method=\"get\" data-refresh-form>", "</form>");
        Assert.Contains(
            $"name=\"mailbox\" value=\"{FirstMailboxId}\"",
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
        Assert.Contains("Updated", current, StringComparison.Ordinal);
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
    public async Task SentNamesWhatIsNotKeptAndDeletedRequiresAnExplicitBoundedSearch()
    {
        using var factory = new IntakeWebApplicationFactory();
        await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1);
        using var client = IntakeWebDriver.CreateClient(factory);

        var sent = await GetHtmlAsync(client, "/Inbox?folder=sent");
        var deleted = await GetHtmlAsync(client, "/Inbox?folder=deleted");

        Assert.Contains("Sent messages are not kept in Pegasus yet.", sent, StringComparison.Ordinal);
        Assert.Contains(
            "Enter a search term to read accepted Deleted Items within the selected approved mailbox scope.",
            deleted,
            StringComparison.Ordinal);
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
            "/Inbox?folder=deleted&mailbox=empty-mailbox&search=needle");

        Assert.Equal("empty-mailbox", source.MailboxId);
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
            "/Inbox?folder=deleted&mailbox=empty-mailbox&search=needle&pageNumber=2");
        Assert.Contains("Deleted match 0", secondPage, StringComparison.Ordinal);
        Assert.Contains("Page 2 of 2", secondPage, StringComparison.Ordinal);

        source.Result = new([], false, DeletedMailSearchState.Unavailable);
        var unavailable = await GetHtmlAsync(
            client,
            "/Inbox?folder=deleted&mailbox=empty-mailbox&search=needle");
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
                [new("empty-mailbox", "empty@example.invalid", "inbox-folder")]),
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
            "/Inbox?folder=deleted&mailbox=empty-mailbox&search=needle");

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
                [new("empty-mailbox", "empty@example.invalid", "inbox-folder")]),
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
            "/Inbox?folder=deleted&mailbox=empty-mailbox&search=needle");

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

        var query = $"?mailbox={FirstMailboxId}&pageNumber=1";
        var message = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}{query}");

        Assert.Contains("Please inspect the vehicle", message, StringComparison.Ordinal);
        Assert.Contains("intake@collisionengineers.co.uk", message, StringComparison.Ordinal);
        // Nothing was processed, so the state strip says so rather than blanking.
        Assert.Contains("Not yet processed", message, StringComparison.Ordinal);
        Assert.Contains("Not associated with a case.", message, StringComparison.Ordinal);
        // Back reconstructs the exact list position.
        Assert.Contains($"/Inbox?mailbox={FirstMailboxId}", message, StringComparison.Ordinal);
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

        Assert.Contains("<h2 id=\"folder-recommendation-heading\">Folder recommendation</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Recommended Outlook folder</dt><dd>Unavailable —", html, StringComparison.Ordinal);
        Assert.Contains("no current classification decision", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Move message", html, StringComparison.OrdinalIgnoreCase);
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

        Assert.Contains("Classification evidence", html, StringComparison.Ordinal);
        Assert.Contains("shared-mail-policy version 3", html, StringComparison.Ordinal);
        Assert.Contains("sender-domain", html, StringComparison.Ordinal);
        Assert.Contains("Permanent correction history", html, StringComparison.Ordinal);
        Assert.Contains("Save classification correction", html, StringComparison.Ordinal);
        Assert.Contains("name=\"ExpectedClassificationVersion\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"1\"", html, StringComparison.Ordinal);
        // The Core operational-destination policy fails closed to the same
        // "Unidentified" wording the page already uses for an unmatched
        // Queue/Filed-to state, computed live from this Unclassified decision.
        Assert.Contains("<dt>Operational destination</dt><dd>Unidentified</dd>", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Destination policy</dt><dd>mail_operational_destination version 1</dd>", html, StringComparison.Ordinal);
        // PLAT-011: "Decided by" resolves the persisted "system-worker:..." actor
        // to an operator-facing name, never the raw stored value.
        Assert.Contains("<dt>Decided by</dt><dd>System</dd>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("system-worker:approved-inbox-poller", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Recommended Outlook folder</dt><dd>Unavailable —", html, StringComparison.Ordinal);
        Assert.Contains("absent or ambiguous", html, StringComparison.Ordinal);
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
        Assert.Contains("<dt>Operational destination</dt><dd>Receiving work</dd>", html, StringComparison.Ordinal);
        Assert.Contains("<dt>Destination policy</dt><dd>mail_operational_destination version 1</dd>", html, StringComparison.Ordinal);
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

        Assert.Contains("<dt>Recommended Outlook folder</dt><dd>Instructions</dd>", html, StringComparison.Ordinal);
        Assert.Contains("mail_logical_folder version 1", html, StringComparison.Ordinal);
        Assert.DoesNotContain("outlook-folder-instructions", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Move message", html, StringComparison.OrdinalIgnoreCase);
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

        var html = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}");

        Assert.Contains("Move to Instructions", html, StringComparison.Ordinal);
        Assert.Contains("Confirm Outlook folder move", html, StringComparison.Ordinal);
        Assert.DoesNotContain("outlook-folder-instructions", html, StringComparison.Ordinal);
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
        Assert.Equal(1, mover.MoveCalls);
        Assert.Equal(FirstMailboxId, mover.Coordinates!.MailboxId);
        Assert.Equal("inbox", mover.Coordinates.SourceFolderId);
        Assert.Equal("outlook-folder-instructions", mover.Coordinates.DestinationFolderId);
        await using var scope = baseFactory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IRetainedMailQueries>();
        Assert.Empty((await queries.ListAsync(
            new(null, MailFolderScope.Inbox), 1, 25, CancellationToken.None)).Items);
        var searchHtml = await GetHtmlAsync(client, "/Inbox?search=estimate");
        Assert.Contains(
            "Search includes retained messages in their current Outlook folders.",
            searchHtml,
            StringComparison.Ordinal);
        Assert.Contains($"Message 0 from {FirstMailboxId}", searchHtml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("outlook-folder-instructions", "Message moved to the recommended Outlook folder.", false)]
    [InlineData("inbox", "The message was not moved. You can retry with a new confirmation.", false)]
    [InlineData("unresolved-folder", "The move result is uncertain. Retry this same confirmation to check its current location.", true)]
    public async Task AuthenticatedUncertainMoveReusesTheSameConfirmationForExactRecovery(
        string recoveredParent,
        string expectedNotice,
        bool remainsUncertain)
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

        var initial = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}");
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

        var uncertain = await GetHtmlAsync(client, confirmation.Headers.Location!.ToString());
        Assert.Contains("Check move status", uncertain, StringComparison.Ordinal);
        Assert.Contains("value=\"Confirmed after reviewing the message.\"", uncertain, StringComparison.Ordinal);
        Assert.DoesNotContain("outlook-folder-instructions", uncertain, StringComparison.Ordinal);
        var recoveryAction = WebUtility.HtmlDecode(Regex.Match(
            uncertain,
            "<form method=\"post\" action=\"([^\"]*handler=MoveToRecommendedFolder[^\"]*)\"",
            RegexOptions.IgnoreCase).Groups[1].Value);
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
        var final = await GetHtmlAsync(client, recovery.Headers.Location!.ToString());

        Assert.Contains(expectedNotice, final, StringComparison.Ordinal);
        Assert.Equal(1, mover.MoveCalls);
        Assert.Equal(remainsUncertain, final.Contains("Check move status", StringComparison.Ordinal));
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
        Assert.Contains("<dt>Forwarded by</dt>", detail, StringComparison.Ordinal);
        Assert.Contains("sender@example.invalid", detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMessageOpenedFromAScopeItIsNotInStillRendersWithTheWayBack()
    {
        using var factory = new IntakeWebApplicationFactory();
        var ids = await SeedAsync(factory, FirstMailboxId, FirstMailboxAddress, count: 1);
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Inbox/{ids[0]:D}?mailbox={SecondMailboxId}");

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
            if (!await context.ApprovedInboxPollStates.AnyAsync(item => item.MailboxId == mailboxId))
            {
                context.ApprovedInboxPollStates.Add(new()
                {
                    MailboxId = mailboxId,
                    MailboxAddress = mailboxAddress,
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
                    mailboxId,
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
            .Where(item => item.MailboxId == mailboxId)
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
        internal string? MailboxId { get; private set; }

        internal string? SearchTerm { get; private set; }

        internal int MaximumMessages { get; private set; }

        internal DeletedMailSourceResult Result { get; set; } = new(
            Enumerable.Range(0, 26)
                .Select(index => new DeletedMailSearchItem(
                    "empty-mailbox",
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
                    new("empty-mailbox", "empty@example.invalid", IsPolled: true),
                    new("other-mailbox", "other@example.invalid", IsPolled: true)
                ]);

        public Task<DeletedMailSourceResult> SearchAsync(
            string? mailboxId,
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
