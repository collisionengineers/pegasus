using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Persists the assessment surface with exactly the guards and evidence of a
/// staff case save: one serializable transaction, operation-key replay via
/// the case workflow event stream, optimistic case version, the server-owned
/// edit lease, and the same three history records (workflow event, permanent
/// action history with before/after values, case history). An Automation
/// save differs from a staff save only in the stored provenance.
/// </summary>
public sealed class EfCaseAssessmentStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider,
    IRepairSpecificationStore repairSpecifications) : ICaseAssessmentStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CaseAssessmentProjection?> GetAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workflow = await context.CaseWorkflows.AsNoTracking()
            .Include(item => item.Case)
            .ThenInclude(item => item.Principal)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        if (workflow is null)
        {
            return null;
        }

        var workId = await CaseWorkScope.CurrentIdAsync(context, caseId, cancellationToken);
        var fields = await context.CaseAssessmentFields.AsNoTracking()
            .Where(item => item.WorkId == workId)
            .OrderBy(item => item.FieldPath)
            .ToArrayAsync(cancellationToken);
        var specificationId = await CurrentSpecificationIdAsync(caseId, cancellationToken);
        var lines = await context.CaseEstimateLines.AsNoTracking()
            .Where(item => item.WorkId == workId
                && item.RepairSpecificationId == specificationId)
            .OrderBy(item => item.Position)
            .ToArrayAsync(cancellationToken);
        var caseDataFields = await context.CaseDataFields.AsNoTracking()
            .Where(item => item.WorkId == workId)
            .ToArrayAsync(cancellationToken);
        var originReceivedAtUtc = await OriginReceivedAtUtcAsync(context, workId, cancellationToken);
        return Map(workflow, fields, lines, caseDataFields, originReceivedAtUtc);
    }

    public async Task<CaseAssessmentProjection> SaveAsync(
        SaveAssessmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request = AssessmentPolicy.ValidateAndNormalize(request);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var requestHash = RequestHash(request);
        var replayed = await CaseOperationReplay.FindAsync(
            context, request.CaseId, request.OperationKey, requestHash, cancellationToken);
        if (replayed)
        {
            return await GetRequiredAsync(context, request.CaseId, cancellationToken);
        }

        var workflow = await context.CaseWorkflows
            .Include(item => item.Case)
            .ThenInclude(item => item.Principal)
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        RequireVersion(workflow, request.ExpectedVersion);
        AssessmentPolicy.RequireOriginalReportScope(
            request.Fields.Keys,
            CaseTypeCodes.Parse(workflow.Case.Type));
        var now = UtcNow();
        RequireLease(workflow, request.Actor, request.EditLeaseToken, now);
        ArchivedCaseGuard.RequireMutable(workflow);
        if (!Enum.TryParse<CaseLifecycleState>(workflow.State, out var state)
            || !AssessmentPolicy.IsWritableState(state))
        {
            throw new InvalidOperationException(
                "The assessment cannot be saved in its current state.");
        }

        // The save writes the current work, resolved after the guards.
        var workId = await CaseWorkScope.CurrentIdAsync(context, request.CaseId, cancellationToken);
        if (request.AiWorkRequestId is { } workRequestId)
        {
            var workRequest = await context.AiWorkRequests.AsNoTracking()
                .SingleOrDefaultAsync(item => item.RequestId == workRequestId, cancellationToken)
                ?? throw new InvalidOperationException(
                    "The referenced Send to AI work request was not found.");
            if (workRequest.CaseId != request.CaseId)
            {
                throw new InvalidOperationException(
                    "The referenced Send to AI work request belongs to another case.");
            }
        }

        var fields = await context.CaseAssessmentFields
            .Where(item => item.WorkId == workId)
            .ToListAsync(cancellationToken);
        var beforeAssessment = fields.ToDictionary(
            item => item.FieldPath, item => (string?)item.Value, StringComparer.Ordinal);
        var mileageField = CaseDataFieldValues.CurrentField(
            await context.CaseDataFields.AsNoTracking()
                .Where(item => item.WorkId == workId)
                .ToArrayAsync(cancellationToken),
            CaseDataFieldNames.VehicleMileage);
        CaseDataSourceKind? mileageProvenance = mileageField is null
            ? null
            : EfCaseDataStore.ParseSourceKind(mileageField.SourceKind);
        var specification = await EfRepairSpecificationStore.DraftQuery(context, workId)
            .SingleOrDefaultAsync(cancellationToken);
        if (specification is null && request.EstimateLines is not null)
        {
            var acceptedExists = await EfRepairSpecificationStore.AcceptedQuery(context, workId)
                .AnyAsync(cancellationToken);
            if (acceptedExists)
            {
                throw new InvalidOperationException(
                    "An accepted repair specification is immutable; start a reasoned correction draft before editing its lines.");
            }
            var version = await EfRepairSpecificationStore.NextVersionAsync(
                context, workId, cancellationToken);
            specification = EfRepairSpecificationStore.NewLegacyDraft(
                workId, version, request.Actor.SubjectId, request.OperationKey, now);
            context.CaseRepairSpecifications.Add(specification);
        }
        var specificationId = specification?.Id;
        var lines = await context.CaseEstimateLines
            .Where(item => item.WorkId == workId
                && item.RepairSpecificationId == specificationId)
            .OrderBy(item => item.Position)
            .ToListAsync(cancellationToken);

        var (fieldsToWrite, merged) = AssessmentWriteSet.Build(request.Fields, fields, request.Actor.Kind);
        AssessmentPolicy.ValidateMergedState(fieldsToWrite, merged);
        var (beforeFields, afterFields) = AssessmentWriteSet.Apply(
            context,
            workId,
            fields,
            fieldsToWrite,
            request.Actor,
            now);

        object? beforeLines = null;
        object? afterLines = null;
        if (request.EstimateLines is { } replacementLines)
        {
            (beforeLines, afterLines) = EstimateLineWriter.Replace(
                context,
                workId,
                specification,
                lines,
                replacementLines,
                request.Actor,
                now);
        }

        var beforeVersion = workflow.Version;
        workflow.Version++;
        ClearLease(workflow);
        CaseMutationHistory.Add(
            context,
            workflow,
            request.Actor,
            request.OperationKey,
            request.Reason,
            "case_assessment_saved",
            requestHash,
            beforeVersion,
            workflow.Version,
            JsonSerializer.Serialize(
                new { Fields = beforeFields, EstimateLines = beforeLines },
                JsonOptions),
            JsonSerializer.Serialize(
                new
                {
                    Fields = afterFields,
                    EstimateLines = afterLines,
                    request.AiWorkRequestId
                },
                JsonOptions),
            $"{AssessmentPolicy.PolicyKey}/v{AssessmentPolicy.PolicyVersion}",
            now);

        var afterAssessment = merged.ToDictionary(
            item => item.Key,
            item => (string?)item.Value,
            StringComparer.Ordinal);
        var freshness = CaseReportFreshness.ClassifyAssessment(
            beforeAssessment,
            afterAssessment,
            CaseVehicleMileageSourcePolicy.Resolve(
                mileageProvenance,
                mileageField is not null,
                beforeAssessment.GetValueOrDefault(AssessmentVocabulary.VehicleMileageSource)),
            CaseVehicleMileageSourcePolicy.Resolve(
                mileageProvenance,
                mileageField is not null,
                afterAssessment.GetValueOrDefault(AssessmentVocabulary.VehicleMileageSource)));
        if (freshness.IsStale)
        {
            await EfCaseReportGenerationStore.MarkStaleAsync(
                context, request.CaseId, freshness.ReasonCode!, now, cancellationToken);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new CaseVersionConflictException(
                request.CaseId,
                request.ExpectedVersion,
                request.ExpectedVersion + 1);
        }

        return await GetRequiredAsync(context, request.CaseId, cancellationToken);
    }

    private async Task<CaseAssessmentProjection> GetRequiredAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var workflow = await context.CaseWorkflows.AsNoTracking()
            .Include(item => item.Case)
            .ThenInclude(item => item.Principal)
            .SingleAsync(item => item.CaseId == caseId, cancellationToken);
        var workId = await CaseWorkScope.CurrentIdAsync(context, caseId, cancellationToken);
        var fields = await context.CaseAssessmentFields.AsNoTracking()
            .Where(item => item.WorkId == workId)
            .OrderBy(item => item.FieldPath)
            .ToArrayAsync(cancellationToken);
        var specificationId = await CurrentSpecificationIdAsync(caseId, cancellationToken);
        var lines = await context.CaseEstimateLines.AsNoTracking()
            .Where(item => item.WorkId == workId
                && item.RepairSpecificationId == specificationId)
            .OrderBy(item => item.Position)
            .ToArrayAsync(cancellationToken);
        var caseDataFields = await context.CaseDataFields.AsNoTracking()
            .Where(item => item.WorkId == workId)
            .ToArrayAsync(cancellationToken);
        var originReceivedAtUtc = await OriginReceivedAtUtcAsync(context, workId, cancellationToken);
        return Map(workflow, fields, lines, caseDataFields, originReceivedAtUtc);
    }

    /// <summary>The origin receipt time <see cref="Map"/> takes, read off the work's snapshot.</summary>
    private static Task<DateTimeOffset?> OriginReceivedAtUtcAsync(
        PegasusDbContext context,
        Guid workId,
        CancellationToken cancellationToken) =>
        context.CaseDataSnapshots.AsNoTracking()
            .Where(item => item.WorkId == workId)
            .Select(item => item.OriginReceivedAtUtc)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// <paramref name="originReceivedAtUtc"/> is the work's case data
    /// snapshot's <c>OriginReceivedAtUtc</c>: when the Case's origin receipt
    /// was received, or null for a manual Case, which has none.
    /// </summary>
    internal static CaseAssessmentProjection Map(
        CaseWorkflowEntity workflow,
        IReadOnlyList<CaseAssessmentFieldEntity> fields,
        IReadOnlyList<CaseEstimateLineEntity> lines,
        IReadOnlyList<CaseDataFieldEntity> caseDataFields,
        DateTimeOffset? originReceivedAtUtc) => new(
        workflow.CaseId,
        workflow.Case.Reference,
        workflow.Version,
        Enum.TryParse<CaseLifecycleState>(workflow.State, out var state)
            ? state
            : throw new InvalidDataException(
                $"Unknown persisted case lifecycle state '{workflow.State}'."),
        workflow.AssignedEngineerId,
        fields.Select(item => new AssessmentFieldValue(
                item.FieldPath,
                item.Value,
                ParseActorKind(item.RecordedByKind),
                item.RecordedBy,
                item.RecordedAtUtc))
            .ToArray(),
        lines.Select(item => new CaseEstimateLineRecord(
                item.Id,
                item.Position,
                item.LineType,
                item.GuideCode,
                item.Description,
                item.WorkUnits,
                item.Price,
                item.Unpriced,
                item.PartNumber,
                item.Betterment,
                item.Status,
                item.EvidenceLabel,
                item.Justification,
                ParseActorKind(item.RecordedByKind),
                item.RecordedBy,
                item.RecordedAtUtc,
                item.PaintWorkUnits,
                item.Quantity))
            .ToArray(),
        MapCaseOwned(workflow.Case, caseDataFields, fields, originReceivedAtUtc));

    /// <summary>
    /// The current specification for report/read purposes is the accepted
    /// one, or the open draft when nothing is accepted yet. <see
    /// cref="IRepairSpecificationStore"/> is the single owner of both
    /// queries; this store only resolves which one wins.
    /// </summary>
    private async Task<Guid?> CurrentSpecificationIdAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var accepted = await repairSpecifications.GetCurrentAcceptedAsync(caseId, cancellationToken);
        if (accepted is not null)
        {
            return accepted.SpecificationId;
        }
        var draft = await repairSpecifications.GetCurrentDraftAsync(caseId, cancellationToken);
        return draft?.SpecificationId;
    }

    private static AssessmentCaseOwnedData MapCaseOwned(
        CaseEntity caseEntity,
        IReadOnlyList<CaseDataFieldEntity> caseDataFields,
        IReadOnlyList<CaseAssessmentFieldEntity> assessmentFields,
        DateTimeOffset? originReceivedAtUtc)
    {
        string? Current(string fieldName) => CaseDataFieldValues.Current(caseDataFields, fieldName);

        DateOnly? CurrentDate(string fieldName) =>
            Current(fieldName) is { } value
                ? DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                : null;

        var inspectionMode = Current(CaseDataFieldNames.InspectionMode) switch
        {
            null => null,
            "physical_address" => nameof(CaseInspectionMode.PhysicalAddress),
            "image_based_assessment" => nameof(CaseInspectionMode.ImageBasedAssessment),
            var unknown => throw new InvalidDataException(
                $"Unknown persisted inspection mode '{unknown}'.")
        };
        // The report's mileage-source sentence is derived from where the case's
        // mileage came from, so it is read off the winning mileage row's own
        // provenance. The recorded assessment value is only the staff pick
        // between the people who could have told them.
        var mileageField = CaseDataFieldValues.CurrentField(
            caseDataFields,
            CaseDataFieldNames.VehicleMileage);
        var mileageSource = CaseVehicleMileageSourcePolicy.Resolve(
            mileageField is null ? null : EfCaseDataStore.ParseSourceKind(mileageField.SourceKind),
            mileageField is not null,
            assessmentFields
                .SingleOrDefault(item => item.FieldPath == AssessmentVocabulary.VehicleMileageSource)
                ?.Value);
        // The Case's Received date (CaseDataPolicy.ReceivedDate); the report
        // prints it as the date instructions were received.
        var receivedDate = CaseDataPolicy.ReceivedDate(originReceivedAtUtc, caseEntity.CreatedAtUtc);
        return new(
            Current(CaseDataFieldNames.VehicleRegistration),
            Current(CaseDataFieldNames.VehicleMake),
            Current(CaseDataFieldNames.VehicleModel),
            Current(CaseDataFieldNames.VehicleYear),
            mileageField is { } mileage
                ? long.Parse(mileage.Value, NumberStyles.None, CultureInfo.InvariantCulture)
                : null,
            Current(CaseDataFieldNames.VehicleMileageUnit),
            mileageSource,
            CurrentDate(CaseDataFieldNames.IncidentDate),
            receivedDate,
            inspectionMode,
            Current(CaseDataFieldNames.InspectionAddress),
            CurrentDate(CaseDataFieldNames.InspectionDate),
            Current(CaseDataFieldNames.ClaimantName),
            Current(CaseDataFieldNames.ClaimNumber));
    }

    private static ActorKind ParseActorKind(string value) =>
        Enum.TryParse<ActorKind>(value, out var kind)
            ? kind
            : throw new InvalidDataException($"Unknown persisted actor kind '{value}'.");

    private static void RequireVersion(CaseWorkflowEntity workflow, long expectedVersion) =>
        CaseMutationGuard.RequireVersion(workflow, expectedVersion);

    private static void RequireLease(
        CaseWorkflowEntity workflow,
        ActionActor actor,
        string token,
        DateTimeOffset now) =>
        CaseMutationGuard.RequireLease(workflow, actor, token, now);

    private static void ClearLease(CaseWorkflowEntity workflow) =>
        CaseMutationGuard.ClearLease(workflow);

    private DateTimeOffset UtcNow()
    {
        var now = timeProvider.GetUtcNow();
        return now.Offset == TimeSpan.Zero ? now : now.ToUniversalTime();
    }

    private static string RequestHash(SaveAssessmentRequest request)
    {
        var material = JsonSerializer.Serialize(new
        {
            Command = "save_assessment",
            request.CaseId,
            request.ExpectedVersion,
            ActorKind = request.Actor.Kind.ToString(),
            request.Actor.SubjectId,
            Roles = request.Actor.Roles
                .OrderBy(role => role)
                .Select(role => role.ToString())
                .ToArray(),
            request.OperationKey,
            request.Reason,
            request.EditLeaseToken,
            Fields = request.Fields.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            request.EstimateLines,
            request.AiWorkRequestId
        }, JsonOptions);
        return CaseOperationReplay.Hash(material);
    }
}

/// <summary>
/// The value a Case currently stands on for a field: a confirmed value, else
/// an extracted fact, else a suggestion. One owner for every reader of the
/// Case's own fields.
/// </summary>
internal static class CaseDataFieldValues
{
    internal static string? Current(IReadOnlyList<CaseDataFieldEntity> fields, string fieldName) =>
        CurrentField(fields, fieldName)?.Value;

    /// <summary>The accepted value: confirmed, else the intake fact; never a suggestion.</summary>
    internal static string? Accepted(IReadOnlyList<CaseDataFieldEntity> fields, string fieldName) =>
        AcceptedField(fields, fieldName)?.Value;

    /// <summary>
    /// The accepted row itself: the staff-confirmed row, else the intake fact,
    /// never a suggestion. The value the Case shows and the one Save posts
    /// back, so the editable-data reader and the field writer both read it
    /// (#837).
    /// </summary>
    internal static CaseDataFieldEntity? AcceptedField(IReadOnlyList<CaseDataFieldEntity> fields, string fieldName)
    {
        var values = fields.Where(item => item.FieldName == fieldName).ToArray();
        return values.SingleOrDefault(item => item.ValueKind == CaseDataCodes.Confirmed)
            ?? values.SingleOrDefault(item => item.ValueKind == CaseDataCodes.Fact);
    }

    /// <summary>
    /// The winning row itself, for the one reader that needs more than the
    /// value: the report's mileage source is derived from the row's provenance.
    /// </summary>
    internal static CaseDataFieldEntity? CurrentField(
        IReadOnlyList<CaseDataFieldEntity> fields,
        string fieldName)
    {
        var values = fields.Where(item => item.FieldName == fieldName).ToArray();
        return values.SingleOrDefault(item => item.ValueKind == CaseDataCodes.Confirmed)
            ?? values.SingleOrDefault(item => item.ValueKind == CaseDataCodes.Fact)
            ?? values.SingleOrDefault(item => item.ValueKind == CaseDataCodes.Suggestion);
    }
}
