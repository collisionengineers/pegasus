using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfRetainedMailFolderMoveStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    IRetainedMailFolderMover mover,
    TimeProvider timeProvider) : IRetainedMailFolderMoveStore
{
    private static readonly TimeSpan CancellationHandoffTimeout = TimeSpan.FromSeconds(30);

    public async Task<RetainedMailFolderMoveResult?> GetLatestAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var operation = await context.RetainedMailFolderMoves.AsNoTracking()
            .Where(item => item.RetainedMailboxMessageId == messageId && item.Outcome != "pending")
            .OrderByDescending(item => item.RecordedAtUtc)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return operation is null ? null : Map(operation, false);
    }

    public async Task<bool> IsCurrentLocationAsync(
        Guid messageId,
        string folderIdentity,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var current = await context.RetainedMailFolderMoves.AsNoTracking()
            .Where(item => item.RetainedMailboxMessageId == messageId && item.Outcome == "succeeded")
            .OrderByDescending(item => item.RecordedAtUtc)
            .ThenByDescending(item => item.Id)
            .Select(item => item.DestinationFolderId)
            .FirstOrDefaultAsync(cancellationToken);
        return string.Equals(current, folderIdentity, StringComparison.Ordinal);
    }

    public async Task<RetainedMailFolderMoveResult?> MoveAsync(
        ActionActor actor,
        MoveRetainedMailFolderRequest request,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var requestHash = Hash(request, actor);
        var replay = await context.RetainedMailFolderMoves
            .SingleOrDefaultAsync(item => item.OperationKey == request.OperationKey, cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay.RequestHash, requestHash, StringComparison.Ordinal))
            {
                throw new RetainedMailFolderMoveException("The operation key was already used for different move inputs.");
            }
            if (replay.Outcome == "pending")
            {
                throw new RetainedMailFolderMoveException("The folder move is still being processed.");
            }
            if (replay.Outcome == "uncertain")
            {
                await RecoverAsync(context, replay, cancellationToken);
            }
            return Map(replay, true);
        }

        var retained = await context.RetainedMailboxMessages
            .SingleOrDefaultAsync(item => item.Id == request.MessageId, cancellationToken);
        if (retained is null)
        {
            return null;
        }
        var decision = await context.IntakeReceipts
            .Where(item => item.SourceChannel == "mailbox"
                && item.ExternalReceiptToken == retained.ExternalReceiptToken)
            .Select(item => item.MailClassificationDecision)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new RetainedMailFolderMoveException("The message has no current classification.");
        if (decision.Version != request.ExpectedClassificationVersion)
        {
            throw new RetainedMailFolderMoveException("The classification changed. Reload the message before confirming the move.");
        }

        var policy = MailLogicalFolderPolicy.Map(EfIntakeReceiptStore.MapMailClassificationDecision(decision));
        if (policy.FolderType is not { } folderType
            || !string.Equals(policy.PolicyKey, request.ExpectedRecommendationPolicyKey, StringComparison.Ordinal)
            || policy.PolicyVersion != request.ExpectedRecommendationPolicyVersion)
        {
            throw new RetainedMailFolderMoveException("The folder recommendation changed. Reload the message before confirming the move.");
        }

        var approved = await context.ApprovedMailboxes
            .Include(item => item.FolderBindings)
            .SingleOrDefaultAsync(item => item.MailboxIdentity == retained.MailboxId, cancellationToken);
        if (approved is null
            || approved.State != ApprovedMailboxState.Approved.ToString()
            || approved.Version != request.ExpectedMailboxVersion)
        {
            throw new RetainedMailFolderMoveException("The approved mailbox binding changed. Reload the message before confirming the move.");
        }
        var destination = approved.FolderBindings.SingleOrDefault(item =>
            item.FolderType == folderType.ToString())?.FolderIdentity
            ?? throw new RetainedMailFolderMoveException("The designated Outlook folder is unavailable.");
        var latestSuccessfulMove = await context.RetainedMailFolderMoves
            .Where(item => item.RetainedMailboxMessageId == retained.Id && item.Outcome == "succeeded")
            .OrderByDescending(item => item.RecordedAtUtc)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var currentFolderId = latestSuccessfulMove?.DestinationFolderId ?? retained.FolderIdentity;
        if (string.Equals(currentFolderId, destination, StringComparison.Ordinal))
        {
            throw new RetainedMailFolderMoveException("The message is already in the designated Outlook folder.");
        }
        if (await context.RetainedMailFolderMoves.AnyAsync(item =>
                item.RetainedMailboxMessageId == retained.Id
                && (item.Outcome == "pending" || item.Outcome == "uncertain"), cancellationToken))
        {
            throw new RetainedMailFolderMoveException("A previous move is still being recovered. Retry that operation instead.");
        }
        var operation = new RetainedMailFolderMoveEntity
        {
            Id = Guid.NewGuid(),
            RetainedMailboxMessageId = retained.Id,
            RetainedMailboxMessage = retained,
            OperationKey = request.OperationKey,
            RequestHash = requestHash,
            ExpectedClassificationVersion = request.ExpectedClassificationVersion,
            ExpectedRecommendationPolicyKey = request.ExpectedRecommendationPolicyKey,
            ExpectedRecommendationPolicyVersion = request.ExpectedRecommendationPolicyVersion,
            ExpectedMailboxVersion = request.ExpectedMailboxVersion,
            MailboxId = retained.MailboxId,
            ImmutableMessageId = retained.ImmutableMessageId,
            SourceFolderId = currentFolderId,
            DestinationFolderId = destination,
            FolderType = folderType.ToString(),
            Actor = MailClassificationActor.Format(actor),
            ActorRolesJson = JsonSerializer.Serialize(
                actor.Roles.OrderBy(role => role).Select(role => role.ToString())),
            Reason = request.Reason,
            Outcome = "pending",
            RecordedAtUtc = timeProvider.GetUtcNow()
        };
        context.RetainedMailFolderMoves.Add(operation);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new RetainedMailFolderMoveException("The operation key was claimed concurrently. Reload before retrying.");
        }

        if (!mover.IsAvailable)
        {
            await CompleteAsync(context, operation, "failed", "Outlook folder moves are unavailable in this runtime.", cancellationToken);
            return Map(operation, false);
        }

        string? currentParent;
        try
        {
            currentParent = await mover.GetParentFolderIdAsync(
                operation.MailboxId,
                operation.ImmutableMessageId,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await MarkUncertainAfterCancellationAsync(operation.Id);
            throw;
        }
        catch
        {
            await CompleteAsync(context, operation, "failed", "The current Outlook folder could not be confirmed.", cancellationToken);
            return Map(operation, false);
        }
        if (!string.Equals(currentParent, operation.SourceFolderId, StringComparison.Ordinal))
        {
            await CompleteAsync(context, operation, "failed", "The message is no longer in the expected Outlook folder.", cancellationToken);
            return Map(operation, false);
        }

        try
        {
            await mover.MoveAsync(Coordinates(operation), cancellationToken);
            await CompleteAsync(context, operation, "succeeded", null, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await MarkUncertainAfterCancellationAsync(operation.Id);
            throw;
        }
        catch (Exception exception)
        {
            operation.Outcome = "uncertain";
            operation.FailureReason = exception.Message;
            await context.SaveChangesAsync(cancellationToken);
            await RecoverAsync(context, operation, cancellationToken);
        }
        return Map(operation, false);
    }

    private async Task MarkUncertainAfterCancellationAsync(Guid operationId)
    {
        using var handoff = new CancellationTokenSource(CancellationHandoffTimeout);
        await using var context = await contextFactory.CreateDbContextAsync(handoff.Token);
        await context.RetainedMailFolderMoves
            .Where(item => item.Id == operationId && item.Outcome == "pending")
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.Outcome, "uncertain")
                    .SetProperty(
                        item => item.FailureReason,
                        "The request ended before the Outlook move outcome was recorded."),
                handoff.Token);
    }

    private async Task RecoverAsync(
        PegasusDbContext context,
        RetainedMailFolderMoveEntity operation,
        CancellationToken cancellationToken)
    {
        string? parent = null;
        try
        {
            parent = await mover.GetParentFolderIdAsync(
                operation.MailboxId,
                operation.ImmutableMessageId,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // The durable uncertain state is the safe outcome when the probe also fails.
        }
        var outcome = string.Equals(parent, operation.DestinationFolderId, StringComparison.Ordinal)
            ? "succeeded"
            : string.Equals(parent, operation.SourceFolderId, StringComparison.Ordinal)
                ? "failed"
                : "uncertain";
        await CompleteAsync(context, operation, outcome, operation.FailureReason, cancellationToken);
    }

    private async Task CompleteAsync(
        PegasusDbContext context,
        RetainedMailFolderMoveEntity operation,
        string outcome,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        if (operation.Outcome == outcome && operation.CompletedAtUtc is not null)
        {
            return;
        }
        operation.Outcome = outcome;
        operation.FailureReason = failureReason;
        operation.CompletedAtUtc = timeProvider.GetUtcNow();
        context.ActionHistory.Add(new ActionHistoryEntity
        {
            Id = Guid.NewGuid(),
            AggregateType = "retained-mail",
            AggregateId = operation.RetainedMailboxMessageId.ToString("D"),
            EventKind = "outlook-folder-move",
            ActorKind = operation.Actor.Split(':', 2)[0],
            ActorSubjectId = operation.Actor.Split(':', 2)[1],
            ActorRolesJson = operation.ActorRolesJson,
            OccurredAtUtc = operation.CompletedAtUtc.Value,
            Outcome = outcome,
            CorrelationId = operation.OperationKey,
            Reason = operation.Reason,
            BeforeJson = JsonSerializer.Serialize(new { folderId = operation.SourceFolderId }),
            AfterJson = JsonSerializer.Serialize(new { folderId = outcome == "succeeded" ? operation.DestinationFolderId : operation.SourceFolderId }),
            PolicyVersion = MailLogicalFolderPolicy.Key + "/v" + MailLogicalFolderPolicy.Version
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private static RetainedMailFolderMoveCoordinates Coordinates(RetainedMailFolderMoveEntity operation) =>
        new(operation.MailboxId, operation.SourceFolderId, operation.ImmutableMessageId, operation.DestinationFolderId);

    private static RetainedMailFolderMoveResult Map(RetainedMailFolderMoveEntity operation, bool replay) =>
        new(
            operation.Outcome switch
            {
                "succeeded" => RetainedMailFolderMoveOutcome.Succeeded,
                "failed" => RetainedMailFolderMoveOutcome.Failed,
                _ => RetainedMailFolderMoveOutcome.Uncertain
            },
            Enum.Parse<MailLogicalFolderType>(operation.FolderType),
            operation.Reason,
            operation.CompletedAtUtc ?? operation.RecordedAtUtc,
            replay,
            operation.OperationKey,
            operation.FailureReason,
            operation.ExpectedClassificationVersion,
            operation.ExpectedRecommendationPolicyKey,
            operation.ExpectedRecommendationPolicyVersion,
            operation.ExpectedMailboxVersion);

    private static string Hash(MoveRetainedMailFolderRequest request, ActionActor actor)
    {
        var value = string.Join('|', request.MessageId, request.ExpectedClassificationVersion,
            request.ExpectedRecommendationPolicyKey, request.ExpectedRecommendationPolicyVersion,
            request.ExpectedMailboxVersion, request.Reason, actor.Kind, actor.SubjectId);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
