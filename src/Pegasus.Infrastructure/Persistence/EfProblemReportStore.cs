using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Support;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfProblemReportStore(IDbContextFactory<PegasusDbContext> contextFactory) : IProblemReportStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ProblemReportAddResult> AddAsync(NewProblemReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.Set<ProblemReportEntity>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationKey == report.OperationKey, cancellationToken);
        if (existing is not null)
        {
            return Replay(existing, report);
        }

        var entity = new ProblemReportEntity
        {
            Id = Guid.NewGuid(),
            StaffId = report.StaffId,
            Description = report.Description,
            SnapshotJson = JsonSerializer.Serialize(report.Snapshot, Json),
            Route = Truncate(report.Snapshot.Route, 400),
            CaseReference = Truncate(report.Snapshot.CaseReference, 40),
            CreatedAtUtc = report.CreatedAtUtc,
            OperationKey = report.OperationKey,
            RequestHash = report.RequestHash,
            Status = nameof(ProblemReportStatus.NotSent)
        };
        context.Set<ProblemReportEntity>().Add(entity);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return new(Map(entity), false);
        }
        catch (DbUpdateException exception) when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            await using var replayContext = await contextFactory.CreateDbContextAsync(cancellationToken);
            var winner = await replayContext.Set<ProblemReportEntity>().AsNoTracking()
                .SingleAsync(item => item.OperationKey == report.OperationKey, cancellationToken);
            return Replay(winner, report);
        }
    }

    public async Task<ProblemReport?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<ProblemReportEntity>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<ProblemReport>> ListAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Set<ProblemReportEntity>().AsNoTracking()
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.Id)
            .ToArrayAsync(cancellationToken);
        return rows.Select(Map).ToArray();
    }

    public async Task<ProblemReport?> TryClaimAsync(
        Guid id,
        string claimToken,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimToken);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
        var claimExpiresAtUtc = nowUtc.Add(leaseDuration);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Set<ProblemReportEntity>()
            .Where(item => item.Id == id
                && item.Status == nameof(ProblemReportStatus.NotSent)
                && item.DispatchClaimToken != null
                && (item.DispatchClaimExpiresAtUtc == null
                    || item.DispatchClaimExpiresAtUtc <= nowUtc))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, nameof(ProblemReportStatus.Unknown))
                .SetProperty(item => item.Failure, "The previous GitHub outcome is unknown. Reconcile by report ID.")
                .SetProperty(item => item.DispatchClaimExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(item => item.DispatchClaimToken, (string?)null), cancellationToken);
        var claimed = await context.Set<ProblemReportEntity>()
            .Where(item => item.Id == id
                && item.Status == nameof(ProblemReportStatus.NotSent)
                && item.DispatchClaimToken == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.DispatchClaimToken, claimToken)
                .SetProperty(item => item.DispatchClaimExpiresAtUtc, claimExpiresAtUtc), cancellationToken);
        if (claimed == 0)
        {
            return null;
        }

        var entity = await context.Set<ProblemReportEntity>().AsNoTracking()
            .SingleAsync(item => item.Id == id, cancellationToken);
        return Map(entity);
    }

    public async Task<ProblemReport> MarkSentAsync(Guid id, string claimToken, ProblemReportDelivery delivery, DateTimeOffset atUtc, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimToken);
        ArgumentNullException.ThrowIfNull(delivery);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var changed = await context.Set<ProblemReportEntity>()
            .Where(item => item.Id == id
                && item.Status == nameof(ProblemReportStatus.NotSent)
                && item.DispatchClaimToken == claimToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, nameof(ProblemReportStatus.Sent))
                .SetProperty(item => item.IssueNumber, delivery.IssueNumber)
                .SetProperty(item => item.IssueUrl, Truncate(delivery.IssueUrl, 400))
                .SetProperty(item => item.Failure, (string?)null)
                .SetProperty(item => item.SentAtUtc, atUtc)
                .SetProperty(item => item.DispatchClaimExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(item => item.DispatchClaimToken, (string?)null), cancellationToken);
        if (changed != 1)
        {
            throw new ProblemReportClaimConflictException();
        }
        return await GetRequiredAsync(context, id, cancellationToken);
    }

    public async Task<ProblemReport> MarkNotSentAsync(Guid id, string claimToken, string failure, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var changed = await context.Set<ProblemReportEntity>()
            .Where(item => item.Id == id
                && item.Status == nameof(ProblemReportStatus.NotSent)
                && item.DispatchClaimToken == claimToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, nameof(ProblemReportStatus.NotSent))
                .SetProperty(item => item.Failure, Truncate(failure, 400))
                .SetProperty(item => item.DispatchClaimExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(item => item.DispatchClaimToken, (string?)null), cancellationToken);
        if (changed != 1)
        {
            throw new ProblemReportClaimConflictException();
        }
        return await GetRequiredAsync(context, id, cancellationToken);
    }

    public async Task<ProblemReport> MarkUnknownAsync(
        Guid id, string claimToken, string reason, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var changed = await context.Set<ProblemReportEntity>()
            .Where(item => item.Id == id
                && item.Status == nameof(ProblemReportStatus.NotSent)
                && item.DispatchClaimToken == claimToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, nameof(ProblemReportStatus.Unknown))
                .SetProperty(item => item.Failure, Truncate(reason, 400))
                .SetProperty(item => item.DispatchClaimExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(item => item.DispatchClaimToken, (string?)null), cancellationToken);
        if (changed != 1)
        {
            throw new ProblemReportClaimConflictException();
        }
        return await GetRequiredAsync(context, id, cancellationToken);
    }

    public async Task<ProblemReport> ConfirmIssueAsync(
        Guid id, ProblemReportDelivery delivery, DateTimeOffset atUtc, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var changed = await context.Set<ProblemReportEntity>()
            .Where(item => item.Id == id && item.Status == nameof(ProblemReportStatus.Unknown))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, nameof(ProblemReportStatus.Sent))
                .SetProperty(item => item.IssueNumber, delivery.IssueNumber)
                .SetProperty(item => item.IssueUrl, Truncate(delivery.IssueUrl, 400))
                .SetProperty(item => item.Failure, (string?)null)
                .SetProperty(item => item.SentAtUtc, atUtc), cancellationToken);
        if (changed != 1)
        {
            throw new ProblemReportClaimConflictException();
        }
        return await GetRequiredAsync(context, id, cancellationToken);
    }

    public async Task<ProblemReport> ConfirmNoIssueAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var changed = await context.Set<ProblemReportEntity>()
            .Where(item => item.Id == id && item.Status == nameof(ProblemReportStatus.Unknown))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, nameof(ProblemReportStatus.NotSent))
                .SetProperty(item => item.Failure, (string?)null), cancellationToken);
        if (changed != 1)
        {
            throw new ProblemReportClaimConflictException();
        }
        return await GetRequiredAsync(context, id, cancellationToken);
    }

    private static ProblemReportAddResult Replay(ProblemReportEntity existing, NewProblemReport report)
    {
        if (existing.StaffId != report.StaffId
            || !string.Equals(existing.RequestHash, report.RequestHash, StringComparison.Ordinal))
        {
            throw new ProblemReportOperationConflictException();
        }

        return new(Map(existing), true);
    }

    private static async Task<ProblemReport> GetRequiredAsync(
        PegasusDbContext context,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await context.Set<ProblemReportEntity>().AsNoTracking()
            .SingleAsync(item => item.Id == id, cancellationToken);
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
        entity.SentAtUtc,
        entity.DispatchClaimExpiresAtUtc,
        entity.DispatchClaimToken,
        entity.OperationKey,
        entity.RequestHash);
}
