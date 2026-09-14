using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Dismiss and Restore on a retained message (Inbox planning, 13 September).
/// The message row carries when and by whom it was dismissed; the act is one
/// action-history entry keyed by the operation, so a retried post returns the
/// committed state rather than writing a second entry. Neither act touches the
/// classification, the intake receipt or any link, and an open Unidentified item
/// is left open.
/// </summary>
internal sealed class EfRetainedMailDismissalStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IRetainedMailDismissalStore
{
    private const string AggregateType = "retained_mail";
    private const string DismissedEventKind = "retained_mail_dismissed";
    private const string RestoredEventKind = "retained_mail_restored";

    public Task<RetainedMailDismissal?> DismissAsync(
        DismissRetainedMailRequest request,
        CancellationToken cancellationToken) =>
        ApplyAsync(request.MessageId, request.Actor, request.OperationKey, dismiss: true, cancellationToken);

    public Task<RetainedMailDismissal?> RestoreAsync(
        RestoreRetainedMailRequest request,
        CancellationToken cancellationToken) =>
        ApplyAsync(request.MessageId, request.Actor, request.OperationKey, dismiss: false, cancellationToken);

    private async Task<RetainedMailDismissal?> ApplyAsync(
        Guid messageId,
        ActionActor actor,
        string operationKey,
        bool dismiss,
        CancellationToken cancellationToken)
    {
        var eventKind = dismiss ? DismissedEventKind : RestoredEventKind;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var replay = await context.ActionHistory.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.AggregateType == AggregateType && item.CorrelationId == operationKey,
                cancellationToken);
        if (replay is not null)
        {
            if (replay.AggregateId != messageId.ToString("D")
                || replay.EventKind != eventKind
                || replay.ActorSubjectId != actor.SubjectId)
            {
                throw new InvalidOperationException(
                    "The operation key was already used for a different dismissal.");
            }

            var state = JsonSerializer.Deserialize<DismissalState>(replay.AfterJson ?? "{}")
                ?? throw new InvalidDataException("The persisted dismissal replay is invalid.");
            await transaction.CommitAsync(cancellationToken);
            return new(messageId, state.DismissedAtUtc is not null, state.DismissedAtUtc, state.DismissedBy, IsReplay: true);
        }

        var message = await context.RetainedMailboxMessages
            .SingleOrDefaultAsync(item => item.Id == messageId, cancellationToken);
        if (message is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var before = new DismissalState(message.DismissedAtUtc, message.DismissedBySubjectId);
        if (dismiss)
        {
            message.DismissedAtUtc = now;
            message.DismissedBySubjectId = actor.SubjectId;
        }
        else
        {
            message.DismissedAtUtc = null;
            message.DismissedBySubjectId = null;
        }

        var after = new DismissalState(message.DismissedAtUtc, message.DismissedBySubjectId);
        context.ActionHistory.Add(new ActionHistoryEntity
        {
            Id = Guid.NewGuid(),
            AggregateType = AggregateType,
            AggregateId = messageId.ToString("D"),
            EventKind = eventKind,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(
                actor.Roles.OrderBy(role => role).Select(role => role.ToString())),
            OccurredAtUtc = now,
            Outcome = "succeeded",
            CorrelationId = operationKey,
            Reason = null,
            BeforeJson = JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(after),
            PolicyVersion = "retained-mail-dismissal-v1"
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(messageId, after.DismissedAtUtc is not null, after.DismissedAtUtc, after.DismissedBy, IsReplay: false);
    }

    private sealed record DismissalState(DateTimeOffset? DismissedAtUtc, string? DismissedBy);
}
