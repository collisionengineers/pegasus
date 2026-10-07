using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfCaseQueryStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : ICaseQueryStore, ICaseKindQueries, ICaseDocumentQueries
{
    /// <inheritdoc />
    async Task<IReadOnlyList<CaseDocument>> ICaseDocumentQueries.ListAsync(Guid caseId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await ReadDocumentsAsync(context, caseId, cancellationToken);
    }

    /// <inheritdoc />
    async Task<CaseType?> ICaseKindQueries.GetAsync(Guid caseId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var code = await context.Cases.AsNoTracking()
            .Where(item => item.Id == caseId)
            .Select(item => item.Type)
            .SingleOrDefaultAsync(cancellationToken);
        return code is null ? null : CaseTypeCodes.Parse(code);
    }

    public async Task<SearchCasesResult> SearchAsync(
        SearchCasesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = ApplySearchFilters(SearchRows(context, query.Filters.IncludeTriage), query.Filters);

        var skip = checked((query.Page - 1) * query.PageSize);
        var ordered = OrderRows(rows, query.Order);
        var page = await ordered
            .ThenBy(item => item.Reference)
            .ThenBy(item => item.CaseId)
            .Skip(skip)
            .Take(query.PageSize + 1)
            .ToArrayAsync(cancellationToken);
        var hasNextPage = page.Length > query.PageSize;
        var now = timeProvider.GetUtcNow();
        var items = page
            .Take(query.PageSize)
            .Select(item => MapSearchItem(item, now))
            .ToArray();

        return new(
            items,
            query.Page,
            query.PageSize,
            query.Page > 1,
            hasNextPage);
    }

    /// <summary>
    /// The keyset-paged sibling of <see cref="SearchAsync"/>:
    /// shares <see cref="SearchRows"/> and <see cref="OrderRows"/> so the two
    /// entry points can never read a different projection or sort a column
    /// differently. The after-values are the decoded cursor's position, both
    /// null on the first page; nullable text columns keyset-compare with
    /// <c>string.Compare</c> so the EF Core SQL Server provider translates
    /// the comparison the same way the official keyset-pagination pattern
    /// does, and a null column sorts as the empty string — the normalized
    /// filters below never store an empty string, so this matches the
    /// database's own NULL-is-lowest ordering in practice.
    /// </summary>
    public async Task<IReadOnlyList<CaseSearchItem>> SearchByCursorAsync(
        CaseSearchFilters filters,
        CaseSearchOrder order,
        DateTimeOffset? afterReceivedAtUtc,
        string? afterSortText,
        Guid? afterId,
        int fetchCount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filters);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = ApplySearchFilters(SearchRows(context, filters.IncludeTriage), filters);
        if (afterId is { } id)
        {
            rows = ApplyCursorPredicate(rows, order, afterReceivedAtUtc, afterSortText, id);
        }

        // The tie-break must run the same direction as the primary column so
        // it agrees with ApplyCursorPredicate's "< (k, id)" / "> (k, id)"
        // keyset test below — unlike the numbered SearchAsync, which always
        // ties on ascending Reference/CaseId because it pages by
        // Skip/Take and never re-derives a predicate from the last row.
        var ordered = OrderRows(rows, order);
        var tieBroken = IsDescendingOrder(order)
            ? ordered.ThenByDescending(item => item.CaseId)
            : ordered.ThenBy(item => item.CaseId);
        var page = await tieBroken
            .Take(fetchCount)
            .ToArrayAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        return page.Select(item => MapSearchItem(item, now)).ToArray();
    }

    private static bool IsDescendingOrder(CaseSearchOrder order) => order switch
    {
        CaseSearchOrder.ReceivedAsc
            or CaseSearchOrder.ReferenceAsc
            or CaseSearchOrder.RegistrationAsc
            or CaseSearchOrder.ClaimantAsc
            or CaseSearchOrder.PrincipalAsc => false,
        _ => true
    };

    private static IQueryable<SearchRow> ApplyCursorPredicate(
        IQueryable<SearchRow> rows,
        CaseSearchOrder order,
        DateTimeOffset? afterReceivedAtUtc,
        string? afterSortText,
        Guid afterId) => order switch
    {
        CaseSearchOrder.ReceivedAsc => rows.Where(item =>
            item.ReceivedAtUtc > afterReceivedAtUtc
            || (item.ReceivedAtUtc == afterReceivedAtUtc && item.CaseId > afterId)),
        CaseSearchOrder.ReferenceAsc => rows.Where(item =>
            string.Compare(item.Reference, afterSortText, StringComparison.Ordinal) > 0
            || (item.Reference == afterSortText && item.CaseId > afterId)),
        CaseSearchOrder.ReferenceDesc => rows.Where(item =>
            string.Compare(item.Reference, afterSortText, StringComparison.Ordinal) < 0
            || (item.Reference == afterSortText && item.CaseId < afterId)),
        CaseSearchOrder.RegistrationAsc => rows.Where(item =>
            string.Compare(item.Registration ?? "", afterSortText, StringComparison.Ordinal) > 0
            || ((item.Registration ?? "") == afterSortText && item.CaseId > afterId)),
        CaseSearchOrder.RegistrationDesc => rows.Where(item =>
            string.Compare(item.Registration ?? "", afterSortText, StringComparison.Ordinal) < 0
            || ((item.Registration ?? "") == afterSortText && item.CaseId < afterId)),
        CaseSearchOrder.ClaimantAsc => rows.Where(item =>
            string.Compare(item.Claimant ?? "", afterSortText, StringComparison.Ordinal) > 0
            || ((item.Claimant ?? "") == afterSortText && item.CaseId > afterId)),
        CaseSearchOrder.ClaimantDesc => rows.Where(item =>
            string.Compare(item.Claimant ?? "", afterSortText, StringComparison.Ordinal) < 0
            || ((item.Claimant ?? "") == afterSortText && item.CaseId < afterId)),
        CaseSearchOrder.PrincipalAsc => rows.Where(item =>
            string.Compare(item.Principal, afterSortText, StringComparison.Ordinal) > 0
            || (item.Principal == afterSortText && item.CaseId > afterId)),
        CaseSearchOrder.PrincipalDesc => rows.Where(item =>
            string.Compare(item.Principal, afterSortText, StringComparison.Ordinal) < 0
            || (item.Principal == afterSortText && item.CaseId < afterId)),
        _ => rows.Where(item =>
            item.ReceivedAtUtc < afterReceivedAtUtc
            || (item.ReceivedAtUtc == afterReceivedAtUtc && item.CaseId < afterId))
    };

    /// <summary>
    /// The <see cref="SearchAsync"/> filter translation, shared with
    /// <see cref="SearchByCursorAsync"/> so the numbered and
    /// cursor search entry points can never read a different set of rows for
    /// the same filters.
    /// </summary>
    private static IQueryable<SearchRow> ApplySearchFilters(IQueryable<SearchRow> rows, CaseSearchFilters filters)
    {
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
            rows = rows.Where(item =>
                item.Reference.Contains(caseReference)
                || item.AuditReference != null && item.AuditReference.Contains(caseReference));
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
        if (filters.States is { Count: > 0 } states)
        {
            var stateNames = states.Select(state => state.ToString()).ToArray();
            rows = rows.Where(item => stateNames.Contains(item.State));
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

        return rows;
    }

    private static IOrderedQueryable<SearchRow> OrderRows(IQueryable<SearchRow> rows, CaseSearchOrder order) =>
        order switch
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

    /// <summary>
    /// The case's summary, workflow and active-lease facts, with the
    /// document, history and open-task lists reduced to a single count query
    /// each instead of materializing every row.
    /// </summary>
    public async Task<CaseHeader?> GetHeaderAsync(
        GetCaseHeaderQuery query,
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
        // The three counts are one command: a header read is a handful of round
        // trips, and each one is paid for by every page that shows a Case.
        var openTaskState = nameof(CaseTaskState.Open);
        var counts = await context.Cases
            .AsNoTracking()
            .Where(item => item.Id == query.CaseId)
            .Select(item => new
            {
                Documents = context.Set<CaseDocumentEntity>().Count(document => document.CaseId == query.CaseId),
                History = context.CaseWorkflowEvents.Count(entry => entry.CaseId == query.CaseId),
                OpenTasks = context.Set<CaseTaskEntity>()
                    .Count(task => task.CaseId == query.CaseId && task.State == openTaskState)
            })
            .SingleAsync(cancellationToken);

        return new CaseHeader(
            MapSearchItem(summaryRow, timeProvider.GetUtcNow()),
            MapWorkflow(workflow),
            ResolveActiveLease(workflow, timeProvider.GetUtcNow()),
            counts.Documents,
            counts.History,
            counts.OpenTasks,
            await CaseWorkScope.LoadSetAsync(context, query.CaseId, cancellationToken));
    }

    public async Task<CaseSectionFrame?> GetSectionFrameAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workflow = await context.CaseWorkflows
            .AsNoTracking()
            .Include(item => item.Case)
                .ThenInclude(item => item.Principal)
            .Include(item => item.ReportApproval)
            .Include(item => item.ReportSentEvidence)
            .Include(item => item.DueWork)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        if (workflow is null)
        {
            return null;
        }

        var summary = MapSearchItem(await SearchRows(context)
            .SingleAsync(item => item.CaseId == caseId, cancellationToken), timeProvider.GetUtcNow());
        return CreateSectionFrame(
            summary, workflow, await CaseWorkScope.LoadSetAsync(context, caseId, cancellationToken));
    }

    /// <summary>
    /// The first-response frame excludes every lazily mounted body. Documents
    /// remain because the always-rendered Report section consumes them.
    /// </summary>
    public async Task<CasePageFrameData?> GetPageFrameAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workflow = await context.CaseWorkflows
            .AsNoTracking()
            .Include(item => item.Case)
                .ThenInclude(item => item.Principal)
            .Include(item => item.ReportApproval)
            .Include(item => item.ReportSentEvidence)
            .Include(item => item.DueWork)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        if (workflow is null)
        {
            return null;
        }

        var summary = MapSearchItem(await SearchRows(context)
            .SingleAsync(item => item.CaseId == caseId, cancellationToken), timeProvider.GetUtcNow());
        var documents = await ReadDocumentsAsync(context, caseId, cancellationToken);
        var availableReportSentEvidence = await context.CaseReportSentEvidence
            .AsNoTracking()
            .Where(item => item.CaseId == null && item.DiscoveredByKind == nameof(ActorKind.SystemWorker))
            .OrderByDescending(item => item.SentAtUtc)
            .ThenBy(item => item.Id)
            .Take(100)
            .ToArrayAsync(cancellationToken);
        var recordNotes = await ReadRecordNotesAsync(context, workflow, caseId, cancellationToken);
        var frame = CreateSectionFrame(
            summary, workflow, await CaseWorkScope.LoadSetAsync(context, caseId, cancellationToken));
        return new(
            frame,
            documents,
            availableReportSentEvidence.Select(MapRetainedEvidence).ToArray(),
            recordNotes,
            // The Next action's cancellation row reads the linked mail's current
            // classification on every render (FRD-13 "Cancellation messages"),
            // and only while the Case can show one.
            CaseCancellationNotice.Applies(frame.Workflow)
                ? await ReadLinkedCancellationMessageIdAsync(context, caseId, cancellationToken)
                : null);
    }

    /// <summary>
    /// The Notes body has its own read so a mounted section does not materialize
    /// documents, correspondence or other Case bodies merely to show history.
    /// Actor names are deliberately resolved in Core, where the actor policy
    /// already lives.
    /// </summary>
    public async Task<IReadOnlyList<CaseHistoryEntry>> ListHistoryAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await context.CaseWorkflowEvents
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(200)
            .ToArrayAsync(cancellationToken);
        return entities
            .Select(MapHistoryEntry)
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenByDescending(item => item.EntryId)
            .ToArray();
    }

    /// <summary>
    /// A reference-only projection for bounded operational rows.  It avoids
    /// loading the full Case header, workflow or any document/history body.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, string>> GetReferencesAsync(
        IReadOnlyCollection<Guid> caseIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caseIds);
        if (caseIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var ids = caseIds.Distinct().ToArray();
        var rows = await context.Set<CaseEntity>()
            .AsNoTracking()
            .Where(item => ids.Contains(item.Id))
            .Select(item => new { item.Id, item.Reference })
            .ToArrayAsync(cancellationToken);
        return rows.ToDictionary(item => item.Id, item => item.Reference);
    }

    /// <summary>
    /// The Files body projection.  It shares the document mapping used by the
    /// full Case read, but deliberately omits history, tasks, chaser, custody
    /// preparations and record notes.
    /// </summary>
    public async Task<CaseFilesSectionData?> GetFilesSectionAsync(
        Guid caseId,
        bool includeDocuments,
        CaseSectionFrame? frame,
        CancellationToken cancellationToken)
    {
        if (frame is not null
            && (frame.Summary.CaseId != caseId || frame.Workflow.CaseId != caseId))
        {
            throw new ArgumentException("The Case section frame belongs to another Case.", nameof(frame));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (frame is null)
        {
            var workflow = await context.CaseWorkflows
                .AsNoTracking()
                .Include(item => item.Case)
                    .ThenInclude(item => item.Principal)
                .Include(item => item.ReportApproval)
                .Include(item => item.ReportSentEvidence)
                .Include(item => item.DueWork)
                .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
            if (workflow is null)
            {
                return null;
            }

            var summary = MapSearchItem(await SearchRows(context)
                .SingleAsync(item => item.CaseId == caseId, cancellationToken), timeProvider.GetUtcNow());
            frame = CreateSectionFrame(
                summary, workflow, await CaseWorkScope.LoadSetAsync(context, caseId, cancellationToken));
        }

        ArgumentNullException.ThrowIfNull(frame);
        var sectionFrame = frame;
        IReadOnlyList<CaseDocument> documents = includeDocuments
            ? await ReadDocumentsAsync(context, caseId, cancellationToken)
            : [];
        var correspondenceEmails = await ReadCorrespondenceEmailsAsync(context, caseId, cancellationToken);
        // The Audit facts the Files section needs are not on the section
        // frame a caller may hand in, so read them in one narrow projection.
        var auditFacts = await context.Cases
            .AsNoTracking()
            .Where(item => item.Id == caseId)
            .Select(item => new
            {
                item.StandaloneAuditEvidenceId,
                item.AuditCustodyRemoteId,
                HasAuditWork = item.Works.Any(work => work.Kind == CaseWorkKinds.Audit),
                AuditFolderFailed = item.ExternalWork.Any(work =>
                    work.Kind == ExternalWorkKinds.CreateAuditReferenceCustody
                    && work.State == ExternalWorkStatePersistence.Failed)
            })
            .SingleAsync(cancellationToken);
        // The Audit's a. folder: confirmed once it has a Box root, failed when
        // its custody work failed, otherwise still being prepared.
        CaseCustodyState? auditCustodyState = !auditFacts.HasAuditWork
            ? null
            : !string.IsNullOrWhiteSpace(auditFacts.AuditCustodyRemoteId)
                ? CaseCustodyState.Confirmed
                : auditFacts.AuditFolderFailed
                    ? CaseCustodyState.Failed
                    : CaseCustodyState.Pending;
        return new(sectionFrame, documents, sectionFrame.CustodyFolderRemoteId,
            sectionFrame.CustodyState, correspondenceEmails,
            auditFacts.StandaloneAuditEvidenceId,
            auditCustodyState,
            auditFacts.HasAuditWork ? auditFacts.AuditCustodyRemoteId : null);
    }

    public async Task<CaseRenderLeaseValidation?> GetRenderLeaseValidationAsync(
        Guid caseId,
        string presentedToken,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workflow = await context.CaseWorkflows
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        return workflow is null
            ? null
            : new(
                workflow.CaseId,
                workflow.Version,
                CaseMutationGuard.RetainedHolderKind(workflow.EditLeaseHolderKind),
                workflow.EditLeaseHolder,
                workflow.EditLeaseExpiresAtUtc,
                !string.IsNullOrWhiteSpace(workflow.EditLeaseTokenHash),
                CaseMutationGuard.MatchesRetainedHash(workflow.EditLeaseTokenHash, presentedToken));
    }

    /// <summary>
    /// The one rule for whether a case's edit lease is live, shared by
    /// every read that reports the lease so they can never disagree about
    /// who — if anyone — currently holds it.
    /// </summary>
    private static CaseEditLeaseSnapshot? ResolveActiveLease(CaseWorkflowEntity workflow, DateTimeOffset now) =>
        workflow.EditLeaseHolder is { } holder
            && workflow.EditLeaseExpiresAtUtc is { } expiresAtUtc
            && workflow.EditLeaseOperationKey is { Length: > 0 } operationKey
            && CaseEditAuthority.IsHeld(expiresAtUtc, now)
                ? new CaseEditLeaseSnapshot(
                    holder,
                    CaseMutationGuard.RetainedHolderKind(workflow.EditLeaseHolderKind),
                    expiresAtUtc,
                    operationKey,
                    workflow.EditLeaseGeneration)
                : null;

    internal static CaseCustodyState ParseCustodyState(string value) => value switch
    {
        "pending" => CaseCustodyState.Pending,
        "confirmed" => CaseCustodyState.Confirmed,
        "failed" => CaseCustodyState.Failed,
        _ => throw new InvalidDataException(
            $"Unknown persisted case custody state '{value}'.")
    };

    private CaseSectionFrame CreateSectionFrame(
        CaseSearchItem summary,
        CaseWorkflowEntity workflow,
        CaseWorkSet works) =>
        new(
            summary,
            MapWorkflow(workflow),
            ResolveActiveLease(workflow, timeProvider.GetUtcNow()),
            workflow.Case.CustodyRootRemoteId,
            ParseCustodyState(workflow.Case.CustodyState),
            works);

    private static async Task<CaseRecordNotes> ReadRecordNotesAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        // One command reads the Principal's notes and the claim source named by
        // the work being edited; the claim source's own notes follow only when
        // there is one.
        var currentWorkIds = CaseWorkScope.SelectedIds(context, caseId, CaseWorkSelector.Current);
        var principal = await context.Organizations.AsNoTracking()
            .Where(item => item.Id == workflow.Case.Principal.OrganizationId)
            .Select(item => new
            {
                item.NotesOnEveryCase,
                ClaimSourceId = context.CaseDataFields
                    .Where(field => currentWorkIds.Contains(field.WorkId)
                        && field.FieldName == CaseDataFieldNames.ClaimSourceId
                        && field.ValueKind == CaseDataCodes.Confirmed)
                    .Select(field => field.Value)
                    .FirstOrDefault()
            })
            .SingleAsync(cancellationToken);
        return new(
            principal.NotesOnEveryCase,
            Guid.TryParse(principal.ClaimSourceId, out var claimSourceOrganizationId)
                ? await context.Organizations.AsNoTracking()
                    .Where(item => item.Id == claimSourceOrganizationId)
                    .Select(item => item.NotesOnEveryCase)
                    .FirstOrDefaultAsync(cancellationToken)
                : null);
    }

    /// <summary>
    /// The Case's correspondence (FRD-20 § Case correspondence view): every
    /// retained email whose receipt is currently linked to the Case, whatever
    /// its classification - the email the Case was created from, polled mail
    /// associated later, and uploaded .eml files - and every Sent item of a
    /// send Pegasus made for the Case, newest first. Only the two channels an
    /// email arrives through are read, since a receipt token is unique only
    /// within its channel. A Triage Case passes the receipt it was opened
    /// from, which stands as its request e-mail without an intake association
    /// of its own.
    /// </summary>
    internal static async Task<IReadOnlyList<CaseCorrespondenceEmail>> ReadCorrespondenceEmailsAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken,
        Guid? originReceiptId = null)
    {
        var received = await ReadReceivedCorrespondenceAsync(context, caseId, originReceiptId, cancellationToken);
        var sent = await ReadSentCorrespondenceAsync(context, caseId, cancellationToken);
        return received.Concat(sent)
            .OrderByDescending(item => item.ReceivedAtUtc)
            .ThenBy(item => item.RetainedMessageId)
            .ToArray();
    }

    /// <summary>
    /// The Sent items the Sent-evidence poll retained for sends whose
    /// observed Sent item they are, where the send named this Case: a general
    /// correspondence or Triage send by its context, a report send by its
    /// generation's Case (FRD-21 "links it to the Case named at send time").
    /// </summary>
    private static async Task<IReadOnlyList<CaseCorrespondenceEmail>> ReadSentCorrespondenceAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var sentScope = EfRetainedMailboxMessageStore.ToCode(MailFolderScope.Sent);
        var operations = context.Set<StaffMailSendOperationEntity>().AsNoTracking();
        var generations = context.Set<CaseReportGenerationEntity>().AsNoTracking();
        var rows = await context.RetainedMailboxMessages.AsNoTracking()
            .Where(message => message.FolderScope == sentScope
                && operations.Any(operation =>
                    operation.State == StaffMailState.Sent
                    && operation.ObservedSentImmutableMessageId == message.ImmutableMessageId
                    && operation.MailboxId == message.MailboxId
                    && (operation.Purpose == StaffMailPurpose.CaseReport
                        ? generations.Any(generation =>
                            generation.Id == operation.ContextId && generation.CaseId == caseId)
                        : operation.ContextId == caseId)))
            .Select(item => new
            {
                item.Id,
                item.ReceivedAtUtc,
                item.MailboxAddress,
                item.SenderDisplayName,
                item.SenderAddress,
                item.Subject,
                item.SourceSha256
            })
            .ToArrayAsync(cancellationToken);
        return rows
            .Select(item => new CaseCorrespondenceEmail(
                item.Id,
                item.ReceivedAtUtc,
                item.MailboxAddress,
                item.SenderDisplayName,
                item.SenderAddress ?? item.MailboxAddress,
                item.Subject,
                Classification: null,
                item.SourceSha256,
                MailDirection.Sent))
            .ToArray();
    }

    private static async Task<IReadOnlyList<CaseCorrespondenceEmail>> ReadReceivedCorrespondenceAsync(
        PegasusDbContext context,
        Guid caseId,
        Guid? originReceiptId,
        CancellationToken cancellationToken)
    {
        var originReceiptIds = originReceiptId is { } origin ? new[] { origin } : [];
        var associatedReceiptIds = context.IntakeManualAssociations.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .Select(item => item.IntakeReceiptId)
            .Union(context.CaseIntakeLinks.AsNoTracking()
                .Where(item => item.CaseId == caseId)
                .Select(item => item.IntakeReceiptId));
        var associatedReceipts = await context.IntakeReceipts.AsNoTracking()
            .Where(item => (associatedReceiptIds.Contains(item.Id) || originReceiptIds.Contains(item.Id))
                && (item.SourceChannel == EfIntakeReceiptStore.ToCode(IntakeSourceChannel.Mailbox)
                    || item.SourceChannel == EfIntakeReceiptStore.ToCode(IntakeSourceChannel.ManualUpload)))
            .Select(item => new
            {
                item.Id,
                item.ExternalReceiptToken,
                Classification = item.MailClassificationDecision,
                EffectiveSenderAddress = item.MailRouteDecision == null ? null : item.MailRouteDecision.EffectiveSenderAddress
            })
            .ToArrayAsync(cancellationToken);
        var associations = await CurrentIntakeAssociations.ReadAsync(
            context, associatedReceipts.Select(item => item.Id).ToArray(), cancellationToken);
        var linkedReceipts = associatedReceipts
            .Where(item => originReceiptIds.Contains(item.Id)
                || (associations.Current.TryGetValue(item.Id, out var association) && association.CaseId == caseId))
            .ToArray();
        var tokens = linkedReceipts.Select(item => item.ExternalReceiptToken).Distinct(StringComparer.Ordinal).ToArray();
        var messages = tokens.Length == 0
            ? []
            : await context.RetainedMailboxMessages.AsNoTracking()
                .Where(item => tokens.Contains(item.ExternalReceiptToken))
                .Select(item => new
                {
                    item.Id,
                    item.ExternalReceiptToken,
                    item.ReceivedAtUtc,
                    item.SenderDisplayName,
                    item.SenderAddress,
                    item.Subject,
                    item.SourceSha256
                })
                .ToArrayAsync(cancellationToken);
        var receiptByToken = linkedReceipts.ToDictionary(item => item.ExternalReceiptToken, StringComparer.Ordinal);
        return messages.Select(item =>
            {
                var receipt = receiptByToken[item.ExternalReceiptToken];
                return new CaseCorrespondenceEmail(item.Id, item.ReceivedAtUtc, receipt.EffectiveSenderAddress,
                    item.SenderDisplayName, item.SenderAddress, item.Subject,
                    receipt.Classification is null ? null
                        : EfIntakeReceiptStore.MapMailClassificationDecision(receipt.Classification).Category,
                    item.SourceSha256);
            })
            .OrderByDescending(item => item.ReceivedAtUtc)
            .ThenBy(item => item.RetainedMessageId)
            .ToArray();
    }

    /// <summary>
    /// The newest retained email currently linked to the Case whose current
    /// classification is a received in-progress cancellation, in one command.
    /// It keeps the rules the correspondence read applies in memory: an email
    /// channel; an active manual association to this Case, or no manual
    /// association and an accepted link to it (<see
    /// cref="CurrentIntakeAssociations"/>); and the category the persisted
    /// decision maps to, which ignores the decision's outcome and is Other
    /// whenever an Other name is recorded.
    /// </summary>
    private static Task<Guid?> ReadLinkedCancellationMessageIdAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var mailbox = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.Mailbox);
        var manualUpload = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.ManualUpload);
        var received = EfIntakeReceiptStore.ToCode(MailDirection.Received);
        var inProgressCases = MailTaxonomy.CategoryName(ReceivedMailFamily.InProgressCases);
        return context.IntakeReceipts.AsNoTracking()
            .Where(item => (item.SourceChannel == mailbox || item.SourceChannel == manualUpload)
                && ((item.ManualAssociation != null
                        && item.ManualAssociation.IsActive
                        && item.ManualAssociation.CaseId == caseId)
                    || (item.ManualAssociation == null
                        && context.CaseIntakeLinks.Any(link =>
                            link.IntakeReceiptId == item.Id && link.CaseId == caseId)))
                && item.MailClassificationDecision != null
                && item.MailClassificationDecision.OtherName == null
                && item.MailClassificationDecision.Direction == received
                && item.MailClassificationDecision.Family == inProgressCases
                && item.MailClassificationDecision.Subtype == MailCategory.CancellationSubtype)
            .Join(
                context.RetainedMailboxMessages.AsNoTracking(),
                receipt => receipt.ExternalReceiptToken,
                message => message.ExternalReceiptToken,
                (receipt, message) => message)
            .OrderByDescending(message => message.ReceivedAtUtc)
            .ThenBy(message => message.Id)
            .Select(message => (Guid?)message.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// The search rows, with Triage Cases only when the caller asks for them.
    /// </summary>
    private static IQueryable<SearchRow> SearchRows(PegasusDbContext context, bool includeTriage) =>
        includeTriage ? SearchRowsIncludingTriage(context) : SearchRows(context);

    /// <summary>
    /// Every Case with a Case workflow and every Triage Case: the Case left
    /// joined to its workflow and to its Triage subtype. A Triage Case row takes
    /// its registration from the Triage and its state from the Triage state
    /// code; it has no workflow, so the workflow-only columns are empty.
    /// </summary>
    private static IQueryable<SearchRow> SearchRowsIncludingTriage(PegasusDbContext context) =>
        from caseEntity in context.Set<CaseEntity>().AsNoTracking()
        join workflowCandidate in context.CaseWorkflows.AsNoTracking()
            on caseEntity.Id equals workflowCandidate.CaseId into workflows
        from workflow in workflows.DefaultIfEmpty()
        join triageCandidate in context.Triage.AsNoTracking()
            on caseEntity.Id equals triageCandidate.CaseId into triages
        from triage in triages.DefaultIfEmpty()
        where workflow != null || triage != null
        join principal in context.Set<PrincipalEntity>().AsNoTracking()
            on caseEntity.PrincipalId equals principal.Id
        join receiptCandidate in context.Set<IntakeReceiptEntity>().AsNoTracking()
            on caseEntity.OriginIntakeReceiptId equals receiptCandidate.Id into receipts
        from receipt in receipts.DefaultIfEmpty()
        join draftCandidate in context.Set<InstructionDraftEntity>().AsNoTracking()
            on receipt.Id equals draftCandidate.IntakeReceiptId into drafts
        from draft in drafts.DefaultIfEmpty()
        join confirmedClaimantCandidate in context.CaseDataFields.AsNoTracking()
                .Where(item => item.FieldName == CaseDataFieldNames.ClaimantName
                    && item.ValueKind == CaseDataCodes.Confirmed)
            on caseEntity.Id equals confirmedClaimantCandidate.WorkId into confirmedClaimants
        from confirmedClaimant in confirmedClaimants.DefaultIfEmpty()
        join confirmedClaimNumberCandidate in context.CaseDataFields.AsNoTracking()
                .Where(item => item.FieldName == CaseDataFieldNames.ClaimNumber
                    && item.ValueKind == CaseDataCodes.Confirmed)
            on caseEntity.Id equals confirmedClaimNumberCandidate.WorkId into confirmedClaimNumbers
        from confirmedClaimNumber in confirmedClaimNumbers.DefaultIfEmpty()
        join confirmedRegistrationCandidate in context.CaseDataFields.AsNoTracking()
                .Where(item => item.FieldName == CaseDataFieldNames.VehicleRegistration
                    && item.ValueKind == CaseDataCodes.Confirmed)
            on caseEntity.Id equals confirmedRegistrationCandidate.WorkId into confirmedRegistrations
        from confirmedRegistration in confirmedRegistrations.DefaultIfEmpty()
        join confirmedMakeCandidate in context.CaseDataFields.AsNoTracking()
                .Where(item => item.FieldName == CaseDataFieldNames.VehicleMake
                    && item.ValueKind == CaseDataCodes.Confirmed)
            on caseEntity.Id equals confirmedMakeCandidate.WorkId into confirmedMakes
        from confirmedMake in confirmedMakes.DefaultIfEmpty()
        join confirmedModelCandidate in context.CaseDataFields.AsNoTracking()
                .Where(item => item.FieldName == CaseDataFieldNames.VehicleModel
                    && item.ValueKind == CaseDataCodes.Confirmed)
            on caseEntity.Id equals confirmedModelCandidate.WorkId into confirmedModels
        from confirmedModel in confirmedModels.DefaultIfEmpty()
        join confirmedCircumstancesCandidate in context.CaseDataFields.AsNoTracking()
                .Where(item => item.FieldName == CaseDataFieldNames.AccidentCircumstances
                    && item.ValueKind == CaseDataCodes.Confirmed)
            on caseEntity.Id equals confirmedCircumstancesCandidate.WorkId into confirmedCircumstanceRows
        from confirmedCircumstances in confirmedCircumstanceRows.DefaultIfEmpty()
        select new SearchRow
        {
            CaseId = caseEntity.Id,
            Reference = caseEntity.Reference,
            AuditReference = caseEntity.AuditReference,
            CaseType = caseEntity.Type,
            Principal = principal.Code,
            State = workflow != null ? workflow.State : triage!.State,
            EngineerId = workflow == null ? null : workflow.AssignedEngineerId,
            Registration = triage != null
                ? triage.NormalizedVehicleRegistration
                : draft == null ? confirmedRegistration!.Value : draft.VehicleRegistration,
            Claimant = draft == null ? confirmedClaimant!.Value : draft.ClaimantName,
            ClaimNumber = draft == null ? confirmedClaimNumber!.Value : draft.ClaimNumber,
            VehicleMake = draft == null ? confirmedMake!.Value : draft.VehicleMake,
            VehicleModel = draft == null ? confirmedModel!.Value : draft.VehicleModel,
            AccidentCircumstances = draft == null
                ? confirmedCircumstances!.Value
                : draft.AccidentCircumstances,
            ReceivedAtUtc = receipt == null ? caseEntity.CreatedAtUtc : receipt.ReceivedAtUtc,
            Origin = receipt == null ? "manual" : receipt.SourceChannel,
            CreatedAtUtc = caseEntity.CreatedAtUtc,
            NextChaseAtUtc = workflow == null || workflow.DueWork == null ? null : workflow.DueWork!.NextChaseAtUtc,
            DueBy = workflow == null || workflow.DueWork == null ? null : workflow.DueWork!.DueBy,
            InstructionComplete = caseEntity.InstructionComplete,
            ImagesComplete = caseEntity.ImagesComplete,
            HoldReviewOn = workflow == null ? null : workflow.HoldReviewOn,
            HeldAtUtc = workflow == null ? null : workflow.HeldAtUtc,
            StateEnteredAtUtc = workflow == null ? null : workflow.StateEnteredAtUtc,
            EditLeaseHolder = workflow == null ? null : workflow.EditLeaseHolder,
            EditLeaseHolderKind = workflow == null ? null : workflow.EditLeaseHolderKind,
            EditLeaseExpiresAtUtc = workflow == null ? null : workflow.EditLeaseExpiresAtUtc
        };

    private static IQueryable<SearchRow> SearchRows(PegasusDbContext context) =>
        from workflow in context.CaseWorkflows.AsNoTracking()
        join caseEntity in context.Set<CaseEntity>().AsNoTracking()
            on workflow.CaseId equals caseEntity.Id
        join principal in context.Set<PrincipalEntity>().AsNoTracking()
            on caseEntity.PrincipalId equals principal.Id
        join receiptCandidate in context.Set<IntakeReceiptEntity>().AsNoTracking()
            on caseEntity.OriginIntakeReceiptId equals receiptCandidate.Id into receipts
        from receipt in receipts.DefaultIfEmpty()
        join draftCandidate in context.Set<InstructionDraftEntity>().AsNoTracking()
            on receipt.Id equals draftCandidate.IntakeReceiptId into drafts
        from draft in drafts.DefaultIfEmpty()
        join confirmedClaimantCandidate in context.CaseDataFields.AsNoTracking()
                .Where(item => item.FieldName == CaseDataFieldNames.ClaimantName
                    && item.ValueKind == CaseDataCodes.Confirmed)
            on caseEntity.Id equals confirmedClaimantCandidate.WorkId into confirmedClaimants
        from confirmedClaimant in confirmedClaimants.DefaultIfEmpty()
        join confirmedClaimNumberCandidate in context.CaseDataFields.AsNoTracking()
                .Where(item => item.FieldName == CaseDataFieldNames.ClaimNumber
                    && item.ValueKind == CaseDataCodes.Confirmed)
            on caseEntity.Id equals confirmedClaimNumberCandidate.WorkId into confirmedClaimNumbers
        from confirmedClaimNumber in confirmedClaimNumbers.DefaultIfEmpty()
        join confirmedRegistrationCandidate in context.CaseDataFields.AsNoTracking()
                .Where(item => item.FieldName == CaseDataFieldNames.VehicleRegistration
                    && item.ValueKind == CaseDataCodes.Confirmed)
            on caseEntity.Id equals confirmedRegistrationCandidate.WorkId into confirmedRegistrations
        from confirmedRegistration in confirmedRegistrations.DefaultIfEmpty()
        join confirmedMakeCandidate in context.CaseDataFields.AsNoTracking()
                .Where(item => item.FieldName == CaseDataFieldNames.VehicleMake
                    && item.ValueKind == CaseDataCodes.Confirmed)
            on caseEntity.Id equals confirmedMakeCandidate.WorkId into confirmedMakes
        from confirmedMake in confirmedMakes.DefaultIfEmpty()
        join confirmedModelCandidate in context.CaseDataFields.AsNoTracking()
                .Where(item => item.FieldName == CaseDataFieldNames.VehicleModel
                    && item.ValueKind == CaseDataCodes.Confirmed)
            on caseEntity.Id equals confirmedModelCandidate.WorkId into confirmedModels
        from confirmedModel in confirmedModels.DefaultIfEmpty()
        join confirmedCircumstancesCandidate in context.CaseDataFields.AsNoTracking()
                .Where(item => item.FieldName == CaseDataFieldNames.AccidentCircumstances
                    && item.ValueKind == CaseDataCodes.Confirmed)
            on caseEntity.Id equals confirmedCircumstancesCandidate.WorkId into confirmedCircumstanceRows
        from confirmedCircumstances in confirmedCircumstanceRows.DefaultIfEmpty()
        select new SearchRow
        {
            CaseId = caseEntity.Id,
            Reference = caseEntity.Reference,
            AuditReference = caseEntity.AuditReference,
            CaseType = caseEntity.Type,
            Principal = principal.Code,
            State = workflow.State,
            EngineerId = workflow.AssignedEngineerId,
            Registration = draft == null ? confirmedRegistration!.Value : draft.VehicleRegistration,
            Claimant = draft == null ? confirmedClaimant!.Value : draft.ClaimantName,
            ClaimNumber = draft == null ? confirmedClaimNumber!.Value : draft.ClaimNumber,
            VehicleMake = draft == null ? confirmedMake!.Value : draft.VehicleMake,
            VehicleModel = draft == null ? confirmedModel!.Value : draft.VehicleModel,
            AccidentCircumstances = draft == null
                ? confirmedCircumstances!.Value
                : draft.AccidentCircumstances,
            ReceivedAtUtc = receipt == null ? caseEntity.CreatedAtUtc : receipt.ReceivedAtUtc,
            Origin = receipt == null ? "manual" : receipt.SourceChannel,
            CreatedAtUtc = caseEntity.CreatedAtUtc,
            NextChaseAtUtc = workflow.DueWork == null ? null : workflow.DueWork!.NextChaseAtUtc,
            DueBy = workflow.DueWork == null ? null : workflow.DueWork!.DueBy,
            InstructionComplete = caseEntity.InstructionComplete,
            ImagesComplete = caseEntity.ImagesComplete,
            HoldReviewOn = workflow.HoldReviewOn,
            HeldAtUtc = workflow.HeldAtUtc,
            StateEnteredAtUtc = workflow.StateEnteredAtUtc,
            EditLeaseHolder = workflow.EditLeaseHolder,
            EditLeaseHolderKind = workflow.EditLeaseHolderKind,
            EditLeaseExpiresAtUtc = workflow.EditLeaseExpiresAtUtc
        };

    internal static async Task<IReadOnlyList<CaseDocument>> ReadDocumentsAsync(
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
        return await MapDocumentsAsync(context, caseId, documentEntities, cancellationToken);
    }

    /// <summary>
    /// The keyset-paged sibling of <see cref="ReadDocumentsAsync"/>:
    /// newest occurrence first, then
    /// occurrence id. The row unit is the occurrence, so a document with
    /// more occurrences than the caller's limit still enumerates every one
    /// across consecutive pages — a document-unit page cannot split one
    /// document's occurrences. Each row carries exactly the version its
    /// occurrence names; no occurrence or version set is ever materialized.
    /// </summary>
    public async Task<IReadOnlyList<CaseDocumentPageItem>> ListDocumentsByCursorAsync(
        Guid caseId,
        DateTimeOffset? afterRecordedAtUtc,
        Guid? afterId,
        int fetchCount,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.Set<DocumentOccurrenceEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == caseId);
        if (afterId is { } id)
        {
            var afterValue = afterRecordedAtUtc!.Value;
            query = query.Where(item =>
                item.RecordedAtUtc < afterValue
                || (item.RecordedAtUtc == afterValue && item.Id < id));
        }

        var occurrences = await query
            .OrderByDescending(item => item.RecordedAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(fetchCount)
            .ToArrayAsync(cancellationToken);
        if (occurrences.Length == 0)
        {
            return [];
        }

        var versionIds = occurrences.Select(item => item.VersionId).ToArray();
        var versionsById = (await context.Set<DocumentVersionEntity>()
                .AsNoTracking()
                .Where(item => versionIds.Contains(item.Id))
                .ToArrayAsync(cancellationToken))
            .ToDictionary(item => item.Id);

        // The paged document list is the file table, not the Images tab: it
        // draws no chips, so it does not pay for the tag read.
        return occurrences.Select(item => new CaseDocumentPageItem(
                MapOccurrence(item, []),
                versionsById.TryGetValue(item.VersionId, out var version)
                    ? MapVersion(version)
                    : throw new InvalidOperationException(
                        $"Document version {item.VersionId:D} named by occurrence {item.Id:D} does not exist.")))
            .ToArray();
    }

    /// <summary>
    /// The shared document-projection tail of <see cref="ReadDocumentsAsync"/>:
    /// given the case's document rows in the caller's own order,
    /// reads their occurrences and versions and maps them into <see
    /// cref="CaseDocument"/> without re-choosing which documents or what
    /// order.
    /// </summary>
    private static async Task<IReadOnlyList<CaseDocument>> MapDocumentsAsync(
        PegasusDbContext context,
        Guid caseId,
        CaseDocumentEntity[] documentEntities,
        CancellationToken cancellationToken)
    {
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

        // The image tags each occurrence wears, read with the occurrences
        // rather than per tile: the Files section draws every image's chips in
        // one pass.
        var occurrenceIds = occurrences.Select(item => item.Id).ToArray();
        var tagRows = await (
                from assignment in context.Set<DocumentOccurrenceTagEntity>().AsNoTracking()
                join tag in context.Set<ImageTagEntity>().AsNoTracking()
                    on assignment.TagId equals tag.Id
                where occurrenceIds.Contains(assignment.OccurrenceId)
                orderby tag.IsBuiltIn descending, tag.Name
                select new
                {
                    assignment.OccurrenceId,
                    tag.Id,
                    tag.Name,
                    tag.Colour,
                    tag.IsBuiltIn,
                    assignment.AppliedAtUtc
                })
            .ToArrayAsync(cancellationToken);
        var tagsByOccurrence = tagRows
            .GroupBy(row => row.OccurrenceId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ImageTagAssignment>)[.. group.Select(row =>
                    new ImageTagAssignment(
                        row.Id,
                        row.Name,
                        Enum.Parse<ImageTagColour>(row.Colour),
                        row.IsBuiltIn,
                        row.AppliedAtUtc))]);

        return documentEntities.Select(document => new CaseDocument(
                document.Id,
                caseId,
                occurrences.Where(item => item.DocumentId == document.Id)
                    .Select(item => MapOccurrence(
                        item,
                        tagsByOccurrence.GetValueOrDefault(item.Id, [])))
                    .ToArray(),
                versions.Where(item => item.DocumentId == document.Id).Select(MapVersion).ToArray()))
            .ToArray();
    }

    private static DocumentOccurrence MapOccurrence(
        DocumentOccurrenceEntity item,
        IReadOnlyList<ImageTagAssignment> tags) => new(
        item.Id,
        item.CaseId,
        item.DocumentId,
        item.VersionId,
        item.SemanticRole,
        item.Source,
        item.SourceOccurrenceIdentity,
        item.RecordedAtUtc,
        tags);

    private static DocumentVersion MapVersion(DocumentVersionEntity item) => new(
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
        item.RemovalReason,
        item.IsRecognisedEstimate);

    /// <summary>
    /// The keyset-paged sibling of <see cref="ListHistoryAsync"/>:
    /// newest event first, then entry id.
    /// </summary>
    public async Task<IReadOnlyList<CaseHistoryEntry>> ListHistoryByCursorAsync(
        Guid caseId,
        DateTimeOffset? afterOccurredAtUtc,
        Guid? afterId,
        int fetchCount,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = context.CaseWorkflowEvents.AsNoTracking().Where(item => item.CaseId == caseId);
        if (afterId is { } id)
        {
            var afterValue = afterOccurredAtUtc!.Value;
            rows = rows.Where(item =>
                item.OccurredAtUtc < afterValue
                || (item.OccurredAtUtc == afterValue && item.Id < id));
        }

        var entities = await rows
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(fetchCount)
            .ToArrayAsync(cancellationToken);
        return entities.Select(MapHistoryEntry).ToArray();
    }

    private static IEnumerable<CaseGuidanceEntry> ReadGuidance(CaseWorkflowEventEntity item)
    {
        if (string.IsNullOrWhiteSpace(item.ResultJson))
        {
            yield break;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(item.ResultJson);
        }
        catch (JsonException)
        {
            yield break;
        }
        using (document)
        {
            if (!document.RootElement.TryGetProperty("guidance", out var guidance)
                || guidance.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }
            foreach (var applied in guidance.EnumerateArray())
            {
                if (!applied.TryGetProperty("eventType", out var eventType)
                    || !applied.TryGetProperty("organizationName", out var organizationName)
                    || !applied.TryGetProperty("text", out var text)
                    || !applied.TryGetProperty("templateVersion", out var templateVersion)
                    || string.IsNullOrWhiteSpace(eventType.GetString())
                    || string.IsNullOrWhiteSpace(organizationName.GetString())
                    || string.IsNullOrWhiteSpace(text.GetString()))
                {
                    continue;
                }
                yield return new CaseGuidanceEntry(eventType.GetString()!, organizationName.GetString()!, templateVersion.GetInt64(), text.GetString()!);
            }
        }
    }

    private static CaseHistoryEntry MapHistoryEntry(CaseWorkflowEventEntity item) => new(
        item.EventType,
        item.ActorSubjectId,
        item.ActorKind,
        item.OccurredAtUtc,
        item.Reason,
        item.BeforeVersion,
        item.AfterVersion)
    {
        EntryId = item.Id,
        Guidance = ReadGuidance(item).ToArray()
    };

    private static CaseSearchItem MapSearchItem(SearchRow item, DateTimeOffset now) => new(
        item.CaseId,
        item.Reference,
        item.AuditReference,
        CaseTypeCodes.Parse(item.CaseType),
        item.Principal,
        // A Triage Case has no Case lifecycle state; its row carries the
        // Triage state instead (TriageState below).
        item.CaseType == CaseTypeCodes.Triage ? default : Enum.Parse<CaseLifecycleState>(item.State),
        item.EngineerId,
        item.Registration,
        item.Claimant,
        item.ClaimNumber,
        item.ReceivedAtUtc,
        item.Origin,
        item.CreatedAtUtc,
        CaseDuePolicy.DueAt(item.NextChaseAtUtc, item.DueBy),
        item.VehicleMake,
        item.VehicleModel,
        item.AccidentCircumstances)
    {
        InstructionComplete = item.InstructionComplete,
        ImagesComplete = item.ImagesComplete,
        HoldReviewOn = item.HoldReviewOn,
        HeldAtUtc = item.HeldAtUtc,
        StateEnteredAtUtc = item.StateEnteredAtUtc,
        EditingStaffId = EditingStaffId(item, now),
        TriageState = item.CaseType == CaseTypeCodes.Triage
            ? (Pegasus.Core.Triage.TriageState?)EfTriageStore.ParseState(item.State)
            : null
    };

    /// <summary>
    /// The staff holder of a live lease, by <see cref="ResolveActiveLease"/>'s
    /// rule; an automation holder or a lapsed lease is nobody editing.
    /// </summary>
    private static Guid? EditingStaffId(SearchRow item, DateTimeOffset now) =>
        item.EditLeaseHolder is { } holder
            && item.EditLeaseExpiresAtUtc is { } expiresAtUtc
            && CaseEditAuthority.IsHeld(expiresAtUtc, now)
            && CaseMutationGuard.RetainedHolderKind(item.EditLeaseHolderKind) == ActorKind.Staff
            && Guid.TryParse(holder, out var staffId)
                ? staffId
                : null;

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

    /// <summary>
    /// A staff member or the Automation Actor: both perform casework
    /// (ADR-0064), so an approval, archive or link either recorded reads back.
    /// </summary>
    private static ActionActor MapStaffActor(string kind, string subjectId, string rolesJson)
    {
        if (kind == nameof(ActorKind.Automation) && !string.IsNullOrWhiteSpace(subjectId))
        {
            return ActionActor.Automation(subjectId);
        }
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
        public required string Origin { get; init; }
        public DateTimeOffset CreatedAtUtc { get; init; }
        public DateTimeOffset? NextChaseAtUtc { get; init; }
        public DateOnly? DueBy { get; init; }
        public string? VehicleMake { get; init; }
        public string? VehicleModel { get; init; }
        public string? AccidentCircumstances { get; init; }
        public bool InstructionComplete { get; init; }
        public bool ImagesComplete { get; init; }
        public DateOnly? HoldReviewOn { get; init; }
        public DateTimeOffset? HeldAtUtc { get; init; }
        public DateTimeOffset? StateEnteredAtUtc { get; init; }
        public string? EditLeaseHolder { get; init; }
        public string? EditLeaseHolderKind { get; init; }
        public DateTimeOffset? EditLeaseExpiresAtUtc { get; init; }
    }
}
