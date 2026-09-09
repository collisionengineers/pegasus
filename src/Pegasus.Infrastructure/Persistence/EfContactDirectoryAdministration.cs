using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Address;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>The one directory writer for external organisations and their
/// roles. Principal policy stays on <see cref="PrincipalEntity"/>; this store
/// never copies credentials or report policy into a contact row.</summary>
public sealed class EfContactDirectoryAdministration(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider)
    : IContactDirectoryAdministration, IContactDirectoryQueries
{
    private const string SaveKind = "save_contact";
    private readonly IDbContextFactory<PegasusDbContext> _contextFactory = contextFactory;
    private readonly TimeProvider _timeProvider = timeProvider;

    public Task<ContactDirectoryRecord> SaveAsync(SaveContactRequest request, CancellationToken cancellationToken)
    {
        var normalized = ContactDirectoryPolicy.Normalize(request);
        return EfOrganizationAdministration.ExecuteWithConcurrencyRetryAsync(
            token => SaveOnceAsync(normalized, token), IsRetryable, cancellationToken);
    }

    public async Task<ContactDirectoryRecord?> GetAsync(ActionActor actor, Guid organizationId, CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageOrganizationsAndPrincipals);
        if (organizationId == Guid.Empty) throw new ArgumentException("A contact identifier is required.", nameof(organizationId));
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Organizations.AsNoTracking()
            .Include(item => item.ContactRoles)
            .Include(item => item.PrincipalLinks)
            .Include(item => item.Principals)
            .SingleOrDefaultAsync(item => item.Id == organizationId, cancellationToken);
        if (entity is null) return null;
        var latest = await LatestCaseRows(context)
            .Where(item => item.OrganizationId == entity.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return ToRecord(entity, latest);
    }

    public async Task<IReadOnlyList<ContactDirectoryRecord>> ListAsync(ContactDirectoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.ManageOrganizationsAndPrincipals);
        var limit = Math.Clamp(query.Limit, 1, ContactDirectoryPolicy.MaximumListLimit);
        var prefix = query.Search?.Trim();
        var role = query.Role is { } selectedRole ? ToCode(selectedRole) : null;
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var contacts = context.Organizations.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(prefix)) contacts = contacts.Where(item => item.Name.StartsWith(prefix));
        if (role is not null) contacts = contacts.Where(item => item.ContactRoles.Any(item => item.Role == role));
        var latestByOrganization = LatestCaseRows(context);
        var rows = from contact in contacts
                   join latest in latestByOrganization on contact.Id equals latest.OrganizationId into latestGroup
                   from latest in latestGroup.DefaultIfEmpty()
                   select new ContactListRow
                   {
                       OrganizationId = contact.Id,
                       Name = contact.Name,
                       FirstRole = context.Set<ContactRoleEntity>()
                           .Where(item => item.OrganizationId == contact.Id)
                           .OrderBy(item => item.Role)
                           .Select(item => item.Role)
                           .FirstOrDefault(),
                       ReceivedAtUtc = (DateTimeOffset?)latest.ReceivedAtUtc,
                       Reference = latest.Reference
                   };
        var ordered = query.Sort switch
        {
            ContactSort.Type => rows.OrderBy(item => item.FirstRole).ThenBy(item => item.Name).ThenBy(item => item.OrganizationId),
            ContactSort.LastCase => rows.OrderByDescending(item => item.ReceivedAtUtc.HasValue)
                .ThenByDescending(item => item.ReceivedAtUtc).ThenBy(item => item.Reference)
                .ThenBy(item => item.Name).ThenBy(item => item.OrganizationId),
            _ => rows.OrderBy(item => item.Name).ThenBy(item => item.OrganizationId)
        };
        var selected = await ordered.Take(limit).ToArrayAsync(cancellationToken);
        return await LoadRecordsAsync(
            context,
            selected.Select(item => item.OrganizationId).ToArray(),
            selected.Where(item => item.ReceivedAtUtc.HasValue && item.Reference is not null)
                .ToDictionary(item => item.OrganizationId,
                    item => new LatestCaseRow
                    {
                        OrganizationId = item.OrganizationId,
                        ReceivedAtUtc = item.ReceivedAtUtc!.Value,
                        Reference = item.Reference!
                    }),
            cancellationToken);
    }

    public async Task<IReadOnlyList<ContactDirectoryRecord>> FindPossibleMatchesAsync(ActionActor actor, string name, CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageOrganizationsAndPrincipals);
        var normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length < 2) return [];
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var ids = await context.Organizations.AsNoTracking()
            .Where(item => item.Name.StartsWith(normalized)).OrderBy(item => item.Name).ThenBy(item => item.Id)
            .Take(10).Select(item => item.Id).ToArrayAsync(cancellationToken);
        return await LoadRecordsAsync(context, ids, latestCases: null, cancellationToken);
    }

    public async Task<IReadOnlyList<ContactDirectoryRecord>> ListClaimSourcesAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var claimSourceRole = ToCode(ContactRole.ClaimSource);
        var ids = await context.Organizations.AsNoTracking()
            .Where(item => item.Active
                && item.ContactRoles.Any(role => role.Role == claimSourceRole))
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        return await LoadRecordsAsync(context, ids, latestCases: null, cancellationToken);
    }

    public async Task<IReadOnlyList<PrincipalAdministrationDetails>> ListPrincipalChoicesAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageOrganizationsAndPrincipals);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Principals.AsNoTracking().Include(item => item.Organization)
            .Where(item => item.IsActive).OrderBy(item => item.Organization.Name).ThenBy(item => item.Code).ToArrayAsync(cancellationToken);
        return rows.Select(ToPrincipalChoice).ToArray();
    }

    private async Task<ContactDirectoryRecord> SaveOnceAsync(SaveContactRequest request, CancellationToken cancellationToken)
    {
        var requestHash = EfOrganizationAdministration.HashRequest(new
        {
            command = SaveKind, request.OrganizationId, request.ExpectedVersion, request.Name, request.ContactPerson,
            request.Email, request.Telephone, request.Address, request.Postcode, request.Active, request.Roles, request.PrincipalCode,
            request.PrincipalInspectionMode, request.PrincipalAssociations, request.GuidanceTemplate, request.GuidanceTemplateVersion
        });
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var receipt = await EfOrganizationAdministration.FindReceiptAsync(context, request.OperationKey, cancellationToken);
        if (receipt is not null)
        {
            if (receipt.CommandKind != SaveKind || !EfOrganizationAdministration.SameHash(receipt.RequestHash, requestHash))
                throw new ContactDirectoryException(ContactDirectoryError.OperationConflict);
            var replay = JsonSerializer.Deserialize<ContactDirectoryRecord>(receipt.ResultJson, EfOrganizationAdministration.SerializerOptions)
                ?? throw new ContactDirectoryException(ContactDirectoryError.OperationConflict);
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }

        var entity = await context.Organizations.Include(item => item.ContactRoles).Include(item => item.PrincipalLinks).Include(item => item.Principals)
            .SingleOrDefaultAsync(item => item.Id == request.OrganizationId, cancellationToken);
        var creating = entity is null;
        if (creating)
        {
            if (request.ExpectedVersion != 0) throw new ContactDirectoryException(ContactDirectoryError.StaleVersion);
            var normalizedName = request.Name.ToUpperInvariant();
            if (await context.Organizations.AnyAsync(item => item.NormalizedName == normalizedName, cancellationToken))
                throw new ContactDirectoryException(ContactDirectoryError.DuplicateOrganizationName);
            entity = new OrganizationEntity { Id = request.OrganizationId, Name = request.Name, Version = 0 };
            context.Organizations.Add(entity);
        }
        else if (entity!.Version != request.ExpectedVersion)
        {
            throw new ContactDirectoryException(ContactDirectoryError.StaleVersion);
        }
        if (!creating)
        {
            await EfEditScopeStore.RequireAsync(
                context, EditScopeKind.Contact, entity!.Id, entity.Version,
                request.ExpectedVersion, request.Actor, request.EditLeaseToken,
                _timeProvider.GetUtcNow(), cancellationToken);
        }

        var before = creating ? null : ToRecord(entity!);
        var guidanceChanged = entity!.GuidanceTemplate != request.GuidanceTemplate;
        var changed = entity.Name != request.Name || entity.ContactPerson != request.ContactPerson || entity.Email != request.Email
            || entity.Telephone != request.Telephone || entity.Address != request.Address || entity.Postcode != request.Postcode || entity.Active != request.Active
            || guidanceChanged
            || !entity.ContactRoles.Select(item => item.Role).Order().SequenceEqual(request.Roles.Select(ToCode).Order());
        entity.Name = request.Name; entity.ContactPerson = request.ContactPerson; entity.Email = request.Email;
        entity.Telephone = request.Telephone; entity.Address = request.Address; entity.Postcode = request.Postcode; entity.Active = request.Active;
        if (guidanceChanged)
        {
            entity.GuidanceTemplate = request.GuidanceTemplate;
            entity.GuidanceTemplateVersion = checked(entity.GuidanceTemplateVersion + 1);
        }
        entity.ContactRoles.Clear();
        foreach (var role in request.Roles) entity.ContactRoles.Add(new() { OrganizationId = entity.Id, Role = ToCode(role) });

        var isPrincipal = request.Roles.Contains(ContactRole.Principal);
        if (isPrincipal && entity.Principals.Count == 0)
        {
            if (await context.Principals.AnyAsync(item => item.Code == request.PrincipalCode, cancellationToken))
                throw new ContactDirectoryException(ContactDirectoryError.DuplicatePrincipalCode);
            var lineage = new PrincipalSequenceLineageEntity { Id = Guid.NewGuid(), CreatedAtUtc = _timeProvider.GetUtcNow() };
            context.PrincipalSequenceLineages.Add(lineage);
            entity.Principals.Add(new PrincipalEntity { Id = Guid.NewGuid(), OrganizationId = entity.Id, Code = request.PrincipalCode!, SequenceLineageId = lineage.Id, SequenceLineage = lineage, IsActive = entity.Active, InspectionMode = ProviderInspectionModePolicy.ToCode(request.PrincipalInspectionMode), Version = 0 });
            changed = true;
        }
        if (!isPrincipal && entity.Principals.Count > 0)
            throw new ContactDirectoryException(ContactDirectoryError.InvalidPrincipalAssociation);
        foreach (var principal in entity.Principals.Where(item => item.SuccessorId is null))
        {
            if (principal.IsActive != entity.Active)
            {
                principal.IsActive = entity.Active;
                changed = true;
            }
        }

        var requestedPrincipalIds = request.PrincipalAssociations
            .Select(association => association.PrincipalId)
            .Distinct()
            .ToArray();
        var principals = await context.Principals
            .Where(item => requestedPrincipalIds.Contains(item.Id) && item.IsActive)
            .ToArrayAsync(cancellationToken);
        if (principals.Length != requestedPrincipalIds.Length || principals.Any(item => item.OrganizationId == entity.Id))
            throw new ContactDirectoryException(ContactDirectoryError.InvalidPrincipalAssociation);
        var existingLinks = await context.Set<ContactPrincipalLinkEntity>().Where(item => item.OrganizationId == entity.Id).ToArrayAsync(cancellationToken);
        var requestedLinks = request.PrincipalAssociations
            .Select(association => (association.PrincipalId, Role: ToCode(association.Role)))
            .ToHashSet();
        var currentLinks = existingLinks.Select(link => (link.PrincipalId, link.Role)).ToHashSet();
        changed |= !currentLinks.SetEquals(requestedLinks);
        context.RemoveRange(existingLinks);
        foreach (var association in request.PrincipalAssociations)
        {
            context.Add(new ContactPrincipalLinkEntity
            {
                PrincipalId = association.PrincipalId,
                OrganizationId = entity.Id,
                Role = ToCode(association.Role)
            });
        }
        entity.Version = changed ? checked(entity.Version + 1) : entity.Version;
        var result = ToRecord(entity, principalAssociations: request.PrincipalAssociations);
        var now = _timeProvider.GetUtcNow();
        EfOrganizationAdministration.AddReceipt(context, request.OperationKey, SaveKind, requestHash, result, now);
        EfOrganizationAdministration.AddHistory(context, "contact", entity.Id, creating ? "contact_created" : "contact_saved", request.Actor, request.OperationKey, now, null, before, result);
        if (!creating) EfEditScopeStore.Complete(context, EditScopeKind.Contact, entity.Id);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 }) { throw new ContactDirectoryException(ContactDirectoryError.DuplicateOrganizationName); }
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static async Task<IReadOnlyList<ContactDirectoryRecord>> LoadRecordsAsync(
        PegasusDbContext context,
        Guid[] orderedIds,
        IReadOnlyDictionary<Guid, LatestCaseRow>? latestCases,
        CancellationToken cancellationToken)
    {
        if (orderedIds.Length == 0) return [];
        var entities = await context.Organizations.AsNoTracking()
            .Where(item => orderedIds.Contains(item.Id))
            .Include(item => item.ContactRoles)
            .Include(item => item.PrincipalLinks)
            .Include(item => item.Principals)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var actualLatestCases = latestCases ?? await LatestCaseRows(context)
            .Where(item => orderedIds.Contains(item.OrganizationId))
            .ToDictionaryAsync(item => item.OrganizationId, cancellationToken);
        return orderedIds.Select(id => ToRecord(
                entities[id],
                actualLatestCases.GetValueOrDefault(id)))
            .ToArray();
    }

    private static IQueryable<LatestCaseRow> LatestCaseRows(PegasusDbContext context)
    {
        var principalCases =
            from @case in context.Cases.AsNoTracking()
            join principal in context.Principals.AsNoTracking() on @case.PrincipalId equals principal.Id
            join snapshot in context.CaseDataSnapshots.AsNoTracking() on @case.Id equals snapshot.CaseId into snapshots
            from snapshot in snapshots.DefaultIfEmpty()
            select new
            {
                OrganizationId = principal.OrganizationId,
                ReceivedAtUtc = snapshot.OriginReceivedAtUtc ?? @case.CreatedAtUtc,
                Reference = @case.Reference
            };

        var snapshotCases =
            from field in context.CaseDataFields.AsNoTracking()
            join snapshot in context.CaseDataSnapshots.AsNoTracking() on field.CaseId equals snapshot.CaseId
            join @case in context.Cases.AsNoTracking() on snapshot.CaseId equals @case.Id
            join sourceKind in context.CaseDataFields.AsNoTracking()
                on new { field.CaseId, FieldName = CaseDataFieldNames.InspectionLocationSourceKind }
                equals new { sourceKind.CaseId, sourceKind.FieldName } into sourceKinds
            from sourceKind in sourceKinds.DefaultIfEmpty()
            join organization in context.Organizations.AsNoTracking() on field.Value equals organization.Id.ToString()
            where field.FieldName == CaseDataFieldNames.ClaimSourceId
                || field.FieldName == CaseDataFieldNames.StorageBusinessId
                || (field.FieldName == CaseDataFieldNames.InspectionLocationSourceId
                    && sourceKind.Value == InspectionLocationSourceKind.Directory.ToString())
            select new
            {
                OrganizationId = organization.Id,
                ReceivedAtUtc = snapshot.OriginReceivedAtUtc ?? @case.CreatedAtUtc,
                Reference = @case.Reference
            };

        var candidates = principalCases.Concat(snapshotCases);
        var latestDates = candidates
            .GroupBy(item => item.OrganizationId)
            .Select(group => new
            {
                OrganizationId = group.Key,
                ReceivedAtUtc = group.Max(item => item.ReceivedAtUtc)
            });

        return
            from candidate in candidates
            join latestDate in latestDates
                on new { candidate.OrganizationId, candidate.ReceivedAtUtc }
                equals new { latestDate.OrganizationId, latestDate.ReceivedAtUtc }
            group candidate by new { candidate.OrganizationId, candidate.ReceivedAtUtc } into grouped
            select new LatestCaseRow
            {
                OrganizationId = grouped.Key.OrganizationId,
                ReceivedAtUtc = grouped.Key.ReceivedAtUtc,
                Reference = grouped.Min(item => item.Reference)!
            };
    }

    private static ContactDirectoryRecord ToRecord(
        OrganizationEntity entity,
        LatestCaseRow? latestCase = null,
        IReadOnlyList<ContactPrincipalAssociation>? principalAssociations = null)
    {
        var roles = entity.ContactRoles.Select(item => ParseRole(item.Role)).OrderBy(item => item).ToList();
        if (entity.Principals.Count > 0 && !roles.Contains(ContactRole.Principal)) roles.Add(ContactRole.Principal);
        var links = principalAssociations ?? entity.PrincipalLinks
            .Select(item => new ContactPrincipalAssociation(
                item.OrganizationId,
                ParseRole(item.Role),
                item.PrincipalId))
            .OrderBy(item => item.Role).ThenBy(item => item.PrincipalId)
            .ToArray();
        return new(
            entity.Id,
            entity.Name,
            entity.ContactPerson,
            entity.Email,
            entity.Telephone,
            entity.Address,
            entity.Postcode,
            entity.Active,
            roles.Order().ToArray(),
            entity.Version,
            latestCase?.ReceivedAtUtc,
            latestCase?.Reference,
            links,
            entity.Principals.Select(item => item.Id).Order().ToArray(),
            entity.GuidanceTemplate,
            entity.GuidanceTemplateVersion);
    }

    private sealed class LatestCaseRow
    {
        public Guid OrganizationId { get; init; }
        public DateTimeOffset ReceivedAtUtc { get; init; }
        public string Reference { get; init; } = string.Empty;
    }

    private sealed class ContactListRow
    {
        public Guid OrganizationId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? FirstRole { get; init; }
        public DateTimeOffset? ReceivedAtUtc { get; init; }
        public string? Reference { get; init; }
    }

    private static PrincipalAdministrationDetails ToPrincipalChoice(PrincipalEntity item) => new(item.Organization.Name, new(item.Id, item.OrganizationId, item.Code, item.SequenceLineageId, item.PredecessorId, item.SuccessorId, item.IsActive, item.Version, 0, ProviderInspectionModePolicy.Parse(item.InspectionMode), Enum.Parse<Pegasus.Core.Reports.PrincipalReportGenerationPolicy>(item.ReportGenerationPolicy), Pegasus.Core.Reports.PrincipalReportRecipientSettings.Normalize(item.IncludeOriginalInstructionSender, JsonSerializer.Deserialize<string[]>(item.ReportRecipientAddressesJson))));
    private static string ToCode(ContactRole role) => role switch { ContactRole.Principal => "principal", ContactRole.ClaimSource => "claim_source", ContactRole.Repairer => "repairer", ContactRole.Storage => "storage", ContactRole.ThirdPartyEngineer => "third_party_engineer", _ => throw new ArgumentOutOfRangeException(nameof(role)) };
    private static ContactRole ParseRole(string role) => role switch { "principal" => ContactRole.Principal, "claim_source" => ContactRole.ClaimSource, "repairer" => ContactRole.Repairer, "storage" => ContactRole.Storage, "third_party_engineer" => ContactRole.ThirdPartyEngineer, _ => throw new InvalidOperationException("The contact role is invalid.") };
    private static bool IsRetryable(Exception error) => error is ContactDirectoryException { Error: ContactDirectoryError.StaleVersion or ContactDirectoryError.DuplicateOrganizationName or ContactDirectoryError.DuplicatePrincipalCode } || error is SqlException { Number: 1205 or 2601 or 2627 } || error.InnerException is not null && IsRetryable(error.InnerException);
}
