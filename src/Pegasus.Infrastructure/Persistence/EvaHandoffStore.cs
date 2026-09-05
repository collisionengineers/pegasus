using System.Data;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
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
    EvaCaseImageReader imageReader,
    IEvaHandoffProxy proxy,
    TimeProvider timeProvider) : IExportCaseBundle
{
    /// <summary>
    /// CASE-019 / ENG-016: the operator's export of a case as the EVA-format
    /// archive, and since ENG-016 the only way to produce one.
    ///
    /// It takes no edit lease. The first export from Review atomically starts
    /// case work and increments the version; a re-send from With Engineer
    /// leaves both unchanged. Its operation key makes the action-history
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
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workflow = await context.CaseWorkflows
            .AsNoTracking()
            .Where(item => item.CaseId == request.CaseId)
            .Select(item => new
            {
                item.State,
                item.AssignedEngineerId,
                item.SignOffEngineerId
            })
            .SingleAsync(cancellationToken);
        var profiles = await new EfStaffAccountQueries(context)
            .ListSignOffEngineersAsync(cancellationToken);
        EvaHandoffPolicy.StateAfterManualSend(Enum.Parse<CaseLifecycleState>(workflow.State));
        EvaHandoffPolicy.ResolveRequiredSignOffEngineer(
            workflow.SignOffEngineerId,
            workflow.AssignedEngineerId,
            profiles);
        var vehicle = await vehicleEvidenceQueries.GetAsync(request.CaseId, cancellationToken);
        var export = CaseEvaMapping.MapForOperatorExport(
            EvaCaseEvidenceReader.Build(caseData, vehicle),
            DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));

        var images = await imageReader.LoadEligibleImagesAsync(
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
        if (lockedState is null)
        {
            throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        }
        var currentState = Enum.Parse<CaseLifecycleState>(lockedState.State);
        var resultingState = EvaHandoffPolicy.StateAfterManualSend(currentState);
        if (resultingState != currentState)
        {
            await CaseEngineerEligibilityPolicy.RequireStartCaseWorkAsync(
                new EfCaseEngineerEligibility(contextFactory),
                currentState,
                lockedState.AssignedEngineerId,
                cancellationToken);
        }
        var eligibleProfiles = await new EfStaffAccountQueries(context)
            .ListSignOffEngineersAsync(cancellationToken);
        var signOffEngineer = EvaHandoffPolicy.ResolveRequiredSignOffEngineer(
            lockedState.SignOffEngineerId,
            lockedState.AssignedEngineerId,
            eligibleProfiles);
        if (lockedState.WorkflowVersion != caseData.Version)
        {
            throw new CaseVersionConflictException(
                request.CaseId,
                caseData.Version,
                lockedState.WorkflowVersion);
        }

        var aggregateId = request.CaseId.ToString("D");
        var eventKind = EvaHandoffPolicy.BundleExportedHistoryEventKind;
        var resultingVersion = currentState == CaseLifecycleState.Review
            ? checked(lockedState.WorkflowVersion + 1)
            : lockedState.WorkflowVersion;
        var afterJson = DocumentActionHistory.Serialize(new
        {
            CaseVersion = resultingVersion,
            AssignedEngineerId = lockedState.AssignedEngineerId,
            SignOffEngineerId = signOffEngineer.StaffId,
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

        if (currentState == CaseLifecycleState.Review)
        {
            lockedState.Workflow.State = resultingState.ToString();
            lockedState.Workflow.Version = resultingVersion;
            lockedState.Workflow.EditLeaseToken = null;
            lockedState.Workflow.EditLeaseTokenHash = null;
            lockedState.Workflow.EditLeaseRequestHash = null;
            lockedState.Workflow.EditLeaseHolder = null;
            lockedState.Workflow.EditLeaseHolderKind = null;
            lockedState.Workflow.EditLeaseOperationKey = null;
            lockedState.Workflow.EditLeaseExpiresAtUtc = null;
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
                LatestExportedWorkflowVersion = resultingVersion,
                ActorSubjectId = request.Actor.SubjectId,
                ClaimsExternalDelivery = false,
                ClaimsEngineerAssignment = false
            };
            context.EvaFirstHandoffProxies.Add(handoff);
        }
        else
        {
            handoff.LatestExportedWorkflowVersion = resultingVersion;
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
            .Select(item => new LockedExportState(
                item,
                item.State,
                item.Version,
                item.AssignedEngineerId,
                item.SignOffEngineerId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private sealed record LockedExportState(
        CaseWorkflowEntity Workflow,
        string State,
        long WorkflowVersion,
        Guid? AssignedEngineerId,
        Guid? SignOffEngineerId);
}
