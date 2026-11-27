using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Operations;

namespace Pegasus.Web.Mcp;

internal sealed record ClassifierRunFactsToolInput(
    [property: Description("Whether the run writes nothing to Pegasus.")] bool DryRun,
    [property: Description("The model the run calls.")] string Model,
    [property: Description("The most e-mails one run deals with.")] int Batch,
    [property: Description("The mail list pages one run reads.")] int ScanPages,
    [property: Description("The run's spending cap, in US dollars.")] decimal MaxBudgetUsd,
    [property: Description("The classifier's version, from its package.")] string? AgentVersion = null);

internal sealed record ClassifierRunItemToolInput(
    [property: Description("The retained message identifier from pegasus_mail_list.")] Guid MessageId,
    [property: Description("What the run did with it: classified, linked, case_created, one_off_case, triage_opened, query_opened, left, left_for_staff, deferred, failed, set_aside or not_reached.")] string Action,
    [property: Description("The mail category the run gave it, when it gave one.")] string? Category = null,
    [property: Description("The Case it was linked to or became, when there is one.")] Guid? CaseId = null,
    [property: Description("That Case's reference.")] string? CaseReference = null,
    [property: Description("The reason the run gave for the act.")] string? Reason = null,
    [property: Description("Pegasus's refusal, or why the act failed.")] string? Refusal = null,
    [property: Description("When the run acted on it.")] DateTimeOffset? At = null);

internal sealed record ClassifierRunResultToolInput(
    [property: Description("How the run ended, as the classifier reports it.")] string Outcome,
    [property: Description("The e-mails the run had to deal with.")] int Eligible,
    [property: Description("Pegasus's count of mail awaiting a decision, from the first list page.")] int AwaitingDecision,
    [property: Description("Whether the list showed no candidate while that count was above zero.")] bool CandidatesHidden,
    [property: Description("Runs in a row that found the list hiding mail.")] int HiddenRuns,
    [property: Description("Messages given a verdict.")] int Verdicts,
    [property: Description("Messages whose write failed.")] int Failed,
    [property: Description("Messages left until the lookups are back.")] int Deferred,
    [property: Description("The model's turns.")] int Turns,
    [property: Description("What the model's calls cost, in US dollars.")] decimal CostUsd,
    [property: Description("How long the run took, in milliseconds.")] int DurationMs,
    [property: Description("One row per message of the batch.")] IReadOnlyList<ClassifierRunItemToolInput>? Items = null,
    [property: Description("Why the run failed, when it did.")] string? Error = null,
    [property: Description("The model's closing summary.")] string? Summary = null);

internal sealed record ClassifierRunRecordToolResult(
    Guid RunId,
    string Stage,
    bool IsReplay,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    string CorrelationId);

/// <summary>
/// The mail classifier's record of its own runs (FRD-08, FRD-10; operator,
/// 27 November 2026; R1318), in the mail scope beside the tools it acts with.
/// It is the classifier's own record, so only the Automation Actor writes
/// one; a member of staff signed in from Pegasus Desktop is refused.
/// </summary>
[McpServerToolType]
internal sealed class ClassifierRunMcpTools(
    RecordClassifierRun record,
    AutomationMcpAuditor auditor)
{
    private const string StageValues = "stage must be open or close.";

    [McpServerTool(
        Name = "pegasus_classifier_run_record",
        Title = "Record a classifier run",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("The mail classifier's record of one run, for the Administrator's Classifier page: open as a run starts, with the settings it runs with; close as it ends, with how it ended and one row per message of its batch. A close whose open never arrived opens the run too. A run is opened once and closed once: repeating either answers the run as recorded and changes nothing. Changes no mail, Case or classification. For the Automation Actor only.")]
    public async Task<ClassifierRunRecordToolResult> RecordAsync(
        [Description("The run's identifier, made by the classifier once per run.")] Guid runId,
        [Description("open or close.")] string stage,
        [Description(AutomationToolText.OperationKey)] string operationKey,
        [Description("The settings the run started with, on both stages.")] ClassifierRunFactsToolInput facts,
        [Description("close only: how the run ended and its messages.")] ClassifierRunResultToolInput? result = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_classifier_run_record",
            AutomationMcpAuditor.Resource(runId),
            operationKey,
            async (context, key) =>
            {
                var closing = (stage?.Trim().ToLowerInvariant()) switch
                {
                    "open" => false,
                    "close" => true,
                    _ => throw new McpException(StageValues)
                };
                if (closing != (result is not null))
                {
                    throw new McpException("result belongs to close, and close needs it.");
                }

                var recorded = await record.ExecuteAsync(
                    new(
                        AutomationMcpErrors.RequireId(runId, "run identifier"),
                        context.Actor,
                        key,
                        Facts(facts),
                        result is null ? null : Result(result)),
                    cancellationToken);
                return new ClassifierRunRecordToolResult(
                    recorded.RunId,
                    closing ? "close" : "open",
                    recorded.IsReplay,
                    recorded.StartedAtUtc,
                    recorded.FinishedAtUtc,
                    AutomationMcpAuditor.CorrelationId(context, key));
            },
            cancellationToken);
    }

    private static ClassifierRunFacts Facts(ClassifierRunFactsToolInput facts) =>
        facts is null
            ? throw new McpException("facts are required on both stages.")
            : new(facts.DryRun, facts.Model, facts.Batch, facts.ScanPages, facts.MaxBudgetUsd, facts.AgentVersion ?? string.Empty);

    private static ClassifierRunResult Result(ClassifierRunResultToolInput result) =>
        new(
            result.Outcome,
            result.Eligible,
            result.AwaitingDecision,
            result.CandidatesHidden,
            result.HiddenRuns,
            result.Verdicts,
            result.Failed,
            result.Deferred,
            result.Turns,
            result.CostUsd,
            result.DurationMs,
            result.Error,
            result.Summary,
            [.. (result.Items ?? []).Select(Item)]);

    private static ClassifierRunItem Item(ClassifierRunItemToolInput item) =>
        item is null
            ? throw new McpException("Each item names a message and what the run did with it.")
            : new(
                item.MessageId,
                ClassifierRunPolicy.ActionFromWire(item.Action)
                    ?? throw new McpException($"'{item.Action}' is not an act the classifier records."),
                item.Category,
                item.CaseId,
                item.CaseReference,
                item.Reason,
                item.Refusal,
                item.At);
}
