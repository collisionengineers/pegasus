using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Support;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfProblemReportStore(IDbContextFactory<PegasusDbContext> contextFactory) : IProblemReportStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ProblemReport> AddAsync(NewProblemReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = new ProblemReportEntity
        {
            Id = Guid.NewGuid(),
            StaffId = report.StaffId,
            Description = report.Description,
            SnapshotJson = JsonSerializer.Serialize(report.Snapshot, Json),
            Route = Truncate(report.Snapshot.Route, 400),
            CaseReference = Truncate(report.Snapshot.CaseReference, 40),
            CreatedAtUtc = report.CreatedAtUtc,
            Status = nameof(ProblemReportStatus.NotSent)
        };
        context.Set<ProblemReportEntity>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<ProblemReport?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<ProblemReportEntity>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<ProblemReport>> ListAsync(int count, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Set<ProblemReportEntity>().AsNoTracking()
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(count)
            .ToArrayAsync(cancellationToken);
        return rows.Select(Map).ToArray();
    }

    public async Task<ProblemReport> MarkSentAsync(Guid id, ProblemReportDelivery delivery, DateTimeOffset atUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<ProblemReportEntity>().SingleAsync(item => item.Id == id, cancellationToken);
        entity.Status = nameof(ProblemReportStatus.Sent);
        entity.IssueNumber = delivery.IssueNumber;
        entity.IssueUrl = Truncate(delivery.IssueUrl, 400);
        entity.Failure = null;
        entity.SentAtUtc = atUtc;
        await context.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<ProblemReport> MarkNotSentAsync(Guid id, string failure, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<ProblemReportEntity>().SingleAsync(item => item.Id == id, cancellationToken);
        entity.Status = nameof(ProblemReportStatus.NotSent);
        entity.Failure = Truncate(failure, 400);
        await context.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    private static string? Truncate(string? value, int length) =>
        value is { Length: > 0 } && value.Length > length ? value[..length] : value;

    private static ProblemReport Map(ProblemReportEntity entity) => new(
        entity.Id,
        entity.StaffId,
        entity.Description,
        JsonSerializer.Deserialize<ProblemReportSnapshot>(entity.SnapshotJson, Json)
            ?? throw new InvalidDataException($"Problem report {entity.Id:D} has no snapshot."),
        entity.CreatedAtUtc,
        Enum.Parse<ProblemReportStatus>(entity.Status),
        entity.IssueNumber,
        entity.IssueUrl,
        entity.Failure,
        entity.SentAtUtc);
}
