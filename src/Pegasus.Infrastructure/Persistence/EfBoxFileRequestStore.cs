using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;

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
        if (!await context.Set<CaseEntity>().AnyAsync(value => value.Id == command.CaseId, cancellationToken))
        {
            throw new InvalidOperationException("The case is unavailable.");
        }

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

            return new(ToFileRequest(replay), null, true);
        }

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
        await context.SaveChangesAsync(cancellationToken);
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

            return ToFileRequest(entity);
        }

        if (entity.Version != command.ExpectedVersion)
        {
            throw new DbUpdateConcurrencyException("The file request version is stale.");
        }

        entity.Status = BoxFileRequestStatus.Deactivated;
        entity.DeactivatedAtUtc = timeProvider.GetUtcNow();
        entity.RevokeOperationKey = command.OperationKey;
        entity.Version = checked(entity.Version + 1);
        await context.SaveChangesAsync(cancellationToken);
        return ToFileRequest(entity);
    }

    private static void ValidateActorAndOperation(string actor, string operationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
    }

    private static BoxFileRequest ToFileRequest(BoxFileRequestEntity value) => new(
        value.Id,
        value.CaseId,
        value.Status,
        value.CreatedAtUtc,
        value.ExpiresAtUtc,
        value.DeactivatedAtUtc,
        value.Version);
}
