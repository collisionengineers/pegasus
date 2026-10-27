using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Who took an AI estimate into a Case (v42 Design A). The signal is a
/// <c>CaseRepairSpecifications</c> row with <c>SourceRoute = AiDraft</c> and
/// the job's id, not <see cref="Pegasus.Core.AiWork.AiJobState.Taken"/>. The
/// Engineer is the staff act that set that specification current, read from
/// the history the act wrote; a staff save of the draft is the fallback when
/// no such act is recorded.
/// </summary>
internal sealed class EfAiDraftAdoptionQueries(
    IDbContextFactory<PegasusDbContext> contextFactory) : IAiDraftAdoptionQueries
{
    private const string SetCurrentEvent = "estimate_set_current";
    private static readonly string AiDraftRoute = RepairSpecificationSourceRoute.AiDraft.ToString();
    private static readonly string StaffActorKind = nameof(ActorKind.Staff);

    public async Task<IReadOnlyDictionary<Guid, Guid>> ReadAsync(
        IReadOnlyCollection<Guid> jobIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(jobIds);
        if (jobIds.Count == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var specifications = await context.CaseRepairSpecifications.AsNoTracking()
            .Where(item => item.AiJobId != null
                && jobIds.Contains(item.AiJobId.Value)
                && item.SourceRoute == AiDraftRoute)
            .Select(item => new SpecificationRow(
                item.AiJobId!.Value,
                item.Id,
                item.Work.CaseId,
                item.CreatedBy,
                item.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
        if (specifications.Length == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        var caseIds = specifications.Select(item => item.CaseId.ToString("D")).Distinct().ToArray();
        var history = await context.ActionHistory.AsNoTracking()
            .Where(item => item.EventKind == SetCurrentEvent
                && item.ActorKind == StaffActorKind
                && caseIds.Contains(item.AggregateId)
                && item.AfterJson != null)
            .Select(item => new HistoryRow(item.AggregateId, item.ActorSubjectId, item.OccurredAtUtc, item.AfterJson!))
            .ToArrayAsync(cancellationToken);

        var adopted = new Dictionary<Guid, Guid>();
        foreach (var group in specifications.GroupBy(item => item.JobId))
        {
            var staffId = AdoptingStaff(group.ToArray(), history);
            if (staffId is { } id)
            {
                adopted[group.Key] = id;
            }
        }

        return adopted;
    }

    /// <summary>
    /// The earliest staff act that made one of the job's drafted specifications
    /// current. The set-current payload names the specification as <c>id</c>
    /// (the store serialises with the web defaults). A later switch away does
    /// not forget who took it. With no such act, the staff member who saved the
    /// draft is the adopter.
    /// </summary>
    private static Guid? AdoptingStaff(IReadOnlyList<SpecificationRow> specifications, IReadOnlyList<HistoryRow> history)
    {
        var ids = specifications.Select(item => item.SpecificationId).ToHashSet();
        var caseKeys = specifications.Select(item => item.CaseId.ToString("D")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var first = history
            .Where(item => caseKeys.Contains(item.AggregateId) && NamesSpecification(item.AfterJson, ids))
            .OrderBy(item => item.OccurredAtUtc)
            .Select(item => Guid.TryParse(item.ActorSubjectId, out var staffId) ? staffId : (Guid?)null)
            .FirstOrDefault(item => item is not null);
        if (first is { } adoptedBy)
        {
            return adoptedBy;
        }

        return specifications
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => Guid.TryParse(item.CreatedBy, out var staffId) ? staffId : (Guid?)null)
            .FirstOrDefault(item => item is not null);
    }

    private static bool NamesSpecification(string json, HashSet<Guid> specificationIds)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            return Guid.TryParse(id.GetString(), out var parsed) && specificationIds.Contains(parsed);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record SpecificationRow(
        Guid JobId,
        Guid SpecificationId,
        Guid CaseId,
        string CreatedBy,
        DateTimeOffset CreatedAtUtc);

    private sealed record HistoryRow(
        string AggregateId,
        string ActorSubjectId,
        DateTimeOffset OccurredAtUtc,
        string AfterJson);
}
