using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Glass;
using Pegasus.Web.Presentation;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Valuation section on the one Case workspace (v33 design D, operator,
/// 6 October 2026): Glass's, Brego, Super CAP, CAP and Cazana are each one row
/// in both modes, whose boxes belong to the Case form so the Case's save
/// records a changed row (23 September 2026) and whose Get valuation fills
/// them in place from the connected provider; AI market research has its own
/// standing row whose Valuation month and Get valuation start the existing
/// job, a pending job reads as Researching there, and the Save posts a
/// changed calculation to the Core policy shape. The chosen source opens:
/// the calculation and the Retail, Trade and Engineer's Value boxes stand
/// under its row, the Engineer's Value is the one place the figure stands,
/// and its label carries the recorded calculation's source as one word.
/// A click on a card is the visible decision to use its figure, a source
/// with no connected provider says so before anything is pressed, and the
/// preview answers with the figures the Save will use or with its own reason
/// (operator, 28 September 2026).
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
    /// valuation button that asks by script and the card's hidden notice when
    /// its provider is connected, or the card's standing notice and no button
    /// when it is not (all five, here). Every card can be chosen.
    /// There is no card Save and no Add valuation dialog.
    /// </summary>
    [Fact]
    public async Task WhileEditingTheValuationSectionOffersTheMonthTheGuideSourcesAndTheResearchForm()
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        using var workspace = await EnterEngineerEditModeAsync(store, valuation.Register);

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");

        // AI market research asks from its own row; no row stands above the sources.
        Assert.DoesNotContain("data-valuation-tools", html, StringComparison.Ordinal);
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
            // A guide card carries no mileage: the Case's own is used (operator, 24 September 2026).
            Assert.DoesNotContain($"guideEntries[{index}].Mileage", card, StringComparison.Ordinal);
            foreach (var box in new[] { "RetailValue", "TradeValue", "GuideMonth" })
            {
                var input = Regex.Match(
                    card,
                    $"<input[^>]*name=\"guideEntries\\[{index}\\]\\.{box}\"[^>]*>",
                    RegexOptions.CultureInvariant);
                Assert.True(input.Success, $"The {source} card must render its {box} box.");
                Assert.Contains("form=\"case-edit-form\"", input.Value, StringComparison.Ordinal);
                Assert.DoesNotContain("required", input.Value, StringComparison.Ordinal);
            }
            // Get valuation fills the first box carrying each hook, so each
            // figure's hook is on its own box alone (QDOS26086: Glass's trade
            // figure landed in the retail box).
            Assert.Single(Regex.Matches(card, "<input[^>]*data-valuation-retail[^>]*>", RegexOptions.CultureInvariant));
            Assert.Single(Regex.Matches(card, "<input[^>]*data-valuation-trade[^>]*>", RegexOptions.CultureInvariant));
            InputTag(card, $"guideEntries[{index}].RetailValue", "data-valuation-retail");
            InputTag(card, $"guideEntries[{index}].TradeValue", "data-valuation-trade");
            // No provider is connected: the card says so now and offers no Get valuation.
            Assert.DoesNotContain("data-valuation-get", card, StringComparison.Ordinal);
            Assert.DoesNotContain("data-valuation-notice hidden", card, StringComparison.Ordinal);
            Assert.Contains("data-valuation-not-connected", card, StringComparison.Ordinal);
            Assert.Contains(
                CaseWorkspaceLabels.Valuation.UnavailableLead(Enum.Parse<ValuationSource>(name)),
                WebUtility.HtmlDecode(card),
                StringComparison.Ordinal);
            Assert.Contains("data-dialog-open=\"problem-dialog\"", card, StringComparison.Ordinal);
            Assert.Contains(CaseWorkspaceLabels.Valuation.ReportAProblem, card, StringComparison.Ordinal);
            // A click on the card is the decision to use it (operator, 8 October
            // 2026): no Use this value button, the whole card is the target, and a
            // card with no retail answers with its own sentence, hidden until then.
            Assert.DoesNotContain("data-valuation-use=", card, StringComparison.Ordinal);
            Assert.DoesNotContain("data-valuation-use ", card, StringComparison.Ordinal);
            Assert.Contains("tabindex=\"0\"", card, StringComparison.Ordinal);
            Assert.Matches(
                "data-valuation-needs-retail hidden[^>]*>" + Regex.Escape(CaseWorkspaceLabels.Valuation.UseNeedsRetail),
                WebUtility.HtmlDecode(card));
            Assert.Contains("data-valuation-chosen-word hidden", card, StringComparison.Ordinal);
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
        Assert.True(
            html.IndexOf("data-valuation-entry=\"cazana\"", StringComparison.Ordinal) < researchForm.Index,
            "AI market research stands as a row after the guide sources.");
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
        // The result is filed without ending the edit, and the card says so.
        Assert.Contains(CaseWorkspaceLabels.Valuation.ResearchFiledNote, html, StringComparison.Ordinal);
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
    /// One Save (23 September 2026): the calculator's controls and the Basis
    /// radios belong to the Case form, beside the calculation the page opened
    /// on; there is no Apply of its own. A Save whose calculation differs from
    /// the opening one carries it to Core as the policy shape (a prior total
    /// loss of 10 per cent is a fraction of 0.10) to record against its card.
    /// </summary>
    [Theory]
    [InlineData(StaffRole.Administrator)]
    [InlineData(StaffRole.Engineer)]
    [InlineData(StaffRole.User)]
    public async Task TheCaseSaveRecordsAChangedCalculationAsThePolicyShape(StaffRole role)
    {
        var store = new RecordingCaseDetailsStore { AcceptWorkspaceSaves = true };
        var valuation = new RecordingValuationSection(store.CaseId);
        var glasses = valuation.AddGuide(ValuationSource.Glasses, 12_500m, 10_250m);
        var preset = valuation.AddPreset("Tow bar", 150m);
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            valuation.Register(services);
            Substitute<ISaveCaseWorkspace>(services, store);
        }, role);

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");
        Assert.DoesNotContain("id=\"case-valuation-form\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-valuation-apply", html, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=ApplyValuation", html, StringComparison.Ordinal);
        Assert.Matches(
            $"name=\"selection.GuideValuationId\" value=\"{glasses.ValuationId:D}\"\\s+form=\"case-edit-form\"",
            html);
        Assert.Contains(
            "name=\"selection.PriorTotalLossPercentage\" value=\"10\" form=\"case-edit-form\"", html, StringComparison.Ordinal);
        Assert.Contains($"name=\"selection.AdditionPresetId\" value=\"{preset.Id:D}\" form=\"case-edit-form\"", html, StringComparison.Ordinal);
        var opening = WebUtility.HtmlDecode(InputValue(html, "selection.Opening"));
        Assert.Contains(glasses.ValuationId.ToString("D"), opening, StringComparison.Ordinal);

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(
                DetailsModelOperationKey,
                "Applied the calculation",
                ("selection.Opening", opening),
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

        AssertPrg(response, store.CaseId);
        var saved = Assert.Single(store.Saves);
        Assert.True(saved.Actor.IsInRole(role));
        var adoption = Assert.IsType<ValuationCalculationSelection>(saved.Valuation!.Adoption);
        Assert.Equal(glasses.ValuationId, adoption.GuideValuationId);
        Assert.Equal(0.10m, adoption.PriorTotalLossPercentage);
        Assert.True(adoption.CommercialVat);
        Assert.Equal(250m, adoption.ConditionDeduction);
        var addition = Assert.Single(adoption.Additions);
        Assert.Equal(preset.Id, addition.PresetId);
        Assert.Equal(preset.Version, addition.PresetVersion);
        Assert.Equal(175m, addition.Amount);
        Assert.Empty(saved.Valuation.GuideEntries!);
    }

    /// <summary>
    /// The Save records only a calculation that changed since the page opened
    /// (operator, 23 September 2026): an untouched calculator records nothing,
    /// and a changed figure on the basis card is a changed calculation.
    /// </summary>
    [Fact]
    public async Task AnUntouchedCalculatorRecordsNothingAndAChangedBasisFigureRecords()
    {
        var store = new RecordingCaseDetailsStore { AcceptWorkspaceSaves = true };
        var valuation = new RecordingValuationSection(store.CaseId);
        var glasses = valuation.AddGuide(ValuationSource.Glasses, 12_500m, 10_250m);
        valuation.AddPreset("Tow bar", 150m);
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            valuation.Register(services);
            Substitute<ISaveCaseWorkspace>(services, store);
        });
        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");
        var opening = WebUtility.HtmlDecode(InputValue(html, "selection.Opening"));
        // The opening names the basis card's trade as shown, since the
        // calculation is recorded against the card as shown.
        Assert.Contains("\"trade\":\"10250.00\"", opening, StringComparison.Ordinal);
        (string, string)[] Untouched(params (string, string)[] more) =>
        [
            ("selection.Opening", opening),
            ("selection.GuideValuationId", glasses.ValuationId.ToString("D")),
            ("selection.PriorTotalLossPercentage", ""),
            ("selection.ConditionDeduction", ""),
            ("selection.AdditionPresetId[0]", valuation.Presets[0].Id.ToString("D")),
            ("selection.AdditionPresetVersion[0]", valuation.Presets[0].Version.ToString(CultureInfo.InvariantCulture)),
            ("selection.AdditionLabel[0]", ""),
            ("selection.AdditionAmount[0]", "150"),
            .. more,
        ];

        using var untouched = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(DetailsModelOperationKey, "Saved the claim", Untouched(("claimNumber", "CLM-42"))));
        AssertPrg(untouched, store.CaseId);
        Assert.Null(Assert.Single(store.Saves).Valuation);

        using var changedCard = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(
                "5f5e5d5c5b5a59585756555453525150",
                "Corrected the Glass's retail",
                Untouched(
                    ("guideEntries[0].Source", nameof(ValuationSource.Glasses)),
                    ("guideEntries[0].GuideMonth", "2031-05"),
                    ("guideEntries[0].RetailValue", "13000.00"),
                    ("guideEntries[0].TradeValue", "10250.00"))));
        AssertPrg(changedCard, store.CaseId);
        var saved = store.Saves[1];
        Assert.Equal(13_000m, Assert.Single(saved.Valuation!.GuideEntries!).RetailValue);
        Assert.Equal(glasses.ValuationId, saved.Valuation.Adoption!.GuideValuationId);

        // A changed trade on the basis card is a changed calculation too.
        using var changedTrade = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(
                "6f6e6d6c6b6a69686766656463626160",
                "Corrected the Glass's trade",
                Untouched(
                    ("guideEntries[0].Source", nameof(ValuationSource.Glasses)),
                    ("guideEntries[0].GuideMonth", "2031-05"),
                    ("guideEntries[0].RetailValue", "12500.00"),
                    ("guideEntries[0].TradeValue", "10500.00"))));
        AssertPrg(changedTrade, store.CaseId);
        Assert.Equal(glasses.ValuationId, store.Saves[2].Valuation!.Adoption!.GuideValuationId);

        // The basis card echoed as recorded is no change.
        using var echoedCard = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(
                "7f7e7d7c7b7a79787776757473727170",
                "Saved the valuation unchanged",
                Untouched(
                    ("guideEntries[0].Source", nameof(ValuationSource.Glasses)),
                    ("guideEntries[0].GuideMonth", "2031-05"),
                    ("guideEntries[0].RetailValue", "12500.00"),
                    ("guideEntries[0].TradeValue", "10250.00"))));
        AssertPrg(echoedCard, store.CaseId);
        Assert.Null(store.Saves[3].Valuation);
    }

    /// <summary>
    /// A source with a connected provider is offered Get valuation and keeps
    /// its notice hidden until the request is refused; the sources without one
    /// say so on their cards now.
    /// </summary>
    [Fact]
    public async Task OnlyAConnectedSourceIsOfferedGetValuationBeforeAnyClick()
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            valuation.Register(services);
            services.AddSingleton<IGuideValuationProvider>(new FakeGuideValuationProvider(ValuationSource.Brego, 13_250m, 11_000m));
        });

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");

        var brego = EntryCard(html, "brego");
        Assert.Contains("data-valuation-get", brego, StringComparison.Ordinal);
        Assert.Contains("data-valuation-notice hidden", brego, StringComparison.Ordinal);
        Assert.DoesNotContain("data-valuation-not-connected", brego, StringComparison.Ordinal);
        foreach (var slug in new[] { "glasses", "super-cap", "cap", "cazana" })
        {
            var card = EntryCard(html, slug);
            Assert.DoesNotContain("data-valuation-get", card, StringComparison.Ordinal);
            Assert.Contains("data-valuation-not-connected", card, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Choosing a card (operator, 28 September 2026): the calculation is
    /// recorded against its basis card when the Engineer chose it, even
    /// when nothing changed since the page opened; a save that does not post
    /// it still records nothing. The two fields it switches on are on the
    /// page and post nothing until then.
    /// </summary>
    [Fact]
    public async Task UsingAnUnchangedDefaultValueIsRecordedAndAnUnrelatedSaveIsNot()
    {
        var store = new RecordingCaseDetailsStore { AcceptWorkspaceSaves = true };
        var valuation = new RecordingValuationSection(store.CaseId);
        var glasses = valuation.AddGuide(ValuationSource.Glasses, 12_500m, 10_250m);
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            valuation.Register(services);
            Substitute<ISaveCaseWorkspace>(services, store);
        });
        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");
        var opening = WebUtility.HtmlDecode(InputValue(html, "selection.Opening"));
        // Both fields are switched off until the button is pressed.
        Assert.Matches("<input type=\"hidden\" name=\"selection[.]Use\"[^>]* disabled", html);
        Assert.Matches("<input type=\"hidden\" name=\"selection[.]GuideSource\"[^>]* disabled", html);
        (string, string)[] Untouched(params (string, string)[] more) =>
        [
            ("selection.Opening", opening),
            ("selection.GuideValuationId", glasses.ValuationId.ToString("D")),
            ("selection.PriorTotalLossPercentage", ""),
            ("selection.ConditionDeduction", ""),
            .. more,
        ];

        using var unrelated = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(DetailsModelOperationKey, "Saved the claim", Untouched(("claimNumber", "CLM-42"))));
        AssertPrg(unrelated, store.CaseId);
        Assert.Null(Assert.Single(store.Saves).Valuation);

        using var used = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(
                "5f5e5d5c5b5a59585756555453525150",
                "Used the Glass's figure",
                Untouched(("selection.Use", "true"))));
        AssertPrg(used, store.CaseId);
        var adoption = Assert.IsType<ValuationCalculationSelection>(store.Saves[1].Valuation!.Adoption);
        Assert.Equal(glasses.ValuationId, adoption.GuideValuationId);
        Assert.Null(adoption.GuideSource);
    }

    /// <summary>
    /// A guide card typed in the same edit has no identity yet, so Use this
    /// value names it by its source and the Save carries the typed card with
    /// the calculation. A card with no retail has nothing to use, and the Save
    /// is refused rather than recording nothing silently.
    /// </summary>
    [Fact]
    public async Task UsingACardTypedInTheSameEditNamesItBySource()
    {
        var store = new RecordingCaseDetailsStore { AcceptWorkspaceSaves = true };
        var valuation = new RecordingValuationSection(store.CaseId);
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            valuation.Register(services);
            Substitute<ISaveCaseWorkspace>(services, store);
        });
        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");
        var opening = WebUtility.HtmlDecode(InputValue(html, "selection.Opening"));

        using var typed = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(
                DetailsModelOperationKey,
                "Used a typed Brego figure",
                ("selection.Opening", opening),
                ("selection.GuideSource", nameof(ValuationSource.Brego)),
                ("selection.Use", "true"),
                ("selection.PriorTotalLossPercentage", ""),
                ("selection.ConditionDeduction", ""),
                ("guideEntries[0].Source", nameof(ValuationSource.Brego)),
                ("guideEntries[0].GuideMonth", "2031-05"),
                ("guideEntries[0].RetailValue", "9800.00"),
                ("guideEntries[0].TradeValue", "")));
        AssertPrg(typed, store.CaseId);
        var saved = Assert.Single(store.Saves);
        Assert.Equal(9_800m, Assert.Single(saved.Valuation!.GuideEntries!).RetailValue);
        var adoption = Assert.IsType<ValuationCalculationSelection>(saved.Valuation.Adoption);
        Assert.Equal(Guid.Empty, adoption.GuideValuationId);
        Assert.Equal(ValuationSource.Brego, adoption.GuideSource);

        // Nothing typed on the card: there is no figure to use.
        using var empty = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(
                "6f6e6d6c6b6a69686766656463626160",
                "Used an empty Cap card",
                ("selection.Opening", opening),
                ("selection.GuideSource", nameof(ValuationSource.Cap)),
                ("selection.Use", "true"),
                ("selection.PriorTotalLossPercentage", ""),
                ("selection.ConditionDeduction", "")));
        Assert.Single(store.Saves);
    }

    /// <summary>
    /// The preview is what the Save uses (operator, 28 September 2026): the
    /// retail as typed and the claimant's VAT position as the form holds it
    /// reach the port, which is the one Core owner.
    /// </summary>
    [Fact]
    public async Task ThePreviewCarriesTheUnsavedRetailAndTheClaimantVatTheFormHolds()
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        var glasses = valuation.AddGuide(ValuationSource.Glasses, 12_500m, 10_250m);
        var preview = new RecordingPreview();
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            valuation.Register(services);
            Substitute<IPreviewValuationCalculation>(services, preview);
        });

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=PreviewValuation",
            Form(
                workspace.AntiforgeryToken,
                ("selection.GuideValuationId", glasses.ValuationId.ToString("D")),
                ("selection.CommercialVat", "true"),
                ("basisRetail", "13000.00"),
                (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.SettlementClaimantVatRegistered), "true")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var asked = Assert.Single(preview.Requests);
        Assert.Equal(13_000m, asked.GuideRetailValue);
        Assert.True(asked.ClaimantVatRegistered);
        Assert.Equal(glasses.ValuationId, asked.Selection.GuideValuationId);

        // Left out, the preview reads what is recorded.
        using var recorded = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=PreviewValuation",
            Form(workspace.AntiforgeryToken, ("selection.GuideValuationId", glasses.ValuationId.ToString("D"))));
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        Assert.Null(preview.Requests[1].GuideRetailValue);
        Assert.Null(preview.Requests[1].ClaimantVatRegistered);

        // A retail box the Engineer cleared is posted empty and means "no
        // retail", as the Save reads it: never the recorded card's figure.
        using var cleared = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=PreviewValuation",
            Form(
                workspace.AntiforgeryToken,
                ("selection.GuideValuationId", glasses.ValuationId.ToString("D")),
                ("basisRetail", "")));
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Equal(0m, preview.Requests[2].GuideRetailValue);
    }

    /// <summary>
    /// A calculation that cannot be worked out answers with Core's own
    /// reason as an alert, never as "None yet" (operator, 28 September 2026);
    /// with no basis chosen there is honestly nothing yet.
    /// </summary>
    [Fact]
    public async Task APreviewThatCannotBeWorkedOutSaysWhyAndNoBasisSaysNoneYet()
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        var glasses = valuation.AddGuide(ValuationSource.Glasses, 1_000m, 800m);
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            valuation.Register(services);
            Substitute<IPreviewValuationCalculation>(
                services,
                new RefusingPreview(new InvalidOperationException(
                    "The valuation deductions exceed the value, so there is no figure to apply.")));
        });

        using var refused = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=PreviewValuation",
            Form(
                workspace.AntiforgeryToken,
                ("selection.GuideValuationId", glasses.ValuationId.ToString("D")),
                ("selection.ConditionDeduction", "5000")));
        var refusedHtml = await refused.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("data-valuation-error", refusedHtml, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", refusedHtml, StringComparison.Ordinal);
        Assert.Contains("deductions exceed the value", refusedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(CaseWorkspaceLabels.Valuation.NoneYet, refusedHtml, StringComparison.Ordinal);

        using var noBasis = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=PreviewValuation",
            Form(workspace.AntiforgeryToken, ("selection.ConditionDeduction", "")));
        var noBasisHtml = await noBasis.Content.ReadAsStringAsync();
        Assert.Contains(CaseWorkspaceLabels.Valuation.NoneYet, noBasisHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("data-valuation-error", noBasisHtml, StringComparison.Ordinal);
    }

    /// <summary>
    /// The chosen card is the basis (operator, 8 October 2026): it offers the
    /// figures it shows and says Selected, the Engineer's Value is the one box
    /// and carries the hook the script fills, the report's Retail and Trade
    /// have no box, the calculation stands once below every card, and it
    /// answers its proposal as the figure the Engineer's Value box takes.
    /// </summary>
    [Fact]
    public async Task ACardChosenAsTheBasisOffersTheFiguresTheEngineersValueTakes()
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        var glasses = valuation.AddGuide(ValuationSource.Glasses, 12_500m, 10_250m);
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            valuation.Register(services);
            Substitute<IPreviewValuationCalculation>(services, new CalculatingPreview(12_500m));
        });
        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");

        var card = EntryCard(html, "glasses");
        Assert.Contains("data-retail=\"12500.00\"", card, StringComparison.Ordinal);
        Assert.Contains("data-trade=\"10250.00\"", card, StringComparison.Ordinal);
        Assert.Contains("class=\"valuation-card entry sel\"", card, StringComparison.Ordinal);
        Assert.Matches("data-valuation-chosen-word>Selected<", card);
        var box = InputTag(
            html,
            CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.ValueEngineer),
            "data-valuation-value=\"engineer\"");
        Assert.Contains("form=\"case-edit-form\"", box, StringComparison.Ordinal);
        Assert.DoesNotContain("data-valuation-value=\"retail\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-valuation-value=\"trade\"", html, StringComparison.Ordinal);
        var research = html.IndexOf("valuation-card--research", StringComparison.Ordinal);
        var calculator = html.IndexOf("data-valuation-calc", StringComparison.Ordinal);
        var value = html.IndexOf("data-valuation-values", StringComparison.Ordinal);
        Assert.True(
            html.IndexOf("data-valuation-entry=\"cazana\"", StringComparison.Ordinal) < research
                && research < calculator && calculator < value,
            "The calculation, then the Engineer's Value, stand once below every card.");
        Assert.DoesNotContain("data-valuation-open", html, StringComparison.Ordinal);
        // One figure: no second total and no applied block.
        Assert.DoesNotContain("Proposed Engineer", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Applied Engineer", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-valuation-history", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Choose a basis card", html, StringComparison.Ordinal);

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=PreviewValuation",
            Form(
                workspace.AntiforgeryToken,
                ("selection.GuideValuationId", glasses.ValuationId.ToString("D")),
                ("selection.CommercialVat", "true")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("data-valuation-proposal=\"15000.00\"", preview, StringComparison.Ordinal);
        // Each adjustment's amount goes to its own cell; there are no lines.
        Assert.Contains("data-valuation-vat-amount=\"+ £2,500.00\"", preview, StringComparison.Ordinal);
        Assert.Contains("data-valuation-ptl-amount=\"\"", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("Guide retail", preview, StringComparison.Ordinal);
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
    /// as dashes, and none carries a control, a card Save or Get valuation.
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
            Assert.Contains("class=\"valuation-card-value", card, StringComparison.Ordinal);
            Assert.DoesNotContain("<input", card, StringComparison.Ordinal);
            Assert.DoesNotContain("tabindex", card, StringComparison.Ordinal);
            Assert.DoesNotContain("data-valuation-get", card, StringComparison.Ordinal);
        }
        Assert.Contains(
            ValuationCalculationPolicy.FormatMoney(12_500m),
            WebUtility.HtmlDecode(EntryCard(html, "glasses")),
            StringComparison.Ordinal);
        // An empty card reads as dashes, not words.
        Assert.Equal(3, Regex.Count(WebUtility.HtmlDecode(EntryCard(html, "brego")), "is-blank\">—<", RegexOptions.CultureInvariant));
        Assert.DoesNotContain(
            Pegasus.Web.Presentation.OperatorLabels.CaseWorkspace.AbsentValue,
            EntryCard(html, "brego"),
            StringComparison.Ordinal);
        Assert.DoesNotContain("guideEntries[", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-valuation-empty", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The calculator opens on the recorded calculation while the Engineer's
    /// Value holds its figure, in both modes: read mode lists the applied
    /// increase with its tick, and editing starts from the same selection
    /// rather than from blank. The Engineer's Value label carries
    /// the recorded source as its one word, the section head reads the saved
    /// figure, and each adjustment's amount stands in its own cell.
    /// </summary>
    [Fact]
    public async Task TheCalculatorOpensOnTheAppliedSelection()
    {
        var store = new RecordingCaseDetailsStore { EngineerValue = "13425.00" };
        var valuation = new RecordingValuationSection(store.CaseId);
        var glasses = valuation.AddGuide(ValuationSource.Glasses, 12_500m, 10_250m);
        var towBar = valuation.AddPreset("Tow bar", 150m);
        valuation.AddPreset("Roof bars", 90m);
        valuation.SetApplied(glasses, towBar, 175m);

        var read = await ReadValuationAsync(store, valuation);
        // Reading lists what the calculation applied, not every preset: the
        // tow bar and Add 20 % VAT, which is a row of the value increases.
        Assert.DoesNotContain("Roof bars", read, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(read, "data-valuation-applied-addition=\"true\"", RegexOptions.CultureInvariant));
        var decoded = WebUtility.HtmlDecode(read);
        Assert.Matches("<span class=\"src-tag\" data-valuation-recorded-word>Glass's</span>", decoded);
        Assert.Matches("data-valuation-head>Engineer's Value £13,425.00<", decoded);
        Assert.Matches("data-valuation-amount=\"vat\">\\+ £2,500.00<", decoded);
        Assert.Matches("data-valuation-amount=\"ptl\">− £1,500.00<", decoded);
        Assert.DoesNotContain("Proposed Engineer", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("Applied Engineer", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain(CaseWorkspaceLabels.Valuation.NoneYet, decoded, StringComparison.Ordinal);
        // Reading, the recorded source is the one Selected.
        Assert.Contains("class=\"valuation-card entry sel\"", EntryCard(decoded, "glasses"), StringComparison.Ordinal);
        Assert.Single(Regex.Matches(decoded, "data-valuation-chosen-word>Selected<", RegexOptions.CultureInvariant));

        using var workspace = await EnterEngineerEditModeAsync(store, valuation.Register);
        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");
        Assert.Matches("<input type=\"checkbox\" id=\"f-valuation-ptl\" checked=\"checked\" data-valuation-ptl-toggle />", html);
        Assert.Matches("name=\"selection.PriorTotalLossPercentage\" value=\"10\" form=\"case-edit-form\" checked=\"checked\"", html);
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
    /// A figure typed over a recorded calculation is the Engineer's own
    /// (operator, 6 October 2026): the recorded source's word is not shown
    /// beside it, no source opens while reading, and the calculator opens
    /// blank rather than on the earlier calculation. A Case with no
    /// calculation recorded says nothing of one; it never reads "None yet"
    /// beside a figure.
    /// </summary>
    [Fact]
    public async Task ATypedFigureOverARecordedCalculationShowsNoRecord()
    {
        var store = new RecordingCaseDetailsStore { EngineerValue = "9999.00" };
        var valuation = new RecordingValuationSection(store.CaseId);
        var glasses = valuation.AddGuide(ValuationSource.Glasses, 12_500m, 10_250m);
        var towBar = valuation.AddPreset("Tow bar", 150m);
        valuation.SetApplied(glasses, towBar, 175m);

        var read = WebUtility.HtmlDecode(await ReadValuationAsync(store, valuation));
        Assert.DoesNotContain("data-valuation-recorded-word", read, StringComparison.Ordinal);
        Assert.DoesNotContain("data-valuation-applied-addition=\"true\"", read, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"valuation-card entry sel\"", read, StringComparison.Ordinal);
        Assert.DoesNotContain("from Glass's retail", read, StringComparison.Ordinal);
        Assert.DoesNotContain(CaseWorkspaceLabels.Valuation.NoneYet, read, StringComparison.Ordinal);
        Assert.Matches("data-valuation-head>Engineer's Value £9,999.00<", read);
        Assert.DoesNotContain("data-valuation-chosen-word>", read, StringComparison.Ordinal);

        using var workspace = await EnterEngineerEditModeAsync(store, valuation.Register);
        var html = WebUtility.HtmlDecode(
            await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation"));
        Assert.Matches("<input type=\"checkbox\" id=\"f-valuation-ptl\" data-valuation-ptl-toggle />", html);
        Assert.DoesNotMatch("PriorTotalLossPercentage\" value=\"(10|20)\" form=\"case-edit-form\" checked", html);
        Assert.DoesNotContain("<div class=\"add on\"", html, StringComparison.Ordinal);
        // The word's place is there for the script, and hidden.
        Assert.Matches("data-valuation-recorded-word hidden=\"hidden\"></span>", html);
    }

    /// <summary>
    /// A commit answered in place carries the Case as saved for the section
    /// to redraw without a reload (operator, 6 October 2026): the head's
    /// figure, and the recorded calculation's figure and source word while
    /// the Engineer's Value holds it. The carrier is disabled, so it posts
    /// nothing with the next save.
    /// </summary>
    [Fact]
    public async Task ACommitAnsweredInPlaceCarriesTheSavedHeadAndTheRecordedSource()
    {
        var store = new RecordingCaseDetailsStore { AcceptWorkspaceSaves = true, EngineerValue = "13425.00" };
        var valuation = new RecordingValuationSection(store.CaseId);
        var glasses = valuation.AddGuide(ValuationSource.Glasses, 12_500m, 10_250m);
        var towBar = valuation.AddPreset("Tow bar", 150m);
        valuation.SetApplied(glasses, towBar, 175m);
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            valuation.Register(services);
            Substitute<ISaveCaseWorkspace>(services, store);
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Cases/{store.CaseId:D}?handler=Save")
        {
            Content = workspace.MutationForm(DetailsModelOperationKey, "Saved as it was made")
        };
        request.Headers.Add("X-Requested-With", "fetch");
        using var response = await workspace.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var answer = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("id=\"section-valuation\"", answer, StringComparison.Ordinal);
        var carrier = Regex.Match(
            answer,
            "<input[^>]*name=\"valuationSaved\"[^>]*>",
            RegexOptions.CultureInvariant);
        Assert.True(carrier.Success, "The commit answer must carry the Valuation section's saved state.");
        Assert.Contains("form=\"case-edit-form\"", carrier.Value, StringComparison.Ordinal);
        Assert.Contains(" disabled", carrier.Value, StringComparison.Ordinal);
        Assert.Contains("data-carry-forward", carrier.Value, StringComparison.Ordinal);
        using var saved = JsonDocument.Parse(WebUtility.HtmlDecode(InputValue(answer, "valuationSaved")));
        Assert.Equal("Engineer's Value £13,425.00", saved.RootElement.GetProperty("head").GetString());
        Assert.Equal("13425.00", saved.RootElement.GetProperty("value").GetString());
        Assert.Equal("Glass's", saved.RootElement.GetProperty("word").GetString());
        Assert.False(saved.RootElement.GetProperty("research").GetBoolean());
    }

    /// <summary>
    /// A row still showing its source's latest recorded figures is not
    /// recorded again by the Case save; only a changed row is carried.
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
                ("guideEntries[0].RetailValue", "12500.00"),
                ("guideEntries[0].TradeValue", "10250.00"),
                ("guideEntries[1].Source", nameof(ValuationSource.Brego)),
                ("guideEntries[1].GuideMonth", "2031-05"),
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
        // The card has no mileage box to fill; the lookup asked with the Case's own.
        Assert.False(figures.TryGetProperty("mileage", out _));
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

    /// <summary>
    /// Glass's connected (ADR-0060): the card is offered Get valuation, which
    /// answers Retail Transacted and Glass's Trade for the posted month, and
    /// the valuation's report — the stocked vehicle's "Values Only" print over
    /// the same signed-in session — is filed after the figures have answered.
    /// The press itself records nothing on the Case.
    /// </summary>
    [Fact]
    public async Task GlassesGetValuationAnswersTheFiguresAndFilesItsReportAfterwards()
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        var mva = ScriptedGlassFor42000Miles();
        var filing = new RecordingReportFiling();
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            valuation.Register(services);
            Substitute<ICaseDataQueries>(services, store);
            ConnectGlass(services, mva);
            services.AddScoped<IFileGuideValuationReport>(_ => filing);
        });

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");
        Assert.Contains("data-valuation-get", EntryCard(html, "glasses"), StringComparison.Ordinal);
        Assert.DoesNotContain("data-valuation-get", EntryCard(html, "brego"), StringComparison.Ordinal);

        using var response = await PostGetValuationAsync(workspace, ValuationSource.Glasses, "2026-08", asJson: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var answer = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", answer.RootElement.GetProperty("status").GetString());
        Assert.Equal("17717.00", answer.RootElement.GetProperty("retail").GetString());
        Assert.Equal("15600.00", answer.RootElement.GetProperty("trade").GetString());
        Assert.Equal("2026-08", answer.RootElement.GetProperty("guideMonth").GetString());
        // The report is fetched after the answer; the fake's request list is
        // read only once filing has finished with it.
        var (filed, pdf) = await filing.Filed.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Contains(mva.Requests, request =>
            request.Path == "/index/create-new-vehicle/value/0/valuate/1/mileage/42000/valdate/202608/condition/false");
        Assert.Equal(store.CaseId, filed.CaseId);
        Assert.Equal(ValuationSource.Glasses, filed.Source);
        Assert.Equal("AB12CDE", filed.Registration);
        Assert.Equal(new DateOnly(2026, 8, 1), filed.GuideMonth);
        Assert.Equal("glass-stock:" + GlassProviderFixture.VehicleId, filed.Report.Identity);
        Assert.Equal(GlassProviderFixture.ReportPdf, pdf);
        Assert.Empty(store.Saves);
    }

    /// <summary>
    /// A valuation Glass's cannot make answers the card's approved notice and
    /// stocks nothing, so no report is filed.
    /// </summary>
    [Fact]
    public async Task AGlassesValuationThatIsNotPossibleAnswersUnavailableAndFilesNothing()
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        var mva = ScriptedGlassFor42000Miles();
        mva.Set("GET /three-phase-vehicle/get-values", new(HttpStatusCode.OK, GlassProviderFixture.ValuationNotPossible));
        var filing = new RecordingReportFiling();
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            valuation.Register(services);
            Substitute<ICaseDataQueries>(services, store);
            ConnectGlass(services, mva);
            services.AddScoped<IFileGuideValuationReport>(_ => filing);
        });

        using var response = await PostGetValuationAsync(workspace, ValuationSource.Glasses, "2026-08", asJson: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var answer = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("unavailable", answer.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, mva.Count("GET /index/create-new-vehicle"));
        Assert.False(filing.Filed.Task.IsCompleted);
        Assert.Empty(store.Saves);
    }

    /// <summary>
    /// A vehicle older than Glass's values answers the card's own approved
    /// sentence (operator, 2 October 2026), not "unavailable", and files nothing.
    /// </summary>
    [Fact]
    public async Task AGlassesVehicleTooOldToValueAnswersItsOwnSentenceAndFilesNothing()
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        var mva = ScriptedGlassFor42000Miles();
        mva.Set("GET /three-phase-vehicle/get-values", new(HttpStatusCode.OK, GlassProviderFixture.ValuationVehicleAge));
        var filing = new RecordingReportFiling();
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            valuation.Register(services);
            Substitute<ICaseDataQueries>(services, store);
            ConnectGlass(services, mva);
            services.AddScoped<IFileGuideValuationReport>(_ => filing);
        });

        using var response = await PostGetValuationAsync(workspace, ValuationSource.Glasses, "2026-08", asJson: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var answer = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("not_valued", answer.RootElement.GetProperty("status").GetString());
        Assert.Equal(
            "Glass's cannot value this vehicle because of its age: Glass's values cars and motorcycles up to 20 years old and light commercial vehicles up to 15.",
            answer.RootElement.GetProperty("message").GetString());
        Assert.Equal(0, mva.Count("GET /index/create-new-vehicle"));
        Assert.False(filing.Filed.Task.IsCompleted);
        Assert.Empty(store.Saves);
    }

    /// <summary>
    /// A registration Cazana holds no data for answers the card's own approved
    /// sentence (operator, 9 October 2026), not "unavailable", and records nothing.
    /// </summary>
    [Fact]
    public async Task ACazanaRegistrationWithNoDataAnswersItsOwnSentence()
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            valuation.Register(services);
            Substitute<ICaseDataQueries>(services, store);
            services.AddSingleton<IGuideValuationProvider>(
                new NotValuedGuideValuationProvider(ValuationSource.Cazana, GuideValuationNotValuedReason.NoVehicleData));
        });

        using var response = await PostGetValuationAsync(workspace, ValuationSource.Cazana, "2026-08", asJson: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var answer = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("not_valued", answer.RootElement.GetProperty("status").GetString());
        Assert.Equal(
            "Cazana cannot value this vehicle: it holds no data for its registration.",
            answer.RootElement.GetProperty("message").GetString());
        Assert.Empty(store.Saves);
    }

    /// <summary>The scripted Glass's, answering the Case's own 42,000 miles.</summary>
    private static ScriptedGlass ScriptedGlassFor42000Miles()
    {
        var mva = new ScriptedGlass();
        GlassProviderFixture.Script(mva);
        mva.Set(
            "GET /index/search-vrm/vrms_reg_no/AB12CDE/valuate/1/vrms_mileage/42000",
            new(HttpStatusCode.OK, "{\"stockcount\":0,\"vehicle_id\":0,\"vrm_lookup\":1,\"natcode\":\""
                + GlassProviderFixture.NatCode + "\"}"));
        return mva;
    }

    /// <summary>Glass's as Production composes it, over the scripted provider.</summary>
    private static void ConnectGlass(IServiceCollection services, ScriptedGlass mva)
    {
        services.AddSingleton(new GlassRepairEstimateOptions(
            GlassProviderFixture.MvaBase,
            GlassProviderFixture.EstimatorBase,
            GlassProviderFixture.CallbackBase,
            GlassProviderFixture.ProfileId,
            ExportPollInterval: TimeSpan.FromMilliseconds(5),
            ExportTimeout: TimeSpan.FromMilliseconds(50),
            MaximumExportBytes: 16 * 1024 * 1024));
        services.Configure<HttpClientFactoryOptions>(
            GlassRepairEstimateOptions.HttpClientName,
            options => options.HttpMessageHandlerBuilderActions.Add(handler => handler.PrimaryHandler = mva));
        services.AddGlassGuideValuation(_ => GlassValuationAccount.Create(key =>
            key.EndsWith("Username", StringComparison.Ordinal) ? "valuation-test" : "synthetic-password"));
    }

    /// <summary>Records the filing the press handed on, and reads its report as filing would.</summary>
    private sealed class RecordingReportFiling : IFileGuideValuationReport
    {
        public TaskCompletionSource<(FileGuideValuationReportRequest Request, byte[] Pdf)> Filed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<Pegasus.Core.Custody.CaseArtifactCustodyResult> ExecuteAsync(
            FileGuideValuationReportRequest request, CancellationToken cancellationToken)
        {
            Filed.TrySetResult((request, await request.Report.FetchPdfAsync(cancellationToken)));
            return new(Pegasus.Core.Custody.CaseArtifactCustodyDisposition.Confirmed,
                null, null, null, null, null, null, null, null, null, null);
        }
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
                Substitute<IGetCaseEditBasis>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseVehicleSection>(services, store);
                Substitute<IGetCaseValuationSection>(services, store);
                Substitute<IGetCaseNotesSection>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
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
    /// One source's row: from its opening tag to the next row, or to the
    /// calculation when the row is the one that opens. The row holds nested
    /// cells, so it is sliced, not matched.
    /// </summary>
    private static string EntryCard(string html, string source)
    {
        var hook = html.IndexOf($"data-valuation-entry=\"{source}\"", StringComparison.Ordinal);
        Assert.True(hook >= 0, $"The Valuation section must render the card for '{source}'.");
        var start = html.LastIndexOf("<div", hook, StringComparison.Ordinal);
        var next = html.IndexOf("class=\"valuation-card ", hook, StringComparison.Ordinal);
        var calc = html.IndexOf("data-valuation-calc", hook, StringComparison.Ordinal);
        var end = new[] { next, calc }.Where(index => index > hook).DefaultIfEmpty(html.Length).Min();
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

    private sealed class NotValuedGuideValuationProvider(ValuationSource source, GuideValuationNotValuedReason reason)
        : IGuideValuationProvider
    {
        public ValuationSource Source => source;

        public Task<GuideValuationQuote> GetAsync(GuideValuationRequest request, CancellationToken cancellationToken) =>
            throw new GuideValuationNotValuedException(source, reason);
    }

    /// <summary>A preview that records what it was asked and answers a fixed calculation.</summary>
    private sealed class RecordingPreview : IPreviewValuationCalculation
    {
        public List<PreviewValuationRequest> Requests { get; } = [];

        public Task<ValuationPreview> ExecuteAsync(PreviewValuationRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new ValuationPreview(
                request.Selection.GuideValuationId,
                ValuationCalculationPolicy.Calculate(new ValuationCalculationInput(
                    request.GuideRetailValue ?? 12_500m, false, false, null, [], 0m))));
        }
    }

    /// <summary>A preview that Core would refuse, with the reason it gives.</summary>
    private sealed class RefusingPreview(Exception refusal) : IPreviewValuationCalculation
    {
        public Task<ValuationPreview> ExecuteAsync(PreviewValuationRequest request, CancellationToken cancellationToken) =>
            throw refusal;
    }

    /// <summary>The calculator's arithmetic over one basis retail, as Core computes it.</summary>
    private sealed class CalculatingPreview(decimal basisRetail) : IPreviewValuationCalculation
    {
        public Task<ValuationPreview> ExecuteAsync(PreviewValuationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ValuationPreview(
                request.Selection.GuideValuationId,
                ValuationCalculationPolicy.Calculate(new ValuationCalculationInput(
                    basisRetail,
                    request.Selection.CommercialVat,
                    false,
                    request.Selection.PriorTotalLossPercentage,
                    [],
                    request.Selection.ConditionDeduction))));
    }

    /// <remarks>
    /// The page takes the pending research from the Case's AI jobs, so the
    /// section's jobs are this Case's pending job, if one is set.
    /// </remarks>
    private sealed class RecordingValuationSection(Guid caseId) :
        IListCaseValuations,
        IListAppliedValuations,
        IListValuationPresets,
        IAiJobQueries,
        IStartMarketResearch
    {
        private static readonly DateTimeOffset RecordedAt = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        private readonly List<CaseValuation> guides = [];
        private readonly List<ValuationPreset> presets = [];
        private AiJobRecord? pending;

        public List<StartMarketResearchRequest> Started { get; } = [];

        public List<ValuationPreset> Presets => presets;

        private AppliedValuation? adopted;

        /// <summary>Substitutes every port the section reads and posts through.</summary>
        public void Register(IServiceCollection services)
        {
            Substitute<IListCaseValuations>(services, this);
            Substitute<IListAppliedValuations>(services, this);
            Substitute<IListValuationPresets>(services, this);
            Substitute<IAiJobQueries>(services, this);
            Substitute<IStartMarketResearch>(services, this);
        }

        /// <summary>
        /// A recorded adoption over <paramref name="basis"/>: a 10 per cent
        /// prior total loss, commercial VAT, one preset increase at
        /// <paramref name="amount"/> and a £250 condition deduction.
        /// </summary>
        public AppliedValuation SetApplied(CaseValuation basis, ValuationPreset preset, decimal amount)
        {
            var retail = basis.Details.RetailValue!.Value;
            var calculation = new ValuationCalculation(
                retail, true, retail * 0.2m, retail * 1.2m, 0.10m, retail * 0.12m,
                [new ValuationAddition(preset.Id, preset.Version, preset.Label, preset.SuggestedAmount, amount)],
                amount, 250m, retail * 1.08m + amount - 250m);
            adopted = new AppliedValuation(
                Guid.NewGuid(),
                caseId,
                4,
                basis.ValuationId,
                basis.LastEditedAtUtc ?? basis.RecordedAtUtc,
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

        Task<IReadOnlyList<CaseValuation>> IListCaseValuations.ExecuteAsync(Guid forCase, CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CaseValuation>>(forCase == caseId ? guides.ToArray() : []);

        Task<IReadOnlyList<AppliedValuation>> IListAppliedValuations.ExecuteAsync(Guid forCase, CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AppliedValuation>>(forCase == caseId && adopted is not null ? [adopted] : []);

        public Task<IReadOnlyList<ValuationPreset>> ExecuteAsync(ActionActor actor, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ValuationPreset>>([.. presets]);

        public Task<IReadOnlyList<AiJobRecord>> ListForSubjectAsync(Guid subjectId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AiJobRecord>>(subjectId == caseId && pending is not null ? [pending] : []);

        public Task<IReadOnlyList<AiJobRecord>> ListOpenAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AiJobQueryPage> ListOpenPageAsync(
            AiJobKind? kind,
            string grantId,
            DateTimeOffset? afterCreatedAtUtc,
            Guid? afterJobId,
            int limit,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<AiJobRecord>> ListRecentAsync(int max, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AiJobCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AiJobRecord> ExecuteAsync(StartMarketResearchRequest request, CancellationToken cancellationToken)
        {
            Started.Add(request);
            return Task.FromResult(pending ?? SetPending(request.GuideMonth));
        }
    }
}
