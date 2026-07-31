using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfBoxFileRequestStore(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    TimeProvider timeProvider) : ICreateBoxFileRequest, IRevokeBoxFileRequest
{
    public async Task<CreateBoxFileRequestResult> ExecuteAsync(
        CreateBoxFileRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateActorAndOperation(command.Actor, command.OperationKey);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var history = await FindHistoryAsync(
            context,
            command.OperationKey,
            cancellationToken);
        var replay = await context.Set<BoxFileRequestEntity>()
            .SingleOrDefaultAsync(
                value => value.CaseId == command.CaseId
                    && value.CreateOperationKey == command.OperationKey,
                cancellationToken);
        if (replay is not null)
        {
            if (replay.ExpiresAtUtc != command.ExpiresAtUtc)
            {
                throw new InvalidOperationException("The file-request operation key was reused with different expiry.");
            }
            if (history is null)
            {
                throw new InvalidDataException(
                    "The replayed Box file-request creation is missing its action history.");
            }
            DocumentActionHistory.RequireExactReplay(
                history,
                "box_file_request",
                replay.Id.ToString("D"),
                "box_file_request_created",
                command.Actor,
                reason: null,
                afterJson: DocumentActionHistory.Serialize(HistoryValue(replay)));

            return new(ToFileRequest(replay), null, true);
        }
        if (history is not null)
        {
            throw new InvalidOperationException(
                "The Box file-request operation key was already used for another audited action.");
        }
        var workflow = await RequireWorkflowAsync(context, command.CaseId, cancellationToken);
        CaseMutationGuard.Require(
            workflow,
            command.Actor,
            command.ExpectedCaseVersion,
            command.EditLeaseToken,
            timeProvider.GetUtcNow());

        var token = RequestUploadPolicy.CreateToken();
        var entity = new BoxFileRequestEntity
        {
            Id = Guid.NewGuid(),
            CaseId = command.CaseId,
            Status = BoxFileRequestStatus.Active,
            CreatedAtUtc = timeProvider.GetUtcNow(),
            ExpiresAtUtc = command.ExpiresAtUtc,
            Version = 1,
            CreateOperationKey = command.OperationKey,
            LinkTokenDigest = token.TokenDigest
        };
        context.Add(entity);
        context.ActionHistory.Add(DocumentActionHistory.Succeeded(
            "box_file_request",
            entity.Id.ToString("D"),
            "box_file_request_created",
            command.Actor,
            entity.CreatedAtUtc,
            command.OperationKey,
            afterJson: DocumentActionHistory.Serialize(HistoryValue(entity))));
        CaseMutationGuard.Complete(workflow);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var url = $"/Requests/Box/{entity.Id:N}?token={Uri.EscapeDataString(token.Secret.Token)}";
        return new(ToFileRequest(entity), new(url), false);
    }

    public async Task<BoxFileRequest> ExecuteAsync(
        RevokeBoxFileRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateActorAndOperation(command.Actor, command.OperationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Reason);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var history = await FindHistoryAsync(
            context,
            command.OperationKey,
            cancellationToken);
        var entity = await context.Set<BoxFileRequestEntity>()
            .SingleOrDefaultAsync(
                value => value.Id == command.FileRequestId && value.CaseId == command.CaseId,
                cancellationToken)
            ?? throw new InvalidOperationException("The file request is unavailable.");
        if (entity.RevokeOperationKey is not null)
        {
            if (!string.Equals(entity.RevokeOperationKey, command.OperationKey, StringComparison.Ordinal))
            {
                throw new DbUpdateConcurrencyException("The file request has already changed.");
            }
            if (history is null)
            {
                throw new InvalidDataException(
                    "The replayed Box file-request revocation is missing its action history.");
            }
            DocumentActionHistory.RequireExactReplay(
                history,
                "box_file_request",
                entity.Id.ToString("D"),
                "box_file_request_revoked",
                command.Actor,
                command.Reason.Trim(),
                DocumentActionHistory.Serialize(HistoryValue(entity)));

            return ToFileRequest(entity);
        }
        if (history is not null)
        {
            throw new InvalidOperationException(
                "The Box file-request operation key was already used for another audited action.");
        }

        var workflow = await RequireWorkflowAsync(context, command.CaseId, cancellationToken);
        CaseMutationGuard.Require(
            workflow,
            command.Actor,
            command.ExpectedCaseVersion,
            command.EditLeaseToken,
            timeProvider.GetUtcNow());
        if (entity.Version != command.ExpectedFileRequestVersion)
        {
            throw new DbUpdateConcurrencyException("The file request version is stale.");
        }

        var beforeJson = DocumentActionHistory.Serialize(HistoryValue(entity));
        entity.Status = BoxFileRequestStatus.Deactivated;
        entity.DeactivatedAtUtc = timeProvider.GetUtcNow();
        entity.RevokeOperationKey = command.OperationKey;
        entity.Version = checked(entity.Version + 1);
        context.ActionHistory.Add(DocumentActionHistory.Succeeded(
            "box_file_request",
            entity.Id.ToString("D"),
            "box_file_request_revoked",
            command.Actor,
            entity.DeactivatedAtUtc.Value,
            command.OperationKey,
            command.Reason.Trim(),
            beforeJson,
            DocumentActionHistory.Serialize(HistoryValue(entity))));
        CaseMutationGuard.Complete(workflow);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToFileRequest(entity);
    }

    private static void ValidateActorAndOperation(ActionActor actor, string operationKey)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
    }

    private static async Task<CaseWorkflowEntity> RequireWorkflowAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken) =>
        await context.CaseWorkflows.SingleOrDefaultAsync(
            value => value.CaseId == caseId,
            cancellationToken)
        ?? throw new InvalidOperationException("The case is unavailable.");

    private static Task<ActionHistoryEntity?> FindHistoryAsync(
        PegasusDbContext context,
        string operationKey,
        CancellationToken cancellationToken) =>
        context.ActionHistory.SingleOrDefaultAsync(
            value => value.AggregateType == "box_file_request"
                && value.CorrelationId == operationKey,
            cancellationToken);

    private static BoxFileRequestHistoryValue HistoryValue(BoxFileRequestEntity value) => new(
        value.Id,
        value.CaseId,
        value.Status.ToString(),
        value.CreatedAtUtc,
        value.ExpiresAtUtc,
        value.DeactivatedAtUtc,
        value.Version);

    private sealed record BoxFileRequestHistoryValue(
        Guid FileRequestId,
        Guid CaseId,
        string Status,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? ExpiresAtUtc,
        DateTimeOffset? DeactivatedAtUtc,
        long Version);

    private static BoxFileRequest ToFileRequest(BoxFileRequestEntity value) => new(
        value.Id,
        value.CaseId,
        value.Status,
        value.CreatedAtUtc,
        value.ExpiresAtUtc,
        value.DeactivatedAtUtc,
        value.Version);
}
