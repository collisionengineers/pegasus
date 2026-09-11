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

namespace Pegasus.IntegrationTests;

/// <summary>
/// The workspace's own edit-mode actions that stay on <c>DetailsModel</c>: renewing the lease
/// and leaving it. Claiming and recovery are covered by the workspace tests.
/// </summary>
public sealed partial class CaseDetailsWebTests
{
    [Theory]
    [InlineData("Engineer", false, true)]
    [InlineData("User", false, false)]
    [InlineData("Engineer", true, false)]
    public async Task WorkspaceSaveUsesCoreFindingAndEligibleSignOffAuthority(
        string role, bool forgeSignOffAccount, bool reachesStore)
    {
        var store = new RecordingCaseDetailsStore { State = CaseLifecycleState.ReportPreparation };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            Substitute<IGetCase>(services, store);
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
        Assert.Equal(role == "Engineer", editing.Contains($"name=\"{outcomeName}\"", StringComparison.Ordinal));
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
                     .Append(AssessmentVocabulary.HistoryCheck))
        {
            var name = CaseWorkspaceLabels.Editors.FormName(path);
            // Administrator includes engineering authority; every editor uses
            // the same Case Save form, including engineering findings.
            Assert.Matches($"<(input|textarea|select)[^>]*name=\"{Regex.Escape(name)}\"[^>]*form=\"case-edit-form\"", html);
        }
        Assert.Single(Regex.Matches(html, "id=\"case-edit-form\""));
        // The history check is the Vehicle section's own labelled area rather
        // than a row among the vehicle's facts, in edit mode as in read mode.
        Assert.Contains("class=\"field vehicle-history\"", html, StringComparison.Ordinal);
        Assert.Contains("<h3 id=\"case-vehicle-history-title\"", html, StringComparison.Ordinal);
        Assert.Contains(
            "aria-labelledby=\"case-vehicle-history-title\"",
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Saving returns the case to Not ready", html, StringComparison.Ordinal);
        Assert.True(store.MetadataReads > 0);

        var fields = new (string Name, string Value)[]
        {
            (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.SettlementExcess), "0"),
            (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.SettlementClaimantVatRegistered), "false"),
            (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.HistoryCheck), ""),
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
    [InlineData(CaseLifecycleState.NotReady)]
    [InlineData(CaseLifecycleState.Review)]
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
    /// CASE-024: the workspace renders a heartbeat form so an open editor is never timed out
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
        Assert.Contains("data-edit-heartbeat", leased, StringComparison.Ordinal);
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
    /// INTK-058/CASE-041: the Case's repairer is an Inspect-at option that
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
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var editing = await workspace.GetWorkspaceAsync();

        Assert.Equal(1, Occurrences(editing, "case-overview-panel"));
        Assert.Equal(1, Occurrences(editing, ">Case overview</h2>"));
        Assert.DoesNotContain("Edit Case data", editing, StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(editing, OperatorLabels.CaseWorkspace.InspectionAddressPanel + "</h2>"));
        // The editor's groups are the read view's groups.
        foreach (var group in new[] { ">Case</h3>", ">Principal</h3>", ">Claimant</h3>" })
        {
            Assert.Equal(1, Occurrences(editing, group));
        }
        Assert.Contains(
            "<label for=\"edit-claim-reference\">Claim reference</label>",
            editing,
            StringComparison.Ordinal);
        Assert.Contains("name=\"claimNumber\"", editing, StringComparison.Ordinal);
        Assert.Contains("<dt>Our ref</dt><dd>QDOS3100042</dd>", editing, StringComparison.Ordinal);
        Assert.DoesNotContain(">Our ref</label>", editing, StringComparison.Ordinal);

        var reading = await ReadCaseAsync(new RecordingCaseDetailsStore());

        Assert.Equal(1, Occurrences(reading, "case-overview-panel"));
        Assert.Equal(1, Occurrences(reading, ">Case overview</h2>"));
        Assert.Equal(1, Occurrences(reading, OperatorLabels.CaseWorkspace.InspectionAddressPanel + "</h2>"));
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

    private sealed partial class RecordingCaseDetailsStore :
        IRenewCaseEditLease,
        IHeartbeatCaseEditLease,
        IReleaseCaseEditLease,
        IGetAssessmentAccess,
        IGetAssessmentWorkspace,
        ICaseReportSnapshotSource,
        ICaseWorkspaceStore
    {
        public int MetadataReads { get; private set; }

        Task<SaveCaseWorkspaceResult> ICaseWorkspaceStore.SaveAsync(
            SaveCaseWorkspaceRequest request, CancellationToken cancellationToken) =>
            ((ISaveCaseWorkspace)this).ExecuteAsync(request, cancellationToken);

        private CaseAssessmentProjection EngineeringAssessment() => new(
            CaseId, "QDOS3100042", CaseVersion, State, null,
            [new(AssessmentVocabulary.ReportDate, "2031-05-06", ActorKind.Staff,
                "recorded-engineer", _now, "recorded-engineer", _now)],
            [], new("AB12CDE", null, null, null, null, null, "tbc", null, null, null, null));

        Task<AssessmentAccessState?> IGetAssessmentAccess.ExecuteAsync(
            GetAssessmentAccessQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<AssessmentAccessState?>(new(State));

        Task<AssessmentWorkspace?> IGetAssessmentWorkspace.ExecuteAsync(
            GetAssessmentWorkspaceQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<AssessmentWorkspace?>(AssessmentWorkspaceTestData.Create(EngineeringAssessment()));

        Task<CaseReportFreezeInputs?> ICaseReportSnapshotSource.GetAsync(
            Guid caseId, ActionActor actor, CancellationToken cancellationToken)
        {
            MetadataReads++;
            var assessment = EngineeringAssessment();
            return Task.FromResult<CaseReportFreezeInputs?>(new CaseReportFreezeInputs(
                new(assessment, "Case claimant", assessment.Reference, "CLM-42", [], null, [], []),
                new(assessment, null, null, [], null, null, [], new Dictionary<Guid, Pegasus.Core.Documents.DocumentVersion>()),
                assessment.Reference, CaseVersion));
        }

        public string RenewedLeaseToken { get; } = "opaque-renewed-case-lease";
        public List<RenewCaseEditLeaseRequest> LeaseRenewals { get; } = [];
        public List<HeartbeatCaseEditLeaseRequest> LeaseHeartbeats { get; } = [];
        public List<ReleaseCaseEditLeaseRequest> LeaseReleases { get; } = [];

        Task<CaseEditLease> IHeartbeatCaseEditLease.ExecuteAsync(
            HeartbeatCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            LeaseHeartbeats.Add(request);
            return Task.FromResult(new CaseEditLease(
                request.CaseId,
                request.LeaseToken,
                request.Actor.SubjectId,
                CaseVersion,
                _now.AddMinutes(5)));
        }

        Task<CaseEditLease> IRenewCaseEditLease.ExecuteAsync(
            RenewCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            LeaseRenewals.Add(request);
            return Task.FromResult(new CaseEditLease(
                request.CaseId,
                RenewedLeaseToken,
                request.Actor.SubjectId,
                request.ExpectedVersion,
                _now.AddMinutes(10)));
        }

        Task IReleaseCaseEditLease.ExecuteAsync(
            ReleaseCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            LeaseReleases.Add(request);
            _leaseHolder = null;
            _leaseOperationKey = null;
            return Task.CompletedTask;
        }
    }
}
