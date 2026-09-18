using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Web.Presentation;
using Pegasus.Core.Workflow;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The workspace's own edit-mode actions that stay on <c>DetailsModel</c>: renewing the lease
/// and leaving it. Claiming and recovery are covered by the workspace tests.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseEditModeWebTests
{
    [Fact]
    public async Task CaseSaveKeepsEditingAndIdentifiesOnlyTheCommittedCommand()
    {
        var store = new RecordingCaseDetailsStore { AcceptWorkspaceSaves = true };
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ISaveCaseWorkspace>(services, store));
        var before = store.CaseVersion;
        using var response = await workspace.Client.PostAsync($"/Cases/{store.CaseId:D}?handler=Save",
            Form(workspace.AntiforgeryToken,
                ("expectedVersion", before.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", DetailsModelOperationKey),
                ("editLeaseToken", store.LeaseToken),
                ("claimNumber", "CLM-42")));
        AssertPrg(response, store.CaseId);
        Assert.Single(store.Saves);
        Assert.Equal(2, store.Claims.Count);
        Assert.Equal(before + 1, store.Claims[1].ExpectedVersion);
        var after = await workspace.GetWorkspaceAsync();
        Assert.Contains("data-case-editing=\"true\"", after, StringComparison.Ordinal);
        AssertEditorCommit(after, "case-edit-form", DetailsModelOperationKey, before);
        Assert.DoesNotContain("data-editor-commit=\"{", await workspace.GetWorkspaceAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Engineer", false, true)]
    [InlineData("User", false, true)]
    [InlineData("Engineer", true, false)]
    [InlineData("User", true, false)]
    public async Task WorkspaceSaveUsesCoreFindingAndEligibleSignOffAuthority(
        string role, bool forgeSignOffAccount, bool reachesStore)
    {
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.Review };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            Substitute<IGetCase>(services, store);
            Substitute<IGetCasePageFrame>(services, store);
            Substitute<IGetCaseVehicleSection>(services, store);
            Substitute<IGetCaseValuationSection>(services, store);
            Substitute<IGetCaseNotesSection>(services, store);
            Substitute<IGetCaseFilesSection>(services, store);
            Substitute<IAcquireCaseEditLease>(services, store);
            Substitute<IGetAssessmentAccess>(services, store);
            Substitute<IGetAssessmentWorkspace>(services, store);
            Substitute<ICaseReportSnapshotSource>(services, store);
            services.RemoveAll<ISaveCaseWorkspace>();
            services.AddScoped<ISaveCaseWorkspace>(provider => new SaveCaseWorkspace(store,
                provider.GetRequiredService<IStaffAccountQueries>()));
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        var initial = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        using var claim = await client.PostAsync($"/Cases/{store.CaseId:D}?handler=ClaimLease",
            Form(AntiforgeryValue(initial), ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", InputValue(initial, "operationKey"))));
        AssertPrg(claim, store.CaseId);
        var editing = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        var outcomeName = CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.Outcome);
        Assert.True(editing.Contains($"name=\"{outcomeName}\"", StringComparison.Ordinal));
        var field = forgeSignOffAccount ? "signOffEngineerId" : outcomeName;
        var value = forgeSignOffAccount
            ? Pegasus.Web.Authentication.DevelopmentOfflineIdentity.AdministratorId.ToString("D") : "repairable";
        using var response = await client.PostAsync($"/Cases/{store.CaseId:D}?handler=Save",
            Form(AntiforgeryValue(editing), ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", DetailsModelOperationKey), ("editLeaseToken", store.LeaseToken),
                ("reason", "Correct recorded settlement"), (field, value)));
        AssertPrg(response, store.CaseId);
        Assert.Equal(reachesStore ? 1 : 0, store.Saves.Count);
        if (reachesStore)
            Assert.Equal("repairable", store.Saves[0].Settlement!.AssessmentFields![AssessmentVocabulary.Outcome]);
        else
            Assert.Contains(forgeSignOffAccount ? "Sign-off Engineer" : "Outcome",
                ProposedValuesPanel(await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CaseSavePreservesUnpostedAcceptedFactsWithoutPromotingSuggestions()
    {
        var store = new RecordingCaseDetailsStore();
        var data = (await store.ExecuteAsync(new Pegasus.Core.Cases.GetCaseQuery(store.CaseId,
            ActionActor.Staff(Pegasus.Web.Authentication.DevelopmentOfflineIdentity.AdministratorId,
                [StaffRole.Administrator])), CancellationToken.None))!.Data!;
        store.DataOverride = data with
        {
            Claim = new(new(data.Claim.Number.Confirmed! with { Kind = CaseDataValueKind.Fact },
                data.Claim.Number.Confirmed! with { Kind = CaseDataValueKind.Suggestion, Value = "CLM-99" }, null)),
            Contact = data.Contact with
            {
                Name = new(data.Contact.Name.Confirmed! with { Kind = CaseDataValueKind.Fact }, null, null)
            }
        };
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ISaveCaseWorkspace>(services, store));
        var html = await workspace.GetWorkspaceAsync();
        Assert.Equal("CLM-42", InputValue(html, "claimNumber"));
        Assert.Equal("Case contact", InputValue(html, "contactName"));
        using var response = await workspace.Client.PostAsync($"/Cases/{store.CaseId:D}?handler=Save",
            Form(workspace.AntiforgeryToken, ("expectedVersion", (store.CaseVersion - 1).ToString(CultureInfo.InvariantCulture)),
                ("operationKey", DetailsModelOperationKey), ("editLeaseToken", store.LeaseToken),
                ("reason", "Corrected claimant spelling"), ("claimantName", "")));
        AssertPrg(response, store.CaseId);
        var saved = Assert.Single(store.Saves);
        Assert.Equal(store.CaseVersion - 1, saved.ExpectedVersion);
        Assert.Null(saved.Overview!.ClaimantName);
        Assert.Equal("CLM-42", saved.Overview.ClaimNumber);
        Assert.Equal("Case contact", saved.Overview.ContactName);
        Assert.Null(saved.Inspection);
        Assert.Null(saved.Vehicle);
        Assert.Null(saved.Report);
        Assert.Null(saved.Settlement);
    }

    [Fact]
    public async Task EngineeringEditorsShareTheCaseSaveAndRetainClearsAndFalseOnConflict()
    {
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.ReportPreparation };
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<ISaveCaseWorkspace>(services, store);
            Substitute<IGetAssessmentAccess>(services, store);
            Substitute<IGetAssessmentWorkspace>(services, store);
            Substitute<ICaseReportSnapshotSource>(services, store);
        });
        var html = await workspace.GetWorkspaceAsync();
        foreach (var path in CaseWorkspaceLabels.Editors.Settlement.Keys.Concat(CaseWorkspaceLabels.Editors.Report.Keys)
                     .Append(AssessmentVocabulary.HistoryCheck).Append(AssessmentVocabulary.VehicleCondition))
        {
            var name = CaseWorkspaceLabels.Editors.FormName(path);
            // Administrator includes engineering authority; every editor uses
            // the same Case Save form, including engineering findings.
            Assert.Matches($"<(input|textarea|select)[^>]*name=\"{Regex.Escape(name)}\"[^>]*form=\"case-edit-form\"", html);
        }
        Assert.Single(Regex.Matches(html, "id=\"case-edit-form\""));
        // The history check is the Vehicle section's own sub-panel rather
        // than a row among the vehicle's facts, in edit mode as in read mode.
        Assert.Contains("data-vehicle-history", html, StringComparison.Ordinal);
        Assert.Matches("<textarea[^>]*id=\"edit-vehicle-history\"[^>]*form=\"case-edit-form\"", html);
        Assert.DoesNotContain("Saving returns the case to Not ready", html, StringComparison.Ordinal);
        Assert.True(store.MetadataReads > 0);

        var fields = new (string Name, string Value)[]
        {
            (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.SettlementExcess), "0"),
            (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.SettlementClaimantVatRegistered), "false"),
            (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.HistoryCheck), ""),
            // The Vehicle section renders this select in every engineering edit form.
            (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.VehicleCondition), "good"),
            (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.EngineersComments), "Engineer comments recorded"),
            (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.ReportDateOverride), "false"),
            (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.ReportIncludeUnrelatedDamage), "false"),
            ("storagePerDay", "14.50"), ("recoveryCharge", "0")
        };
        using var response = await workspace.Client.PostAsync($"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(DetailsModelOperationKey, "Correct recorded settlement", fields));
        AssertPrg(response, store.CaseId);
        var saved = Assert.Single(store.Saves);
        AssertLeasedMutation(workspace, saved, DetailsModelOperationKey, "Correct recorded settlement");
        Assert.Null(saved.Overview);
        Assert.Equal("0", saved.Settlement!.AssessmentFields![AssessmentVocabulary.SettlementExcess]);
        Assert.Equal("false", saved.Settlement.AssessmentFields[AssessmentVocabulary.SettlementClaimantVatRegistered]);
        Assert.Null(saved.Vehicle!.AssessmentFields![AssessmentVocabulary.HistoryCheck]);
        Assert.Equal("good", saved.Vehicle.AssessmentFields[AssessmentVocabulary.VehicleCondition]);
        Assert.Equal("Engineer comments recorded", saved.Report!.AssessmentFields![AssessmentVocabulary.EngineersComments]);
        Assert.Equal("false", saved.Report.AssessmentFields[AssessmentVocabulary.ReportDateOverride]);
        Assert.Equal(new DateOnly(2031, 5, 6), saved.Report.ReportDate);
        Assert.Equal(14.50m, saved.Inspection!.StoragePerDay);
        Assert.Equal(0m, saved.Inspection.RecoveryCharge);

        var refused = await workspace.GetWorkspaceAsync();
        var panel = ProposedValuesPanel(refused);
        Assert.Contains("Vehicle history", panel, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.CaseWorkspace.AbsentValue, panel, StringComparison.Ordinal);
        Assert.Contains("No", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("<input", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(store.LeaseToken, panel, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CaseLifecycleState.Held)]
    [InlineData(CaseLifecycleState.PostReportComplete)]
    public async Task CraftedEngineeringSaveIsRefusedOutsideTheCoreEditStates(CaseLifecycleState state)
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ISaveCaseWorkspace>(services, store));
        store.State = state;
        using var response = await workspace.Client.PostAsync($"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(DetailsModelOperationKey, "Correct recorded settlement",
                (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.SettlementExcess), "0")));
        AssertPrg(response, store.CaseId);
        Assert.Empty(store.Saves);
        Assert.Contains("Excess", ProposedValuesPanel(await workspace.GetWorkspaceAsync()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReviewStateOrdinaryEngineeringSaveByNonEngineerReachesTheStore()
    {
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.Review };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            Substitute<IGetCase>(services, store);
            Substitute<IGetCasePageFrame>(services, store);
            Substitute<IGetCaseVehicleSection>(services, store);
            Substitute<IGetCaseValuationSection>(services, store);
            Substitute<IGetCaseNotesSection>(services, store);
            Substitute<IGetCaseFilesSection>(services, store);
            Substitute<IAcquireCaseEditLease>(services, store);
            Substitute<IGetAssessmentAccess>(services, store);
            Substitute<IGetAssessmentWorkspace>(services, store);
            Substitute<ICaseReportSnapshotSource>(services, store);
            services.RemoveAll<ISaveCaseWorkspace>();
            services.AddScoped<ISaveCaseWorkspace>(provider => new SaveCaseWorkspace(store,
                provider.GetRequiredService<IStaffAccountQueries>()));
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");
        var initial = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        using var claim = await client.PostAsync($"/Cases/{store.CaseId:D}?handler=ClaimLease",
            Form(AntiforgeryValue(initial),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", InputValue(initial, "operationKey"))));
        AssertPrg(claim, store.CaseId);
        var editing = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        var excessName = CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.SettlementExcess);
        Assert.Contains($"name=\"{excessName}\"", editing, StringComparison.Ordinal);

        using var response = await client.PostAsync($"/Cases/{store.CaseId:D}?handler=Save",
            Form(AntiforgeryValue(editing),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", DetailsModelOperationKey),
                ("editLeaseToken", store.LeaseToken),
                (excessName, "250")));

        AssertPrg(response, store.CaseId);
        var saved = Assert.Single(store.Saves);
        Assert.Equal("250", saved.Settlement!.AssessmentFields![AssessmentVocabulary.SettlementExcess]);
    }

    [Fact]
    public async Task InvalidTypedEngineeringValueCannotBecomeASilentClear()
    {
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.PostReport };
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ISaveCaseWorkspace>(services, store));
        using var response = await workspace.Client.PostAsync($"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(DetailsModelOperationKey, "Correct recorded settlement", ("recoveryCharge", "invalid amount")));
        AssertPrg(response, store.CaseId);
        Assert.Empty(store.Saves);
        Assert.Contains("invalid amount", ProposedValuesPanel(await workspace.GetWorkspaceAsync()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAssessmentPathOutsideTheCaseEditorRefusesTheWholeSave()
    {
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.PostReport };
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ISaveCaseWorkspace>(services, store));
        using var response = await workspace.Client.PostAsync($"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(DetailsModelOperationKey, "Correct recorded settlement",
                (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.SettlementExcess), "0"),
                (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.StatementOfTruth),
                    "I confirm this report is true")));
        AssertPrg(response, store.CaseId);
        Assert.Empty(store.Saves);
        Assert.Contains("Excess", ProposedValuesPanel(await workspace.GetWorkspaceAsync()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAuthorizationRefusalKeepsTheProposedCaseValuesWithoutKeepingEditAuthority()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<ISaveCaseWorkspace>(services, store));
        store.NextFailure = new StaffAuthorizationException(StaffAccessRight.PerformCasework);
        using var response = await workspace.Client.PostAsync($"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(DetailsModelOperationKey, "Corrected claimant spelling",
                ("claimantName", "Rebecca Proposed")));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(store.Saves);
        var refused = await workspace.GetWorkspaceAsync();
        Assert.Contains("Rebecca Proposed", ProposedValuesPanel(refused), StringComparison.Ordinal);
        Assert.DoesNotContain(store.LeaseToken, refused, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"editLeaseToken\"", refused, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkspaceRenewsAndLeavesEditModeWithTheOperationKeysItRendered()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<IRenewCaseEditLease>(services, store);
            Substitute<IReleaseCaseEditLease>(services, store);
        });
        var leased = await workspace.GetWorkspaceAsync();
        var renewKey = HandlerFormInputValue(leased, "RenewLease", "operationKey");
        var releaseKey = HandlerFormInputValue(leased, "ReleaseLease", "operationKey");

        using var renewed = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=RenewLease",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", renewKey),
                ("editLeaseToken", store.LeaseToken)));
        AssertPrg(renewed, store.CaseId);
        var renewal = Assert.Single(store.LeaseRenewals);
        AssertClaimant(workspace, renewal.Actor);
        Assert.Equal(store.CaseId, renewal.CaseId);
        Assert.Equal(store.CaseVersion, renewal.ExpectedVersion);
        Assert.Equal(store.LeaseToken, renewal.LeaseToken);
        Assert.Equal(renewKey, renewal.OperationKey);
        var afterRenewal = await workspace.GetWorkspaceAsync();
        Assert.Contains("Edit mode was renewed.", afterRenewal, StringComparison.Ordinal);
        Assert.Equal(store.RenewedLeaseToken, InputValue(afterRenewal, "editLeaseToken"));
        Assert.NotEqual(renewKey, HandlerFormInputValue(afterRenewal, "RenewLease", "operationKey"));

        // A refusal that is not a lost lease keeps edit mode and the same renew key for the retry.
        store.NextFailure = new InvalidOperationException("The lease store is unavailable.");
        var retryKey = HandlerFormInputValue(afterRenewal, "RenewLease", "operationKey");
        using var refused = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=RenewLease",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", retryKey),
                ("editLeaseToken", store.RenewedLeaseToken)));
        AssertPrg(refused, store.CaseId);
        var afterRefusal = await workspace.GetWorkspaceAsync();
        Assert.Contains("Edit mode could not be renewed", afterRefusal, StringComparison.Ordinal);
        Assert.Equal(store.RenewedLeaseToken, InputValue(afterRefusal, "editLeaseToken"));
        Assert.Equal(retryKey, HandlerFormInputValue(afterRefusal, "RenewLease", "operationKey"));

        using var left = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ReleaseLease",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("operationKey", releaseKey),
                ("editLeaseToken", store.RenewedLeaseToken)));
        AssertPrg(left, store.CaseId);
        var release = Assert.Single(store.LeaseReleases);
        AssertClaimant(workspace, release.Actor);
        Assert.Equal(store.CaseId, release.CaseId);
        Assert.Equal(store.RenewedLeaseToken, release.LeaseToken);
        Assert.Equal(releaseKey, release.OperationKey);
        var afterRelease = await workspace.GetWorkspaceAsync();
        Assert.Contains("Edit mode was left safely.", afterRelease, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"editLeaseToken\"", afterRelease, StringComparison.Ordinal);
        Assert.Contains("handler=ClaimLease", afterRelease, StringComparison.Ordinal);
    }

    /// <summary>
    /// The workspace renders a heartbeat form so an open editor is never timed out
    /// mid-edit, and answers it without a redirect, a status message, or - crucially - any
    /// TempData write. TempData here is cookie-backed, so a beat that re-issued that cookie could
    /// race a form post the operator did make and lose them the token they are editing under.
    /// </summary>
    [Fact]
    public async Task WorkspaceHeartbeatKeepsEditingAliveWithoutDisturbingTheOperatorsLeaseState()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<IHeartbeatCaseEditLease>(services, store);
        });
        var leased = await workspace.GetWorkspaceAsync();
        var renewKey = HandlerFormInputValue(leased, "RenewLease", "operationKey");
        // v26: the record's own heartbeat form (`data-case-heartbeat`), beaten
        // by case-workspace.js; the Renew editing form stays as the no-script path.
        Assert.Contains("data-case-heartbeat", leased, StringComparison.Ordinal);
        Assert.Contains(
            $"data-heartbeat-seconds=\"{(int)CaseEditAuthority.HeartbeatInterval.TotalSeconds}\"",
            leased,
            StringComparison.Ordinal);

        using var beat = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=HeartbeatLease",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("editLeaseToken", store.LeaseToken)));

        Assert.Equal(HttpStatusCode.NoContent, beat.StatusCode);
        var heartbeat = Assert.Single(store.LeaseHeartbeats);
        AssertClaimant(workspace, heartbeat.Actor);
        Assert.Equal(store.CaseId, heartbeat.CaseId);
        Assert.Equal(store.LeaseToken, heartbeat.LeaseToken);

        // The operator is exactly where they were: same token, same keys, no message.
        var afterBeat = await workspace.GetWorkspaceAsync();
        Assert.Equal(store.LeaseToken, InputValue(afterBeat, "editLeaseToken"));
        Assert.Equal(renewKey, HandlerFormInputValue(afterBeat, "RenewLease", "operationKey"));
        Assert.DoesNotContain("Edit mode was renewed", afterBeat, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkspaceHeartbeatReportsALostLeaseAndIsRefusedWithoutItsAntiforgeryToken()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<IHeartbeatCaseEditLease>(services, store);
        });

        store.NextFailure = new CaseEditLeaseExpiredException(store.CaseId, store.CaseVersion);
        using var lost = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=HeartbeatLease",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("editLeaseToken", store.LeaseToken)));

        // 409 is the browser's signal to stop beating; the page it lands on next already shows
        // the case's real edit state, so nothing is said here.
        Assert.Equal(HttpStatusCode.Conflict, lost.StatusCode);

        // A faulted store says nothing about the lease: the answer is the fault itself, never
        // the 409 the browser would read as the lease being gone.
        store.NextFailure = new InvalidOperationException("The lease store is unavailable.");
        using var faulted = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=HeartbeatLease",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("editLeaseToken", store.LeaseToken)));
        Assert.Equal(HttpStatusCode.InternalServerError, faulted.StatusCode);

        using var unprotected = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=HeartbeatLease",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = store.CaseId.ToString("D"),
                ["editLeaseToken"] = store.LeaseToken,
            }));

        Assert.Equal(HttpStatusCode.BadRequest, unprotected.StatusCode);
    }

    /// <summary>The value of one hidden input inside the form that posts to the named handler.</summary>
    /// <summary>
    /// The Case's repairer is an Inspect-at option that
    /// names the repairer it came from, and the Save carries the repairer the
    /// operator confirmed into the one Case edit.
    /// </summary>
    [Fact]
    public async Task InspectAtOffersTheRepairerAndTheSaveCarriesTheConfirmedRepairer()
    {
        var store = new RecordingCaseDetailsStore
        {
            InspectionChoices = new(
                "8 Claimant Street",
                "12 Kingsway, Leeds LS1 1AA",
                "14 Storage Lane",
                [],
                "Kingsway Accident Repair")
        };
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<ISaveCaseWorkspace>(services, store);
            Substitute<Pegasus.Core.Address.IInspectionAddressChoicesQueries>(services, store);
        });
        var html = await workspace.GetWorkspaceAsync();

        Assert.Contains(
            System.Text.Encodings.Web.HtmlEncoder.Default.Encode(
                $"{OperatorLabels.CaseWorkspace.RepairerLocation} · Kingsway Accident Repair"),
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            "value=\"RepairerLocation\" data-address=\"12 Kingsway, Leeds LS1 1AA\"",
            html,
            StringComparison.Ordinal);
        Assert.Contains("name=\"repairerDirectoryId\"", html, StringComparison.Ordinal);

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            Form(workspace.AntiforgeryToken,
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", DetailsModelOperationKey),
                ("editLeaseToken", store.LeaseToken),
                ("reason", "Confirm the repairer"),
                ("repairerDirectoryId", string.Empty),
                ("repairerName", "Kingsway Accident Repair"),
                ("repairerAddress", "12 Kingsway, Leeds LS1 1AA"),
                ("inspectionAddress", "12 Kingsway, Leeds LS1 1AA")));

        AssertPrg(response, store.CaseId);
        var saved = Assert.Single(store.Saves);
        Assert.Equal("Kingsway Accident Repair", saved.Overview!.Repairer!.Name);
        Assert.Null(saved.Overview.Repairer.DirectoryOrganizationId);
        Assert.Equal("12 Kingsway, Leeds LS1 1AA", saved.Overview.RepairerAddress);
        Assert.Equal("12 Kingsway, Leeds LS1 1AA", saved.Inspection!.Address);
    }

    /// <summary>
    /// WP6 (issue 3): edit mode replaces the read panels rather than adding to
    /// them. One Case overview and one Inspection panel render in either mode,
    /// the editor keeps the read view's groups, and the claim number is named
    /// Claim reference — Our ref is the Case's own immutable reference.
    /// </summary>
    [Fact]
    public async Task EditModeReplacesTheReadPanelsRatherThanRenderingBothOfThem()
    {
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.Review };
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var editing = await workspace.GetWorkspaceAsync();

        // v26: one geometry for read and edit — the same Overview and
        // Inspection details sections render in either mode, the value box
        // and the control sharing each cell.
        Assert.Equal(1, Occurrences(editing, "id=\"section-overview\""));
        Assert.Equal(1, Occurrences(editing, ">Overview</h2>"));
        Assert.DoesNotContain("Edit Case data", editing, StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(editing, CaseWorkspaceLabels.Inspection.SectionTitle + "</h2>"));
        // The editor's groups are the read view's groups.
        foreach (var group in new[] { ">Case</h3>", ">Principal</h3>", ">Claimant</h3>" })
        {
            Assert.Equal(1, Occurrences(editing, group));
        }
        Assert.Contains(
            "<label for=\"f-claim-reference\">Claim reference</label>",
            editing,
            StringComparison.Ordinal);
        Assert.Contains("name=\"claimNumber\"", editing, StringComparison.Ordinal);
        // Our ref is the Case's own immutable reference: an identity cell with
        // its lock, never a control.
        Assert.Matches("<span class=\"lbl\">Our ref<svg[^>]*class=\"icon lk\"", editing);
        Assert.Contains("<div class=\"fv mono\">QDOS3100042</div>", editing, StringComparison.Ordinal);
        Assert.DoesNotContain(">Our ref</label>", editing, StringComparison.Ordinal);

        var reading = await ReadCaseAsync(new RecordingCaseDetailsStore());

        Assert.Equal(1, Occurrences(reading, "id=\"section-overview\""));
        Assert.Equal(1, Occurrences(reading, ">Overview</h2>"));
        Assert.Equal(1, Occurrences(reading, CaseWorkspaceLabels.Inspection.SectionTitle + "</h2>"));
        Assert.DoesNotContain("name=\"vehicleRegistration\"", reading, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"case-edit-form\"", reading, StringComparison.Ordinal);
    }

    private static string HandlerFormInputValue(string html, string handler, string name)
    {
        var form = Regex.Match(
            html,
            $"<form[^>]*handler={Regex.Escape(handler)}[^>]*>.*?</form>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(form.Success, $"The workspace must render the '{handler}' form.");
        return InputValue(form.Value, name);
    }



    [Fact]
    public async Task HoldingTheEditLeaseDefersOnlyFilesAndKeepsTheSingleEditorComplete()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = await workspace.GetWorkspaceAsync();

        Assert.Equal(["files"], DeferredSections(html));
        Assert.Contains("id=\"section-files\"", html, StringComparison.Ordinal);
        Assert.Contains("section-placeholder", html, StringComparison.Ordinal);
        Assert.Equal(CaseSectionKeys, HostOrder(html));
        Assert.Equal(store.LeaseToken, InputValue(html, "editLeaseToken"));
        Assert.Equal(1, Occurrences(html, "id=\"case-edit-form\""));
        foreach (var field in new[]
        {
            "vehicleRegistration",
            "vehicleMake",
            "vehicleModel",
            "vehicleMileage",
            "inspectionAddress",
            "claimantName"
        })
        {
            Assert.Equal(1, Occurrences(html, $"name=\"{field}\""));
        }
    }

    /// <summary>
    /// A lazy Files request is an asynchronous read that can run beside a
    /// Claim, Save or release redirect. Without the page's render-only header,
    /// it does not restore edit state or reissue the cookie-backed TempData
    /// lease token.
    /// </summary>

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
                SubstituteDetailsPageReaders(services, store);
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
            "Another member of staff is editing",
            wrongHolderHtml,
            StringComparison.Ordinal);
        Assert.DoesNotContain(otherStaffId, wrongHolderHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"editLeaseToken\"", wrongHolderHtml, StringComparison.Ordinal);

        store.LeaseHolder = claimant;
        var recoveryHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        // The one control, reading Take over for the holder's own other
        // window (v26 § R): the claim replays this holder's retained lease.
        Assert.Contains("Take over", RecordBar(recoveryHtml), StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"editLeaseToken\"", recoveryHtml, StringComparison.Ordinal);
        Assert.Equal(claimOperationKey, InputValue(recoveryHtml, "operationKey"));
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
                services.RemoveAll<ISaveCaseWorkspace>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<ISaveCaseWorkspace>(store);
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
                services.RemoveAll<ISaveCaseWorkspace>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<ISaveCaseWorkspace>(store);
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
        // v26 names that one control Take over (the holder's own lease, not this browser's).
        Assert.DoesNotContain("name=\"editLeaseToken\"", refusedHtml, StringComparison.Ordinal);
        Assert.Contains("Take over", RecordBar(refusedHtml), StringComparison.Ordinal);
        Assert.Contains("handler=ClaimLease", refusedHtml, StringComparison.Ordinal);
    }


    [Fact]
    public async Task ARefusalOnOneCaseSurvivesAVisitToAnother()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        var otherStore = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<ISaveCaseWorkspace>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                var readers = new TwoCasePageReaders(store, otherStore);
                Substitute<IGetCasePageFrame>(services, readers);
                Substitute<IGetAssessmentWorkspace>(services, readers);
                services.AddSingleton<ISaveCaseWorkspace>(store);
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
        using var otherCaseResponse = await client.GetAsync($"/Cases/{otherStore.CaseId:D}");
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
                services.RemoveAll<ISaveCaseWorkspace>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<ISaveCaseWorkspace>(store);
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
                services.RemoveAll<ISaveCaseWorkspace>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<ISaveCaseWorkspace>(store);
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
                SubstituteDetailsPageReaders(services, store);
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

        Assert.Contains("r.hughes is editing", note, StringComparison.Ordinal);
        // An open editor keeps its own lease alive, so no moment when editing
        // becomes available is knowable here, and naming one would be a broken promise.
        Assert.DoesNotContain("Editing becomes available", note, StringComparison.Ordinal);
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
                SubstituteDetailsPageReaders(services, store);
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
            "Another member of staff is editing",
            note,
            StringComparison.Ordinal);
        // An open editor keeps its own lease alive, so no moment when editing
        // becomes available is knowable here, and naming one would be a broken promise.
        Assert.DoesNotContain("Editing becomes available", note, StringComparison.Ordinal);
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
        var store = new RecordingCaseDetailsStore
        {
            LeaseHolder = "pegasus-automation",
            LeaseHolderKind = ActorKind.Automation
        };
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IDescribeCaseEditAuthorityHolder>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
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

        Assert.Contains("AI is editing", note, StringComparison.Ordinal);
        // An open editor keeps its own lease alive, so no moment when editing
        // becomes available is knowable here, and naming one would be a broken promise.
        Assert.DoesNotContain("Editing becomes available", note, StringComparison.Ordinal);
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
                services.RemoveAll<ISaveCaseWorkspace>();
                services.RemoveAll<IDescribeCaseEditAuthorityHolder>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<ISaveCaseWorkspace>(store);
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
        var holderRecord = RecordBar(holderHtml);
        Assert.Contains("id=\"case-finish-editing-form\"", holderRecord, StringComparison.Ordinal);
        Assert.Contains("form=\"case-finish-editing-form\"", holderRecord, StringComparison.Ordinal);
        Assert.Contains("form=\"case-edit-form\"", holderRecord, StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(holderRecord, ">Cancel</span>"));
        Assert.Equal(1, Occurrences(holderRecord, ">Save</span>"));
        // WP6: both ways out of an edit are on the one action row.
        Assert.Equal(1, Occurrences(StickyActionRow(holderHtml), ">Cancel</span>"));
        Assert.Equal(1, Occurrences(StickyActionRow(holderHtml), ">Save</span>"));
        Assert.DoesNotContain("Finish editing", holderRecord, StringComparison.Ordinal);
        Assert.DoesNotContain("Save case data", holderRecord, StringComparison.Ordinal);
        AssertNoBannedVocabulary(holderRecord);

        // Recover: the same holder without the protected browser state.
        using (var recoveryClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        }))
        {
            var recoverHtml = await GetHtmlAsync(recoveryClient, $"/Cases/{store.CaseId:D}");
            Assert.Contains("Take over", RecordBar(recoverHtml), StringComparison.Ordinal);
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
                "r.hughes is editing",
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

    [Fact]
    public async Task AnAutomationHeldCaseIsReadOnlyToStaffAndAPostedClaimIsRefused()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore
        {
            LeaseHolder = "pegasus-automation",
            LeaseHolderKind = ActorKind.Automation
        };
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Contains("AI is editing", EditAuthorityNote(html), StringComparison.Ordinal);
        Assert.DoesNotContain("handler=ClaimLease", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Edit case<", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"editLeaseToken\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("pegasus-automation", html, StringComparison.OrdinalIgnoreCase);

        store.NextFailure = new CaseEditLeaseConflictException(store.CaseId, store.CaseVersion);
        using var claimResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(html),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", Guid.NewGuid().ToString("N"))));
        AssertPrg(claimResponse, store.CaseId);

        Assert.Empty(store.Claims);
        Assert.Equal("pegasus-automation", store.LeaseHolder);
        Assert.Equal(ActorKind.Automation, store.LeaseHolderKind);
        var afterRefusal = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Contains("This case is already being edited.", afterRefusal, StringComparison.Ordinal);
        Assert.Contains("AI is editing", EditAuthorityNote(afterRefusal), StringComparison.Ordinal);
        Assert.DoesNotContain("handler=ClaimLease", afterRefusal, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"editLeaseToken\"", afterRefusal, StringComparison.Ordinal);
    }

    /// <summary>
    /// Review point 12: the adverse disposition is not a step in the workflow.
    /// It stands alone at the end of the record bar, away from the progression
    /// actions, and offers only the outcomes Core will actually accept from
    /// the Case's current state. Created in error and E-mail unlinked each
    /// have their own action, so neither is ever in the chooser, and post-report
    /// completion is progression rather than an adverse disposition.
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


    private static string EditAuthorityNote(string html)
    {
        var start = html.IndexOf("data-edit-authority", StringComparison.Ordinal);
        Assert.True(start >= 0, "The edit-authority note is not rendered.");
        var end = html.IndexOf("</span>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The edit-authority note is not closed.");
        return html[start..end];
    }

    /// <summary>
    /// v26: the refused editor's proposed values are a sub-panel of Overview
    /// (`[data-proposed-values]`) rather than a section of their own.
    /// </summary>

    private static string ProposedValuesPanel(string html)
    {
        var start = html.IndexOf("data-proposed-values", StringComparison.Ordinal);
        Assert.True(start >= 0, "The proposed-values panel is not rendered.");
        var end = html.IndexOf("</table>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The proposed-values panel is not closed.");
        return html[start..end];
    }

    /// <summary>The Files section's upload-requests sub-panel (v26): the table of links.</summary>

    /// <summary>
    /// While the Automation Actor holds the lease, the workspace is read-only to
    /// staff — the holder is disclosed from its retained kind through the real descriptor, no
    /// claim control is rendered, and a claim posted anyway is refused without the page
    /// pretending edit mode was entered. The refusal is the shared owner's own conflict.
    /// </summary>

    private sealed class StubEditAuthorityHolders(string? displayName, bool isAutomation = false)
        : IDescribeCaseEditAuthorityHolder
    {
        public Task<CaseEditAuthorityHolder> ExecuteAsync(
            ActorKind? holderKind,
            string holderSubjectId,
            ActionActor actor,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CaseEditAuthorityHolder(displayName, isAutomation));
        }
    }

    /// <summary>
    /// The EVA stores the send page reads: the case has never been sent, and its
    /// principal carries the modes the test states.
    /// </summary>

    private sealed class TwoCasePageReaders(
        RecordingCaseDetailsStore first,
        RecordingCaseDetailsStore second) : IGetCasePageFrame, IGetAssessmentWorkspace
    {
        public Task<CasePageFrame?> ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken) =>
            query.CaseId == first.CaseId
                ? ((IGetCasePageFrame)first).ExecuteAsync(query, cancellationToken)
                : query.CaseId == second.CaseId
                    ? ((IGetCasePageFrame)second).ExecuteAsync(query, cancellationToken)
                    : Task.FromResult<CasePageFrame?>(null);

        public Task<AssessmentWorkspace?> ExecuteAsync(
            GetAssessmentWorkspaceQuery query,
            CancellationToken cancellationToken) =>
            query.CaseId == first.CaseId
                ? ((IGetAssessmentWorkspace)first).ExecuteAsync(query, cancellationToken)
                : query.CaseId == second.CaseId
                    ? ((IGetAssessmentWorkspace)second).ExecuteAsync(query, cancellationToken)
                    : Task.FromResult<AssessmentWorkspace?>(null);
    }

}
