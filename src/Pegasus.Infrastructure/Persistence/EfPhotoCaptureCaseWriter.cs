using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Reports;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Custody;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Case-side writes of a settled photo link (FRD-28): the images-complete
/// transition, as system work under the Case's workflow lock, once per
/// capture (R792) and keyed by the settle's operation key; and the fill of
/// the Case's empty fields from the
/// sender's details, as <see cref="EfVehicleCaptureFill"/> fills from a
/// capture document. A closed, archived or Triage Case records nothing.
/// </summary>
public sealed class EfPhotoCaptureCaseWriter(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider,
    IEnumerable<IPrincipalCaseMatchPolicy>? caseMatchPolicies = null) : IPhotoCaptureCaseWriter
{
    public const string ImagesCompletedEventType = "case_images_completed_from_photo_link";
    public const string FilledEventType = "photo_link_filled";

    /// <summary>The Automation actor the fill is recorded as.</summary>
    public const string FillRecorderId = "photo-link";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task CompleteImagesAsync(
        Guid caseId,
        Guid captureId,
        string operationKey,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        await EfCaseWorkflowStore.AcquireWorkflowMutationLockAsync(context, caseId, cancellationToken);
        // Once per capture (R792): the capture's marker is read and written
        // under the Case's workflow lock, in the transaction that completes the
        // images, so a later batch never completes them again, even after
        // staff marked them incomplete.
        var capture = await context.PhotoCaptures
            .SingleOrDefaultAsync(item => item.Id == captureId, cancellationToken)
            ?? throw new KeyNotFoundException("The photo link was not found.");
        var now = timeProvider.GetUtcNow();
        var workflow = await context.CaseWorkflows
            .Include(item => item.Case)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        // A Triage Case has no workflow and records no image completeness,
        // and a closed or archived Case takes no system completion; the
        // capture's marker is recorded all the same (R792, R841).
        var completes = PhotoCaptureImages.Completes(
            capture.ImagesCompletedAtUtc,
            workflow?.Case.ImagesComplete ?? false,
            takesSystemCompletion: workflow is { ArchivedAtUtc: null }
                && Enum.TryParse<CaseLifecycleState>(workflow.State, ignoreCase: false, out var state)
                && !CaseLifecycleRules.IsTerminal(state));
        if (capture.ImagesCompletedAtUtc is null)
        {
            capture.ImagesCompletedAtUtc = now;
            capture.Version++;
        }

        if (!completes
            || workflow is null
            || await context.CaseWorkflowEvents.AsNoTracking()
                .AnyAsync(item => item.CaseId == caseId && item.OperationKey == operationKey, cancellationToken))
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var (before, after, evaluation) = await EfCaseArtifactCustody.CompleteCaseImagesAsync(
            context, workflow, now, cancellationToken);
        var beforeVersion = workflow.Version;
        CaseMutationGuard.Advance(workflow);
        CaseMutationHistory.Add(
            context,
            workflow,
            ActionActor.SystemWorker(PhotoCaptureIntakeRules.SettleActorId),
            operationKey,
            reason,
            ImagesCompletedEventType,
            CaseOperationReplay.Hash(operationKey),
            beforeVersion,
            workflow.Version,
            JsonSerializer.Serialize(before),
            JsonSerializer.Serialize(after),
            $"{evaluation.PolicyKey}/v{evaluation.PolicyVersion}",
            now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Fills the Case's empty fields, retried on a lost race. A closed,
    /// archived or Triage Case takes nothing; any other failure propagates, so
    /// the settle leaves the batch open and the next run fills it, rather
    /// than closing the batch with the sender's details unwritten.
    /// </summary>
    public Task<IReadOnlyList<string>> FillEmptyFieldsAsync(
        Guid caseId,
        IReadOnlyDictionary<string, string> values,
        Guid captureId,
        string sourceLabel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLabel);
        if (values.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        return StoreConcurrencyRetry.ExecuteAsync(
            token => FillOnceAsync(caseId, values, captureId, sourceLabel, token),
            cancellationToken);
    }

    private async Task<IReadOnlyList<string>> FillOnceAsync(
        Guid caseId,
        IReadOnlyDictionary<string, string> values,
        Guid captureId,
        string sourceLabel,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        await EfCaseWorkflowStore.AcquireWorkflowMutationLockAsync(context, caseId, cancellationToken);
        var workflow = await context.CaseWorkflows
            .Include(item => item.Case)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        if (workflow is null
            || workflow.ArchivedAtUtc is not null
            || !Enum.TryParse<CaseLifecycleState>(workflow.State, ignoreCase: false, out var state)
            || CaseLifecycleRules.IsTerminal(state))
        {
            return [];
        }

        var now = timeProvider.GetUtcNow();
        var workId = await CaseWorkScope.CurrentIdAsync(context, caseId, cancellationToken);
        var caseDataFields = await context.CaseDataFields
            .Where(item => item.WorkId == workId)
            .ToListAsync(cancellationToken);
        var (filled, replaced) = FillCaseData(context, caseDataFields, workId, values, captureId, sourceLabel);
        if (filled.Count == 0)
        {
            return [];
        }

        var beforeVersion = workflow.Version;
        CaseMutationGuard.Advance(workflow);
        var resultJson = JsonSerializer.Serialize(new { Facts = filled }, JsonOptions);
        // A replaced DVSA estimate keeps its value in the history (R843);
        // a fill into empty fields replaced nothing.
        var beforeJson = replaced.Count == 0 ? null : JsonSerializer.Serialize(new { Facts = replaced }, JsonOptions);
        const string reason = "The photo link's details filled the Case's empty fields.";
        var operationKey = $"photo-link-fill:{captureId:N}:{caseId:N}";
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Workflow = workflow,
            EventType = FilledEventType,
            OperationKey = $"{operationKey}:{beforeVersion}",
            RequestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(resultJson))),
            ActorKind = ActorKind.Automation.ToString(),
            ActorSubjectId = FillRecorderId,
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
            EventKind = FilledEventType,
            ActorKind = ActorKind.Automation.ToString(),
            ActorSubjectId = FillRecorderId,
            ActorRolesJson = "[]",
            OccurredAtUtc = now,
            Outcome = "Succeeded",
            CorrelationId = operationKey,
            Reason = reason,
            BeforeJson = beforeJson,
            AfterJson = resultJson,
            PolicyVersion = $"{PhotoCaptureCaseFields.PolicyKey}/v{PhotoCaptureCaseFields.PolicyVersion}"
        });

        // The claimant's name and the incident date are case-match keys,
        // so the index follows the filled facts as a staff edit's does.
        if (caseMatchPolicies is not null
            && (filled.Contains(CaseDataFieldNames.ClaimantName) || filled.Contains(CaseDataFieldNames.IncidentDate)))
        {
            CaseMatchIndexProjector.Apply(
                context,
                await context.CaseMatchIndex.SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken),
                CaseMatchIndexProjector.Project(workflow.Case, caseDataFields, caseMatchPolicies, now));
        }

        // Every field the fill writes prints on the report.
        await EfCaseReportGenerationStore.MarkWorkStaleAsync(
            context, caseId, workId, CaseReportStaleReasons.AssessmentFactsChanged, now, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return filled;
    }

    public async Task<Guid?> AssignedEngineerAsync(Guid caseId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.CaseWorkflows.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .Select(item => item.AssignedEngineerId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>A fact a fill replaced, as the Case held it before.</summary>
    private sealed record ReplacedFact(string FieldName, string Value, string SourceKind, string PolicyKey);

    /// <summary>
    /// The fields filled, by name, and the facts among them that were
    /// replaced rather than filled into an empty field. A field fills where the Case holds neither
    /// an extracted fact nor a confirmed value; a filled mileage brings its
    /// unit, as a looked-up one does. A typed mileage replaces the DVSA
    /// estimate, and only that, in its own fact row and its unit's
    /// (<see cref="PhotoCaptureCaseFields.Replaces"/>; R843).
    /// </summary>
    private static (List<string> Filled, List<ReplacedFact> Replaced) FillCaseData(
        PegasusDbContext context,
        List<CaseDataFieldEntity> caseDataFields,
        Guid workId,
        IReadOnlyDictionary<string, string> values,
        Guid captureId,
        string sourceLabel)
    {
        var filled = new List<string>();
        var replaced = new List<ReplacedFact>();

        void Fill(string fieldName, string valueType, string value)
        {
            var fact = caseDataFields.Find(item => item.FieldName == fieldName && item.ValueKind == CaseDataCodes.Fact);
            if (string.IsNullOrWhiteSpace(value)
                || !PhotoCaptureCaseFields.Replaces(
                    fieldName,
                    fact is null ? null : EfCaseDataStore.Source(fact),
                    caseDataFields.Exists(item => item.FieldName == fieldName && item.ValueKind == CaseDataCodes.Confirmed)))
            {
                return;
            }

            if (fact is not null)
            {
                // The DVSA estimate's own row takes the typed reading, so the
                // Case holds one mileage fact (R843).
                replaced.Add(new ReplacedFact(fact.FieldName, fact.Value, fact.SourceKind, fact.PolicyKey));
                fact.ValueType = valueType;
                fact.Value = value.Trim();
                fact.SourceKind = CaseDataCodes.PhotoLink;
                fact.SourceIdentity = captureId.ToString("D");
                fact.SourceLabel = sourceLabel;
                fact.PolicyKey = PhotoCaptureCaseFields.PolicyKey;
                fact.PolicyVersion = PhotoCaptureCaseFields.PolicyVersion;
                filled.Add(fieldName);
                return;
            }

            var field = new CaseDataFieldEntity
            {
                WorkId = workId,
                FieldName = fieldName,
                ValueKind = CaseDataCodes.Fact,
                ValueType = valueType,
                Value = value.Trim(),
                SourceKind = CaseDataCodes.PhotoLink,
                SourceIdentity = captureId.ToString("D"),
                SourceLabel = sourceLabel,
                PolicyKey = PhotoCaptureCaseFields.PolicyKey,
                PolicyVersion = PhotoCaptureCaseFields.PolicyVersion
            };
            context.CaseDataFields.Add(field);
            caseDataFields.Add(field);
            filled.Add(fieldName);
        }

        foreach (var (fieldName, value) in values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
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

        return (filled, replaced);
    }
}
