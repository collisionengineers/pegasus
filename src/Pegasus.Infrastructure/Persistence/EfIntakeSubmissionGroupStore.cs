using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfIntakeSubmissionGroupStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : IIntakeSubmissionGroupStore
{
    public async Task<IReadOnlyList<Guid>> ListPendingImageGroupReceiptsAsync(
        int maximumItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var emptyFields = EfIntakeReceiptStore.SerializeFields([]);
        var groupOrigin = nameof(UnidentifiedOriginKind.SubmissionGroup);
        var eligible = from submissionGroup in context.IntakeSubmissionGroups
                       join member in context.IntakeSubmissionGroupMembers on submissionGroup.Id equals member.GroupId
                       join work in context.IntakeWorkItems on member.StagedReceiptId equals work.StagedReceiptId
                       join receipt in context.IntakeReceipts on work.ProcessedReceiptId equals (Guid?)receipt.Id
                       where submissionGroup.DiscardedAtUtc == null
                           && submissionGroup.ExpectedMemberCount > 1 && work.State == "completed"
                           && receipt.Decision == "needs_sorting"
                           && receipt.InstructionDraft == null && receipt.FieldsJson == emptyFields
                           && receipt.Assets.Any()
                           && !receipt.Assets.Any(asset => !EF.Functions.Like(
                               asset.MediaType, ImageIntakeLifecycleRules.ImageMediaTypePrefix + "%"))
                           && !context.UnidentifiedItems.Any(item => item.OriginKind == groupOrigin && item.OriginId == submissionGroup.Id)
                       select new { submissionGroup.Id, submissionGroup.ReceivedAtUtc, ReceiptId = receipt.Id, member.Ordinal };
        return await eligible.GroupBy(item => new { item.Id, item.ReceivedAtUtc })
            .OrderBy(group => group.Key.ReceivedAtUtc)
            .ThenBy(group => group.Key.Id)
            .Select(group => group.OrderBy(item => item.Ordinal).Select(item => item.ReceiptId).First())
            .Take(maximumItems)
            .ToListAsync(cancellationToken);
    }

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
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var group = await context.IntakeSubmissionGroups.SingleOrDefaultAsync(
            item => item.Id == groupId,
            cancellationToken)
            ?? throw new InvalidDataException("The submission group was not found.");
        if (group.DiscardedAtUtc is not null)
        {
            throw new InvalidOperationException("The discarded submission cannot accept another file.");
        }
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

            await transaction.CommitAsync(cancellationToken);
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
        group.Version++;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(groupId, ordinal, staged.Id, staged.SourceFileName, staged.SourceHash, received.IsDuplicate);
    }

    public async Task<DiscardIntakeSubmissionGroupResult> DiscardAsync(
        DiscardIntakeSubmissionGroupRequest request,
        DateTimeOffset discardedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var operationKey = $"upload-discard:{request.OperationId:N}";
        var fingerprint = DiscardFingerprint(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var group = await context.IntakeSubmissionGroups
            .Include(item => item.Members)
            .SingleOrDefaultAsync(item => item.Id == request.GroupId, cancellationToken);
        if (group is null)
        {
            return DiscardIntakeSubmissionGroupResult.Conflict("This submission is no longer available.");
        }
        if (group.DiscardedAtUtc is not null)
        {
            var replay = string.Equals(group.DiscardOperationKey, operationKey, StringComparison.Ordinal)
                && group.DiscardRequestFingerprint is not null
                && CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(group.DiscardRequestFingerprint),
                    Encoding.UTF8.GetBytes(fingerprint));
            return replay
                ? new(true, true, "This submission was already discarded.")
                : DiscardIntakeSubmissionGroupResult.Conflict(
                    "This submission already has a different terminal decision.");
        }
        if (group.SourceChannel != ToCode(IntakeSourceChannel.ManualUpload))
        {
            return DiscardIntakeSubmissionGroupResult.Conflict("Only manual uploads can be discarded here.");
        }
        if (group.Version != request.ExpectedGroupVersion)
        {
            return DiscardIntakeSubmissionGroupResult.Conflict(
                "This submission changed after it was reviewed. Refresh and try again.");
        }

        var members = group.Members.OrderBy(item => item.Ordinal).ToArray();
        if (members.Length != group.ExpectedMemberCount
            || members.Select((member, ordinal) => member.Ordinal != ordinal).Any(value => value))
        {
            return DiscardIntakeSubmissionGroupResult.Conflict(
                "This submission is incomplete. Wait for every file to finish.");
        }

        var stagedReceiptIds = members.Select(item => item.StagedReceiptId).ToArray();
        var workItems = await context.IntakeWorkItems
            .Where(item => stagedReceiptIds.Contains(item.StagedReceiptId))
            .ToArrayAsync(cancellationToken);
        if (workItems.Length != stagedReceiptIds.Length
            || workItems.Any(item => item.State != "completed" || item.ProcessedReceiptId is null))
        {
            return DiscardIntakeSubmissionGroupResult.Conflict(
                "This submission is still being processed. Wait for every file to finish.");
        }

        var receiptIds = workItems.Select(item => item.ProcessedReceiptId!.Value).Order().ToArray();
        if (receiptIds.Distinct().Count() != receiptIds.Length
            || request.ExpectedReceiptVersions.Count != receiptIds.Length)
        {
            return DiscardIntakeSubmissionGroupResult.Conflict(
                "This submission changed after it was reviewed. Refresh and try again.");
        }
        var receipts = await context.IntakeReceipts
            .Where(item => receiptIds.Contains(item.Id))
            .ToArrayAsync(cancellationToken);
        if (receipts.Length != receiptIds.Length
            || receipts.Any(item => !request.ExpectedReceiptVersions.TryGetValue(item.Id, out var version)
                || item.Version != version))
        {
            return DiscardIntakeSubmissionGroupResult.Conflict(
                "A file in this submission changed after it was reviewed. Refresh and try again.");
        }
        if (await context.IntakeManualAssociations.AnyAsync(
                item => receiptIds.Contains(item.IntakeReceiptId) && item.IsActive,
                cancellationToken)
            || await context.CaseIntakeLinks.AnyAsync(
                item => receiptIds.Contains(item.IntakeReceiptId), cancellationToken)
            || await context.IntakeAllocationAttempts.AnyAsync(
                item => receiptIds.Contains(item.IntakeReceiptId) && item.Status == "succeeded",
                cancellationToken)
            || await context.ImageIntakes.AnyAsync(
                item => item.SubmissionGroupId == group.Id || receiptIds.Contains(item.OriginReceiptId),
                cancellationToken))
        {
            return DiscardIntakeSubmissionGroupResult.Conflict(
                "This submission is already associated, allocated, or registered and cannot be discarded.");
        }

        var beforeVersion = group.Version;
        group.Version++;
        group.DiscardedAtUtc = discardedAtUtc;
        group.DiscardedByActorKind = request.Actor.Kind.ToString();
        group.DiscardedByActorSubjectId = request.Actor.SubjectId;
        group.DiscardOperationKey = operationKey;
        group.DiscardRequestFingerprint = fingerprint;
        context.IntakeSubmissionGroupHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            Group = group,
            EventType = "submission_discarded",
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(request.Actor.Roles.OrderBy(role => role)),
            OperationKey = operationKey,
            RequestFingerprint = fingerprint,
            OccurredAtUtc = discardedAtUtc,
            BeforeVersion = beforeVersion,
            AfterVersion = group.Version
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, false, "This submission was discarded. Its retained source and processing record remain available.");
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
            entity.ParentReceiptId,
            entity.Version,
            entity.DiscardedAtUtc is null ? null : new(
                entity.DiscardedByActorKind!,
                entity.DiscardedByActorSubjectId!,
                entity.DiscardedAtUtc.Value,
                entity.DiscardOperationKey!,
                entity.DiscardRequestFingerprint!));
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

    private static string DiscardFingerprint(DiscardIntakeSubmissionGroupRequest request)
    {
        var material = string.Join('|',
            request.GroupId.ToString("N"),
            request.ExpectedGroupVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            request.OperationId.ToString("N"),
            request.Actor.Kind.ToString(),
            request.Actor.SubjectId,
            string.Join(',', request.ExpectedReceiptVersions
                .OrderBy(item => item.Key)
                .Select(item => $"{item.Key:N}:{item.Value}")));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }

}
