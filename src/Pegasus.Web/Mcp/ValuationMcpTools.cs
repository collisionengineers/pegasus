using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Mcp;

internal sealed record ValuationToolItem(
    Guid ValuationId,
    string Source,
    string Date,
    long? Mileage,
    decimal? RetailValue,
    decimal? TradeValue,
    string? GuideMonth,
    string RecordedBy,
    DateTimeOffset RecordedAtUtc);

internal sealed record ValuationListToolResult(
    Guid CaseId,
    IReadOnlyList<ValuationToolItem> Valuations,
    string CorrelationId);

internal sealed record ValuationSaveToolResult(
    Guid CaseId,
    long CaseVersion,
    IReadOnlyList<ValuationToolItem> Valuations,
    string OperationKey);

internal sealed record ValuationGetToolResult(
    Guid CaseId,
    [property: Description("Recorded, Unavailable, VehicleAge, NoVehicleData or AlreadyAsked.")] string Outcome,
    string? Message,
    decimal? RetailValue,
    decimal? TradeValue,
    string? GuideMonth,
    long? CaseVersion,
    IReadOnlyList<ValuationToolItem> Valuations,
    string OperationKey);

internal sealed record GuideCardToolInput(
    [property: Description("The guide the figures come from: Glasses, Cazana, EngineersValue, Brego, SuperCap or Cap.")] string Source,
    [property: Description("Retail value in pounds.")] decimal? RetailValue = null,
    [property: Description("Trade value in pounds.")] decimal? TradeValue = null,
    [property: Description("The guide month the figures are for, yyyy-MM.")] string? GuideMonth = null);

internal sealed record ValuationAdditionToolInput(
    [property: Description("The valuation addition preset identifier; the empty identifier 00000000-0000-0000-0000-000000000000 is an Other increase, which counts only with a label and an amount above zero and is otherwise left out.")] Guid PresetId,
    [property: Description("The preset version the amount was read at.")] long PresetVersion,
    [property: Description("The addition's label.")] string? Label,
    [property: Description("The addition amount in pounds.")] decimal Amount);

internal sealed record ValuationAdoptionToolInput(
    [property: Description("The recorded guide card the calculation is based on (pegasus_valuation_list); omit when the basis is a card saved in this same call, and name its guideSource instead.")] Guid? GuideValuationId,
    [property: Description("The basis card's source when it is saved in this same call.")] string? GuideSource,
    [property: Description("Whether commercial VAT applies.")] bool CommercialVat,
    [property: Description("A prior total-loss percentage deduction, if any.")] decimal? PriorTotalLossPercentage,
    [property: Description("Additions to the basis value.")] IReadOnlyList<ValuationAdditionToolInput>? Additions,
    [property: Description("Condition deduction in pounds.")] decimal ConditionDeduction);

/// <summary>
/// Automation Actor valuation cards (FRD-10, ADR-0064): the guide cards and
/// valuation calculation a staff member records in the Case's Valuation
/// section, written through the same Core commands as the staff Case page.
/// </summary>
[McpServerToolType]
internal sealed class ValuationMcpTools(
    ISaveCaseWorkspace saveCaseWorkspace,
    IListCaseValuations listValuations,
    IFetchGuideValuation fetchGuideValuation,
    TimeProvider timeProvider,
    AutomationMcpAuditor auditor,
    OneCommandEdit leases)
{
    [McpServerTool(
        Name = "pegasus_valuation_list",
        Title = "List case valuations",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Lists the valuation cards recorded on a case: each guide card, the Engineer's Value card and any AI market research card, with its figures and identifier.")]
    public async Task<ValuationListToolResult> ListValuationsAsync(
        [Description(AutomationToolText.CaseId)] Guid caseId,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_valuation_list",
            AutomationMcpAuditor.Resource(caseId),
            async context =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                var valuations = await listValuations.ExecuteAsync(caseId, CaseWorkSelector.Current, cancellationToken);
                return new ValuationListToolResult(
                    caseId, valuations.Select(Map).ToArray(), context.TraceIdentifier);
            },
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_valuation_save",
        Title = "Save case valuation cards",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Records valuation guide cards on a case and, optionally, the valuation calculation against a basis card, as the Valuation section's Save does. A card for the same source and guide month replaces the earlier one. A calculation writes the report's Retail and Trade values (assessment.values.retail, assessment.values.trade) from its basis card; nothing else writes them. The Engineer's Value (assessment.values.engineer) is written with pegasus_assessment_update. Needs the expected case version (present an edit lease token for multi-step work, or omit it and the tool holds the lease for this one command).")]
    public async Task<ValuationSaveToolResult> SaveValuationsAsync(
        [Description(AutomationToolText.CaseId)] Guid caseId,
        [Description(AutomationToolText.ExpectedCaseVersion)] long expectedVersion,
        [Description(AutomationToolText.OperationKey)] string operationKey,
        [Description("The guide cards to record.")] IReadOnlyList<GuideCardToolInput>? guideCards = null,
        [Description("The calculation to record against its basis card.")] ValuationAdoptionToolInput? adoption = null,
        [Description("Why the valuation is being recorded (case history reason).")] string? reason = null,
        [Description(AutomationToolText.EditLeaseToken)] string? editLeaseToken = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_valuation_save",
            AutomationMcpAuditor.Resource(caseId),
            operationKey,
            async (context, key) =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                var recordedAt = LondonCalendar.TimeAt(timeProvider.GetUtcNow());
                var cards = (guideCards ?? [])
                    .Select(card => new ValuationDetails(
                        ParseSource(card.Source),
                        DateOnly.FromDateTime(recordedAt),
                        TimeOnly.FromDateTime(recordedAt),
                        // A guide card carries no mileage: the Case's own is used
                        // wherever a valuation needs one (operator, 24 September 2026).
                        null,
                        card.RetailValue,
                        card.TradeValue,
                        ParseGuideMonth(card.GuideMonth)))
                    .ToArray();
                var selection = adoption is null
                    ? null
                    : new ValuationCalculationSelection(
                        adoption.GuideValuationId ?? Guid.Empty,
                        adoption.CommercialVat,
                        adoption.PriorTotalLossPercentage,
                        (adoption.Additions ?? [])
                            .Select(addition => new ValuationAdditionSelection(
                                addition.PresetId, addition.PresetVersion, addition.Label, addition.Amount))
                            .Where(addition => addition.Counts)
                            .ToArray(),
                        adoption.ConditionDeduction)
                    {
                        GuideSource = adoption.GuideSource is { } source ? ParseSource(source) : null,
                    };
                if (cards.Length == 0 && selection is null)
                {
                    throw new McpException("Name at least one guide card or a calculation to record.");
                }
                var saved = await leases.RunCaseAsync(
                    caseId,
                    expectedVersion,
                    editLeaseToken,
                    context.Actor,
                    key,
                    token => saveCaseWorkspace.ExecuteAsync(
                        new(caseId, expectedVersion, context.Actor, key, reason, token)
                        {
                            Valuation = new(cards, selection),
                        },
                        cancellationToken),
                    cancellationToken);
                var valuations = await listValuations.ExecuteAsync(caseId, CaseWorkSelector.Current, cancellationToken);
                return new ValuationSaveToolResult(
                    caseId,
                    saved.Version,
                    valuations.Select(Map).ToArray(),
                    key);
            },
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_valuation_get",
        Title = "Get a guide valuation",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Runs the Valuation section's Get valuation for one guide source and records that source's card on the case with the figures it answers, as a member of staff pressing Get valuation and saving does. Pegasus asks the source's connected provider (Glass's, Brego or Cazana) for the case's confirmed registration and mileage in the guide month, files the provider's own valuation report on the case afterwards and fills an empty case VIN the provider names (Glass's, Brego and Cazana offer both). Outcome Recorded carries the figures; Unavailable (no connected provider, or it could not answer), VehicleAge (the source does not value a vehicle of this age), NoVehicleData (the source holds no data for the registration) and AlreadyAsked (the source has already answered for this case and guide month at its current registration and mileage, so it is not asked or charged again) record nothing. Adopting a basis card, which writes the report's Retail and Trade values, stays with pegasus_valuation_save; the Engineer's Value stays with pegasus_assessment_update. A source is asked once per case and guide month at its confirmed registration and mileage: only an answer with figures uses that ask, and a corrected registration or mileage allows one more. Needs the expected case version (present an edit lease token for multi-step work, or omit it and the tool holds the lease for this one command).")]
    public async Task<ValuationGetToolResult> GetValuationAsync(
        [Description(AutomationToolText.CaseId)] Guid caseId,
        [Description(AutomationToolText.ExpectedCaseVersion)] long expectedVersion,
        [Description(AutomationToolText.OperationKey)] string operationKey,
        [Description("The guide source to value with: Glasses, Cazana, Brego, SuperCap or Cap.")] string source,
        [Description("The guide month to value in, yyyy-MM; omit for the current month.")] string? guideMonth = null,
        [Description("Why the valuation is being recorded (case history reason).")] string? reason = null,
        [Description(AutomationToolText.EditLeaseToken)] string? editLeaseToken = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_valuation_get",
            AutomationMcpAuditor.Resource(caseId),
            operationKey,
            async (context, key) =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                var valuationSource = ParseSource(source);
                var today = LondonCalendar.DateAt(timeProvider.GetUtcNow());
                var month = ParseGuideMonth(guideMonth) ?? new DateOnly(today.Year, today.Month, 1);
                var fetched = await leases.RunCaseAsync(
                    caseId,
                    expectedVersion,
                    editLeaseToken,
                    context.Actor,
                    key,
                    async token =>
                    {
                        GuideValuationQuote quote;
                        try
                        {
                            quote = await fetchGuideValuation.ExecuteAsync(
                                new(caseId, expectedVersion, context.Actor, key, token, valuationSource, month),
                                cancellationToken);
                        }
                        catch (GuideValuationProviderUnavailableException)
                        {
                            return new FetchedValuation(
                                "Unavailable", CaseWorkspaceLabels.Valuation.Unavailable(valuationSource), null, null);
                        }
                        catch (GuideValuationNotValuedException notValued)
                        {
                            return new FetchedValuation(
                                notValued.Reason.ToString(), CaseWorkspaceLabels.Valuation.NotValued(valuationSource, notValued.Reason), null, null);
                        }
                        catch (GuideValuationAlreadyFetchedException)
                        {
                            return new FetchedValuation(
                                "AlreadyAsked", CaseWorkspaceLabels.Valuation.AlreadyFetched(valuationSource), null, null);
                        }

                        // The fetched figures are recorded as the card's Save records them.
                        // A VIN the fetch filled advanced the version as system work, which
                        // the lease this command holds still covers.
                        var recordedAt = LondonCalendar.TimeAt(timeProvider.GetUtcNow());
                        var card = new ValuationDetails(
                            valuationSource,
                            DateOnly.FromDateTime(recordedAt),
                            TimeOnly.FromDateTime(recordedAt),
                            // A guide card carries no mileage: the Case's own is used.
                            null,
                            quote.RetailValue,
                            quote.TradeValue,
                            quote.GuideMonth);
                        var saved = await saveCaseWorkspace.ExecuteAsync(
                            new(caseId, expectedVersion, context.Actor, key, reason, token)
                            {
                                Valuation = new([card], null),
                            },
                            cancellationToken);
                        return new FetchedValuation("Recorded", null, quote, saved.Version);
                    },
                    cancellationToken);
                var valuations = await listValuations.ExecuteAsync(caseId, CaseWorkSelector.Current, cancellationToken);
                return new ValuationGetToolResult(
                    caseId,
                    fetched.Outcome,
                    fetched.Message,
                    fetched.Quote?.RetailValue,
                    fetched.Quote?.TradeValue,
                    fetched.Quote?.GuideMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                    fetched.CaseVersion,
                    valuations.Select(Map).ToArray(),
                    key);
            },
            cancellationToken);
    }

    private sealed record FetchedValuation(
        string Outcome, string? Message, GuideValuationQuote? Quote, long? CaseVersion);

    private static ValuationSource ParseSource(string value) =>
        AutomationMcpErrors.ParseName<ValuationSource>(
            value,
            "The valuation source must be one of: " + string.Join(", ", Enum.GetNames<ValuationSource>()) + ".");

    private static DateOnly? ParseGuideMonth(string? value) =>
        AutomationMcpErrors.OptionalMonth(value, "The guide month must be yyyy-MM.");

    private static ValuationToolItem Map(CaseValuation valuation) => new(
        valuation.ValuationId,
        valuation.Details.Source.ToString(),
        valuation.Details.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        valuation.Details.Mileage,
        valuation.Details.RetailValue,
        valuation.Details.TradeValue,
        valuation.Details.GuideMonth?.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        valuation.RecordedBy,
        valuation.RecordedAtUtc);
}
