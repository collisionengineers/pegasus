using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Web.Presentation;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The v26 Valuation section on the one Case workspace: Glass's, Brego, Super
/// CAP, CAP and Cazana are each one card in both modes, whose boxes belong to
/// the Case form so the ribbon Save records a changed card (23 September
/// 2026) and whose Get valuation fills them in place from the connected
/// provider; the Valuation month and AI market research start the existing
/// job for the month, a pending job shows as a Researching card, and Apply as
/// Engineer's Value posts the calculator's selection to the Core policy shape.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseValuationV26WebTests
{
    private static readonly (string Slug, string Name)[] GuideSources =
    [
        ("glasses", "Glasses"),
        ("brego", "Brego"),
        ("super-cap", "SuperCap"),
        ("cap", "Cap"),
        ("cazana", "Cazana")
    ];

    /// <summary>
    /// The edit session renders the month input on the research form, the AI
    /// market research button, and one card per guide source whose month,
    /// mileage, retail and trade boxes join the Case form, with a Get
    /// valuation button that asks by script and the card's hidden notice.
    /// There is no card Save and no Add valuation dialog.
    /// </summary>
    [Fact]
    public async Task WhileEditingTheValuationSectionOffersTheMonthTheGuideSourcesAndTheResearchForm()
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        using var workspace = await EnterEngineerEditModeAsync(store, valuation.Register);

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");

        Assert.Contains("data-valuation-tools", html, StringComparison.Ordinal);
        var currentMonth = Pegasus.Core.LondonCalendar.DateAt(DateTimeOffset.UtcNow)
            .ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var month = InputTag(html, "guideMonth", "data-valuation-month");
        Assert.Contains("type=\"month\"", month, StringComparison.Ordinal);
        Assert.Contains("form=\"case-market-research-form\"", month, StringComparison.Ordinal);
        Assert.Contains($"value=\"{currentMonth}\"", month, StringComparison.Ordinal);

        for (var index = 0; index < GuideSources.Length; index++)
        {
            var (source, name) = GuideSources[index];
            var card = EntryCard(html, source);
            Assert.DoesNotContain("<form", card, StringComparison.Ordinal);
            Assert.Contains(
                $"name=\"guideEntries[{index}].Source\" value=\"{name}\" form=\"case-edit-form\"",
                card,
                StringComparison.Ordinal);
            foreach (var box in new[] { "RetailValue", "TradeValue", "Mileage", "GuideMonth" })
            {
                var input = Regex.Match(
                    card,
                    $"<input[^>]*name=\"guideEntries\\[{index}\\]\\.{box}\"[^>]*>",
                    RegexOptions.CultureInvariant);
                Assert.True(input.Success, $"The {source} card must render its {box} box.");
                Assert.Contains("form=\"case-edit-form\"", input.Value, StringComparison.Ordinal);
                Assert.DoesNotContain("required", input.Value, StringComparison.Ordinal);
            }
            var button = ButtonTag(html, source);
            Assert.Contains("type=\"button\"", button, StringComparison.Ordinal);
            Assert.Contains("data-valuation-get", button, StringComparison.Ordinal);
            Assert.Contains("handler=GetValuation", button, StringComparison.Ordinal);
            Assert.Contains("source=" + name, button, StringComparison.Ordinal);
            Assert.DoesNotContain("form=", button, StringComparison.Ordinal);
            Assert.Contains("data-valuation-notice hidden", card, StringComparison.Ordinal);
            Assert.Contains("data-dialog-open=\"problem-dialog\"", card, StringComparison.Ordinal);
            Assert.Contains(CaseWorkspaceLabels.Valuation.ReportAProblem, card, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("data-valuation-save", html, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=SaveValuation", html, StringComparison.Ordinal);
        Assert.DoesNotContain("add-valuation", html, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=AddValuation", html, StringComparison.Ordinal);
        var research = ButtonTag(html, "ai-market-research");
        Assert.Contains("type=\"submit\"", research, StringComparison.Ordinal);
        Assert.Contains("form=\"case-market-research-form\"", research, StringComparison.Ordinal);

        var researchForm = Regex.Match(
            html,
            "<form[^>]*id=\"case-market-research-form\"[^>]*>",
            RegexOptions.CultureInvariant);
        Assert.True(researchForm.Success, "The Valuation section must render the AI market research form.");
        Assert.Contains("handler=StartMarketResearch", researchForm.Value, StringComparison.Ordinal);
        Assert.Contains("data-valuation-research-form", researchForm.Value, StringComparison.Ordinal);
        Assert.Contains(CaseWorkspaceLabels.Valuation.GetValuation, html, StringComparison.Ordinal);
        Assert.Contains(CaseWorkspaceLabels.Valuation.ValuationMonth, html, StringComparison.Ordinal);
        // The buttons state nothing about a provider: no gated tooltip, no disabled state (v26).
        Assert.DoesNotContain("data-condition=", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// While a Market research job is in progress the AI card reads as
    /// Researching for the job's month, stands in for the recorded AI card,
    /// and the month input follows the job rather than the calendar.
    /// </summary>
    [Fact]
    public async Task APendingMarketResearchJobShowsAsAResearchingCardForItsMonth()
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        var glasses = valuation.AddGuide(ValuationSource.Glasses, 12_500m, 10_250m);
        var earlierResearch = valuation.AddGuide(ValuationSource.AiMarketResearch, 12_100m, 9_900m);
        var pending = valuation.SetPending(new DateOnly(2026, 9, 1));
        using var workspace = await EnterEngineerEditModeAsync(store, valuation.Register);

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");

        Assert.Contains($"data-valuation-pending=\"{pending.JobId:D}\"", html, StringComparison.Ordinal);
        Assert.Contains(
            CaseWorkspaceLabels.Valuation.Researching + " · Sep 2026",
            html,
            StringComparison.Ordinal);
        Assert.Contains($"data-valuation-card=\"{glasses.ValuationId:D}\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain($"data-valuation-card=\"{earlierResearch.ValuationId:D}\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"2026-09\"", InputTag(html, "guideMonth", "data-valuation-month"), StringComparison.Ordinal);
    }

    /// <summary>
    /// AI market research reaches its port with the posted month, the Case,
    /// the operation key and the editing Engineer; the job takes no lease, so
    /// the edit session carries on as it was.
    /// </summary>
    [Theory]
    [InlineData(StaffRole.Administrator)]
    [InlineData(StaffRole.Engineer)]
    [InlineData(StaffRole.User)]
    public async Task StartMarketResearchReachesThePortWithThePostedMonthAndKeepsTheSession(StaffRole role)
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        using var workspace = await EnterEngineerEditModeAsync(store, valuation.Register, role);
        const string operationKey = "2f2e2d2c2b2a29282726252423222120";

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=StartMarketResearch",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("operationKey", operationKey),
                ("editLeaseToken", store.LeaseToken),
                ("guideMonth", "2026-09")));

        AssertValuationPrg(response, store.CaseId);
        var started = Assert.Single(valuation.Started);
        Assert.True(started.Actor.IsInRole(role));
        Assert.Equal(store.CaseId, started.CaseId);
        Assert.Equal(new DateOnly(2026, 9, 1), started.GuideMonth);
        Assert.Equal(operationKey, started.OperationKey);
        AssertClaimant(workspace, started.Actor);
        Assert.Single(store.Claims);
        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");
        Assert.Contains("data-case-editing=\"true\"", html, StringComparison.Ordinal);
        Assert.Equal(store.LeaseToken, InputValue(html, "editLeaseToken"));
        Assert.DoesNotContain("role=\"alert\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The research command is guarded like every section command: without an
    /// edit lease or with an expired form it is refused on the Valuation
    /// section and no job is started.
    /// </summary>
    [Theory]
    [InlineData("", "3f3e3d3c3b3a39383736353433323130")]
    [InlineData("opaque-live-case-lease", "not-an-operation-key")]
    public async Task StartMarketResearchIsRefusedWithoutALeaseOrALiveForm(string editLeaseToken, string operationKey)
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        using var workspace = await EnterEngineerEditModeAsync(store, valuation.Register);

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=StartMarketResearch",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("operationKey", operationKey),
                ("editLeaseToken", editLeaseToken),
                ("guideMonth", "2026-09")));

        AssertValuationPrg(response, store.CaseId);
        Assert.Empty(valuation.Started);
        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");
        Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The calculator's form carries the basis card, its stamp and the lease
    /// envelope; Apply posts the selection as the screen offers it (a prior
    /// total loss of 10 per cent) and Core's port receives the policy shape
    /// (a fraction of 0.10). Decision F: an immediate post keeps the session.
    /// </summary>
    [Theory]
    [InlineData(StaffRole.Administrator)]
    [InlineData(StaffRole.Engineer)]
    [InlineData(StaffRole.User)]
    public async Task ApplyValuationPostsTheSelectionToThePortAsThePolicyShape(StaffRole role)
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        var glasses = valuation.AddGuide(ValuationSource.Glasses, 12_500m, 10_250m);
        var preset = valuation.AddPreset("Tow bar", 150m);
        using var workspace = await EnterEngineerEditModeAsync(store, valuation.Register, role);
        var stamp = Pegasus.Web.Pages.Cases.DetailsModel.StampOf(glasses).ToString("o", CultureInfo.InvariantCulture);

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");
        var applyForm = Regex.Match(
            html,
            "<form[^>]*id=\"case-valuation-form\"[^>]*>(?<body>(?:(?!</form>).)*)</form>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(applyForm.Success, "The Valuation section must render the calculator's form while editing.");
        Assert.Contains("handler=ApplyValuation", applyForm.Value, StringComparison.Ordinal);
        Assert.Contains("data-valuation-form", applyForm.Value, StringComparison.Ordinal);
        Assert.Equal(store.LeaseToken, InputValue(applyForm.Groups["body"].Value, "editLeaseToken"));
        Assert.Equal(stamp, InputValue(applyForm.Groups["body"].Value, "guideValuationStampUtc"));
        Assert.Contains(
            $"name=\"selection.GuideValuationId\" value=\"{glasses.ValuationId:D}\"",
            html,
            StringComparison.Ordinal);
        Assert.Contains("name=\"selection.PriorTotalLossPercentage\"", html, StringComparison.Ordinal);
        Assert.Contains($"name=\"selection.AdditionPresetId\" value=\"{preset.Id:D}\"", html, StringComparison.Ordinal);
        var apply = ButtonTagByHook(html, "data-valuation-apply");
        Assert.DoesNotContain("disabled", apply, StringComparison.Ordinal);
        const string operationKey = "4f4e4d4c4b4a49484746454443424140";

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ApplyValuation",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", operationKey),
                ("editLeaseToken", store.LeaseToken),
                ("guideValuationStampUtc", stamp),
                ("selection.GuideValuationId", glasses.ValuationId.ToString("D")),
                ("selection.PriorTotalLossPercentage", "10"),
                ("selection.CommercialVat", "true"),
                ("selection.ConditionDeduction", "250"),
                ("correctedEngineerValue", "999999"),
                ("selection.AdditionSelected[0]", "0"),
                ("selection.AdditionPresetId[0]", preset.Id.ToString("D")),
                ("selection.AdditionPresetVersion[0]", preset.Version.ToString(CultureInfo.InvariantCulture)),
                ("selection.AdditionLabel[0]", ""),
                ("selection.AdditionAmount[0]", "175")));

        AssertValuationPrg(response, store.CaseId);
        var applied = Assert.Single(valuation.Applied);
        Assert.True(applied.Actor.IsInRole(role));
        Assert.Equal(store.CaseId, applied.CaseId);
        Assert.Equal(store.CaseVersion, applied.ExpectedVersion);
        Assert.Equal(store.LeaseToken, applied.EditLeaseToken);
        Assert.Equal(operationKey, applied.OperationKey);
        Assert.Equal(DateTimeOffset.Parse(stamp, CultureInfo.InvariantCulture), applied.GuideValuationStampUtc);
        Assert.Equal(glasses.ValuationId, applied.Selection.GuideValuationId);
        Assert.Equal(0.10m, applied.Selection.PriorTotalLossPercentage);
        Assert.True(applied.Selection.CommercialVat);
        Assert.Equal(250m, applied.Selection.ConditionDeduction);
        var addition = Assert.Single(applied.Selection.Additions);
        Assert.Equal(preset.Id, addition.PresetId);
        Assert.Equal(preset.Version, addition.PresetVersion);
        Assert.Equal(175m, addition.Amount);
        var claimant = store.Claims[0].Actor;
        Assert.Equal(claimant.SubjectId, applied.Actor.SubjectId);

        // v25 decision F: the immediate post reclaims the session rather than ending it.
        Assert.Equal(2, store.Claims.Count);
        var after = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");
        Assert.Contains("data-case-editing=\"true\"", after, StringComparison.Ordinal);
        Assert.Equal(store.LeaseToken, InputValue(after, "editLeaseToken"));
        AssertEditorCommit(after, "case-valuation-form", operationKey, applied.ExpectedVersion);
    }

    [Fact]
    public async Task SelectableValuationBasisCardsAreFocusable()
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        var glasses = valuation.AddGuide(ValuationSource.Glasses, 12_500m, 10_250m);
        using var workspace = await EnterEngineerEditModeAsync(store, valuation.Register);

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");

        var entry = EntryCard(html, "glasses");
        Assert.Contains($"data-valuation-card=\"{glasses.ValuationId:D}\"", entry, StringComparison.Ordinal);
        Assert.Contains("tabindex=\"0\"", entry, StringComparison.Ordinal);
    }

    /// <summary>
    /// Read mode shows the same five source cards as editing (23 September
    /// 2026): each holds its source's latest figures in greyed boxes, or reads
    /// Not recorded, and none carries a control, a card Save or Get valuation.
    /// </summary>
    [Fact]
    public async Task ReadModeShowsTheSameSourceCardsWithoutControls()
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        valuation.AddGuide(ValuationSource.Glasses, 12_500m, 10_250m);

        var html = await ReadValuationAsync(store, valuation);

        foreach (var (source, _) in GuideSources)
        {
            var card = EntryCard(html, source);
            Assert.Contains("class=\"fc ro\"", card, StringComparison.Ordinal);
            Assert.DoesNotContain("<input", card, StringComparison.Ordinal);
            Assert.DoesNotContain("data-valuation-get", card, StringComparison.Ordinal);
        }
        Assert.Contains(
            ValuationCalculationPolicy.FormatMoney(12_500m),
            WebUtility.HtmlDecode(EntryCard(html, "glasses")),
            StringComparison.Ordinal);
        Assert.Contains(
            Pegasus.Web.Presentation.OperatorLabels.CaseWorkspace.AbsentValue,
            EntryCard(html, "brego"),
            StringComparison.Ordinal);
        Assert.DoesNotContain("guideEntries[", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-valuation-empty", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The calculator opens on the selection the latest adoption applied, in
    /// both modes: read mode ticks the applied increase among every preset,
    /// and editing starts from the same selection rather than from blank.
    /// </summary>
    [Fact]
    public async Task TheCalculatorOpensOnTheAppliedSelection()
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        var glasses = valuation.AddGuide(ValuationSource.Glasses, 12_500m, 10_250m);
        var towBar = valuation.AddPreset("Tow bar", 150m);
        valuation.AddPreset("Roof bars", 90m);
        valuation.SetApplied(glasses, towBar, 175m);

        var read = await ReadValuationAsync(store, valuation);
        Assert.Contains("Roof bars", read, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(read, "data-valuation-applied-addition=\"true\"", RegexOptions.CultureInvariant));

        using var workspace = await EnterEngineerEditModeAsync(store, valuation.Register);
        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");
        Assert.Matches("<option value=\"10\" selected=\"selected\">", html);
        Assert.Contains("value=\"250.00\" data-valuation-input", html, StringComparison.Ordinal);
        var towBarRow = Regex.Match(
            html,
            "<div class=\"add on\" data-valuation-add=\"0\">.*?</div>",
            RegexOptions.CultureInvariant | RegexOptions.Singleline);
        Assert.True(towBarRow.Success, "The applied preset must open ticked.");
        Assert.Contains("checked=\"checked\"", towBarRow.Value, StringComparison.Ordinal);
        Assert.Contains("value=\"175\"", towBarRow.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// A card still showing its source's latest recorded figures is not
    /// recorded again by the Case save; only a changed card is carried.
    /// </summary>
    [Fact]
    public async Task AnUntouchedGuideCardIsNotRecordedAgain()
    {
        var store = new RecordingCaseDetailsStore { AcceptWorkspaceSaves = true };
        var valuation = new RecordingValuationSection(store.CaseId);
        valuation.AddGuide(ValuationSource.Glasses, 12_500m, 10_250m);
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            valuation.Register(services);
            Substitute<ISaveCaseWorkspace>(services, store);
        });

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(
                DetailsModelOperationKey,
                "Recorded the Brego figure",
                ("guideEntries[0].Source", nameof(ValuationSource.Glasses)),
                ("guideEntries[0].GuideMonth", "2031-05"),
                ("guideEntries[0].Mileage", "42000"),
                ("guideEntries[0].RetailValue", "12500.00"),
                ("guideEntries[0].TradeValue", "10250.00"),
                ("guideEntries[1].Source", nameof(ValuationSource.Brego)),
                ("guideEntries[1].GuideMonth", "2031-05"),
                ("guideEntries[1].Mileage", "42000"),
                ("guideEntries[1].RetailValue", "13250.00"),
                ("guideEntries[1].TradeValue", "11000.00")));

        AssertPrg(response, store.CaseId);
        var saved = Assert.Single(store.Saves);
        var card = Assert.Single(saved.Valuation!.GuideEntries!);
        Assert.Equal(ValuationSource.Brego, card.Source);
        Assert.Equal(13_250m, card.RetailValue);
    }

    /// <summary>
    /// A guide source with no connected provider answers the card's script
    /// with "unavailable", so the card shows its own notice; without script
    /// the answer is the approved sentence on the Valuation section. Nothing
    /// is recorded and the edit session is left as it was.
    /// </summary>
    [Fact]
    public async Task GetValuationWithoutAConnectedProviderAnswersUnavailableAndRecordsNothing()
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        using var workspace = await EnterEngineerEditModeAsync(store, valuation.Register);

        using (var json = await PostGetValuationAsync(workspace, ValuationSource.SuperCap, "2026-09", asJson: true))
        {
            Assert.Equal(HttpStatusCode.OK, json.StatusCode);
            using var answer = JsonDocument.Parse(await json.Content.ReadAsStringAsync());
            Assert.Equal("unavailable", answer.RootElement.GetProperty("status").GetString());
        }

        using var response = await PostGetValuationAsync(workspace, ValuationSource.SuperCap, "2026-09", asJson: false);

        AssertValuationPrg(response, store.CaseId);
        Assert.Empty(store.Saves);
        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");
        Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
        Assert.Contains(
            CaseWorkspaceLabels.Valuation.Unavailable(ValuationSource.SuperCap),
            html,
            StringComparison.Ordinal);
        Assert.Contains("data-case-editing=\"true\"", html, StringComparison.Ordinal);
        Assert.Equal(store.LeaseToken, InputValue(html, "editLeaseToken"));
    }

    /// <summary>
    /// The card's script is refused as JSON, not redirected, when the request
    /// carries no edit lease: the page is never redrawn under the operator.
    /// </summary>
    [Fact]
    public async Task GetValuationWithoutALeaseIsRefusedAsJson()
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        using var workspace = await EnterEngineerEditModeAsync(store, valuation.Register);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/Cases/{store.CaseId:D}?handler=GetValuation&source={ValuationSource.Brego}")
        {
            Content = Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("operationKey", "6f6e6d6c6b6a69686766656463626160"),
                ("editLeaseToken", ""),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("guideMonth", "2026-09"))
        };
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await workspace.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var answer = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("refused", answer.RootElement.GetProperty("status").GetString());
        var message = answer.RootElement.GetProperty("message").GetString();
        Assert.False(string.IsNullOrWhiteSpace(message));
        // The refusal was answered to the script, not left for the next page.
        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");
        Assert.DoesNotContain(message!, html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A connected provider is asked for the Case's accepted registration and
    /// mileage in the posted month, and its figures come back for the card's
    /// boxes; nothing is recorded and nothing is held for a later redraw, and
    /// the edit session continues.
    /// </summary>
    [Theory]
    [InlineData(StaffRole.Administrator)]
    [InlineData(StaffRole.Engineer)]
    [InlineData(StaffRole.User)]
    public async Task GetValuationWithAConnectedProviderAnswersTheFiguresAndRecordsNothing(StaffRole role)
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        var provider = new FakeGuideValuationProvider(ValuationSource.Brego, 13_250m, 11_000m);
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            valuation.Register(services);
            Substitute<ICaseDataQueries>(services, store);
            services.AddSingleton<IGuideValuationProvider>(provider);
        }, role);

        using var response = await PostGetValuationAsync(workspace, ValuationSource.Brego, "2026-08", asJson: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var answer = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var figures = answer.RootElement;
        Assert.Equal("ok", figures.GetProperty("status").GetString());
        Assert.Equal("13250.00", figures.GetProperty("retail").GetString());
        Assert.Equal("11000.00", figures.GetProperty("trade").GetString());
        Assert.Equal("42000", figures.GetProperty("mileage").GetString());
        Assert.Equal("2026-08", figures.GetProperty("guideMonth").GetString());
        var asked = Assert.Single(provider.Requests);
        Assert.Equal("AB12CDE", asked.Registration);
        Assert.Equal(42_000L, asked.Mileage);
        Assert.Equal(new DateOnly(2026, 8, 1), asked.GuideMonth);
        Assert.Empty(store.Saves);

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");
        Assert.Contains("data-case-editing=\"true\"", html, StringComparison.Ordinal);
        Assert.Equal(store.LeaseToken, InputValue(html, "editLeaseToken"));
        Assert.DoesNotContain("value=\"13250.00\"", EntryCard(html, "brego"), StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> PostGetValuationAsync(
        LeasedWorkspace workspace,
        ValuationSource source,
        string guideMonth,
        bool asJson)
    {
        var store = workspace.Store;
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/Cases/{store.CaseId:D}?handler=GetValuation&source={source}")
        {
            Content = Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("operationKey", "5f5e5d5c5b5a59585756555453525150"),
                ("editLeaseToken", store.LeaseToken),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("guideMonth", guideMonth))
        };
        if (asJson)
        {
            request.Headers.Add("X-Requested-With", "fetch");
            request.Headers.Accept.ParseAdd("application/json");
        }
        return await workspace.Client.SendAsync(request);
    }

    /// <summary>The Case record read without an edit session, with the section's ports substituted.</summary>
    private static async Task<string> ReadValuationAsync(
        RecordingCaseDetailsStore store,
        RecordingValuationSection valuation)
    {
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseVehicleSection>(services, store);
                Substitute<IGetCaseValuationSection>(services, store);
                Substitute<IGetCaseNotesSection>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IGetAssessmentAccess>(services, new FakeGetAssessmentAccess(canOpen: true));
                Substitute<IGetAssessmentWorkspace>(services, store);
                valuation.Register(services);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", nameof(StaffRole.Engineer));
        return await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=valuation");
    }

    /// <summary>
    /// One source's card: from its opening tag to the next card or the end of
    /// the cards. The card holds nested cells, so it is sliced, not matched.
    /// </summary>
    private static string EntryCard(string html, string source)
    {
        var hook = html.IndexOf($"data-valuation-entry=\"{source}\"", StringComparison.Ordinal);
        Assert.True(hook >= 0, $"The Valuation section must render the card for '{source}'.");
        var start = html.LastIndexOf("<div", hook, StringComparison.Ordinal);
        var next = html.IndexOf("class=\"valuation-card", hook, StringComparison.Ordinal);
        var calc = html.IndexOf("data-valuation-calc", hook, StringComparison.Ordinal);
        // The calculator's Apply form follows the last card while editing.
        var applyForm = html.IndexOf("id=\"case-valuation-form\"", hook, StringComparison.Ordinal);
        var apply = applyForm < 0 ? -1 : html.LastIndexOf("<form", applyForm, StringComparison.Ordinal);
        var end = new[] { next, calc, apply }.Where(index => index > hook).DefaultIfEmpty(html.Length).Min();
        return html[start..end];
    }

    /// <summary>The one input carrying <paramref name="name"/> and <paramref name="hook"/>.</summary>
    private static string InputTag(string html, string name, string hook)
    {
        var tag = Regex.Match(
            html,
            $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*{Regex.Escape(hook)}[^>]*>",
            RegexOptions.CultureInvariant);
        Assert.True(tag.Success, $"The Valuation section must render the input '{name}' with {hook}.");
        return tag.Value;
    }

    /// <summary>The Get valuation button for one source.</summary>
    private static string ButtonTag(string html, string source) =>
        ButtonTagByHook(html, $"data-valuation-source=\"{source}\"");

    private static string ButtonTagByHook(string html, string hook)
    {
        var tag = Regex.Match(
            html,
            $"<button[^>]*{Regex.Escape(hook)}[^>]*>",
            RegexOptions.CultureInvariant);
        Assert.True(tag.Success, $"The Valuation section must render the button {hook}.");
        return tag.Value;
    }

    /// <summary>
    /// The Valuation section's own ports, substituted together so the section
    /// renders from seeded cards, presets and a pending job and its commands
    /// are recorded rather than persisted.
    /// </summary>
    private sealed class FakeGuideValuationProvider(ValuationSource source, decimal retail, decimal trade)
        : IGuideValuationProvider
    {
        public List<GuideValuationRequest> Requests { get; } = [];

        public ValuationSource Source => source;

        public Task<GuideValuationQuote> GetAsync(GuideValuationRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new GuideValuationQuote(retail, trade, request.GuideMonth, request.Mileage));
        }
    }

    private sealed class RecordingValuationSection(Guid caseId) :
        IListCaseValuations,
        IListAppliedValuations,
        IListValuationPresets,
        IMarketResearchQueries,
        IStartMarketResearch,
        IApplyValuationCalculation
    {
        private static readonly DateTimeOffset RecordedAt = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        private readonly List<CaseValuation> guides = [];
        private readonly List<ValuationPreset> presets = [];
        private AiJobRecord? pending;

        public List<StartMarketResearchRequest> Started { get; } = [];

        public List<ApplyValuationRequest> Applied { get; } = [];

        private AppliedValuation? adopted;

        /// <summary>Substitutes every port the section reads and posts through.</summary>
        public void Register(IServiceCollection services)
        {
            Substitute<IListCaseValuations>(services, this);
            Substitute<IListAppliedValuations>(services, this);
            Substitute<IListValuationPresets>(services, this);
            Substitute<IMarketResearchQueries>(services, this);
            Substitute<IStartMarketResearch>(services, this);
            Substitute<IApplyValuationCalculation>(services, this);
        }

        /// <summary>
        /// A recorded adoption over <paramref name="basis"/>: a 10 per cent
        /// prior total loss, commercial VAT, one preset increase at
        /// <paramref name="amount"/> and a £250 condition deduction.
        /// </summary>
        public AppliedValuation SetApplied(CaseValuation basis, ValuationPreset preset, decimal amount)
        {
            var retail = basis.Details.RetailValue;
            var calculation = new ValuationCalculation(
                retail, true, retail * 0.2m, retail * 1.2m, 0.10m, retail * 0.12m,
                [new ValuationAddition(preset.Id, preset.Version, preset.Label, preset.SuggestedAmount, amount)],
                amount, 250m, retail * 1.08m + amount - 250m);
            adopted = new AppliedValuation(
                Guid.NewGuid(),
                caseId,
                4,
                basis.ValuationId,
                Pegasus.Web.Pages.Cases.DetailsModel.StampOf(basis),
                calculation,
                calculation.Proposal,
                "test",
                RecordedAt,
                "Engineer's Value applied.",
                "test");
            return adopted;
        }

        public CaseValuation AddGuide(ValuationSource source, decimal retail, decimal trade)
        {
            var valuation = new CaseValuation(
                Guid.NewGuid(),
                caseId,
                new ValuationDetails(source, new DateOnly(2031, 5, 6), new TimeOnly(9, 30), 42_000, retail, trade, new DateOnly(2031, 5, 1)),
                Guid.NewGuid().ToString("D"),
                RecordedAt.AddMinutes(guides.Count));
            guides.Add(valuation);
            return valuation;
        }

        public ValuationPreset AddPreset(string label, decimal suggestedAmount)
        {
            var preset = new ValuationPreset(
                Guid.NewGuid(), label, suggestedAmount, true, 3, Guid.NewGuid().ToString("D"), RecordedAt);
            presets.Add(preset);
            return preset;
        }

        public AiJobRecord SetPending(DateOnly guideMonth)
        {
            pending = new AiJobRecord(
                Guid.NewGuid(),
                AiJobKind.MarketResearch,
                AiJobSubjectKind.Case,
                caseId,
                "QDOS26001",
                MarketResearchPolicy.Instruction(guideMonth),
                null,
                null,
                AiJobState.Queued,
                ActorKind.Staff,
                Guid.NewGuid().ToString("D"),
                RecordedAt,
                RecordedAt.AddDays(1),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                1);
            return pending;
        }

        Task<IReadOnlyList<CaseValuation>> IListCaseValuations.ExecuteAsync(Guid forCase, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CaseValuation>>(forCase == caseId ? guides.ToArray() : []);

        Task<IReadOnlyList<AppliedValuation>> IListAppliedValuations.ExecuteAsync(Guid forCase, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AppliedValuation>>(forCase == caseId && adopted is not null ? [adopted] : []);

        public Task<IReadOnlyList<ValuationPreset>> ExecuteAsync(ActionActor actor, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ValuationPreset>>([.. presets]);

        public Task<AiJobRecord?> GetPendingAsync(Guid forCase, CancellationToken cancellationToken) =>
            Task.FromResult(forCase == caseId ? pending : null);

        public Task<AiJobRecord> ExecuteAsync(StartMarketResearchRequest request, CancellationToken cancellationToken)
        {
            Started.Add(request);
            return Task.FromResult(pending ?? SetPending(request.GuideMonth));
        }

        public Task<AppliedValuation> ExecuteAsync(ApplyValuationRequest request, CancellationToken cancellationToken)
        {
            Applied.Add(request);
            var basis = guides.Single(guide => guide.ValuationId == request.Selection.GuideValuationId);
            var calculation = new ValuationCalculation(
                basis.Details.RetailValue, false, 0m, basis.Details.RetailValue,
                request.Selection.PriorTotalLossPercentage, 0m, [], 0m, request.Selection.ConditionDeduction,
                basis.Details.RetailValue);
            return Task.FromResult(new AppliedValuation(
                Guid.NewGuid(),
                request.CaseId,
                request.ExpectedVersion + 1,
                request.Selection.GuideValuationId,
                request.GuideValuationStampUtc,
                calculation,
                calculation.Proposal,
                request.Actor.SubjectId,
                RecordedAt,
                request.Reason,
                "test"));
        }
    }
}
