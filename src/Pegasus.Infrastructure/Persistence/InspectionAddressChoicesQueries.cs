using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// EXT-18/S05 item 3: also implements <see cref="IInspectionLocationChoices"/>
/// — the bounded local address-suggestion search — beside the existing
/// address-choice query. It unions the case's own current claimant/repairer/
/// storage addresses, the principal's prior accepted locations (the same
/// cross-case history <see cref="GetAsync"/> already reads) and the active
/// <see cref="IOrganizationDirectoryQueries"/> directory locations; no
/// external address provider or fuzzy/geographic inference is part of it.
/// </summary>
public sealed class InspectionAddressChoicesQueries(
    IDbContextFactory<PegasusDbContext> contextFactory,
    IOrganizationDirectoryQueries directory)
    : IInspectionAddressChoicesQueries, IInspectionLocationChoices
{
    private readonly IOrganizationDirectoryQueries _directory =
        directory ?? throw new ArgumentNullException(nameof(directory));


    public async Task<InspectionAddressChoicesData?> GetAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var current = await EfCaseDataStore.SnapshotQuery(context, tracking: false)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        if (current is null)
        {
            return null;
        }

        var workflow = await context.CaseWorkflows.AsNoTracking()
            .SingleAsync(item => item.CaseId == caseId, cancellationToken);
        var projection = EfCaseDataStore.Map(current, workflow);
        var principalId = current.Case.PrincipalId;

        var candidates = await context.CaseDataFields.AsNoTracking()
            .Where(field => field.FieldName == CaseDataFieldNames.InspectionAddress
                && field.ValueKind == CaseDataCodes.Confirmed
                && field.CaseId != caseId
                && field.Snapshot.Case.PrincipalId == principalId
                && field.Value != Ext18InspectionAddressPolicy.ImageBasedAssessment)
            .Select(field => new
            {
                field.Value,
                ConfirmedAtUtc = field.ConfirmedAtUtc!.Value
            })
            .ToListAsync(cancellationToken);

        var previousAddresses = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Value))
            .GroupBy(candidate => candidate.Value.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(candidate => candidate.ConfirmedAtUtc).First())
            .OrderByDescending(candidate => candidate.ConfirmedAtUtc)
            .Select(candidate => candidate.Value.Trim())
            .ToArray();

        return new(
            projection.Claimant.Address.Current?.Value,
            projection.Inspection.RepairerAddress?.Current?.Value,
            projection.Inspection.StorageLocation?.Current?.Value,
            previousAddresses);
    }

    public async Task<IReadOnlyList<InspectionLocationChoice>> SearchAsync(
        InspectionLocationChoicesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.Actor);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(query));
        }

        var namePrefix = InspectionLocationMatchPolicy.NormalizeNamePrefix(query.Prefix ?? string.Empty);
        var postcodePrefix = InspectionLocationMatchPolicy.NormalizePostcodePrefix(query.Prefix ?? string.Empty);
        var nameQualifies = InspectionLocationMatchPolicy.MeetsMinimumLength(namePrefix);
        var postcodeQualifies = InspectionLocationMatchPolicy.MeetsMinimumLength(postcodePrefix);
        if (!nameQualifies && !postcodeQualifies)
        {
            return [];
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var current = await EfCaseDataStore.SnapshotQuery(context, tracking: false)
            .SingleOrDefaultAsync(item => item.CaseId == query.CaseId, cancellationToken);
        if (current is null)
        {
            return [];
        }

        var workflow = await context.CaseWorkflows.AsNoTracking()
            .SingleAsync(item => item.CaseId == query.CaseId, cancellationToken);
        var projection = EfCaseDataStore.Map(current, workflow);
        var principalId = current.Case.PrincipalId;

        var candidates = new List<InspectionLocationChoice>();
        AddIfMatches(
            candidates,
            projection.Claimant.Address.Current?.Value,
            "Claimant",
            InspectionLocationSourceKind.Claimant,
            query.CaseId,
            projection.Version,
            namePrefix);
        AddIfMatches(
            candidates,
            projection.Inspection.RepairerAddress?.Current?.Value,
            "Repairer",
            InspectionLocationSourceKind.Repairer,
            query.CaseId,
            projection.Version,
            namePrefix);
        AddIfMatches(
            candidates,
            projection.Inspection.StorageLocation?.Current?.Value,
            "Storage",
            InspectionLocationSourceKind.Storage,
            query.CaseId,
            projection.Version,
            namePrefix);

        if (nameQualifies)
        {
            // A coarse, case-insensitive SQL-level prefix predicate (the
            // database's default collation, not the exact NormalizeNamePrefix
            // rule) plus a deterministic order, applied before the bounded
            // fetch — otherwise a principal with more than 500 prior cases
            // gets an arbitrary, unordered 500 and a real prefix match past
            // that cutoff is silently dropped before it is ever compared.
            // AddIfMatches below still applies the exact normalized
            // comparison as the real filter. C06 review R-17: this predicate
            // must not be narrower than that real filter, so it compares
            // against the same collapsed-whitespace, uppercased `namePrefix`
            // NormalizeNamePrefix already produced, and collapses runs of
            // whitespace in the stored value the same way (bounded passes —
            // still a pre-filter, not the exact rule) so irregular spacing in
            // stored data does not drop a real match before AddIfMatches ever
            // sees it.
            var priorRows = await context.CaseDataFields.AsNoTracking()
                .Where(field => field.FieldName == CaseDataFieldNames.InspectionAddress
                    && field.ValueKind == CaseDataCodes.Confirmed
                    && field.CaseId != query.CaseId
                    && field.Snapshot.Case.PrincipalId == principalId
                    && field.Value != Ext18InspectionAddressPolicy.ImageBasedAssessment
                    && field.Value
                        .Replace("\t", " ").Replace("\r", " ").Replace("\n", " ")
                        .Replace("  ", " ").Replace("  ", " ").Replace("  ", " ").Replace("  ", " ")
                        .Trim()
                        .StartsWith(namePrefix))
                .OrderByDescending(field => field.ConfirmedAtUtc)
                .Select(field => new
                {
                    field.CaseId,
                    field.Value,
                    ConfirmedAtUtc = field.ConfirmedAtUtc!.Value
                })
                .Take(500)
                .ToListAsync(cancellationToken);

            var priorLocations = priorRows
                .Where(row => !string.IsNullOrWhiteSpace(row.Value))
                .GroupBy(row => row.Value.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(row => row.ConfirmedAtUtc).First());
            foreach (var row in priorLocations)
            {
                AddIfMatches(
                    candidates,
                    row.Value,
                    "Previous",
                    InspectionLocationSourceKind.PriorPrincipalLocation,
                    row.CaseId,
                    row.ConfirmedAtUtc.UtcTicks,
                    namePrefix,
                    idSeed: row.CaseId);
            }
        }

        var directoryQuery = new OrganizationDirectoryQuery(query.Actor, query.Prefix ?? string.Empty, Role: null);
        var directoryEntries = await _directory.SearchAsync(directoryQuery, cancellationToken);
        candidates.AddRange(directoryEntries.Select(entry => new InspectionLocationChoice(
            entry.Id,
            entry.Name,
            InspectionAddressEvidenceKind.PhysicalAddress,
            entry.Address,
            entry.Postcode,
            entry.Role.ToString(),
            InspectionLocationSourceKind.Directory,
            entry.SourceRecordId,
            entry.SourceVersion)));

        return candidates
            .DistinctBy(choice => choice.Id)
            .OrderByDescending(choice => IsExactMatch(choice, namePrefix, postcodePrefix))
            .ThenBy(choice => InspectionLocationMatchPolicy.NormalizeNamePrefix(choice.Label))
            .ThenBy(choice => choice.Postcode is null
                ? null
                : InspectionLocationMatchPolicy.NormalizePostcodePrefix(choice.Postcode))
            .ThenBy(choice => choice.Id)
            .Take(InspectionLocationMatchPolicy.MaximumResultLimit)
            .ToArray();
    }

    private static bool IsExactMatch(
        InspectionLocationChoice choice,
        string namePrefix,
        string postcodePrefix) =>
        InspectionLocationMatchPolicy.IsExactMatch(
            InspectionLocationMatchPolicy.NormalizeNamePrefix(choice.Label),
            choice.Postcode is null
                ? null
                : InspectionLocationMatchPolicy.NormalizePostcodePrefix(choice.Postcode),
            namePrefix,
            postcodePrefix);

    private static void AddIfMatches(
        List<InspectionLocationChoice> candidates,
        string? address,
        string role,
        InspectionLocationSourceKind sourceKind,
        Guid sourceRecordId,
        long sourceVersion,
        string namePrefix,
        Guid? idSeed = null)
    {
        if (string.IsNullOrWhiteSpace(address)
            || address == Ext18InspectionAddressPolicy.ImageBasedAssessment)
        {
            return;
        }

        // namePrefix's normalization (collapse interior whitespace) can
        // never be shorter than postcodePrefix's (remove all whitespace), so
        // every caller that reaches this point already has a qualifying
        // namePrefix — SearchAsync's minimum-length gate guarantees it. The
        // prefix test below is therefore always active, never bypassed.
        if (!InspectionLocationMatchPolicy.NormalizeNamePrefix(address).StartsWith(
                namePrefix, StringComparison.Ordinal))
        {
            return;
        }

        candidates.Add(new InspectionLocationChoice(
            DeterministicId(idSeed ?? sourceRecordId, role),
            address,
            InspectionAddressEvidenceKind.PhysicalAddress,
            address,
            null,
            role,
            sourceKind,
            sourceRecordId,
            sourceVersion));
    }

    /// <summary>
    /// A stable location id for a case-derived candidate that has no natural
    /// row id of its own: deterministic per (seed, discriminator) pair, so
    /// the same underlying value always dedupes to the same id across
    /// repeated searches.
    /// </summary>
    private static Guid DeterministicId(Guid seed, string discriminator)
    {
        var material = new byte[16 + Encoding.UTF8.GetByteCount(discriminator)];
        seed.TryWriteBytes(material);
        Encoding.UTF8.GetBytes(discriminator, material.AsSpan(16));
        var hash = SHA256.HashData(material);
        return new Guid(hash.AsSpan(0, 16));
    }
}
