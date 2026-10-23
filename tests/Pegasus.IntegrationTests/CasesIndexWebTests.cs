using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Web.Authentication;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class CasesIndexWebTests
{
    [Fact]
    public async Task AwaitingImageSelectionCarriesTheExactOriginReceiptIntoConfirmation()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine("AB12CDE"));
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "AB12 CDE", "AWAITING-ORIGIN-01");

        var upload = await IntakeWebDriver.UploadAsync(
            client,
            "awaiting-origin.png",
            "image/png",
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
            Guid.NewGuid().ToString("N"));
        var stagedReceiptId = IntakeWebDriver.ReceiptId(upload);
        var processed = await IntakeWebDriver.ProcessQueuedAsync(factory, upload);
        var receiptId = IntakeWebDriver.ReceiptId(processed);
        await using var scope = factory.Services.CreateAsyncScope();
        // The matched Case holds the upload unregistered until staff leave it
        // undecided; registering is what makes the awaiting record.
        await IntakeWebDriver.DeclineImageMatchAndReplayAsync(scope.ServiceProvider, receiptId);
        var image = await scope.ServiceProvider.GetRequiredService<IImageIntakeQueries>()
            .GetByOriginReceiptAsync(receiptId, CancellationToken.None);
        Assert.NotNull(image);

        using var response = await client.GetAsync(
            $"/Cases?tab=unassigned&selected={image!.Record.Id:D}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"name=\"receiptId\" value=\"{receiptId:D}\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"receiptVersion\"", html, StringComparison.Ordinal);
        Assert.Contains("Add to an existing case", html, StringComparison.Ordinal);
        Assert.DoesNotContain(stagedReceiptId.ToString("D"), html, StringComparison.Ordinal);

        var receipt = await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        var workflow = await scope.ServiceProvider.GetRequiredService<ICaseWorkflowStore>()
            .GetAsync(caseId, CancellationToken.None);
        Assert.NotNull(receipt);
        Assert.NotNull(workflow);
        var operationId = Guid.NewGuid();
        var fields = new Dictionary<string, string>
        {
            ["id"] = image.Record.Id.ToString("D"),
            ["receiptId"] = receiptId.ToString("D"),
            ["operationId"] = operationId.ToString("D"),
            ["receiptVersion"] = receipt!.Version.ToString(CultureInfo.InvariantCulture),
            ["caseId"] = caseId.ToString("D"),
            ["caseVersion"] = workflow!.Version.ToString(CultureInfo.InvariantCulture),
            ["reference"] = string.Empty,
            ["reason"] = "Staff matched the reviewed image to the instructed case."
        };

        using var first = await PostImageAttachAsync(client, fields);
        using var replay = await PostImageAttachAsync(client, fields);
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, replay.StatusCode);

        var forged = new Dictionary<string, string>(fields)
        {
            ["receiptId"] = Guid.NewGuid().ToString("D")
        };
        using var rejected = await PostImageAttachAsync(client, forged);
        Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
    }

    [Fact]
    public async Task AwaitingManualImageGroupLinksToItsSubmissionConfirmation()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "AB12 CDE", "GROUP-GUARD-01");
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);
        var upload = await IntakeWebDriver.PostUploadManyAsync(
            client, form.AntiforgeryToken, form.ExternalReceiptToken,
            [
                ("overview.png", "image/png", Convert.FromBase64String(MultiFormatFixture.TinyPngBase64)),
                ("close-up.png", "image/png", Convert.FromBase64String(MultiFormatFixture.TinyPngBase64))
            ]);
        var groupId = Guid.Parse(upload.Location!.OriginalString.Split('/').Last());
        var processed = await IntakeWebDriver.ProcessQueuedAsync(factory, upload);
        var receiptId = IntakeWebDriver.ReceiptId(processed);
        await using (var reconcileScope = factory.Services.CreateAsyncScope())
        {
            await IntakeWebDriver.ReconcileGroupedImageIntakeAsync(reconcileScope.ServiceProvider);
        }

        // Registration is automatic (v30, 24 September 2026); the fixture
        // registers the group the way the pipeline does.
        await using var scope = factory.Services.CreateAsyncScope();
        var origin = await scope.ServiceProvider.GetRequiredService<IImageIntakeOriginResolver>()
            .ResolveOriginAsync(receiptId, CancellationToken.None);
        Assert.NotNull(origin);
        await scope.ServiceProvider.GetRequiredService<IRegisterImageIntake>().ExecuteAsync(
            new(
                origin!,
                "AB12CDE",
                ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]),
                $"image-intake-register:group:{groupId:N}",
                "Registered for the Cases index fixture.",
                groupId),
            CancellationToken.None);
        var image = await scope.ServiceProvider.GetRequiredService<IImageIntakeQueries>()
            .GetByOriginReceiptAsync(receiptId, CancellationToken.None);
        Assert.NotNull(image);
        using var page = await client.GetAsync($"/Cases?tab=unassigned&selected={image!.Record.Id:D}");
        var html = await page.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains($"/Upload/Group/{groupId:D}", html, StringComparison.Ordinal);
        Assert.Contains("Continue with this submission", html, StringComparison.Ordinal);
        Assert.DoesNotContain("?handler=Attach", html, StringComparison.Ordinal);

        var receipt = await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        var workflow = await scope.ServiceProvider.GetRequiredService<ICaseWorkflowStore>()
            .GetAsync(caseId, CancellationToken.None);
        Assert.NotNull(receipt);
        Assert.NotNull(workflow);
        var caseReference = workflow!.Identity.Reference;
        var forgedIndex = new Dictionary<string, string>
        {
            ["id"] = image.Record.Id.ToString("D"),
            ["receiptId"] = receiptId.ToString("D"),
            ["operationId"] = Guid.NewGuid().ToString("D"),
            ["receiptVersion"] = receipt!.Version.ToString(CultureInfo.InvariantCulture),
            ["caseId"] = caseId.ToString("D"),
            ["caseVersion"] = workflow!.Version.ToString(CultureInfo.InvariantCulture),
            ["reference"] = caseReference,
            ["reason"] = "Forged single-member group attachment."
        };
        using var rejected = await PostImageAttachAsync(client, forgedIndex);
        Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        Assert.Null((await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None))!.CurrentCaseId);

        var group = await scope.ServiceProvider.GetRequiredService<IIntakeSubmissionGroupStore>()
            .GetAsync(groupId, CancellationToken.None);
        Assert.NotNull(group);
        var statusQueries = scope.ServiceProvider.GetRequiredService<IQueuedIntakeStatusQueries>();
        var memberStatuses = await Task.WhenAll(group!.Members.Select(async member => new
        {
            Member = member,
            Status = await statusQueries.GetAsync(member.StagedReceiptId, CancellationToken.None)
        }));
        var memberId = Assert.Single(memberStatuses, item => item.Status?.ProcessedReceiptId == receiptId)
            .Member.StagedReceiptId;
        using var memberPage = await client.GetAsync($"/Upload/Status/{memberId:D}");
        Assert.Equal(HttpStatusCode.Redirect, memberPage.StatusCode);
        Assert.Equal($"/Upload/Group/{groupId:D}", memberPage.Headers.Location?.OriginalString);

        forgedIndex.Remove("id");
        forgedIndex["__RequestVerificationToken"] = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        using var memberRejected = await client.PostAsync(
            $"/Upload/Status/{memberId:D}?handler=Attach",
            new FormUrlEncodedContent(forgedIndex));
        Assert.Equal(HttpStatusCode.Redirect, memberRejected.StatusCode);
        Assert.Null((await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None))!.CurrentCaseId);
        foreach (var memberStatus in memberStatuses)
        {
            var memberReceiptId = memberStatus.Status!.ProcessedReceiptId ?? memberStatus.Member.StagedReceiptId;
            Assert.Null((await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(memberReceiptId, CancellationToken.None))!.CurrentCaseId);
        }
    }

    [Fact]
    public async Task SearchUsesAuthorizedCoreQueryAndPreservesEveryFilterInPagingUrl()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var search = new RecordingSearchCases();
        using var factory = Configure(baseFactory, search);
        using var client = CreateClient(factory);
        var engineerId = Guid.NewGuid();
        var path = "/Search?case=QDOS3100042&registration=AB12CDE&claimant=Claimant&claimNumber=CLM42"
            + $"&principal=QDOS&state=Review&engineerId={engineerId:D}"
            + "&receivedDate=2031-05-01"
            + "&fromDate=2031-04-01&toDate=2031-05-31&origin=Email&query=needle&page=2";

        using var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        // The shell also searches Held cases for its notification menu after
        // this handler. Identify the actual filtered request rather than
        // assuming it is the last call made while rendering the page.
        var query = Assert.Single(search.Queries, candidate => candidate.Filters.Query == "needle");
        Assert.Equal(ActorKind.Staff, query.Actor.Kind);
        Assert.NotEmpty(query.Actor.Roles);
        Assert.Equal(2, query.Page);
        Assert.Equal(25, query.PageSize);
        Assert.Equal("QDOS3100042", query.Filters.CaseReference);
        Assert.Equal("AB12CDE", query.Filters.Registration);
        Assert.Equal("Claimant", query.Filters.Claimant);
        Assert.Equal("CLM42", query.Filters.ClaimNumber);
        Assert.Equal("QDOS", query.Filters.Principal);
        Assert.Equal(new[] { CaseLifecycleState.Review }, query.Filters.States);
        Assert.Equal(engineerId, query.Filters.EngineerId);
        Assert.Equal(new DateOnly(2031, 5, 1), query.Filters.ReceivedDate);
        Assert.Equal(new DateOnly(2031, 4, 1), query.Filters.FromDate);
        Assert.Equal(new DateOnly(2031, 5, 31), query.Filters.ToDate);
        Assert.Equal("Email", query.Filters.Origin);
        Assert.Equal("needle", query.Filters.Query);
        Assert.Contains($"href=\"/Cases/{search.CaseId:D}\"", html, StringComparison.Ordinal);
        foreach (var (field, value) in new[]
                 {
                     ("search-query", "needle"), ("search-registration", "AB12CDE"),
                     ("search-claimant", "Claimant"), ("search-claim-number", "CLM42"),
                     ("search-from-date", "2031-04-01"),
                     ("search-to-date", "2031-05-31"), ("search-origin", "Email")
                 })
        {
            Assert.Matches(
                $"id=\"{field}\"[^>]*value=\"{Regex.Escape(value)}\"",
                html);
        }

        // Principal and Engineer are selects (v41 W2): the value filtering
        // stays listed, selected, when the lists do not offer it.
        Assert.Contains("<option value=\"QDOS\" selected=\"selected\">QDOS</option>", html, StringComparison.Ordinal);
        Assert.Contains($"<option value=\"{engineerId:D}\" selected=\"selected\">{engineerId:D}</option>", html, StringComparison.Ordinal);

        // The ported page: selectable rows, a server-rendered Selected Case
        // pane, and the Closed chip on the closed result (operator, 15 October
        // 2026).
        Assert.Contains("Selected Case", html, StringComparison.Ordinal);
        Assert.Contains("data-preview-target", html, StringComparison.Ordinal);
        Assert.Contains($"data-select-id=\"{search.CaseId:D}\"", html, StringComparison.Ordinal);
        Assert.Contains($"data-select-id=\"{search.ClosedCaseId:D}\"", html, StringComparison.Ordinal);
        Assert.Contains(
            $"<span class=\"status status--neutral\">{Pegasus.Web.Presentation.OperatorLabels.ClosedStage}</span>",
            html,
            StringComparison.Ordinal);

        var next = Regex.Match(
            html,
            "<a[^>]*href=\"(?<href>[^\"]+)\"[^>]*>Next</a>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(next.Success, "The bounded search result must render a next-page URL.");
        var href = Uri.UnescapeDataString(WebUtility.HtmlDecode(next.Groups["href"].Value));
        foreach (var expected in new[]
                 {
                     "case=QDOS3100042", "registration=AB12CDE", "claimant=Claimant",
                     "claimNumber=CLM42", "principal=QDOS", "state=Review",
                     $"engineerId={engineerId:D}", "receivedDate=2031-05-01",
                     "fromDate=2031-04-01", "toDate=2031-05-31",
                     "origin=Email", "query=needle", "page=3"
                 })
        {
            Assert.Contains(expected, href, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain("total", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The State filter is one option per stage, each posting its key
    /// (operator, 11 October 2026): With Engineer finds its one state, Report
    /// sent finds Report sent Cases with no open query, Query finds every
    /// open Case with an open query, whatever its state, and Closed finds every
    /// closed disposition (operator, 15 October 2026).
    /// </summary>
    [Theory]
    [InlineData("ReportPreparation", "With Engineer", null)]
    [InlineData("ReportSent", "Report sent", false)]
    [InlineData("Query", "Query", true)]
    [InlineData(Pegasus.Web.Presentation.OperatorLabels.ClosedStage, Pegasus.Web.Presentation.OperatorLabels.ClosedStage, null)]
    public async Task SearchStateFilterTakesAStageKey(string key, string label, bool? hasOpenQuery)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var search = new RecordingSearchCases();
        using var factory = Configure(baseFactory, search);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync($"/Search?state={key}&query=needle");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.Single(search.Queries, candidate => candidate.Filters.Query == "needle");
        IReadOnlyList<CaseLifecycleState> expectedStates = key switch
        {
            "ReportPreparation" => [CaseLifecycleState.ReportPreparation],
            "ReportSent" => [CaseLifecycleState.ReportSent],
            Pegasus.Web.Presentation.OperatorLabels.ClosedStage =>
                [.. Enum.GetValues<CaseLifecycleState>().Where(Pegasus.Core.Lifecycle.CaseLifecycleRules.IsTerminal)],
            _ => [.. Enum.GetValues<CaseLifecycleState>().Where(state => !Pegasus.Core.Lifecycle.CaseLifecycleRules.IsTerminal(state))]
        };
        Assert.Equal(expectedStates, query.Filters.States);
        Assert.Equal(hasOpenQuery, query.Filters.HasOpenQuery);
        var option = Assert.Single(Regex.Matches(
            html,
            $"<option[^>]*>{label}</option>",
            RegexOptions.CultureInvariant));
        Assert.Contains($"value=\"{key}\"", option.Value, StringComparison.Ordinal);
        Assert.Contains("selected", option.Value, StringComparison.Ordinal);

        // Every option posts a stage key, With Engineer once.
        var select = Regex.Match(html, "<select id=\"search-state\"[^>]*>.*?</select>", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(select.Success, "The State filter is not rendered.");
        Assert.Equal(
            new[] { string.Empty }.Concat(Pegasus.Web.Presentation.OperatorLabels.CaseStageOptions.Select(stage => stage.Key)).ToArray(),
            Regex.Matches(select.Value, "<option value=\"(?<key>[^\"]*)\"").Select(match => match.Groups["key"].Value).ToArray());
        Assert.Single(Regex.Matches(select.Value, ">With Engineer</option>"));
    }

    /// <summary>
    /// A Case with an open query reads Query in the results, whatever its state
    /// (operator, 11 October 2026).
    /// </summary>
    [Fact]
    public async Task SearchReadsACaseWithAnOpenQueryAsQuery()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var search = new RecordingSearchCases { OpenQueryOnFirst = true };
        using var factory = Configure(baseFactory, search);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/Search?query=needle");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<span class=\"status status--purple\">Query</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyAndUnavailableQueriesRenderDistinctNonLeakingStates()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var search = new RecordingSearchCases { ReturnEmpty = true };
        using var factory = Configure(baseFactory, search);
        using var client = CreateClient(factory);

        using var emptyResponse = await client.GetAsync("/Search?principal=QDOS");
        var emptyHtml = await emptyResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
        // The empty result is one sentence, matching the "No vehicle images
        // match these filters." line the same page renders for the image
        // section. The superseded second sentence must not come back.
        Assert.Contains("No cases match these filters.", emptyHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("No matching cases", emptyHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("unauthorised", emptyHtml, StringComparison.OrdinalIgnoreCase);

        search.ThrowUnavailable = true;
        using var failedResponse = await client.GetAsync("/Search?principal=QDOS");
        var failedHtml = await failedResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failedResponse.StatusCode);
        Assert.Contains("Cases are unavailable", failedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), failedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelectedRowPreviewsItsFactsServerSide()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var search = new RecordingSearchCases();
        using var factory = Configure(baseFactory, search);
        using var client = CreateClient(factory);

        // ?selected= reads the named row server-side: its facts, its D3
        // terminal chip and its own Case link.
        using var response = await client.GetAsync($"/Search?selected={search.ClosedCaseId:D}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = Regex.Match(
            html,
            "<tr[^>]*data-select-id=\"" + search.ClosedCaseId.ToString("D") + "\"[^>]*aria-selected=\"(?<selected>[^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(row.Success, "The selected row must carry its selection state.");
        Assert.Equal("true", row.Groups["selected"].Value);

        Assert.Contains("Selected Case", html, StringComparison.Ordinal);
        Assert.Contains("Inspection", html, StringComparison.Ordinal);
        // The Case/PO is the span the preview's Copy Case/PO reads (v41 W3).
        Assert.Contains("<span id=\"selected-case-po\">QDOS3100043</span> · AB12CDE", html, StringComparison.Ordinal);
        Assert.Contains("Rear-end impact at a roundabout", html, StringComparison.Ordinal);
        // The circumstances are read once, for the selected Case alone.
        Assert.Equal(search.ClosedCaseId, Assert.Single(search.Circumstances.Reads));
        Assert.Contains($"<dt>{Pegasus.Web.Presentation.CaseWorkspaceLabels.Frame.ClaimReference}</dt><dd>CLM43</dd>", html, StringComparison.Ordinal);
        Assert.Contains("Unassigned", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Cases/{search.ClosedCaseId:D}\"", html, StringComparison.Ordinal);
        // Copy Case/PO ships hidden and site.js reveals it (v41 W3).
        Assert.Contains(
            $"<button type=\"button\" class=\"btn\" data-copy-target=\"selected-case-po\" hidden>{Pegasus.Web.Presentation.OperatorLabels.Search.CopyCaseReference}</button>",
            html,
            StringComparison.Ordinal);
        Assert.Contains("name=\"selected\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Principal and Engineer are selects of everything the filters can find,
    /// not of what one page of results shows, and State reads Case stage
    /// (v41 W2, signed off 19 October 2026).
    /// </summary>
    [Fact]
    public async Task PrincipalAndEngineerAreSelectsOfEveryChoiceAndStateReadsCaseStage()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var search = new RecordingSearchCases();
        var images = new RecordingImageIntakeQueries { ActivePrincipalCodes = ["QDOS", "ZEN"] };
        var picked = new CaseEngineerChoice(Guid.NewGuid(), "e.mawdsley");
        using var factory = Configure(
            baseFactory, search, images, new FixedEngineerChoices(picked, new CaseEngineerChoice(Guid.NewGuid(), "r.okafor")));
        using var client = CreateClient(factory);

        using var response = await client.GetAsync($"/Search?query=needle&principal=ZEN&engineerId={picked.StaffId:D}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var principal = Regex.Match(html, "<select id=\"search-principal\"[^>]*>.*?</select>", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(principal.Success, "The Principal filter is not a select.");
        Assert.Equal(
            [string.Empty, "QDOS", "ZEN"],
            Regex.Matches(principal.Value, "<option value=\"(?<code>[^\"]*)\"").Select(match => match.Groups["code"].Value).ToArray());
        Assert.Contains("<option value=\"ZEN\" selected=\"selected\">ZEN</option>", principal.Value, StringComparison.Ordinal);
        Assert.Contains(
            Pegasus.Web.Presentation.OperatorLabels.Search.AllPrincipals,
            principal.Value,
            StringComparison.Ordinal);

        var engineer = Regex.Match(html, "<select id=\"search-engineer\"[^>]*>.*?</select>", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(engineer.Success, "The Engineer filter is not a select.");
        Assert.Equal(3, Regex.Count(engineer.Value, "<option "));
        Assert.Contains($"<option value=\"{picked.StaffId:D}\" selected=\"selected\">e.mawdsley</option>", engineer.Value, StringComparison.Ordinal);
        Assert.Contains("r.okafor</option>", engineer.Value, StringComparison.Ordinal);
        Assert.Contains(
            Pegasus.Web.Presentation.OperatorLabels.Search.AllEngineers,
            engineer.Value,
            StringComparison.Ordinal);

        Assert.Contains(
            $"<label for=\"search-state\">{Pegasus.Web.Presentation.OperatorLabels.Search.CaseStage}</label>",
            html,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Results gain Type, before State, and Due, after it, read from the
    /// projection the page already holds (v41 W3).
    /// </summary>
    [Fact]
    public async Task ResultsGainTypeBeforeStateAndDueAfterIt()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var search = new RecordingSearchCases();
        using var factory = Configure(baseFactory, search);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/Search?query=needle");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var heads = Regex.Matches(html, "<th scope=\"col\">(?<head>[^<]*)</th>", RegexOptions.CultureInvariant)
            .Select(match => match.Groups["head"].Value)
            .ToArray();
        string[] expected =
        [
            "Case/PO", "Vehicle", "Claimant", "Principal",
            Pegasus.Web.Presentation.OperatorLabels.Search.TypeColumn,
            "State",
            Pegasus.Web.Presentation.CaseWorkspaceLabels.Frame.Due,
            Pegasus.Web.Presentation.CaseWorkspaceLabels.Frame.Editing
        ];
        Assert.Equal(expected, heads.Skip(Array.IndexOf(heads, "Case/PO")).Take(expected.Length).ToArray());
        Assert.Contains(
            $"<td>{Pegasus.Web.Presentation.OperatorLabels.CaseTypeName(CaseType.Inspection)}</td>",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoSelectionTheFirstCaseIsReadAndEveryOtherRowCarriesTheAddressForItsOwn()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var search = new RecordingSearchCases();
        using var factory = Configure(baseFactory, search);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/Search?query=needle");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(search.CaseId, Assert.Single(search.Circumstances.Reads));
        Assert.Contains("Struck from behind at a junction", html, StringComparison.Ordinal);
        var otherRow = Regex.Match(
            html,
            "<tr[^>]*data-select-id=\"" + search.ClosedCaseId.ToString("D") + "\".*?</tr>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant).Value;
        // Link generation orders the query itself, so the address is read back and its parts checked.
        var address = WebUtility.HtmlDecode(
            Regex.Match(otherRow, "data-circumstances-url=\"(?<url>[^\"]+)\"", RegexOptions.CultureInvariant).Groups["url"].Value);
        Assert.StartsWith("/Search?", address, StringComparison.Ordinal);
        Assert.Contains("handler=Circumstances", address, StringComparison.Ordinal);
        Assert.Contains($"id={search.ClosedCaseId:D}", address, StringComparison.Ordinal);
        Assert.Contains(
            Pegasus.Web.Presentation.OperatorLabels.Busy.Loading,
            WebUtility.HtmlDecode(otherRow),
            StringComparison.Ordinal);
        Assert.DoesNotContain("Rear-end impact at a roundabout", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheCircumstancesHandlerAnswersTheNamedCaseAsJson()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var search = new RecordingSearchCases();
        using var factory = Configure(baseFactory, search);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync($"/Search?handler=Circumstances&id={search.ClosedCaseId:D}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "Rear-end impact at a roundabout",
            body.RootElement.GetProperty("circumstances").GetString());
        Assert.Equal(search.ClosedCaseId, Assert.Single(search.Circumstances.Reads));

        // A Case read and found blank answers the word the preview prints for it.
        using var blank = await client.GetAsync($"/Search?handler=Circumstances&id={Guid.NewGuid():D}");
        using var blankBody = JsonDocument.Parse(await blank.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, blank.StatusCode);
        Assert.Equal("Not recorded", blankBody.RootElement.GetProperty("circumstances").GetString());
    }

    [Fact]
    public async Task AFailedCircumstancesReadTakesTheUnavailablePath()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var search = new RecordingSearchCases();
        search.Circumstances.ThrowUnavailable = true;
        using var factory = Configure(baseFactory, search);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/Search?query=needle");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Cases are unavailable", html, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Decision 8 and W: an Inspection + Audit Case with its Audit lists as
    /// two entries of the same Case, <c>{Case/PO}</c> opening the Inspection
    /// view and <c>a.{Case/PO}</c> the Audit view, each with its own preview
    /// heading, while paging still counts Cases; a Triage Case lists with its
    /// Triage state and opens <c>/Cases/{id}</c>.
    /// </summary>
    [Fact]
    public async Task AnAuditedCaseListsTwoEntriesAndATriageCaseShowsItsTriageState()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var search = new RecordingSearchCases { WithAuditAndTriage = true };
        using var factory = Configure(baseFactory, search);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/Search?query=QDOS31000");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.Single(search.Queries, candidate => candidate.Filters.Query == "QDOS31000");
        Assert.True(query.Filters.IncludeTriage);
        Assert.Equal(25, query.PageSize); // Cases, not entries.

        Assert.Contains($"href=\"/Cases/{search.AuditCaseId:D}?view=inspection\">QDOS3100044</a>", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Cases/{search.AuditCaseId:D}?view=audit\">a.QDOS3100044</a>", html, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(html, $"<tr[^>]*data-select-id=\"{search.AuditCaseId:D}\""));
        Assert.Contains("<h2><span id=\"selected-case-po\">QDOS3100044</span> · AB12CDE</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<h2><span id=\"selected-case-po\">a.QDOS3100044</span> · AB12CDE</h2>", html, StringComparison.Ordinal);
        Assert.Contains(">3 results<", html, StringComparison.Ordinal);
        // With nothing selected the first entry is, and a refresh keeps it.
        Assert.Matches("<tr[^>]*data-select-view=\"inspection\"[^>]*aria-selected=\"true\"", html);
        Assert.Matches("<tr[^>]*data-select-view=\"audit\"[^>]*aria-selected=\"false\"", html);
        Assert.Contains("name=\"selectedView\" value=\"inspection\"", html, StringComparison.Ordinal);

        Assert.Contains($"href=\"/Cases/{search.TriageCaseId:D}\">t.QDOS3100045</a>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"status status--navy\">Open</span>", html, StringComparison.Ordinal);
        // Razor keeps a data- attribute whose value is null, empty: a Case listed once names no view.
        Assert.DoesNotMatch($"<tr[^>]*data-select-id=\"{search.TriageCaseId:D}\"[^>]*data-select-view=\"[^\"]", html);
        // A Triage Case has no Case completeness requirements.
        Assert.DoesNotContain("Outstanding (", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A standalone Audit Case lists by its <c>a.</c> reference alone: no
    /// Audit pill beside it, as no Inspection or Triage row carries one
    /// (operator, 5 October 2026).
    /// </summary>
    [Fact]
    public async Task AStandaloneAuditCaseListsByItsReferenceWithNoTypePill()
    {
        // The list's quick detail reads the selected Case, so the row is a
        // real Case: an instruction Case recorded as a standalone Audit.
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "AU46 DIT", "STANDALONE-AUDIT-46");
        string reference;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PegasusDbContext>();
            var created = await context.Cases.SingleAsync(item => item.Id == caseId);
            created.Type = CaseTypeCodes.Audit;
            created.Reference = $"a.{created.Reference}";
            reference = created.Reference;
            await context.SaveChangesAsync();
        }

        using var response = await client.GetAsync($"/Cases?tab=review&selected={caseId:D}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"href=\"/Cases/{caseId:D}\">{reference}</a>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-cases-type", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// v41 U3, U7 and U8: the Principal filter sits in the list head and lists
    /// every Principal on the Case queues, not only those on the page's rows;
    /// the footer names the page only when another page exists; and a Case's
    /// quick detail is one fact grid ending with what the row does not show.
    /// </summary>
    [Fact]
    public async Task TheListHeadHoldsThePrincipalFilterAndTheQuickDetailIsOneFactGrid()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "PA12 NEL", "V41-PANE-01");
        // A Not ready Case of another Principal: no Review row carries it.
        var notReadyId = await SeedImagesIncompleteCaseAsync(
            factory, client, "PA12 NRY", "V41-PANE-02", CaseLifecycleState.NotReady);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PegasusDbContext>();
            var organizationId = Guid.NewGuid();
            var lineageId = Guid.NewGuid();
            var principalId = Guid.NewGuid();
            var now = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO Organizations (Role, Id, Name, Version) VALUES (N'principal', {organizationId}, {"Filter test provider"}, {0L})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {now})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({principalId}, {organizationId}, {"ZZPANE"}, {lineageId}, {true}, {0L})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Cases SET PrincipalId = {principalId} WHERE Id = {notReadyId}");
        }

        using var response = await client.GetAsync($"/Cases?tab=review&selected={caseId:D}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches(
            new Regex(@"<div class=""pane-head"">\s*<h2>Review</h2>\s*<span class=""meta"">1 case</span>\s*<form method=""get"" class=""filter-bar cases-filters"""),
            html);
        Assert.Contains("<option value=\"ZZPANE\">ZZPANE</option>", html, StringComparison.Ordinal);
        Assert.Contains(
            $"<span>{Pegasus.Web.Presentation.OperatorLabels.CaseQueueFooter.PageLine(1, false)}</span>",
            html,
            StringComparison.Ordinal);
        Assert.Matches(
            new Regex(@"<dt>Vehicle</dt>.*<dt>Type</dt>.*<dt>State</dt>.*<dt>Current work</dt>.*<dt>Engineer</dt>", RegexOptions.Singleline),
            html);
    }

    /// <summary>
    /// PERF-WEB-03: in Not ready and Held a Case's Current work is its first
    /// outstanding requirement, read from its frame's completeness facts, and
    /// the Case page's aside names the same step.
    /// </summary>
    [Theory]
    [InlineData("not_ready", CaseLifecycleState.NotReady, "QD21 NRY", "QUICK-DETAIL-01")]
    [InlineData("held", CaseLifecycleState.Held, "QD21 HLD", "QUICK-DETAIL-02")]
    public async Task TheQuickDetailNamesTheAsidesFirstRequirement(
        string tab,
        CaseLifecycleState state,
        string registration,
        string claimNumber)
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await SeedImagesIncompleteCaseAsync(factory, client, registration, claimNumber, state);
        var step = Pegasus.Web.Presentation.OperatorLabels.RequirementIncomplete("Images");

        Assert.Matches(
            CurrentWork(step),
            await IntakeWebDriver.GetHtmlAsync(client, $"/Cases?tab={tab}&selected={caseId:D}"));
        Assert.Contains(
            AsideStep(step),
            await IntakeWebDriver.GetHtmlAsync(client, $"/Cases/{caseId:D}"),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// PERF-WEB-03: the requirements are evaluated under the workflow
    /// configuration, not from the raw facts: with images no longer required
    /// the same Not ready Case's step is its stage, on both surfaces.
    /// </summary>
    [Fact]
    public async Task TheQuickDetailHonoursTheConfiguredRequirements()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await SeedImagesIncompleteCaseAsync(
            factory, client, "QD21 CFG", "QUICK-DETAIL-03", CaseLifecycleState.NotReady);
        await using (var context = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            var configuration = await context.Set<WorkflowConfigurationEntity>().SingleAsync();
            configuration.RequireImages = false;
            configuration.Version++;
            await context.SaveChangesAsync();
        }
        var step = Pegasus.Web.Presentation.OperatorLabels.CaseStage(CaseLifecycleState.NotReady);

        Assert.Matches(
            CurrentWork(step),
            await IntakeWebDriver.GetHtmlAsync(client, $"/Cases?tab=not_ready&selected={caseId:D}"));
        Assert.Contains(
            AsideStep(step),
            await IntakeWebDriver.GetHtmlAsync(client, $"/Cases/{caseId:D}"),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// PERF-WEB-03: the selected Case's quick detail reads its frame, not its
    /// accepted data snapshot with every field.
    /// </summary>
    [Fact]
    public async Task TheQuickDetailReadsNoCaseDataSnapshot()
    {
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine(), commandInterceptor: counter);
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "QD21 RVW", "QUICK-DETAIL-04");

        counter.Reset();
        var html = await IntakeWebDriver.GetHtmlAsync(client, $"/Cases?tab=review&selected={caseId:D}");

        Assert.Matches(CurrentWork(Pegasus.Web.Presentation.CaseWorkspaceLabels.AssignEngineer), html);
        Assert.Equal(0, counter.CountContaining("[CaseDataSnapshots]"));
    }

    /// <summary>
    /// R155: a Not ready Audit kept without its original report names Original
    /// report missing first, as the Case page's aside lists it, ahead of its
    /// incomplete images. Only the Audit reads whether its report is filed, in
    /// one document query; an Inspection Case in the same queue reads none.
    /// </summary>
    [Fact]
    public async Task AnAuditWithoutItsOriginalReportNamesItAsTheAsideDoes()
    {
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine(), commandInterceptor: counter);
        using var client = IntakeWebDriver.CreateClient(factory);
        var inspectionId = await SeedImagesIncompleteCaseAsync(
            factory, client, "QD21 INS", "QUICK-DETAIL-05", CaseLifecycleState.NotReady);
        var auditId = await SeedImagesIncompleteCaseAsync(
            factory, client, "QD21 AUD", "QUICK-DETAIL-06", CaseLifecycleState.NotReady, audit: true);

        counter.Reset();
        var inspection = await IntakeWebDriver.GetHtmlAsync(client, $"/Cases?tab=not_ready&selected={inspectionId:D}");
        var inspectionDocumentReads = counter.CountContaining("[DocumentOccurrences]");
        counter.Reset();
        var audit = await IntakeWebDriver.GetHtmlAsync(client, $"/Cases?tab=not_ready&selected={auditId:D}");
        var auditDocumentReads = counter.CountContaining("[DocumentOccurrences]");

        Assert.Matches(CurrentWork(Pegasus.Web.Presentation.OperatorLabels.RequirementIncomplete("Images")), inspection);
        Assert.Matches(CurrentWork(Pegasus.Web.Presentation.OperatorLabels.OriginalReportMissing), audit);
        Assert.Equal(0, inspectionDocumentReads);
        Assert.Equal(1, auditDocumentReads);
        Assert.Contains(
            AsideStep(Pegasus.Web.Presentation.OperatorLabels.OriginalReportMissing),
            await IntakeWebDriver.GetHtmlAsync(client, $"/Cases/{auditId:D}"),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheSelectedEntryOfAnAuditedCaseIsReadServerSide()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var search = new RecordingSearchCases { WithAuditAndTriage = true };
        using var factory = Configure(baseFactory, search);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync($"/Search?selected={search.AuditCaseId:D}&selectedView=audit");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches("<tr[^>]*data-select-view=\"audit\"[^>]*aria-selected=\"true\"", html);
        Assert.Matches("<tr[^>]*data-select-view=\"inspection\"[^>]*aria-selected=\"false\"", html);
        var pane = html[html.IndexOf("data-preview-target", StringComparison.Ordinal)..];
        Assert.Contains("<h2><span id=\"selected-case-po\">a.QDOS3100044</span> · AB12CDE</h2>", pane, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Cases/{search.AuditCaseId:D}?view=audit\"", pane, StringComparison.Ordinal);
        Assert.Contains("name=\"selectedView\" value=\"audit\"", html, StringComparison.Ordinal);
        Assert.Contains($"selected={search.AuditCaseId:D}&amp;selectedView=audit", html, StringComparison.Ordinal);
        // One read serves both entries of the Case: the pane and each
        // entry's template carry its circumstances.
        Assert.Equal(search.AuditCaseId, Assert.Single(search.Circumstances.Reads));
        Assert.Equal(3, Regex.Count(html, "Side impact at a crossroads"));

        // A link without the discriminator reads the Case's first entry.
        using var caseOnly = await client.GetAsync($"/Search?selected={search.AuditCaseId:D}");
        var caseOnlyHtml = await caseOnly.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, caseOnly.StatusCode);
        Assert.Matches("<tr[^>]*data-select-view=\"inspection\"[^>]*aria-selected=\"true\"", caseOnlyHtml);
    }

    [Fact]
    public async Task ExactImageReferenceResultCarriesItsRecordedLifecycleState()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var search = new RecordingSearchCases { ReturnEmpty = true };
        var images = new RecordingImageIntakeQueries
        {
            State = ImageInitiatedCaseState.MergedIntoInstructionCase
        };
        using var factory = Configure(baseFactory, search, images);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync($"/Search?query={RecordingImageIntakeQueries.Reference}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // The exact-reference lookup used to drop the record's state and take
        // ImageIntakeSummary's AwaitingInstruction default, so a merged record
        // was listed as still awaiting its instruction.
        Assert.Contains("Merged into Instruction-initiated Case", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Awaiting definitive instruction", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnavailableImageQueryRendersTheFailureNoticeNotAnEmptyList()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var search = new RecordingSearchCases { ReturnEmpty = true };
        var images = new RecordingImageIntakeQueries { ThrowUnavailable = true };
        using var factory = Configure(baseFactory, search, images);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/Search?kind=images&registration=AB12CDE");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Cases are unavailable", html, StringComparison.Ordinal);
        // A failed image query answered "no vehicle images match these
        // filters", which is a claim about the estate the failed read cannot
        // make.
        Assert.DoesNotContain("No vehicle images match these filters.", html, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), html, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> Configure(
        IntakeWebApplicationFactory baseFactory,
        RecordingSearchCases search,
        RecordingImageIntakeQueries? images = null,
        ICaseEngineerChoices? engineers = null) => baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISearchCases>();
                services.AddSingleton<ISearchCases>(search);
                services.RemoveAll<IGetCaseCircumstances>();
                services.AddSingleton<IGetCaseCircumstances>(search.Circumstances);
                if (images is not null)
                {
                    services.RemoveAll<IImageIntakeQueries>();
                    services.AddSingleton<IImageIntakeQueries>(images);
                }

                if (engineers is not null)
                {
                    services.RemoveAll<ICaseEngineerChoices>();
                    services.AddSingleton(engineers);
                }
            }));

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    /// <summary>The quick detail's Current work fact reading <paramref name="step"/>.</summary>
    private static Regex CurrentWork(string step) =>
        new(@"<dt>Current work</dt>\s*<dd>" + Regex.Escape(step) + "</dd>");

    /// <summary>The Case page aside's Next action step reading <paramref name="step"/>.</summary>
    private static string AsideStep(string step) => $"<strong data-next-label>{step}</strong>";

    /// <summary>
    /// A seeded instruction Case moved by direct writes to
    /// <paramref name="state"/> (a Held Case held from Not ready) with its
    /// images incomplete; with <paramref name="audit"/>, an Audit kept with no
    /// standalone evidence and no original report.
    /// </summary>
    private static async Task<Guid> SeedImagesIncompleteCaseAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client,
        string registration,
        string claimNumber,
        CaseLifecycleState state,
        bool audit = false)
    {
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(factory, client, registration, claimNumber);
        await using var context = await (await factory.DatabaseAsync).CreateContextAsync();
        var workflow = await context.CaseWorkflows
            .Include(item => item.Case)
            .SingleAsync(item => item.CaseId == caseId);
        workflow.Case.ImagesComplete = false;
        if (audit)
        {
            workflow.Case.Type = CaseTypeCodes.Audit;
        }
        workflow.State = state.ToString();
        if (state == CaseLifecycleState.Held)
        {
            workflow.PreHoldState = nameof(CaseLifecycleState.NotReady);
            workflow.HeldAtUtc = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow();
        }
        await context.SaveChangesAsync();
        return caseId;
    }

    private static async Task<HttpResponseMessage> PostImageAttachAsync(
        HttpClient client,
        Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        return await client.PostAsync("/Cases?handler=Attach", new FormUrlEncodedContent(fields));
    }

    /// <summary>
    /// The circumstances read the Search page makes, recorded: each Case it
    /// was told about answers its text, any other Case answers none.
    /// </summary>
    private sealed class RecordingCaseCircumstances(IReadOnlyDictionary<Guid, string> answers) : IGetCaseCircumstances
    {
        public List<Guid> Reads { get; } = [];

        public bool ThrowUnavailable { get; set; }

        public Task<string?> ExecuteAsync(ActionActor actor, Guid caseId, CancellationToken cancellationToken)
        {
            Reads.Add(caseId);
            if (ThrowUnavailable)
            {
                throw new InvalidOperationException("sensitive store failure");
            }

            return Task.FromResult(answers.GetValueOrDefault(caseId));
        }
    }

    private sealed class RecordingSearchCases : ISearchCases
    {
        public RecordingSearchCases() => Circumstances = new(new Dictionary<Guid, string>
        {
            [CaseId] = "Struck from behind at a junction",
            [ClosedCaseId] = "Rear-end impact at a roundabout",
            [AuditCaseId] = "Side impact at a crossroads"
        });

        public RecordingCaseCircumstances Circumstances { get; }

        public Guid CaseId { get; } = Guid.NewGuid();

        /// <summary>A second result in a D3 terminal state, with the search projection fields.</summary>
        public Guid ClosedCaseId { get; } = Guid.NewGuid();

        /// <summary>An Inspection + Audit Case with its Audit: two Search entries of one Case.</summary>
        public Guid AuditCaseId { get; } = Guid.NewGuid();

        /// <summary>A Triage Case, listed with its own Triage state.</summary>
        public Guid TriageCaseId { get; } = Guid.NewGuid();

        public List<SearchCasesQuery> Queries { get; } = [];

        public bool ReturnEmpty { get; set; }

        /// <summary>Gives the first (Review) Case an open query.</summary>
        public bool OpenQueryOnFirst { get; set; }

        public bool ThrowUnavailable { get; set; }

        /// <summary>Answers with <see cref="AuditCaseId"/> and <see cref="TriageCaseId"/> instead of the default pair.</summary>
        public bool WithAuditAndTriage { get; set; }

        public Task<SearchCasesResult> ExecuteAsync(
            SearchCasesQuery query,
            CancellationToken cancellationToken)
        {
            Queries.Add(query);
            if (ThrowUnavailable)
            {
                throw new InvalidOperationException("sensitive store failure");
            }

            if (WithAuditAndTriage)
            {
                var received = new DateTimeOffset(2031, 5, 1, 10, 0, 0, TimeSpan.Zero);
                IReadOnlyList<CaseSearchItem> auditAndTriage =
                [
                    new(
                        AuditCaseId,
                        "QDOS3100044",
                        "a.QDOS3100044",
                        CaseType.InspectionAndAudit,
                        "QDOS",
                        CaseLifecycleState.ReportPreparation,
                        null,
                        "AB12CDE",
                        "Claimant",
                        "CLM44",
                        received,
                        "Email",
                        received),
                    new(
                        TriageCaseId,
                        "t.QDOS3100045",
                        null,
                        CaseType.Triage,
                        "QDOS",
                        CaseLifecycleState.NotReady,
                        null,
                        "TR32AGE",
                        null,
                        null,
                        received,
                        "Email",
                        received)
                    {
                        InstructionComplete = false,
                        ImagesComplete = false,
                        TriageState = Pegasus.Core.Triage.TriageState.Open
                    }
                ];
                return Task.FromResult(new SearchCasesResult(auditAndTriage, query.Page, query.PageSize, false, false));
            }

            IReadOnlyList<CaseSearchItem> items = ReturnEmpty
                ? []
                :
                [
                    new(
                        CaseId,
                        "QDOS3100042",
                        null,
                        CaseType.Inspection,
                        "QDOS",
                        CaseLifecycleState.Review,
                        query.Filters.EngineerId,
                        "AB12CDE",
                        "Claimant",
                        "CLM42",
                        new DateTimeOffset(2031, 5, 1, 10, 0, 0, TimeSpan.Zero),
                        "Email",
                        new DateTimeOffset(2031, 5, 1, 10, 0, 0, TimeSpan.Zero))
                    {
                        HasOpenQuery = OpenQueryOnFirst
                    },
                    new(
                        ClosedCaseId,
                        "QDOS3100043",
                        null,
                        CaseType.Inspection,
                        "QDOS",
                        CaseLifecycleState.CreatedInError,
                        null,
                        "AB12CDE",
                        "Claimant",
                        "CLM43",
                        new DateTimeOffset(2031, 5, 1, 10, 0, 0, TimeSpan.Zero),
                        "Email",
                        new DateTimeOffset(2031, 5, 1, 10, 0, 0, TimeSpan.Zero))
                    {
                        VehicleMake = "Ford",
                        VehicleModel = "Fiesta"
                    }
                ];
            return Task.FromResult(new SearchCasesResult(items, query.Page, query.PageSize, true, true));
        }
    }

    /// <summary>The enabled accounts Search's Engineer filter offers.</summary>
    private sealed class FixedEngineerChoices(params CaseEngineerChoice[] choices) : ICaseEngineerChoices
    {
        public Task<IReadOnlyList<CaseEngineerChoice>> GetAsync(ActionActor actor, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CaseEngineerChoice>>(choices);
    }

    /// <summary>
    /// The Image-intake reads this page makes: an exact reference lookup and a
    /// registration search. Only those two are exercised here; the rest of the
    /// port is not reached by /Search and stays unimplemented rather than
    /// answering with invented records.
    /// </summary>
    private sealed class RecordingImageIntakeQueries : IImageIntakeQueries
    {
        public const string Reference = "AB12CDE-01";

        public Guid Id { get; } = Guid.NewGuid();

        public ImageInitiatedCaseState State { get; set; } =
            ImageInitiatedCaseState.AwaitingInstruction;

        public bool ThrowUnavailable { get; set; }

        /// <summary>The active Principals Search's filter offers, in code order.</summary>
        public IReadOnlyList<string> ActivePrincipalCodes { get; set; } = [];

        public Task<IReadOnlyList<string>> ListActivePrincipalCodesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ActivePrincipalCodes);

        public Task<ImageIntakeDetail?> GetByReferenceAsync(
            string imageIntakeReference,
            CancellationToken cancellationToken)
        {
            if (ThrowUnavailable)
            {
                throw new InvalidOperationException("sensitive store failure");
            }

            return Task.FromResult<ImageIntakeDetail?>(
                string.Equals(imageIntakeReference, Reference, StringComparison.Ordinal)
                    ? new ImageIntakeDetail(
                        new ImageIntakeRecord(
                            Id,
                            new ImageIntakeOrigin(
                                Guid.NewGuid(),
                                new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, "token"),
                                "hash",
                                Guid.NewGuid()),
                            "AB12CDE",
                            Reference,
                            State),
                        new DateTimeOffset(2031, 5, 1, 10, 0, 0, TimeSpan.Zero),
                        null,
                        null)
                    : null);
        }

        public Task<IReadOnlyList<ImageIntakeSummary>> SearchByRegistrationAsync(
            string normalizedVehicleRegistration,
            CancellationToken cancellationToken)
        {
            if (ThrowUnavailable)
            {
                throw new InvalidOperationException("sensitive store failure");
            }

            return Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);
        }

        public Task<IReadOnlyList<ImageIntakeSummary>> ListAsync(
            bool? associated,
            ImageInitiatedCaseState? state,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ImageIntakeDetail?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ImageIntakeDetail?> GetByOriginReceiptAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ImageIntakeSummary>> ListForCaseAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<ImageIntakeImage>>> ListImagesAsync(
            IReadOnlyCollection<Guid> imageIntakeIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<ImageIntakeImage>>>(
                imageIntakeIds.Distinct().ToDictionary(
                    imageIntakeId => imageIntakeId,
                    _ => (IReadOnlyList<ImageIntakeImage>)[]));
    }
}
