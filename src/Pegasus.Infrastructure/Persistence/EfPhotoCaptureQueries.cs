using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The staff reads of photo links (FRD-28). A capture's status is derived
/// on read and never stored. Direct links reads its whole tab's captures and
/// evaluates each one's missing items and status in memory, once per request:
/// that one evaluation gives the scopes' counts, before any filter, and the
/// rows the scope and search filters keep, which are then paged (v51 N;
/// R948). Standing links filters by nothing a capture derives, only its
/// standing link and the search, so it reads its tab narrowly, cuts the page
/// and evaluates the rows it shows alone (#2088; <see cref="ListStandingAsync"/>).
/// Of the items an evaluation reads only what the required items rule and
/// the photograph count use, grouped in SQL (<see cref="ItemFactsAsync"/>),
/// so a repeated photo of the same thing adds to a count and not a row;
/// issue #2020 measured the lists at scale before this and keeps measuring
/// them (<c>PhotoCaptureListScaleTests</c>; R881, R882). The statement count
/// is fixed whatever the row count: the captures, their item facts, their
/// contacts, which quick Case links were handed on, their Vehicle images
/// records, the issuers' names (kept in <see cref="StaffAccountReads"/>) and
/// which of their Cases are archived. The rows shown then read their From
/// and their preview photographs (<see cref="ShownAsync"/>).
/// </summary>
public sealed class EfPhotoCaptureQueries(
    IDbContextFactory<PegasusDbContext> contextFactory,
    IStaffAccountQueries staffAccounts,
    TimeProvider timeProvider) : IPhotoCaptureQueries
{
    // The stored contact type code of a repairer (EfContactDirectoryAdministration).
    private const string RepairerRole = "repairer";

    private static readonly string[] SentByUsOrigins = [PhotoCaptureCodes.ToCode(PhotoCaptureOrigin.CaseLink)];

    private static readonly string[] StandingOrigins =
    [
        PhotoCaptureCodes.ToCode(PhotoCaptureOrigin.StandingStaff),
        PhotoCaptureCodes.ToCode(PhotoCaptureOrigin.StandingForwarded),
        PhotoCaptureCodes.ToCode(PhotoCaptureOrigin.Website)
    ];

    /// <summary>The photograph kinds' stored codes: the items the lists count and the only ones the required items rule reads.</summary>
    private static readonly string[] PhotographKinds =
    [
        .. Enum.GetValues<PhotoCaptureItemKind>()
            .Where(PhotoCaptureStory.IsPhotographKind)
            .Select(kind => PhotoCaptureCodes.ToCode(kind))
    ];

    public async Task<PhotoCaptureListPage> ListAsync(PhotoCaptureListFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (!filter.Direct)
        {
            return await ListStandingAsync(filter, cancellationToken);
        }

        // One evaluation of the whole tab gives both the scopes' counts,
        // before any filter, and the rows the filters keep (#2020).
        var now = timeProvider.GetUtcNow();
        var evaluated = await EvaluateDirectLinksAsync(cancellationToken);
        var countsByScope = new Dictionary<PhotoCaptureScope, int>();
        foreach (var scope in Enum.GetValues<PhotoCaptureScope>())
        {
            countsByScope[scope] = evaluated.Count(row => InScope(row, scope, now));
        }

        var search = filter.Search?.Trim();
        var matching = evaluated
            .Where(row => InScope(row, filter.Scope, now))
            .Where(row => string.IsNullOrEmpty(search) || Matches(
                row.Row.Capture.NormalizedRegistration,
                row.Row.Capture.CustomerName,
                row.Row.Capture.CaseReference,
                row.Row.OrganizationName,
                search))
            .ToArray();
        var rows = matching
            .Skip(PageStart(filter))
            .Take(PageSize(filter))
            .Select(row => row.Row)
            .ToArray();
        if (rows.Length == 0)
        {
            return new(rows, matching.Length, countsByScope);
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return new(await ShownAsync(context, rows, cancellationToken), matching.Length, countsByScope);
    }

    private static int PageSize(PhotoCaptureListFilter filter) => Math.Max(1, filter.PageSize);

    private static int PageStart(PhotoCaptureListFilter filter) => (Math.Max(1, filter.Page) - 1) * PageSize(filter);

    /// <summary>What the Standing links filters read of one capture: its place in the list and what the search matches.</summary>
    private sealed record StandingListing(
        Guid Id,
        string? NormalizedRegistration,
        string? CustomerName,
        string? CaseReference,
        string? ContactName);

    /// <summary>
    /// One page of the Standing links tab. Nothing the tab filters or counts
    /// by is derived from a capture's items, so the tab is read narrowly,
    /// newest first, the search is applied to that, and only the page's
    /// captures are loaded and evaluated: a tab of any size costs the items
    /// of the rows it shows (#2088). A capture's contact is read with it, as
    /// the search matches the contact's name. The page is cut from one read
    /// and loaded by a second, so a capture made between them shows on the
    /// next load.
    /// </summary>
    private async Task<PhotoCaptureListPage> ListStandingAsync(PhotoCaptureListFilter filter, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var tab = context.PhotoCaptures.AsNoTracking()
            .Where(capture => StandingOrigins.Contains(capture.Origin));
        if (filter.StandingLinkId is { } standingLinkId)
        {
            tab = tab.Where(capture => capture.StandingLinkId == standingLinkId);
        }

        var listed = await tab
            .OrderByDescending(capture => capture.CreatedAtUtc)
            .ThenByDescending(capture => capture.Id)
            .Select(capture => new StandingListing(
                capture.Id,
                capture.NormalizedRegistration,
                capture.CustomerName,
                capture.CaseReference,
                context.Organizations
                    .Where(organization => organization.Id == capture.OrganizationId)
                    .Select(organization => organization.Name)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
        var search = filter.Search?.Trim();
        var matching = listed
            .Where(row => string.IsNullOrEmpty(search)
                || Matches(row.NormalizedRegistration, row.CustomerName, row.CaseReference, row.ContactName, search))
            .ToArray();
        var ids = matching
            .Skip(PageStart(filter))
            .Take(PageSize(filter))
            .Select(row => row.Id)
            .ToArray();
        var noCounts = new Dictionary<PhotoCaptureScope, int>();
        if (ids.Length == 0)
        {
            return new([], matching.Length, noCounts);
        }

        var captures = await context.PhotoCaptures.AsNoTracking()
            .Where(capture => EF.Parameter(ids).Contains(capture.Id))
            .ToListAsync(cancellationToken);
        // Back into the list's order by each id's place in it: SQL Server and
        // .NET order a uniqueidentifier differently, so the ids are never sorted here.
        var place = new Dictionary<Guid, int>(ids.Length);
        for (var index = 0; index < ids.Length; index++)
        {
            place[ids[index]] = index;
        }

        captures.Sort((left, right) => place[left.Id].CompareTo(place[right.Id]));
        var evaluated = await EvaluateAsync(context, captures, cancellationToken);
        var rows = evaluated.Select(row => row.Row).ToArray();
        return new(await ShownAsync(context, rows, cancellationToken), matching.Length, noCounts);
    }

    private static bool InScope(EvaluatedRow row, PhotoCaptureScope scope, DateTimeOffset now) =>
        PhotoCaptureStatus.InScope(
            scope,
            row.State,
            PhotoCaptureStatus.DueReminder(row.Row, now),
            PhotoCaptureAccess.IsLive(row.Row.Capture, now, row.Row.CaseArchived));

    public async Task<PhotoCaptureListRow?> GetRowAsync(Guid captureId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var captures = await context.PhotoCaptures.AsNoTracking()
            .Where(capture => capture.Id == captureId)
            .ToListAsync(cancellationToken);
        var evaluated = await EvaluateAsync(context, captures, cancellationToken);
        return evaluated.Count == 0 ? null : (await ShownAsync(context, [evaluated[0].Row], cancellationToken))[0];
    }

    public async Task<IReadOnlyList<CasePhotoCaptureStory>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // The captures recorded on the Case, and those any of whose Vehicle
        // images records was merged into it before the settle sweep recorded
        // the Case: each batch makes its own record (FRD-28).
        var mergedReceipts = MemberReceipts(
            context, context.ImageIntakes.Where(record => record.MergedIntoCaseId == caseId));
        var captures = await context.PhotoCaptures.AsNoTracking()
            .Where(capture => capture.CaseId == caseId
                || context.PhotoCaptureItems.Any(item => item.CaptureId == capture.Id
                    && mergedReceipts.Contains(item.StagedReceiptId)))
            .OrderBy(capture => capture.CreatedAtUtc)
            .ThenBy(capture => capture.Id)
            .ToListAsync(cancellationToken);
        return await StoriesAsync(context, captures, cancellationToken);
    }

    public async Task<IReadOnlyList<CasePhotoCaptureStory>> ListForImageIntakeAsync(Guid imageIntakeId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var recordReceipts = MemberReceipts(
            context, context.ImageIntakes.Where(record => record.Id == imageIntakeId));
        var captures = await context.PhotoCaptures.AsNoTracking()
            .Where(capture => context.PhotoCaptureItems.Any(item => item.CaptureId == capture.Id
                && recordReceipts.Contains(item.StagedReceiptId)))
            .OrderBy(capture => capture.CreatedAtUtc)
            .ThenBy(capture => capture.Id)
            .ToListAsync(cancellationToken);
        return await StoriesAsync(context, captures, cancellationToken);
    }

    /// <summary>
    /// The staged receipts of the records' submission groups. A capture names
    /// only its latest batch's group (<c>SubmissionGroupId</c>), so a record
    /// finds its capture through the items whose receipts are its group's
    /// members: every batch's record finds it, not only the latest.
    /// </summary>
    private static IQueryable<Guid?> MemberReceipts(PegasusDbContext context, IQueryable<ImageIntakeEntity> records) =>
        records.SelectMany(record => context.IntakeSubmissionGroupMembers
            .Where(member => member.GroupId == record.SubmissionGroupId)
            .Select(member => (Guid?)member.StagedReceiptId));

    public async Task<IReadOnlyList<StandingPhotoLinkListing>> ListStandingLinksAsync(CancellationToken cancellationToken)
    {
        // Two statements: the links with their contacts, and one grouped
        // count of the captures each started (v51 P; R948).
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var links = await EfStandingPhotoLinkStore.ReadAllAsync(context, link => link.RetiredAtUtc == null, cancellationToken);
        if (links.Count == 0)
        {
            return [];
        }

        var submissions = await context.PhotoCaptures.AsNoTracking()
            .Where(capture => capture.StandingLinkId != null)
            .GroupBy(capture => capture.StandingLinkId!.Value)
            .Select(group => new { LinkId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.LinkId, item => item.Count, cancellationToken);
        return links
            .OrderBy(link => link.Kind == StandingPhotoLinkKind.Website ? 1 : 0)
            .ThenBy(link => link.OrganizationName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(link => link.Id)
            .Select(link => new StandingPhotoLinkListing(link, submissions.GetValueOrDefault(link.Id)))
            .ToArray();
    }

    public async Task<int> CountAwaitingAsync(CancellationToken cancellationToken)
    {
        // Awaiting is a live link not yet complete, so only the Direct links
        // that have not expired and are not cancelled are evaluated.
        var now = timeProvider.GetUtcNow();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var captures = await context.PhotoCaptures.AsNoTracking()
            .Where(capture => SentByUsOrigins.Contains(capture.Origin)
                && capture.RevokedAtUtc == null
                && capture.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);
        var evaluated = await EvaluateAsync(context, captures, cancellationToken);
        return evaluated.Count(row => InScope(row, PhotoCaptureScope.Awaiting, now));
    }

    public async Task<IReadOnlyList<PhotoLinkAct>> ListHistoryAsync(Guid captureId, CancellationToken cancellationToken)
    {
        var aggregateId = captureId.ToString("D");
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.ActionHistory.AsNoTracking()
            .Where(item => item.AggregateType == EfPhotoCaptureStore.HistoryAggregateType && item.AggregateId == aggregateId)
            .OrderBy(item => item.OccurredAtUtc)
            .ThenBy(item => item.Id)
            .Select(item => new { item.EventKind, item.OccurredAtUtc, item.ActorSubjectId })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return [];
        }

        var staffIds = rows
            .Select(row => Guid.TryParse(row.ActorSubjectId, out var staffId) ? staffId : Guid.Empty)
            .Where(staffId => staffId != Guid.Empty)
            .Distinct()
            .ToArray();
        var names = staffIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await staffAccounts.GetNamesAsync(staffIds, cancellationToken);
        return rows
            .Select(row => new PhotoLinkAct(
                row.EventKind,
                row.OccurredAtUtc,
                Guid.TryParse(row.ActorSubjectId, out var staffId) && names.TryGetValue(staffId, out var name) ? name : null))
            .ToArray();
    }

    private sealed record EvaluatedRow(PhotoCaptureListRow Row, PhotoCaptureState State);

    /// <summary>Every Direct link, newest first, each with its missing items and status.</summary>
    private async Task<IReadOnlyList<EvaluatedRow>> EvaluateDirectLinksAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var captures = await context.PhotoCaptures.AsNoTracking()
            .Where(capture => SentByUsOrigins.Contains(capture.Origin))
            .OrderByDescending(capture => capture.CreatedAtUtc)
            .ThenByDescending(capture => capture.Id)
            .ToListAsync(cancellationToken);
        return await EvaluateAsync(context, captures, cancellationToken);
    }

    /// <summary>The rows of <paramref name="captures"/>, in their order, each with its missing items and status.</summary>
    private async Task<IReadOnlyList<EvaluatedRow>> EvaluateAsync(
        PegasusDbContext context,
        List<PhotoCaptureEntity> captures,
        CancellationToken cancellationToken)
    {
        if (captures.Count == 0)
        {
            return [];
        }

        var mapped = captures.Select(capture => EfPhotoCaptureStore.Map(capture)).ToArray();
        var itemFacts = await ItemFactsAsync(context, captures, cancellationToken);
        var contacts = await ContactsAsync(context, captures, cancellationToken);
        // Read before the required items, so a handed-on link's missing VIN,
        // its status, the tab's counts and its "Sent on" agree (R874).
        var handedOn = await HandedOnAsync(context, mapped, cancellationToken);
        var groupIds = captures
            .Where(capture => capture.SubmissionGroupId is not null)
            .Select(capture => capture.SubmissionGroupId!.Value)
            .Distinct()
            .ToArray();
        var records = new Dictionary<Guid, (Guid Id, string Reference)>();
        if (groupIds.Length > 0)
        {
            // At most one Vehicle images record per group (a filtered unique index).
            var registered = await context.ImageIntakes.AsNoTracking()
                .Where(record => record.SubmissionGroupId != null
                    && EF.Parameter(groupIds).Contains(record.SubmissionGroupId.Value))
                .Select(record => new { GroupId = record.SubmissionGroupId!.Value, record.Id, record.ImageIntakeReference })
                .ToListAsync(cancellationToken);
            foreach (var record in registered)
            {
                records.TryAdd(record.GroupId, (record.Id, record.ImageIntakeReference));
            }
        }

        var issuerIds = captures
            .Where(capture => capture.IssuedByStaffId is not null)
            .Select(capture => capture.IssuedByStaffId!.Value)
            .Distinct()
            .ToArray();
        var issuers = issuerIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await staffAccounts.GetNamesAsync(issuerIds, cancellationToken);

        // A capture whose Case is archived reads Expired and is due nothing (R793).
        var caseIds = captures
            .Where(capture => capture.CaseId is not null)
            .Select(capture => capture.CaseId!.Value)
            .Distinct()
            .ToArray();
        var archivedCases = caseIds.Length == 0
            ? []
            : (await context.CaseWorkflows.AsNoTracking()
                    .Where(workflow => workflow.ArchivedAtUtc != null && EF.Parameter(caseIds).Contains(workflow.CaseId))
                    .Select(workflow => workflow.CaseId)
                    .ToListAsync(cancellationToken))
                .ToHashSet();

        var now = timeProvider.GetUtcNow();
        var rows = new List<EvaluatedRow>(mapped.Length);
        foreach (var capture in mapped)
        {
            var contact = capture.OrganizationId is { } organizationId && contacts.TryGetValue(organizationId, out var found)
                ? found
                : ((string Name, bool IsRepairer)?)null;
            var facts = itemFacts.TryGetValue(capture.Id, out var read) ? read : ItemFacts.None;
            var missing = PhotoCaptureRequiredItems.Evaluate(
                capture,
                facts.Presence,
                PhotoCaptureRequiredItems.VinRequired(capture, contact?.IsRepairer ?? false, handedOn.Contains(capture.Id)));
            var caseArchived = capture.CaseId is { } caseId && archivedCases.Contains(caseId);
            var photographs = facts.Photographs;
            var state = PhotoCaptureStatus.Evaluate(capture, missing, photographs, now, caseArchived);
            (Guid Id, string Reference)? record = capture.SubmissionGroupId is { } groupId
                && records.TryGetValue(groupId, out var groupRecord)
                    ? groupRecord
                    : null;
            rows.Add(new(
                new PhotoCaptureListRow(
                    capture,
                    contact?.Name,
                    record?.Reference,
                    record?.Id,
                    missing.Select(item => item.Label).ToArray(),
                    capture.IssuedByStaffId is { } issuer && issuers.TryGetValue(issuer, out var name) ? name : null,
                    caseArchived,
                    photographs,
                    SentOnToCustomer: handedOn.Contains(capture.Id)),
                state));
        }

        return rows;
    }

    /// <summary>What a list needs of one capture's items: what the required items rule reads, and how many photographs it holds.</summary>
    private sealed class ItemFacts
    {
        public static readonly ItemFacts None = new();

        public List<PhotoCaptureItemPresence> Presence { get; } = [];

        public int Photographs { get; set; }
    }

    /// <summary>
    /// What the lists need of the captures' items, in one statement for all
    /// of them and never the item rows themselves (#2020). Only photographs
    /// are read: they are what the Photos column counts and what decides
    /// Opened or Photos coming in, paperwork or video alone not being photos
    /// (R877), and the required items rule asks for nothing else. They are
    /// grouped by capture and kind with only the columns the rule reads of
    /// that kind, a damage photo's area and stage and an overview's position,
    /// so a repeated photo of the same thing adds to its group's count and not
    /// a row; the counts summed are the capture's photographs, every received
    /// item row of a photograph kind. An item is received once its upload
    /// reached intake (<c>StagedReceiptId</c>): a row whose upload failed
    /// after it was written is not a photo the link has, as the phone and the
    /// Send count them. The capture's own <c>PhotoCount</c> is not read
    /// instead: it is the caps' counter, kept by the item add alone, and
    /// nothing holds it equal to the rows.
    /// </summary>
    private static async Task<Dictionary<Guid, ItemFacts>> ItemFactsAsync(
        PegasusDbContext context,
        List<PhotoCaptureEntity> captures,
        CancellationToken cancellationToken)
    {
        // One JSON parameter, so no tab of captures can reach the parameter limit.
        var ids = captures.Select(capture => capture.Id).ToArray();
        var photographKinds = PhotographKinds;
        // Constants, so the grouping and the select list carry the same text.
        var overview = PhotoCaptureCodes.ToCode(PhotoCaptureItemKind.Overview);
        var damage = PhotoCaptureCodes.ToCode(PhotoCaptureItemKind.Damage);
        var groups = await context.PhotoCaptureItems.AsNoTracking()
            .Where(item => EF.Parameter(ids).Contains(item.CaptureId)
                && item.StagedReceiptId != null
                && photographKinds.Contains(item.Kind))
            .GroupBy(item => new
            {
                item.CaptureId,
                item.Kind,
                Area = item.Kind == EF.Constant(damage) ? item.Area : null,
                Stage = item.Kind == EF.Constant(damage) ? item.Stage : null,
                Position = item.Kind == EF.Constant(overview) ? item.Position : 0
            })
            .Select(group => new
            {
                group.Key.CaptureId,
                group.Key.Kind,
                group.Key.Area,
                group.Key.Stage,
                group.Key.Position,
                Items = group.Count()
            })
            .ToListAsync(cancellationToken);
        var facts = new Dictionary<Guid, ItemFacts>();
        foreach (var group in groups)
        {
            if (!facts.TryGetValue(group.CaptureId, out var held))
            {
                held = new ItemFacts();
                facts.Add(group.CaptureId, held);
            }

            held.Presence.Add(new(
                PhotoCaptureCodes.ParseItemKind(group.Kind),
                group.Area,
                PhotoCaptureCodes.ParseOptionalStage(group.Stage),
                group.Position));
            held.Photographs += group.Items;
        }

        return facts;
    }

    /// <summary>The photographs a list row previews (v51 N; R948).</summary>
    private const int PreviewPhotographs = 3;

    /// <summary>
    /// What only the rows being shown carry, read after the page is cut and
    /// never for the whole tab: a Case link's From, which is the Case's
    /// Principal read now (R805), so a corrected Principal shows at once; and
    /// the first photographs each row previews (v51 N; R948). At most two
    /// statements, each for the page's rows.
    /// </summary>
    private static async Task<PhotoCaptureListRow[]> ShownAsync(
        PegasusDbContext context,
        PhotoCaptureListRow[] rows,
        CancellationToken cancellationToken)
    {
        var linkCaseIds = rows
            .Where(row => row.Capture.Origin == PhotoCaptureOrigin.CaseLink && row.Capture.CaseId is not null)
            .Select(row => row.Capture.CaseId!.Value)
            .Distinct()
            .ToArray();
        var principals = linkCaseIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await context.Cases.AsNoTracking()
                .Where(caseEntity => EF.Parameter(linkCaseIds).Contains(caseEntity.Id))
                .Select(caseEntity => new { caseEntity.Id, caseEntity.Principal.Organization.Name })
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        var withPhotographs = rows
            .Where(row => row.Photographs > 0)
            .Select(row => row.Capture.Id)
            .ToArray();
        var previews = new Dictionary<Guid, List<PhotoCapturePreview>>();
        if (withPhotographs.Length > 0)
        {
            // At most PreviewPhotographs rows per capture, cut in SQL (one
            // CROSS APPLY), so a long capture costs the page no more than a short one.
            var items = await context.PhotoCaptures.AsNoTracking()
                .Where(capture => EF.Parameter(withPhotographs).Contains(capture.Id))
                .SelectMany(capture => context.PhotoCaptureItems.AsNoTracking()
                    .Where(item => item.CaptureId == capture.Id
                        && EF.Parameter(PhotographKinds).Contains(item.Kind)
                        && (item.IntakeReceiptId != null || item.StagedReceiptId != null))
                    .OrderBy(item => item.StoryGroup)
                    .ThenBy(item => item.StorySequence)
                    .ThenBy(item => item.Ordinal)
                    .Take(PreviewPhotographs)
                    .Select(item => new
                    {
                        item.CaptureId,
                        item.StoryGroup,
                        item.StorySequence,
                        item.Ordinal,
                        item.IntakeReceiptId,
                        item.IntakeAssetId,
                        item.StagedReceiptId,
                        item.FileName,
                        item.MediaType,
                        item.ContentHash
                    }))
                .ToListAsync(cancellationToken);
            foreach (var item in items
                .OrderBy(item => item.StoryGroup)
                .ThenBy(item => item.StorySequence)
                .ThenBy(item => item.Ordinal))
            {
                var shown = previews.TryGetValue(item.CaptureId, out var list) ? list : previews[item.CaptureId] = [];

                // A settled photograph is its intake asset; before the settle it is the staged receipt.
                shown.Add(item.IntakeReceiptId is { } receiptId && item.IntakeAssetId is { } assetId
                    ? new PhotoCapturePreview(receiptId, assetId, item.FileName, item.MediaType, item.ContentHash)
                    : new PhotoCapturePreview(item.StagedReceiptId!.Value, null, item.FileName, item.MediaType, item.ContentHash));
            }
        }

        return rows.Select(row => row with
        {
            OrganizationName = row.Capture.Origin == PhotoCaptureOrigin.CaseLink
                && row.Capture.CaseId is { } linkCaseId
                && principals.TryGetValue(linkCaseId, out var principal)
                    ? principal
                    : row.OrganizationName,
            Preview = previews.TryGetValue(row.Capture.Id, out var preview) ? preview : []
        }).ToArray();
    }

    private static async Task<IReadOnlyList<CasePhotoCaptureStory>> StoriesAsync(
        PegasusDbContext context,
        List<PhotoCaptureEntity> captures,
        CancellationToken cancellationToken)
    {
        if (captures.Count == 0)
        {
            return [];
        }

        var withItems = await EfPhotoCaptureStore.WithItemsAsync(context, captures, cancellationToken);
        var contacts = await ContactsAsync(context, captures, cancellationToken);
        var handedOn = await HandedOnAsync(context, withItems.Select(pending => pending.Capture), cancellationToken);
        return withItems
            .Select(pending =>
            {
                (string Name, bool IsRepairer)? contact = pending.Capture.OrganizationId is { } organizationId
                    && contacts.TryGetValue(organizationId, out var found)
                        ? found
                        : null;
                return new CasePhotoCaptureStory(
                    pending.Capture,
                    pending.Items,
                    contact?.Name,
                    contact?.IsRepairer ?? false,
                    handedOn.Contains(pending.Capture.Id));
            })
            .ToArray();
    }

    /// <summary>
    /// The quick Case links among <paramref name="captures"/> their firm
    /// handed on to their customer, which a Handoff token on it records
    /// (R798): one statement, and none when no capture is a quick Case link.
    /// </summary>
    private static async Task<HashSet<Guid>> HandedOnAsync(
        PegasusDbContext context,
        IEnumerable<PhotoCapture> captures,
        CancellationToken cancellationToken)
    {
        var ids = captures
            .Where(HandOnCaseLink.IsQuickCaseLink)
            .Select(capture => capture.Id)
            .ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        var handoff = PhotoCaptureCodes.ToCode(PhotoLinkTokenPurpose.Handoff);
        return (await context.PhotoCaptureTokens.AsNoTracking()
                .Where(token => token.Purpose == handoff && EF.Parameter(ids).Contains(token.CaptureId))
                .Select(token => token.CaptureId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();
    }

    /// <summary>The captures' contacts in one statement: each one's name and whether it is a repairer.</summary>
    private static async Task<IReadOnlyDictionary<Guid, (string Name, bool IsRepairer)>> ContactsAsync(
        PegasusDbContext context,
        List<PhotoCaptureEntity> captures,
        CancellationToken cancellationToken)
    {
        var organizationIds = captures
            .Where(capture => capture.OrganizationId is not null)
            .Select(capture => capture.OrganizationId!.Value)
            .Distinct()
            .ToArray();
        if (organizationIds.Length == 0)
        {
            return new Dictionary<Guid, (string Name, bool IsRepairer)>();
        }

        var rows = await context.Organizations.AsNoTracking()
            .Where(organization => EF.Parameter(organizationIds).Contains(organization.Id))
            .Select(organization => new { organization.Id, organization.Name, organization.Role })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(
            row => row.Id,
            row => (row.Name, string.Equals(row.Role, RepairerRole, StringComparison.Ordinal)));
    }

    /// <summary>
    /// The search: the registration typed with or without its space, the
    /// customer's name, the Case reference or the contact's name, ignoring case.
    /// </summary>
    private static bool Matches(
        string? registration,
        string? customerName,
        string? caseReference,
        string? contactName,
        string search)
    {
        var compact = search.Replace(" ", string.Empty, StringComparison.Ordinal);
        return (compact.Length > 0
                && registration is not null
                && registration.Replace(" ", string.Empty, StringComparison.Ordinal)
                    .Contains(compact, StringComparison.OrdinalIgnoreCase))
            || Contains(customerName, search)
            || Contains(caseReference, search)
            || Contains(contactName, search);
    }

    private static bool Contains(string? value, string search) =>
        value is not null && value.Contains(search, StringComparison.OrdinalIgnoreCase);
}
