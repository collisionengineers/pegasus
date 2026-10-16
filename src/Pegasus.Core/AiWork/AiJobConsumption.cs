using Pegasus.Core.Identity;

namespace Pegasus.Core.AiWork;

/// <summary>
/// Why the one confirm of a consumed AI draft left its job as the ledger has
/// it (operator, 16 October 2026): the job was cancelled, another act had
/// already completed it, or it changed while the consuming act ran.
/// </summary>
public enum AiJobConfirmRefusal
{
    Cancelled,
    AlreadyCompleted,
    Changed
}

/// <summary>
/// The one confirm of a staff act that consumes a Draft ready job's result
/// (FRD-27 § AI Job List): **Use repair spec** for an Estimate job, and the
/// accepted send of a reply composed on a Query response draft. Only a Draft
/// ready job is confirmed. A job that was cancelled, that another act
/// completed, or that the ledger finds changed under the confirm is a refusal
/// the act may carry; any other failure propagates.
/// </summary>
public static class AiJobConsumption
{
    /// <summary>
    /// Confirms <paramref name="job"/> Completed for the act. Null when this
    /// act completed it, now or on an earlier run under the same key, or when
    /// the job never reached Draft ready and so held no draft to consume.
    /// </summary>
    public static async Task<AiJobConfirmRefusal?> ConfirmAsync(
        IAiJobStore jobs,
        IConfirmAiJob confirm,
        AiJobRecord job,
        ActionActor actor,
        string actOperationKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(confirm);
        ArgumentNullException.ThrowIfNull(job);
        var key = ConfirmationKey(actOperationKey);
        switch (AiJobPolicy.EffectiveState(job, now))
        {
            case AiJobState.DraftReady:
                break;
            case AiJobState.Cancelled or AiJobState.Completed:
                return Refusal(job, now, key);
            default:
                return null;
        }

        try
        {
            await confirm.ExecuteAsync(new(job.JobId, job.Version, actor, key), cancellationToken);
            return null;
        }
        catch (InvalidOperationException)
        {
            // The ledger refused. That is this confirm's refusal only when the
            // job moved on after it was read; otherwise the cause lies elsewhere.
            var current = await jobs.GetAsync(job.JobId, cancellationToken);
            if (current is null || current.Version == job.Version)
            {
                throw;
            }
            return Refusal(current, now, key);
        }
    }

    /// <summary>
    /// The confirmation's operation key: the act's own key, cut to 96
    /// characters, plus <c>:job</c>, so a replayed act replays its confirmation
    /// inside the ledger's 100-character key limit.
    /// </summary>
    private static string ConfirmationKey(string actOperationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actOperationKey);
        return string.Concat(actOperationKey.AsSpan(0, Math.Min(actOperationKey.Length, 96)), ":job");
    }

    private static AiJobConfirmRefusal? Refusal(AiJobRecord job, DateTimeOffset now, string key) =>
        AiJobPolicy.EffectiveState(job, now) switch
        {
            AiJobState.Cancelled => AiJobConfirmRefusal.Cancelled,
            // This act's own confirmation, read back when the act is replayed.
            AiJobState.Completed when string.Equals(job.LastOperationKey, key, StringComparison.Ordinal) => null,
            AiJobState.Completed => AiJobConfirmRefusal.AlreadyCompleted,
            _ => AiJobConfirmRefusal.Changed
        };
}
