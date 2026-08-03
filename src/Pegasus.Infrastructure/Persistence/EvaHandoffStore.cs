using System.Buffers.Binary;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Eva;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EvaHandoffStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    ICaseDataQueries caseDataQueries,
    IVehicleEvidenceQueries vehicleEvidenceQueries,
    IDocumentContentStore contentStore,
    IEvaHandoffProxy proxy,
    EvaMappingAcceptance mappingAcceptance,
    TimeProvider timeProvider) : IEvaHandoffQueries, IGenerateEvaHandoff
{
    private const string GeneratedEvent = "eva_handoff_generated";
    private const string ReusedEvent = "eva_handoff_revision_reused";

    public async Task<EvaHandoffPreparation?> GetPreparationAsync(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        if (caseId == Guid.Empty)
        {
            return null;
        }

        var caseData = await caseDataQueries.GetAsync(caseId, cancellationToken);
        if (caseData is null)
        {
            return null;
        }

        var vehicle = await vehicleEvidenceQueries.GetAsync(caseId, cancellationToken);
        var mapping = MapAcceptedCase(caseData, vehicle);
        var reasons = mapping.BlockingReasons.ToList();

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var caseState = await (
                from workflow in context.CaseWorkflows.AsNoTracking()
                join caseRecord in context.Cases.AsNoTracking()
                    on workflow.CaseId equals caseRecord.Id
                where workflow.CaseId == caseId
                select new
                {
                    workflow.Version,
                    workflow.State,
                    workflow.ArchivedAtUtc,
                    caseRecord.CustodyState,
                    caseRecord.CustodyConfirmedAtUtc
                })
            .SingleOrDefaultAsync(cancellationToken);
        if (caseState is null)
        {
            return null;
        }

        if (caseData.Version != caseState.Version)
        {
            reasons.Add("Accepted case evidence is stale relative to the current case version.");
        }
        if (!IsConfirmedCustody(caseState.CustodyState, caseState.CustodyConfirmedAtUtc))
        {
            reasons.Add("Case custody has not been confirmed.");
        }
        if (caseState.ArchivedAtUtc is not null)
        {
            reasons.Add("Archived cases cannot generate EVA handoffs.");
        }
        if (IsTerminalWorkflow(caseState.State))
        {
            reasons.Add("Terminal cases cannot generate EVA handoffs until they are reasoned and reopened.");
        }

        var images = await (
                from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                    on occurrence.VersionId equals version.Id
                where occurrence.CaseId == caseId
                      && occurrence.SemanticRole == DocumentSemanticRole.Image
                      && version.DocumentId == occurrence.DocumentId
                       && version.IsCurrent
                       && !version.IsLogicallyRemoved
                       && version.CustodyStatus == DocumentCustodyStatus.Confirmed
                       && occurrence.ThirdPartyVehicleConfirmedAtUtc == null
                       && (version.MediaType == "image/jpeg" || version.MediaType == "image/png")
                orderby occurrence.RecordedAtUtc, occurrence.Id
                select new EvaHandoffImageOption(
                    occurrence.Id,
                    occurrence.DocumentId,
                    version.Id,
                    version.Version,
                    version.FileName,
                    version.MediaType,
                    version.ContentLength,
                    version.Sha256,
                    occurrence.Source,
                    occurrence.SourceOccurrenceIdentity))
            .ToArrayAsync(cancellationToken);
        if (images.Length == 0)
        {
            reasons.Add("At least one custody-confirmed current image version is required.");
        }

        var proxyEvidence = await context.EvaFirstHandoffProxies
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        var firstProxyRevisionId = proxyEvidence?.RevisionId;
        var revisions = await context.EvaHandoffRevisions
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderBy(item => item.Revision)
            .Select(item => new EvaHandoffRevisionSummary(
                item.Revision,
                item.FileName,
                item.BundleSha256,
                item.JsonSha256,
                item.GeneratedAtUtc,
                item.GeneratedBy,
                firstProxyRevisionId == item.Id))
            .ToArrayAsync(cancellationToken);

        return new(
            caseId,
            caseState.Version,
            caseData.Identity.Reference,
            images,
            revisions,
            proxyEvidence?.RecordedAtUtc,
            reasons.Distinct(StringComparer.Ordinal).ToArray());
    }

    public async Task<EvaHandoffRevisionArtifact?> GetRevisionAsync(
        Guid caseId,
        int revision,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (caseId == Guid.Empty || revision <= 0)
        {
            return null;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var artifact = await context.EvaHandoffRevisions
            .AsNoTracking()
            .Where(item => item.CaseId == caseId && item.Revision == revision)
            .Select(item => new EvaHandoffRevisionArtifact(
                item.Revision,
                item.FileName,
                item.BundleContent,
                item.BundleSha256))
            .SingleOrDefaultAsync(cancellationToken);
        if (artifact is null)
        {
            return null;
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSafeBundleFileName(artifact.FileName)
            || !HashesMatch(artifact.BundleSha256, Hash(artifact.Content)))
        {
            throw new InvalidDataException("The persisted EVA handoff artifact failed its integrity check.");
        }

        return artifact;
    }

    public async Task<GenerateEvaHandoffResult> ExecuteAsync(
        GenerateEvaHandoffRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        var operationKey = request.OperationKey.Trim();
        var requestHash = HashRequest(request);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var replay = await context.EvaHandoffOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationKey == operationKey, cancellationToken);
        if (replay is not null)
        {
            if (replay.CaseId != request.CaseId
                || !HashesMatch(replay.RequestHash, requestHash))
            {
                return Conflict("The EVA handoff operation key was already used for a different request.");
            }

            var replayRevision = await context.EvaHandoffRevisions
                .AsNoTracking()
                .SingleAsync(item => item.Id == replay.RevisionId, cancellationToken);
            var firstProxyOperation = await context.EvaFirstHandoffProxies
                .AsNoTracking()
                .Where(item => item.CaseId == request.CaseId)
                .Select(item => item.OperationKey)
                .SingleOrDefaultAsync(cancellationToken);
            return Generated(
                replayRevision,
                string.Equals(firstProxyOperation, operationKey, StringComparison.Ordinal));
        }

        var workflow = await context.CaseWorkflows
            .Include(item => item.Case)
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken);
        if (workflow is null)
        {
            return new(
                GenerateEvaHandoffOutcome.NotFound,
                null,
                ["The case was not found."]);
        }
        if (workflow.ArchivedAtUtc is not null)
        {
            return Blocked("Archived cases cannot generate EVA handoffs.");
        }
        if (IsTerminalWorkflow(workflow.State))
        {
            return Blocked(
                "Terminal cases cannot generate EVA handoffs until they are reasoned and reopened.");
        }
        if (workflow.Version != request.ExpectedCaseVersion)
        {
            return Conflict("The case changed after the EVA handoff was loaded. Reload before retrying.");
        }

        var now = timeProvider.GetUtcNow();
        try
        {
            RequireLease(workflow, request.Actor, request.EditLeaseToken, now);
        }
        catch (InvalidOperationException exception)
            when (exception is CaseEditLeaseExpiredException or CaseEditLeaseConflictException)
        {
            return Conflict(exception.Message);
        }

        var caseData = await caseDataQueries.GetAsync(request.CaseId, cancellationToken);
        if (caseData is null)
        {
            return new(
                GenerateEvaHandoffOutcome.NotFound,
                null,
                ["The accepted case data was not found."]);
        }
        if (caseData.Version != workflow.Version)
        {
            return Conflict("The accepted case evidence changed during EVA generation. Reload before retrying.");
        }

        var vehicle = await vehicleEvidenceQueries.GetAsync(request.CaseId, cancellationToken);
        var mapping = MapAcceptedCase(caseData, vehicle);
        var reasons = mapping.BlockingReasons.ToList();
        if (!IsConfirmedCustody(workflow.Case.CustodyState, workflow.Case.CustodyConfirmedAtUtc))
        {
            reasons.Add("Case custody has not been confirmed.");
        }

        var selectedRows = await (
                from occurrence in context.Set<DocumentOccurrenceEntity>()
                join version in context.Set<DocumentVersionEntity>()
                    on occurrence.VersionId equals version.Id
                where occurrence.CaseId == request.CaseId
                      && occurrence.SemanticRole == DocumentSemanticRole.Image
                      && version.DocumentId == occurrence.DocumentId
                       && version.CustodyStatus == DocumentCustodyStatus.Confirmed
                       && version.IsCurrent
                       && !version.IsLogicallyRemoved
                       && occurrence.ThirdPartyVehicleConfirmedAtUtc == null
                       && (version.MediaType == "image/jpeg" || version.MediaType == "image/png")
                orderby occurrence.RecordedAtUtc, occurrence.Id
                select new SelectedDocument(
                    occurrence.Id,
                    occurrence.CaseId,
                    occurrence.DocumentId,
                    occurrence.Source,
                    occurrence.SourceOccurrenceIdentity,
                    occurrence.SemanticRole,
                    version.Id,
                    version.DocumentId,
                    version.Version,
                    version.FileName,
                    version.MediaType,
                    version.ContentLength,
                    version.Sha256,
                    version.CustodyStatus,
                    version.IsCurrent,
                    version.IsLogicallyRemoved))
            .ToArrayAsync(cancellationToken);
        if (selectedRows.Length == 0)
        {
            reasons.Add("At least one custody-confirmed current image version is required.");
        }
        if (reasons.Count != 0 || mapping.Source is null)
        {
            return new(
                GenerateEvaHandoffOutcome.Blocked,
                null,
                reasons.Distinct(StringComparer.Ordinal).ToArray());
        }

        var bundleImages = new List<EvaBundleImage>(selectedRows.Length);
        foreach (var selected in selectedRows)
        {
            if (selected.ContentLength > int.MaxValue)
            {
                return Blocked($"The selected image '{selected.FileName}' is too large for the offline EVA handoff.");
            }

            await using var content = await contentStore.OpenReadAsync(
                request.CaseId,
                selected.VersionId,
                selected.Sha256,
                selected.ContentLength,
                cancellationToken);
            var bytes = GC.AllocateUninitializedArray<byte>(checked((int)selected.ContentLength));
            await content.ReadExactlyAsync(bytes, cancellationToken);
            bundleImages.Add(new(
                selected.OccurrenceId,
                selected.DocumentId,
                selected.VersionId,
                selected.Version,
                selected.FileName,
                selected.MediaType,
                selected.SemanticRole,
                selected.Source,
                selected.SourceOccurrenceIdentity,
                bytes,
                selected.Sha256,
                CustodyConfirmed: true,
                IsCurrent: true));
        }

        var bundle = EvaBundleSchema.CreateOfflineReplay(
            mapping.Source,
            new(bundleImages));
        var existingRevision = await context.EvaHandoffRevisions
            .SingleOrDefaultAsync(
                item => item.CaseId == request.CaseId
                        && item.InputFingerprint == bundle.Sha256,
                cancellationToken);

        var firstSentToEngineerRecorded = false;
        EvaHandoffRevisionEntity revision;
        if (existingRevision is null)
        {
            var nextRevision = await context.EvaHandoffRevisions
                .Where(item => item.CaseId == request.CaseId)
                .Select(item => (int?)item.Revision)
                .MaxAsync(cancellationToken) ?? 0;
            revision = NewRevision(
                request,
                checked(nextRevision + 1),
                bundle,
                now);
            context.EvaHandoffRevisions.Add(revision);

            var hasFirstProxy = await context.EvaFirstHandoffProxies
                .AnyAsync(item => item.CaseId == request.CaseId, cancellationToken);
            if (!hasFirstProxy)
            {
                var receipt = await proxy.RecordFirstGenerationAsync(
                    new(
                        request.CaseId,
                        revision.Revision,
                        bundle.Sha256,
                        request.Actor,
                        operationKey),
                    cancellationToken);
                if (receipt.ClaimsExternalDelivery || receipt.ClaimsEngineerAssignment)
                {
                    throw new InvalidDataException(
                        "The offline EVA proxy must not claim delivery or Engineer assignment.");
                }

                context.EvaFirstHandoffProxies.Add(new()
                {
                    CaseId = request.CaseId,
                    RevisionId = revision.Id,
                    OperationKey = operationKey,
                    AdapterKey = receipt.AdapterKey,
                    AdapterVersion = receipt.AdapterVersion,
                    RecordedAtUtc = receipt.RecordedAtUtc,
                    ActorSubjectId = request.Actor.SubjectId,
                    ClaimsExternalDelivery = false,
                    ClaimsEngineerAssignment = false
                });
                firstSentToEngineerRecorded = true;
            }
        }
        else
        {
            revision = existingRevision;
        }

        context.EvaHandoffOperations.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = request.CaseId,
            RevisionId = revision.Id,
            OperationKey = operationKey,
            RequestHash = requestHash,
            RecordedAtUtc = now,
            ActorSubjectId = request.Actor.SubjectId
        });

        var beforeVersion = workflow.Version;
        workflow.Version = checked(workflow.Version + 1);
        ClearLease(workflow);
        AddAuditEvidence(
            context,
            workflow,
            request,
            operationKey,
            requestHash,
            existingRevision is null ? GeneratedEvent : ReusedEvent,
            beforeVersion,
            workflow.Version,
            revision,
            firstSentToEngineerRecorded,
            now);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("The case changed during EVA generation. Reload before retrying.");
        }

        return Generated(revision, firstSentToEngineerRecorded);
    }

    private EvaMappingResult MapAcceptedCase(
        CaseDataProjection caseData,
        CaseVehicleEvidence? vehicle)
    {
        var caseId = caseData.Identity.CaseId;
        var inspection = ResolveInspection(caseData);
        var acceptedVehicle = vehicle?.CaseId == caseId
            ? vehicle.Confirmed
            : null;
        var evidence = new EvaAcceptedCaseEvidence(
            caseId,
            caseData.Version,
            caseData.AcceptedAtUtc != default,
            caseData.Completeness.Values.InstructionComplete
                && caseData.Completeness.Evaluation.SatisfiesPolicy,
            caseData.Completeness.Values.ImagesComplete
                && caseData.Completeness.Evaluation.SatisfiesPolicy,
            new(
                caseData.Identity.Reference,
                EvaEvidenceStatus.Accepted,
                $"case-identity:{caseId:D}",
                "case-reference/v1"),
            FromCaseField(caseData.Provider.WorkProviderCode, static value => value),
            FromVehicleField(acceptedVehicle?.Registration, static value => value),
            VehicleModel(acceptedVehicle),
            FromCaseField(caseData.Claimant.Name, static value => value),
            FromCaseField(caseData.Accident.IncidentDate, static value => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
            FromCaseField(caseData.Instruction.InstructionDate, static value => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
            FromCaseField(caseData.Inspection.InspectionDate, static value => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
            inspection,
            FromCaseField(caseData.Accident.Circumstances, static value => value),
            FromCaseField(caseData.Instruction.VatStatus, static value => value),
            FromVehicleField(acceptedVehicle?.Mileage, static value => value.ToString(CultureInfo.InvariantCulture)),
            FromVehicleField(acceptedVehicle?.MileageUnit, static value => value switch
            {
                VehicleMileageUnit.Miles => "miles",
                VehicleMileageUnit.Kilometres => "kilometres",
                _ => value.ToString()
            }));
        return CaseEvaMapping.MapForProduction(evidence, mappingAcceptance);
    }

    private static EvaAddressResolution ResolveInspection(CaseDataProjection caseData)
    {
        var mode = Accepted(caseData.Inspection.Mode);
        var address = Accepted(caseData.Inspection.Address);
        if (mode is null || address is null)
        {
            return new(
                mode?.Value == CaseInspectionMode.ImageBasedAssessment
                    ? EvaInspectionMode.ImageBasedAssessment
                    : EvaInspectionMode.PhysicalAddress,
                MissingEvidence);
        }

        var modeEvidence = FromCaseValue(mode, static value => value.ToString());
        var addressEvidence = FromCaseValue(address, static value => value);
        var evidence = addressEvidence with
        {
            Status = modeEvidence.Status == EvaEvidenceStatus.Corrected
                     || addressEvidence.Status == EvaEvidenceStatus.Corrected
                ? EvaEvidenceStatus.Corrected
                : EvaEvidenceStatus.Accepted,
            Source = $"{modeEvidence.Source}|{addressEvidence.Source}",
            SourceVersion = $"{modeEvidence.SourceVersion}|{addressEvidence.SourceVersion}"
        };
        return mode.Value switch
        {
            CaseInspectionMode.ImageBasedAssessment => new(
                EvaInspectionMode.ImageBasedAssessment,
                evidence),
            CaseInspectionMode.PhysicalAddress
                when !string.Equals(
                    address.Value.Trim(),
                    CaseEvaMapping.ImageBasedAssessment,
                    StringComparison.Ordinal) => new(
                        EvaInspectionMode.PhysicalAddress,
                        evidence),
            _ => new(EvaInspectionMode.PhysicalAddress, evidence with
            {
                Status = EvaEvidenceStatus.Suggested
            })
        };
    }

    private static EvaEvidenceValue VehicleModel(ConfirmedVehicleEvidence? vehicle)
    {
        var values = new List<EvaEvidenceValue>(2);
        if (vehicle?.Make is not null)
        {
            values.Add(FromVehicleField(vehicle.Make, static value => value));
        }
        if (vehicle?.Model is not null)
        {
            values.Add(FromVehicleField(vehicle.Model, static value => value));
        }
        if (values.Count == 0)
        {
            return MissingEvidence;
        }

        return values.Aggregate(Combine);
    }

    private static EvaEvidenceValue FromCaseField<T>(
        CaseField<T> field,
        Func<T, string> format)
        where T : notnull =>
        Accepted(field) is { } value
            ? FromCaseValue(value, format)
            : MissingEvidence;

    private static CaseDataValue<T>? Accepted<T>(CaseField<T> field)
        where T : notnull =>
        field.Confirmed is { IsAccepted: true } confirmed
            ? confirmed
            : field.Fact is { IsAccepted: true } fact
                ? fact
                : null;

    private static EvaEvidenceValue FromCaseValue<T>(
        CaseDataValue<T> value,
        Func<T, string> format)
        where T : notnull
    {
        var sourceVersion = !string.IsNullOrWhiteSpace(value.Source.PolicyKey)
                            && value.Source.PolicyVersion > 0
            ? $"{value.Source.PolicyKey.Trim()}/v{value.Source.PolicyVersion}"
            : string.Empty;
        var confirmed = value.ConfirmedByActor is null
            ? string.Empty
            : $";confirmed={value.ConfirmedByActor}@{value.ConfirmedAtUtc:O}";
        return new(
            format(value.Value),
            value.Source.Kind == CaseDataSourceKind.StaffCorrection
                ? EvaEvidenceStatus.Corrected
                : EvaEvidenceStatus.Accepted,
            $"case-data:{value.Source.Kind}:{value.Source.Identity}:{value.Source.Label}{confirmed}",
            sourceVersion);
    }

    private static EvaEvidenceValue FromVehicleField<T>(
        ConfirmedVehicleField<T>? field,
        Func<T, string> format)
        where T : notnull
    {
        if (field is null)
        {
            return MissingEvidence;
        }

        var external = field.ExternalProvenance is null
            ? string.Empty
            : $";provider={field.ExternalProvenance.Provider};response={field.ExternalProvenance.ResponseIdentity};observed={field.ExternalProvenance.RetrievedAtUtc:O}";
        var sourceVersion = !string.IsNullOrWhiteSpace(field.PolicyKey)
                            && field.PolicyVersion > 0
            ? $"{field.PolicyKey.Trim()}/v{field.PolicyVersion}"
            : string.Empty;
        return new(
            format(field.Value),
            field.SourceKind.Equals("staff-correction", StringComparison.Ordinal)
                ? EvaEvidenceStatus.Corrected
                : EvaEvidenceStatus.Accepted,
            $"vehicle:{field.SourceKind}:{field.SourceIdentity}:{field.SourceLabel};confirmed={field.ConfirmedByActor}@{field.ConfirmedAtUtc:O}{external}",
            sourceVersion);
    }

    private static EvaEvidenceValue Combine(EvaEvidenceValue first, EvaEvidenceValue second) => new(
        string.Join(' ', new[] { first.Value, second.Value }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())),
        first.Status == EvaEvidenceStatus.Corrected || second.Status == EvaEvidenceStatus.Corrected
            ? EvaEvidenceStatus.Corrected
            : first.IsAccepted && second.IsAccepted
                ? EvaEvidenceStatus.Accepted
                : EvaEvidenceStatus.Suggested,
        $"{first.Source}|{second.Source}",
        $"{first.SourceVersion}|{second.SourceVersion}");

    private static EvaEvidenceValue MissingEvidence { get; } =
        new(null, EvaEvidenceStatus.Suggested, "missing", "missing");

    private static EvaHandoffRevisionEntity NewRevision(
        GenerateEvaHandoffRequest request,
        int revision,
        EvaBundle bundle,
        DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        CaseId = request.CaseId,
        Revision = revision,
        AcceptedCaseVersion = request.ExpectedCaseVersion,
        SchemaVersion = EvaBundleSchema.SchemaVersion,
        InputFingerprint = bundle.Sha256,
        FileName = bundle.FileName,
        BundleContent = bundle.Content,
        BundleSha256 = bundle.Sha256,
        JsonContent = bundle.JsonContent,
        JsonSha256 = bundle.JsonSha256,
        ProvenanceContent = bundle.ProvenanceContent,
        ProvenanceSha256 = bundle.ProvenanceSha256,
        ManifestContent = bundle.ManifestContent,
        GeneratedAtUtc = now,
        GeneratedBy = request.Actor.SubjectId
    };

    private static GenerateEvaHandoffResult Generated(
        EvaHandoffRevisionEntity revision,
        bool firstSentToEngineerRecorded) => new(
        GenerateEvaHandoffOutcome.Generated,
        Bundle(revision),
        [],
        revision.Revision,
        firstSentToEngineerRecorded);

    private static EvaBundle Bundle(EvaHandoffRevisionEntity revision) => new(
        revision.BundleContent,
        revision.BundleSha256,
        revision.JsonContent,
        revision.JsonSha256,
        revision.ProvenanceContent,
        revision.ProvenanceSha256,
        revision.ManifestContent,
        revision.FileName);


    private static void AddAuditEvidence(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        GenerateEvaHandoffRequest request,
        string operationKey,
        string requestHash,
        string eventType,
        long beforeVersion,
        long afterVersion,
        EvaHandoffRevisionEntity revision,
        bool firstSentToEngineerRecorded,
        DateTimeOffset now)
    {
        var evidenceReason = firstSentToEngineerRecorded
            ? $"Generated immutable EVA handoff revision {revision.Revision} and recorded the once-per-case First sent to Engineer export proxy. No delivery or Engineer assignment was claimed."
            : eventType == ReusedEvent
                ? $"Reused immutable EVA handoff revision {revision.Revision} for unchanged accepted inputs. No delivery or Engineer assignment was claimed."
                : $"Generated immutable EVA handoff revision {revision.Revision}. No delivery or Engineer assignment was claimed.";
        var reason = $"{request.Reason.Trim()} {evidenceReason}";
        var roles = RolesJson(request.Actor);
        var result = JsonSerializer.Serialize(new
        {
            revision.Revision,
            revision.FileName,
            Sha256 = revision.BundleSha256,
            FirstSentToEngineerRecorded = firstSentToEngineerRecorded
        });
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = request.CaseId,
            Workflow = workflow,
            EventType = eventType,
            OperationKey = operationKey,
            RequestHash = requestHash,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = roles,
            Reason = reason,
            OccurredAtUtc = now,
            BeforeVersion = beforeVersion,
            AfterVersion = afterVersion,
            ResultJson = result
        });
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = "case",
            AggregateId = request.CaseId.ToString("D"),
            EventKind = eventType,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = roles,
            OccurredAtUtc = now,
            Outcome = "Succeeded",
            CorrelationId = operationKey,
            Reason = reason,
            BeforeJson = JsonSerializer.Serialize(new { Version = beforeVersion }),
            AfterJson = result,
            PolicyVersion = $"{CaseEvaMapping.MappingKey}/v{CaseEvaMapping.MappingVersion}"
        });
    }

    private static void Validate(GenerateEvaHandoffRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(request));
        }
        if (request.ExpectedCaseVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The expected case version cannot be negative.");
        }
        ArgumentNullException.ThrowIfNull(request.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.EditLeaseToken);
        if (request.OperationKey.Length > 100
            || request.Reason.Length > 300
            || request.EditLeaseToken.Length > 128)
        {
            throw new ArgumentException("The EVA operation key, reason, or edit-lease token is too long.", nameof(request));
        }
    }

    private static void RequireLease(
        CaseWorkflowEntity workflow,
        ActionActor actor,
        string token,
        DateTimeOffset now)
    {
        if (workflow.EditLeaseExpiresAtUtc is null
            || workflow.EditLeaseExpiresAtUtc <= now
            || workflow.EditLeaseTokenHash is null
            || workflow.EditLeaseHolder is null)
        {
            throw new CaseEditLeaseExpiredException(workflow.CaseId);
        }
        if (!string.Equals(workflow.EditLeaseHolder, actor.SubjectId, StringComparison.Ordinal)
            || !HashesMatch(workflow.EditLeaseTokenHash, Hash(token)))
        {
            throw new CaseEditLeaseConflictException(workflow.CaseId);
        }
    }

    private static void ClearLease(CaseWorkflowEntity workflow)
    {
        workflow.EditLeaseToken = null;
        workflow.EditLeaseTokenHash = null;
        workflow.EditLeaseRequestHash = null;
        workflow.EditLeaseHolder = null;
        workflow.EditLeaseOperationKey = null;
        workflow.EditLeaseExpiresAtUtc = null;
    }

    private static string HashRequest(GenerateEvaHandoffRequest request)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "generate-eva-handoff/v1");
        Append(hash, request.CaseId.ToString("D"));
        Append(hash, request.ExpectedCaseVersion.ToString(CultureInfo.InvariantCulture));
        Append(hash, request.Actor.Kind.ToString());
        Append(hash, request.Actor.SubjectId);
        foreach (var role in request.Actor.Roles.OrderBy(role => role))
        {
            Append(hash, role.ToString());
        }
        Append(hash, request.Reason.Trim());
        Append(hash, Hash(request.EditLeaseToken));
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void Append(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string Hash(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();


    private static bool HashesMatch(string? stored, string expected)
    {
        if (stored is null || stored.Length != expected.Length)
        {
            return false;
        }
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(stored),
                Convert.FromHexString(expected));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsSafeBundleFileName(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && fileName.Length <= 260
        && fileName.Equals(fileName.Trim(), StringComparison.Ordinal)
        && fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
        && !fileName.Contains('/')
        && !fileName.Contains('\\')
        && !fileName.Any(char.IsControl)
        && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static bool IsTerminalWorkflow(string state) =>
        Enum.TryParse<CaseLifecycleState>(state, out var parsed)
        && parsed is CaseLifecycleState.PostReportComplete
            or CaseLifecycleState.ProviderCancelled
            or CaseLifecycleState.CollisionEngineersRejected
            or CaseLifecycleState.CreatedInError;

    private static bool IsConfirmedCustody(string state, DateTimeOffset? confirmedAtUtc) =>
        state.Equals("confirmed", StringComparison.OrdinalIgnoreCase)
        && confirmedAtUtc is not null;

    private static bool IsSupportedImage(string mediaType) =>
        mediaType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
        || mediaType.Equals("image/png", StringComparison.OrdinalIgnoreCase);

    private static string RolesJson(ActionActor actor) =>
        JsonSerializer.Serialize(actor.Roles.OrderBy(role => role).Select(role => role.ToString()));

    private static GenerateEvaHandoffResult Blocked(string reason) =>
        new(GenerateEvaHandoffOutcome.Blocked, null, [reason]);

    private static GenerateEvaHandoffResult Conflict(string reason) =>
        new(GenerateEvaHandoffOutcome.Conflict, null, [reason]);

    private sealed record SelectedDocument(
        Guid OccurrenceId,
        Guid CaseId,
        Guid DocumentId,
        DocumentSource Source,
        string SourceOccurrenceIdentity,
        DocumentSemanticRole SemanticRole,
        Guid VersionId,
        Guid VersionDocumentId,
        int Version,
        string FileName,
        string MediaType,
        long ContentLength,
        string Sha256,
        DocumentCustodyStatus CustodyStatus,
        bool IsCurrent,
        bool IsLogicallyRemoved);
}
