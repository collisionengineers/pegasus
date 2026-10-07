using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Fills a work's empty VIN with the one Glass's named, inside the caller's
/// transaction, as <see cref="GlassVinFillPolicy"/> decides. The fill is system
/// work of its own: the Case version advances, whoever is editing keeps their
/// session, and its history records the value the work held before, so a Save
/// prepared before the fill keeps it
/// (<see cref="EfVehicleLookupWorkStore.FilledSinceAsync"/>).
/// </summary>
internal static class GlassVinFillWriter
{
    public const string EventType = "glass_vin_filled";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Whether the VIN was filled. This never saves — the caller's transaction does.</summary>
    public static async Task<bool> ApplyAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        Guid workId,
        string? vin,
        string operationKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(workflow);
        var value = GlassVinFillPolicy.Carried(vin);
        if (value is null)
        {
            return false;
        }

        var existing = await context.CaseAssessmentFields.SingleOrDefaultAsync(
            item => item.WorkId == workId && item.FieldPath == AssessmentVocabulary.VehicleVin,
            cancellationToken);
        if (!GlassVinFillPolicy.Fills(existing?.Value))
        {
            return false;
        }

        AssessmentFieldWriter.Write(
            context, workId, existing, AssessmentVocabulary.VehicleVin, value,
            ActorKind.Automation, GlassVinFillPolicy.RecorderId, now);
        var beforeVersion = workflow.Version;
        CaseMutationGuard.Advance(workflow);

        // The work showed no VIN before the fill, whatever blank row it held.
        var filled = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [AssessmentVocabulary.VehicleVin] = null
        };
        var resultJson = JsonSerializer.Serialize(new { Filled = filled }, JsonOptions);
        const string reason = "Glass's VIN filled the Case's empty VIN.";
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Workflow = workflow,
            EventType = EventType,
            OperationKey = operationKey,
            RequestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(resultJson))),
            ActorKind = ActorKind.Automation.ToString(),
            ActorSubjectId = GlassVinFillPolicy.RecorderId,
            ActorRolesJson = "[]",
            Reason = reason,
            OccurredAtUtc = now,
            BeforeVersion = beforeVersion,
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
            ActorSubjectId = GlassVinFillPolicy.RecorderId,
            ActorRolesJson = "[]",
            OccurredAtUtc = now,
            Outcome = "Succeeded",
            CorrelationId = operationKey,
            Reason = reason,
            BeforeJson = null,
            AfterJson = resultJson,
            PolicyVersion = $"{AssessmentPolicy.PolicyKey}/v{AssessmentPolicy.PolicyVersion}"
        });

        var freshness = CaseReportFreshness.ClassifyAssessment(
            new Dictionary<string, string?>(StringComparer.Ordinal) { [AssessmentVocabulary.VehicleVin] = null },
            new Dictionary<string, string?>(StringComparer.Ordinal) { [AssessmentVocabulary.VehicleVin] = value });
        if (freshness.IsStale)
        {
            await EfCaseReportGenerationStore.MarkWorkStaleAsync(
                context, workflow.CaseId, workId, freshness.ReasonCode!, now, cancellationToken);
        }

        return true;
    }
}

/// <summary>
/// Records a Glass's valuation's VIN on the work it valued, in a transaction
/// of its own once the figures have been answered. Anything that stops the
/// fill — a closed Case, a concurrent write, the database — is logged and the
/// valuation stands without it.
/// </summary>
internal sealed partial class EfGlassVinFill(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider,
    ILogger<EfGlassVinFill> logger) : IFillGlassVin
{
    public async Task FillAsync(
        ActionActor actor,
        Guid caseId,
        CaseWorkSelector work,
        string vin,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var workflow = await context.CaseWorkflows
                .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken)
                ?? throw new KeyNotFoundException($"Case '{caseId}' was not found.");
            CaseMutationGuard.RequireOpen(workflow, actor);
            var workId = await CaseWorkScope.ResolveIdAsync(context, caseId, work, cancellationToken);
            if (!await GlassVinFillWriter.ApplyAsync(
                    context, workflow, workId, vin, $"glass-vin:{Guid.NewGuid():N}",
                    timeProvider.GetUtcNow(), cancellationToken))
            {
                return;
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogNotFilled(logger, caseId, exception.GetType().Name);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Glass's VIN for case {CaseId} was not recorded: {Reason}")]
    private static partial void LogNotFilled(ILogger logger, Guid caseId, string reason);
}
