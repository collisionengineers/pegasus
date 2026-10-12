using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake.ThirdPartyReports;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Vehicle;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Fills a Case's empty vehicle facts and VIN from a capture PDF's reading,
/// in a transaction of its own, as <see cref="VehicleCaptureFill"/> decides.
/// The fill is system work: whoever is editing keeps their session, and its
/// history records the VIN the work held before, so a Save prepared before the
/// fill keeps it (<see cref="EfVehicleLookupWorkStore.FilledSinceAsync"/>).
/// A closed or archived Case, or one that no longer names the printed
/// registration, is never filled. Anything that stops the fill is logged and
/// the filed photographs stand without it.
/// </summary>
internal sealed partial class EfVehicleCaptureFill(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider,
    ILogger<EfVehicleCaptureFill> logger) : IFillCaseFromVehicleCapture
{
    public const string EventType = "vehicle_capture_filled";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task FillAsync(Guid caseId, VehicleCaptureReading reading, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reading);
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var workflow = await context.CaseWorkflows
                .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
            if (workflow is null
                || workflow.ArchivedAtUtc is not null
                || !Enum.TryParse<CaseLifecycleState>(workflow.State, ignoreCase: false, out var state)
                || CaseLifecycleRules.IsTerminal(state))
            {
                return;
            }

            var now = timeProvider.GetUtcNow();
            var workId = await CaseWorkScope.CurrentIdAsync(context, caseId, cancellationToken);
            var caseDataFields = await context.CaseDataFields
                .Where(item => item.WorkId == workId)
                .ToListAsync(cancellationToken);
            var registration = EfVehicleWorkflowStore.CurrentRegistration(
                caseDataFields
                    .Where(item => item.FieldName == CaseDataFieldNames.VehicleRegistration)
                    .Select(item => (item.ValueKind, item.Value)));
            if (!string.Equals(registration, reading.Registration, StringComparison.Ordinal))
            {
                return;
            }

            var facts = FillCaseData(context, caseDataFields, workId, reading);
            var vinFilled = await FillVinAsync(context, workId, reading.Vin, now, cancellationToken);
            if (facts.Count == 0 && vinFilled is null)
            {
                return;
            }

            var beforeVersion = workflow.Version;
            CaseMutationGuard.Advance(workflow);
            // The work showed no VIN before the fill, whatever blank row it held.
            var filled = new Dictionary<string, string?>(StringComparer.Ordinal);
            if (vinFilled is not null)
            {
                filled[AssessmentVocabulary.VehicleVin] = null;
            }

            var resultJson = JsonSerializer.Serialize(new { Filled = filled, Facts = facts }, JsonOptions);
            const string reason = "The capture document filled the Case's empty vehicle details.";
            var operationKey = $"vehicle-capture:{reading.IntakeAssetId:N}:{caseId:N}";
            context.CaseWorkflowEvents.Add(new()
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                Workflow = workflow,
                EventType = EventType,
                OperationKey = $"{operationKey}:{beforeVersion}",
                RequestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(resultJson))),
                ActorKind = ActorKind.Automation.ToString(),
                ActorSubjectId = VehicleCaptureFill.RecorderId,
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
                AggregateId = caseId.ToString("D"),
                EventKind = EventType,
                ActorKind = ActorKind.Automation.ToString(),
                ActorSubjectId = VehicleCaptureFill.RecorderId,
                ActorRolesJson = "[]",
                OccurredAtUtc = now,
                Outcome = "Succeeded",
                CorrelationId = operationKey,
                Reason = reason,
                BeforeJson = null,
                AfterJson = resultJson,
                PolicyVersion = $"{VehicleCaptureFill.PolicyKey}/v{VehicleCaptureFill.PolicyVersion}"
            });

            // Make, model, mileage and accident date print on the report.
            var freshness = facts.Count > 0
                ? CaseReportFreshnessDecision.Stale(CaseReportStaleReasons.AssessmentFactsChanged)
                : CaseReportFreshness.ClassifyAssessment(
                    filled,
                    new Dictionary<string, string?>(StringComparer.Ordinal) { [AssessmentVocabulary.VehicleVin] = vinFilled });
            if (freshness.IsStale)
            {
                await EfCaseReportGenerationStore.MarkWorkStaleAsync(
                    context, caseId, workId, freshness.ReasonCode!, now, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogNotFilled(logger, caseId, exception.GetType().Name);
        }
    }

    /// <summary>
    /// The Case-data fields filled, by name. A field fills where the Case
    /// holds neither an extracted fact nor a confirmed value; a filled mileage
    /// brings its unit, as a looked-up one does.
    /// </summary>
    private static List<string> FillCaseData(
        PegasusDbContext context,
        List<CaseDataFieldEntity> caseDataFields,
        Guid workId,
        VehicleCaptureReading reading)
    {
        var answered = caseDataFields
            .Where(item => item.ValueKind is CaseDataCodes.Fact or CaseDataCodes.Confirmed)
            .ToArray();
        var filled = new List<string>();

        void Fill(string fieldName, string valueType, string value)
        {
            if (!VehicleLookupFillPolicy.Fills(
                    answered.Any(item => item.FieldName == fieldName && item.ValueKind == CaseDataCodes.Fact),
                    answered.Any(item => item.FieldName == fieldName && item.ValueKind == CaseDataCodes.Confirmed)))
            {
                return;
            }

            var field = new CaseDataFieldEntity
            {
                WorkId = workId,
                FieldName = fieldName,
                ValueKind = CaseDataCodes.Fact,
                ValueType = valueType,
                Value = value,
                SourceKind = CaseDataCodes.IntakeEvidence,
                SourceIdentity = reading.IntakeAssetId.ToString("D"),
                SourceLabel = reading.SourceLabel,
                PolicyKey = VehicleCaptureFill.PolicyKey,
                PolicyVersion = VehicleCaptureFill.PolicyVersion
            };
            context.CaseDataFields.Add(field);
            caseDataFields.Add(field);
            filled.Add(fieldName);
        }

        foreach (var (fieldName, value) in VehicleCaptureFill.CaseDataValues(reading))
        {
            Fill(fieldName, fieldName switch
            {
                CaseDataFieldNames.VehicleMileage => CaseDataCodes.Integer,
                CaseDataFieldNames.IncidentDate => CaseDataCodes.Date,
                _ => CaseDataCodes.Text
            }, value);
        }

        if (filled.Contains(CaseDataFieldNames.VehicleMileage))
        {
            Fill(CaseDataFieldNames.VehicleMileageUnit, CaseDataCodes.Text, VehicleMileageUnit.Miles.ToString());
        }

        return filled;
    }

    /// <summary>The VIN written, or null when the work already holds one or the reading names none.</summary>
    private static async Task<string?> FillVinAsync(
        PegasusDbContext context,
        Guid workId,
        string? vin,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var value = GlassVinFillPolicy.Carried(vin);
        if (value is null)
        {
            return null;
        }

        var existing = await context.CaseAssessmentFields.SingleOrDefaultAsync(
            item => item.WorkId == workId && item.FieldPath == AssessmentVocabulary.VehicleVin,
            cancellationToken);
        if (!GlassVinFillPolicy.Fills(existing?.Value))
        {
            return null;
        }

        AssessmentFieldWriter.Write(
            context, workId, existing, AssessmentVocabulary.VehicleVin, value,
            ActorKind.Automation, VehicleCaptureFill.RecorderId, now);
        return value;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The capture document's vehicle details for case {CaseId} were not recorded: {Reason}")]
    private static partial void LogNotFilled(ILogger logger, Guid caseId, string reason);
}
