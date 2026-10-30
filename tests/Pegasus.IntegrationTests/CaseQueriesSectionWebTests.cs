using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.AiWork;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

using static Pegasus.IntegrationTests.CaseWebTestSupport;
using QueryLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.Queries;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Case's Queries section and the Correspondence tab's Open query
/// (operator, 27 October 2026): every query raised on the Case, open ones
/// first, a mail query through its subject, a staff-logged query through
/// its channel and details, and the one sentence when none is open.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseQueriesSectionWebTests
{
    private const string Reference = "QDOS3100042";

    private static readonly DateTimeOffset RaisedAtUtc = new(2031, 5, 6, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// With no open query the section says so in the operator's one sentence
    /// and draws no table, whether the page addresses it or the frame fetches
    /// it as a fragment.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ACaseWithNoQueryReadsTheEmptySentence(bool fragment)
    {
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.ReportSent };

        var body = QueriesBody(await ReadQueriesAsync(store, fragment));

        Assert.Contains($"<p class=\"muted\" data-queries-empty>{QueryLabels.Empty}</p>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("data-queries-table", body, StringComparison.Ordinal);
        Assert.DoesNotContain("data-case-query=", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolved queries stay listed after the last open one closes, under the
    /// same sentence: no query is active.
    /// </summary>
    [Fact]
    public async Task ResolvedQueriesAreListedUnderTheEmptySentence()
    {
        var resolved = StaffQuery(CaseQueryChannel.WhatsApp, "Claimant asked for a copy", resolved: true);
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.ReportSent, Queries = [resolved] };

        var body = QueriesBody(await ReadQueriesAsync(store, fragment: false));

        Assert.Contains($"<p class=\"muted\" data-queries-empty>{QueryLabels.Empty}</p>", body, StringComparison.Ordinal);
        var row = Assert.Single(Rows(body));
        Assert.Equal(resolved.Id.ToString("D"), row.Id);
        Assert.False(row.Open);
        Assert.Contains($">{QueryLabels.Done}</span>", row.Markup, StringComparison.Ordinal);
        Assert.Contains("Pat Resolver", row.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Open queries come first, then the resolved ones, whatever order the
    /// store answers in. A mail query shows its sender and subject as the
    /// message link and, while open and staff mail is available, Reply; a
    /// staff query shows its channel and details.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenQueriesComeFirstAndEachShowsItsSource(bool fragment)
    {
        var messageId = Guid.NewGuid();
        var openMail = MailQuery(messageId);
        var openStaff = StaffQuery(CaseQueryChannel.Phone, "Repairer asked for the labour rate", resolved: false);
        var resolved = StaffQuery(CaseQueryChannel.WhatsApp, "Claimant asked for a copy", resolved: true);
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportSent,
            HasOpenQuery = true,
            Queries = [resolved, openStaff, openMail]
        };

        var html = await ReadQueriesAsync(store, fragment, staffMail: true);
        var body = QueriesBody(html);

        Assert.DoesNotContain("data-queries-empty", body, StringComparison.Ordinal);
        Assert.Contains("data-queries-table", body, StringComparison.Ordinal);
        var rows = Rows(body);
        Assert.Equal(3, rows.Length);
        Assert.Equal([true, true, false], rows.Select(row => row.Open).ToArray());
        Assert.Equal(resolved.Id.ToString("D"), rows[2].Id);
        Assert.Equal(
            new[] { openStaff.Id.ToString("D"), openMail.Id.ToString("D") }.Order(),
            rows.Take(2).Select(row => row.Id).Order());

        var mail = rows.Single(row => row.Id == openMail.Id.ToString("D"));
        Assert.Contains($">{QueryLabels.Open}</span>", mail.Markup, StringComparison.Ordinal);
        Assert.Contains("handler@principal.example", mail.Markup, StringComparison.Ordinal);
        Assert.Contains("When is the supplementary due?", mail.Markup, StringComparison.Ordinal);
        Assert.Contains($">{QueryLabels.Channel(CaseQueryChannel.Email)}<", mail.Markup, StringComparison.Ordinal);
        Assert.Contains($"data-dialog-open=\"case-message-{messageId:N}\"", mail.Markup, StringComparison.Ordinal);
        Assert.Contains("query-subject-link", mail.Markup, StringComparison.Ordinal);
        var reply = Regex.Match(mail.Markup, "<a[^>]*data-query-reply=\"[^\"]+\"[^>]*>");
        Assert.True(reply.Success, "An open mail query offers no Reply.");
        Assert.Contains($"href=\"/Inbox?open={messageId:D}", reply.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("compose=reply", reply.Value, StringComparison.Ordinal);
        Assert.Contains($"correspondenceCaseReference={Reference}", reply.Value, StringComparison.Ordinal);
        Assert.Contains($"data-dialog=\"case-message-{messageId:N}\" data-case-message-dialog", html, StringComparison.Ordinal);

        var staff = rows.Single(row => row.Id == openStaff.Id.ToString("D"));
        Assert.Contains($">{QueryLabels.Open}</span>", staff.Markup, StringComparison.Ordinal);
        Assert.Contains($">{QueryLabels.Channel(CaseQueryChannel.Phone)}<", staff.Markup, StringComparison.Ordinal);
        Assert.Contains("Repairer asked for the labour rate", staff.Markup, StringComparison.Ordinal);
        Assert.Contains("Sam Staff", staff.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("data-query-reply", staff.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dialog-open=\"case-message-", staff.Markup, StringComparison.Ordinal);
        Assert.Contains("data-dialog-open=\"case-assign-query-dialog\"", staff.Markup, StringComparison.Ordinal);

        Assert.Contains($">{QueryLabels.Done}</span>", rows[2].Markup, StringComparison.Ordinal);
        Assert.Contains($">{QueryLabels.Channel(CaseQueryChannel.WhatsApp)}<", rows[2].Markup, StringComparison.Ordinal);
        Assert.Contains("Claimant asked for a copy", rows[2].Markup, StringComparison.Ordinal);
    }

    /// <summary>Without staff mail an open mail query's subject still opens the message, and the row offers no Reply.</summary>
    [Fact]
    public async Task WithoutStaffMailAMailQueryOpensItsMessageAndOffersNoReply()
    {
        var messageId = Guid.NewGuid();
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportSent,
            HasOpenQuery = true,
            Queries = [MailQuery(messageId)]
        };

        var row = Assert.Single(Rows(QueriesBody(await ReadQueriesAsync(store, fragment: false))));

        Assert.Contains($"data-dialog-open=\"case-message-{messageId:N}\"", row.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("data-query-reply", row.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Row actions are Reply and Mark done on an open mail query, Mark done
    /// on an open logged query, and Reopen on a Done query (operator, 27
    /// October 2026).
    /// </summary>
    [Theory]
    [InlineData("open-mail", true, false)]
    [InlineData("open-logged", false, false)]
    [InlineData("done", false, true)]
    public async Task AQueryRowOffersReplyAndMarkDoneOrReopen(string name, bool mail, bool done)
    {
        Assert.NotEmpty(name);
        var messageId = Guid.NewGuid();
        var query = mail
            ? MailQuery(messageId)
            : StaffQuery(CaseQueryChannel.Phone, "Repairer asked for the labour rate", done);
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportSent,
            HasOpenQuery = !done,
            Queries = [query]
        };

        var row = Assert.Single(Rows(QueriesBody(await ReadQueriesAsync(store, fragment: false, staffMail: true))));

        Assert.Equal(mail && !done, row.Markup.Contains("data-query-reply", StringComparison.Ordinal));
        Assert.Equal(!done, row.Markup.Contains("data-mark-query-done", StringComparison.Ordinal));
        Assert.Equal(done, row.Markup.Contains("data-reopen-query", StringComparison.Ordinal));
        if (!done)
        {
            Assert.Contains($">{QueryLabels.MarkDone}</button>", row.Markup, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains($">{QueryLabels.Reopen}</button>", row.Markup, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Open query is offered on a received row that holds no query, once the
    /// report is sent, and never on a Sent row. It posts the Case's own
    /// Open query handler for that message.
    /// </summary>
    [Fact]
    public async Task TheCorrespondenceTabOpensAQueryOnAReceivedMessageThatHoldsNone()
    {
        var receivedAtUtc = new DateTimeOffset(2031, 5, 6, 9, 15, 0, TimeSpan.Zero);
        var generalId = Guid.NewGuid();
        var postReportId = Guid.NewGuid();
        var sentId = Guid.NewGuid();
        var undecidedId = Guid.NewGuid();
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportSent,
            CorrespondenceEmails =
            [
                new(generalId, receivedAtUtc, "handler@principal.example", null, null, "Quick question",
                    MailCategory.Cancellation) { ClassificationVersion = 4 },
                new(postReportId, receivedAtUtc.AddMinutes(-1), "handler@principal.example", null, null, "Repair query",
                    MailCategory.Cancellation) { ClassificationVersion = 5 },
                new(sentId, receivedAtUtc.AddMinutes(-2), "desk@collision.example", null, null, "Your report",
                    null, IsSent: true) { ClassificationVersion = 6 },
                new(undecidedId, receivedAtUtc.AddMinutes(-3), "handler@principal.example", null, null, "Not yet classified",
                    null)
            ]
        };

        var html = WebUtility.HtmlDecode(await ReadCaseAsync(store, section: "files"));
        var correspondence = Correspondence(html);

        Assert.Equal(3, Regex.Count(correspondence, "data-open-query="));
        Assert.DoesNotContain($"data-open-query=\"{sentId:D}\"", correspondence, StringComparison.Ordinal);
        Assert.DoesNotContain("data-tag-as-query=", correspondence, StringComparison.Ordinal);
        var form = Regex.Match(
            correspondence,
            $"<form[^>]*data-open-query=\"{generalId:D}\"[^>]*>.*?</form>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(form.Success, "The received message offers no Open query.");
        Assert.Contains("handler=OpenQuery", form.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"retainedMessageId\" value=\"{generalId:D}\"", form.Value, StringComparison.Ordinal);
        Assert.Contains($">{CaseWorkspaceLabels.Frame.OpenQuery}</button>", form.Value, StringComparison.Ordinal);
    }

    private static CaseQueryItem MailQuery(Guid messageId) => new(
        Guid.NewGuid(),
        CaseQuerySource.Mail,
        null,
        null,
        new(messageId, RaisedAtUtc, "handler@principal.example", "Handler", "handler@principal.example",
            "When is the supplementary due?",
            MailCategory.Cancellation),
        RaisedAtUtc.AddHours(1),
        "SystemWorker",
        "intake",
        null,
        null,
        null,
        null);

    private static CaseQueryItem StaffQuery(CaseQueryChannel channel, string details, bool resolved) => new(
        Guid.NewGuid(),
        CaseQuerySource.Staff,
        channel,
        details,
        null,
        RaisedAtUtc.AddHours(resolved ? 3 : 2),
        "Staff",
        "Sam Staff",
        resolved ? CaseQueryClosure.Resolved : null,
        resolved ? RaisedAtUtc.AddHours(4) : null,
        resolved ? "Staff" : null,
        resolved ? "Pat Resolver" : null);

    /// <summary>The Case read with the Queries section addressed, as the page or as the frame's fragment.</summary>
    private static async Task<string> ReadQueriesAsync(
        RecordingCaseDetailsStore store,
        bool fragment,
        bool staffMail = false,
        IReadOnlyList<AiJobRecord>? jobs = null,
        bool sendToAi = true)
    {
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCaseEditBasis>(services, store);
                SubstituteDetailsPageReaders(services, store);
                Substitute<IAiJobQueries>(services, new FixedAiJobs(jobs ?? []));
                Substitute<ISendToAiControl>(services, new FixedSendToAi(sendToAi));
                if (staffMail)
                {
                    Substitute<IStaffMailSend>(services, new OfferedStaffMailSend());
                }
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        var path = fragment
            ? $"/Cases/{store.CaseId:D}/Section?section=queries"
            : $"/Cases/{store.CaseId:D}?section=queries";
        return WebUtility.HtmlDecode(await GetHtmlAsync(client, path));
    }

    /// <summary>The Queries body, up to the message dialogs drawn after its table.</summary>
    private static string QueriesBody(string html)
    {
        var start = html.IndexOf("data-case-queries>", StringComparison.Ordinal);
        Assert.True(start >= 0, "The Queries section is not rendered.");
        var end = html.IndexOf("data-case-message-dialog", start, StringComparison.Ordinal);
        return end < 0 ? html[start..] : html[start..end];
    }

    private static (string Id, bool Open, string Markup)[] Rows(string body) =>
    [
        .. Regex.Matches(
                body,
                "<tr data-case-query=\"(?<id>[^\"]+)\" data-case-query-open=\"(?<open>true|false)\">.*?</tr>",
                RegexOptions.Singleline | RegexOptions.CultureInvariant)
            .Select(match => (match.Groups["id"].Value, match.Groups["open"].Value == "true", match.Value))
    ];

    /// <summary>The Files section's Correspondence table.</summary>
    private static string Correspondence(string html)
    {
        var start = html.IndexOf("data-correspondence>", StringComparison.Ordinal);
        Assert.True(start >= 0, "The Correspondence tab is not rendered.");
        var end = html.IndexOf("</table>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The Correspondence table is not rendered.");
        return html[start..end];
    }

    /// <summary>The Case's AI jobs as the page reads them; nothing else is asked of the ledger.</summary>
    private sealed class FixedAiJobs(IReadOnlyList<AiJobRecord> jobs) : IAiJobQueries
    {
        public Task<IReadOnlyList<AiJobRecord>> ListOpenForSubjectAsync(Guid subjectId, CancellationToken cancellationToken) =>
            Task.FromResult(jobs);

        public Task<IReadOnlyList<AiJobRecord>> ListOpenAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<AiJobQueryPage> ListOpenPageAsync(
            AiJobKind? kind, string grantId, DateTimeOffset? afterCreatedAtUtc, Guid? afterJobId, int limit,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<AiJobRecord?> GetPendingMarketResearchAsync(Guid subjectId, CancellationToken cancellationToken) =>
            Task.FromResult<AiJobRecord?>(null);

        public Task<IReadOnlyList<AiJobRecord>> ListRecentAsync(int max, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<AiJobCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");
    }

    private sealed class FixedSendToAi(bool enabled) : ISendToAiControl
    {
        public Task<bool> IsEnabledAsync(CancellationToken cancellationToken) => Task.FromResult(enabled);

        public Task<bool> SetEnabledAsync(
            bool value, ActionActor actor, string? reason, string operationKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");
    }
}
