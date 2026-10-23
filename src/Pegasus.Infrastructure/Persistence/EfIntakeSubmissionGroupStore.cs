using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfIntakeSubmissionGroupStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider? timeProvider = null) : IIntakeSubmissionGroupStore
{
    public async Task<IReadOnlyList<Guid>> ListPendingImageGroupReceiptsAsync(
        int maximumItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // Unpromoted Case filings first. A backlog of unresolved image groups
        // used to fill maximumItems and drop this recovery entirely.
        var results = new List<Guid>();
        await AppendUnpromotedCaseReceiptsAsync(context, results, maximumItems, cancellationToken);
        if (results.Count >= maximumItems)
        {
            return results;
        }

        var emptyFields = EfIntakeReceiptStore.SerializeFields([]);
        var groupOrigin = nameof(UnidentifiedOriginKind.SubmissionGroup);
        var receiptOrigin = nameof(UnidentifiedOriginKind.Receipt);
        var manualChannel = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.ManualUpload);
        var eligible = from submissionGroup in context.IntakeSubmissionGroups
                       join member in context.IntakeSubmissionGroupMembers on submissionGroup.Id equals member.GroupId
                       join work in context.IntakeWorkItems on member.StagedReceiptId equals work.StagedReceiptId
                       join receipt in context.IntakeReceipts on work.ProcessedReceiptId equals (Guid?)receipt.Id
                       where submissionGroup.DiscardedAtUtc == null
                           && (submissionGroup.ExpectedMemberCount > 1 || submissionGroup.SourceChannel == manualChannel)
                           && work.State == "completed"
                           && receipt.Decision == "needs_sorting"
                           && receipt.InstructionDraft == null && receipt.FieldsJson == emptyFields
                           && !context.UnidentifiedItems.Any(item => item.OriginKind == groupOrigin && item.OriginId == submissionGroup.Id)
                           && !context.UnidentifiedItems.Any(item => item.OriginKind == receiptOrigin && item.OriginId == receipt.Id)
                           && !context.IntakeManualAssociations.Any(association => association.IntakeReceiptId == receipt.Id && association.IsActive)
                           && !context.CaseIntakeLinks.Any(link => link.IntakeReceiptId == receipt.Id)
                       select new { submissionGroup.Id, submissionGroup.ReceivedAtUtc, ReceiptId = receipt.Id, member.Ordinal };
        // Eligibility is Core's rule over the whole receipt, so it runs in
        // memory, but only over one ordered page of rows at a time and only
        // until maximumItems is reached. A group's receipt is its lowest
        // ordinal eligible member; the group is spent whether or not that
        // receipt was already listed.
        // Each page seeks past the last row of the one before on the unique
        // (ReceivedAtUtc, group Id, Ordinal) order, so rows leaving the set
        // between pages cannot shift a page. The receipts of one page load in
        // a single read.
        Guid? spentGroupId = null;
        var hasLast = false;
        var lastReceivedAtUtc = default(DateTimeOffset);
        var lastGroupId = Guid.Empty;
        var lastOrdinal = 0;
        while (true)
        {
            var remaining = hasLast
                ? eligible.Where(candidate => candidate.ReceivedAtUtc > lastReceivedAtUtc
                    || (candidate.ReceivedAtUtc == lastReceivedAtUtc
                        && (candidate.Id.CompareTo(lastGroupId) > 0
                            || (candidate.Id == lastGroupId && candidate.Ordinal > lastOrdinal))))
                : eligible;
            var page = await remaining
                .OrderBy(candidate => candidate.ReceivedAtUtc)
                .ThenBy(candidate => candidate.Id)
                .ThenBy(candidate => candidate.Ordinal)
                .Take(maximumItems)
                .ToArrayAsync(cancellationToken);
            // A group is contiguous in this order. Rows of a group already
            // spent on an earlier page are not read at all.
            var groupsOnPage = page
                .Where(candidate => candidate.Id != spentGroupId)
                .GroupBy(candidate => candidate.Id)
                .Select(rows => rows.ToArray())
                .ToArray();
            var receipts = new Dictionary<Guid, IntakeReceiptEntity>();
            bool IsEligible(Guid receiptId) =>
                receipts.TryGetValue(receiptId, out var receipt)
                && ImageIntakeLifecycleRules.IsImageAutomationEligible(
                    EfIntakeReceiptStore.Map(receipt, isDuplicate: false));

            await LoadReceiptsAsync(
                context,
                receipts,
                groupsOnPage.SelectMany(rows => rows).Select(candidate => candidate.ReceiptId),
                cancellationToken);
            foreach (var rows in groupsOnPage)
            {
                var chosen = rows.FirstOrDefault(candidate => IsEligible(candidate.ReceiptId));
                if (chosen is null)
                {
                    continue;
                }

                spentGroupId = chosen.Id;
                if (!results.Contains(chosen.ReceiptId))
                {
                    results.Add(chosen.ReceiptId);
                    if (results.Count >= maximumItems)
                    {
                        return results;
                    }
                }
            }

            if (page.Length < maximumItems)
            {
                return results;
            }

            var last = page[^1];
            hasLast = true;
            lastReceivedAtUtc = last.ReceivedAtUtc;
            lastGroupId = last.Id;
            lastOrdinal = last.Ordinal;
        }
    }

    private static async Task LoadReceiptsAsync(
        PegasusDbContext context,
        Dictionary<Guid, IntakeReceiptEntity> receipts,
        IEnumerable<Guid> receiptIds,
        CancellationToken cancellationToken)
    {
        var ids = receiptIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        foreach (var receipt in await context.IntakeReceipts.AsNoTracking()
                     .Include(receipt => receipt.Assets)
                     .Include(receipt => receipt.MailClassificationDecision)
                     .Include(receipt => receipt.MailRouteDecision)
                     .AsSplitQuery()
                     .Where(receipt => ids.Contains(receipt.Id))
                     .ToArrayAsync(cancellationToken))
        {
            receipts[receipt.Id] = receipt;
        }
    }

    /// <summary>
    /// Completed Case-associated receipts whose automatic filing has not
    /// recorded its occurrences, oldest association first. A set whose every
    /// planned occurrence exists and at least one is still Pending is left to
    /// <c>ReconcilePendingArtifactCustody</c>; a Failed set is not selected
    /// there, so it stays listed for replay.
    /// </summary>
    private async Task AppendUnpromotedCaseReceiptsAsync(
        PegasusDbContext context,
        List<Guid> results,
        int maximumItems,
        CancellationToken cancellationToken)
    {
        var unpromotedCandidates = await (
            from association in context.IntakeManualAssociations.AsNoTracking()
            join work in context.IntakeWorkItems.AsNoTracking()
                on (Guid?)association.IntakeReceiptId equals work.ProcessedReceiptId
            join receipt in context.IntakeReceipts.AsNoTracking()
                on association.IntakeReceiptId equals receipt.Id
            where association.IsActive
                && work.State == "completed"
                && receipt.Decision != "image_intake_registered"
                && !context.Cases.Any(caseRow => caseRow.Id == association.CaseId
                    && caseRow.OriginIntakeReceiptId == receipt.Id)
                && context.IntakeAssets.Any(asset => asset.IntakeReceiptId == receipt.Id
                    && asset.Kind == "source"
                    && asset.Disposition == "source"
                    && asset.ContentLength > 0
                    && asset.StorageKey != "")
                && !context.IntakeSubmissionGroups.Any(submissionGroup => submissionGroup.DiscardedAtUtc != null
                    && context.IntakeSubmissionGroupMembers.Any(member => member.GroupId == submissionGroup.Id
                        && member.StagedReceiptId == work.StagedReceiptId))
                && !context.IntakeMutationHistory.Any(history => history.IntakeReceiptId == receipt.Id
                    && history.CaseId == association.CaseId
                    && history.EventType == "intake_case_evidence_promotion_completed")
            orderby association.LinkedAtUtc, receipt.Id
            select new { ReceiptId = receipt.Id, association.CaseId })
            .ToArrayAsync(cancellationToken);
        var nowUtc = (timeProvider ?? TimeProvider.System).GetUtcNow();
        foreach (var candidate in unpromotedCandidates)
        {
            if (results.Contains(candidate.ReceiptId))
            {
                continue;
            }

            var target = await IntakePromotionTarget.EvaluateAsync(
                context,
                candidate.CaseId,
                candidate.ReceiptId,
                nowUtc,
                cancellationToken);
            if (target.Disposition != PromotionTargetDisposition.Current)
            {
                continue;
            }

            var planKey = AutomaticCaseEvidencePromotionOperationKey.Plan(candidate.ReceiptId);
            var plan = await context.IntakeMutationHistory.AsNoTracking()
                .SingleOrDefaultAsync(item => item.OperationKey == planKey, cancellationToken);
            if (plan?.CaseId == candidate.CaseId && !string.IsNullOrWhiteSpace(plan.AfterJson))
            {
                var promotionPlan = JsonSerializer.Deserialize<AutomaticCaseEvidencePromotionPlan>(plan.AfterJson);
                var assetIds = promotionPlan?.AssetIds?.ToArray() ?? [];
                if (assetIds.Length > 0)
                {
                    var keys = assetIds.Select(assetId =>
                        AutomaticCaseEvidencePromotionOperationKey.For(
                            candidate.CaseId,
                            candidate.ReceiptId,
                            assetId)).ToArray();
                    var statuses = await (
                        from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                        join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                            on occurrence.VersionId equals version.Id
                        where occurrence.CaseId == candidate.CaseId
                            && keys.Contains(occurrence.OperationKey)
                            && !version.IsLogicallyRemoved
                        select new { occurrence.OperationKey, version.CustodyStatus })
                        .ToArrayAsync(cancellationToken);
                    if (statuses.Select(item => item.OperationKey).Distinct().Count() == keys.Length
                        && statuses.Any(item => item.CustodyStatus == Pegasus.Core.Documents.DocumentCustodyStatus.Pending))
                    {
                        // Every planned occurrence exists and at least one is still
                        // Pending, which ReconcilePendingArtifactCustody selects.
                        // A Failed set is not selected there, so it stays listed
                        // for replay.
                        continue;
                    }
                }
            }

            results.Add(candidate.ReceiptId);
            if (results.Count >= maximumItems)
            {
                break;
            }
        }
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
                item => item.SourceChannel == EfIntakeReceiptStore.ToCode(channel)
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
        // parent token verbatim — find its own group.
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
    // existing group" and race the unique (SourceChannel, SubmissionToken)
    // index at commit.
    private Task<IntakeSubmissionGroup> GetOrCreateWithRetryAsync(
        Guid groupId,
        IntakeSourceChannel channel,
        string submissionToken,
        int expectedMemberCount,
        string actor,
        DateTimeOffset receivedAtUtc,
        Guid? parentReceiptId,
        CancellationToken cancellationToken) =>
        StoreConcurrencyRetry.ExecuteAsync(
            token => GetOrCreateCoreAsync(
                groupId,
                channel,
                submissionToken,
                expectedMemberCount,
                actor,
                receivedAtUtc,
                parentReceiptId,
                token),
            cancellationToken,
            StoreConcurrencyRetry.IsDeadlockOrKeyCollision);

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
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        var existing = await context.IntakeSubmissionGroups
            .SingleOrDefaultAsync(
                item => item.SourceChannel == EfIntakeReceiptStore.ToCode(channel)
                    && item.SubmissionToken == submissionToken,
                cancellationToken);
        if (existing is null)
        {
            existing = new()
            {
                Id = groupId,
                SourceChannel = EfIntakeReceiptStore.ToCode(channel),
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
    private Task<IntakeSubmissionGroupMember> AddMemberWithRetryAsync(
        Guid groupId,
        int ordinal,
        ReceivedIntake received,
        CancellationToken cancellationToken) =>
        StoreConcurrencyRetry.ExecuteAsync(
            token => AddMemberCoreAsync(groupId, ordinal, received, token),
            cancellationToken,
            StoreConcurrencyRetry.IsDeadlockOrKeyCollision);

    private async Task<IntakeSubmissionGroupMember> AddMemberCoreAsync(
        Guid groupId,
        int ordinal,
        ReceivedIntake received,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
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
        // Serializable: the discard is decided on every member receipt's
        // associations, Case links, allocations and Image intakes, and no one
        // row covers them.
        await using var transaction = await context.Database.BeginSerializableAsync(cancellationToken);

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
        if (group.SourceChannel != EfIntakeReceiptStore.ToCode(IntakeSourceChannel.ManualUpload))
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

    public async Task DeclineImageMatchAsync(
        Guid groupId,
        string staffSubjectId,
        DateTimeOffset declinedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(staffSubjectId);
        var operationKey = $"image-match-decline:{groupId:N}";
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        var group = await context.IntakeSubmissionGroups
            .SingleOrDefaultAsync(item => item.Id == groupId, cancellationToken);
        if (group is null || group.DiscardedAtUtc is not null || group.ImageMatchDeclinedAtUtc is not null)
        {
            return;
        }

        var fingerprint = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes($"{groupId:N}|image_match_declined|{staffSubjectId}")))
            .ToLowerInvariant();
        var beforeVersion = group.Version;
        group.Version++;
        group.ImageMatchDeclinedAtUtc = declinedAtUtc;
        group.ImageMatchDeclinedByActorSubjectId = staffSubjectId;
        context.IntakeSubmissionGroupHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            Group = group,
            EventType = "image_match_declined",
            ActorKind = "Staff",
            ActorSubjectId = staffSubjectId,
            ActorRolesJson = "[]",
            OperationKey = operationKey,
            RequestFingerprint = fingerprint,
            OccurredAtUtc = declinedAtUtc,
            BeforeVersion = beforeVersion,
            AfterVersion = group.Version
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

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
        return await MapMembersAsync(context, entities, false, cancellationToken);
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
        var mapped = await MapMembersAsync(context, members, false, cancellationToken);

        return new(
            entity.Id,
            EfIntakeReceiptStore.ParseSourceChannel(entity.SourceChannel),
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
                entity.DiscardRequestFingerprint!),
            entity.ImageMatchDeclinedAtUtc is null ? null : new(
                entity.ImageMatchDeclinedByActorSubjectId!,
                entity.ImageMatchDeclinedAtUtc.Value));
    }

    private static async Task<IntakeSubmissionGroupMember> MapMemberAsync(
        PegasusDbContext context,
        IntakeSubmissionGroupMemberEntity entity,
        bool isDuplicate,
        CancellationToken cancellationToken) =>
        (await MapMembersAsync(context, [entity], isDuplicate, cancellationToken))[0];

    /// <summary>
    /// Maps members with two set reads however many there are: the staged
    /// receipts, then each member's processing outcome. A member whose staged
    /// receipt is missing still throws, and the input order is kept.
    /// </summary>
    private static async Task<List<IntakeSubmissionGroupMember>> MapMembersAsync(
        PegasusDbContext context,
        IntakeSubmissionGroupMemberEntity[] entities,
        bool isDuplicate,
        CancellationToken cancellationToken)
    {
        if (entities.Length == 0)
        {
            return [];
        }

        var stagedIds = entities.Select(entity => entity.StagedReceiptId).Distinct().ToArray();
        var staged = (await context.IntakeStagedReceipts
                .AsNoTracking()
                .Where(item => stagedIds.Contains(item.Id))
                .ToArrayAsync(cancellationToken))
            .ToDictionary(item => item.Id);
        // The member's own read status, from the receipt its processing produced:
        // a file that could not be read is flagged on its row, never split off.
        var outcomes = (await (
                    from work in context.IntakeWorkItems.AsNoTracking()
                    join receipt in context.IntakeReceipts.AsNoTracking() on work.ProcessedReceiptId equals receipt.Id
                    where stagedIds.Contains(work.StagedReceiptId)
                    select new { work.StagedReceiptId, receipt.Id, receipt.Decision })
                .ToArrayAsync(cancellationToken))
            .GroupBy(item => item.StagedReceiptId)
            .ToDictionary(group => group.Key, group => group.First());
        var members = new List<IntakeSubmissionGroupMember>(entities.Length);
        foreach (var entity in entities)
        {
            if (!staged.TryGetValue(entity.StagedReceiptId, out var receipt))
            {
                throw new InvalidOperationException($"Staged receipt {entity.StagedReceiptId} for the group member was not found.");
            }

            outcomes.TryGetValue(entity.StagedReceiptId, out var processed);
            members.Add(new(
                entity.GroupId,
                entity.Ordinal,
                entity.StagedReceiptId,
                receipt.SourceFileName,
                receipt.SourceHash,
                isDuplicate)
            {
                ProcessedReceiptId = processed?.Id,
                CouldNotBeRead = processed is null
                    ? null
                    : IntakeDecisionPolicy.CouldNotBeRead(EfIntakeReceiptStore.ParseDecision(processed.Decision))
            });
        }

        return members;
    }

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
