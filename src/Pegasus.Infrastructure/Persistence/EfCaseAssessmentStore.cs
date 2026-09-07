using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Persists the assessment surface with exactly the guards and evidence of a
/// staff case save: one serializable transaction, operation-key replay via
/// the case workflow event stream, optimistic case version, the server-owned
/// edit lease, and the same three history records (workflow event, permanent
/// action history with before/after values, case history). An Automation
/// save differs from a staff save only in the stored provenance: its values
/// carry the unconfirmed mark until staff review.
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

        var fields = await context.CaseAssessmentFields.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderBy(item => item.FieldPath)
            .ToArrayAsync(cancellationToken);
        var specificationId = await CurrentSpecificationIdAsync(caseId, cancellationToken);
        var lines = await context.CaseEstimateLines.AsNoTracking()
            .Where(item => item.CaseId == caseId
                && item.RepairSpecificationId == specificationId)
            .OrderBy(item => item.Position)
            .ToArrayAsync(cancellationToken);
        var caseDataFields = await context.CaseDataFields.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .ToArrayAsync(cancellationToken);
        return Map(workflow, fields, lines, caseDataFields);
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
        var now = UtcNow();
        RequireLease(workflow, request.Actor, request.EditLeaseToken, now);
        ArchivedCaseGuard.RequireMutable(workflow);
        if (!Enum.TryParse<CaseLifecycleState>(workflow.State, out var state)
            || !AssessmentPolicy.IsWritableState(state))
        {
            throw new InvalidOperationException(
                "The assessment can be saved only on a Not ready, Review, or Report preparation case.");
        }

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
            .Where(item => item.CaseId == request.CaseId)
            .ToListAsync(cancellationToken);
        var specification = await EfRepairSpecificationStore.DraftQuery(context, request.CaseId)
            .SingleOrDefaultAsync(cancellationToken);
        if (specification is null && request.EstimateLines is not null)
        {
            var acceptedExists = await EfRepairSpecificationStore.AcceptedQuery(context, request.CaseId)
                .AnyAsync(cancellationToken);
            if (acceptedExists)
            {
                throw new InvalidOperationException(
                    "An accepted repair specification is immutable; start a reasoned correction draft before editing its lines.");
            }
            var version = await EfRepairSpecificationStore.NextVersionAsync(
                context, request.CaseId, cancellationToken);
            specification = EfRepairSpecificationStore.NewLegacyDraft(
                request.CaseId, workflow.Case, version, request.Actor.SubjectId, request.OperationKey, now);
            context.CaseRepairSpecifications.Add(specification);
        }
        var specificationId = specification?.Id;
        var lines = await context.CaseEstimateLines
            .Where(item => item.CaseId == request.CaseId
                && item.RepairSpecificationId == specificationId)
            .OrderBy(item => item.Position)
            .ToListAsync(cancellationToken);

        var (fieldsToWrite, merged) = AssessmentWriteSet.Build(request.Fields, fields);
        AssessmentPolicy.ValidateMergedState(request.Fields, merged);
        var confirmedBy = request.Actor.Kind == ActorKind.Staff ? request.Actor.SubjectId : null;
        var (beforeFields, afterFields) = AssessmentWriteSet.Apply(
            context,
            workflow.Case,
            request.CaseId,
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
                request.CaseId,
                workflow.Case,
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
        var fields = await context.CaseAssessmentFields.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderBy(item => item.FieldPath)
            .ToArrayAsync(cancellationToken);
        var specificationId = await CurrentSpecificationIdAsync(caseId, cancellationToken);
        var lines = await context.CaseEstimateLines.AsNoTracking()
            .Where(item => item.CaseId == caseId
                && item.RepairSpecificationId == specificationId)
            .OrderBy(item => item.Position)
            .ToArrayAsync(cancellationToken);
        var caseDataFields = await context.CaseDataFields.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .ToArrayAsync(cancellationToken);
        return Map(workflow, fields, lines, caseDataFields);
    }

    internal static CaseAssessmentProjection Map(
        CaseWorkflowEntity workflow,
        IReadOnlyList<CaseAssessmentFieldEntity> fields,
        IReadOnlyList<CaseEstimateLineEntity> lines,
        IReadOnlyList<CaseDataFieldEntity> caseDataFields) => new(
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
                item.RecordedAtUtc,
                item.ConfirmedBy,
                item.ConfirmedAtUtc))
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
                item.ConfirmedBy,
                item.ConfirmedAtUtc,
                item.PaintWorkUnits,
                item.Quantity))
            .ToArray(),
        MapCaseOwned(caseDataFields));

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
        IReadOnlyList<CaseDataFieldEntity> caseDataFields)
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
        return new(
            Current(CaseDataFieldNames.VehicleRegistration),
            Current(CaseDataFieldNames.VehicleMake),
            Current(CaseDataFieldNames.VehicleModel),
            Current(CaseDataFieldNames.VehicleMileage) is { } mileage
                ? long.Parse(mileage, NumberStyles.None, CultureInfo.InvariantCulture)
                : null,
            Current(CaseDataFieldNames.VehicleMileageUnit),
            CurrentDate(CaseDataFieldNames.IncidentDate),
            CurrentDate(CaseDataFieldNames.InstructionDate),
            inspectionMode,
            Current(CaseDataFieldNames.InspectionAddress));
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
    internal static string? Current(IReadOnlyList<CaseDataFieldEntity> fields, string fieldName)
    {
        var values = fields.Where(item => item.FieldName == fieldName).ToArray();
        var current = values.SingleOrDefault(item => item.ValueKind == CaseDataCodes.Confirmed)
            ?? values.SingleOrDefault(item => item.ValueKind == CaseDataCodes.Fact)
            ?? values.SingleOrDefault(item => item.ValueKind == CaseDataCodes.Suggestion);
        return current?.Value;
    }
}
