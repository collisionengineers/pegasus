using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// FRD-27 § AI Job List: the Repair Spec section's Send to AI
/// queues an Estimate-kind AI job through <see cref="ICreateAiJob"/>. The
/// switch-off gate stays visible as the control's condition, and the
/// handler surfaces Core's refusal sentences unchanged. The ledger's own
/// state machine is owned by the AI job ledger Core tests.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class SendToAiIntegrationTests
{
    /// <summary>
    /// v26: a switched-off control is absent, never disabled — no dialog, no
    /// opener, no tooltip. The Estimate section states one availability
    /// sentence, and the only Send to AI condition it names is the missing
    /// Engineer's Value; an Administrator's switch leaves the head without it.
    /// </summary>
    [Fact]
    public async Task ASwitchedOffControlStatesTheConditionAndIsNotOffered()
    {
        var caseId = Guid.NewGuid();
        using var factory = Compose(caseId, controlEnabled: false);
        using var client = CreateClient(factory);

        var html = await EnterEditModeAsync(client, caseId);
        Assert.DoesNotContain("data-dialog=\"send-to-ai-dialog\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-estimate-send-to-ai", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-condition=", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Send to Claude", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// D11: the workspace's mutation gate refuses OnPostSendToClaudeAsync
    /// when CanOpen is false — the ordinary pre-ReportPreparation case,
    /// where D30 removed the page-level 404 that used to back this up, so
    /// HasAssessmentAccessAsync's CanOpen check is now the only defence.
    /// The control itself is also absent rather than a dead end.
    /// </summary>
    [Fact]
    public async Task InaccessibleCaseCannotPostSendToClaude()
    {
        var caseId = Guid.NewGuid();
        using var factory = Compose(caseId, canOpen: false);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.DoesNotContain("data-dialog=\"send-to-ai-dialog\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dialog-open=\"send-to-ai-dialog\"", html, StringComparison.Ordinal);

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=SendToClaude&section=estimate",
            Form(
                AntiforgeryValue(html),
                ("operationKey", Guid.NewGuid().ToString("N")),
                ("direction", "Draft the estimate"),
                ("targetPercent", "80")));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SendingRecordsAnEstimateJobWithTheDirectionAndTarget()
    {
        var caseId = Guid.NewGuid();
        using var factory = Compose(caseId);
        using var client = CreateClient(factory);

        // v26: Send to AI (never a vendor name) sits in the Estimate head
        // inside the edit session; its dialog carries the Direction, the
        // optional 0–80 target and the Case valuation the amount is read from.
        var html = await EnterEditModeAsync(client, caseId);
        Assert.Contains("data-dialog=\"send-to-ai-dialog\"", html, StringComparison.Ordinal);
        Assert.Contains("data-estimate-send-to-ai", html, StringComparison.Ordinal);
        Assert.Contains("data-estimate-range-base=\"9000\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"targetPercent\" min=\"0\" max=\"80\" step=\"1\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"targetPercent\" min=\"0\" max=\"80\" step=\"1\" value=", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Send to Claude", html, StringComparison.Ordinal);
        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=SendToClaude&section=estimate",
            Form(
                AntiforgeryValue(html),
                ("operationKey", InputValue(html, "operationKey")),
                ("direction", "Target the repair, not the paint."),
                ("targetPercent", "80")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var command = Assert.Single(((RecordingCreateAiJob)GetJobFactory(factory)).Commands);
        Assert.Equal(AiJobKind.Estimate, command.Kind);
        Assert.Equal(caseId, command.SubjectId);
        Assert.Equal("QDOS-2026-00042", command.SubjectReference);
        Assert.Equal("Target the repair, not the paint.", command.Instruction);
        Assert.Equal(80, command.TargetPercentOfEngineerValue);

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.Contains("Sent to AI", afterHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Sent to Claude", afterHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendingWithoutATargetLeavesItUnspecified()
    {
        var caseId = Guid.NewGuid();
        using var factory = Compose(caseId);
        using var client = CreateClient(factory);

        var html = await EnterEditModeAsync(client, caseId);
        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=SendToClaude&section=estimate",
            Form(
                AntiforgeryValue(html),
                ("operationKey", InputValue(html, "operationKey")),
                ("direction", "Draft the estimate"),
                ("targetPercent", "")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var command = Assert.Single(((RecordingCreateAiJob)GetJobFactory(factory)).Commands);
        Assert.Null(command.TargetPercentOfEngineerValue);
    }

    /// <summary>
    /// Core names the Case in the instruction when no direction is given
    /// (<c>AiJobPolicy.DefaultEstimateInstruction</c>), for staff and the
    /// Automation Actor alike; the page passes the empty direction through.
    /// </summary>
    [Fact]
    public async Task AnEmptyDirectionLeavesTheNamedInstructionToCore()
    {
        var caseId = Guid.NewGuid();
        using var factory = Compose(caseId);
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=SendToClaude&section=estimate",
            Form(
                AntiforgeryValue(html),
                ("operationKey", InputValue(html, "operationKey")),
                ("direction", "   "),
                ("targetPercent", "75")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var command = Assert.Single(((RecordingCreateAiJob)GetJobFactory(factory)).Commands);
        Assert.Equal(string.Empty, command.Instruction);
    }

    /// <summary>
    /// Core owns the refusal (no Engineer's Value, wrong state,
    /// switch off); the page surfaces the sentence it is given rather than
    /// rewriting it.
    /// </summary>
    [Fact]
    public async Task ACoreRefusalIsSurfacedUnchanged()
    {
        var caseId = Guid.NewGuid();
        using var factory = Compose(
            caseId,
            refusal: "An estimate job needs an Engineer's Value on the case.");
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=SendToClaude&section=estimate",
            Form(
                AntiforgeryValue(html),
                ("operationKey", InputValue(html, "operationKey")),
                ("direction", "Draft the estimate"),
                ("targetPercent", "80")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        // Decoded first: Razor encodes the apostrophe in "Engineer's", and
        // the claim is that Core's sentence reaches the operator unrewritten.
        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.Contains(
            "An estimate job needs an Engineer's Value on the case.",
            WebUtility.HtmlDecode(afterHtml),
            StringComparison.Ordinal);
    }

    private static ICreateAiJob GetJobFactory(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ICreateAiJob>();
    }

    private static WebApplicationFactory<Program> Compose(
        Guid caseId,
        bool controlEnabled = true,
        string? refusal = null,
        bool canOpen = true)
    {
        var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        // The page takes its access answer from the Case's state, so a Case the
        // assessment cannot open is one in a closed state.
        var source = new FakeGetCase(
            caseId, canOpen ? CaseLifecycleState.ReportPreparation : CaseLifecycleState.Held);
        var jobs = new RecordingCreateAiJob(refusal);
        return baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCaseHeader>();
                services.RemoveAll<IGetCasePageFrame>();
                services.RemoveAll<IGetCaseVehicleSection>();
                services.RemoveAll<IGetCaseValuationSection>();
                services.RemoveAll<IGetCaseNotesSection>();
                services.RemoveAll<IGetAssessmentAccess>();
                services.RemoveAll<IGetAssessmentWorkspace>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.RemoveAll<ICreateAiJob>();
                services.RemoveAll<ISendToAiControl>();
                services.AddSingleton<IGetCaseHeader>(source);
                services.AddSingleton<IGetCaseEditBasis>(source);
                services.AddSingleton<IGetCasePageFrame>(source);
                services.AddSingleton<IGetCaseVehicleSection>(source);
                services.AddSingleton<IGetCaseValuationSection>(source);
                services.AddSingleton<IGetCaseNotesSection>(source);
                services.AddSingleton<IAcquireCaseEditLease>(source);
                services.AddSingleton<IGetAssessmentAccess>(new FakeGetAssessmentAccess(canOpen));
                services.AddSingleton<IGetAssessmentWorkspace>(source);
                services.AddSingleton<ICreateAiJob>(jobs);
                services.AddSingleton<ISendToAiControl>(new FixedSendToAiControl(controlEnabled));
            }));
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

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
    /// v26: the Estimate section's controls render inside the page-wide edit
    /// session, entered the way the operator enters it — the ribbon's Edit
    /// Case claim, answered by the fake lease.
    /// </summary>
    private static async Task<string> EnterEditModeAsync(HttpClient client, Guid caseId)
    {
        var initial = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        using var claim = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(initial),
                ("id", caseId.ToString("D")),
                ("expectedVersion", InputValue(initial, "expectedVersion")),
                ("operationKey", InputValue(initial, "operationKey")),
                ("section", "estimate")));
        Assert.Equal(HttpStatusCode.Redirect, claim.StatusCode);
        var editing = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.Contains("data-case-editing=\"true\"", editing, StringComparison.Ordinal);
        return editing;
    }

    private static string InputValue(string html, string name)
    {
        var tag = Regex.Match(
            html,
            $"<input[^>]*name=\\\"{Regex.Escape(name)}\\\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(tag.Success, $"The page must render '{name}'.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, $"The field '{name}' must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    private static string AntiforgeryValue(string html)
    {
        var tag = AntiforgeryTagRegex().Match(html);
        Assert.True(tag.Success, "The page must render an antiforgery token.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, "The antiforgery token must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValueRegex();

    /// <summary>
    /// One recording fake for the page's job creation: it remembers the
    /// command, or throws the refusal sentence the real Core use case
    /// would, so the page's surfacing is exercised without the ledger's
    /// own state machine.
    /// </summary>
    private sealed class RecordingCreateAiJob(string? refusal) : ICreateAiJob
    {
        public List<CreateAiJobCommand> Commands { get; } = [];

        public Task<AiJobRecord> ExecuteAsync(
            CreateAiJobCommand command,
            CancellationToken cancellationToken = default)
        {
            if (refusal is not null)
            {
                throw new InvalidOperationException(refusal);
            }
            Commands.Add(command);
            return Task.FromResult(new AiJobRecord(
                JobId: Guid.NewGuid(),
                Kind: command.Kind,
                SubjectKind: AiJobSubjectKind.Case,
                SubjectId: command.SubjectId,
                SubjectReference: command.SubjectReference ?? string.Empty,
                Instruction: command.Instruction,
                TargetPercentOfEngineerValue: command.TargetPercentOfEngineerValue,
                EngineerValueAtSend: null,
                State: AiJobState.Queued,
                CreatedByKind: command.Actor.Kind,
                CreatedBy: command.Actor.SubjectId,
                CreatedAtUtc: DateTimeOffset.UtcNow,
                ExpiresAtUtc: DateTimeOffset.UtcNow.AddHours(6),
                TakenBy: null,
                TakenAtUtc: null,
                LeaseExpiresAtUtc: null,
                ProgressNote: null,
                ResultKind: null,
                ResultReference: null,
                ResultText: null,
                ClosedAtUtc: null,
                ClosureReason: null,
                Version: 1));
        }
    }

    private sealed class FixedSendToAiControl(bool enabled) : ISendToAiControl
    {
        public Task<bool> IsEnabledAsync(CancellationToken cancellationToken) =>
            Task.FromResult(enabled);

        public Task<bool> SetEnabledAsync(
            bool enabled,
            ActionActor actor,
            string? reason,
            string operationKey,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeGetCase(Guid caseId, CaseLifecycleState state) :
        IGetCaseHeader, IGetCaseEditBasis,
        IGetCasePageFrame,
        IGetCaseVehicleSection,
        IGetCaseValuationSection,
        IGetCaseNotesSection,
        IGetAssessmentWorkspace,
        IAcquireCaseEditLease
    {
        private CaseEditLeaseSnapshot? activeLease;

        /// <summary>The page's own Edit Case claim, so the section renders inside the session.</summary>
        public Task<CaseEditLease> ExecuteAsync(
            ClaimCaseEditLeaseRequest request, CancellationToken cancellationToken)
        {
            activeLease = new(
                request.Actor.SubjectId, request.Actor.Kind, DateTimeOffset.UtcNow.AddMinutes(5), request.OperationKey);
            return Task.FromResult(new CaseEditLease(
                request.CaseId, "lease-1", request.Actor.SubjectId, request.ExpectedVersion,
                DateTimeOffset.UtcNow.AddMinutes(5)));
        }

        Task<CaseEditBasis?> IGetCaseEditBasis.ExecuteAsync(
            GetCaseQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<CaseEditBasis?>(Frame(query.CaseId) is { } frame
                ? new(frame, CreateData())
                : null);

        Task<CaseHeader?> IGetCaseHeader.ExecuteAsync(
            GetCaseHeaderQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<CaseHeader?>(Frame(query.CaseId) is { } frame
                ? new(frame.Summary, frame.Workflow, frame.ActiveEditLease, 0, 0, 0)
                : null);

        /// <summary>The Case's frame as a focused read returns it; null for any other Case.</summary>
        private CaseSectionFrame? Frame(Guid requestedCaseId)
        {
            if (requestedCaseId != caseId)
            {
                return null;
            }

            var identity = new CaseIdentity(caseId, "QDOS", 2026, 42, "QDOS-2026-00042");
            var workflow = new CaseWorkflowRecord(
                caseId, identity, state, null, null,
                null, null, null, null, null, 7);
            var summary = new CaseSearchItem(
                caseId, identity.Reference, null, CaseType.Inspection, "Approved Principal",
                workflow.State, null, "AB12CDE", "Alex Example", "P-100",
                DateTimeOffset.UtcNow, "Email", DateTimeOffset.UtcNow);
            return new(summary, workflow, activeLease);
        }

        Task<CasePageFrame?> IGetCasePageFrame.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult<CasePageFrame?>(Frame(query.CaseId) is { } frame
                ? new(frame, [], [], CaseRecordNotes.None, CreateData())
                : null);

        Task<CaseVehicleSection?> IGetCaseVehicleSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult<CaseVehicleSection?>(Frame(query.CaseId) is { } frame
                ? new(
                    frame,
                    query.AssessmentWorkspace?.Data ?? query.Data ?? CreateData(),
                    null,
                    query.AssessmentWorkspace?.Assessment ?? CreateAssessment())
                : null);

        Task<CaseValuationSection?> IGetCaseValuationSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult<CaseValuationSection?>(Frame(query.CaseId) is { } frame
                ? new(
                    frame,
                    query.AssessmentWorkspace?.Data ?? query.Data ?? CreateData(),
                    query.AssessmentWorkspace?.Assessment ?? CreateAssessment())
                : null);

        Task<CaseNotesSection?> IGetCaseNotesSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult<CaseNotesSection?>(Frame(query.CaseId) is { } frame ? new(frame, []) : null);

        public Task<AssessmentWorkspace?> ExecuteAsync(
            GetAssessmentWorkspaceQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Frame(query.CaseId) is { } frame
                ? AssessmentWorkspaceTestData.Create(frame, CreateAssessment(), CreateData())
                : null);

        private CaseAssessmentProjection CreateAssessment() => new(
            caseId,
            "QDOS-2026-00042",
            7,
            state,
            null,
            [
                new(
                    AssessmentVocabulary.ValueEngineer,
                    "9000",
                    ActorKind.Staff,
                    "engineer-1",
                    DateTimeOffset.UtcNow)
            ],
            [],
            new(null, null, null, null, null, null, "tbc", null, new DateOnly(2026, 8, 1), null, null, null, null, null));

        private CaseDataProjection CreateData() => AssessmentWorkspaceTestData.Create(CreateAssessment()).Data;
    }
}
