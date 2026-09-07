using System.Data;
using System.Text.Json;
using Pegasus.Core.Documents;
using Pegasus.Core.Intake;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfIntakeReceiptStore(IDbContextFactory<PegasusDbContext> contextFactory)
    : IIntakeReceiptStore, IIntakeReceiptQueries, ICaseEvidenceImageQueries
{
    private const int JsonVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IntakeReceipt> StoreAsync(
        IntakeReceiptDraft draft,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                return await StoreOnceAsync(draft, cancellationToken);
            }
            catch (Exception exception) when (attempt < 3 && IsRetryableConcurrencyFailure(exception))
            {
                var duplicate = await FindBySourceIdentityAsync(draft.SourceIdentity, cancellationToken);
                if (duplicate is not null)
                {
                    EnsureMatchingContent(duplicate.SourceHash, draft.SourceHash);
                    return duplicate with { IsDuplicate = true };
                }

                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }

        throw new InvalidOperationException("The intake receipt could not be stored after the concurrency retry limit.");
    }
    public async Task<IntakeReceipt> ReplaceEvaluationAsync(
        IntakeReceiptDraft draft,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var channelCode = ToCode(draft.SourceIdentity.Channel);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var receipt = await context.IntakeReceipts
            .Include(item => item.Assets)
            .Include(item => item.SearchDocuments)
            .Include(item => item.InstructionDraft)
            .Include(item => item.MailRouteDecision)
            .Include(item => item.MailClassificationDecision)
            .Include(item => item.CaseMatchDecision)
            .Include(item => item.ManualAssociation)
            .SingleOrDefaultAsync(
                item => item.SourceChannel == channelCode
                    && item.ExternalReceiptToken == draft.SourceIdentity.ExternalReceiptToken,
                cancellationToken)
            ?? throw new InvalidDataException(
                "The intake receipt selected for re-evaluation does not exist.");
        EnsureMatchingContent(receipt.SourceHash, draft.SourceHash);

        var retainedSource = receipt.Assets
            .Where(item => item.Kind == ToCode(IntakeAssetKind.Source)
                && item.Disposition == ToCode(IntakeAssetDisposition.Source))
            .Take(2)
            .ToArray();
        var evaluatedSource = draft.AssetRecords
            .Where(item => item.Kind == IntakeAssetKind.Source
                && item.Disposition == IntakeAssetDisposition.Source)
            .Take(2)
            .ToArray();
        if (retainedSource.Length != 1
            || evaluatedSource.Length != 1
            || retainedSource[0].ContentLength != evaluatedSource[0].ContentLength
            || !string.Equals(
                retainedSource[0].ContentHash,
                evaluatedSource[0].ContentHash,
                StringComparison.Ordinal)
            || !string.Equals(
                retainedSource[0].StorageKey,
                evaluatedSource[0].StorageKey,
                StringComparison.Ordinal))
        {
            throw new IntakeArtifactIntegrityException();
        }

        // The originals of the three versioned decision records are preserved in the
        // re-evaluation event before the in-place replacement below: a rule change never
        // silently erases the decision history it supersedes.
        var priorDecisions = new
        {
            MailRoute = receipt.MailRouteDecision is null
                ? null
                : MapMailRouteDecision(receipt.MailRouteDecision),
            MailClassification = receipt.MailClassificationDecision is null
                ? null
                : MapMailClassificationDecision(receipt.MailClassificationDecision),
            CaseMatch = receipt.CaseMatchDecision is null
                ? null
                : MapCaseMatchDecision(receipt.CaseMatchDecision)
        };

        receipt.ProcessedAtUtc = draft.ProcessedAtUtc;
        receipt.SourceReaderKey = draft.SourceReaderKey;
        receipt.SourceReaderVersion = draft.SourceReaderVersion;
        receipt.ExtractionPolicyKey = draft.ExtractionPolicyKey;
        receipt.ExtractionPolicyVersion = draft.ExtractionPolicyVersion;
        receipt.Decision = ToCode(draft.Decision);
        receipt.DecisionReason = draft.DecisionReason;
        receipt.EvidenceJson = SerializeEvidence(draft.Evidence);
        receipt.FieldsJson = SerializeFields(draft.Fields);
        receipt.OcrCandidatesJson = SerializeEnvelope(draft.ScannedPdfPages);
        receipt.FailureCode = draft.FailureCode;
        receipt.FailureReason = draft.FailureReason;
        ApplyInstructionDraft(context, receipt, draft.InstructionDraft);
        ApplyMailRouteDecision(context, receipt, draft.MailRouteDecision);
        ApplyMailClassificationDecision(
            context,
            receipt,
            draft.MailClassificationDecision,
            draft.Actor,
            draft.ProcessedAtUtc);
        ApplyCaseMatchDecision(context, receipt, draft.CaseMatchDecision);
        AppendNewDerivedAssets(receipt, draft.AssetRecords);
        ReplaceSearchDocuments(context, receipt, draft.SearchDocumentRecords);
        receipt.Version++;

        context.IntakeReceiptEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            IntakeReceiptId = receipt.Id,
            EventType = "intake_receipt_reevaluated",
            Actor = draft.Actor,
            OccurredAtUtc = draft.ProcessedAtUtc,
            DetailsJson = SerializeEnvelope(new
            {
                Decision = receipt.Decision,
                receipt.Version,
                receipt.ExtractionPolicyKey,
                receipt.ExtractionPolicyVersion,
                PriorDecisions = priorDecisions
            })
        });

        var acceptedCaseId = await context.CaseIntakeLinks
            .AsNoTracking()
            .Where(item => item.IntakeReceiptId == receipt.Id)
            .Select(item => (Guid?)item.CaseId)
            .SingleOrDefaultAsync(cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(receipt, false, acceptedCaseId);
    }

    /// <summary>
    /// How much received material is still waiting for a person.
    /// </summary>
    /// <remarks>
    /// Receipts that produced a case are excluded. Without that filter every
    /// count was cumulative for all time — creating a case from a receipt never
    /// decremented anything, so the dashboard's queue numbers only ever grew.
    /// </remarks>
    public async Task<IntakeQueueCounts> GetCountsAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var decisions = await context.IntakeReceipts
            .AsNoTracking()
            .Where(item => !context.CaseIntakeLinks.Any(link => link.IntakeReceiptId == item.Id))
            .Select(item => item.Decision)
            .ToListAsync(cancellationToken);
        var parsedDecisions = decisions.Select(ParseDecision).ToArray();
        return new(
            parsedDecisions.Count(item => item == IntakeDecision.NeedsSorting),
            parsedDecisions.Count(item => item == IntakeDecision.BlockedIntake));
    }

    public async Task<IntakeListPage> ListAsync(
        IntakeDecision? decision,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Deliberately no case-link exclusion here, unlike the counts. Received
        // items is a viewer of everything received, and a message that became a
        // case is still a message that was received — the row says so and links to
        // the case. What was wrong before was the label, not the presence: an
        // accepted receipt sat here reading "Instruction draft" with no indication
        // that it had produced anything.
        //
        // Filtered, ordered, counted and paged in SQL. This used to materialise
        // every receipt with its mail-route decision, sort in memory and take the
        // first hundred, and the caller then paged inside that hundred and reported
        // it as the total — so the list had exactly four reachable pages at
        // twenty-five a page, and the page count it printed was false.
        var matches = context.IntakeReceipts.AsNoTracking();
        if (decision is { } requested)
        {
            var code = ToCode(requested);
            matches = matches.Where(item => item.Decision == code);
        }

        var totalCount = await matches.CountAsync(cancellationToken);
        var rows = await matches
            .OrderByDescending(item => item.ReceivedAtUtc)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new
            {
                item.Id,
                item.SourceFileName,
                item.ReceivedAtUtc,
                item.Decision,
                item.FailureReason,
                item.EvidenceJson,
                Sender = item.MailRouteDecision!.EffectiveSenderAddress
            })
            .ToListAsync(cancellationToken);

        // One join for the page rather than a lookup per row.
        var receiptIds = rows.Select(item => item.Id).ToArray();
        var cases = receiptIds.Length == 0
            ? []
            : await context.IntakeManualAssociations
                .AsNoTracking()
                .Where(association => association.IsActive
                    && receiptIds.Contains(association.IntakeReceiptId))
                .Select(association => new
                {
                    association.IntakeReceiptId,
                    association.CaseId,
                    association.Case.Reference
                })
                .ToDictionaryAsync(item => item.IntakeReceiptId, cancellationToken);
        var allocationStates = receiptIds.Length == 0
            ? new Dictionary<Guid, IntakeAllocationState>()
            : (await context.IntakeAllocationAttempts
                .AsNoTracking()
                .Where(item => receiptIds.Contains(item.IntakeReceiptId))
                .OrderByDescending(item => item.AttemptNumber)
                .ToListAsync(cancellationToken))
                .GroupBy(item => item.IntakeReceiptId)
                .ToDictionary(
                    group => group.Key,
                    group => IntakeAllocationState.FromAttempt(
                        EfIntakeAllocationStore.Map(group.First())));

        var summaries = rows
            .Select(item =>
            {
                cases.TryGetValue(item.Id, out var linkedCase);
                allocationStates.TryGetValue(item.Id, out var allocationState);
                return new IntakeReceiptSummary(
                    item.Id,
                    item.SourceFileName,
                    item.ReceivedAtUtc,
                    ParseDecision(item.Decision),
                    item.FailureReason,
                    item.Sender,
                    ReadSubject(item.EvidenceJson),
                    linkedCase?.CaseId,
                    linkedCase?.Reference,
                    allocationState);
            })
            .ToArray();
        return new(summaries, page, pageSize, totalCount);
    }

    /// <summary>
    /// One keyset page of received items, newest first, strictly after the
    /// caller's recorded position.
    ///
    /// The order is the same (ReceivedAtUtc DESC, Id DESC) the offset list
    /// uses, so the two views agree about what "newest first" means. The id is
    /// not decoration: two receipts can share a millisecond, and without it a
    /// page boundary that falls between them either drops one or serves it
    /// twice, silently, for ever.
    /// </summary>
    public async Task<KeysetPage<IntakeReceiptSummary>> ListByCursorAsync(
        IntakeDecision? decision,
        KeysetPosition? after,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var matches = context.IntakeReceipts.AsNoTracking();
        if (decision is { } requested)
        {
            var code = ToCode(requested);
            matches = matches.Where(item => item.Decision == code);
        }

        if (after is { } position)
        {
            // Descending order, so "after" means strictly older: an equal
            // timestamp continues at a strictly smaller id. Written as two
            // comparisons rather than a tuple so SQL Server translates it and
            // the index on (ReceivedAtUtc, Id) is used.
            matches = matches.Where(item =>
                item.ReceivedAtUtc < position.SortKey
                || (item.ReceivedAtUtc == position.SortKey
                    && item.Id.CompareTo(position.Id) < 0));
        }

        // One row beyond the page: its presence is what says another page
        // exists, without a second count query that could disagree with it.
        var rows = await matches
            .OrderByDescending(item => item.ReceivedAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(limit + 1)
            .Select(item => new SummaryRow(
                item.Id,
                item.SourceFileName,
                item.ReceivedAtUtc,
                item.Decision,
                item.FailureReason,
                item.EvidenceJson,
                item.MailRouteDecision!.EffectiveSenderAddress))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        var page = rows.Take(limit).ToArray();
        var summaries = await EnrichAsync(context, page, cancellationToken);
        var next = hasMore && page.Length > 0
            ? new KeysetPosition(page[^1].ReceivedAtUtc, page[^1].Id)
            : null;
        return new(summaries, next);
    }

    private sealed record SummaryRow(
        Guid Id,
        string SourceFileName,
        DateTimeOffset ReceivedAtUtc,
        string Decision,
        string? FailureReason,
        string EvidenceJson,
        string? Sender);

    /// <summary>
    /// Adds the case link and allocation state to one page of rows with one
    /// query each, never one per row.
    /// </summary>
    private static async Task<IReadOnlyList<IntakeReceiptSummary>> EnrichAsync(
        PegasusDbContext context,
        IReadOnlyList<SummaryRow> rows,
        CancellationToken cancellationToken)
    {
        var receiptIds = rows.Select(item => item.Id).ToArray();
        if (receiptIds.Length == 0)
        {
            return [];
        }

        var cases = await context.IntakeManualAssociations
            .AsNoTracking()
            .Where(association => association.IsActive
                && receiptIds.Contains(association.IntakeReceiptId))
            .Select(association => new
            {
                association.IntakeReceiptId,
                association.CaseId,
                association.Case.Reference
            })
            .ToDictionaryAsync(item => item.IntakeReceiptId, cancellationToken);
        var allocationStates = (await context.IntakeAllocationAttempts
            .AsNoTracking()
            .Where(item => receiptIds.Contains(item.IntakeReceiptId))
            .OrderByDescending(item => item.AttemptNumber)
            .ToListAsync(cancellationToken))
            .GroupBy(item => item.IntakeReceiptId)
            .ToDictionary(
                group => group.Key,
                group => IntakeAllocationState.FromAttempt(
                    EfIntakeAllocationStore.Map(group.First())));

        return rows.Select(item =>
        {
            cases.TryGetValue(item.Id, out var linkedCase);
            allocationStates.TryGetValue(item.Id, out var allocationState);
            return new IntakeReceiptSummary(
                item.Id,
                item.SourceFileName,
                item.ReceivedAtUtc,
                ParseDecision(item.Decision),
                item.FailureReason,
                item.Sender,
                ReadSubject(item.EvidenceJson),
                linkedCase?.CaseId,
                linkedCase?.Reference,
                allocationState);
        }).ToArray();
    }

    public async Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.IntakeReceipts
            .AsNoTracking()
            .Include(item => item.Assets)
            .Include(item => item.InstructionDraft)
            .Include(item => item.MailRouteDecision)
            .Include(item => item.MailClassificationDecision)
            .Include(item => item.CaseMatchDecision)
            .Include(item => item.ManualAssociation)
            .ThenInclude(item => item!.Case)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var acceptedCase = await context.CaseIntakeLinks
            .AsNoTracking()
            .Where(item => item.IntakeReceiptId == id)
            .Select(item => new { item.CaseId, item.Case.Reference })
            .SingleOrDefaultAsync(cancellationToken);
        var allocationState = await GetAllocationStateAsync(context, id, cancellationToken);
        return Map(
            entity,
            false,
            acceptedCase?.CaseId,
            allocationState,
            acceptedCase?.Reference,
            entity.ManualAssociation is { IsActive: true } association
                ? association.Case.Reference
                : null);
    }

    public async Task<IntakeReceipt?> FindBySourceIdentityAsync(
        IntakeSourceIdentity sourceIdentity,
        CancellationToken cancellationToken)
    {
        var channelCode = ToCode(sourceIdentity.Channel);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.IntakeReceipts
            .AsNoTracking()
            .Include(item => item.Assets)
            .Include(item => item.InstructionDraft)
            .Include(item => item.MailRouteDecision)
            .Include(item => item.MailClassificationDecision)
            .Include(item => item.CaseMatchDecision)
            .Include(item => item.ManualAssociation)
            .ThenInclude(item => item!.Case)
            .SingleOrDefaultAsync(
                item => item.SourceChannel == channelCode
                    && item.ExternalReceiptToken == sourceIdentity.ExternalReceiptToken,
                cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var acceptedCase = await context.CaseIntakeLinks
            .AsNoTracking()
            .Where(item => item.IntakeReceiptId == entity.Id)
            .Select(item => new { item.CaseId, item.Case.Reference })
            .SingleOrDefaultAsync(cancellationToken);
        var allocationState = await GetAllocationStateAsync(context, entity.Id, cancellationToken);
        return Map(
            entity,
            false,
            acceptedCase?.CaseId,
            allocationState,
            acceptedCase?.Reference,
            entity.ManualAssociation is { IsActive: true } association
                ? association.Case.Reference
                : null);
    }

    public async Task<IntakeAssetRecord?> GetAssetAsync(
        Guid receiptId,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.IntakeAssets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.IntakeReceiptId == receiptId && item.Id == assetId,
                cancellationToken);
        return entity is null ? null : MapAsset(entity);
    }

    private async Task<IntakeReceipt> StoreOnceAsync(
        IntakeReceiptDraft draft,
        CancellationToken cancellationToken)
    {
        var channelCode = ToCode(draft.SourceIdentity.Channel);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existingQuery = context.IntakeReceipts
            .AsNoTracking()
            .Include(item => item.Assets)
            .Include(item => item.InstructionDraft)
            .Include(item => item.MailRouteDecision)
            .Include(item => item.MailClassificationDecision)
            .Include(item => item.CaseMatchDecision)
            .Include(item => item.ManualAssociation);
        if (context.Database.IsSqlServer())
        {
            existingQuery = context.IntakeReceipts
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM [IntakeReceipts] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [SourceChannel] = {channelCode}
                      AND [ExternalReceiptToken] = {draft.SourceIdentity.ExternalReceiptToken}
                """)
                .AsNoTracking()
                .Include(item => item.Assets)
                .Include(item => item.InstructionDraft)
                .Include(item => item.MailRouteDecision)
            .Include(item => item.MailClassificationDecision)
            .Include(item => item.CaseMatchDecision)
                .Include(item => item.ManualAssociation);
        }

        var existing = await existingQuery.SingleOrDefaultAsync(
            item => item.SourceChannel == channelCode
                && item.ExternalReceiptToken == draft.SourceIdentity.ExternalReceiptToken,
            cancellationToken);
        if (existing is not null)
        {
            EnsureMatchingContent(existing.SourceHash, draft.SourceHash);
            return Map(existing, true);
        }

        var receipt = new IntakeReceiptEntity
        {
            Id = Guid.NewGuid(),
            SourceFileName = draft.SourceFileName,
            MediaType = draft.MediaType,
            SourceLength = draft.SourceLength,
            SourceHash = draft.SourceHash,
            SourceChannel = channelCode,
            ExternalReceiptToken = draft.SourceIdentity.ExternalReceiptToken,
            ReceivedAtUtc = draft.ReceivedAtUtc,
            ProcessedAtUtc = draft.ProcessedAtUtc,
            SourceReaderKey = draft.SourceReaderKey,
            SourceReaderVersion = draft.SourceReaderVersion,
            ExtractionPolicyKey = draft.ExtractionPolicyKey,
            ExtractionPolicyVersion = draft.ExtractionPolicyVersion,
            Decision = ToCode(draft.Decision),
            DecisionReason = draft.DecisionReason,
            EvidenceJson = SerializeEvidence(draft.Evidence),
            FieldsJson = SerializeFields(draft.Fields),
            OcrCandidatesJson = SerializeEnvelope(draft.ScannedPdfPages),
            FailureCode = draft.FailureCode,
            FailureReason = draft.FailureReason
        };
        if (draft.InstructionDraft is not null)
        {
            receipt.InstructionDraft = new()
            {
                IntakeReceiptId = receipt.Id,
                IntakeReceipt = receipt,
                SuggestedPrincipalCode = draft.InstructionDraft.SuggestedPrincipalCode,
                ClaimantName = draft.InstructionDraft.ClaimantName,
                ClaimNumber = draft.InstructionDraft.ClaimNumber,
                VehicleRegistration = draft.InstructionDraft.VehicleRegistration,
                VehicleMake = draft.InstructionDraft.VehicleMake,
                VehicleModel = draft.InstructionDraft.VehicleModel,
                VehicleMileage = draft.InstructionDraft.VehicleMileage,
                AccidentCircumstances = draft.InstructionDraft.AccidentCircumstances,
                DateOfIncident = draft.InstructionDraft.DateOfIncident,
                InstructionDate = draft.InstructionDraft.InstructionDate,
                InspectionDate = draft.InstructionDraft.InspectionDate,
                InspectionAddress = draft.InstructionDraft.InspectionAddress,
                VehicleMileageUnit = draft.InstructionDraft.VehicleMileageUnit,
                VatStatus = draft.InstructionDraft.VatStatus,
                ClaimantAddress = draft.InstructionDraft.ClaimantAddress,
                ClaimantContactNumber = draft.InstructionDraft.ClaimantContactNumber,
                FileHandlerName = draft.InstructionDraft.FileHandlerName,
                FileHandlerEmailAddress = draft.InstructionDraft.FileHandlerEmailAddress,
                FileHandlerPhoneNumber = draft.InstructionDraft.FileHandlerPhoneNumber,
                Notes = draft.InstructionDraft.Notes
            };
        }

        if (draft.MailRouteDecision is not null)
        {
            receipt.MailRouteDecision = MapMailRouteDecision(draft.MailRouteDecision, receipt);
        }

        if (draft.MailClassificationDecision is not null)
        {
            receipt.MailClassificationDecision =
                MapMailClassificationDecision(draft.MailClassificationDecision, receipt);
            receipt.MailClassificationDecision.DecidedByActor = draft.Actor;
            receipt.MailClassificationDecision.DecidedAtUtc = draft.ProcessedAtUtc;
        }

        if (draft.CaseMatchDecision is not null)
        {
            receipt.CaseMatchDecision = MapCaseMatchDecision(draft.CaseMatchDecision, receipt);
        }

        receipt.Assets.AddRange(draft.AssetRecords.Select(asset => new IntakeAssetEntity
        {
            Id = asset.Id,
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt,
            SourceLabel = asset.SourceLabel,
            FileName = asset.FileName,
            MediaType = asset.MediaType,
            Kind = ToCode(asset.Kind),
            Disposition = ToCode(asset.Disposition),
            ContentLength = asset.ContentLength,
            ContentHash = asset.ContentHash,
            StorageKey = asset.StorageKey,
            PageNumber = asset.PageNumber,
            BoundsJson = asset.Bounds is null ? null : SerializeEnvelope(asset.Bounds),
            WidthPixels = asset.WidthPixels,
            HeightPixels = asset.HeightPixels
        }));
        AddSearchDocuments(receipt, draft.SearchDocumentRecords);
        context.IntakeReceipts.Add(receipt);
        context.IntakeReceiptEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            IntakeReceiptId = receipt.Id,
            EventType = "intake_receipt_recorded",
            Actor = draft.Actor,
            OccurredAtUtc = draft.ProcessedAtUtc,
            DetailsJson = SerializeEnvelope(new IntakeReceiptEventDetails(
                ToCode(draft.Decision),
                channelCode,
                draft.SourceIdentity.ExternalReceiptToken,
                draft.SourceHash))
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(receipt, false);
    }

    internal static IntakeReceipt Map(
        IntakeReceiptEntity entity,
        bool isDuplicate,
        Guid? acceptedCaseId = null,
        IntakeAllocationState? allocationState = null,
        string? acceptedCaseReference = null,
        string? manualLinkedCaseReference = null)
    {
        var fields = DeserializeFields(entity.FieldsJson);
        return new(
            entity.Id,
            entity.SourceFileName,
            entity.MediaType,
            entity.SourceLength,
            entity.SourceHash,
            new(ParseSourceChannel(entity.SourceChannel), entity.ExternalReceiptToken),
            entity.ReceivedAtUtc,
            entity.ProcessedAtUtc,
            ParseDecision(entity.Decision),
            entity.DecisionReason,
            DeserializeEvidence(entity.EvidenceJson),
            fields,
            entity.InstructionDraft is null ? null : MapInstructionDraft(entity.InstructionDraft),
            fields.Where(field => field.Candidates.Count == 0).Select(field => field.Name).ToArray(),
            entity.FailureCode,
            entity.FailureReason,
            isDuplicate,
            entity.SourceReaderKey,
            entity.SourceReaderVersion,
            entity.ExtractionPolicyKey,
            entity.ExtractionPolicyVersion,
            entity.Assets.OrderBy(asset => asset.Id).Select(MapAsset).ToArray(),
            DeserializeEnvelope<IReadOnlyList<ScannedPdfOcrCandidate>>(entity.OcrCandidatesJson) ?? [],
            entity.MailRouteDecision is null ? null : MapMailRouteDecision(entity.MailRouteDecision),
            entity.Version,
            acceptedCaseId,
            entity.ManualAssociation is { IsActive: true } association ? association.CaseId : null,
            entity.ManualAssociation?.Version,
            entity.MailClassificationDecision is null
                ? null
                : MapMailClassificationDecision(entity.MailClassificationDecision),
            entity.CaseMatchDecision is null
                ? null
                : MapCaseMatchDecision(entity.CaseMatchDecision),
            allocationState,
            acceptedCaseReference,
            manualLinkedCaseReference,
            entity.ManualAssociation is { IsActive: true } activeAssociation
                && Enum.TryParse<ActorKind>(activeAssociation.ActorKind, ignoreCase: false, out var associationActorKind)
                ? associationActorKind
                : null);
    }

    private static async Task<IntakeAllocationState?> GetAllocationStateAsync(
        PegasusDbContext context,
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        var attempt = await context.IntakeAllocationAttempts
            .AsNoTracking()
            .Where(item => item.IntakeReceiptId == receiptId)
            .OrderByDescending(item => item.AttemptNumber)
            .FirstOrDefaultAsync(cancellationToken);
        return attempt is null
            ? null
            : IntakeAllocationState.FromAttempt(EfIntakeAllocationStore.Map(attempt));
    }

    private static InstructionDraft MapInstructionDraft(InstructionDraftEntity entity) => new(
        entity.SuggestedPrincipalCode,
        entity.ClaimantName,
        entity.ClaimNumber,
        entity.VehicleRegistration,
        entity.VehicleMake,
        entity.VehicleModel,
        entity.VehicleMileage,
        entity.AccidentCircumstances,
        entity.DateOfIncident,
        entity.InstructionDate,
        entity.InspectionAddress,
        entity.InspectionDate,
        entity.VehicleMileageUnit,
        entity.VatStatus,
        entity.ClaimantAddress,
        entity.ClaimantContactNumber,
        entity.FileHandlerName,
        entity.FileHandlerEmailAddress,
        entity.FileHandlerPhoneNumber,
        entity.Notes);

    private static IntakeMailRouteDecisionEntity MapMailRouteDecision(
        MailRouteEvaluationResult decision,
        IntakeReceiptEntity receipt) =>
        new()
        {
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt,
            Disposition = ToCode(decision.Disposition),
            RouteOwnerCode = decision.SelectedRoute?.RouteOwnerCode,
            RouteKind = decision.SelectedRoute is null ? null : ToCode(decision.SelectedRoute.Kind),
            WorkProviderCode = decision.SelectedRoute?.WorkProviderCode,
            PredicatesJson = SerializeEnvelope(decision.Predicates),
            Reason = decision.Reason,
            PolicyKey = decision.PolicyKey,
            PolicyVersion = decision.PolicyVersion,
            TransportIdentitiesJson = SerializeEnvelope(decision.TransportIdentities),
            OriginalIdentitiesJson = SerializeEnvelope(decision.OriginalIdentities),
            EffectiveSenderAddress = decision.EffectiveSender?.Address,
            EffectiveSenderSourceLabel = decision.EffectiveSender?.SourceLabel
        };
    private static IntakeMailClassificationDecisionEntity MapMailClassificationDecision(
        MailClassificationResult decision,
        IntakeReceiptEntity receipt) =>
        new()
        {
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt,
            Outcome = ToCode(decision.Outcome),
            Direction = decision.Category is null ? null : ToCode(decision.Category.Direction),
            Family = decision.Category is { IsOther: false } category
                ? category.Name
                : null,
            Subtype = decision.Category?.Subtype,
            CaseType = decision.CaseType is null ? null : ToCode(decision.CaseType.Value),
            IsReplyContext = decision.Category?.IsReplyContext ?? false,
            OtherName = decision.Category?.OtherName,
            OtherReasoning = decision.Category?.OtherReasoning,
            AmbiguousCandidatesJson = SerializeEnvelope(decision.AmbiguousCandidates),
            PredicatesJson = SerializeEnvelope(decision.Predicates),
            Reason = decision.Reason,
            PolicyKey = decision.PolicyKey,
            PolicyVersion = decision.PolicyVersion,
            StandaloneAuditReportAssetSourceLabel = decision.StandaloneAuditReport?.AssetSourceLabel,
            StandaloneAuditReportAssessment = decision.StandaloneAuditReport is { } report
                ? ToCode(report.Assessment)
                : null,
            DecidedByActor = string.Empty,
            DecidedAtUtc = receipt.ProcessedAtUtc
        };

    internal static MailClassificationResult MapMailClassificationDecision(
        IntakeMailClassificationDecisionEntity entity)
    {
        MailCategory? category = null;
        if (entity.OtherName is not null)
        {
            if (entity.Direction is null || entity.OtherReasoning is null)
            {
                throw new InvalidDataException(
                    "The persisted 'Other' classification is incomplete.");
            }

            category = MailCategory.Other(
                ParseMailDirection(entity.Direction),
                entity.OtherName,
                entity.OtherReasoning);
        }
        else if (entity.Family is not null)
        {
            if (entity.Direction is null)
            {
                throw new InvalidDataException(
                    "The persisted classification family carries no direction.");
            }

            category = ParseMailDirection(entity.Direction) == MailDirection.Received
                ? MailCategory.Received(
                    MailTaxonomy.ParseReceivedFamily(entity.Family),
                    entity.Subtype,
                    entity.IsReplyContext)
                : MailCategory.Sent(
                    MailTaxonomy.ParseSentFamily(entity.Family),
                    entity.IsReplyContext);
        }

        var hasAnyAuditReportValue = entity.StandaloneAuditReportAssetSourceLabel is not null
            || entity.StandaloneAuditReportAssessment is not null;
        var hasCompleteAuditReport = entity.StandaloneAuditReportAssetSourceLabel is not null
            && entity.StandaloneAuditReportAssessment is not null;
        if (hasAnyAuditReportValue != hasCompleteAuditReport)
        {
            throw new InvalidDataException(
                "The persisted standalone Audit report evaluation is incomplete.");
        }

        return new(
            ParseMailClassificationOutcome(entity.Outcome),
            category,
            DeserializeEnvelope<IReadOnlyList<string>>(entity.AmbiguousCandidatesJson) ?? [],
            DeserializeEnvelope<IReadOnlyList<MailClassificationPredicateResult>>(entity.PredicatesJson),
            entity.Reason,
            entity.PolicyKey,
            entity.PolicyVersion,
            entity.CaseType is null ? null : ParseCaseType(entity.CaseType),
            hasCompleteAuditReport
                ? new(
                    entity.StandaloneAuditReportAssetSourceLabel!,
                    ParseAuditAssessment(entity.StandaloneAuditReportAssessment!))
                : null);
    }

    private static void ApplyMailClassificationDecision(
        PegasusDbContext context,
        IntakeReceiptEntity receipt,
        MailClassificationResult? decision,
        string actor,
        DateTimeOffset decidedAtUtc)
    {
        if (decision is null)
        {
            if (receipt.MailClassificationDecision is not null)
            {
                context.Remove(receipt.MailClassificationDecision);
                receipt.MailClassificationDecision = null;
            }
            return;
        }

        var replacement = MapMailClassificationDecision(decision, receipt);
        if (receipt.MailClassificationDecision is null)
        {
            replacement.DecidedByActor = actor;
            replacement.DecidedAtUtc = decidedAtUtc;
            receipt.MailClassificationDecision = replacement;
            return;
        }

        var entity = receipt.MailClassificationDecision;
        // A staff correction is the accepted current decision. Automated replay may
        // still recompute evidence elsewhere, but it must not silently overwrite it.
        if (entity.Version > 1)
        {
            return;
        }
        entity.Outcome = replacement.Outcome;
        entity.Direction = replacement.Direction;
        entity.Family = replacement.Family;
        entity.Subtype = replacement.Subtype;
        entity.CaseType = replacement.CaseType;
        entity.IsReplyContext = replacement.IsReplyContext;
        entity.OtherName = replacement.OtherName;
        entity.OtherReasoning = replacement.OtherReasoning;
        entity.AmbiguousCandidatesJson = replacement.AmbiguousCandidatesJson;
        entity.PredicatesJson = replacement.PredicatesJson;
        entity.Reason = replacement.Reason;
        entity.PolicyKey = replacement.PolicyKey;
        entity.PolicyVersion = replacement.PolicyVersion;
        entity.StandaloneAuditReportAssetSourceLabel = replacement.StandaloneAuditReportAssetSourceLabel;
        entity.StandaloneAuditReportAssessment = replacement.StandaloneAuditReportAssessment;
        entity.DecidedByActor = actor;
        entity.DecidedAtUtc = decidedAtUtc;
    }

    private static IntakeCaseMatchDecisionEntity MapCaseMatchDecision(
        CaseMatchEvaluationResult decision,
        IntakeReceiptEntity receipt) =>
        new()
        {
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt,
            Outcome = ToCode(decision.Outcome),
            MatchedCaseId = decision.MatchedCaseId,
            RedirectedFromCaseId = decision.RedirectedFromCaseId,
            MatchKeysJson = SerializeEnvelope(decision.Keys),
            CandidatesJson = SerializeEnvelope(decision.Candidates),
            Reason = decision.Reason,
            PolicyKey = decision.PolicyKey,
            PolicyVersion = decision.PolicyVersion
        };

    private static CaseMatchEvaluationResult MapCaseMatchDecision(
        IntakeCaseMatchDecisionEntity entity) =>
        new(
            ParseCaseMatchOutcome(entity.Outcome),
            entity.MatchedCaseId,
            entity.RedirectedFromCaseId,
            DeserializeEnvelope<CaseMatchKeys>(entity.MatchKeysJson)
                ?? new(null, null, null, null, null),
            DeserializeEnvelope<IReadOnlyList<CaseMatchCandidateEvaluation>>(entity.CandidatesJson)
                ?? [],
            entity.Reason,
            entity.PolicyKey,
            entity.PolicyVersion);

    private static void ApplyCaseMatchDecision(
        PegasusDbContext context,
        IntakeReceiptEntity receipt,
        CaseMatchEvaluationResult? decision)
    {
        if (decision is null)
        {
            if (receipt.CaseMatchDecision is not null)
            {
                context.Remove(receipt.CaseMatchDecision);
                receipt.CaseMatchDecision = null;
            }
            return;
        }

        var replacement = MapCaseMatchDecision(decision, receipt);
        if (receipt.CaseMatchDecision is null)
        {
            receipt.CaseMatchDecision = replacement;
            return;
        }

        var entity = receipt.CaseMatchDecision;
        entity.Outcome = replacement.Outcome;
        entity.MatchedCaseId = replacement.MatchedCaseId;
        entity.RedirectedFromCaseId = replacement.RedirectedFromCaseId;
        entity.MatchKeysJson = replacement.MatchKeysJson;
        entity.CandidatesJson = replacement.CandidatesJson;
        entity.Reason = replacement.Reason;
        entity.PolicyKey = replacement.PolicyKey;
        entity.PolicyVersion = replacement.PolicyVersion;
    }

    private static void ApplyInstructionDraft(
        PegasusDbContext context,
        IntakeReceiptEntity receipt,
        InstructionDraft? draft)
    {
        if (draft is null)
        {
            if (receipt.InstructionDraft is not null)
            {
                context.Remove(receipt.InstructionDraft);
                receipt.InstructionDraft = null;
            }
            return;
        }

        var entity = receipt.InstructionDraft ?? new InstructionDraftEntity
        {
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt
        };
        entity.SuggestedPrincipalCode = draft.SuggestedPrincipalCode;
        entity.ClaimantName = draft.ClaimantName;
        entity.ClaimNumber = draft.ClaimNumber;
        entity.VehicleRegistration = draft.VehicleRegistration;
        entity.VehicleMake = draft.VehicleMake;
        entity.VehicleModel = draft.VehicleModel;
        entity.VehicleMileage = draft.VehicleMileage;
        entity.AccidentCircumstances = draft.AccidentCircumstances;
        entity.DateOfIncident = draft.DateOfIncident;
        entity.InstructionDate = draft.InstructionDate;
        entity.InspectionAddress = draft.InspectionAddress;
        entity.InspectionDate = draft.InspectionDate;
        entity.VehicleMileageUnit = draft.VehicleMileageUnit;
        entity.VatStatus = draft.VatStatus;
        entity.ClaimantAddress = draft.ClaimantAddress;
        entity.ClaimantContactNumber = draft.ClaimantContactNumber;
        entity.FileHandlerName = draft.FileHandlerName;
        entity.FileHandlerEmailAddress = draft.FileHandlerEmailAddress;
        entity.FileHandlerPhoneNumber = draft.FileHandlerPhoneNumber;
        entity.Notes = draft.Notes;
        receipt.InstructionDraft = entity;
    }

    private static void ReplaceSearchDocuments(
        PegasusDbContext context,
        IntakeReceiptEntity receipt,
        IReadOnlyList<IntakeSearchDocument> documents)
    {
        context.RemoveRange(receipt.SearchDocuments);
        receipt.SearchDocuments.Clear();
        AddSearchDocuments(receipt, documents);
    }

    private static void AddSearchDocuments(
        IntakeReceiptEntity receipt,
        IReadOnlyList<IntakeSearchDocument> documents)
    {
        for (var ordinal = 0; ordinal < documents.Count; ordinal++)
        {
            var document = documents[ordinal];
            receipt.SearchDocuments.Add(new()
            {
                Id = Guid.NewGuid(),
                IntakeReceiptId = receipt.Id,
                IntakeReceipt = receipt,
                Ordinal = ordinal,
                AttachmentOrdinal = document.AttachmentOrdinal,
                SourceLabel = document.SourceLabel,
                AttachmentFileName = document.AttachmentFileName,
                Text = document.Text
            });
        }
    }

    private static void ApplyMailRouteDecision(
        PegasusDbContext context,
        IntakeReceiptEntity receipt,
        MailRouteEvaluationResult? decision)
    {
        if (decision is null)
        {
            if (receipt.MailRouteDecision is not null)
            {
                context.Remove(receipt.MailRouteDecision);
                receipt.MailRouteDecision = null;
            }
            return;
        }

        var replacement = MapMailRouteDecision(decision, receipt);
        if (receipt.MailRouteDecision is null)
        {
            receipt.MailRouteDecision = replacement;
            return;
        }

        var entity = receipt.MailRouteDecision;
        entity.Disposition = replacement.Disposition;
        entity.RouteOwnerCode = replacement.RouteOwnerCode;
        entity.RouteKind = replacement.RouteKind;
        entity.WorkProviderCode = replacement.WorkProviderCode;
        entity.PredicatesJson = replacement.PredicatesJson;
        entity.Reason = replacement.Reason;
        entity.PolicyKey = replacement.PolicyKey;
        entity.PolicyVersion = replacement.PolicyVersion;
        entity.TransportIdentitiesJson = replacement.TransportIdentitiesJson;
        entity.OriginalIdentitiesJson = replacement.OriginalIdentitiesJson;
        entity.EffectiveSenderAddress = replacement.EffectiveSenderAddress;
        entity.EffectiveSenderSourceLabel = replacement.EffectiveSenderSourceLabel;
    }

    private static void AppendNewDerivedAssets(
        IntakeReceiptEntity receipt,
        IReadOnlyList<IntakeAssetRecord> evaluatedAssets)
    {
        var existing = receipt.Assets
            .Select(AssetIdentity)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var asset in evaluatedAssets.Where(item => item.Kind != IntakeAssetKind.Source))
        {
            if (!existing.Add(AssetIdentity(asset)))
            {
                continue;
            }

            receipt.Assets.Add(new()
            {
                Id = asset.Id,
                IntakeReceiptId = receipt.Id,
                IntakeReceipt = receipt,
                SourceLabel = asset.SourceLabel,
                FileName = asset.FileName,
                MediaType = asset.MediaType,
                Kind = ToCode(asset.Kind),
                Disposition = ToCode(asset.Disposition),
                ContentLength = asset.ContentLength,
                ContentHash = asset.ContentHash,
                StorageKey = asset.StorageKey,
                PageNumber = asset.PageNumber,
                BoundsJson = asset.Bounds is null ? null : SerializeEnvelope(asset.Bounds),
                WidthPixels = asset.WidthPixels,
                HeightPixels = asset.HeightPixels
            });
        }
    }

    private static string AssetIdentity(IntakeAssetEntity asset) =>
        $"{asset.Kind}|{asset.Disposition}|{asset.ContentHash}|{asset.StorageKey}";

    private static string AssetIdentity(IntakeAssetRecord asset) =>
        $"{ToCode(asset.Kind)}|{ToCode(asset.Disposition)}|{asset.ContentHash}|{asset.StorageKey}";

    private static MailRouteEvaluationResult MapMailRouteDecision(
        IntakeMailRouteDecisionEntity entity)
    {
        var hasAnySelectionValue = entity.RouteOwnerCode is not null
            || entity.RouteKind is not null
            || entity.WorkProviderCode is not null;
        var hasCompleteSelection = entity.RouteOwnerCode is not null
            && entity.RouteKind is not null
            && entity.WorkProviderCode is not null;
        if (hasAnySelectionValue != hasCompleteSelection)
        {
            throw new InvalidDataException(
                "The persisted mail-route selection is incomplete.");
        }

        var hasAnyEffectiveSenderValue = entity.EffectiveSenderAddress is not null
            || entity.EffectiveSenderSourceLabel is not null;
        var hasCompleteEffectiveSender = entity.EffectiveSenderAddress is not null
            && entity.EffectiveSenderSourceLabel is not null;
        if (hasAnyEffectiveSenderValue != hasCompleteEffectiveSender)
        {
            throw new InvalidDataException(
                "The persisted effective sender identity is incomplete.");
        }

        return new(
            ParseMailRouteDisposition(entity.Disposition),
            hasCompleteSelection
                ? new(
                    entity.RouteOwnerCode!,
                    ParseMailRouteKind(entity.RouteKind!),
                    entity.WorkProviderCode!)
                : null,
            DeserializeEnvelope<IReadOnlyList<MailRoutePredicateResult>>(entity.PredicatesJson),
            entity.Reason,
            entity.PolicyKey,
            entity.PolicyVersion,
            DeserializeEnvelope<IReadOnlyList<MailRouteIdentity>>(entity.TransportIdentitiesJson),
            DeserializeEnvelope<IReadOnlyList<MailRouteIdentity>>(entity.OriginalIdentitiesJson),
            hasCompleteEffectiveSender
                ? new(entity.EffectiveSenderAddress!, entity.EffectiveSenderSourceLabel!)
                : null);
    }

    internal static IntakeAssetRecord MapAsset(IntakeAssetEntity entity) => new(
        entity.Id,
        entity.SourceLabel,
        entity.FileName,
        entity.MediaType,
        ParseAssetKind(entity.Kind),
        ParseAssetDisposition(entity.Disposition),
        entity.ContentLength,
        entity.ContentHash,
        entity.StorageKey,
        entity.PageNumber,
        entity.BoundsJson is null ? null : DeserializeEnvelope<IntakeAssetBounds>(entity.BoundsJson),
        entity.WidthPixels,
        entity.HeightPixels);

    /// <summary>
    /// The message subject, read back from the recorded evidence.
    /// </summary>
    /// <remarks>
    /// The subject is not a column; it is evidence, recorded by the reader that
    /// found it. Reading it here rather than adding a column keeps one writer
    /// for the fact and avoids a migration for something the Inbox only
    /// displays.
    /// </remarks>
    /// <summary>
    /// Internal rather than private: <c>EfUnidentifiedStore.ListQueueAsync</c>
    /// reuses this to extract the e-mail subject for an Unidentified queue
    /// row, so the JSON evidence format has exactly one reader.
    /// </summary>
    internal static string? ReadSubject(string evidenceJson)
    {
        try
        {
            return DeserializeEvidence(evidenceJson)
                .FirstOrDefault(item => item.Source == IntakeEvidenceSource.Subject)
                ?.Detail;
        }
        catch (JsonException)
        {
            // A row whose evidence cannot be read still has a file name and a
            // decision, and the Inbox is more useful showing those than failing.
            return null;
        }
    }

    internal static string SerializeEvidence(IReadOnlyList<IntakeEvidence> evidence) =>
        SerializeEnvelope<IReadOnlyList<PersistedEvidence>>(evidence.Select(item => new PersistedEvidence(
            IntakeEvidenceSourceCodes.ToCode(item.Source),
            ToCode(item.Strength),
            ToCode(item.Finding),
            item.Signal,
            item.Detail,
            item.MatcherKey,
            item.MatcherVersion)).ToArray());

    internal static IntakeEvidence[] DeserializeEvidence(string json) =>
        (DeserializeEnvelope<IReadOnlyList<PersistedEvidence>>(json) ?? [])
        .Select(item => new IntakeEvidence(
            IntakeEvidenceSourceCodes.Parse(item.Source),
            ParseEvidenceStrength(item.Strength),
            ParseEvidenceFinding(item.Finding),
            item.Signal,
            item.Detail,
            item.MatcherKey,
            item.MatcherVersion))
        .ToArray();

    internal static string SerializeFields(IReadOnlyList<InstructionReviewField> fields) =>
        SerializeEnvelope<IReadOnlyList<PersistedField>>(fields.Select(field => new PersistedField(
            field.Name,
            field.SuggestedValue,
            field.Candidates.Select(candidate => new PersistedFieldCandidate(
                candidate.Value,
                IntakeEvidenceSourceCodes.ToCode(candidate.Source),
                candidate.SourceLabel)).ToArray(),
            field.IsDefaulted,
            field.HasConflict)).ToArray());

    internal static InstructionReviewField[] DeserializeFields(string json) =>
        (DeserializeEnvelope<IReadOnlyList<PersistedField>>(json) ?? [])
        .Select(field => new InstructionReviewField(
            field.Name,
            field.SuggestedValue,
            field.Candidates.Select(candidate => new InstructionFieldCandidate(
                candidate.Value,
                IntakeEvidenceSourceCodes.Parse(candidate.Source),
                candidate.SourceLabel)).ToArray(),
            field.IsDefaulted,
            field.HasConflict))
        .ToArray();

    internal static string SerializeEnvelope<T>(T data) =>
        JsonSerializer.Serialize(new VersionedEnvelope<T>(JsonVersion, data), JsonOptions);

    private static T DeserializeEnvelope<T>(string json)
    {
        var envelope = JsonSerializer.Deserialize<VersionedEnvelope<T>>(json, JsonOptions)
            ?? throw new InvalidDataException("The persisted intake JSON envelope is missing.");
        if (envelope.Version != JsonVersion)
        {
            throw new InvalidDataException($"Unsupported persisted intake JSON version '{envelope.Version}'.");
        }

        return envelope.Data
            ?? throw new InvalidDataException("The persisted intake JSON envelope has no data.");
    }

    private static string ToCode(MailRouteDisposition value) => value switch
    {
        MailRouteDisposition.Accepted => "accepted",
        MailRouteDisposition.NoMatch => "no_match",
        MailRouteDisposition.NeedsSorting => "needs_sorting",
        _ => throw UnknownEnum(value)
    };

    private static MailRouteDisposition ParseMailRouteDisposition(string value) => value switch
    {
        "accepted" => MailRouteDisposition.Accepted,
        "no_match" => MailRouteDisposition.NoMatch,
        "needs_sorting" => MailRouteDisposition.NeedsSorting,
        _ => throw UnknownCode("mail-route disposition", value)
    };

    private static string ToCode(MailClassificationOutcome value) => value switch
    {
        MailClassificationOutcome.Classified => "classified",
        MailClassificationOutcome.Ambiguous => "ambiguous",
        MailClassificationOutcome.Unclassified => "unclassified",
        _ => throw UnknownEnum(value)
    };

    private static MailClassificationOutcome ParseMailClassificationOutcome(string value) => value switch
    {
        "classified" => MailClassificationOutcome.Classified,
        "ambiguous" => MailClassificationOutcome.Ambiguous,
        "unclassified" => MailClassificationOutcome.Unclassified,
        _ => throw UnknownCode("mail-classification outcome", value)
    };

    internal static string ToCode(CaseType value) => value switch
    {
        CaseType.Inspection => "inspection",
        CaseType.Audit => "audit",
        CaseType.InspectionAndAudit => "inspection_and_audit",
        _ => throw UnknownEnum(value)
    };

    private static CaseType ParseCaseType(string value) => value switch
    {
        "inspection" => CaseType.Inspection,
        "audit" => CaseType.Audit,
        "inspection_and_audit" => CaseType.InspectionAndAudit,
        _ => throw UnknownCode("case type", value)
    };

    internal static string ToCode(AuditAssessment value) => value switch
    {
        AuditAssessment.Repairable => "repairable",
        AuditAssessment.TotalLoss => "total_loss",
        _ => throw UnknownEnum(value)
    };

    private static AuditAssessment ParseAuditAssessment(string value) => value switch
    {
        "repairable" => AuditAssessment.Repairable,
        "total_loss" => AuditAssessment.TotalLoss,
        _ => throw UnknownCode("audit assessment", value)
    };

    private static string ToCode(CaseMatchOutcome value) => value switch
    {
        CaseMatchOutcome.UniqueMatch => "unique_match",
        CaseMatchOutcome.NoMatch => "no_match",
        CaseMatchOutcome.NoKeys => "no_keys",
        CaseMatchOutcome.Ambiguous => "ambiguous",
        _ => throw UnknownEnum(value)
    };

    private static CaseMatchOutcome ParseCaseMatchOutcome(string value) => value switch
    {
        "unique_match" => CaseMatchOutcome.UniqueMatch,
        "no_match" => CaseMatchOutcome.NoMatch,
        "no_keys" => CaseMatchOutcome.NoKeys,
        "ambiguous" => CaseMatchOutcome.Ambiguous,
        _ => throw UnknownCode("case-match outcome", value)
    };

    private static string ToCode(MailDirection value) => value switch
    {
        MailDirection.Received => "received",
        MailDirection.Sent => "sent",
        _ => throw UnknownEnum(value)
    };

    private static MailDirection ParseMailDirection(string value) => value switch
    {
        "received" => MailDirection.Received,
        "sent" => MailDirection.Sent,
        _ => throw UnknownCode("mail direction", value)
    };

    private static string ToCode(MailRouteKind value) => value switch
    {
        MailRouteKind.DirectProvider => "direct_provider",
        MailRouteKind.Intermediary => "intermediary",
        _ => throw UnknownEnum(value)
    };

    private static MailRouteKind ParseMailRouteKind(string value) => value switch
    {
        "direct_provider" => MailRouteKind.DirectProvider,
        "intermediary" => MailRouteKind.Intermediary,
        _ => throw UnknownCode("mail-route kind", value)
    };

    internal static string ToCode(IntakeDecision value) => value switch
    {
        IntakeDecision.CaseCreated => "case_created",
        IntakeDecision.NeedsSorting => "needs_sorting",
        IntakeDecision.BlockedIntake => "blocked_intake",
        IntakeDecision.Unsupported => "unsupported",
        IntakeDecision.OcrRequired => "ocr_required",
        IntakeDecision.TechnicalFailure => "technical_failure",
        IntakeDecision.ImageIntakeRegistered => "image_intake_registered",
        _ => throw UnknownEnum(value)
    };

    internal static IntakeDecision ParseDecision(string value) => value switch
    {
        "case_created" => IntakeDecision.CaseCreated,
        "needs_sorting" => IntakeDecision.NeedsSorting,
        "blocked_intake" => IntakeDecision.BlockedIntake,
        "unsupported" => IntakeDecision.Unsupported,
        "ocr_required" => IntakeDecision.OcrRequired,
        "technical_failure" => IntakeDecision.TechnicalFailure,
        "image_intake_registered" => IntakeDecision.ImageIntakeRegistered,
        _ => throw UnknownCode("decision", value)
    };

    /// <summary>
    /// Internal rather than private: <c>EfDashboardQueries</c> reuses this so
    /// a channel-scoped count (e.g. mail received today) asks the one place
    /// this mapping is defined instead of duplicating the channel code.
    /// </summary>
    internal static string ToCode(IntakeSourceChannel value) => value switch
    {
        IntakeSourceChannel.ManualUpload => "manual_upload",
        IntakeSourceChannel.Mailbox => "mailbox",
        IntakeSourceChannel.Automation => "automation",
        IntakeSourceChannel.ProviderApi => "provider_api",
        _ => throw UnknownEnum(value)
    };

    /// <summary>
    /// Internal rather than private: <c>EfUnidentifiedStore.ListQueueAsync</c>
    /// reuses this to classify an Unidentified row's media kind, so the
    /// persisted channel code has exactly one parser.
    /// </summary>
    internal static IntakeSourceChannel ParseSourceChannel(string value) => value switch
    {
        "manual_upload" => IntakeSourceChannel.ManualUpload,
        "mailbox" => IntakeSourceChannel.Mailbox,
        "automation" => IntakeSourceChannel.Automation,
        "provider_api" => IntakeSourceChannel.ProviderApi,
        _ => throw UnknownCode("source channel", value)
    };

    private static string ToCode(IntakeEvidenceStrength value) => value switch
    {
        IntakeEvidenceStrength.Strong => "strong",
        IntakeEvidenceStrength.Weak => "weak",
        _ => throw UnknownEnum(value)
    };

    private static IntakeEvidenceStrength ParseEvidenceStrength(string value) => value switch
    {
        "strong" => IntakeEvidenceStrength.Strong,
        "weak" => IntakeEvidenceStrength.Weak,
        _ => throw UnknownCode("evidence strength", value)
    };

    private static string ToCode(IntakeEvidenceFinding value) => value switch
    {
        IntakeEvidenceFinding.SupportsPrincipal => "supports_principal",
        IntakeEvidenceFinding.ContradictsTransport => "contradicts_transport",
        IntakeEvidenceFinding.ExtractedField => "extracted_field",
        IntakeEvidenceFinding.ConflictingField => "conflicting_field",
        IntakeEvidenceFinding.MissingField => "missing_field",
        IntakeEvidenceFinding.Information => "information",
        IntakeEvidenceFinding.AcceptedTriageMatch => "accepted_triage_match",
        _ => throw UnknownEnum(value)
    };

    private static IntakeEvidenceFinding ParseEvidenceFinding(string value) => value switch
    {
        "supports_principal" => IntakeEvidenceFinding.SupportsPrincipal,
        "contradicts_transport" => IntakeEvidenceFinding.ContradictsTransport,
        "extracted_field" => IntakeEvidenceFinding.ExtractedField,
        "conflicting_field" => IntakeEvidenceFinding.ConflictingField,
        "missing_field" => IntakeEvidenceFinding.MissingField,
        "information" => IntakeEvidenceFinding.Information,
        "accepted_triage_match" => IntakeEvidenceFinding.AcceptedTriageMatch,
        _ => throw UnknownCode("evidence finding", value)
    };

    private static string ToCode(IntakeAssetKind value) => value switch
    {
        IntakeAssetKind.Source => "source",
        IntakeAssetKind.Attachment => "attachment",
        IntakeAssetKind.InlineImage => "inline_image",
        IntakeAssetKind.EmbeddedImage => "embedded_image",
        _ => throw UnknownEnum(value)
    };

    /// <summary>
    /// The evidence photographs of a case's instruction receipts (origin
    /// receipt plus manually linked ones), resolved through the one
    /// <see cref="InstructionEvidenceImages"/> selection rule.
    /// </summary>
    public async Task<IReadOnlyList<CaseEvidenceImage>> ListForCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var originIds = await context.Cases
            .AsNoTracking()
            .Where(item => item.Id == caseId)
            .Select(item => item.OriginIntakeReceiptId)
            .ToListAsync(cancellationToken);
        var linkedIds = await context.CaseIntakeLinks
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .Select(item => item.IntakeReceiptId)
            .ToListAsync(cancellationToken);
        var receiptIds = originIds
            .Concat(linkedIds)
            .Distinct()
            .ToArray();
        if (receiptIds.Length == 0)
        {
            return [];
        }

        // DOCS-007: Box is the record. Where intake's photographs have been
        // registered as case documents, the gallery reads them and serves them
        // through the case-document route — the intake blob is staging, not
        // custody, and it ages out. A case accepted before those records
        // existed still renders from its retained asset rather than going
        // blank, which is the additive transition the ticket required.
        var documentImages = await (
                from occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                    on occurrence.VersionId equals version.Id
                where occurrence.CaseId == caseId
                    && occurrence.SemanticRole == DocumentSemanticRole.Image
                    && version.IsCurrent
                    && !version.IsLogicallyRemoved
                    && version.CustodyStatus == DocumentCustodyStatus.Confirmed
                orderby occurrence.Ordinal
                // DOCS-010: named, not positional. Built positionally, the two
                // adjacent Guid slots were filled in the wrong order — the
                // document id landed in OccurrenceId and every gallery URL 404d
                // before Box was reached. There is no intake asset behind an
                // image served from Box, so ReceiptId and AssetId are empty.
                select new CaseEvidenceImage(
                    ReceiptId: Guid.Empty,
                    AssetId: Guid.Empty,
                    FileName: version.FileName,
                    MediaType: version.MediaType,
                    ContentLength: version.ContentLength,
                    OccurrenceId: occurrence.Id,
                    VersionId: version.Id))
            .ToArrayAsync(cancellationToken);
        if (documentImages.Length > 0)
        {
            return documentImages;
        }

        var assets = await context.IntakeAssets
            .AsNoTracking()
            .Where(item => receiptIds.Contains(item.IntakeReceiptId)
                && (item.Kind == "attachment" || item.Kind == "embedded_image"))
            .ToListAsync(cancellationToken);
        var byRecordId = assets.ToDictionary(item => item.Id, item => item.IntakeReceiptId);
        return InstructionEvidenceImages.Select(assets.Select(MapAsset))
            .Select(record => new CaseEvidenceImage(
                byRecordId[record.Id],
                record.Id,
                record.FileName,
                record.MediaType,
                record.ContentLength))
            .ToArray();
    }

    private static IntakeAssetKind ParseAssetKind(string value) => value switch
    {
        "source" => IntakeAssetKind.Source,
        "attachment" => IntakeAssetKind.Attachment,
        "inline_image" => IntakeAssetKind.InlineImage,
        "embedded_image" => IntakeAssetKind.EmbeddedImage,
        _ => throw UnknownCode("asset kind", value)
    };

    private static string ToCode(IntakeAssetDisposition value) => value switch
    {
        IntakeAssetDisposition.Source => "source",
        IntakeAssetDisposition.Attachment => "attachment",
        IntakeAssetDisposition.Inline => "inline",
        IntakeAssetDisposition.Embedded => "embedded",
        _ => throw UnknownEnum(value)
    };

    private static IntakeAssetDisposition ParseAssetDisposition(string value) => value switch
    {
        "source" => IntakeAssetDisposition.Source,
        "attachment" => IntakeAssetDisposition.Attachment,
        "inline" => IntakeAssetDisposition.Inline,
        "embedded" => IntakeAssetDisposition.Embedded,
        _ => throw UnknownCode("asset disposition", value)
    };

    private static InvalidDataException UnknownCode(string kind, string value) =>
        new($"Unknown persisted intake {kind} code '{value}'.");

    private static InvalidOperationException UnknownEnum<T>(T value) where T : struct, Enum =>
        new($"Unknown {typeof(T).Name} value '{Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture)}'.");

    private static void EnsureMatchingContent(string existingSourceHash, string sourceHash)
    {
        if (!string.Equals(existingSourceHash, sourceHash, StringComparison.Ordinal))
        {
            throw new IntakeSourceIdentityConflictException();
        }
    }

    private static bool IsRetryableConcurrencyFailure(Exception exception) => exception switch
    {
        SqlException { Number: 1205 or 2601 or 2627 } => true,
        _ when exception.InnerException is not null => IsRetryableConcurrencyFailure(exception.InnerException),
        _ => false
    };

    private sealed record VersionedEnvelope<T>(int Version, T Data);
    private sealed record PersistedEvidence(
        string Source,
        string Strength,
        string Finding,
        string Signal,
        string Detail,
        string? MatcherKey = null,
        int? MatcherVersion = null);
    private sealed record PersistedField(
        string Name,
        string? SuggestedValue,
        IReadOnlyList<PersistedFieldCandidate> Candidates,
        bool IsDefaulted,
        bool HasConflict);
    private sealed record PersistedFieldCandidate(string Value, string Source, string SourceLabel);
    private sealed record IntakeReceiptEventDetails(
        string Decision,
        string SourceChannel,
        string ExternalReceiptToken,
        string SourceHash);
}
