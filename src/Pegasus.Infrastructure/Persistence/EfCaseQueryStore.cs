using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfCaseQueryStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : ICaseQueryStore
{
    public async Task<SearchCasesResult> SearchAsync(
        SearchCasesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = SearchRows(context);
        var filters = query.Filters;
        if (filters.Query is { } globalQuery)
        {
            var compactRegistrationQuery = string.Concat(
                globalQuery.Where(char.IsLetterOrDigit)).ToUpperInvariant();
            var hasRegistrationQuery = compactRegistrationQuery.Length > 0;
            var principalQuery = globalQuery.ToUpperInvariant();
            var hasEngineerQuery = Guid.TryParse(globalQuery, out var engineerQuery);
            rows = rows.Where(item =>
                item.Reference.Contains(globalQuery)
                || item.AuditReference != null && item.AuditReference.Contains(globalQuery)
                || item.Registration != null
                    && hasRegistrationQuery
                    && item.Registration.Replace(" ", "").Replace("-", "")
                        .Contains(compactRegistrationQuery)
                || item.Claimant != null && item.Claimant.Contains(globalQuery)
                || item.ClaimNumber != null && item.ClaimNumber.Contains(globalQuery)
                || item.Principal == principalQuery
                || item.State.Contains(globalQuery)
                || hasEngineerQuery && item.EngineerId == engineerQuery
                || item.Origin.Contains(globalQuery));
        }

        if (filters.CaseReference is { } caseReference)
        {
            rows = rows.Where(item => item.Reference.Contains(caseReference));
        }
        if (filters.Registration is { } registration)
        {
            rows = rows.Where(item => item.Registration != null
                && item.Registration.Replace(" ", "").Replace("-", "") == registration);
        }
        if (filters.Claimant is { } claimant)
        {
            rows = rows.Where(item => item.Claimant != null && item.Claimant.Contains(claimant));
        }
        if (filters.ClaimNumber is { } claimNumber)
        {
            rows = rows.Where(item => item.ClaimNumber != null && item.ClaimNumber.Contains(claimNumber));
        }
        if (filters.Principal is { } principal)
        {
            rows = rows.Where(item => item.Principal == principal);
        }
        if (filters.State is { } state)
        {
            var stateName = state.ToString();
            rows = rows.Where(item => item.State == stateName);
        }
        if (filters.EngineerId is { } engineerId)
        {
            rows = rows.Where(item => item.EngineerId == engineerId);
        }
        if (filters.ReceivedDate is { } receivedDate)
        {
            var receivedStart = LondonCalendar.StartOfDay(receivedDate);
            var receivedEnd = LondonCalendar.StartOfNextDay(receivedDate);
            rows = rows.Where(item => item.ReceivedAtUtc >= receivedStart
                && (receivedEnd == null || item.ReceivedAtUtc < receivedEnd));
        }
        if (filters.InstructionDate is { } instructionDate)
        {
            rows = rows.Where(item => item.InstructionDate == instructionDate);
        }
        if (filters.FromDate is { } fromDate)
        {
            var from = LondonCalendar.StartOfDay(fromDate);
            rows = rows.Where(item => item.ReceivedAtUtc >= from);
        }
        if (filters.ToDate is { } toDate && LondonCalendar.StartOfNextDay(toDate) is { } to)
        {
            rows = rows.Where(item => item.ReceivedAtUtc < to);
        }
        if (filters.Origin is { } origin)
        {
            rows = rows.Where(item => item.Origin == origin);
        }

        var skip = checked((query.Page - 1) * query.PageSize);
        IOrderedQueryable<SearchRow> ordered = query.Order switch
        {
            CaseSearchOrder.ReceivedAsc => rows.OrderBy(item => item.ReceivedAtUtc),
            CaseSearchOrder.ReferenceAsc => rows.OrderBy(item => item.Reference),
            CaseSearchOrder.ReferenceDesc => rows.OrderByDescending(item => item.Reference),
            CaseSearchOrder.RegistrationAsc => rows.OrderBy(item => item.Registration),
            CaseSearchOrder.RegistrationDesc => rows.OrderByDescending(item => item.Registration),
            CaseSearchOrder.ClaimantAsc => rows.OrderBy(item => item.Claimant),
            CaseSearchOrder.ClaimantDesc => rows.OrderByDescending(item => item.Claimant),
            CaseSearchOrder.PrincipalAsc => rows.OrderBy(item => item.Principal),
            CaseSearchOrder.PrincipalDesc => rows.OrderByDescending(item => item.Principal),
            _ => rows.OrderByDescending(item => item.ReceivedAtUtc)
        };
        var page = await ordered
            .ThenBy(item => item.Reference)
            .ThenBy(item => item.CaseId)
            .Skip(skip)
            .Take(query.PageSize + 1)
            .ToArrayAsync(cancellationToken);
        var hasNextPage = page.Length > query.PageSize;
        var items = page
            .Take(query.PageSize)
            .Select(MapSearchItem)
            .ToArray();

        return new(
            items,
            query.Page,
            query.PageSize,
            query.Page > 1,
            hasNextPage);
    }

    public async Task<CaseDetails?> GetAsync(
        GetCaseQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workflow = await context.CaseWorkflows
            .AsNoTracking()
            .Include(item => item.Case)
                .ThenInclude(item => item.Principal)
            .Include(item => item.ReportApproval)
            .Include(item => item.ReportSentEvidence)
            .Include(item => item.DueWork)
            .SingleOrDefaultAsync(item => item.CaseId == query.CaseId, cancellationToken);
        if (workflow is null)
        {
            return null;
        }

        var summaryRow = await SearchRows(context)
            .SingleAsync(item => item.CaseId == query.CaseId, cancellationToken);
        var documents = await ReadDocumentsAsync(context, query.CaseId, cancellationToken);
        var requestUploadLinks = await context.Set<RequestUploadLinkEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == query.CaseId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .Take(100)
            .Select(item => new CaseRequestUploadSummary(
                item.Id,
                item.Status,
                item.CreatedAtUtc,
                item.ExpiresAtUtc,
                item.RevokedAtUtc,
                item.AcceptedFileCount,
                item.AcceptedByteCount,
                item.Version))
            .ToArrayAsync(cancellationToken);
        var availableReportSentEvidence = await context.CaseReportSentEvidence
            .AsNoTracking()
            .Where(item => item.CaseId == null
                && item.DiscoveredByKind == nameof(ActorKind.SystemWorker))
            .OrderByDescending(item => item.SentAtUtc)
            .ThenBy(item => item.Id)
            .Take(100)
            .ToArrayAsync(cancellationToken);
        var querySelection = MailOperationalDestinationPolicy.Query(
            MailOperationalDestination.Queries);
        var queryFamilies = querySelection.Families
            .Select(MailTaxonomy.CategoryName)
            .ToArray();
        var exactQuery = querySelection.ExactClassification;
        var exactDirection = exactQuery?.Direction.ToString().ToLowerInvariant();
        var associatedReceiptIds = context.IntakeManualAssociations
            .AsNoTracking()
            .Where(item => item.CaseId == query.CaseId)
            .Select(item => item.IntakeReceiptId)
            .Union(context.CaseIntakeLinks
                .AsNoTracking()
                .Where(item => item.CaseId == query.CaseId)
                .Select(item => item.IntakeReceiptId));
        var classifiedQueryReceipts = await context.IntakeReceipts
            .AsNoTracking()
            .Where(item => associatedReceiptIds.Contains(item.Id)
                && item.SourceChannel == EfIntakeReceiptStore.ToCode(IntakeSourceChannel.Mailbox)
                && item.MailClassificationDecision != null
                && item.MailClassificationDecision.Outcome == "classified"
                && ((item.MailClassificationDecision.Direction == "received"
                        && item.MailClassificationDecision.Family != null
                        && queryFamilies.Contains(item.MailClassificationDecision.Family))
                    || (exactQuery != null
                        && item.MailClassificationDecision.OtherName == null
                        && item.MailClassificationDecision.Direction == exactDirection
                        && item.MailClassificationDecision.Family == exactQuery.Name
                        && item.MailClassificationDecision.Subtype == exactQuery.Subtype)))
            .Select(item => new
            {
                item.Id,
                item.ExternalReceiptToken,
                Classification = item.MailClassificationDecision!,
                EffectiveSenderAddress = item.MailRouteDecision == null
                    ? null
                    : item.MailRouteDecision.EffectiveSenderAddress
            })
            .ToArrayAsync(cancellationToken);
        var queryAssociations = await CurrentIntakeAssociations.ReadAsync(
            context,
            classifiedQueryReceipts.Select(item => item.Id).ToArray(),
            cancellationToken);
        var linkedQueryReceipts = classifiedQueryReceipts
            .Where(item => queryAssociations.Current.TryGetValue(item.Id, out var association)
                && association.CaseId == query.CaseId)
            .ToArray();
        var linkedQueryTokens = linkedQueryReceipts
            .Select(item => item.ExternalReceiptToken)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var retainedQueryMessages = linkedQueryTokens.Length == 0
            ? []
            : await context.RetainedMailboxMessages
                .AsNoTracking()
                .Where(item => linkedQueryTokens.Contains(item.ExternalReceiptToken))
                .Select(item => new
                {
                    item.Id,
                    item.ExternalReceiptToken,
                    item.ReceivedAtUtc,
                    item.SenderDisplayName,
                    item.SenderAddress,
                    item.Subject
                })
                .ToArrayAsync(cancellationToken);
        var queryReceiptByToken = linkedQueryReceipts.ToDictionary(
            item => item.ExternalReceiptToken,
            StringComparer.Ordinal);
        var queryEmails = retainedQueryMessages
            .Select(item =>
            {
                var receipt = queryReceiptByToken[item.ExternalReceiptToken];
                return new CaseQueryEmail(
                    item.Id,
                    item.ReceivedAtUtc,
                    receipt.EffectiveSenderAddress,
                    item.SenderDisplayName,
                    item.SenderAddress,
                    item.Subject,
                    EfIntakeReceiptStore.MapMailClassificationDecision(receipt.Classification)
                        .Category!);
            })
            .OrderByDescending(item => item.ReceivedAtUtc)
            .ThenBy(item => item.RetainedMessageId)
            .ToArray();
        var history = await context.CaseWorkflowEvents
            .AsNoTracking()
            .Where(item => item.CaseId == query.CaseId)
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(200)
            .Select(item => new CaseHistoryEntry(
                item.EventType,
                item.ActorSubjectId,
                item.ActorKind,
                item.OccurredAtUtc,
                item.Reason,
                item.BeforeVersion,
                item.AfterVersion))
            .ToArrayAsync(cancellationToken);
        var activeLease = workflow.EditLeaseHolder is { } holder
            && workflow.EditLeaseExpiresAtUtc is { } expiresAtUtc
            && workflow.EditLeaseOperationKey is { Length: > 0 } operationKey
            && CaseEditAuthority.IsHeld(expiresAtUtc, timeProvider.GetUtcNow())
                ? new CaseEditLeaseSnapshot(
                    holder,
                    CaseMutationGuard.RetainedHolderKind(workflow.EditLeaseHolderKind),
                    expiresAtUtc,
                    operationKey,
                    workflow.EditLeaseGeneration)
                : null;

        return new CaseDetails(
            MapSearchItem(summaryRow),
            MapWorkflow(workflow),
            activeLease,
            documents,
            workflow.Case.CustodyRootRemoteId,
            ParseCustodyState(workflow.Case.CustodyState),
            requestUploadLinks,
            availableReportSentEvidence.Select(MapRetainedEvidence).ToArray(),
            history)
        {
            QueryEmails = queryEmails
        };
    }

    private static CaseCustodyState ParseCustodyState(string value) => value switch
    {
        "pending" => CaseCustodyState.Pending,
        "confirmed" => CaseCustodyState.Confirmed,
        "failed" => CaseCustodyState.Failed,
        _ => throw new InvalidDataException(
            $"Unknown persisted case custody state '{value}'.")
    };

    private static IQueryable<SearchRow> SearchRows(PegasusDbContext context) =>
        from workflow in context.CaseWorkflows.AsNoTracking()
        join caseEntity in context.Set<CaseEntity>().AsNoTracking()
            on workflow.CaseId equals caseEntity.Id
        join principal in context.Set<PrincipalEntity>().AsNoTracking()
            on caseEntity.PrincipalId equals principal.Id
        join receipt in context.Set<IntakeReceiptEntity>().AsNoTracking()
            on caseEntity.OriginIntakeReceiptId equals receipt.Id
        join draftCandidate in context.Set<InstructionDraftEntity>().AsNoTracking()
            on receipt.Id equals draftCandidate.IntakeReceiptId into drafts
        from draft in drafts.DefaultIfEmpty()
        select new SearchRow
        {
            CaseId = caseEntity.Id,
            Reference = caseEntity.Reference,
            AuditReference = caseEntity.AuditReference,
            CaseType = caseEntity.Type,
            Principal = principal.Code,
            State = workflow.State,
            EngineerId = workflow.AssignedEngineerId,
            Registration = draft == null ? null : draft.VehicleRegistration,
            Claimant = draft == null ? null : draft.ClaimantName,
            ClaimNumber = draft == null ? null : draft.ClaimNumber,
            VehicleMake = draft == null ? null : draft.VehicleMake,
            VehicleModel = draft == null ? null : draft.VehicleModel,
            AccidentCircumstances = draft == null ? null : draft.AccidentCircumstances,
            ReceivedAtUtc = receipt.ReceivedAtUtc,
            InstructionDate = draft == null ? null : draft.InstructionDate,
            Origin = receipt.SourceChannel,
            CreatedAtUtc = caseEntity.CreatedAtUtc,
            NextChaseAtUtc = workflow.DueWork == null ? null : workflow.DueWork!.NextChaseAtUtc,
            InstructionComplete = caseEntity.InstructionComplete,
            ImagesComplete = caseEntity.ImagesComplete
        };

    private static async Task<IReadOnlyList<CaseDocument>> ReadDocumentsAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var documentEntities = await context.Set<CaseDocumentEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderBy(item => item.Id)
            .Take(500)
            .ToArrayAsync(cancellationToken);
        if (documentEntities.Length == 0)
        {
            return [];
        }

        var documentIds = documentEntities.Select(item => item.Id).ToArray();
        var occurrences = await context.Set<DocumentOccurrenceEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == caseId && documentIds.Contains(item.DocumentId))
            .OrderBy(item => item.RecordedAtUtc)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
        var versions = await context.Set<DocumentVersionEntity>()
            .AsNoTracking()
            .Where(item => documentIds.Contains(item.DocumentId))
            .OrderByDescending(item => item.Version)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);

        return documentEntities.Select(document => new CaseDocument(
                document.Id,
                caseId,
                occurrences
                    .Where(item => item.DocumentId == document.Id)
                    .Select(item => new DocumentOccurrence(
                        item.Id,
                        item.CaseId,
                        item.DocumentId,
                        item.VersionId,
                        item.SemanticRole,
                        item.Source,
                        item.SourceOccurrenceIdentity,
                        item.RecordedAtUtc,
                        item.ThirdPartyVehicleConfirmedAtUtc,
                        item.ThirdPartyVehicleConfirmationReason))
                    .ToArray(),
                versions
                    .Where(item => item.DocumentId == document.Id)
                    .Select(item => new DocumentVersion(
                        item.Id,
                        item.DocumentId,
                        item.Version,
                        item.FileName,
                        item.MediaType,
                        item.ContentLength,
                        item.Sha256,
                        item.CustodyStatus,
                        item.CreatedAtUtc,
                        item.CreatedBy,
                        item.IsCurrent,
                        item.IsLogicallyRemoved,
                        item.RemovalReason))
                    .ToArray()))
            .ToArray();
    }

    private static CaseSearchItem MapSearchItem(SearchRow item) => new(
        item.CaseId,
        item.Reference,
        item.AuditReference,
        ParseCaseType(item.CaseType),
        item.Principal,
        Enum.Parse<CaseLifecycleState>(item.State),
        item.EngineerId,
        item.Registration,
        item.Claimant,
        item.ClaimNumber,
        item.ReceivedAtUtc,
        item.InstructionDate,
        item.Origin,
        item.CreatedAtUtc,
        item.NextChaseAtUtc,
        item.VehicleMake,
        item.VehicleModel,
        item.AccidentCircumstances)
    {
        InstructionComplete = item.InstructionComplete,
        ImagesComplete = item.ImagesComplete
    };

    internal static CaseType ParseCaseType(string value)
    {
        if (string.Equals(value, "inspection", StringComparison.OrdinalIgnoreCase))
        {
            return CaseType.Inspection;
        }
        if (string.Equals(value, "audit", StringComparison.OrdinalIgnoreCase))
        {
            return CaseType.Audit;
        }
        if (string.Equals(
                value,
                "inspection_and_audit",
                StringComparison.OrdinalIgnoreCase))
        {
            return CaseType.InspectionAndAudit;
        }

        throw new InvalidDataException(
            $"Case data contains unsupported type code '{value}'.");
    }

    private static CaseWorkflowRecord MapWorkflow(CaseWorkflowEntity entity)
    {
        var workflow = new CaseWorkflowRecord(
            entity.CaseId,
            new CaseIdentity(
                entity.CaseId,
                entity.Case.Principal.Code,
                entity.Case.Year,
                entity.Case.Sequence,
                entity.Case.Reference,
                entity.Case.AuditReference),
            Enum.Parse<CaseLifecycleState>(entity.State),
            entity.AssignedEngineerId,
            entity.ReportApproval is null
                ? null
                : new ReportApprovalEvidence(
                    entity.ReportApproval.Id,
                    entity.ReportApproval.ArtifactIdentity,
                    entity.ReportApproval.ArtifactSha256,
                    MapStaffActor(
                        entity.ReportApproval.ApprovedByKind,
                        entity.ReportApproval.ApprovedBySubjectId,
                        entity.ReportApproval.ApprovedByRolesJson),
                    entity.ReportApproval.ApprovedAtUtc),
            entity.ReportSentEvidence is null ? null : MapLinkedEvidence(entity.ReportSentEvidence),
            entity.DueWork is null
                ? null
                : new CaseDueWork(
                    entity.DueWork.CaseId,
                    entity.Case.Reference,
                    entity.DueWork.MissingMaterialReason,
                    entity.DueWork.DueBy,
                    Enum.Parse<CaseDueWorkState>(entity.DueWork.State),
                    entity.DueWork.NextChaseAtUtc,
                    entity.DueWork.HeldAtUtc,
                    entity.DueWork.RemainingChaseIntervalTicks is null
                        ? null
                        : TimeSpan.FromTicks(entity.DueWork.RemainingChaseIntervalTicks.Value),
                    entity.DueWork.MostRecentChannel,
                    entity.DueWork.MostRecentOutcome,
                    entity.DueWork.MostRecentNote,
                    entity.DueWork.Version),
            entity.ClosureOutcome is null
                ? null
                : Enum.Parse<CaseClosureOutcome>(entity.ClosureOutcome),
            entity.OriginalCaseId,
            entity.ReplacementCaseId,
            entity.Version)
        {
            SignOffEngineerId = entity.SignOffEngineerId
        };
        if (entity.ArchivedAtUtc is null)
        {
            if (entity.ArchivedByKind is not null
                || entity.ArchivedBySubjectId is not null
                || entity.ArchivedByRolesJson is not null
                || entity.ArchiveReason is not null)
            {
                throw new InvalidDataException("Case archive metadata is incomplete.");
            }

            return workflow;
        }
        if (entity.ArchivedByKind is null
            || entity.ArchivedBySubjectId is null
            || entity.ArchivedByRolesJson is null
            || entity.ArchiveReason is null)
        {
            throw new InvalidDataException("Case archive metadata is incomplete.");
        }

        return workflow with
        {
            Archive = new(
                entity.ArchivedAtUtc.Value,
                MapStaffActor(
                    entity.ArchivedByKind,
                    entity.ArchivedBySubjectId,
                    entity.ArchivedByRolesJson),
                entity.ArchiveReason)
        };
    }

    private static ApprovedMailboxReportSentEvidence? MapLinkedEvidence(
        CaseReportSentEvidenceEntity entity)
    {
        if (string.Equals(entity.DiscoveredByKind, "LegacyUnverified", StringComparison.Ordinal))
        {
            return null;
        }
        if (entity.LinkedAtUtc is not { } linkedAtUtc
            || entity.LinkedByKind is null
            || entity.LinkedBySubjectId is null
            || entity.LinkedByRolesJson is null)
        {
            throw new InvalidDataException(
                "Case report-sent evidence is missing its authoritative link metadata.");
        }

        return new(
            entity.Id,
            entity.MailboxIdentity,
            entity.SentFolderIdentity,
            entity.ImmutableItemIdentity,
            entity.InternetMessageIdentity,
            entity.ConversationIdentity,
            entity.ReplyChainIdentity,
            entity.SourceOccurrenceIdentity,
            entity.SourceSha256,
            entity.MimeSha256,
            entity.SentAtUtc,
            entity.DiscoveredAtUtc,
            MapDiscoveryActor(entity.DiscoveredByKind, entity.DiscoveredBySubjectId),
            linkedAtUtc,
            MapLinkActor(entity.LinkedByKind, entity.LinkedBySubjectId, entity.LinkedByRolesJson));
    }

    private static RetainedApprovedMailboxReportSentEvidence MapRetainedEvidence(
        CaseReportSentEvidenceEntity entity) => new(
        entity.Id,
        entity.MailboxIdentity,
        entity.SentFolderIdentity,
        entity.ImmutableItemIdentity,
        entity.InternetMessageIdentity,
        entity.ConversationIdentity,
        entity.ReplyChainIdentity,
        entity.SourceOccurrenceIdentity,
        entity.SourceSha256,
        entity.MimeSha256,
        entity.SentAtUtc,
        entity.DiscoveredAtUtc,
        MapDiscoveryActor(entity.DiscoveredByKind, entity.DiscoveredBySubjectId));

    private static ActionActor MapLinkActor(string kind, string subjectId, string rolesJson)
    {
        if (kind == nameof(ActorKind.SystemWorker))
        {
            var roles = JsonSerializer.Deserialize<StaffRole[]>(rolesJson) ?? [];
            if (roles.Length != 0)
            {
                throw new InvalidDataException(
                    "System-worker report-evidence linkage cannot contain staff roles.");
            }

            return ActionActor.SystemWorker(subjectId);
        }

        return MapStaffActor(kind, subjectId, rolesJson);
    }

    private static ActionActor MapStaffActor(string kind, string subjectId, string rolesJson)
    {
        if (kind != nameof(ActorKind.Staff)
            || !Guid.TryParse(subjectId, out var staffId)
            || staffId == Guid.Empty)
        {
            throw new InvalidDataException("Case evidence contains an unsupported staff actor.");
        }

        return ActionActor.Staff(
            staffId,
            JsonSerializer.Deserialize<StaffRole[]>(rolesJson) ?? []);
    }

    private static ActionActor MapDiscoveryActor(string kind, string subjectId) => kind switch
    {
        nameof(ActorKind.SystemWorker) => ActionActor.SystemWorker(subjectId),
        _ => throw new InvalidDataException(
            "Case report-sent evidence contains an unsupported discovery actor.")
    };


    private sealed class SearchRow
    {
        public Guid CaseId { get; init; }
        public required string Reference { get; init; }
        public string? AuditReference { get; init; }
        public required string CaseType { get; init; }
        public required string Principal { get; init; }
        public required string State { get; init; }
        public Guid? EngineerId { get; init; }
        public string? Registration { get; init; }
        public string? Claimant { get; init; }
        public string? ClaimNumber { get; init; }
        public DateTimeOffset ReceivedAtUtc { get; init; }
        public DateOnly? InstructionDate { get; init; }
        public required string Origin { get; init; }
        public DateTimeOffset CreatedAtUtc { get; init; }
        public DateTimeOffset? NextChaseAtUtc { get; init; }
        public string? VehicleMake { get; init; }
        public string? VehicleModel { get; init; }
        public string? AccidentCircumstances { get; init; }
        public bool InstructionComplete { get; init; }
        public bool ImagesComplete { get; init; }
    }
}
