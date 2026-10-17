using System.Globalization;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.ReferenceDataLoader;

internal sealed record LoadSettings(string Target, ActionActor Actor, string Batch, DateTimeOffset ReviewedAtUtc);

/// <summary>The ports a load reads and writes through: no entity, no context, no SQL.</summary>
internal sealed record LoadPorts(
    IContactDirectoryQueries Contacts,
    IGetPrincipal Principals,
    IContactDirectoryAdministration ContactWriter,
    IInspectionLocationAdministration LocationWriter);

/// <param name="PrincipalIdsByCode">Every stored Principal's id by its code, for the links an apply resolves.</param>
internal sealed record PlannedLoad(LoadPlan Plan, IReadOnlyDictionary<string, Guid> PrincipalIdsByCode);

/// <summary>
/// Computes what a load writes from the stored state and the files. It reads
/// through the query ports only and writes nothing; <see cref="LoadRunner"/>
/// writes exactly what it plans. The loader matches a stored Principal by
/// code and a stored Claim Source by name and never merges any other
/// contact (FRD-04).
/// </summary>
internal sealed class LoadPlanner(ReferenceData data, LoadSettings settings, LoadPorts ports)
{
    private readonly List<RowReject> _rejects = [];
    private readonly List<HygieneDrop> _hygiene = [];
    private readonly List<RestEntry> _rest = [];
    private readonly List<Overwrite> _overwrites = [];
    private readonly List<ContactWrite> _contactWrites = [];
    private readonly List<LocationWrite> _locationWrites = [];
    private readonly List<MatchedLocation> _matched = [];
    private readonly Dictionary<(string File, string Action, string Review), int> _skipped = new();
    private readonly Dictionary<string, Tally> _tallies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Guid> _principalIdsByCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, string> _codesByPrincipalId = new();
    private readonly Dictionary<string, Guid> _organizationIdsByCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _knownCodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _codesInLoad = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _namesInLoad = new(StringComparer.Ordinal);
    private readonly HashSet<string> _loadedContacts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<CsvRow>> _linkRows = new(StringComparer.Ordinal);
    private readonly HashSet<string> _linkedContacts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ContactDirectoryRecord> _storedClaimSources = new(StringComparer.OrdinalIgnoreCase);
    private List<ContactDirectoryRecord> _storedPrincipals = [];
    private int _linked;
    private int _linksSkipped;
    private int _linksRejected;
    private int _locationLinks;
    private int _locationsSkipped;
    private int _locationsRejected;
    private int _locationLinksRejected;

    internal async Task<PlannedLoad> PlanAsync(CancellationToken cancellationToken)
    {
        // Only Principals and Claim Sources are listed, every page, for matching.
        _storedPrincipals = await ListAllAsync(ContactRole.Principal, cancellationToken);
        foreach (var claimSource in await ListAllAsync(ContactRole.ClaimSource, cancellationToken))
        {
            _storedClaimSources.TryAdd(claimSource.Name, claimSource);
        }

        foreach (var organization in _storedPrincipals)
        {
            foreach (var principalId in organization.OwnPrincipalIds ?? [])
            {
                var principal = await ports.Principals.ExecuteAsync(settings.Actor, principalId, cancellationToken)
                    ?? throw new InvalidOperationException($"Principal {principalId} could not be read.");
                var code = principal.Principal.Code;
                _principalIdsByCode[code] = principalId;
                _codesByPrincipalId[principalId] = code;
                _organizationIdsByCode[code] = organization.OrganizationId;
                _knownCodes.Add(code);
            }
        }

        string[] storedCodes = [.. _knownCodes.Order(StringComparer.Ordinal)];
        GroupLinks();
        foreach (var file in ReferenceFiles.ContactFiles)
        {
            foreach (var row in data.Tables[file.Name].Rows)
            {
                await PlanContactAsync(file, row, cancellationToken);
            }
        }

        foreach (var (key, rows) in _linkRows)
        {
            if (!_linkedContacts.Contains(key))
            {
                Skip(ReferenceFiles.Links, "organisation not loaded", ReferenceFiles.Approved, rows.Count);
                _linksSkipped += rows.Count;
            }
        }

        foreach (var row in data.Tables[ReferenceFiles.InspectionLocations].Rows)
        {
            PlanLocation(row);
        }

        var plan = new LoadPlan(
            settings.Target,
            settings.Actor.SubjectId,
            settings.Batch,
            settings.ReviewedAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            [.. data.Tables.Values
                .OrderBy(table => table.File, StringComparer.Ordinal)
                .Select(table => new InputFile(table.File, table.Sha256, table.Rows.Count))],
            new Baseline(_storedPrincipals.Count, _storedClaimSources.Count, storedCodes),
            [.. ReferenceFiles.ContactFiles.Select(file => TallyOf(file.Kind).Count(file.Kind))],
            [.. _skipped
                .OrderBy(pair => pair.Key.File, StringComparer.Ordinal)
                .ThenBy(pair => pair.Key.Action, StringComparer.Ordinal)
                .ThenBy(pair => pair.Key.Review, StringComparer.Ordinal)
                .Select(pair => new SkippedRows(pair.Key.File, pair.Key.Action, pair.Key.Review, pair.Value))],
            _rejects,
            _hygiene,
            _rest,
            _overwrites,
            new LinkCounts(_linked, _linksSkipped, _linksRejected),
            new LocationCounts(
                _locationWrites.Count, _locationLinks, _matched.Count, _locationsSkipped, _locationsRejected, _locationLinksRejected),
            _matched,
            _contactWrites,
            _locationWrites);
        return new(plan, _principalIdsByCode);
    }

    private async Task<List<ContactDirectoryRecord>> ListAllAsync(ContactRole role, CancellationToken cancellationToken)
    {
        var all = new List<ContactDirectoryRecord>();
        for (var page = 1; ; page++)
        {
            var listed = await ports.Contacts.ListAsync(new(settings.Actor, role, null, page), cancellationToken);
            all.AddRange(listed.Items);
            if (!listed.HasNextPage)
            {
                return all;
            }
        }
    }

    /// <summary>The approved links that may be written, by the contact row they belong to.</summary>
    private void GroupLinks()
    {
        foreach (var row in data.Tables[ReferenceFiles.Links].Rows)
        {
            var linkable = row[ReferenceFiles.LinkableAtLoad];
            if (row[ReferenceFiles.Review] != ReferenceFiles.Approved || linkable != ReferenceFiles.Yes)
            {
                Skip(ReferenceFiles.Links, $"{ReferenceFiles.LinkableAtLoad}={linkable}", row[ReferenceFiles.Review]);
                _linksSkipped++;
                continue;
            }

            var key = Key(row[ReferenceFiles.OrganisationKind], row[ReferenceFiles.OrganisationRowId]);
            if (!_linkRows.TryGetValue(key, out var rows))
            {
                rows = [];
                _linkRows[key] = rows;
            }

            rows.Add(row);
        }
    }

    private async Task PlanContactAsync(ContactFile file, CsvRow row, CancellationToken cancellationToken)
    {
        var rowId = row[ReferenceFiles.RowId];
        var action = row[ReferenceFiles.Action];
        var review = row[ReferenceFiles.Review];
        if (review != ReferenceFiles.Approved || !ReferenceFiles.LoadActions.Contains(action, StringComparer.Ordinal))
        {
            Skip(file.Name, action, review);
            return;
        }

        foreach (var entry in ReferenceFiles.RestEntries(row[ReferenceFiles.RestListed]))
        {
            _rest.Add(new(file.Name, rowId, entry));
        }

        try
        {
            await PlanContactRowAsync(file, row, rowId, cancellationToken);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException)
        {
            _rejects.Add(new(file.Name, rowId, exception.Message));
            TallyOf(file.Kind).Reject++;
        }
    }

    private async Task PlanContactRowAsync(ContactFile file, CsvRow row, string rowId, CancellationToken cancellationToken)
    {
        var isPrincipal = file.Role == ContactRole.Principal;
        string? code = null;
        if (isPrincipal)
        {
            code = OrganizationAdministrationPolicy.NormalizePrincipalCode(row[ReferenceFiles.PrincipalCode]);
            if (!_codesInLoad.Add(code))
            {
                throw new ArgumentException($"principal_code {code} is on an earlier row.");
            }
        }

        var name = row[ReferenceFiles.Name].Trim();
        if (name.Length > 0 && !NamesInLoad(file.Kind).Add(name))
        {
            throw new ArgumentException($"name {name} is on an earlier {file.Kind} row.");
        }

        var organizationId = OrganizationIdOf(file, rowId, code, name);
        var current = await ports.Contacts.GetAsync(settings.Actor, organizationId, cancellationToken);
        var expectedVersion = current?.Version ?? 0;
        var operationKey = string.Create(
            CultureInfo.InvariantCulture, $"ref04:{settings.Batch}:{file.Kind}:{rowId}:v{expectedVersion}");

        // A blank cell keeps the stored value (R15 "enriched"); a value
        // Normalize refuses is left blank and reported (R23).
        var active = Flag(row[ReferenceFiles.Active]) ?? current?.Active
            ?? throw new ArgumentException("active is blank.");
        var mode = isPrincipal
            ? Mode(row[ReferenceFiles.InspectionMode]) ?? current?.PrincipalInspectionMode
                ?? throw new ArgumentException("inspection_mode is blank.")
            : CaseInspectionMode.PhysicalAddress;
        var notes = file.Columns.Contains(ReferenceFiles.NotesOnEveryCase)
            ? Clean(file, row, rowId, ReferenceFiles.NotesOnEveryCase)
            : null;
        var normalized = ContactDirectoryPolicy.Normalize(new SaveContactRequest(
            settings.Actor,
            organizationId,
            expectedVersion,
            name.Length > 0 ? name : current?.Name ?? string.Empty,
            Clean(file, row, rowId, ReferenceFiles.ContactPerson) ?? current?.ContactPerson,
            Clean(file, row, rowId, ReferenceFiles.Email) ?? current?.Email,
            Clean(file, row, rowId, ReferenceFiles.Telephone) ?? current?.Telephone,
            Clean(file, row, rowId, ReferenceFiles.Address) ?? current?.Address,
            Clean(file, row, rowId, ReferenceFiles.Postcode) ?? current?.Postcode,
            active,
            file.Role,
            code,
            mode,
            [],
            operationKey,
            notes ?? current?.NotesOnEveryCase));

        if ((current is null || !string.Equals(current.Name, normalized.Name, StringComparison.OrdinalIgnoreCase))
            && await NameHeldByAnotherAsync(file.Role, normalized.Name, organizationId, cancellationToken))
        {
            throw new ArgumentException($"name {normalized.Name} is held by another stored {file.Kind}.");
        }

        string[] links = isPrincipal ? [] : LinksOf(file, rowId, current);
        var after = new ContactFields(
            normalized.Name,
            normalized.ContactPerson,
            normalized.Email,
            normalized.Telephone,
            normalized.Address,
            normalized.Postcode,
            normalized.Active,
            normalized.NotesOnEveryCase,
            normalized.PrincipalCode,
            isPrincipal ? PrincipalInspectionModePolicy.ToCode(normalized.PrincipalInspectionMode) : null);
        _loadedContacts.Add(Key(file.Kind, rowId));
        if (code is not null)
        {
            _knownCodes.Add(code);
        }

        var tally = TallyOf(file.Kind);
        var step = isPrincipal ? "a" : "b";
        if (current is null)
        {
            tally.Create++;
            _contactWrites.Add(new(step, file.Name, rowId, file.Kind, organizationId, operationKey, 0, true, after, links));
            return;
        }

        string[] storedLinks = isPrincipal ? [] : [.. (current.LinkedPrincipalIds ?? []).Select(CodeOf).Order(StringComparer.Ordinal)];
        var linksChanged = !storedLinks.SequenceEqual(links, StringComparer.Ordinal);
        var changes = Changes(FieldsOf(current, isPrincipal), after);
        if (changes.Count == 0 && !linksChanged)
        {
            tally.Unchanged++;
            return;
        }

        foreach (var (field, before, now) in changes)
        {
            if (!string.IsNullOrEmpty(before))
            {
                _overwrites.Add(new(file.Name, rowId, field, before, now));
            }
        }

        if (linksChanged && storedLinks.Length > 0)
        {
            _overwrites.Add(new(file.Name, rowId, "links", string.Join(';', storedLinks), string.Join(';', links)));
        }

        tally.Update++;
        _contactWrites.Add(new(step, file.Name, rowId, file.Kind, organizationId, operationKey, expectedVersion, false, after, links));
    }

    /// <summary>
    /// A stored Principal keeps its id when its code matches, a stored Claim
    /// Source when its name matches; any other row has its name-based id.
    /// </summary>
    private Guid OrganizationIdOf(ContactFile file, string rowId, string? code, string name)
    {
        if (code is not null && _organizationIdsByCode.TryGetValue(code, out var principalOrganizationId))
        {
            return principalOrganizationId;
        }

        if (file.Role == ContactRole.ClaimSource && _storedClaimSources.TryGetValue(name, out var claimSource))
        {
            return claimSource.OrganizationId;
        }

        return ReferenceFiles.OrganizationId(file.Kind, rowId);
    }

    private async Task<bool> NameHeldByAnotherAsync(
        ContactRole role,
        string name,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        IEnumerable<ContactDirectoryRecord> holders = role switch
        {
            ContactRole.Principal => _storedPrincipals,
            ContactRole.ClaimSource => _storedClaimSources.Values,
            _ => await ports.Contacts.FindPossibleMatchesAsync(settings.Actor, name, cancellationToken)
        };
        return holders.Any(holder => holder.Role == role
            && holder.OrganizationId != organizationId
            && string.Equals(holder.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The contact's whole link set: its approved links to a stored or loaded
    /// Principal, inactive ones included (operator, 16 October 2026), or the
    /// stored set when the files give it none. A link to any other code is
    /// rejected.
    /// </summary>
    private string[] LinksOf(ContactFile file, string rowId, ContactDirectoryRecord? current)
    {
        var key = Key(file.Kind, rowId);
        _linkedContacts.Add(key);
        var planned = new SortedSet<string>(StringComparer.Ordinal);
        if (_linkRows.TryGetValue(key, out var rows))
        {
            foreach (var row in rows)
            {
                var code = row[ReferenceFiles.PrincipalCode].Trim();
                if (_knownCodes.TryGetValue(code, out var known))
                {
                    planned.Add(known);
                    _linked++;
                }
                else
                {
                    _rejects.Add(new(ReferenceFiles.Links, row[ReferenceFiles.RowId], $"principal_code {code} is neither stored nor loaded."));
                    _linksRejected++;
                }
            }
        }

        return planned.Count > 0
            ? [.. planned]
            : [.. (current?.LinkedPrincipalIds ?? []).Select(CodeOf).Order(StringComparer.Ordinal)];
    }

    private void PlanLocation(CsvRow row)
    {
        const string file = ReferenceFiles.InspectionLocations;
        var rowId = row[ReferenceFiles.RowId];
        var action = row[ReferenceFiles.Action];
        var review = row[ReferenceFiles.Review];
        if (review == ReferenceFiles.Approved && action == ReferenceFiles.LocationMatched)
        {
            var kind = row[ReferenceFiles.MatchedKind];
            var contact = row[ReferenceFiles.MatchedContactId];
            _matched.Add(new(rowId, contact, kind, _loadedContacts.Contains(Key(kind, contact))));
            return;
        }

        if (review != ReferenceFiles.Approved || action != ReferenceFiles.LocationAdd)
        {
            Skip(file, action, review);
            _locationsSkipped++;
            return;
        }

        foreach (var entry in ReferenceFiles.RestEntries(row[ReferenceFiles.RestListed]))
        {
            _rest.Add(new(file, rowId, entry));
        }

        var operationKey = $"ref04:{settings.Batch}:{ReferenceFiles.LocationKind}:{rowId}";
        SaveInspectionLocationRequest normalized;
        try
        {
            normalized = InspectionLocationPolicy.Normalize(new(
                settings.Actor,
                row[ReferenceFiles.Name],
                row[ReferenceFiles.FullAddress],
                row[ReferenceFiles.Postcode],
                Active: true,
                ReferenceFiles.LocationSource,
                rowId,
                settings.ReviewedAtUtc,
                [],
                operationKey));
        }
        catch (ArgumentException exception)
        {
            _rejects.Add(new(file, rowId, exception.Message));
            _locationsRejected++;
            return;
        }

        var principals = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var code in row[ReferenceFiles.PrincipalCodes].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (_knownCodes.TryGetValue(code, out var known))
            {
                principals.Add(known);
            }
            else
            {
                _rejects.Add(new(file, rowId, $"principal code {code} is neither stored nor loaded; that link is not written."));
                _locationLinksRejected++;
            }
        }

        _locationLinks += principals.Count;
        _locationWrites.Add(new(rowId, operationKey, normalized.Name, normalized.Address, normalized.Postcode, [.. principals]));
    }

    /// <summary>
    /// The cell, or null when it is blank or <see cref="ContactDirectoryPolicy.Normalize"/>
    /// refuses it; a refusal is reported with the row, column, value and
    /// reason, and no rule is added (R23).
    /// </summary>
    private string? Clean(ContactFile file, CsvRow row, string rowId, string column)
    {
        var value = row[column];
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var probe = new SaveContactRequest(
            settings.Actor,
            Guid.NewGuid(),
            0,
            "probe",
            null,
            null,
            null,
            null,
            null,
            true,
            file.Role,
            file.Role == ContactRole.Principal ? "PROBE" : null,
            CaseInspectionMode.PhysicalAddress,
            [],
            "probe");
        probe = column switch
        {
            ReferenceFiles.ContactPerson => probe with { ContactPerson = value },
            ReferenceFiles.Email => probe with { Email = value },
            ReferenceFiles.Telephone => probe with { Telephone = value },
            ReferenceFiles.Address => probe with { Address = value },
            ReferenceFiles.Postcode => probe with { Postcode = value },
            ReferenceFiles.NotesOnEveryCase => probe with { NotesOnEveryCase = value },
            _ => throw new InvalidOperationException($"{column} is not a contact field.")
        };
        try
        {
            _ = ContactDirectoryPolicy.Normalize(probe);
            return value;
        }
        catch (ArgumentException exception)
        {
            _hygiene.Add(new(file.Name, rowId, column, value, exception.Message));
            return null;
        }
    }

    private string CodeOf(Guid principalId) =>
        _codesByPrincipalId.TryGetValue(principalId, out var code)
            ? code
            : throw new InvalidOperationException($"A stored link names Principal {principalId}, which no stored Principal is.");

    private static bool? Flag(string value) => value.Trim() switch
    {
        "" => null,
        ReferenceFiles.Yes => true,
        "false" => false,
        var other => throw new ArgumentException($"active {other} is not true or false.")
    };

    private static CaseInspectionMode? Mode(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : PrincipalInspectionModePolicy.Parse(value.Trim());

    private static ContactFields FieldsOf(ContactDirectoryRecord current, bool isPrincipal) => new(
        current.Name,
        current.ContactPerson,
        current.Email,
        current.Telephone,
        current.Address,
        current.Postcode,
        current.Active,
        current.NotesOnEveryCase,
        current.PrincipalCode,
        isPrincipal && current.PrincipalInspectionMode is { } mode ? PrincipalInspectionModePolicy.ToCode(mode) : null);

    private static List<(string Field, string? Before, string? After)> Changes(ContactFields before, ContactFields after)
    {
        (string Field, string? Before, string? After)[] pairs =
        [
            (ReferenceFiles.Name, before.Name, after.Name),
            (ReferenceFiles.ContactPerson, before.ContactPerson, after.ContactPerson),
            (ReferenceFiles.Email, before.Email, after.Email),
            (ReferenceFiles.Telephone, before.Telephone, after.Telephone),
            (ReferenceFiles.Address, before.Address, after.Address),
            (ReferenceFiles.Postcode, before.Postcode, after.Postcode),
            (ReferenceFiles.NotesOnEveryCase, before.NotesOnEveryCase, after.NotesOnEveryCase),
            (ReferenceFiles.Active, Text(before.Active), Text(after.Active)),
            (ReferenceFiles.InspectionMode, before.InspectionMode, after.InspectionMode)
        ];
        return [.. pairs.Where(pair => pair.Before != pair.After)];
    }

    private static string Text(bool value) => value ? ReferenceFiles.Yes : "false";

    private static string Key(string kind, string rowId) => $"{kind}|{rowId}";

    private HashSet<string> NamesInLoad(string kind)
    {
        if (!_namesInLoad.TryGetValue(kind, out var names))
        {
            names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _namesInLoad[kind] = names;
        }

        return names;
    }

    private Tally TallyOf(string kind)
    {
        if (!_tallies.TryGetValue(kind, out var tally))
        {
            tally = new Tally();
            _tallies[kind] = tally;
        }

        return tally;
    }

    private void Skip(string file, string action, string review, int rows = 1)
    {
        var key = (file, action, review);
        _skipped[key] = _skipped.GetValueOrDefault(key) + rows;
    }

    private sealed class Tally
    {
        public int Create { get; set; }
        public int Update { get; set; }
        public int Unchanged { get; set; }
        public int Reject { get; set; }

        public KindCount Count(string kind) => new(kind, Create, Update, Unchanged, Reject);
    }
}
