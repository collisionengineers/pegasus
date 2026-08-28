using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class CaseDetailsWebTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task AssessmentControlReflectsTheSharedAccessDecision(
        bool canOpen,
        bool expectsLink)
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
        var actionBar = RecordBar(html);

        Assert.Contains("Open assessment", actionBar, StringComparison.Ordinal);
        Assert.Equal(expectsLink, actionBar.Contains(
            $"/Cases/{store.CaseId:D}/Assessment",
            StringComparison.OrdinalIgnoreCase));
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

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?tab=history");

        Assert.Contains("alex", html, StringComparison.Ordinal);
        Assert.Contains("Automation", html, StringComparison.Ordinal);
        Assert.DoesNotContain(staffSubjectId, html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(automationSubjectId, html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore { ExposeCustody = true };
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
        Assert.Contains($"/Cases/{store.CaseId:D}/Custody?handler=RetryCustody", html, StringComparison.Ordinal);
        Assert.Contains("name=\"expectedVersion\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"operationKey\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"editLeaseToken\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"reason\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain(store.CaseId.ToString("D"), VisibleText(html), StringComparison.OrdinalIgnoreCase);

        // ENG-016: the export must post, because it records the once-per-case
        // First sent to Engineer proxy and a prefetched or refreshed GET must
        // not be able to fire it.
        //
        // EXT-04 moved the control: the action bar now carries one Send to EVA
        // link to the page where the operator chooses between the API
        // submission and the export, and the export's form lives there. So the
        // rule is pinned on the route rather than on the bar - the export is
        // reachable by no link at all, and answers a GET with a redirect
        // rather than a package (asserted below).
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

    [Fact]
    public async Task ManualChasePostUsesAntiforgeryServerActorLiveLeaseVersionAndReplayKey()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
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

        var leasedHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        var refreshedHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Equal(
            InputValue(leasedHtml, "editLeaseToken"),
            InputValue(refreshedHtml, "editLeaseToken"));
        leasedHtml = refreshedHtml;
        Assert.Contains("Record manual chase", leasedHtml, StringComparison.Ordinal);
        var operationKey = "manual-chase-replay";
        var attemptedAtUtc = InputValue(leasedHtml, "attemptedAtUtc");
        using var firstResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}/Tasks?handler=RecordManualChase",
            ManualChaseForm(AntiforgeryValue(leasedHtml), store, operationKey, attemptedAtUtc));
        AssertPrg(firstResponse, store.CaseId);

        var currentHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Contains("Telephone", currentHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"editLeaseToken\"", currentHtml, StringComparison.Ordinal);
        Assert.Contains("Awaiting requested photographs", currentHtml, StringComparison.Ordinal);
        using var replayResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}/Tasks?handler=RecordManualChase",
            ManualChaseForm(AntiforgeryValue(currentHtml), store, operationKey, attemptedAtUtc));
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
        Assert.Equal(
            DateTimeOffset.Parse(attemptedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            command.AttemptedAtUtc);
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
        Assert.Contains("Hold case", leasedHtml, StringComparison.Ordinal);
        Assert.Contains("Release hold", leasedHtml, StringComparison.Ordinal);
        Assert.Contains("Transition to report preparation", leasedHtml, StringComparison.Ordinal);
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
        string operationKey,
        string attemptedAtUtc) => Form(
            antiforgeryToken,
            ("id", store.CaseId.ToString("D")),
            ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
            ("operationKey", operationKey),
            ("editLeaseToken", store.LeaseToken),
            ("reason", "Missing evidence follow-up"),
            ("attemptedAtUtc", attemptedAtUtc),
            ("channel", "Telephone"),
            ("targetPartyOrAddress", "Provider claims team"),
            ("outcome", "Awaiting requested photographs"),
            ("note", "Asked provider for missing images"));

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
        Assert.Contains("Editing becomes available at", note, StringComparison.Ordinal);
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
        Assert.Contains("Editing becomes available at", note, StringComparison.Ordinal);
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
        var store = new RecordingCaseDetailsStore { LeaseHolder = "pegasus-automation" };
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
        Assert.Contains("Editing becomes available at", note, StringComparison.Ordinal);
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
        var start = html.IndexOf("class=\"record__bar\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The record bar is not rendered.");
        var end = html.IndexOf("<nav", start, StringComparison.Ordinal);
        Assert.True(end > start, "The record bar is not closed before the tabs.");
        return html[start..end];
    }

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

    private sealed class StubEditAuthorityHolders(string? displayName, bool isAutomation = false)
        : IDescribeCaseEditAuthorityHolder
    {
        public Task<CaseEditAuthorityHolder> ExecuteAsync(
            string holderSubjectId,
            ActionActor actor,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CaseEditAuthorityHolder(displayName, isAutomation));
        }
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
        IAcquireCaseEditLease,
        IRecordManualCaseChase,
        IHoldCase,
        IReleaseCase,
        ITransitionCase,
        IConfirmCompleteness,
        ISaveCase
    {
        private readonly DateTimeOffset _now = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        private CaseDueWork _dueWork;
        private string? _leaseHolder;
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

        public bool ExposeCustody { get; init; }

        public IReadOnlyList<CaseHistoryEntry> HistoryEntries { get; init; } = [];

        public string LeaseToken { get; } = "opaque-live-case-lease";

        public List<ClaimCaseEditLeaseRequest> Claims { get; } = [];
        public string? LeaseHolder
        {
            get => _leaseHolder;
            set => _leaseHolder = value;
        }

        public List<SaveCaseRequest> Saves { get; } = [];
        public List<ConfirmCompletenessRequest> CompletenessConfirmations { get; } = [];
        public List<ManualChaseRecord> ManualChases { get; } = [];
        public List<PutCaseOnHoldRequest> Holds { get; } = [];
        public List<CaseMutationRequest> Releases { get; } = [];
        public List<TransitionCaseRequest> Transitions { get; } = [];

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
                "AB12CDE",
                "Case claimant",
                "CLM-42",
                _now.AddDays(-2),
                new DateOnly(2031, 5, 5),
                "Email",
                _now.AddDays(-2));
            CaseDetails details = new(
                summary,
                workflow,
                _leaseHolder is null ? null : new(_leaseHolder, _now.AddMinutes(5), _leaseOperationKey!),
                [],
                null,
                CaseCustodyState.Pending,
                [],
                [],
                HistoryEntries)
            {
                Data = CreateData(),
                Custody = ExposeCustody
                    ? [new(CaseId, CaseVersion, CustodyTargetKind.CaseSource, "Failed", "Provider storage was unavailable.", 1, true)]
                    : []
            };
            return Task.FromResult<CaseDetails?>(details);
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
                CaseLifecycleState.NotReady,
                new(
                    new(
                        InstructionComplete: true,
                        ImagesComplete: true,
                        InstructionConfirmedByStaff: false,
                        ImagesConfirmedByStaff: false),
                    new(false, "case-completeness", 1)),
                new(Confirmed("QDOS")),
                new(Confirmed("Case claimant")),
                new(Confirmed("CLM-42")),
                new(
                    Confirmed("AB12CDE"),
                    Confirmed("Ford"),
                    Confirmed("Transit"),
                    Confirmed(42_000L),
                    Confirmed("miles")),
                new(Empty<DateOnly>(), Confirmed("Rear impact")),
                new(Confirmed("Case contact"), Empty<string>(), Empty<string>()),
                new(Empty<DateOnly>(), Confirmed("Standard")),
                new(
                    Empty<DateOnly>(),
                    Empty<DateOnly>(),
                    Confirmed("1 Depot Road"),
                    Confirmed(CaseInspectionMode.PhysicalAddress)));

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
            _leaseHolder = request.Actor.SubjectId;
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
                CaseLifecycleState.NotReady,
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
