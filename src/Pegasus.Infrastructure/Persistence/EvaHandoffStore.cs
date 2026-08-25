using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The one act that produces the EVA package (ENG-016). It reads a case,
/// maps the thirteen fields, loads every eligible retained photograph, writes
/// the archive, records each successful export in action history, creates the
/// <c>First sent to Engineer</c> proxy on the first success, and updates its
/// latest exported workflow version on every success.
///
/// It used to be two acts. The gated hand-off that recorded frozen revisions,
/// moved the case version and took an edit lease is gone, together with its
/// three tables; what it contributed, the once-per-case proxy, moved here.
///
/// The type name still says "handoff" because renaming it would widen the
/// conflict surface across a stack of dependent branches for no behavioural
/// gain; the rename is recorded as outstanding rather than done quietly.
/// </summary>
public sealed class EvaHandoffStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    ICaseDataQueries caseDataQueries,
    IVehicleEvidenceQueries vehicleEvidenceQueries,
    IDocumentContentStore contentStore,
    IEvaHandoffProxy proxy,
    TimeProvider timeProvider) : IExportCaseBundle
{
    /// <summary>
    /// CASE-019 / ENG-016: the operator's export of a case as the EVA-format
    /// archive, and since ENG-016 the only way to produce one.
    ///
    /// It takes no edit lease and does not move the case version: an export is
    /// not a case-data mutation. Its operation key makes the action-history
    /// write replay-safe. The first export also writes the once-per-case
    /// <c>First sent to Engineer</c> proxy row; every export updates its latest
    /// exported workflow version.
    ///
    /// Order matters. The proxy is recorded only after the archive has been
    /// built, so "first success only" is literal: an export that fails records
    /// nothing. "Once per case" is enforced by <c>EvaFirstHandoffProxies</c>'
    /// primary key on the case; the operation key separately owns exact replay
    /// of the per-export action-history record.
    /// </summary>
    public async Task<ExportCaseBundleResult?> ExecuteAsync(
        ExportCaseBundleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CaseId == Guid.Empty)
        {
            return null;
        }
        if (!Guid.TryParseExact(request.OperationKey, "N", out _))
        {
            throw new ArgumentException("The operation key is invalid.", nameof(request));
        }

        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        var caseData = await caseDataQueries.GetAsync(request.CaseId, cancellationToken);
        if (caseData is null)
        {
            return null;
        }
        if (caseData.State != CaseLifecycleState.Review)
        {
            throw new CaseNotInReviewException(request.CaseId);
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var vehicle = await vehicleEvidenceQueries.GetAsync(request.CaseId, cancellationToken);
        var export = CaseEvaMapping.MapForOperatorExport(
            BuildEvidence(caseData, vehicle),
            DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));

        var images = await LoadEligibleImagesAsync(
            context,
            request.CaseId,
            caseData.Identity.Reference,
            cancellationToken);
        if (images.Count == 0)
        {
            return new(
                null,
                export.UnrecordedFields,
                [EvaHandoffPolicy.NoRetainedImagesReason]);
        }

        var bundle = EvaBundleSchema.CreateOfflineReplay(
            export.Source,
            new(images),
            caseData.Identity.Reference);
        await RecordExportAsync(
            context,
            request,
            caseData,
            export.Source,
            images,
            bundle,
            cancellationToken);

        return new(bundle, export.UnrecordedFields, []);
    }

    /// <summary>
    /// The once-per-case <c>First sent to Engineer</c> proxy. It is a proxy and
    /// nothing more: it proves that Pegasus produced the package, never that
    /// EVA received it or that a named Engineer was assigned. A receipt that
    /// claims either is refused here, and the two
    /// <c>CK_EvaFirstHandoffProxies_*</c> check constraints refuse it again in
    /// the database.
    ///
    /// The short database section takes the existing case-workflow row lock.
    /// Same-case exports therefore observe replay and the first-send proxy in
    /// commit order, while package creation and image reads stay outside the
    /// transaction. The proxy primary key remains the final once-per-case
    /// database constraint.
    /// </summary>
    private async Task RecordExportAsync(
        PegasusDbContext context,
        ExportCaseBundleRequest request,
        CaseDataProjection caseData,
        EvaBundleSource source,
        IReadOnlyList<EvaBundleImage> images,
        EvaBundle bundle,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var lockedState = await ReadLockedExportStateAsync(
            context,
            request.CaseId,
            cancellationToken);
        if (lockedState is null || !string.Equals(
                lockedState.State,
                CaseLifecycleState.Review.ToString(),
                StringComparison.Ordinal))
        {
            throw new CaseNotInReviewException(request.CaseId);
        }
        if (lockedState.WorkflowVersion != caseData.Version)
        {
            throw new CaseVersionConflictException(
                request.CaseId,
                caseData.Version,
                lockedState.WorkflowVersion);
        }

        var aggregateId = request.CaseId.ToString("D");
        const string eventKind = "eva_bundle_exported";
        var afterJson = DocumentActionHistory.Serialize(new
        {
            CaseVersion = caseData.Version,
            Mapping = new
            {
                source.MappingKey,
                source.MappingVersion
            },
            source.Fields,
            source.Provenance,
            BundleSha256 = bundle.Sha256,
            JsonSha256 = bundle.JsonSha256,
            Images = images.Select(image => new
            {
                image.OccurrenceId,
                image.DocumentId,
                image.VersionId,
                image.Version,
                image.Sha256,
                image.SourceOccurrenceIdentity
            })
        });
        var existingHistory = await context.ActionHistory
            .SingleOrDefaultAsync(
                item => item.AggregateType == "Case"
                    && item.AggregateId == aggregateId
                    && item.EventKind == eventKind
                    && item.CorrelationId == request.OperationKey,
                cancellationToken);
        if (existingHistory is not null)
        {
            DocumentActionHistory.RequireExactReplay(
                existingHistory,
                "Case",
                aggregateId,
                eventKind,
                request.Actor,
                reason: null,
                afterJson);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var handoff = await context.EvaFirstHandoffProxies
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken);
        if (handoff is null)
        {
            var receipt = await proxy.RecordFirstGenerationAsync(
                new(request.CaseId, bundle.Sha256, request.Actor),
                cancellationToken);
            if (receipt.ClaimsExternalDelivery || receipt.ClaimsEngineerAssignment)
            {
                throw new InvalidDataException(
                    "The offline EVA proxy must not claim delivery or Engineer assignment.");
            }

            handoff = new()
            {
                CaseId = request.CaseId,
                AdapterKey = receipt.AdapterKey,
                AdapterVersion = receipt.AdapterVersion,
                RecordedAtUtc = receipt.RecordedAtUtc,
                LatestExportedWorkflowVersion = caseData.Version,
                ActorSubjectId = request.Actor.SubjectId,
                ClaimsExternalDelivery = false,
                ClaimsEngineerAssignment = false
            };
            context.EvaFirstHandoffProxies.Add(handoff);
        }
        else
        {
            handoff.LatestExportedWorkflowVersion = caseData.Version;
        }

        var history = DocumentActionHistory.Succeeded(
            "Case",
            aggregateId,
            eventKind,
            request.Actor,
            timeProvider.GetUtcNow(),
            request.OperationKey,
            afterJson: afterJson);
        history.PolicyVersion = $"{source.MappingKey}/v{source.MappingVersion}";
        context.ActionHistory.Add(history);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static Task<LockedExportState?> ReadLockedExportStateAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var workflows = context.Database.IsSqlServer()
            ? context.CaseWorkflows.FromSqlInterpolated($"""
                SELECT *
                FROM [CaseWorkflows] WITH (UPDLOCK, HOLDLOCK)
                WHERE [CaseId] = {caseId}
                """)
            : context.CaseWorkflows.Where(item => item.CaseId == caseId);
        return workflows
            .AsNoTracking()
            .Select(item => new LockedExportState(item.State, item.Version))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private sealed record LockedExportState(string State, long WorkflowVersion);

    /// <summary>
    /// Every eligible retained photograph of a case, with its content, chosen
    /// by <see cref="EvaHandoffPolicy.SelectEligibleImages"/>.
    /// </summary>
    private async Task<List<EvaBundleImage>> LoadEligibleImagesAsync(
        PegasusDbContext context,
        Guid caseId,
        string caseReference,
        CancellationToken cancellationToken)
    {
        var candidateRows = await (
                from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                    on occurrence.VersionId equals version.Id
                join caseEntity in context.Cases.AsNoTracking()
                    on occurrence.CaseId equals caseEntity.Id
                where occurrence.CaseId == caseId
                      && version.DocumentId == occurrence.DocumentId
                orderby occurrence.Ordinal
                select new SelectedDocument(
                    occurrence.Id,
                    occurrence.Ordinal,
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
                    version.IsLogicallyRemoved,
                    occurrence.ThirdPartyVehicleConfirmedAtUtc != null,
                    caseEntity.CustodyRootRemoteId))
            .ToArrayAsync(cancellationToken);
        var eligibleVersionIds = EvaHandoffPolicy.SelectEligibleImages(candidateRows.Select(
                selected => new EvaHandoffImageCandidate(
                    selected.OccurrenceId,
                    selected.DocumentId,
                    selected.VersionId,
                    selected.Version,
                    selected.FileName,
                    selected.MediaType,
                    selected.ContentLength,
                    selected.Sha256,
                    selected.SemanticRole,
                    selected.Source,
                    selected.SourceOccurrenceIdentity,
                    selected.CustodyStatus == DocumentCustodyStatus.Confirmed,
                    selected.IsCurrent,
                    selected.IsLogicallyRemoved,
                    selected.IsThirdPartyVehicle,
                    selected.Ordinal)))
            .Select(candidate => candidate.VersionId)
            .ToHashSet();

        var selectedImages = candidateRows.Where(
            selected => eligibleVersionIds.Contains(selected.VersionId)
                        && selected.ContentLength <= int.MaxValue)
            .ToArray();
        var caseRootRemoteId = selectedImages.Length == 0
            ? null
            : selectedImages[0].CaseRootRemoteId;
        var reads = selectedImages.Select(selected => new ManagedDocumentContentRead(
                new ManagedDocumentContentAddress(
                    caseId,
                    caseReference,
                    caseRootRemoteId,
                    selected.OccurrenceId,
                    selected.Ordinal,
                    selected.DocumentId,
                    selected.VersionId,
                    selected.Version,
                    selected.SemanticRole,
                    selected.FileName,
                    selected.MediaType),
                selected.Sha256,
                selected.ContentLength))
            .ToArray();
        var contents = await contentStore.ReadVersionsAsync(reads, cancellationToken);

        var images = new List<EvaBundleImage>(selectedImages.Length);
        for (var index = 0; index < selectedImages.Length; index++)
        {
            var selected = selectedImages[index];
            images.Add(new(
                selected.OccurrenceId,
                selected.DocumentId,
                selected.VersionId,
                selected.Version,
                selected.FileName,
                selected.MediaType,
                selected.SemanticRole,
                selected.Source,
                selected.SourceOccurrenceIdentity,
                contents[index].ToArray(),
                selected.Sha256,
                CustodyConfirmed: true,
                IsCurrent: true,
                selected.Ordinal));
        }

        return images;
    }

    /// <summary>
    /// The thirteen EVA fields read off one case, written once.
    ///
    /// This used to take an <c>includeSuggestions</c> flag, which its own
    /// comment called "the whole difference between the hand-off and an
    /// operator export". With one act left there is one answer: a suggested
    /// value counts, and travels with its real suggested status — which is how
    /// the lookup-derived mileage ENG-013 writes reaches the archive.
    /// </summary>
    private static EvaAcceptedCaseEvidence BuildEvidence(
        CaseDataProjection caseData,
        CaseVehicleEvidence? vehicle)
    {
        var caseId = caseData.Identity.CaseId;
        var inspection = ResolveInspection(caseData);
        var acceptedVehicle = vehicle?.CaseId == caseId
            ? vehicle.Confirmed
            : null;
        return new EvaAcceptedCaseEvidence(
            caseId,
            caseData.Version,
            caseData.AcceptedAtUtc != default,
            caseData.Completeness.Values.InstructionComplete
                && caseData.Completeness.Evaluation.SatisfiesPolicy,
            caseData.Completeness.Values.ImagesComplete
                && caseData.Completeness.Evaluation.SatisfiesPolicy,
            FromCaseField(caseData.Claim.Number, static value => value),
            FromCaseField(caseData.Provider.WorkProviderCode, static value => value),
            Fallback(
                FromVehicleField(acceptedVehicle?.Registration, static value => value),
                caseData.Vehicle.Registration,
                static value => value),
            VehicleModel(acceptedVehicle, caseData),
            FromCaseField(caseData.Claimant.Name, static value => value),
            FromCaseField(caseData.Accident.IncidentDate, static value => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
            FromCaseField(caseData.Instruction.InstructionDate, static value => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
            FromCaseField(caseData.Inspection.InspectionDate, static value => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
            inspection,
            FromCaseField(caseData.Accident.Circumstances, static value => value),
            FromCaseField(caseData.Instruction.VatStatus, static value => value),
            Fallback(
                FromVehicleField(acceptedVehicle?.Mileage, static value => value.ToString(CultureInfo.InvariantCulture)),
                caseData.Vehicle.Mileage,
                static value => value.ToString(CultureInfo.InvariantCulture)),
            Fallback(
                FromVehicleField(acceptedVehicle?.MileageUnit, MileageUnit),
                caseData.Vehicle.MileageUnit,
                MileageUnit));
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
            Status = EvaEvidenceStatus.Accepted,
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

    /// <summary>
    /// Make and model as one value, from whichever source the case has.
    ///
    /// The staff-confirmed vehicle record wins, exactly as before. What changed
    /// (ENG-015) is the fallback: it used to read <c>Vehicle.Model</c> alone, so
    /// an export carried "X5 SE - X DRIVE Type 5 DOOR SUV" where EVA is sent
    /// "BMW X5 …". Both branches now compose the same way, so the two cannot
    /// state the vehicle differently.
    /// </summary>
    private static EvaEvidenceValue VehicleModel(
        ConfirmedVehicleEvidence? vehicle,
        CaseDataProjection caseData)
    {
        var confirmed = Compose(
            vehicle?.Make is null ? null : FromVehicleField(vehicle.Make, static value => value),
            vehicle?.Model is null ? null : FromVehicleField(vehicle.Model, static value => value));
        return string.IsNullOrWhiteSpace(confirmed.Value)
            ? Compose(
                FromCaseField(caseData.Vehicle.Make, static value => value),
                FromCaseField(caseData.Vehicle.Model, static value => value))
            : confirmed;
    }

    /// <summary>Make and model joined, skipping whichever the case lacks.</summary>
    private static EvaEvidenceValue Compose(EvaEvidenceValue? make, EvaEvidenceValue? model)
    {
        var values = new[] { make, model }
            .Where(value => value is not null && !string.IsNullOrWhiteSpace(value.Value))
            .Select(value => value!)
            .ToArray();
        return values.Length == 0 ? MissingEvidence : values.Aggregate(Combine);
    }

    /// <summary>
    /// EVA's own two words for the mileage unit (ENG-015). The original
    /// extractor resolves this field to exactly "Miles" or "Km", so those are
    /// the only two values a bundle may carry — written once here so the
    /// confirmed-record branch and the case-field branch cannot drift.
    /// </summary>
    private static string MileageUnit(VehicleMileageUnit unit) =>
        unit == VehicleMileageUnit.Kilometres ? "Km" : "Miles";

    private static string MileageUnit(string value) =>
        Enum.TryParse<VehicleMileageUnit>(value, ignoreCase: true, out var unit)
            ? MileageUnit(unit)
            : value.Trim();

    private static EvaEvidenceValue FromCaseField<T>(
        CaseField<T> field,
        Func<T, string> format)
        where T : notnull =>
        Accepted(field) is { } value
            ? FromCaseValue(value, format)
            : field.Suggestion is { } suggestion
                ? FromCaseValue(suggestion, format) with { Status = EvaEvidenceStatus.Suggested }
                : MissingEvidence;

    /// <summary>
    /// The vehicle fields have their own confirmed record. The export falls
    /// back to the case's own field when that record has nothing — which is
    /// where ENG-013 writes what the DVLA and DVSA lookup found, so an export
    /// carries a mileage the documents never supplied. It never overrides a
    /// confirmed value.
    /// </summary>
    private static EvaEvidenceValue Fallback<T>(
        EvaEvidenceValue confirmed,
        CaseField<T> field,
        Func<T, string> format)
        where T : notnull =>
        string.IsNullOrWhiteSpace(confirmed.Value)
            ? FromCaseField(field, format)
            : confirmed;

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
            EvaEvidenceStatus.Accepted,
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
            EvaEvidenceStatus.Accepted,
            $"vehicle:{field.SourceKind}:{field.SourceIdentity}:{field.SourceLabel};confirmed={field.ConfirmedByActor}@{field.ConfirmedAtUtc:O}{external}",
            sourceVersion);
    }

    private static EvaEvidenceValue Combine(EvaEvidenceValue first, EvaEvidenceValue second) => new(
        string.Join(' ', new[] { first.Value, second.Value }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())),
        first.IsAccepted && second.IsAccepted
            ? EvaEvidenceStatus.Accepted
            : EvaEvidenceStatus.Suggested,
        $"{first.Source}|{second.Source}",
        $"{first.SourceVersion}|{second.SourceVersion}");

    private static EvaEvidenceValue MissingEvidence { get; } =
        new(null, EvaEvidenceStatus.Unrecorded, "unrecorded", "unrecorded");

    private sealed record SelectedDocument(
        Guid OccurrenceId,
        int Ordinal,
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
        bool IsLogicallyRemoved,
        bool IsThirdPartyVehicle,
        string? CaseRootRemoteId);
}
