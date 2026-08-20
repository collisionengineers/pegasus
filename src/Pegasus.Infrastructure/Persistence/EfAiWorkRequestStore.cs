using System.Data;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Persists the Send to AI work-request lifecycle. Creation is idempotent
/// per (case, operation key); transitions are optimistic on the record
/// version and validated against the legal state graph; and every change
/// writes permanent attributable action history correlated by the request
/// identifier, so the whole round trip is one activity query.
/// </summary>
public sealed class EfAiWorkRequestStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IAiWorkRequestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string AggregateType = "ai_work_request";

    public async Task<AiWorkRequestRecord> CreateAsync(
        CreateAiWorkRequestCommand command,
        CancellationToken cancellationToken)
    {
        AiWorkPolicy.ValidateCreate(command);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var requestHash = CreateHash(command);
        var existing = await context.AiWorkRequests.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CaseId == command.CaseId && item.OperationKey == command.OperationKey,
                cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            {
                throw new CaseOperationConflictException(command.CaseId, command.OperationKey);
            }

            return Map(existing);
        }

        var now = UtcNow();
        var entity = new AiWorkRequestEntity
        {
            RequestId = Guid.NewGuid(),
            CaseId = command.CaseId,
            CaseReference = command.CaseReference,
            CaseVersionAtSend = command.CaseVersion,
            CapabilityScope = AiWorkPolicy.CapabilityScope,
            Instruction = command.Instruction.Trim(),
            State = nameof(AiWorkRequestState.Created),
            OperationKey = command.OperationKey,
            RequestHash = requestHash,
            CreatedAtUtc = now,
            CreatedBy = command.Actor.SubjectId,
            ExpiresAtUtc = now + command.Expiry,
            Version = 0
        };
        context.AiWorkRequests.Add(entity);
        AddHistory(
            context,
            entity,
            "ai_work_request_created",
            command.Actor,
            reason: "Send to AI hand-off requested.",
            now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<AiWorkRequestRecord?> GetAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("A work-request identifier is required.", nameof(requestId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.AiWorkRequests.AsNoTracking()
            .SingleOrDefaultAsync(item => item.RequestId == requestId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<AiWorkRequestRecord?> GetLatestForCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.AiWorkRequests.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.RequestId)
            .FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<AiWorkRequestRecord> TransitionAsync(
        AiWorkRequestTransition transition,
        CancellationToken cancellationToken)
    {
        AiWorkPolicy.ValidateTransition(transition);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var entity = await context.AiWorkRequests
            .SingleOrDefaultAsync(item => item.RequestId == transition.RequestId, cancellationToken)
            ?? throw new KeyNotFoundException("The Send to AI request was not found.");
        var currentState = ParseState(entity.State);
        if (currentState == transition.TargetState)
        {
            return Map(entity);
        }
        if (!AiWorkPolicy.IsLegalTransition(currentState, transition.TargetState))
        {
            throw new InvalidOperationException(
                $"A Send to AI request cannot move from {currentState} to {transition.TargetState}.");
        }
        if (entity.Version != transition.ExpectedVersion)
        {
            throw new InvalidOperationException(
                "The Send to AI request changed concurrently; reload and retry.");
        }

        var now = UtcNow();
        entity.State = transition.TargetState.ToString();
        entity.Version++;
        if (transition.TargetState == AiWorkRequestState.HandedOff)
        {
            entity.HandedOffAtUtc = now;
        }
        else
        {
            entity.ClosedAtUtc = now;
            entity.ClosureReason = transition.Reason;
            entity.ReplyStatus = transition.ReplyStatus;
            entity.ReplyMessage = transition.ReplyMessage;
        }

        AddHistory(
            context,
            entity,
            "ai_work_request_" + transition.TargetState.ToString().ToLowerInvariant(),
            transition.Actor,
            transition.Reason,
            now);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "The Send to AI request changed concurrently; reload and retry.");
        }

        return Map(entity);
    }

    private static void AddHistory(
        PegasusDbContext context,
        AiWorkRequestEntity entity,
        string eventKind,
        ActionActor actor,
        string? reason,
        DateTimeOffset occurredAtUtc) =>
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = AggregateType,
            AggregateId = entity.RequestId.ToString("D"),
            EventKind = eventKind,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(
                actor.Roles.OrderBy(role => role),
                JsonOptions),
            OccurredAtUtc = occurredAtUtc,
            Outcome = "Succeeded",
            CorrelationId = entity.RequestId.ToString("D"),
            Reason = reason,
            AfterJson = JsonSerializer.Serialize(
                new
                {
                    entity.CaseId,
                    entity.CaseReference,
                    entity.State,
                    entity.CapabilityScope,
                    entity.ReplyStatus
                },
                JsonOptions)
        });

    private static AiWorkRequestRecord Map(AiWorkRequestEntity entity) => new(
        entity.RequestId,
        entity.CaseId,
        entity.CaseReference,
        entity.CaseVersionAtSend,
        entity.CapabilityScope,
        entity.Instruction,
        ParseState(entity.State),
        entity.CreatedAtUtc,
        entity.CreatedBy,
        entity.ExpiresAtUtc,
        entity.HandedOffAtUtc,
        entity.ClosedAtUtc,
        entity.ClosureReason,
        entity.ReplyStatus,
        entity.ReplyMessage,
        entity.Version);

    private static AiWorkRequestState ParseState(string value) =>
        Enum.TryParse<AiWorkRequestState>(value, out var state)
            ? state
            : throw new InvalidDataException($"Unknown persisted work-request state '{value}'.");

    private DateTimeOffset UtcNow()
    {
        var now = timeProvider.GetUtcNow();
        return now.Offset == TimeSpan.Zero ? now : now.ToUniversalTime();
    }

    private static string CreateHash(CreateAiWorkRequestCommand command)
    {
        var material = JsonSerializer.Serialize(new
        {
            Command = "create_ai_work_request",
            command.CaseId,
            command.CaseReference,
            command.CaseVersion,
            ActorKind = command.Actor.Kind.ToString(),
            command.Actor.SubjectId,
            command.OperationKey,
            Instruction = command.Instruction.Trim(),
            Expiry = command.Expiry.Ticks
        }, JsonOptions);
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }
}

/// <summary>
/// The Administrator-held Send to AI switch. Absent row means enabled: the
/// composition gate is the master switch, and this control is the immediate
/// operational cut point beneath it.
/// </summary>
public sealed class EfSendToAiControlStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : ISendToAiControl
{
    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var control = await context.SendToAiControl.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == SendToAiControlEntity.SingletonId,
                cancellationToken);
        return control?.Enabled ?? true;
    }

    public async Task<bool> SetEnabledAsync(
        bool enabled,
        ActionActor actor,
        string reason,
        string operationKey,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var control = await context.SendToAiControl
            .SingleOrDefaultAsync(
                item => item.Id == SendToAiControlEntity.SingletonId,
                cancellationToken);
        var previous = control?.Enabled ?? true;
        if (control is null)
        {
            control = new() { Id = SendToAiControlEntity.SingletonId, Enabled = enabled, Version = 0 };
            context.SendToAiControl.Add(control);
        }
        else
        {
            control.Enabled = enabled;
            control.Version++;
        }

        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = "send_to_ai",
            AggregateId = SendToAiControlEntity.SingletonId,
            EventKind = enabled ? "send_to_ai_enabled" : "send_to_ai_disabled",
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = System.Text.Json.JsonSerializer.Serialize(
                actor.Roles.OrderBy(role => role)),
            OccurredAtUtc = timeProvider.GetUtcNow(),
            Outcome = previous == enabled ? "Unchanged" : "Succeeded",
            CorrelationId = operationKey.Trim(),
            Reason = reason.Trim()
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return enabled;
    }
}

/// <summary>
/// Administration-held Send to AI connector settings on the same singleton
/// row as the switch. The token is protected with the application's data
/// protection ring at entry and unprotected only for the outbound transport;
/// Administration reads only whether one is held and when it last changed.
/// Every update writes attributed permanent history recording that a value
/// changed, never the value.
/// </summary>
public sealed class EfAiChannelConnectorStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    IDataProtectionProvider dataProtection,
    TimeProvider timeProvider) : IAiChannelConnectorStore
{
    private const string ProtectionPurpose = "Pegasus.SendToAi.ChannelToken";

    public async Task<AiChannelConnectorSettings> GetAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var control = await ReadAsync(context, tracking: false, cancellationToken);
        return Map(control);
    }

    public async Task<AiChannelConnectorRuntime> GetRuntimeAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var control = await ReadAsync(context, tracking: false, cancellationToken);
        string? token = null;
        if (control?.ChannelTokenProtected is { } protectedToken)
        {
            try
            {
                token = dataProtection.CreateProtector(ProtectionPurpose)
                    .Unprotect(protectedToken);
            }
            catch (CryptographicException)
            {
                throw new InvalidOperationException(
                    "The stored channel token could not be read. Rotate it from Administration.");
            }
        }

        return new(
            control?.ChannelBaseUrl is { } baseUrl ? new Uri(baseUrl, UriKind.Absolute) : null,
            control?.TimeoutSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null,
            token);
    }

    public Task<AiChannelConnectorSettings> UpdateAsync(
        UpdateAiChannelConnectorCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ChannelBaseUrl is { } baseUrl
            && !AiChannelConnectorRules.TryParseBaseUrl(baseUrl, out _))
        {
            throw new ArgumentException(
                "The channel address must be a loopback http origin without path or query.",
                nameof(command));
        }
        if (command.TimeoutSeconds is { } seconds
            && !AiChannelConnectorRules.IsValidTimeoutSeconds(seconds))
        {
            throw new ArgumentException(
                "The timeout must be between 1 and 60 seconds.",
                nameof(command));
        }

        return WriteAsync(
            command.Actor,
            command.Reason,
            command.OperationKey,
            "send_to_ai_connector_updated",
            control =>
            {
                control.ChannelBaseUrl = command.ChannelBaseUrl;
                control.TimeoutSeconds = command.TimeoutSeconds;
            },
            cancellationToken);
    }

    public Task<AiChannelConnectorSettings> RotateTokenAsync(
        RotateAiChannelTokenCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.NewToken is not null
            && !AiChannelConnectorRules.IsValidToken(command.NewToken))
        {
            throw new ArgumentException(
                "The channel token must be at least 32 characters.",
                nameof(command));
        }

        var protectedToken = command.NewToken is { } newToken
            ? dataProtection.CreateProtector(ProtectionPurpose).Protect(newToken)
            : null;
        return WriteAsync(
            command.Actor,
            command.Reason,
            command.OperationKey,
            protectedToken is null
                ? "send_to_ai_channel_token_cleared"
                : "send_to_ai_channel_token_rotated",
            control =>
            {
                control.ChannelTokenProtected = protectedToken;
                control.TokenRotatedAtUtc = protectedToken is null
                    ? null
                    : timeProvider.GetUtcNow();
            },
            cancellationToken);
    }

    private async Task<AiChannelConnectorSettings> WriteAsync(
        ActionActor actor,
        string reason,
        string operationKey,
        string eventKind,
        Action<SendToAiControlEntity> apply,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var control = await ReadAsync(context, tracking: true, cancellationToken);
        if (control is null)
        {
            control = new()
            {
                Id = SendToAiControlEntity.SingletonId,
                Enabled = true,
                Version = 0
            };
            context.SendToAiControl.Add(control);
        }
        else
        {
            control.Version++;
        }

        apply(control);
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = "send_to_ai",
            AggregateId = SendToAiControlEntity.SingletonId,
            EventKind = eventKind,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(actor.Roles.OrderBy(role => role)),
            OccurredAtUtc = timeProvider.GetUtcNow(),
            Outcome = "Succeeded",
            CorrelationId = operationKey.Trim(),
            Reason = reason.Trim()
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(control);
    }

    private static Task<SendToAiControlEntity?> ReadAsync(
        PegasusDbContext context,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var query = tracking
            ? context.SendToAiControl
            : context.SendToAiControl.AsNoTracking();
        return query.SingleOrDefaultAsync(
            item => item.Id == SendToAiControlEntity.SingletonId,
            cancellationToken);
    }

    private static AiChannelConnectorSettings Map(SendToAiControlEntity? control) => new(
        control?.ChannelBaseUrl,
        control?.TimeoutSeconds,
        control?.ChannelTokenProtected is not null,
        control?.TokenRotatedAtUtc,
        control?.Version ?? 0);
}
