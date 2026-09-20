using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Web.Presentation;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The v26 Valuation section on the one Case workspace: while editing,
/// Glass's, Brego and Super CAP are each one entry card whose Get valuation
/// fills its boxes from the connected provider and whose Save records them
/// (one route to a card), the Valuation month and AI market research start
/// the existing job for the month, a pending job shows as a Researching
/// card, and Apply as Engineer's Value posts the calculator's selection to
/// the Core policy shape.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseValuationV26WebTests
{
    /// <summary>
    /// The edit session renders the month input on the research form, the AI
    /// market research button, and one entry card per guide source: its own
    /// form posting SaveValuation with month, mileage, retail and trade boxes,
    /// and a Get valuation button posting the same boxes to GetValuation.
    /// There is no Add valuation dialog.
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

        foreach (var (source, name) in new[] { ("glasses", "Glasses"), ("brego", "Brego"), ("super-cap", "SuperCap") })
        {
            var card = EntryCard(html, source);
            Assert.Contains("handler=SaveValuation", card, StringComparison.Ordinal);
            foreach (var box in new[] { "retailValue", "tradeValue", "mileage", "guideMonth" })
            {
                Assert.Contains($"name=\"{box}\"", card, StringComparison.Ordinal);
            }
            Assert.Contains($"name=\"source\" value=\"{name}\"", card, StringComparison.Ordinal);
            var button = ButtonTag(html, source);
            Assert.Contains("type=\"submit\"", button, StringComparison.Ordinal);
            Assert.Contains("handler=GetValuation", button, StringComparison.Ordinal);
            Assert.Contains("source=" + name, button, StringComparison.Ordinal);
            Assert.DoesNotContain("form=", button, StringComparison.Ordinal);
            ButtonTagByHook(html, $"data-valuation-save=\"{source}\"");
        }
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
    [Fact]
    public async Task StartMarketResearchReachesThePortWithThePostedMonthAndKeepsTheSession()
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        using var workspace = await EnterEngineerEditModeAsync(store, valuation.Register);
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
    [Fact]
    public async Task ApplyValuationPostsTheSelectionToThePortAsThePolicyShape()
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        var glasses = valuation.AddGuide(ValuationSource.Glasses, 12_500m, 10_250m);
        var preset = valuation.AddPreset("Tow bar", 150m);
        using var workspace = await EnterEngineerEditModeAsync(store, valuation.Register);
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
                ("selection.AdditionSelected[0]", "0"),
                ("selection.AdditionPresetId[0]", preset.Id.ToString("D")),
                ("selection.AdditionPresetVersion[0]", preset.Version.ToString(CultureInfo.InvariantCulture)),
                ("selection.AdditionLabel[0]", ""),
                ("selection.AdditionAmount[0]", "175")));

        AssertValuationPrg(response, store.CaseId);
        var applied = Assert.Single(valuation.Applied);
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
        Assert.Null(applied.CorrectedEngineerValue);
        var claimant = store.Claims[0].Actor;
        Assert.Equal(claimant.SubjectId, applied.Actor.SubjectId);

        // v25 decision F: the immediate post reclaims the session rather than ending it.
        Assert.Equal(2, store.Claims.Count);
        var after = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");
        Assert.Contains("data-case-editing=\"true\"", after, StringComparison.Ordinal);
        Assert.Equal(store.LeaseToken, InputValue(after, "editLeaseToken"));
        AssertEditorCommit(after, "case-valuation-form", operationKey, applied.ExpectedVersion);
    }

    /// <summary>
    /// A guide source with no connected provider answers with the not-connected
    /// notice, records nothing, and leaves the edit session as it was.
    /// </summary>
    [Fact]
    public async Task GetValuationWithoutAConnectedProviderAnswersWithANoticeAndRecordsNothing()
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        using var workspace = await EnterEngineerEditModeAsync(store, valuation.Register);

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=GetValuation&source={ValuationSource.SuperCap}",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("operationKey", "4f4e4d4c4b4a49484746454443424140"),
                ("editLeaseToken", store.LeaseToken),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("guideMonth", "2026-09")));

        AssertValuationPrg(response, store.CaseId);
        Assert.Empty(valuation.Saved);
        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");
        Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
        Assert.Contains(
            CaseWorkspaceLabels.Valuation.Error,
            html,
            StringComparison.Ordinal);
        Assert.Contains("data-case-editing=\"true\"", html, StringComparison.Ordinal);
        Assert.Equal(store.LeaseToken, InputValue(html, "editLeaseToken"));
    }

    /// <summary>
    /// A connected provider is asked for the Case's accepted registration and
    /// mileage in the posted month, and its figures fill that source's entry
    /// card; nothing is recorded until the card is saved, and the edit session
    /// continues.
    /// </summary>
    [Fact]
    public async Task GetValuationWithAConnectedProviderFillsTheSourcesCardAndRecordsNothing()
    {
        var store = new RecordingCaseDetailsStore();
        var valuation = new RecordingValuationSection(store.CaseId);
        var provider = new FakeGuideValuationProvider(ValuationSource.Brego, 13_250m, 11_000m);
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            valuation.Register(services);
            Substitute<ICaseDataQueries>(services, store);
            services.AddSingleton<IGuideValuationProvider>(provider);
        });
        const string operationKey = "5f5e5d5c5b5a59585756555453525150";

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=GetValuation&source={ValuationSource.Brego}",
            Form(
                workspace.AntiforgeryToken,
                ("id", store.CaseId.ToString("D")),
                ("operationKey", operationKey),
                ("editLeaseToken", store.LeaseToken),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("guideMonth", "2026-08")));

        AssertValuationPrg(response, store.CaseId);
        var asked = Assert.Single(provider.Requests);
        Assert.Equal("AB12CDE", asked.Registration);
        Assert.Equal(42_000L, asked.Mileage);
        Assert.Equal(new DateOnly(2026, 8, 1), asked.GuideMonth);
        Assert.Empty(valuation.Saved);

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");
        Assert.Contains("data-case-editing=\"true\"", html, StringComparison.Ordinal);
        Assert.Equal(store.LeaseToken, InputValue(html, "editLeaseToken"));
        var card = EntryCard(html, "brego");
        Assert.Contains("name=\"retailValue\" required value=\"13250.00\"", card, StringComparison.Ordinal);
        Assert.Contains("name=\"tradeValue\" required value=\"11000.00\"", card, StringComparison.Ordinal);
        Assert.Contains("name=\"guideMonth\" value=\"2026-08\"", card, StringComparison.Ordinal);
        Assert.Contains("name=\"mileage\" required value=\"42000\"", card, StringComparison.Ordinal);
        // The figures were held for one redraw only.
        var again = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=valuation");
        Assert.DoesNotContain("value=\"13250.00\"", EntryCard(again, "brego"), StringComparison.Ordinal);
    }

    /// <summary>One source's entry card: the form from its opening tag to its closing tag.</summary>
    private static string EntryCard(string html, string source)
    {
        var card = Regex.Match(
            html,
            $"<form[^>]*data-valuation-entry=\"{Regex.Escape(source)}\"[^>]*>.*?</form>",
            RegexOptions.CultureInvariant | RegexOptions.Singleline);
        Assert.True(card.Success, $"The Valuation section must render the entry card for '{source}'.");
        return card.Value;
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
        IApplyValuationCalculation,
        ISaveValuation
    {
        private static readonly DateTimeOffset RecordedAt = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        private readonly List<CaseValuation> guides = [];
        private readonly List<ValuationPreset> presets = [];
        private AiJobRecord? pending;

        public List<StartMarketResearchRequest> Started { get; } = [];

        public List<ApplyValuationRequest> Applied { get; } = [];

        public List<SaveValuationRequest> Saved { get; } = [];

        /// <summary>Substitutes every port the section reads and posts through.</summary>
        public void Register(IServiceCollection services)
        {
            Substitute<IListCaseValuations>(services, this);
            Substitute<IListAppliedValuations>(services, this);
            Substitute<IListValuationPresets>(services, this);
            Substitute<IMarketResearchQueries>(services, this);
            Substitute<IStartMarketResearch>(services, this);
            Substitute<IApplyValuationCalculation>(services, this);
            Substitute<ISaveValuation>(services, this);
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
            Task.FromResult<IReadOnlyList<AppliedValuation>>([]);

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

        public Task<CaseValuation> ExecuteAsync(SaveValuationRequest request, CancellationToken cancellationToken)
        {
            Saved.Add(request);
            return Task.FromResult(AddGuide(request.Details.Source, request.Details.RetailValue, request.Details.TradeValue));
        }
    }
}
