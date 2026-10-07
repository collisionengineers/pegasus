using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Fills a linked Case's empty Roadworthiness and repair outcome from a Triage
/// finding, inside the caller's transaction, as <see cref="TriageFindingFill"/>
/// decides. The fill is system work: whoever is editing keeps their session,
/// and its history records the value each cell held before, so a Save prepared
/// before the fill keeps it (<see cref="EfVehicleLookupWorkStore.FilledSinceAsync"/>).
/// A closed or archived Case is never filled.
/// </summary>
internal static class TriageFindingFillWriter
{
    public const string EventType = "triage_finding_filled";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Whether anything was filled. <paramref name="beforeVersion"/> is the
    /// version the caller already advanced the Case from in this transaction
    /// (a link), so the fill shares that write; null advances the Case here.
    /// This never saves — the caller's transaction does.
    /// </summary>
    public static async Task<bool> ApplyAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        long? beforeVersion,
        RoadworthinessFinding? roadworthiness,
        AssessmentFinding? assessment,
        string operationKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(workflow);
        var values = TriageFindingFill.Values(roadworthiness, assessment);
        if (values.Count == 0
            || workflow.ArchivedAtUtc is not null
            || !Enum.TryParse<CaseLifecycleState>(workflow.State, ignoreCase: false, out var state)
            || CaseLifecycleRules.IsTerminal(state))
        {
            return false;
        }

        var workId = await CaseWorkScope.CurrentIdAsync(context, workflow.CaseId, cancellationToken);
        var paths = values.Keys.ToArray();
        var existing = await context.CaseAssessmentFields
            .Where(item => item.WorkId == workId && paths.Contains(item.FieldPath))
            .ToDictionaryAsync(item => item.FieldPath, StringComparer.Ordinal, cancellationToken);
        var filled = new Dictionary<string, string?>(StringComparer.Ordinal);
        var after = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (path, value) in values)
        {
            var row = existing.GetValueOrDefault(path);
            if (!TriageFindingFill.Fills(row?.Value))
            {
                continue;
            }

            AssessmentFieldWriter.Write(
                context, workId, row, path, value,
                ActorKind.Automation, TriageFindingFill.RecorderId, now);
            // The work showed no value before the fill, whatever blank row it held.
            filled[path] = null;
            after[path] = value;
        }

        if (filled.Count == 0)
        {
            return false;
        }

        var fromVersion = beforeVersion ?? workflow.Version;
        if (beforeVersion is null)
        {
            CaseMutationGuard.Advance(workflow);
        }

        var resultJson = JsonSerializer.Serialize(new { Filled = filled }, JsonOptions);
        const string reason = "The linked Triage finding filled the Case's empty findings.";
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Workflow = workflow,
            EventType = EventType,
            // A key of its own beside the caller's, within the column's 100 characters.
            OperationKey = $"triage-fill:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(operationKey)))[..32]}",
            RequestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(resultJson))),
            ActorKind = ActorKind.Automation.ToString(),
            ActorSubjectId = TriageFindingFill.RecorderId,
            ActorRolesJson = "[]",
            Reason = reason,
            OccurredAtUtc = now,
            BeforeVersion = fromVersion,
            AfterVersion = workflow.Version,
            ResultJson = resultJson
        });
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = "case",
            AggregateId = workflow.CaseId.ToString("D"),
            EventKind = EventType,
            ActorKind = ActorKind.Automation.ToString(),
            ActorSubjectId = TriageFindingFill.RecorderId,
            ActorRolesJson = "[]",
            OccurredAtUtc = now,
            Outcome = "Succeeded",
            CorrelationId = operationKey,
            Reason = reason,
            BeforeJson = null,
            AfterJson = resultJson,
            PolicyVersion = $"{AssessmentPolicy.PolicyKey}/v{AssessmentPolicy.PolicyVersion}"
        });

        var freshness = CaseReportFreshness.ClassifyAssessment(filled, after);
        if (freshness.IsStale)
        {
            await EfCaseReportGenerationStore.MarkWorkStaleAsync(
                context, workflow.CaseId, workId, freshness.ReasonCode!, now, cancellationToken);
        }

        return true;
    }
}
