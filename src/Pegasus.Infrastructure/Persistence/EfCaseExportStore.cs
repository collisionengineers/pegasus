using System.Data;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.CaseExport;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The one act that produces the case export. It reads a case, maps the
/// thirteen fields, loads every eligible retained photograph, writes the
/// archive and records each successful export in action history. It changes
/// no case state or version, so it is available in every lifecycle state.
/// </summary>
public sealed class EfCaseExportStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    ICaseDataQueries caseDataQueries,
    IVehicleEvidenceQueries vehicleEvidenceQueries,
    CaseExportImageReader imageReader,
    TimeProvider timeProvider) : IExportCaseBundle
{
    /// <summary>
    /// The operator's export of a case. It takes no edit lease. Its operation
    /// key makes the action-history write replay-safe. A case with no eligible
    /// photograph exports the data file alone.
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
        var caseData = await caseDataQueries.GetAsync(request.CaseId, CaseWorkSelector.Current, cancellationToken);
        if (caseData is null)
        {
            return null;
        }
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var vehicle = await vehicleEvidenceQueries.GetAsync(request.CaseId, cancellationToken);
        var export = CaseExportMapping.MapForOperatorExport(
            CaseExportEvidenceReader.Build(caseData, vehicle),
            DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));

        var images = await imageReader.LoadEligibleImagesAsync(
            context,
            request.CaseId,
            caseData.Identity.Reference,
            cancellationToken);
        var bundle = CaseExportArchive.Create(
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
    /// One action-history row per distinct export. The short database section
    /// takes the case-workflow row lock, so same-case exports observe replay
    /// in commit order, while package creation and image reads stay outside
    /// the transaction.
    /// </summary>
    private async Task RecordExportAsync(
        PegasusDbContext context,
        ExportCaseBundleRequest request,
        CaseDataProjection caseData,
        CaseExportSource source,
        IReadOnlyList<CaseExportImage> images,
        CaseExportBundle bundle,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        if (!await LockCaseWorkflowAsync(context, request.CaseId, cancellationToken))
        {
            throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        }

        var aggregateId = request.CaseId.ToString("D");
        var eventKind = CaseExportPolicy.BundleExportedHistoryEventKind;
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

    private static Task<bool> LockCaseWorkflowAsync(
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
        return workflows.AnyAsync(cancellationToken);
    }
}
