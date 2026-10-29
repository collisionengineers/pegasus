using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Identity;
using Pegasus.Core.ReleaseNotes;
using Pegasus.Core.Support;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Pages;
using Pegasus.Web.Presentation;
using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Report a problem (FRD-12, FRD-17, ADR-0055) through the real pages, Core
/// and the EF store over the seeded LocalDB, with the outward sink replaced:
/// a report is kept, raised, and listed; a failed raise is kept as Not sent
/// and an Administrator retries it.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class ProblemReportsWebTests
{
    private const string ListPage = "/Administration/ProblemReports";

    /// <summary>The Report a problem dialog as the shell fetches it on first open.</summary>
    private const string ProblemDialog = "/Notifications?handler=ShellDialog&name=problem";

    /// <summary>The list's Page cell reads the page's name, never its route or a record's identifier (v41 A25).</summary>
    [Theory]
    [InlineData("/Cases/6a1f0f8e-3f56-4c43-9c11-2f4a8f0d7b1e?section=estimate", "Cases")]
    [InlineData("/Administration/ProblemReports", "Administration")]
    [InlineData("/", "/")]
    public void ThePageCellNamesThePageByTheFirstSegmentOfItsRoute(string route, string expected) =>
        Assert.Equal(expected, OperatorLabels.ProblemReports.PageName(route));

    [Fact]
    public async Task AReportFromTheShellIsKeptRaisedAndListed()
    {
        var sink = new RecordingSink();
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory, sink, "Engineer");

        // The page carries the empty dialog; the form arrives when it first opens.
        var page = await GetHtmlAsync(client, "/");
        Assert.Matches("<div class=\"dialog-backdrop\" data-dialog=\"problem-dialog\" data-dialog-fragment=\"[^\"]+\" hidden></div>", page);
        var home = await GetHtmlAsync(client, ProblemDialog);
        Assert.Contains("data-problem-form", home, StringComparison.Ordinal);
        var sectionKeys = Regex.Match(home, "data-problem-case-sections=\"(?<keys>[^\"]+)\"")
            .Groups["keys"].Value.Split(',');
        Assert.Contains("claim", sectionKeys);
        Assert.Contains("original-report", sectionKeys);
        Assert.DoesNotContain("unknown-section", sectionKeys);
        Assert.Contains("name=\"method\" value=\"GET\"", home, StringComparison.Ordinal);
        var reportOpener = Regex.Match(
            await GetHtmlAsync(client, "/Notifications?handler=ShellDialog&name=account"),
            "<button[^>]*data-dialog-open=\"problem-dialog\"[^>]*>.*?</button>",
            RegexOptions.Singleline).Value;
        Assert.NotEmpty(reportOpener);
        Assert.DoesNotContain("data-dialog-close", reportOpener, StringComparison.Ordinal);

        using var response = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new()
        {
            ["returnUrl"] = "/Cases",
            ["route"] = "/Cases?section=estimate&claimant=claimant-secret&mail=mail-secret",
            ["method"] = "GET",
            ["traceId"] = "00-abc-01",
            ["caseReference"] = "QDOS26001",
            ["viewport"] = "1580x1000",
            ["editing"] = "true",
            ["errors"] = "[\"2026-09-20T10:00:00Z TypeError: x is undefined\"]",
            ["description"] = "The Save button did nothing."
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Cases", response.Headers.Location!.OriginalString);

        var sent = Assert.Single(sink.Sent);
        Assert.Equal("The Save button did nothing.", sent.Description);
        Assert.Equal("/Cases?section=estimate", sent.Snapshot.Route);
        var body = ProblemReportPolicy.Body(sent);
        Assert.Contains(sent.Id.ToString("D"), body, StringComparison.Ordinal);
        Assert.Contains("The Save button did nothing.", body, StringComparison.Ordinal);
        Assert.Contains("/Cases?section=estimate", body, StringComparison.Ordinal);
        Assert.DoesNotContain("claimant-secret", body, StringComparison.Ordinal);
        Assert.DoesNotContain("mail-secret", body, StringComparison.Ordinal);
        Assert.Equal("QDOS26001", sent.Snapshot.CaseReference);
        Assert.Equal("00-abc-01", sent.Snapshot.TraceId);
        Assert.True(sent.Snapshot.Client.Editing);
        Assert.Equal("1580x1000", sent.Snapshot.Client.Viewport);
        Assert.Single(sent.Snapshot.Client.RecentErrors);
        Assert.Equal(40, sent.Snapshot.SourceSha.Length);

        var after = await GetHtmlAsync(client, "/Cases");
        Assert.Contains("Reported as #7.", after, StringComparison.Ordinal);

        using var administrator = CreateClient(factory, sink);
        var list = await GetHtmlAsync(administrator, ListPage);
        Assert.Contains("The Save button did nothing.", list, StringComparison.Ordinal);
        Assert.Contains("href=\"https://github.com/collisionengineers/pegasus/issues/7\"", list, StringComparison.Ordinal);
        Assert.Contains(">Sent<", list, StringComparison.Ordinal);
        Assert.DoesNotContain("Retry", list, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AProblemReportIncludesOnlyActionsPerformedByTheReporter()
    {
        var reporterId = Guid.NewGuid();
        var administratorId = Guid.NewGuid();
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var now = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            context.SecurityEvents.AddRange(
                new SecurityEventEntity
                {
                    Id = Guid.NewGuid(),
                    Type = "PasswordChanged",
                    SubjectId = reporterId.ToString("D"),
                    OccurredAtUtc = now.AddMinutes(-1),
                    Outcome = "Succeeded",
                    CorrelationId = "reporter-action",
                    ActorKind = nameof(ActorKind.Staff),
                    ActorSubjectId = reporterId.ToString("D")
                },
                new SecurityEventEntity
                {
                    Id = Guid.NewGuid(),
                    Type = "SecurityStampChanged",
                    SubjectId = reporterId.ToString("D"),
                    OccurredAtUtc = now.AddMinutes(-2),
                    Outcome = "Succeeded",
                    CorrelationId = "administrator-action-on-reporter",
                    ActorKind = nameof(ActorKind.Staff),
                    ActorSubjectId = administratorId.ToString("D")
                });
            await context.SaveChangesAsync();
        }

        var sink = new RecordingSink();
        using var client = CreateClient(factory, sink, "User");
        client.DefaultRequestHeaders.Add("X-Test-Subject", reporterId.ToString("D"));
        var home = await GetHtmlAsync(client, ProblemDialog);

        using var response = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new()
        {
            ["returnUrl"] = "/",
            ["description"] = "The report action list is wrong."
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var sent = Assert.Single(sink.Sent);
        Assert.Contains(sent.Snapshot.RecentActions, action => action.Operation == "PasswordChanged");
        Assert.DoesNotContain(sent.Snapshot.RecentActions, action => action.Operation == "SecurityStampChanged");
    }

    [Fact]
    public async Task RepeatedFormSubmissionKeepsOneLocalReportAndOneIssue()
    {
        var sink = new RecordingSink();
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory, sink, "User");
        var home = await GetHtmlAsync(client, ProblemDialog);
        var key = Guid.NewGuid().ToString("N");
        var fields = new Dictionary<string, string>
        {
            ["returnUrl"] = "/",
            ["description"] = "The page failed.",
            ["operationKey"] = key
        };

        using var first = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new(fields)));
        using var replay = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new(fields)));
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, replay.StatusCode);
        Assert.Single(sink.Sent);

        using var changed = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new()
        {
            ["returnUrl"] = "/",
            ["description"] = "Different details.",
            ["operationKey"] = key
        }));
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        Assert.Single(sink.Sent);
        using var administrator = CreateClient(factory, sink);
        var list = await GetHtmlAsync(administrator, ListPage);
        Assert.Single(Regex.Matches(list, "data-problem-report="));
    }

    [Fact]
    public void ErrorPostInitializationRetainsOriginatingFaultContext()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var http = new DefaultHttpContext
        {
            TraceIdentifier = "00-error-trace"
        };
        http.Request.Method = "POST";
        var feature = new ExceptionHandlerFeature
        {
            Path = "/Cases",
            Error = new InvalidOperationException("The Save request failed.",
                new InvalidOperationException("The inner cause."))
        };
        http.Features.Set<IExceptionHandlerFeature>(feature);
        http.Features.Set<IExceptionHandlerPathFeature>(feature);

        // The Error page asks Url whether the originating path is local, so the
        // model needs the url helper the framework gives it in a real request.
        var actionContext = new ActionContext(
            http, new Microsoft.AspNetCore.Routing.RouteData(), new ActionDescriptor());
        var model = new ErrorModel(cache)
        {
            PageContext = new PageContext(actionContext),
            Url = new UrlHelper(actionContext)
        };
        model.OnPost();

        var request = ProblemReportRequests.Build(
            http,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            new ApplicationBuild("0.1.0-alpha.1", new string('a', 40)),
            cache,
            new ProblemReportRequests.PostedFacts(
                "The Save request failed.",
                model.ReturnPath,
                http.Request.Method,
                model.RequestId,
                null,
                null,
                false,
                null,
                Guid.NewGuid().ToString("N")));

        Assert.Equal("POST", request.Method);
        Assert.Equal("00-error-trace", request.TraceId);
        Assert.Equal("/Cases", request.Route);
        Assert.Equal(typeof(InvalidOperationException).FullName, request.ExceptionType);
        Assert.Equal("The Save request failed.", request.ExceptionMessage);
        Assert.Contains("InvalidOperationException: The Save request failed.", request.ExceptionDetails, StringComparison.Ordinal);
        Assert.Contains("The inner cause.", request.ExceptionDetails, StringComparison.Ordinal);
    }

    /// <summary>
    /// The list pages its 201 reports at 50 across five pages, and the oldest,
    /// Not sent, is on one of them with its Retry; no page reads more than a page and one
    /// row, and none reads a snapshot (#1542, R242).
    /// </summary>
    [Fact]
    public async Task TheAdministrationListKeepsTheOldestFailedReportBeyondTwoHundredRows()
    {
        var sink = new RecordingSink { Failure = new HttpRequestException("first report failed") };
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, useIntegrationTestAuthentication: true, commandInterceptor: counter);
        using var client = CreateClient(factory, sink, "Engineer");
        var home = await GetHtmlAsync(client, ProblemDialog);

        using var first = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new()
        {
            ["returnUrl"] = "/",
            ["description"] = "Oldest retained report"
        }));
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);

        sink.Failure = null;
        for (var index = 1; index <= 200; index++)
        {
            using var response = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new()
            {
                ["returnUrl"] = "/",
                ["description"] = $"Retained report {index}"
            }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        using var administrator = CreateClient(factory, sink);
        // The fixed test clock gives every report the same time, so the order
        // falls to the id and the oldest can be on any page: it is looked for
        // on each one.
        var list = string.Empty;
        for (var page = 1; page <= 5; page++)
        {
            counter.Reset();
            var html = await GetHtmlAsync(administrator, $"{ListPage}?pageNumber={page}");
            if (html.Contains("Oldest retained report", StringComparison.Ordinal))
            {
                list = html;
            }
            Assert.Equal(page == 5 ? 1 : ListProblemReports.PageSize, Regex.Count(html, "<tr data-problem-report="));
            Assert.Equal(page > 1, html.Contains($"pageNumber={page - 1}\">Previous</a>", StringComparison.Ordinal));
            Assert.Equal(page < 5, html.Contains($"pageNumber={page + 1}\">Next</a>", StringComparison.Ordinal));
            Assert.True(
                counter.CountMentioning("[SnapshotJson]") == 0,
                $"Page {page} read a snapshot." + Environment.NewLine + counter.Describe());
            Assert.All(
                counter.Reads.Where(read => read.CommandText.Contains("FROM [ProblemReports]", StringComparison.Ordinal)),
                read => Assert.True(read.Rows <= ListProblemReports.PageSize + 1, $"A list read took {read.Rows} rows."));
        }

        var descriptionIndex = list.IndexOf("Oldest retained report", StringComparison.Ordinal);
        Assert.True(descriptionIndex >= 0);
        var reportStart = list.LastIndexOf("<tr", descriptionIndex, StringComparison.Ordinal);
        var reportEnd = list.IndexOf("</tr>", reportStart, StringComparison.Ordinal);
        var row = list[reportStart..reportEnd];
        Assert.Contains("Oldest retained report", row, StringComparison.Ordinal);
        Assert.Contains(">Not sent<", row, StringComparison.Ordinal);
        Assert.Contains(">Retry<", row, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SameOriginFallbackRouteKeepsReturnUrlButSanitisesTheCapturedSnapshot()
    {
        var sink = new RecordingSink();
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory, sink, "Engineer");
        client.DefaultRequestHeaders.Referrer = new Uri("https://localhost:7139/Search?section=estimate&claimant=claimant-secret&mail=mail-secret");

        var home = await GetHtmlAsync(client, ProblemDialog);
        using var response = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new()
        {
            ["description"] = "The filtered page failed."
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Search?section=estimate&claimant=claimant-secret&mail=mail-secret", response.Headers.Location!.OriginalString);
        var sent = Assert.Single(sink.Sent);
        Assert.Equal("/Search?section=estimate", sent.Snapshot.Route);
        var body = ProblemReportPolicy.Body(sent);
        Assert.DoesNotContain("claimant-secret", body, StringComparison.Ordinal);
        Assert.DoesNotContain("mail-secret", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcurrentRetriesClaimOneReportBeforeCallingTheSink()
    {
        var sink = new RecordingSink { Failure = new HttpRequestException("temporary failure") };
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory, sink, "User");

        var home = await GetHtmlAsync(client, ProblemDialog);
        using var created = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new()
        {
            ["returnUrl"] = "/",
            ["description"] = "Nothing loads."
        }));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        using var administrator = CreateClient(factory, sink);
        var list = await GetHtmlAsync(administrator, ListPage);
        var id = Regex.Match(list, "data-problem-report=\"(?<id>[^\"]+)\"").Groups["id"].Value;
        Assert.NotEmpty(id);

        sink.Failure = null;
        sink.BlockSuccessfulSends();
        var first = RetryAsync(administrator, list, id);
        await sink.Entered.Task;

        using var second = await RetryAsync(administrator, list, id);
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.Equal(1, sink.SuccessfulCalls);
        // A Retry that finds another still sending says nothing (R423).
        Assert.DoesNotContain("data-problem-notice", await GetHtmlAsync(administrator, ListPage), StringComparison.Ordinal);

        sink.Release.TrySetResult(true);
        using var firstResponse = await first;
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);
        Assert.Single(sink.Sent);
        list = await GetHtmlAsync(administrator, ListPage);
        Assert.Contains(">Sent<", list, StringComparison.Ordinal);
        Assert.Contains("Reported as #7.", list, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExpiredDispatchClaimRequiresReconciliationBeforeRetry()
    {
        var sink = new RecordingSink { Failure = new HttpRequestException("temporary failure") };
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory, sink, "User");

        var home = await GetHtmlAsync(client, ProblemDialog);
        using var created = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new()
        {
            ["returnUrl"] = "/",
            ["description"] = "Nothing loads."
        }));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        using var administrator = CreateClient(factory, sink);
        var list = await GetHtmlAsync(administrator, ListPage);
        var id = Guid.Parse(Regex.Match(list, "data-problem-report=\"(?<id>[^\"]+)\"").Groups["id"].Value);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IProblemReportStore>();
            var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
            Assert.NotNull(await store.TryClaimAsync(
                id,
                "abandoned",
                now - ProblemReportPolicy.DispatchClaimLease,
                ProblemReportPolicy.DispatchClaimLease,
                default));
        }

        sink.Failure = null;
        using var retried = await RetryAsync(administrator, list, id.ToString("D"));

        Assert.Equal(HttpStatusCode.Redirect, retried.StatusCode);
        Assert.Empty(sink.Sent);
        list = await GetHtmlAsync(administrator, ListPage);
        Assert.Contains($">{OperatorLabels.ProblemReports.DeliveryUnknown}<", list, StringComparison.Ordinal);
        AssertProblemNotice(list, OperatorLabels.ProblemReports.Unknown);
        // Only the row that needs reconciling prints its report ID (v41 A12).
        Assert.Contains($"<small class=\"mono\">{id:D}</small>", list, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.ProblemReports.ConfirmNoIssue}</button>", list, StringComparison.Ordinal);
        using var reconciled = await administrator.PostAsync(
            $"{ListPage}?handler=ConfirmNoIssue&id={id}", AdminForm(list));
        Assert.Equal(HttpStatusCode.Redirect, reconciled.StatusCode);
        list = await GetHtmlAsync(administrator, ListPage);
        // Not sent with the reason, beside the chip (#1578, R538).
        Assert.Matches(
            $">{OperatorLabels.ProblemReports.NotSent}</span>\\s*<span class=\"muted\">{Regex.Escape(ProblemReportPolicy.NoIssueConfirmed)}</span>",
            list);
        Assert.Contains(OperatorLabels.ProblemReports.CanBeRetried, list, StringComparison.Ordinal);
        using var sent = await RetryAsync(administrator, list, id.ToString("D"));
        Assert.Equal(HttpStatusCode.Redirect, sent.StatusCode);
        Assert.Single(sink.Sent);
    }

    [Fact]
    public async Task AFailedRaiseIsKeptAsNotSentAndAnAdministratorRetriesIt()
    {
        var sink = new RecordingSink { Failure = new HttpRequestException("GitHub refused the issue with 401 Unauthorized.") };
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory, sink, "User");

        var home = await GetHtmlAsync(client, ProblemDialog);
        using var response = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new()
        {
            ["returnUrl"] = "/",
            ["description"] = "Nothing loads."
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var after = await GetHtmlAsync(client, "/");
        Assert.Contains("The report was kept but could not be sent.", after, StringComparison.Ordinal);

        using var user = CreateClient(factory, sink, "User");
        using var forbidden = await user.GetAsync(ListPage);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var administrator = CreateClient(factory, sink);
        var list = await GetHtmlAsync(administrator, ListPage);
        Assert.Contains(">Not sent<", list, StringComparison.Ordinal);
        Assert.Contains("GitHub refused the issue with 401 Unauthorized.", list, StringComparison.Ordinal);
        var id = Regex.Match(list, "data-problem-report=\"(?<id>[^\"]+)\"").Groups["id"].Value;

        // A Retry while the raise still fails says so, and the report stays Not sent.
        using var failedRetry = await RetryAsync(administrator, list, id);
        Assert.Equal(HttpStatusCode.Redirect, failedRetry.StatusCode);
        list = await GetHtmlAsync(administrator, ListPage);
        AssertProblemNotice(list, OperatorLabels.ProblemReports.Kept);
        Assert.Contains(">Not sent<", list, StringComparison.Ordinal);
        Assert.Empty(sink.Sent);

        sink.Failure = null;
        using var retried = await RetryAsync(administrator, list, id);
        Assert.Equal(HttpStatusCode.Redirect, retried.StatusCode);
        list = await GetHtmlAsync(administrator, ListPage);
        Assert.Contains(">Sent<", list, StringComparison.Ordinal);
        Assert.Contains("Reported as #7.", list, StringComparison.Ordinal);
        Assert.DoesNotContain("data-problem-notice", list, StringComparison.Ordinal);
        Assert.Single(sink.Sent);
    }

    /// <summary>
    /// A Retry whose dispatch claim is lost while the raise fails asks for a
    /// reload in the page's own words; it is not a server error.
    /// </summary>
    [Fact]
    public async Task ARetryWhoseClaimWasLostAsksForAReload()
    {
        var sink = new RecordingSink { Failure = new HttpRequestException("temporary failure") };
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory, sink, "User");
        var home = await GetHtmlAsync(client, ProblemDialog);
        using var created = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new()
        {
            ["returnUrl"] = "/",
            ["description"] = "Nothing loads."
        }));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        using var administrator = CreateClient(factory, sink);
        var list = await GetHtmlAsync(administrator, ListPage);
        var id = Guid.Parse(Regex.Match(list, "data-problem-report=\"(?<id>[^\"]+)\"").Groups["id"].Value);
        sink.OnSend = async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await using var context = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            Assert.Equal(1, await context.Set<ProblemReportEntity>()
                .Where(item => item.Id == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.DispatchClaimToken, (string?)null)
                    .SetProperty(item => item.DispatchClaimExpiresAtUtc, (DateTimeOffset?)null)));
        };

        using var retried = await RetryAsync(administrator, list, id.ToString("D"));

        Assert.Equal(HttpStatusCode.Redirect, retried.StatusCode);
        list = await GetHtmlAsync(administrator, ListPage);
        AssertProblemNotice(list, OperatorLabels.ProblemReports.StatusChanged);
        Assert.Contains(">Not sent<", list, StringComparison.Ordinal);
    }

    /// <summary>
    /// Confirm issue takes the number alone: the sink gives the issue's
    /// address, a number that is not one is refused in the page's words, and a
    /// report that is no longer Delivery unknown asks for a reload. The row's
    /// words are the label owner's, and its forms carry no operation key.
    /// </summary>
    [Fact]
    public async Task AnUnknownReportConfirmedByIssueNumberLinksTheSinksIssue()
    {
        var sink = new RecordingSink { Failure = new ProblemReportDeliveryUnknownException("The GitHub issue POST outcome is unknown.") };
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory, sink, "User");
        var home = await GetHtmlAsync(client, ProblemDialog);
        using var created = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new()
        {
            ["returnUrl"] = "/",
            ["description"] = "Nothing loads."
        }));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        using var administrator = CreateClient(factory, sink);
        var list = await GetHtmlAsync(administrator, ListPage);
        var id = Regex.Match(list, "data-problem-report=\"(?<id>[^\"]+)\"").Groups["id"].Value;
        foreach (var heading in new[]
        {
            OperatorLabels.ProblemReports.When, OperatorLabels.ProblemReports.Who, OperatorLabels.ProblemReports.Page,
            OperatorLabels.ProblemReports.Case, OperatorLabels.ProblemReports.WhatHappened,
            OperatorLabels.ProblemReports.Status, OperatorLabels.ProblemReports.Issue
        })
        {
            Assert.Contains($"<th scope=\"col\">{heading}</th>", list, StringComparison.Ordinal);
        }

        Assert.Contains($"<span class=\"sr-only\">{OperatorLabels.ProblemReports.Actions}</span>", list, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.ProblemReports.MatchingIssueNumber}</label>", list, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.ProblemReports.ConfirmIssue}</button>", list, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.ProblemReports.ConfirmNoIssue}</button>", list, StringComparison.Ordinal);
        // R533: the unruled guidance sentence is gone; the row posts no key (#1579).
        Assert.DoesNotContain("Search the approved GitHub repository", list, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"operationKey\"", list, StringComparison.Ordinal);

        using var refused = await administrator.PostAsync(
            $"{ListPage}?handler=ConfirmIssue&id={id}", AdminForm(list, new() { ["issueNumber"] = "0" }));
        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        list = await GetHtmlAsync(administrator, ListPage);
        AssertProblemNotice(list, OperatorLabels.ProblemReports.IssueNumberRequired);
        Assert.Contains($">{OperatorLabels.ProblemReports.DeliveryUnknown}<", list, StringComparison.Ordinal);
        // A second tab still open on the Delivery unknown row; a Sent row offers no form.
        var unknownPage = list;

        using var confirmed = await administrator.PostAsync(
            $"{ListPage}?handler=ConfirmIssue&id={id}", AdminForm(list, new() { ["issueNumber"] = "12" }));
        Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode);
        list = await GetHtmlAsync(administrator, ListPage);
        Assert.Contains("Reported as #12.", list, StringComparison.Ordinal);
        Assert.Contains(">Sent<", list, StringComparison.Ordinal);
        Assert.Contains($"href=\"{sink.IssueFor(12).IssueUrl}\"", list, StringComparison.Ordinal);
        Assert.Empty(sink.Sent);

        using var stale = await administrator.PostAsync(
            $"{ListPage}?handler=ConfirmIssue&id={id}", AdminForm(unknownPage, new() { ["issueNumber"] = "13" }));
        Assert.Equal(HttpStatusCode.Redirect, stale.StatusCode);
        list = await GetHtmlAsync(administrator, ListPage);
        AssertProblemNotice(list, OperatorLabels.ProblemReports.StatusChanged);
        Assert.Contains($"href=\"{sink.IssueFor(12).IssueUrl}\"", list, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEmptyReportIsRefusedAndNothingIsRaised()
    {
        var sink = new RecordingSink();
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = CreateClient(factory, sink, "Engineer");
        var home = await GetHtmlAsync(client, ProblemDialog);

        using var response = await client.PostAsync("/ProblemReports?handler=Report", Form(home, new()
        {
            ["returnUrl"] = "/",
            ["description"] = "   "
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(sink.Sent);
        // The refusal shows its reason and no internal parameter name (#1581).
        var after = await GetHtmlAsync(client, "/");
        AssertProblemNotice(after, $"A problem report says what happened, in 1 to {ProblemReportPolicy.MaximumDescriptionLength} characters.");
        Assert.DoesNotContain("(Parameter", after, StringComparison.Ordinal);
        using var administrator = CreateClient(factory, sink);
        Assert.Contains("No problem reports", await GetHtmlAsync(administrator, ListPage), StringComparison.Ordinal);
    }

    private static HttpClient CreateClient(IntakeWebApplicationFactory factory, RecordingSink sink, string? role = null)
    {
        var client = factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProblemReportSink>();
                services.AddSingleton<IProblemReportSink>(sink);
            }))
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost:7139")
            });
        if (role is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        }

        return client;
    }

    /// <summary>The staff Send's form: the antiforgery token and the form's operation key.</summary>
    private static FormUrlEncodedContent Form(string page, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = AntiforgeryToken(page);
        fields.TryAdd("operationKey", Guid.NewGuid().ToString("N"));
        return new FormUrlEncodedContent(fields);
    }

    /// <summary>An Administrator's row action: the antiforgery token and no key; the dispatch claim is the replay guard (#1579).</summary>
    private static FormUrlEncodedContent AdminForm(string page, Dictionary<string, string>? fields = null)
    {
        fields ??= [];
        fields["__RequestVerificationToken"] = AntiforgeryToken(page);
        return new FormUrlEncodedContent(fields);
    }

    private static string AntiforgeryToken(string page)
    {
        var token = Regex.Match(page, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase);
        Assert.True(token.Success, "The page carries no antiforgery token.");
        return WebUtility.HtmlDecode(
            Regex.Match(token.Value, "value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase).Groups["value"].Value);
    }

    private static Task<HttpResponseMessage> RetryAsync(HttpClient client, string page, string id) =>
        client.PostAsync($"{ListPage}?handler=Retry&id={id}", AdminForm(page));

    /// <summary>The layout's warning notice (data-problem-notice) carries exactly these words.</summary>
    private static void AssertProblemNotice(string page, string words)
    {
        var notice = Regex.Match(page, "<div[^>]*data-problem-notice[^>]*>[\\s\\S]*?</div>");
        Assert.True(notice.Success, "The page shows no problem notice.");
        Assert.Contains($"<span>{words}</span>", notice.Value, StringComparison.Ordinal);
    }

    private sealed class RecordingSink : IProblemReportSink
    {
        public List<ProblemReport> Sent { get; } = [];
        public Exception? Failure { get; set; }

        /// <summary>Runs before the raise, as another dispatcher would.</summary>
        public Func<Task>? OnSend { get; set; }

        public TaskCompletionSource<bool> Entered { get; private set; } = NewSignal();
        public TaskCompletionSource<bool> Release { get; private set; } = NewSignal();
        public bool Block { get; private set; }
        public int SuccessfulCalls;

        public void BlockSuccessfulSends()
        {
            Block = true;
            Entered = NewSignal();
            Release = NewSignal();
        }

        public async Task<ProblemReportDelivery> SendAsync(ProblemReport report, CancellationToken cancellationToken)
        {
            if (OnSend is { } onSend)
            {
                await onSend();
            }

            if (Failure is not null)
            {
                throw Failure;
            }

            Interlocked.Increment(ref SuccessfulCalls);
            if (Block)
            {
                Entered.TrySetResult(true);
                await Release.Task.WaitAsync(cancellationToken);
            }

            Sent.Add(report);
            return IssueFor(7);
        }

        public ProblemReportDelivery IssueFor(int issueNumber) =>
            new(issueNumber, $"https://github.com/collisionengineers/pegasus/issues/{issueNumber}");

        private static TaskCompletionSource<bool> NewSignal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
