using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The saved e-mail templates. A save checks the expected version (0 inserts),
/// writes the body and one Administration action-log row in the same
/// transaction, and a replay of the same operation key returns what it saved,
/// as the workflow configuration store does.
/// </summary>
public sealed class EfEmailTemplateStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IEmailTemplateStore
{
    internal const string AggregateType = "email_template";
    internal const string EventKind = "email_template_updated";

    public async Task<EmailTemplate?> GetAsync(
        EmailTemplatePurpose purpose,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<EmailTemplateEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Purpose == purpose.ToString(), cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<EmailTemplate> UpdateAsync(
        UpdateEmailTemplateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ManageEmailTemplates);
        var body = EmailTemplates.Validate(request.Purpose, request.Body);
        var purpose = request.Purpose.ToString();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var replay = await context.ActionHistory
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.AggregateType == AggregateType
                    && item.CorrelationId == request.OperationKey,
                cancellationToken);
        if (replay is not null)
        {
            var replayed = Replay(request, body, replay);
            await transaction.CommitAsync(cancellationToken);
            return replayed;
        }

        var entity = await context.Set<EmailTemplateEntity>()
            .SingleOrDefaultAsync(item => item.Purpose == purpose, cancellationToken);
        if ((entity?.Version ?? 0) != request.ExpectedVersion)
        {
            throw new EmailTemplateVersionConflictException();
        }

        var before = entity is null ? null : Snapshot(entity);
        var now = timeProvider.GetUtcNow();
        if (entity is null)
        {
            entity = new EmailTemplateEntity
            {
                Purpose = purpose,
                Body = body,
                UpdatedBy = request.Actor.SubjectId,
                OperationKey = request.OperationKey
            };
            context.Set<EmailTemplateEntity>().Add(entity);
        }

        entity.Body = body;
        entity.Version = checked(request.ExpectedVersion + 1);
        entity.UpdatedAtUtc = now;
        entity.UpdatedBy = request.Actor.SubjectId;
        entity.OperationKey = request.OperationKey;

        context.ActionHistory.Add(new ActionHistoryEntity
        {
            Id = Guid.NewGuid(),
            AggregateType = AggregateType,
            AggregateId = purpose,
            EventKind = EventKind,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(
                request.Actor.Roles.OrderBy(role => role).Select(role => role.ToString())),
            OccurredAtUtc = now,
            Outcome = "succeeded",
            CorrelationId = request.OperationKey,
            Reason = null,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(Snapshot(entity)),
            PolicyVersion = $"{AggregateType}:{purpose}/v{entity.Version}"
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    private static EmailTemplate Replay(
        UpdateEmailTemplateRequest request,
        string body,
        ActionHistoryEntity history)
    {
        if (history.AggregateId != request.Purpose.ToString()
            || history.EventKind != EventKind
            || history.ActorKind != request.Actor.Kind.ToString()
            || history.ActorSubjectId != request.Actor.SubjectId
            || history.AfterJson is null)
        {
            throw new EmailTemplateOperationConflictException();
        }

        var snapshot = JsonSerializer.Deserialize<EmailTemplateSnapshot>(history.AfterJson)
            ?? throw new EmailTemplateOperationConflictException();
        if (snapshot.Version != checked(request.ExpectedVersion + 1)
            || !string.Equals(snapshot.Body, body, StringComparison.Ordinal))
        {
            throw new EmailTemplateOperationConflictException();
        }

        return new(request.Purpose, snapshot.Body, snapshot.Version, snapshot.UpdatedAtUtc, snapshot.UpdatedBy);
    }

    private static EmailTemplateSnapshot Snapshot(EmailTemplateEntity entity) =>
        new(entity.Purpose, entity.Body, entity.Version, entity.UpdatedAtUtc, entity.UpdatedBy);

    private static EmailTemplate Map(EmailTemplateEntity entity) => new(
        Enum.Parse<EmailTemplatePurpose>(entity.Purpose),
        entity.Body,
        entity.Version,
        entity.UpdatedAtUtc,
        entity.UpdatedBy);

    private sealed record EmailTemplateSnapshot(
        string Purpose,
        string Body,
        long Version,
        DateTimeOffset UpdatedAtUtc,
        string UpdatedBy);
}
