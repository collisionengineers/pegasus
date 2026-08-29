using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfIntakeSubmissionGroupStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : IIntakeSubmissionGroupStore
{
    public async Task<IntakeSubmissionGroup?> GetAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.IntakeSubmissionGroups
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == groupId, cancellationToken);
        return entity is null ? null : await MapAsync(context, entity, cancellationToken);
    }

    public async Task<IntakeSubmissionGroup?> FindAsync(
        IntakeSourceChannel channel,
        string submissionToken,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.IntakeSubmissionGroups
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.SourceChannel == ToCode(channel)
                    && item.SubmissionToken == submissionToken,
                cancellationToken);
        return entity is null ? null : await MapAsync(context, entity, cancellationToken);
    }

    public async Task<IntakeSubmissionGroup?> FindForMemberSourceAsync(
        IntakeSourceIdentity sourceIdentity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceIdentity);
        // GroupedIntakeMemberToken owns the token convention; this lookup
        // just tries each parent candidate it names. The bare-token
        // candidate is what lets an ordinal-0 member — which carries the
        // parent token verbatim — find its own group (INTK-012).
        foreach (var parentToken in GroupedIntakeMemberToken.ParentTokenCandidates(
            sourceIdentity.ExternalReceiptToken))
        {
            var group = await FindAsync(sourceIdentity.Channel, parentToken, cancellationToken);
            if (group is not null)
            {
                return group;
            }
        }

        return null;
    }

    public Task<IntakeSubmissionGroup> GetOrCreateAsync(
        Guid groupId,
        IntakeSourceChannel channel,
        string submissionToken,
        int expectedMemberCount,
        string actor,
        DateTimeOffset receivedAtUtc,
        Guid? parentReceiptId,
        CancellationToken cancellationToken = default) =>
        GetOrCreateWithRetryAsync(
            groupId,
            channel,
            submissionToken,
            expectedMemberCount,
            actor,
            receivedAtUtc,
            parentReceiptId,
            cancellationToken);

    // Same concurrent-insert window as AddMemberWithRetryAsync below: two
    // requests replaying the same (channel, token) can both read "no
    // existing group" under Serializable isolation and race the unique
    // (SourceChannel, SubmissionToken) index at commit.
    private async Task<IntakeSubmissionGroup> GetOrCreateWithRetryAsync(
        Guid groupId,
        IntakeSourceChannel channel,
        string submissionToken,
        int expectedMemberCount,
        string actor,
        DateTimeOffset receivedAtUtc,
        Guid? parentReceiptId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                return await GetOrCreateCoreAsync(
                    groupId,
                    channel,
                    submissionToken,
                    expectedMemberCount,
                    actor,
                    receivedAtUtc,
                    parentReceiptId,
                    cancellationToken);
            }
            catch (Exception exception)
                when (attempt < 3 && IsRetryableConcurrencyFailure(exception))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }

        throw new InvalidOperationException(
            "The submission group could not be stored after the concurrency retry limit.");
    }

    private async Task<IntakeSubmissionGroup> GetOrCreateCoreAsync(
        Guid groupId,
        IntakeSourceChannel channel,
        string submissionToken,
        int expectedMemberCount,
        string actor,
        DateTimeOffset receivedAtUtc,
        Guid? parentReceiptId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var existing = await context.IntakeSubmissionGroups
            .SingleOrDefaultAsync(
                item => item.SourceChannel == ToCode(channel)
                    && item.SubmissionToken == submissionToken,
                cancellationToken);
        if (existing is null)
        {
            existing = new()
            {
                Id = groupId,
                SourceChannel = ToCode(channel),
                SubmissionToken = submissionToken,
                ExpectedMemberCount = expectedMemberCount,
                Actor = actor,
                ReceivedAtUtc = receivedAtUtc,
                ParentReceiptId = parentReceiptId
            };
            context.IntakeSubmissionGroups.Add(existing);
            await context.SaveChangesAsync(cancellationToken);
        }
        else if (existing.ExpectedMemberCount != expectedMemberCount
            || existing.ParentReceiptId != parentReceiptId)
        {
            throw new InvalidDataException(
                "The submission token is already bound to different group provenance.");
        }

        await transaction.CommitAsync(cancellationToken);
        return await MapAsync(context, existing, cancellationToken);
    }

    public async Task<IntakeSubmissionGroupMember?> FindMemberAsync(
        Guid groupId,
        int ordinal,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.IntakeSubmissionGroupMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.GroupId == groupId && item.Ordinal == ordinal,
                cancellationToken);
        return entity is null ? null : await MapMemberAsync(context, entity, false, cancellationToken);
    }

    public Task<IntakeSubmissionGroupMember> AddMemberAsync(
        Guid groupId,
        int ordinal,
        ReceivedIntake received,
        CancellationToken cancellationToken = default) =>
        AddMemberWithRetryAsync(groupId, ordinal, received, cancellationToken);

    // Same shape as EfIntakeWorkStore.ReceiveWithRetryAsync: the read-then-
    // insert below can lose a race on the unique (GroupId, Ordinal) index
    // when two requests replay the same submission token concurrently (the
    // same ordinal is deterministic per token, so both can read "no existing
    // member" before either commits). Retrying re-reads first, so the loser
    // of the race sees the winner's row and returns it instead of failing.
    private async Task<IntakeSubmissionGroupMember> AddMemberWithRetryAsync(
        Guid groupId,
        int ordinal,
        ReceivedIntake received,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                return await AddMemberCoreAsync(groupId, ordinal, received, cancellationToken);
            }
            catch (Exception exception)
                when (attempt < 3 && IsRetryableConcurrencyFailure(exception))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }

        throw new InvalidOperationException(
            "The group member could not be stored after the concurrency retry limit.");
    }

    private async Task<IntakeSubmissionGroupMember> AddMemberCoreAsync(
        Guid groupId,
        int ordinal,
        ReceivedIntake received,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var staged = await context.IntakeStagedReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == received.StagedReceiptId, cancellationToken)
            ?? throw new InvalidDataException("The staged receipt for the group member was not found.");
        var existing = await context.IntakeSubmissionGroupMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.GroupId == groupId && item.Ordinal == ordinal,
                cancellationToken);
        if (existing is not null)
        {
            if (existing.StagedReceiptId != received.StagedReceiptId)
            {
                throw new InvalidDataException("The group ordinal is already bound to another receipt.");
            }

            return await MapMemberAsync(context, existing, received.IsDuplicate, cancellationToken);
        }

        var entity = new IntakeSubmissionGroupMemberEntity
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            Ordinal = ordinal,
            StagedReceiptId = staged.Id,
            SourceFileName = staged.SourceFileName,
            SourceHash = staged.SourceHash,
            AddedAtUtc = DateTimeOffset.UtcNow
        };
        context.IntakeSubmissionGroupMembers.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return new(groupId, ordinal, staged.Id, staged.SourceFileName, staged.SourceHash, received.IsDuplicate);
    }

    private static bool IsRetryableConcurrencyFailure(Exception exception) => exception switch
    {
        SqlException { Number: 1205 or 2601 or 2627 } => true,
        _ when exception.InnerException is not null =>
            IsRetryableConcurrencyFailure(exception.InnerException),
        _ => false
    };

    public async Task<IReadOnlyList<IntakeSubmissionGroupMember>> ListMembersAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await context.IntakeSubmissionGroupMembers
            .AsNoTracking()
            .Where(item => item.GroupId == groupId)
            .OrderBy(item => item.Ordinal)
            .ToArrayAsync(cancellationToken);
        var members = new List<IntakeSubmissionGroupMember>(entities.Length);
        foreach (var entity in entities)
        {
            members.Add(await MapMemberAsync(context, entity, false, cancellationToken));
        }

        return members;
    }

    private static async Task<IntakeSubmissionGroup> MapAsync(
        PegasusDbContext context,
        IntakeSubmissionGroupEntity entity,
        CancellationToken cancellationToken)
    {
        var members = await context.IntakeSubmissionGroupMembers
            .AsNoTracking()
            .Where(item => item.GroupId == entity.Id)
            .OrderBy(item => item.Ordinal)
            .ToArrayAsync(cancellationToken);
        var mapped = new List<IntakeSubmissionGroupMember>(members.Length);
        foreach (var member in members)
        {
            mapped.Add(await MapMemberAsync(context, member, false, cancellationToken));
        }

        return new(
            entity.Id,
            ParseChannel(entity.SourceChannel),
            entity.SubmissionToken,
            entity.ExpectedMemberCount,
            entity.Actor,
            entity.ReceivedAtUtc,
            mapped,
            entity.ParentReceiptId);
    }

    private static async Task<IntakeSubmissionGroupMember> MapMemberAsync(
        PegasusDbContext context,
        IntakeSubmissionGroupMemberEntity entity,
        bool isDuplicate,
        CancellationToken cancellationToken)
    {
        var staged = await context.IntakeStagedReceipts
            .AsNoTracking()
            .SingleAsync(item => item.Id == entity.StagedReceiptId, cancellationToken);
        return new(
            entity.GroupId,
            entity.Ordinal,
            entity.StagedReceiptId,
            staged.SourceFileName,
            staged.SourceHash,
            isDuplicate);
    }

    private static string ToCode(IntakeSourceChannel channel) => channel switch
    {
        IntakeSourceChannel.ManualUpload => "manual_upload",
        IntakeSourceChannel.Mailbox => "mailbox",
        IntakeSourceChannel.Automation => "automation",
        IntakeSourceChannel.ProviderApi => "provider_api",
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unsupported source channel.")
    };

    private static IntakeSourceChannel ParseChannel(string channel) => channel switch
    {
        "manual_upload" => IntakeSourceChannel.ManualUpload,
        "mailbox" => IntakeSourceChannel.Mailbox,
        "automation" => IntakeSourceChannel.Automation,
        "provider_api" => IntakeSourceChannel.ProviderApi,
        _ => throw new InvalidDataException($"Unknown intake source channel '{channel}'.")
    };
}
