using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The staff reads of photo links (FRD-28). A capture's status is derived
/// on read and never stored, so a list reads its tab's captures and their
/// items, evaluates each one's missing items and status in memory, and then
/// filters, searches and pages. The tabs hold hundreds of links, not
/// millions, and the statement count is fixed whatever the row count: the
/// captures, their items, their contacts, their Vehicle images records, the
/// issuers' names (kept in <see cref="StaffAccountReads"/>) and which of
/// their Cases are archived. The rows
/// shown then read their From and pairing (<see cref="ShownAsync"/>).
/// </summary>
public sealed class EfPhotoCaptureQueries(
    IDbContextFactory<PegasusDbContext> contextFactory,
    IStaffAccountQueries staffAccounts,
    TimeProvider timeProvider) : IPhotoCaptureQueries
{
    // The stored contact type code of a repairer (EfContactDirectoryAdministration).
    private const string RepairerRole = "repairer";

    private static readonly string[] SentByUsOrigins = [PhotoCaptureCodes.ToCode(PhotoCaptureOrigin.CaseLink)];

    private static readonly string[] ComingInOrigins =
    [
        PhotoCaptureCodes.ToCode(PhotoCaptureOrigin.StandingStaff),
        PhotoCaptureCodes.ToCode(PhotoCaptureOrigin.StandingForwarded),
        PhotoCaptureCodes.ToCode(PhotoCaptureOrigin.Website)
    ];

    public async Task<PhotoCaptureListPage> ListAsync(PhotoCaptureListFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var evaluated = await EvaluateTabAsync(filter.SentByUs, filter.Origin, cancellationToken);
        var search = filter.Search?.Trim();
        var matching = evaluated
            .Where(row => filter.State is null || row.State == filter.State)
            .Where(row => !filter.NoCaseYet || row.Row.Capture.CaseId is null)
            .Where(row => string.IsNullOrEmpty(search) || Matches(row.Row, search))
            .ToArray();
        var pageSize = Math.Max(1, filter.PageSize);
        var page = Math.Max(1, filter.Page);
        var rows = matching
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => row.Row)
            .ToArray();
        if (rows.Length == 0)
        {
            return new(rows, matching.Length);
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return new(await ShownAsync(context, rows, cancellationToken), matching.Length);
    }

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
        // The captures recorded on the Case, and those whose Vehicle images
        // record was merged into it before the settle sweep recorded the Case.
        var captures = await context.PhotoCaptures.AsNoTracking()
            .Where(capture => capture.CaseId == caseId
                || (capture.SubmissionGroupId != null
                    && context.ImageIntakes.Any(record => record.SubmissionGroupId == capture.SubmissionGroupId
                        && record.MergedIntoCaseId == caseId)))
            .OrderBy(capture => capture.CreatedAtUtc)
            .ThenBy(capture => capture.Id)
            .ToListAsync(cancellationToken);
        return await StoriesAsync(context, captures, cancellationToken);
    }

    public async Task<IReadOnlyList<CasePhotoCaptureStory>> ListForImageIntakeAsync(Guid imageIntakeId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var captures = await context.PhotoCaptures.AsNoTracking()
            .Where(capture => capture.SubmissionGroupId != null
                && context.ImageIntakes.Any(record => record.Id == imageIntakeId
                    && record.SubmissionGroupId == capture.SubmissionGroupId))
            .OrderBy(capture => capture.CreatedAtUtc)
            .ThenBy(capture => capture.Id)
            .ToListAsync(cancellationToken);
        return await StoriesAsync(context, captures, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<PhotoCaptureState, int>> CountByStateAsync(bool sentByUs, CancellationToken cancellationToken)
    {
        var evaluated = await EvaluateTabAsync(sentByUs, origin: null, cancellationToken);
        return evaluated
            .GroupBy(row => row.State)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    public async Task<PhotoCaptureTabCounts> CountByTabAsync(CancellationToken cancellationToken)
    {
        // One grouped count in SQL, not the tabs' evaluation (R806).
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var byOrigin = await context.PhotoCaptures.AsNoTracking()
            .GroupBy(capture => capture.Origin)
            .Select(group => new { Origin = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        return new(
            byOrigin.Where(item => ComingInOrigins.Contains(item.Origin, StringComparer.Ordinal)).Sum(item => item.Count),
            byOrigin.Where(item => SentByUsOrigins.Contains(item.Origin, StringComparer.Ordinal)).Sum(item => item.Count));
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

    private async Task<IReadOnlyList<EvaluatedRow>> EvaluateTabAsync(
        bool sentByUs,
        PhotoCaptureOrigin? origin,
        CancellationToken cancellationToken)
    {
        var origins = sentByUs ? SentByUsOrigins : ComingInOrigins;
        if (origin is { } wanted)
        {
            var code = PhotoCaptureCodes.ToCode(wanted);
            origins = origins.Contains(code, StringComparer.Ordinal) ? [code] : [];
        }

        if (origins.Length == 0)
        {
            return [];
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var captures = await context.PhotoCaptures.AsNoTracking()
            .Where(capture => origins.Contains(capture.Origin))
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

        var withItems = await EfPhotoCaptureStore.WithItemsAsync(context, captures, cancellationToken);
        var contacts = await ContactsAsync(context, captures, cancellationToken);
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
        var rows = new List<EvaluatedRow>(withItems.Count);
        foreach (var pending in withItems)
        {
            var capture = pending.Capture;
            var contact = capture.OrganizationId is { } organizationId && contacts.TryGetValue(organizationId, out var found)
                ? found
                : ((string Name, bool IsRepairer)?)null;
            var missing = PhotoCaptureRequiredItems.Evaluate(
                capture,
                pending.Items,
                PhotoCaptureRequiredItems.VinRequired(capture.Origin, contact?.IsRepairer ?? false));
            var caseArchived = capture.CaseId is { } caseId && archivedCases.Contains(caseId);
            var photographs = pending.Items.Count(item => PhotoCaptureStory.IsPhotographKind(item.Kind));
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
                    PhotoCapturePairing.None,
                    caseArchived,
                    photographs),
                state));
        }

        return rows;
    }

    /// <summary>
    /// What only the rows being shown carry, read after the page is cut and
    /// never for the whole tab: a Case link's From, which is the Case's
    /// Principal read now (R805), so a corrected Principal shows at once; and
    /// how a standing or website capture reached its Case. At most two
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

        var pairedGroupIds = rows
            .Where(row => row.Capture.Origin != PhotoCaptureOrigin.CaseLink
                && row.Capture.CaseId is not null
                && row.Capture.SubmissionGroupId is not null)
            .Select(row => row.Capture.SubmissionGroupId!.Value)
            .Distinct()
            .ToArray();
        var chosenByStaff = new Dictionary<(Guid GroupId, Guid CaseId), bool>();
        if (pairedGroupIds.Length > 0)
        {
            // The record's origin association owns the pairing decision, as
            // the merge itself reads it (EfImageIntakeStore), whoever ran the
            // merge step.
            var staff = nameof(ActorKind.Staff);
            var merged = await context.ImageIntakes.AsNoTracking()
                .Where(record => record.SubmissionGroupId != null
                    && record.MergedIntoCaseId != null
                    && EF.Parameter(pairedGroupIds).Contains(record.SubmissionGroupId.Value))
                .Select(record => new
                {
                    GroupId = record.SubmissionGroupId!.Value,
                    CaseId = record.MergedIntoCaseId!.Value,
                    ChosenByStaff = context.IntakeManualAssociations.Any(association =>
                        association.IntakeReceiptId == record.OriginReceiptId && association.ActorKind == staff)
                })
                .ToListAsync(cancellationToken);
            foreach (var record in merged)
            {
                chosenByStaff.TryAdd((record.GroupId, record.CaseId), record.ChosenByStaff);
            }
        }

        return rows.Select(row => Shown(row, principals, chosenByStaff)).ToArray();
    }

    private static PhotoCaptureListRow Shown(
        PhotoCaptureListRow row,
        Dictionary<Guid, string> principals,
        Dictionary<(Guid GroupId, Guid CaseId), bool> chosenByStaff)
    {
        var capture = row.Capture;
        bool? recordChosenByStaff = capture.SubmissionGroupId is { } groupId
            && capture.CaseId is { } caseId
            && chosenByStaff.TryGetValue((groupId, caseId), out var byStaff)
                ? byStaff
                : null;
        var organizationName = capture.Origin == PhotoCaptureOrigin.CaseLink
            && capture.CaseId is { } linkCaseId
            && principals.TryGetValue(linkCaseId, out var principal)
                ? principal
                : row.OrganizationName;
        return row with
        {
            OrganizationName = organizationName,
            Pairing = PhotoCaptureStatus.Pairing(capture, recordChosenByStaff)
        };
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
        return withItems
            .Select(pending =>
            {
                (string Name, bool IsRepairer)? contact = pending.Capture.OrganizationId is { } organizationId
                    && contacts.TryGetValue(organizationId, out var found)
                        ? found
                        : null;
                return new CasePhotoCaptureStory(pending.Capture, pending.Items, contact?.Name, contact?.IsRepairer ?? false);
            })
            .ToArray();
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
    private static bool Matches(PhotoCaptureListRow row, string search)
    {
        var capture = row.Capture;
        var compact = search.Replace(" ", string.Empty, StringComparison.Ordinal);
        return (compact.Length > 0
                && capture.NormalizedRegistration is { } registration
                && registration.Replace(" ", string.Empty, StringComparison.Ordinal)
                    .Contains(compact, StringComparison.OrdinalIgnoreCase))
            || Contains(capture.CustomerName, search)
            || Contains(capture.CaseReference, search)
            || Contains(row.OrganizationName, search);
    }

    private static bool Contains(string? value, string search) =>
        value is not null && value.Contains(search, StringComparison.OrdinalIgnoreCase);
}
