using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Glass;
using Pegasus.Web.Background;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Mcp;

internal sealed record GlassSessionToolItem(
    Guid SessionId,
    long Version,
    [property: Description("Prepared, Launching, Active, Importing, AwaitingImport, Completed, Failed, Unknown, Expired or Cancelled.")] string State,
    [property: Description("The glass.* code of what Glass's answered, when it stopped or refused.")] string? FailureCode,
    DateTimeOffset CreatedAtUtc,
    [property: Description("Work for the session is queued or running now; read it again later.")] bool Working,
    [property: Description("The Draft repair spec a finished session landed as.")] Guid? DraftSpecificationId,
    [property: Description(AutomationToolText.GlassSessionOwner)] string Owner,
    [property: Description(AutomationToolText.GlassSessionHelpOn)] bool? HelpOn,
    [property: Description(AutomationToolText.GlassSessionWindowConnected)] bool? WindowConnected);

internal sealed record GlassAccountToolItem(
    [property: Description("No session holds the Glass's account.")] bool Free,
    [property: Description("staff or automation, when a session holds the account.")] string? HeldBy,
    Guid? CaseId,
    string? CaseReference);

internal sealed record GlassEstimateToolItem(
    [property: Description("Each position's id on the estimate.")] IReadOnlyList<string> PositionIds,
    [property: Description("The codes on the estimate, as Glass's lists them.")] IReadOnlyList<string> Codes,
    [property: Description(AutomationToolText.GlassReadTotals)] IReadOnlyList<GlassTotalToolItem>? Totals);

internal sealed record GlassTotalToolItem(
    [property: Description(AutomationToolText.GlassReadTotalLabel)] string Label,
    decimal Amount);

internal sealed record GlassPositionToolItem(
    string PositionId,
    string? PartCode,
    string? Description,
    [property: Description(AutomationToolText.GlassReadOperation)] string? Operation,
    [property: Description(AutomationToolText.GlassReadPrice)] decimal? Price,
    [property: Description(AutomationToolText.GlassReadHours)] decimal? Hours,
    [property: Description(AutomationToolText.GlassReadSide)] string? Side,
    string? PartNumber,
    [property: Description(AutomationToolText.GlassReadPaint)] GlassPositionPaintToolItem? Paint);

internal sealed record GlassPositionPaintToolItem(
    string PositionId, string? Description, decimal? Price, decimal? LabourTime);

internal sealed record GlassCalculationTradeToolItem(
    [property: Description(AutomationToolText.GlassReadTrade)] string Trade,
    decimal? LabourRate,
    decimal? LabourTime,
    decimal? LabourCosts,
    decimal? MaterialCosts);

internal sealed record GlassCalculationToolItem(
    IReadOnlyList<GlassCalculationTradeToolItem> Trades,
    decimal? TotalLabourTime,
    decimal? TotalLabourCosts,
    decimal? TotalMaterial,
    decimal? RepairCostsExclVat,
    [property: Description(AutomationToolText.GlassReadVatLabel)] string? VatLabel,
    decimal? Vat,
    decimal? RepairCostsInclVat);

internal sealed record GlassListedPartToolItem(
    string Code,
    string Text,
    [property: Description(AutomationToolText.GlassActMandatoryPartTicked)] bool Ticked,
    [property: Description(AutomationToolText.GlassActMandatoryPartOptional)] bool Optional);

/// <summary>What an add or a preview gives Glass's dialog (R1206): later versions add members.</summary>
internal sealed record GlassPositionInputsToolInput(
    [property: Description(AutomationToolText.GlassInputsEquipment)] IReadOnlyList<string>? Equipment = null,
    [property: Description(AutomationToolText.GlassInputsOperation)] string? Operation = null,
    [property: Description(AutomationToolText.GlassInputsButton)] string? Button = null,
    [property: Description(AutomationToolText.GlassInputsSide)] string? Side = null,
    [property: Description(AutomationToolText.GlassInputsOptionalParts)] IReadOnlyList<string>? OptionalParts = null,
    [property: Description(AutomationToolText.GlassInputsConfirmNoFigures)] bool? ConfirmNoFigures = null,
    [property: Description(AutomationToolText.GlassInputsHours)] decimal? Hours = null,
    [property: Description(AutomationToolText.GlassInputsPrice)] decimal? Price = null,
    [property: Description(AutomationToolText.GlassInputsOverlapHours)] decimal? OverlapHours = null,
    [property: Description(AutomationToolText.GlassInputsReason)] string? Reason = null,
    [property: Description(AutomationToolText.GlassInputsPaint)] GlassPaintInputsToolInput? Paint = null);

/// <summary>The paint choices of a part's dialog (R1206).</summary>
internal sealed record GlassPaintInputsToolInput(
    [property: Description(AutomationToolText.GlassInputsPaintCoats)] string? Coats = null,
    [property: Description(AutomationToolText.GlassInputsPaintLevel)] string? Level = null,
    [property: Description(AutomationToolText.GlassInputsPaintSurcharge)] bool? Surcharge = null,
    [property: Description(AutomationToolText.GlassInputsPaintHours)] decimal? Hours = null);

/// <summary>A dialog's figures as an act records them (R1206): served by Glass's, or sent by the post.</summary>
internal sealed record GlassFiguresToolItem(
    decimal? Price,
    decimal? Hours,
    decimal? OverlapHours,
    [property: Description(AutomationToolText.GlassActPaintFigures)] string? PaintCoats = null,
    [property: Description(AutomationToolText.GlassActPaintFigures)] string? PaintLevel = null,
    [property: Description(AutomationToolText.GlassActPaintFigures)] decimal? PaintPrice = null,
    [property: Description(AutomationToolText.GlassActPaintFigures)] decimal? PaintHours = null,
    [property: Description(AutomationToolText.GlassActPaintFigures)] bool? Surcharge = null);

/// <summary>A preview's dialog as a form (R1206); Glass's own labels and texts pass through unchanged.</summary>
internal sealed record GlassDialogToolItem(
    [property: Description(AutomationToolText.GlassPreviewKind)] string Kind,
    string? Description,
    string? PartNumber,
    string? PartCode,
    [property: Description(AutomationToolText.GlassReadSide)] string? Side,
    [property: Description(AutomationToolText.GlassPreviewOperations)] IReadOnlyList<GlassDialogOperationToolItem> Operations,
    [property: Description(AutomationToolText.GlassPreviewFields)] IReadOnlyList<GlassDialogFieldToolItem> Fields,
    [property: Description(AutomationToolText.GlassPreviewPaint)] GlassDialogPaintToolItem? Paint,
    [property: Description(AutomationToolText.GlassPreviewButtons)] IReadOnlyList<GlassDialogChoiceToolItem> Buttons,
    [property: Description(AutomationToolText.GlassPreviewSends)] GlassDialogSendToolItem? Sends);

internal sealed record GlassDialogOperationToolItem(
    [property: Description(AutomationToolText.GlassPreviewOperationCode)] string Code,
    [property: Description(AutomationToolText.GlassPreviewText)] string Text,
    [property: Description(AutomationToolText.GlassPreviewOperationFigures)] decimal? Price,
    [property: Description(AutomationToolText.GlassPreviewOperationFigures)] decimal? Hours,
    [property: Description(AutomationToolText.GlassPreviewGlassHours)] decimal? GlassHours,
    [property: Description(AutomationToolText.GlassPreviewServed)] bool Served,
    [property: Description(AutomationToolText.GlassPreviewChangeable)] bool Changeable,
    [property: Description(AutomationToolText.GlassPreviewOperationRefusedCode)] string? RefusedCode = null);

internal sealed record GlassDialogChoiceToolItem(string Value, [property: Description(AutomationToolText.GlassPreviewText)] string Text);

internal sealed record GlassDialogFieldToolItem(
    [property: Description(AutomationToolText.GlassPreviewFieldName)] string Name,
    [property: Description(AutomationToolText.GlassPreviewFieldLabel)] string? Label,
    [property: Description(AutomationToolText.GlassPreviewFieldValue)] string? Value,
    IReadOnlyList<GlassDialogChoiceToolItem>? Choices,
    [property: Description(AutomationToolText.GlassPreviewFieldDisabled)] bool Disabled,
    [property: Description(AutomationToolText.GlassPreviewChangeable)] bool Changeable,
    [property: Description(AutomationToolText.GlassPreviewFieldInput)] string? Input = null,
    [property: Description(AutomationToolText.GlassPreviewFieldReasonAbove)] decimal? ReasonAbove = null);

internal sealed record GlassDialogPaintToolItem(
    GlassDialogChoiceToolItem? Area,
    GlassDialogChoiceToolItem? Coats,
    GlassDialogChoiceToolItem? Level,
    decimal? Price,
    decimal? Hours,
    [property: Description(AutomationToolText.GlassPreviewPaintChoices)] IReadOnlyList<GlassDialogPaintChoiceToolItem>? Choices = null,
    [property: Description(AutomationToolText.GlassPreviewPaintPositionsOwn)] bool PositionsOwn = false,
    [property: Description(AutomationToolText.GlassPreviewPaintSurcharge)] bool? Surcharge = null,
    [property: Description(AutomationToolText.GlassPreviewPaintSurchargeFixed)] bool SurchargeFixed = false);

internal sealed record GlassDialogPaintChoiceToolItem(
    [property: Description(AutomationToolText.GlassInputsPaintCoats)] string Coats,
    [property: Description(AutomationToolText.GlassInputsPaintLevel)] string Level,
    [property: Description(AutomationToolText.GlassPreviewText)] string? LevelText,
    [property: Description(AutomationToolText.GlassPreviewPaintChoiceFigures)] decimal? Price,
    [property: Description(AutomationToolText.GlassPreviewPaintChoiceFigures)] decimal? Hours);

internal sealed record GlassDialogSendToolItem(
    [property: Description(AutomationToolText.GlassPreviewSendsButton)] string? Button,
    string? Operation,
    decimal? Price,
    decimal? Hours,
    string? PaintLevel,
    decimal? PaintPrice,
    decimal? PaintHours,
    bool BothSides,
    [property: Description(AutomationToolText.GlassPreviewQuestions)] IReadOnlyList<GlassDialogQuestionToolItem> Questions,
    [property: Description(AutomationToolText.GlassPreviewSendsCode)] string? Code,
    decimal? OverlapHours = null,
    string? PaintCoats = null,
    bool? Surcharge = null);

internal sealed record GlassDialogQuestionToolItem(
    [property: Description(AutomationToolText.GlassPreviewQuestionKind)] string Kind,
    [property: Description(AutomationToolText.GlassPreviewQuestionText)] string? Text,
    [property: Description(AutomationToolText.GlassPreviewQuestionAnswered)] bool Answered);

internal sealed record GlassEquipmentOptionToolItem(
    [property: Description(AutomationToolText.GlassEquipmentOptionCode)] string Code,
    [property: Description(AutomationToolText.GlassEquipmentOptionName)] string Name,
    [property: Description(AutomationToolText.GlassEquipmentOptionTicked)] bool Ticked,
    [property: Description(AutomationToolText.GlassEquipmentOptionGreyed)] bool Greyed,
    [property: Description(AutomationToolText.GlassEquipmentOptionGroup)] string? Group,
    [property: Description(AutomationToolText.GlassEquipmentOptionNote)] string? AffectsOtherParts);

internal sealed record GlassEquipmentAnswerToolItem(
    [property: Description(AutomationToolText.GlassEquipmentAnswerTicked)] IReadOnlyList<string> Ticked,
    [property: Description(AutomationToolText.GlassEquipmentAnswerRows)] string Rows,
    [property: Description(AutomationToolText.GlassEquipmentAnswerFigures)] string? PartNumber,
    [property: Description(AutomationToolText.GlassEquipmentAnswerFigures)] decimal? Price,
    [property: Description(AutomationToolText.GlassEquipmentAnswerFigures)] decimal? Hours);

internal sealed record GlassEquipmentToolItem(
    IReadOnlyList<GlassEquipmentOptionToolItem> Options,
    [property: Description(AutomationToolText.GlassEquipmentAnswers)] IReadOnlyList<GlassEquipmentAnswerToolItem>? Answers,
    [property: Description(AutomationToolText.GlassEquipmentMapParts)] IReadOnlyList<GlassEquipmentAnswerToolItem>? MapParts,
    [property: Description(AutomationToolText.GlassEquipmentMapHours)] IReadOnlyList<GlassEquipmentAnswerToolItem>? MapHours);

internal sealed record GlassEquipmentChangeToolItem(string Code, bool Ticked);

internal sealed record GlassActToolItem(
    string OperationKey,
    [property: Description(AutomationToolText.GlassPositionAction)] string Action,
    [property: Description(AutomationToolText.GlassActOutcome)] string Outcome,
    [property: Description(AutomationToolText.GlassActCode)] string? Code,
    [property: Description(AutomationToolText.GlassCatalogueKind)] string? Kind,
    string? Row,
    string? Operation,
    string? Side,
    string? PositionId,
    [property: Description(AutomationToolText.GlassActPositionIds)] IReadOnlyList<string> PositionIds,
    [property: Description(AutomationToolText.GlassActDescription)] string? Description,
    [property: Description(AutomationToolText.GlassActPrice)] decimal? Price,
    [property: Description(AutomationToolText.GlassActHours)] decimal? Hours,
    [property: Description(AutomationToolText.GlassActMandatoryParts)] IReadOnlyList<GlassListedPartToolItem> MandatoryParts,
    DateTimeOffset RequestedAtUtc,
    [property: Description(AutomationToolText.GlassActInputs)] GlassPositionInputsToolInput? Inputs = null,
    [property: Description(AutomationToolText.GlassActEquipment)] GlassEquipmentToolItem? Equipment = null,
    [property: Description(AutomationToolText.GlassActRowPair)] string? RowPair = null,
    [property: Description(AutomationToolText.GlassActEquipmentChanged)] IReadOnlyList<GlassEquipmentChangeToolItem>? EquipmentChanged = null,
    [property: Description(AutomationToolText.GlassActEquipmentStored)] bool? EquipmentStored = null,
    [property: Description(AutomationToolText.GlassActServed)] GlassFiguresToolItem? Served = null,
    [property: Description(AutomationToolText.GlassActSent)] GlassFiguresToolItem? Sent = null,
    [property: Description(AutomationToolText.GlassActPosition)] GlassPositionToolItem? Position = null);

internal sealed record GlassSessionGetToolResult(
    Guid CaseId,
    GlassSessionToolItem? Session,
    GlassAccountToolItem Account,
    [property: Description(AutomationToolText.GlassReadEstimate)] GlassEstimateToolItem? Estimate,
    [property: Description(AutomationToolText.GlassReadPosition)] GlassPositionToolItem? Position,
    [property: Description(AutomationToolText.GlassReadCalculation)] GlassCalculationToolItem? Calculation,
    [property: Description(AutomationToolText.GlassReadCode)] string? ReadCode,
    [property: Description(AutomationToolText.GlassActs)] IReadOnlyList<GlassActToolItem> Acts,
    string CorrelationId,
    [property: Description(AutomationToolText.GlassHelpCode)] string? Code,
    [property: Description(AutomationToolText.GlassHelpSessions)] IReadOnlyList<GlassSessionToolItem> Sessions);

internal sealed record GlassCatalogueRowToolItem(
    [property: Description(AutomationToolText.GlassCatalogueRow)] string Row,
    [property: Description(AutomationToolText.GlassCatalogueKind)] string Kind,
    string Description,
    string? PartCode,
    string? PartNumber,
    [property: Description(AutomationToolText.GlassCatalogueSide)] string? Side,
    [property: Description(AutomationToolText.GlassCataloguePaintable)] bool Paintable,
    [property: Description(AutomationToolText.GlassCatalogueEquipment)] GlassEquipmentToolItem? Equipment = null);

internal sealed record GlassCatalogueSearchToolResult(
    Guid CaseId,
    GlassSessionToolItem? Session,
    IReadOnlyList<GlassCatalogueRowToolItem> Items,
    string? NextCursor,
    [property: Description(AutomationToolText.GlassCatalogueCode)] string? Code,
    string CorrelationId);

internal sealed record GlassPositionActToolResult(
    Guid CaseId,
    GlassSessionToolItem? Session,
    [property: Description(AutomationToolText.GlassActOfAddOrRemove)] GlassActToolItem? Act,
    string OperationKey,
    [property: Description(AutomationToolText.GlassAlsoNew)] IReadOnlyList<string>? AlsoNew = null,
    [property: Description(AutomationToolText.GlassShownInWindow)] bool? ShownInWindow = null,
    [property: Description(AutomationToolText.GlassPreviewDialog)] GlassDialogToolItem? Dialog = null,
    [property: Description(AutomationToolText.GlassPreviewCode)] string? PreviewCode = null);

internal sealed record GlassSessionActToolResult(
    Guid CaseId,
    GlassSessionToolItem? Session,
    string OperationKey);

/// <summary>
/// The Automation actor's own Glass's sessions (design 07 section 3; operator,
/// 18 November 2026, R1177 to R1180): it starts a session under the login the
/// deployment holds for it, finishes it as a Draft repair spec, or closes it.
/// Every tool finds the Automation's session by its Case. A refusal Pegasus
/// makes before Glass's is asked is an error; what Glass's answered is the
/// session's state and code. Work that asks Glass's runs as provider work
/// keyed on the session, one piece at a time, and a call waits for it for up
/// to <see cref="AnswerWindow"/>.
/// </summary>
/// <remarks>
/// The read, the catalogue search and the position act also work in an
/// Engineer's own open session while they have Automation help on (R1170,
/// R1182, R1185; design 08 section 5): the session is chosen by
/// <see cref="GlassSessionHelpPolicy.Find"/>, refused with its
/// <c>glass.help.*</c> code, and acted in through <see cref="GlassSessionHelp"/>
/// alone, which cannot save, exit or close it. No Case lease is taken there;
/// start, finish and close refuse an Engineer's session.
/// </remarks>
[McpServerToolType]
internal sealed class GlassMcpTools(
    IGlassRepairEstimateGateway gateway,
    IGlassRepairEstimateSessionReader sessions,
    IGlassRepairEstimateSessionStore store,
    GlassRepairEstimateAvailability availability,
    ProviderWorkQueue queue,
    ListCaseReferences listCaseReferences,
    IServiceProvider services,
    TimeProvider timeProvider,
    ICursorProtector cursors,
    AutomationMcpAuditor auditor,
    OneCommandEdit leases,
    IGlassSessionHelpStore helpStore)
{
    /// <summary>
    /// How long a call waits for its work: the wait <c>pegasus_edit_begin</c>
    /// already makes inside a call (design 07 section 6). An engineering
    /// bound, not a business rule.
    /// </summary>
    private static readonly TimeSpan AnswerWindow = TimeSpan.FromSeconds(45);

    [McpServerTool(
        Name = "pegasus_glass_session_get",
        Title = "Get the Automation's Glass's session",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description(AutomationToolText.GlassSessionGet + " " + AutomationToolText.GlassHelpWorksIn)]
    public async Task<GlassSessionGetToolResult> GetSessionAsync(
        [Description(AutomationToolText.CaseId)] Guid caseId,
        [Description(AutomationToolText.GlassRead)] string? read = null,
        [Description(AutomationToolText.GlassReadPositionId)] string? positionId = null,
        [Description(AutomationToolText.GlassSessionId)] Guid? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_glass_session_get",
            AutomationMcpAuditor.Resource(caseId),
            async context =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                var wanted = ReadOf(read, positionId);
                RequireAvailable();
                var candidates = await sessions.FindHelpableForCaseAsync(caseId, cancellationToken);
                var target = Found(candidates, sessionId);
                var session = target.Session;
                var code = target.Code;
                var answered = GlassReadAnswer.None;
                if (target.IsStaff && session is not null)
                {
                    // An Engineer's session is read through help alone, and
                    // only while it may be acted in.
                    if (wanted is not null && code is null)
                    {
                        try
                        {
                            RequireIdle(session);
                            answered = GlassReadAnswer.Of(await Help.ReadAsync(context.Actor, session, wanted, cancellationToken));
                        }
                        catch (GlassSessionHelpRefusalException refused)
                        {
                            code = refused.Code;
                        }
                    }

                    return new GlassSessionGetToolResult(
                        caseId, Map(session), await AccountAsync(context.Actor, cancellationToken),
                        answered.Estimate, answered.Position, answered.Calculation, answered.Code, [],
                        context.TraceIdentifier, code, []);
                }

                // A session waiting on work that nothing runs was interrupted,
                // and is settled as the Glass's window's poll settles it.
                if (session is not null
                    && GlassRepairEstimateSessionPolicy.AwaitsProviderWork(session.State)
                    && !queue.IsInFlight(session.Id))
                {
                    session = await gateway.SettleInterruptedAsync(context.Actor, session.Id, cancellationToken);
                }

                if (wanted is { } toRead
                    && session is { State: GlassRepairEstimateSessionState.Active }
                    && !queue.IsInFlight(session.Id))
                {
                    var readId = session.Id;
                    var done = await RunHereAsync(
                        readId,
                        "GlassAutomationRead",
                        token => gateway.ReadAutomationEstimateAsync(context.Actor, readId, toRead, token),
                        cancellationToken);
                    if (done is not null)
                    {
                        session = done.Session;
                        answered = GlassReadAnswer.Of(done);
                    }
                }

                return new GlassSessionGetToolResult(
                    caseId,
                    Map(session),
                    await AccountAsync(context.Actor, cancellationToken),
                    answered.Estimate,
                    answered.Position,
                    answered.Calculation,
                    answered.Code,
                    session?.Automation?.Acts.Select(MapAct).ToList() ?? [],
                    context.TraceIdentifier,
                    code,
                    code == GlassSessionHelpPolicy.SeveralCode
                        ? GlassSessionHelpPolicy.Open(candidates).Select(MapSession).ToList()
                        : []);
            },
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_glass_session_act",
        Title = "Start, finish or close the Automation's Glass's session",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description(AutomationToolText.GlassSessionAct + " " + AutomationToolText.GlassSessionActOwnOnly)]
    public async Task<GlassSessionActToolResult> ActOnSessionAsync(
        [Description(AutomationToolText.CaseId)] Guid caseId,
        [Description("start, finish or close.")] string action,
        [Description(AutomationToolText.OperationKey)] string operationKey,
        [Description("start and finish: " + AutomationToolText.ExpectedCaseVersion)] long? expectedVersion = null,
        [Description("finish and close: the session version from pegasus_glass_session_get; a stale value fails closed.")] long? expectedSessionVersion = null,
        [Description("close: why the session is ended without saving, 1 to 2,000 characters.")] string? reason = null,
        [Description(AutomationToolText.EditLeaseToken)] string? editLeaseToken = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_glass_session_act",
            AutomationMcpAuditor.Resource(caseId),
            operationKey,
            async (context, key) =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                RequireAvailable();
                var session = action?.Trim() switch
                {
                    "start" => await StartAsync(
                        context.Actor, caseId, RequireValue(expectedVersion, "expectedVersion"), key,
                        editLeaseToken, cancellationToken),
                    "finish" => await FinishAsync(
                        context.Actor, caseId, RequireValue(expectedVersion, "expectedVersion"),
                        RequireValue(expectedSessionVersion, "expectedSessionVersion"), key, editLeaseToken,
                        cancellationToken),
                    "close" => await CloseAsync(
                        context.Actor, caseId, RequireValue(expectedSessionVersion, "expectedSessionVersion"),
                        reason, cancellationToken),
                    _ => throw new McpException("The action must be one of: start, finish, close."),
                };
                return new GlassSessionActToolResult(caseId, Map(session), key);
            },
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_glass_catalogue_search",
        Title = "Search the Automation's open Glass's estimate",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description(AutomationToolText.GlassCatalogueSearch + " " + AutomationToolText.GlassHelpWorksIn)]
    public async Task<GlassCatalogueSearchToolResult> SearchCatalogueAsync(
        [Description(AutomationToolText.CaseId)] Guid caseId,
        [Description(AutomationToolText.GlassSearchTerm)] string? term = null,
        [Description(AutomationToolText.GlassSearchKind)] string? kind = null,
        [Description(AutomationToolText.GlassSearchSide)] string? side = null,
        [Description(AutomationToolText.Cursor)] string? cursor = null,
        [Description(AutomationToolText.PageSize)] int? limit = null,
        [Description(AutomationToolText.GlassSessionId)] Guid? sessionId = null,
        [Description(AutomationToolText.GlassSearchRow)] string? row = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_glass_catalogue_search",
            AutomationMcpAuditor.Resource(caseId),
            async context =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                RequireAvailable();
                var wantedRow = string.IsNullOrWhiteSpace(row) ? null : row.Trim();
                var wantedTerm = wantedRow is null ? term?.Trim() ?? string.Empty : string.Empty;
                if (wantedRow is null && wantedTerm.Length is 0 or > MaximumTermLength)
                {
                    throw new McpException(AutomationToolText.GlassSearchTermRefused);
                }

                GlassAutomationPositionKind? wantedKind = kind?.Trim() switch
                {
                    null or "" or "any" => null,
                    "part" => GlassAutomationPositionKind.Part,
                    "special_operation" => GlassAutomationPositionKind.SpecialOperation,
                    _ => throw new McpException(AutomationToolText.GlassSearchKindRefused),
                };
                var wantedSide = SideOf(side);
                var pageSize = CursorPaging.NormalizeLimit(limit);
                var target = await FindAsync(caseId, sessionId, cancellationToken);
                var session = RequireActable(target);
                if (session is not { State: GlassRepairEstimateSessionState.Active })
                {
                    throw new McpException(AutomationToolText.GlassNoSession);
                }

                // The cursor is bound to the caller, the session, the term,
                // the kind, the side and the row (FRD-10): an offset into the
                // rows found, which each page asks Glass's for again (no cache).
                var searched = session.Id;
                var scope = CursorPaging.CreateScope(
                    "pegasus_glass_catalogue_search", context.Actor,
                    searched.ToString("D"), wantedTerm, wantedKind is { } scoped ? KindName(scoped) : "any", wantedSide,
                    wantedRow);
                var offset = 0;
                if (!string.IsNullOrEmpty(cursor))
                {
                    var (sortKey, id) = cursors.Unprotect(cursor, scope);
                    if (id != searched
                        || !int.TryParse(sortKey, NumberStyles.None, CultureInfo.InvariantCulture, out offset))
                    {
                        throw new CursorRejectedException();
                    }
                }

                var query = new GlassAutomationCatalogueQuery(context.Actor, searched, wantedTerm, wantedKind, wantedSide)
                {
                    Row = wantedRow,
                };
                var found = target.IsStaff
                    ? await HelpAsync(() => Help.SearchAsync(query, session, cancellationToken))
                    : await RunHereAsync(
                        searched,
                        "GlassAutomationSearch",
                        token => gateway.SearchAutomationCatalogueAsync(query, token),
                        cancellationToken)
                        ?? throw new McpException(AutomationToolText.GlassNoSession);
                var items = found.Rows
                    .Skip(offset)
                    .Take(pageSize)
                    .Select(item => new GlassCatalogueRowToolItem(
                        item.Row, KindName(item.Kind), item.Description, item.PartCode, item.PartNumber, item.Side,
                        item.Paintable, MapEquipment(item.Equipment)))
                    .ToList();
                var next = offset + pageSize < found.Rows.Count
                    ? cursors.Protect(scope, (offset + pageSize).ToString(CultureInfo.InvariantCulture), searched)
                    : null;
                return new GlassCatalogueSearchToolResult(
                    caseId, Map(found.Session), items, next, found.Code, context.TraceIdentifier);
            },
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_glass_position_act",
        Title = "Add, change, remove or preview a position on the Automation's Glass's estimate",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description(AutomationToolText.GlassPositionAct + " " + AutomationToolText.GlassHelpWorksIn)]
    public async Task<GlassPositionActToolResult> ActOnPositionAsync(
        [Description(AutomationToolText.CaseId)] Guid caseId,
        [Description(AutomationToolText.GlassPositionAction)] string action,
        [Description(AutomationToolText.OperationKey)] string operationKey,
        [Description(AutomationToolText.GlassExpectedSessionVersion)] long? expectedSessionVersion = null,
        [Description(AutomationToolText.GlassAddKind)] string? kind = null,
        [Description(AutomationToolText.GlassAddRow)] string? row = null,
        [Description(AutomationToolText.GlassAddOperation)] string? operation = null,
        [Description(AutomationToolText.GlassAddSide)] string? side = null,
        [Description(AutomationToolText.GlassRemovePositionId)] string? positionId = null,
        [Description(AutomationToolText.GlassSessionId)] Guid? sessionId = null,
        [Description(AutomationToolText.GlassAddInputs)] GlassPositionInputsToolInput? inputs = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_glass_position_act",
            AutomationMcpAuditor.Resource(caseId),
            operationKey,
            async (context, key) =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                RequireAvailable();
                if (action?.Trim() == "preview")
                {
                    return await PreviewAsync(
                        caseId, PositionRequest(context.Actor, action, key, 0, kind, row, operation, side, positionId, inputs),
                        sessionId, key, cancellationToken);
                }

                var asked = PositionRequest(
                    context.Actor, action, key, RequireValue(expectedSessionVersion, "expectedSessionVersion"),
                    kind, row, operation, side, positionId, inputs);
                // The same key again answers the act as recorded and sends
                // nothing, whichever of the Case's Automation sessions holds it.
                if (await store.FindAutomationActSessionAsync(caseId, key, cancellationToken) is { } replayed
                    && GlassAutomationPositionPolicy.Find(replayed.Automation, key) is { } recorded)
                {
                    return new GlassPositionActToolResult(caseId, Map(replayed), MapAct(recorded), key);
                }
                // So does a key recorded by a help act in an Engineer's session.
                if (await helpStore.FindHelpActAsync(caseId, key, cancellationToken) is { } helped)
                {
                    var helpedIn = (await FindAsync(caseId, helped.SessionId, cancellationToken)).Session;
                    return new GlassPositionActToolResult(caseId, Map(helpedIn), MapAct(helped.Act), key);
                }

                var target = await FindAsync(caseId, sessionId, cancellationToken);
                var session = RequireActable(target);
                if (target.IsStaff)
                {
                    // In an Engineer's session: no Case lease, no work item,
                    // one help act at a time, and a cap of its own so a
                    // client that gives up never cuts a post in half.
                    var helpedSession = session;
                    var request = asked with { SessionId = helpedSession.Id };
                    using var cap = new CancellationTokenSource(ProviderWorkService.TimeCap, timeProvider);
                    var outcome = await HelpAsync(() => request.Action switch
                    {
                        GlassAutomationActAction.Remove => Help.RemoveAsync(request, helpedSession, cap.Token),
                        GlassAutomationActAction.Change => Help.ChangeAsync(request, helpedSession, cap.Token),
                        _ => Help.AddAsync(request, helpedSession, cap.Token),
                    });
                    return new GlassPositionActToolResult(
                        caseId, Map(helpedSession), MapAct(outcome.Act), key, outcome.AlsoNew, outcome.ShownInWindow);
                }
                if (session is not { State: GlassRepairEstimateSessionState.Active })
                {
                    throw new McpException(AutomationToolText.GlassNoSession);
                }
                if (queue.IsInFlight(session.Id))
                {
                    throw new McpException(AutomationToolText.GlassSessionWorking);
                }

                var id = session.Id;
                using var reservation = queue.Reserve(id);
                var step = await gateway.PrepareAutomationPositionAsync(asked with { SessionId = id }, cancellationToken)
                    ?? throw new McpException(AutomationToolText.GlassNoSession);
                if (!step.Owed)
                {
                    return new GlassPositionActToolResult(caseId, Map(step.Session), MapAct(step.Act), key);
                }

                await RunAsync(
                    reservation,
                    "GlassAutomationPosition",
                    (work, workToken) => work.GetRequiredService<IGlassRepairEstimateGateway>()
                        .ContinueAutomationPositionAsync(new(context.Actor, id, key), workToken),
                    cancellationToken);
                var after = await sessions.GetAutomationForCaseAsync(caseId, cancellationToken);
                var act = GlassAutomationPositionPolicy.Find(after?.Automation, key) ?? step.Act;
                return new GlassPositionActToolResult(caseId, Map(after), MapAct(act), key);
            },
            cancellationToken);
    }

    /// <summary>How long a catalogue search's term may be (design 07 section 3.3).</summary>
    private const int MaximumTermLength = 200;

    /// <summary>
    /// preview (R1206): the dialog read as a form, in the Automation's own
    /// session under the session's turn in the queue as a search is, or in an
    /// Engineer's through help. Nothing is posted and no act is recorded, so
    /// the operation key is not looked up and the session version is not
    /// asked for.
    /// </summary>
    private async Task<GlassPositionActToolResult> PreviewAsync(
        Guid caseId, GlassAutomationPositionRequest asked, Guid? sessionId, string key, CancellationToken cancellationToken)
    {
        var target = await FindAsync(caseId, sessionId, cancellationToken);
        var session = RequireActable(target);
        GlassAutomationPreview previewed;
        if (target.IsStaff)
        {
            var helped = session;
            previewed = await HelpAsync(() => Help.PreviewAsync(asked with { SessionId = helped.Id }, helped, cancellationToken));
        }
        else
        {
            if (session is not { State: GlassRepairEstimateSessionState.Active })
            {
                throw new McpException(AutomationToolText.GlassNoSession);
            }
            if (queue.IsInFlight(session.Id))
            {
                throw new McpException(AutomationToolText.GlassSessionWorking);
            }

            var id = session.Id;
            previewed = await RunHereAsync(
                id,
                "GlassAutomationPreview",
                token => gateway.PreviewAutomationPositionAsync(asked with { SessionId = id }, token),
                cancellationToken)
                ?? throw new McpException(AutomationToolText.GlassNoSession);
        }

        return new GlassPositionActToolResult(
            caseId, Map(previewed.Session), null, key, Dialog: MapDialog(previewed.Dialog), PreviewCode: previewed.Code);
    }

    /// <summary>A preview's dialog as the tool answers it; Glass's own labels and texts pass through unchanged.</summary>
    private static GlassDialogToolItem? MapDialog(GlassDialogPreview? dialog) =>
        dialog is null
            ? null
            : new GlassDialogToolItem(
                dialog.Kind,
                dialog.Description,
                dialog.PartNumber,
                dialog.PartCode,
                dialog.Side,
                dialog.Operations
                    .Select(item => new GlassDialogOperationToolItem(
                        item.Code, item.Text, item.Price, item.Hours, item.GlassHours, item.Served, item.Changeable,
                        item.RefusedCode))
                    .ToList(),
                dialog.Fields
                    .Select(field => new GlassDialogFieldToolItem(
                        field.Name, field.Label, field.Value, MapChoices(field.Choices), field.Disabled, field.Changeable,
                        field.Input, field.ReasonAbove))
                    .ToList(),
                dialog.Paint is { } paint
                    ? new GlassDialogPaintToolItem(
                        MapChoice(paint.Area), MapChoice(paint.Coats), MapChoice(paint.Level), paint.Price, paint.Hours,
                        paint.Choices
                            .Select(choice => new GlassDialogPaintChoiceToolItem(
                                choice.Coats, choice.Level, choice.LevelText, choice.Price, choice.Hours))
                            .ToList(),
                        paint.PositionsOwn,
                        paint.Surcharge,
                        paint.SurchargeFixed)
                    : null,
                MapChoices(dialog.Buttons) ?? [],
                dialog.Sends is { } sends
                    ? new GlassDialogSendToolItem(
                        sends.Button, sends.Operation, sends.Price, sends.Hours, sends.PaintLevel, sends.PaintPrice,
                        sends.PaintHours, sends.BothSides,
                        sends.Questions.Select(question => new GlassDialogQuestionToolItem(
                            question.Kind, question.Text, question.Answered)).ToList(),
                        sends.Code,
                        sends.OverlapHours,
                        sends.PaintCoats,
                        sends.Surcharge)
                    : null);

    private static GlassDialogChoiceToolItem? MapChoice(GlassDialogChoice? choice) =>
        choice is null ? null : new GlassDialogChoiceToolItem(choice.Value, choice.Text);

    private static List<GlassDialogChoiceToolItem>? MapChoices(IReadOnlyList<GlassDialogChoice>? choices) =>
        choices?.Select(choice => new GlassDialogChoiceToolItem(choice.Value, choice.Text)).ToList();

    /// <summary>
    /// A position act or a preview as asked, refused before anything is read
    /// when its action, kind, operation, side or inputs are not ones the tool
    /// takes (<see cref="GlassAutomationPositionPolicy.InputRefused"/>), or
    /// when what the action needs is missing. A preview names a new position
    /// as an add does, or a position on the estimate by positionId alone. The
    /// session is named once found.
    /// </summary>
    private static GlassAutomationPositionRequest PositionRequest(
        ActionActor actor, string? action, string key, long expectedSessionVersion,
        string? kind, string? row, string? operation, string? side, string? positionId,
        GlassPositionInputsToolInput? inputs)
    {
        var given = InputsOf(inputs);
        var verb = action?.Trim();
        if ((verb == "preview" && !string.IsNullOrWhiteSpace(positionId)) || verb == "change")
        {
            // A position already on the estimate (R1206): change takes what its dialog lets a
            // person change, and preview shows it with what that change would send.
            if (kind is not null || row is not null || operation is not null || side is not null)
            {
                throw new McpException(verb == "change"
                    ? AutomationToolText.GlassChangePositionAndInputs
                    : AutomationToolText.GlassPreviewPositionAlone);
            }
            if (verb == "change" && (string.IsNullOrWhiteSpace(positionId) || given is null))
            {
                throw new McpException(AutomationToolText.GlassChangePositionAndInputs);
            }
            if (!GlassAutomationRead.IsPositionId(positionId!.Trim()))
            {
                throw new McpException(AutomationToolText.GlassReadPositionIdRefused);
            }

            var onPosition = verb == "change" ? GlassAutomationActAction.Change : GlassAutomationActAction.Preview;
            if (GlassAutomationPositionPolicy.InputRefused(onPosition, null, null, given) is { } notChanged)
            {
                throw new McpException(InputRefusal(notChanged));
            }

            return new(actor, Guid.Empty, expectedSessionVersion, key, onPosition)
            {
                PositionId = positionId.Trim(),
                Inputs = given,
            };
        }

        switch (verb)
        {
            case "remove":
                if (string.IsNullOrWhiteSpace(positionId))
                {
                    throw new McpException(AutomationToolText.GlassRemovePositionIdRefused);
                }

                if (GlassAutomationPositionPolicy.InputRefused(GlassAutomationActAction.Remove, null, null, given) is { } notTaken)
                {
                    throw new McpException(InputRefusal(notTaken));
                }

                return new(actor, Guid.Empty, expectedSessionVersion, key, GlassAutomationActAction.Remove)
                {
                    PositionId = positionId.Trim(),
                };
            case "add" or "preview":
                var wantedKind = kind?.Trim() switch
                {
                    "part" => GlassAutomationPositionKind.Part,
                    "special_operation" => GlassAutomationPositionKind.SpecialOperation,
                    _ => throw new McpException(AutomationToolText.GlassAddKindRefused),
                };
                if (string.IsNullOrWhiteSpace(row))
                {
                    throw new McpException(AutomationToolText.GlassAddRowRefused);
                }

                GlassAutomationPartOperation? wantedOperation = operation?.Trim() switch
                {
                    null or "" => null,
                    "part" => GlassAutomationPartOperation.Part,
                    "part_and_paint" => GlassAutomationPartOperation.PartAndPaint,
                    "paint" => GlassAutomationPartOperation.Paint,
                    "repair" => GlassAutomationPartOperation.Repair,
                    "remove_and_refit" => GlassAutomationPartOperation.RemoveAndRefit,
                    _ => throw new McpException(AutomationToolText.GlassAddOperationRefused),
                };
                if (wantedKind == GlassAutomationPositionKind.Part && wantedOperation is null)
                {
                    throw new McpException(AutomationToolText.GlassAddOperationMissing);
                }
                if (wantedKind == GlassAutomationPositionKind.SpecialOperation && wantedOperation is not null)
                {
                    throw new McpException(AutomationToolText.GlassAddOperationNotForSpecial);
                }

                var wantedAction = verb == "add" ? GlassAutomationActAction.Add : GlassAutomationActAction.Preview;
                if (GlassAutomationPositionPolicy.InputRefused(wantedAction, wantedKind, wantedOperation, given) is { } refused)
                {
                    throw new McpException(InputRefusal(refused));
                }

                return new(actor, Guid.Empty, expectedSessionVersion, key, wantedAction)
                {
                    Kind = wantedKind,
                    Row = row.Trim(),
                    Operation = wantedOperation,
                    Side = SideOf(side),
                    Inputs = given,
                };
            default:
                throw new McpException(AutomationToolText.GlassPositionActionRefused);
        }
    }

    /// <summary>
    /// The inputs as the tool takes them (R1206), each value trimmed, or null
    /// when none is given; a button or a side the tool does not have is
    /// refused here.
    /// </summary>
    private static GlassAutomationInputs? InputsOf(GlassPositionInputsToolInput? inputs)
    {
        if (inputs is null)
        {
            return null;
        }

        GlassAutomationButton? button = inputs.Button?.Trim() switch
        {
            null => null,
            "part" => GlassAutomationButton.Part,
            "paint" => GlassAutomationButton.Paint,
            "part_and_paint" => GlassAutomationButton.PartAndPaint,
            _ => throw new McpException(AutomationToolText.GlassInputsButtonRefused),
        };
        bool? both = inputs.Side?.Trim() switch
        {
            null => null,
            "both" => true,
            _ => throw new McpException(AutomationToolText.GlassInputsSideRefused),
        };
        return GlassAutomationPositionPolicy.Kept(new GlassAutomationInputs(
            inputs.Equipment?.Select(code => code?.Trim() ?? string.Empty).ToList(),
            inputs.Operation?.Trim(),
            button,
            both,
            inputs.OptionalParts?.Select(code => code?.Trim() ?? string.Empty).ToList(),
            inputs.ConfirmNoFigures,
            inputs.Hours,
            inputs.Price,
            inputs.OverlapHours,
            inputs.Reason,
            inputs.Paint is { } paint
                ? new GlassAutomationPaint(paint.Coats?.Trim(), paint.Level?.Trim(), paint.Surcharge, paint.Hours)
                : null));
    }

    /// <summary>The sentence an input the act does not take is refused with, by its tool name.</summary>
    private static string InputRefusal(string input) => input switch
    {
        "inputs.equipment" => AutomationToolText.GlassInputsEquipmentRefused,
        "inputs.operation" => AutomationToolText.GlassInputsOperationRefused,
        "inputs.button" => AutomationToolText.GlassInputsButtonRefused,
        "inputs.side" => AutomationToolText.GlassInputsSideRefused,
        "inputs.optionalParts" => AutomationToolText.GlassInputsOptionalPartsRefused,
        "inputs.hours" => AutomationToolText.GlassInputsHoursRefused,
        "inputs.price" => AutomationToolText.GlassInputsPriceRefused,
        "inputs.overlapHours" => AutomationToolText.GlassInputsOverlapHoursRefused,
        "inputs.reason" => AutomationToolText.GlassInputsReasonRefused,
        "inputs.paint" => AutomationToolText.GlassInputsPaintRefused,
        _ => AutomationToolText.GlassInputsConfirmNoFiguresRefused,
    };

    /// <summary>
    /// What session_get is asked to read of the open estimate (R1206), or
    /// null for none; refused before anything is read when the kind is not
    /// one the tool takes, a position read names no position id as Glass's
    /// lists them, or a position id is given for another read.
    /// </summary>
    private static GlassAutomationRead? ReadOf(string? read, string? positionId)
    {
        GlassAutomationRead? wanted = read?.Trim() switch
        {
            null or "" => null,
            "positions" => GlassAutomationRead.Positions,
            "calculation" => GlassAutomationRead.Calculation,
            "position" => GlassAutomationRead.IsPositionId(positionId?.Trim())
                ? GlassAutomationRead.Position(positionId!.Trim())
                : throw new McpException(AutomationToolText.GlassReadPositionIdRefused),
            _ => throw new McpException(AutomationToolText.GlassReadRefused),
        };
        if (wanted?.Kind != GlassAutomationReadKind.Position && !string.IsNullOrWhiteSpace(positionId))
        {
            throw new McpException(AutomationToolText.GlassReadPositionIdNotForRead);
        }

        return wanted;
    }

    /// <summary>A read's answer as session_get gives it: what was read, or the code of a read Glass's did not answer.</summary>
    private sealed record GlassReadAnswer(
        GlassEstimateToolItem? Estimate,
        GlassPositionToolItem? Position,
        GlassCalculationToolItem? Calculation,
        string? Code)
    {
        public static GlassReadAnswer None { get; } = new(null, null, null, null);

        public static GlassReadAnswer Of(GlassAutomationEstimate read) =>
            new(
                read.PositionIds is { } positions
                    ? new GlassEstimateToolItem(
                        positions,
                        read.Codes ?? [],
                        read.Totals?.Select(total => new GlassTotalToolItem(total.Label, total.Amount)).ToList())
                    : null,
                read.Position is { } position ? MapPosition(position) : null,
                read.Calculation is { } calculation
                    ? new GlassCalculationToolItem(
                        calculation.Trades
                            .Select(trade => new GlassCalculationTradeToolItem(
                                trade.Trade, trade.LabourRate, trade.LabourTime, trade.LabourCosts, trade.MaterialCosts))
                            .ToList(),
                        calculation.TotalLabourTime,
                        calculation.TotalLabourCosts,
                        calculation.TotalMaterial,
                        calculation.RepairCostsExclVat,
                        calculation.VatLabel,
                        calculation.Vat,
                        calculation.RepairCostsInclVat)
                    : null,
                read.Code);
    }

    /// <summary>One position's figures as Glass's record holds them, as the tools answer them.</summary>
    private static GlassPositionToolItem MapPosition(GlassPositionFigures position) =>
        new(
            position.PositionId,
            position.PartCode,
            position.Description,
            position.Operation,
            position.Price,
            position.Hours,
            position.Side,
            position.PartNumber,
            position.Paint is { } paint
                ? new GlassPositionPaintToolItem(paint.PositionId, paint.Description, paint.Price, paint.LabourTime)
                : null);

    /// <summary>A side as a tool takes it: L or R, or none when omitted.</summary>
    private static string? SideOf(string? side) => side?.Trim() switch
    {
        null or "" => null,
        "L" => "L",
        "R" => "R",
        _ => throw new McpException(AutomationToolText.GlassSideRefused),
    };

    private static string KindName(GlassAutomationPositionKind kind) =>
        kind == GlassAutomationPositionKind.SpecialOperation ? "special_operation" : "part";

    private static string OperationName(GlassAutomationPartOperation operation) => operation switch
    {
        GlassAutomationPartOperation.PartAndPaint => "part_and_paint",
        GlassAutomationPartOperation.Paint => "paint",
        GlassAutomationPartOperation.Repair => "repair",
        GlassAutomationPartOperation.RemoveAndRefit => "remove_and_refit",
        _ => "part",
    };

    private static string OutcomeName(GlassAutomationActOutcome outcome) => outcome switch
    {
        GlassAutomationActOutcome.Added => "added",
        GlassAutomationActOutcome.Changed => "changed",
        GlassAutomationActOutcome.Removed => "removed",
        GlassAutomationActOutcome.NotConfirmed => "not_confirmed",
        GlassAutomationActOutcome.Refused => "refused",
        _ => "pending",
    };

    private static GlassActToolItem MapAct(GlassAutomationAct act) =>
        new(
            act.OperationKey,
            act.Action switch
            {
                GlassAutomationActAction.Remove => "remove",
                GlassAutomationActAction.Change => "change",
                _ => "add",
            },
            OutcomeName(act.Outcome),
            act.Code,
            act.Kind is { } kind ? KindName(kind) : null,
            act.Row,
            act.Operation is { } operation ? OperationName(operation) : null,
            act.Side,
            act.PositionId,
            act.PositionIds,
            act.Description,
            act.Served?.Price,
            act.Served?.Hours,
            act.Listed.Select(listed => new GlassListedPartToolItem(listed.Code, listed.Text, listed.Ticked, listed.Optional)).ToList(),
            act.RequestedAtUtc,
            act.Inputs is { } given
                ? new GlassPositionInputsToolInput(
                    given.Equipment,
                    given.Operation,
                    given.Button switch
                    {
                        GlassAutomationButton.Part => "part",
                        GlassAutomationButton.Paint => "paint",
                        GlassAutomationButton.PartAndPaint => "part_and_paint",
                        _ => null,
                    },
                    given.BothSides == true ? "both" : null,
                    given.OptionalParts,
                    given.ConfirmNoFigures,
                    given.Hours,
                    given.Price,
                    given.OverlapHours,
                    given.Reason,
                    given.Paint is { } paint
                        ? new GlassPaintInputsToolInput(paint.Coats, paint.Level, paint.Surcharge, paint.Hours)
                        : null)
                : null,
            MapEquipment(act.Equipment),
            act.RowPair,
            act.EquipmentChanged.Count > 0
                ? act.EquipmentChanged.Select(change => new GlassEquipmentChangeToolItem(change.Code, change.Ticked)).ToList()
                : null,
            act.EquipmentStored ? true : null,
            MapFigures(act.DialogServed),
            MapFigures(act.Sent),
            act.Position is { } position ? MapPosition(position) : null);

    private static GlassFiguresToolItem? MapFigures(GlassAutomationFigures? figures) =>
        figures is null
            ? null
            : new GlassFiguresToolItem(
                figures.Price, figures.Hours, figures.OverlapHours,
                figures.PaintCoats, figures.PaintLevel, figures.PaintPrice, figures.PaintHours, figures.Surcharge);

    /// <summary>A part's equipment question as the tools answer it; Glass's own names and note pass through unchanged.</summary>
    private static GlassEquipmentToolItem? MapEquipment(GlassEquipmentQuestion? question) =>
        question is null
            ? null
            : new GlassEquipmentToolItem(
                question.Options
                    .Select(option => new GlassEquipmentOptionToolItem(
                        option.Code, option.Name, option.Ticked, option.Greyed, option.Group, option.Note))
                    .ToList(),
                MapAnswers(question.Answers),
                MapAnswers(question.MapParts),
                MapAnswers(question.MapHours));

    private static List<GlassEquipmentAnswerToolItem>? MapAnswers(IReadOnlyList<GlassEquipmentAnswer>? answers) =>
        answers?.Select(answer => new GlassEquipmentAnswerToolItem(
            answer.Ticked, answer.Rows, answer.PartNumber, answer.Price, answer.Hours)).ToList();

    /// <summary>
    /// start: under the Case's one-command lease, the prepare half proves and
    /// claims, then the launch runs as provider work the call waits for. The
    /// new session's id comes from the operation key, so a replay names the
    /// same one.
    /// </summary>
    private async Task<GlassRepairEstimateSession?> StartAsync(
        ActionActor actor, Guid caseId, long expectedVersion, string key, string? editLeaseToken,
        CancellationToken cancellationToken)
    {
        // Work already in hand for the Automation's session here is not
        // overtaken: the session is answered as it stands.
        var current = await sessions.GetAutomationForCaseAsync(caseId, cancellationToken);
        if (current is not null && queue.IsInFlight(current.Id))
        {
            return current;
        }

        var sessionId = AutomationMcpErrors.DeriveOperationGuid("glass-start", actor, key);
        // The Automation's live session here is the one a start continues.
        var live = current is not null
            && current.Id != sessionId
            && GlassRepairEstimateSessionPolicy.OccupiesAccount(current.State)
                ? current.Id
                : (Guid?)null;
        return await leases.RunCaseAsync(
            caseId,
            expectedVersion,
            editLeaseToken,
            actor,
            key,
            async token =>
            {
                // Both sessions a start may act on are held busy from before
                // its claim, so nothing settles either between the claim and
                // its work.
                using var reservation = queue.Reserve(sessionId);
                using var continued = live is { } liveId ? queue.Reserve(liveId) : null;
                GlassRepairEstimateStep step;
                try
                {
                    step = await gateway.PrepareAutomationLaunchAsync(
                        new GlassRepairEstimateLaunchRequest(actor, caseId, expectedVersion, token, key, sessionId),
                        cancellationToken);
                }
                catch (GlassRepairEstimateSessionConflictException conflict)
                    when (conflict.Conflict == GlassRepairEstimateSessionConflict.ActiveAccount)
                {
                    throw new McpException(await HeldAsync(actor, conflict, cancellationToken));
                }

                if (step.Continuation == GlassRepairEstimateContinuation.None)
                {
                    return step.Session;
                }

                var id = step.Session.Id;
                using var late = id == sessionId || id == live ? null : queue.Reserve(id);
                await RunAsync(
                    id == sessionId ? reservation : late ?? continued!,
                    "GlassAutomationStart",
                    (work, workToken) => work.GetRequiredService<IGlassRepairEstimateGateway>()
                        .ContinueAutomationStartAsync(new(actor, id), workToken),
                    cancellationToken);
                return await sessions.GetAutomationForCaseAsync(caseId, cancellationToken);
            },
            cancellationToken);
    }

    /// <summary>
    /// finish: under the Case's one-command lease, so the import lands under
    /// the authority this call proves; the save and the return run as
    /// provider work the call waits for. A held estimate is landed the same
    /// way. An estimate with no position is refused and nothing is saved.
    /// </summary>
    private async Task<GlassRepairEstimateSession?> FinishAsync(
        ActionActor actor, Guid caseId, long expectedVersion, long expectedSessionVersion, string key,
        string? editLeaseToken, CancellationToken cancellationToken)
    {
        var session = await sessions.GetAutomationForCaseAsync(caseId, cancellationToken);
        if (session is not null
            && string.Equals(session.Automation?.FinishOperationKey, key, StringComparison.Ordinal)
            && session.State != GlassRepairEstimateSessionState.AwaitingImport)
        {
            // The same finish again: the session as it stands.
            return session;
        }
        if (session is null || !GlassRepairEstimateSessionPolicy.OccupiesAccount(session.State))
        {
            await RefuseEngineersSessionAsync(caseId, cancellationToken);
            throw new McpException(AutomationToolText.GlassNoSession);
        }
        if (queue.IsInFlight(session.Id))
        {
            return session;
        }

        var id = session.Id;
        var finished = await leases.RunCaseAsync(
            caseId,
            expectedVersion,
            editLeaseToken,
            actor,
            key,
            async token =>
            {
                using var reservation = queue.Reserve(id);
                var step = await gateway.PrepareAutomationFinishAsync(
                    new GlassAutomationFinishRequest(actor, id, expectedSessionVersion, expectedVersion, token, key),
                    cancellationToken);
                if (step.Continuation == GlassRepairEstimateContinuation.None)
                {
                    return step.Session;
                }

                await RunAsync(
                    reservation,
                    step.Continuation == GlassRepairEstimateContinuation.Import ? "GlassImport" : "GlassAutomationFinish",
                    (work, workToken) =>
                    {
                        var glass = work.GetRequiredService<IGlassRepairEstimateGateway>();
                        return step.Continuation == GlassRepairEstimateContinuation.Import
                            ? glass.ContinueImportAsync(new(actor, id), workToken)
                            : glass.ContinueAutomationFinishAsync(new(actor, id), workToken);
                    },
                    cancellationToken);
                return await sessions.GetAutomationForCaseAsync(caseId, cancellationToken);
            },
            cancellationToken);
        if (finished is { State: GlassRepairEstimateSessionState.Active, FailureCode: GlassRepairEstimateSessionPolicy.AutomationEmptyFailureCode }
            && finished.Id == id
            && !queue.IsInFlight(id))
        {
            throw new McpException(AutomationToolText.GlassEstimateEmpty);
        }

        return finished;
    }

    /// <summary>
    /// close: the call is the confirmation of external closure, as a member
    /// of staff's Close is. Nothing is sent to Glass's and no Case lease is
    /// taken. Refused while the session's work runs, as the staff Close is.
    /// </summary>
    private async Task<GlassRepairEstimateSession> CloseAsync(
        ActionActor actor, Guid caseId, long expectedSessionVersion, string? reason,
        CancellationToken cancellationToken)
    {
        var session = await sessions.GetAutomationForCaseAsync(caseId, cancellationToken);
        if (session is null || !GlassRepairEstimateSessionPolicy.OccupiesAccount(session.State))
        {
            await RefuseEngineersSessionAsync(caseId, cancellationToken);
            throw new McpException(AutomationToolText.GlassNoSession);
        }
        if (queue.IsInFlight(session.Id))
        {
            throw new McpException(CaseWorkspaceLabels.GlassSession.CloseWhileWorking);
        }

        return await gateway.CloseAsync(
            new GlassRepairEstimateCloseRequest(
                actor, session.Id, expectedSessionVersion, ExternalSessionClosed: true, reason ?? string.Empty),
            cancellationToken);
    }

    /// <summary>
    /// Queues the work under the reservation and waits for it for up to
    /// <see cref="AnswerWindow"/>. Work in the queue runs to its own cap
    /// whatever the caller does, so a client that gives up never cancels a
    /// request to Glass's halfway. A full queue runs the work here, under its
    /// own cap rather than the call's.
    /// </summary>
    private async Task RunAsync(
        ProviderWorkReservation reservation,
        string kind,
        Func<IServiceProvider, CancellationToken, Task> run,
        CancellationToken cancellationToken)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = new ProviderWork(
            reservation.Key,
            kind,
            async (provider, token) =>
            {
                try
                {
                    await run(provider, token);
                }
                finally
                {
                    done.TrySetResult();
                }
            });
        if (reservation.Admit(work) == ProviderWorkAdmission.Full)
        {
            using var cap = new CancellationTokenSource(ProviderWorkService.TimeCap, timeProvider);
            await reservation.RunHereAsync(work, services, cap.Token);
            return;
        }

        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await Task.WhenAny(done.Task, Task.Delay(AnswerWindow, timeProvider, wait.Token));
        await wait.CancelAsync();
    }

    /// <summary>One read that asks Glass's, run here under the session's turn in the queue.</summary>
    private async Task<T> RunHereAsync<T>(
        Guid sessionId, string kind, Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken)
    {
        using var reservation = queue.Reserve(sessionId);
        T result = default!;
        await reservation.RunHereAsync(
            new ProviderWork(sessionId, kind, async (_, token) => result = await read(token)),
            services,
            cancellationToken);
        return result;
    }

    /// <summary>
    /// The refusal that names who holds the Glass's account, and on which
    /// Case. The conflict names the session in the way, or, when the
    /// account's index decided a race, the session that was not made; the
    /// holder is then read off the account. One freed meanwhile leaves the
    /// conflict's own sentence.
    /// </summary>
    private async Task<string> HeldAsync(
        ActionActor actor, GlassRepairEstimateSessionConflictException conflict, CancellationToken cancellationToken)
    {
        var holder = (await store.GetAsync(conflict.SessionId, cancellationToken))?.Session;
        if (holder is null || !GlassRepairEstimateSessionPolicy.OccupiesAccount(holder.State))
        {
            holder = await sessions.FindLiveForAccountAsync(
                services.GetRequiredService<GlassAutomationAccountSource>().Read().AccountKey, cancellationToken);
        }
        if (holder is null)
        {
            return conflict.Message;
        }

        var reference = await ReferenceAsync(actor, holder.CaseId, cancellationToken);
        return holder.OwnerKind == ActorKind.Automation
            ? AutomationToolText.GlassAccountHeldByAutomation(reference)
            : AutomationToolText.GlassAccountHeldByStaff(reference);
    }

    /// <summary>
    /// Whether the Automation's Glass's account is free, or which Case holds
    /// it and whether staff or the Automation do. No member of staff is named.
    /// </summary>
    private async Task<GlassAccountToolItem> AccountAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        var account = services.GetRequiredService<GlassAutomationAccountSource>().Read();
        var live = await sessions.FindLiveForAccountAsync(account.AccountKey, cancellationToken);
        return live is null
            ? new GlassAccountToolItem(true, null, null, null)
            : new GlassAccountToolItem(
                false,
                live.OwnerKind == ActorKind.Automation ? "automation" : "staff",
                live.CaseId,
                await ReferenceAsync(actor, live.CaseId, cancellationToken));
    }

    private async Task<string> ReferenceAsync(ActionActor actor, Guid caseId, CancellationToken cancellationToken)
    {
        var references = await listCaseReferences.ExecuteAsync(new(actor, [caseId]), cancellationToken);
        return references.GetValueOrDefault(caseId, caseId.ToString("D"));
    }

    /// <summary>Help in an Engineer's session; resolved only on a host that reaches Glass's.</summary>
    private GlassSessionHelp Help => services.GetRequiredService<GlassSessionHelp>();

    /// <summary>The editor keys the helper handed over; resolved only on a host that reaches Glass's.</summary>
    private GlassHelpKeys Keys => services.GetRequiredService<GlassHelpKeys>();

    /// <summary>
    /// The session the tools act in on the Case (<see cref="GlassSessionHelpPolicy.Find"/>).
    /// An Engineer's session found no longer <c>Active</c> forgets its key.
    /// </summary>
    private GlassSessionHelpTarget Found(GlassSessionHelpCandidates candidates, Guid? sessionId)
    {
        var target = GlassSessionHelpPolicy.Find(candidates, sessionId);
        if (target is { IsStaff: true, Session: { } ended }
            && ended.State != GlassRepairEstimateSessionState.Active)
        {
            Keys.Drop(ended.Id);
        }

        return target;
    }

    private async Task<GlassSessionHelpTarget> FindAsync(
        Guid caseId, Guid? sessionId, CancellationToken cancellationToken) =>
        Found(await sessions.FindHelpableForCaseAsync(caseId, cancellationToken), sessionId);

    /// <summary>
    /// The session an act runs in, or its refusal: none chosen, an Engineer's
    /// that may not be acted in, or one whose provider work runs. The
    /// Automation's own is answered as it stands, for its own checks.
    /// </summary>
    private GlassRepairEstimateSession RequireActable(GlassSessionHelpTarget target)
    {
        if (target.Session is not { } session)
        {
            throw new McpException(AutomationToolText.GlassHelpRefusal(target.Code ?? GlassSessionHelpPolicy.NoSessionCode));
        }
        if (target.IsStaff)
        {
            if ((target.Code ?? (queue.IsInFlight(session.Id) ? GlassSessionHelpPolicy.BusyCode : null)) is { } code)
            {
                throw new McpException(AutomationToolText.GlassHelpRefusal(code));
            }
        }

        return session;
    }

    /// <summary>An Engineer's session whose provider work runs (a resume, the return) is busy.</summary>
    private void RequireIdle(GlassRepairEstimateSession session)
    {
        if (queue.IsInFlight(session.Id))
        {
            throw new GlassSessionHelpRefusalException(GlassSessionHelpPolicy.BusyCode);
        }
    }

    /// <summary>A help call, its glass.help.* refusal answered with its sentence.</summary>
    private static async Task<T> HelpAsync<T>(Func<Task<T>> help)
    {
        try
        {
            return await help();
        }
        catch (GlassSessionHelpRefusalException refused)
        {
            throw new McpException(AutomationToolText.GlassHelpRefusal(refused.Code));
        }
    }

    /// <summary>
    /// start, finish and close are the Automation's own session's: on a Case
    /// whose only session open to it is an Engineer's, they say so.
    /// </summary>
    private async Task RefuseEngineersSessionAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var candidates = await sessions.FindHelpableForCaseAsync(caseId, cancellationToken);
        if (candidates.Staff.Any(item => GlassSessionHelpPolicy.RefusalOf(item) is null))
        {
            throw new McpException(
                AutomationToolText.GlassHelpRefusal(GlassSessionHelpPolicy.NotAutomationSessionCode));
        }
    }

    private void RequireAvailable()
    {
        if (!availability.Enabled)
        {
            throw new McpException(CaseWorkspaceLabels.GlassSession.Unavailable);
        }
    }

    private static long RequireValue(long? value, string name) =>
        value ?? throw new McpException($"{name} is required for this action.");

    private GlassSessionToolItem? Map(GlassRepairEstimateSession? session) =>
        session is null ? null : MapSession(session);

    /// <summary>
    /// A session as the tools answer it. An Engineer's says whether help is
    /// on and whether their window is connected, and names no member of staff.
    /// </summary>
    private GlassSessionToolItem MapSession(GlassRepairEstimateSession session)
    {
        var staff = session.OwnerKind == ActorKind.Staff;
        return new GlassSessionToolItem(
            session.Id,
            session.Version,
            session.State.ToString(),
            session.FailureCode,
            session.CreatedAtUtc,
            queue.IsInFlight(session.Id),
            session.Automation?.DraftSpecificationId,
            staff ? "staff" : "automation",
            staff ? GlassSessionHelpPolicy.HelpOn(session) : null,
            staff ? Keys.IsHeld(session.Id) : null);
    }
}
