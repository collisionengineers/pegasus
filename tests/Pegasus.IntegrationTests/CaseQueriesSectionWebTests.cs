using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.AiWork;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Notifications;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

using static Pegasus.IntegrationTests.CaseWebTestSupport;
using QueryLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.Queries;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Case's Queries section and the Correspondence tab's Tag as query
/// (operator, 11 October 2026): every query raised on the Case, open ones
/// first, a mail query through its message and a staff-logged query through
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
        Assert.Contains($">{QueryLabels.Resolved}</span>", row.Markup, StringComparison.Ordinal);
        Assert.Contains("Pat Resolver", row.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Open queries come first, then the resolved ones, whatever order the
    /// store answers in. A mail query shows its sender and subject, Open
    /// message and, while open and staff mail is available, Reply; a staff
    /// query shows its channel and details.
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
        Assert.Contains($">{OperatorLabels.CaseWorkspace.OpenMessage}</a>", mail.Markup, StringComparison.Ordinal);
        var reply = Regex.Match(mail.Markup, "<a[^>]*data-query-reply=\"[^\"]+\"[^>]*>");
        Assert.True(reply.Success, "An open mail query offers no Reply.");
        Assert.Contains($"href=\"/Inbox/{messageId:D}?", reply.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("compose=reply", reply.Value, StringComparison.Ordinal);
        Assert.Contains($"correspondenceCaseReference={Reference}", reply.Value, StringComparison.Ordinal);
        Assert.Contains($"data-dialog=\"case-message-{messageId:N}\" data-case-message-dialog", html, StringComparison.Ordinal);

        var staff = rows.Single(row => row.Id == openStaff.Id.ToString("D"));
        Assert.Contains($">{QueryLabels.Open}</span>", staff.Markup, StringComparison.Ordinal);
        Assert.Contains($">{QueryLabels.Channel(CaseQueryChannel.Phone)}<", staff.Markup, StringComparison.Ordinal);
        Assert.Contains("Repairer asked for the labour rate", staff.Markup, StringComparison.Ordinal);
        Assert.Contains("Sam Staff", staff.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("data-query-reply", staff.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dialog-open", staff.Markup, StringComparison.Ordinal);

        Assert.Contains($">{QueryLabels.Resolved}</span>", rows[2].Markup, StringComparison.Ordinal);
        Assert.Contains($">{QueryLabels.Channel(CaseQueryChannel.WhatsApp)}<", rows[2].Markup, StringComparison.Ordinal);
        Assert.Contains("Claimant asked for a copy", rows[2].Markup, StringComparison.Ordinal);
    }

    /// <summary>Without staff mail an open mail query is opened, not replied to, from the section.</summary>
    [Fact]
    public async Task WithoutStaffMailAMailQueryOffersOpenMessageAlone()
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
    /// An open mail query carries its AI draft (FRD-27): with no live job the
    /// row offers Draft reply with AI beside Reply, posting the Case's own
    /// handler for that message; while the job is Queued or Taken the row
    /// reads the pending state instead; once Draft ready, Reply with AI draft
    /// replaces Reply and opens the message's composer on the job.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(AiJobState.Queued)]
    [InlineData(AiJobState.Taken)]
    [InlineData(AiJobState.DraftReady)]
    public async Task AnOpenMailQueryOffersAnAiDraftAndReadsItsJobsState(AiJobState? jobState)
    {
        var messageId = Guid.NewGuid();
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportSent,
            HasOpenQuery = true,
            Queries = [MailQuery(messageId)]
        };
        var job = jobState is { } state ? Job(store.CaseId, messageId, state) : null;

        var html = await ReadQueriesAsync(store, fragment: false, staffMail: true, jobs: job is null ? [] : [job]);
        var row = Assert.Single(Rows(QueriesBody(html)));

        var replies = Regex.Matches(row.Markup, "<a[^>]*data-query-reply=\"[^\"]+\"[^>]*>");
        var reply = Assert.Single(replies);
        var startForm = Regex.Match(
            row.Markup,
            $"<form[^>]*data-query-ai-draft-start=\"{messageId:D}\"[^>]*>.*?</form>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        if (job is null)
        {
            Assert.DoesNotContain("aiJob=", reply.Value, StringComparison.Ordinal);
            Assert.True(startForm.Success, "The row offers no Draft reply with AI.");
            Assert.Contains($"action=\"/Cases/{store.CaseId:D}?handler=DraftQueryReply\"", startForm.Value, StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"name=\"messageId\" value=\"{messageId:D}\"", startForm.Value, StringComparison.Ordinal);
            Assert.Contains($">{OperatorLabels.QueryResponseJobs.Create}</span>", startForm.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("data-query-ai-pending", row.Markup, StringComparison.Ordinal);
        }
        else if (job.State == AiJobState.DraftReady)
        {
            Assert.Contains($"data-query-ai-draft=\"{job.JobId:D}\"", reply.Value, StringComparison.Ordinal);
            Assert.Contains(
                $"href=\"{StaffNotificationPolicy.MessageReplyRoute(messageId, Reference, job.JobId)}\"",
                reply.Value,
                StringComparison.Ordinal);
            Assert.Contains($">{OperatorLabels.QueryResponseJobs.ReplyWithDraft}</span>", row.Markup, StringComparison.Ordinal);
            Assert.False(startForm.Success);
            Assert.DoesNotContain("data-query-ai-pending", row.Markup, StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain("aiJob=", reply.Value, StringComparison.Ordinal);
            Assert.False(startForm.Success);
            Assert.Contains(
                $"data-query-ai-pending=\"{job.JobId:D}\">{OperatorLabels.QueryResponseJobs.Pending(job.State)}</span>",
                row.Markup,
                StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Draft reply with AI is withheld with its reason, never silently
    /// disabled: while the Administrator has stopped AI work, and on a Case
    /// whose query stayed open after it left post-report work.
    /// </summary>
    [Theory]
    [InlineData(CaseLifecycleState.ReportSent, false, OperatorLabels.QueryResponseJobs.AutomationStopped)]
    [InlineData(CaseLifecycleState.ReportPreparation, true, OperatorLabels.QueryResponseJobs.AvailableInPostReportWork)]
    public async Task DraftReplyWithAiIsGatedWithItsReason(CaseLifecycleState state, bool sendToAi, string condition)
    {
        var messageId = Guid.NewGuid();
        var store = new RecordingCaseDetailsStore
        {
            State = state,
            HasOpenQuery = true,
            Queries = [MailQuery(messageId)]
        };

        var row = Assert.Single(Rows(QueriesBody(await ReadQueriesAsync(store, fragment: true, sendToAi: sendToAi))));

        Assert.DoesNotContain("data-query-ai-draft-start", row.Markup, StringComparison.Ordinal);
        var gated = Regex.Match(row.Markup, "<span class=\"gated\" data-condition=\"(?<condition>[^\"]+)\">", RegexOptions.CultureInvariant);
        Assert.True(gated.Success, "The withheld action carries no reason.");
        Assert.Equal(condition, gated.Groups["condition"].Value);
        Assert.Contains($">{OperatorLabels.QueryResponseJobs.Create}</span>", row.Markup, StringComparison.Ordinal);
    }

    private static AiJobRecord Job(Guid caseId, Guid messageId, AiJobState state)
    {
        // The page reads the job against the host's clock, which the test host fixes.
        var now = IntakeWebApplicationFactory.FixedUtcNow;
        var ready = state == AiJobState.DraftReady;
        return new(
            Guid.NewGuid(), AiJobKind.QueryResponse, AiJobSubjectKind.Case, caseId, Reference,
            messageId.ToString("D"), null, null, state, ActorKind.Staff, Guid.NewGuid().ToString("D"),
            now.AddMinutes(-10), now + AiJobPolicy.DefaultExpiry,
            state == AiJobState.Queued ? null : "draft-client",
            state == AiJobState.Queued ? null : now.AddMinutes(-5),
            state == AiJobState.Queued ? null : now + AiJobPolicy.LeaseDuration,
            null,
            ready ? AiJobResultKind.DraftReply : null,
            ready ? messageId.ToString("D") : null,
            ready ? "Dear Ms Patel, thank you for your e-mail." : null,
            null, null, 2)
        {
            DraftReadyAtUtc = ready ? now.AddMinutes(-1) : null
        };
    }

    /// <summary>
    /// Tag as query is offered on a received row that carries a
    /// classification version and is not already post-report, never on a
    /// post-report, Sent or undecided row. It posts the Inbox record's own
    /// correction to the post-report query, with the version it read, the
    /// recorded reason, and the Case to come back to.
    /// </summary>
    [Fact]
    public async Task TheCorrespondenceTabTagsAReceivedNonPostReportMessageAsAQuery()
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
                    MailCategory.General) { ClassificationVersion = 4 },
                new(postReportId, receivedAtUtc.AddMinutes(-1), "handler@principal.example", null, null, "Repair query",
                    MailCategory.Query) { ClassificationVersion = 5 },
                new(sentId, receivedAtUtc.AddMinutes(-2), "desk@collision.example", null, null, "Your report",
                    null, IsSent: true) { ClassificationVersion = 6 },
                new(undecidedId, receivedAtUtc.AddMinutes(-3), "handler@principal.example", null, null, "Not yet classified",
                    null)
            ]
        };

        var html = WebUtility.HtmlDecode(await ReadCaseAsync(store, section: "files"));
        var correspondence = Correspondence(html);

        Assert.Equal(1, Regex.Count(correspondence, "data-tag-as-query="));
        foreach (var untagged in new[] { postReportId, sentId, undecidedId })
        {
            Assert.DoesNotContain($"data-tag-as-query=\"{untagged:D}\"", correspondence, StringComparison.Ordinal);
        }
        var form = Regex.Match(
            correspondence,
            $"<form[^>]*data-tag-as-query=\"{generalId:D}\"[^>]*>.*?</form>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(form.Success, "The received message offers no Tag as query.");
        Assert.Contains($"action=\"/Inbox/{generalId:D}?handler=CorrectClassification\"", form.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"ClassificationKey\" value=\"{MailClassificationSelection.QueryKey}\"", form.Value, StringComparison.Ordinal);
        Assert.Contains("name=\"ExpectedClassificationVersion\" value=\"4\"", form.Value, StringComparison.Ordinal);
        Assert.Contains($"name=\"CorrectionReason\" value=\"{MailClassificationSelection.TaggedAsQueryReason}\"", form.Value, StringComparison.Ordinal);
        Assert.Contains($"name=\"ReturnCaseId\" value=\"{store.CaseId:D}\"", form.Value, StringComparison.Ordinal);
        Assert.Contains($">{CaseWorkspaceLabels.Frame.TagAsQuery}</button>", form.Value, StringComparison.Ordinal);
    }

    private static CaseQueryItem MailQuery(Guid messageId) => new(
        Guid.NewGuid(),
        CaseQuerySource.Mail,
        null,
        null,
        new(messageId, RaisedAtUtc, "handler@principal.example", "Handler", "handler@principal.example",
            "When is the supplementary due?",
            MailCategory.Query),
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
        using var baseFactory = new IntakeWebApplicationFactory();
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
        public Task<IReadOnlyList<AiJobRecord>> ListForSubjectAsync(Guid subjectId, CancellationToken cancellationToken) =>
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
