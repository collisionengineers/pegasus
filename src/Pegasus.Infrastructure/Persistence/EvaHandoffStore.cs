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
    EvaCaseImageReader imageReader,
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
}
