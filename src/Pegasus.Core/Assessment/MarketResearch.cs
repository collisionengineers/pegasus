using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

/// <summary>
/// The Valuation section's "AI market research" button (planning, 13 September):
/// it starts the existing Market research AI job for the Case and the guide
/// month, and the card shows as pending until the job hands its figures back.
/// One job at a time: pressing again while one is in progress returns that job.
/// The other guide buttons have no provider yet and start nothing.
/// </summary>
public sealed record StartMarketResearchRequest(
    Guid CaseId,
    DateOnly GuideMonth,
    ActionActor Actor,
    string OperationKey);

public interface IStartMarketResearch
{
    Task<AiJobRecord> ExecuteAsync(StartMarketResearchRequest request, CancellationToken cancellationToken);
}

/// <summary>The Case's Market research job still in progress, so the card can read as pending.</summary>
public interface IMarketResearchQueries
{
    Task<AiJobRecord?> GetPendingAsync(Guid caseId, CancellationToken cancellationToken);
}

public static class MarketResearchPolicy
{
    /// <summary>
    /// A market research job is pending while a client may still be working it. Once
    /// its files and figures are attached the card is filled, so a Draft ready job
    /// neither shows as pending nor blocks a run for another month.
    /// </summary>
    public static bool IsPending(AiJobRecord job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return job.Kind == AiJobKind.MarketResearch
            && job.State is AiJobState.Queued or AiJobState.Taken;
    }

    /// <summary>The instruction the job carries, naming the month the research is for.</summary>
    public static string Instruction(DateOnly guideMonth) =>
        $"Market research valuation for guide month {guideMonth:yyyy-MM}.";

    public static DateOnly ValidateGuideMonth(DateOnly guideMonth)
    {
        if (guideMonth.Day != 1)
        {
            throw new ArgumentException(
                "The valuation guide month must be represented by the first day of the month.",
                nameof(guideMonth));
        }

        return guideMonth;
    }
}

public sealed class MarketResearchQueries(IAiJobQueries jobs) : IMarketResearchQueries
{
    private readonly IAiJobQueries _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));

    public async Task<AiJobRecord?> GetPendingAsync(Guid caseId, CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        var jobs = await _jobs.ListForSubjectAsync(caseId, cancellationToken);
        return jobs
            .Where(MarketResearchPolicy.IsPending)
            .OrderByDescending(job => job.CreatedAtUtc)
            .FirstOrDefault();
    }
}

public sealed class StartMarketResearch(
    IMarketResearchQueries queries,
    ICreateAiJob createJob) : IStartMarketResearch
{
    private readonly IMarketResearchQueries _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    private readonly ICreateAiJob _createJob = createJob ?? throw new ArgumentNullException(nameof(createJob));

    public async Task<AiJobRecord> ExecuteAsync(
        StartMarketResearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        if (request.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.OperationKey))
        {
            throw new ArgumentException("An operation key is required.", nameof(request));
        }

        var guideMonth = MarketResearchPolicy.ValidateGuideMonth(request.GuideMonth);
        var pending = await _queries.GetPendingAsync(request.CaseId, cancellationToken);
        if (pending is not null)
        {
            return pending;
        }

        return await _createJob.ExecuteAsync(
            new CreateAiJobCommand(
                AiJobKind.MarketResearch,
                request.CaseId,
                null,
                MarketResearchPolicy.Instruction(guideMonth),
                null,
                request.Actor,
                request.OperationKey.Trim()),
            cancellationToken);
    }
}
