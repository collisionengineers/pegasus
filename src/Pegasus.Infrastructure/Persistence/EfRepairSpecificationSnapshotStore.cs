using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Frozen repair specification versions (v28 P43). A version is the header
/// and lines as JSON; an unchanged draft reuses the latest version unless
/// the act must leave its own mark.
/// </summary>
public sealed class EfRepairSpecificationSnapshotStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IRepairSpecificationSnapshotStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.General);

    public async Task<RepairSpecificationSnapshot> FreezeAsync(
        FreezeRepairSpecificationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Kind == RepairSpecificationSnapshotKind.Sent)
        {
            throw new InvalidOperationException("A Sent version is recorded only with observed mail evidence.");
        }
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.CaseRepairSpecifications.Include(item => item.Lines).AsNoTracking()
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId && item.Id == request.SpecificationId, cancellationToken)
            ?? throw new KeyNotFoundException("The repair specification was not found.");
        var frozen = Freeze(
            context, EfRepairSpecificationStore.Map(entity), request.Actor, request.Kind, request.Origin,
            timeProvider.GetUtcNow(),
            await context.CaseRepairSpecificationSnapshots.AsNoTracking()
                .Where(item => item.SpecificationId == request.SpecificationId)
                .OrderByDescending(item => item.Number)
                .FirstOrDefaultAsync(cancellationToken));
        await context.SaveChangesAsync(cancellationToken);
        return Map(frozen);
    }

    /// <summary>
    /// Adds the version row inside the caller's context. The latest version
    /// is reused when the content is unchanged and the act leaves no mark of
    /// its own (BeforeScaling, Scaled, ScalingRemoved, both restore marks and
    /// Sent always do).
    /// </summary>
    internal static CaseRepairSpecificationSnapshotEntity Freeze(
        PegasusDbContext context,
        RepairSpecificationVersion specification,
        ActionActor actor,
        RepairSpecificationSnapshotKind kind,
        string origin,
        DateTimeOffset now,
        CaseRepairSpecificationSnapshotEntity? latest = null)
    {
        latest ??= context.CaseRepairSpecificationSnapshots.Local
            .Where(item => item.SpecificationId == specification.SpecificationId)
            .OrderByDescending(item => item.Number)
            .FirstOrDefault();
        var detailsJson = JsonSerializer.Serialize(specification.Details, Json);
        var linesJson = JsonSerializer.Serialize(
            specification.Lines.OrderBy(line => line.Position).ToArray(), Json);
        var supplementaryJson = JsonSerializer.Serialize(specification.Supplementary, Json);
        var hash = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(detailsJson + linesJson + supplementaryJson)));
        var marks = kind is RepairSpecificationSnapshotKind.BeforeScaling
            or RepairSpecificationSnapshotKind.Scaled
            or RepairSpecificationSnapshotKind.ScalingRemoved
            or RepairSpecificationSnapshotKind.BeforeRestore
            or RepairSpecificationSnapshotKind.Restored
            or RepairSpecificationSnapshotKind.Sent;
        if (latest is not null && latest.ContentHash == hash && !marks)
        {
            return latest;
        }
        var entity = new CaseRepairSpecificationSnapshotEntity
        {
            Id = Guid.NewGuid(),
            CaseId = specification.CaseId,
            SpecificationId = specification.SpecificationId,
            Number = (latest?.Number ?? 0) + 1,
            Kind = kind.ToString(),
            Origin = origin.Length > 500 ? origin[..500] : origin,
            CreatedBy = actor.SubjectId,
            CreatedAtUtc = now,
            DetailsJson = detailsJson,
            LinesJson = linesJson,
            SupplementaryJson = supplementaryJson,
            ContentHash = hash,
            Gross = EstimateTotals.ForProjection(specification).Printed.Gross,
            SentOnReport = kind == RepairSpecificationSnapshotKind.Sent,
        };
        context.CaseRepairSpecificationSnapshots.Add(entity);
        return entity;
    }

    public async Task<IReadOnlyList<RepairSpecificationSnapshot>> ListAsync(
        Guid caseId, Guid specificationId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.CaseRepairSpecificationSnapshots.AsNoTracking()
            .Where(item => item.CaseId == caseId && item.SpecificationId == specificationId)
            .OrderBy(item => item.Number)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToArray();
    }

    public async Task<RepairSpecificationSnapshot?> GetAsync(
        Guid caseId, Guid snapshotId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.CaseRepairSpecificationSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CaseId == caseId && item.Id == snapshotId, cancellationToken);
        return row is null ? null : Map(row);
    }

    internal static RepairSpecificationSnapshot Map(CaseRepairSpecificationSnapshotEntity entity) => new(
        entity.Id, entity.CaseId, entity.SpecificationId, entity.Number,
        Enum.Parse<RepairSpecificationSnapshotKind>(entity.Kind), entity.Origin,
        entity.CreatedBy, entity.CreatedAtUtc,
        JsonSerializer.Deserialize<EstimateDetails>(entity.DetailsJson, Json)
            ?? throw new InvalidOperationException("A frozen version has no header."),
        JsonSerializer.Deserialize<CaseEstimateLineRecord[]>(entity.LinesJson, Json) ?? [],
        entity.Gross, entity.SentOnReport,
        JsonSerializer.Deserialize<RepairSpecificationSupplementary?>(entity.SupplementaryJson, Json));
}
