using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Cases;

namespace Pegasus.ReferenceDataLoader;

/// <summary>One of the contact files and the one type of contact it holds.</summary>
/// <param name="Kind">The stored type code, as the files' <c>kind</c> and <c>organisation_kind</c> columns write it.</param>
internal sealed record ContactFile(string Name, ContactRole Role, string Kind, string[] Columns);

internal sealed record CsvTable(string File, string Sha256, IReadOnlyList<CsvRow> Rows);

internal sealed record ReferenceData(IReadOnlyDictionary<string, CsvTable> Tables);

internal sealed class CsvRow(IReadOnlyDictionary<string, string> cells)
{
    internal string this[string column] => cells[column];
}

/// <summary>
/// The reviewed register's load files, read from <c>artifacts/</c> (operator,
/// 15 October 2026: "reviewed CSVs under <c>artifacts/</c>"), and the columns
/// this loader reads. Every other column is evidence for the review and is
/// not read.
/// </summary>
internal static class ReferenceFiles
{
    internal const string Principals = "principals.csv";
    internal const string ClaimSources = "claim-sources.csv";
    internal const string Repairers = "repairers.csv";
    internal const string Storage = "storage.csv";
    internal const string Engineers = "engineers.csv";
    internal const string InspectionLocations = "inspection-locations.csv";
    internal const string Links = "links.csv";

    internal const string RowId = "row_id";
    internal const string Name = "name";
    internal const string ContactPerson = "contact_person";
    internal const string Email = "email";
    internal const string Telephone = "telephone";
    internal const string Address = "address";
    internal const string Postcode = "postcode";
    internal const string Active = "active";
    internal const string Action = "action";
    internal const string RestListed = "rest_listed";
    internal const string Review = "review";
    internal const string PrincipalCode = "principal_code";
    internal const string InspectionMode = "inspection_mode";
    internal const string NotesOnEveryCase = "notes_on_every_case";
    internal const string FullAddress = "full_address";
    internal const string PrincipalCodes = "eva_principal_codes";
    internal const string MatchedContactId = "matched_contact_id";
    internal const string MatchedKind = "matched_kind";
    internal const string OrganisationRowId = "organisation_row_id";
    internal const string OrganisationKind = "organisation_kind";
    internal const string LinkableAtLoad = "linkable_at_load";

    /// <summary>Only a row the operator approved is loaded; every other row is counted, never written.</summary>
    internal const string Approved = "approved";

    internal const string Yes = "true";
    internal const string LocationAdd = "add";

    /// <summary>A location the review matched to a repairer or storage contact: it writes nothing (R22).</summary>
    internal const string LocationMatched = "moved";

    /// <summary>The source an inspection location is keyed by, with its register row id.</summary>
    internal const string LocationSource = "register";
    internal const string LocationKind = "inspection_location";

    /// <summary>The actions that write a contact row; any other action is counted, never written.</summary>
    internal static readonly string[] LoadActions = ["update-live", "add", "add-inactive", "deactivate"];

    private static readonly Guid IdentityNamespace = new("c9cb12ec-9ac0-4ba2-b9f8-337a7809dd58");
    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];
    private static readonly string[] ContactColumns =
        [RowId, Name, ContactPerson, Email, Telephone, Address, Postcode, Active, Action, RestListed, Review];

    /// <summary>The contact files in load order: the Principals first, so every other type can link them.</summary>
    internal static readonly ContactFile[] ContactFiles =
    [
        new(Principals, ContactRole.Principal, "principal", [.. ContactColumns, PrincipalCode, InspectionMode, NotesOnEveryCase]),
        new(ClaimSources, ContactRole.ClaimSource, "claim_source", [.. ContactColumns, NotesOnEveryCase]),
        new(Repairers, ContactRole.Repairer, "repairer", ContactColumns),
        new(Storage, ContactRole.Storage, "storage", ContactColumns),
        new(Engineers, ContactRole.ThirdPartyEngineer, "third_party_engineer", ContactColumns)
    ];

    internal static readonly string[] LocationColumns =
        [RowId, Name, FullAddress, Postcode, Action, RestListed, Review, PrincipalCodes, MatchedContactId, MatchedKind];

    internal static readonly string[] LinkColumns =
        [RowId, OrganisationRowId, OrganisationKind, PrincipalCode, LinkableAtLoad, Review];

    internal static (string File, string[] Columns)[] AllFiles() =>
        [.. ContactFiles.Select(file => (File: file.Name, file.Columns)), (InspectionLocations, LocationColumns), (Links, LinkColumns)];

    internal static ContactRole RoleOf(string kind) =>
        ContactFiles.Single(file => file.Kind == kind).Role;

    /// <summary>
    /// A path the loader reads or writes: under an <c>artifacts</c> folder and
    /// never under <c>reference/</c> or <c>corpus/</c>, which no load changes.
    /// </summary>
    internal static string RequireArtifactsPath(string path, string option)
    {
        var full = Path.GetFullPath(path);
        var segments = full.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        if (!segments.Contains("artifacts", StringComparer.OrdinalIgnoreCase)
            || segments.Contains("reference", StringComparer.OrdinalIgnoreCase)
            || segments.Contains("corpus", StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"--{option} must be under artifacts/ and never under reference/ or corpus/.");
        }

        return full;
    }

    /// <summary>Every file and the columns this loader reads, checked before the database is read.</summary>
    internal static async Task<ReferenceData> ReadAsync(string folder, CancellationToken cancellationToken)
    {
        var tables = new Dictionary<string, CsvTable>(StringComparer.Ordinal);
        foreach (var (file, columns) in AllFiles())
        {
            var path = Path.Combine(folder, file);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"{file} is not in {folder}.");
            }

            tables[file] = Parse(file, await File.ReadAllBytesAsync(path, cancellationToken), columns);
        }

        return new(tables);
    }

    /// <summary>
    /// The organisation id of a row no stored contact matches: a name-based
    /// id (RFC 9562 version 8, SHA-256) from a fixed namespace, the type and
    /// the row id, so a later load finds the same row again.
    /// </summary>
    internal static Guid OrganizationId(string kind, string rowId)
    {
        byte[] input = [.. IdentityNamespace.ToByteArray(bigEndian: true), .. Encoding.UTF8.GetBytes($"{kind}:{rowId}")];
        var hash = SHA256.HashData(input);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash.AsSpan(0, 16), bigEndian: true);
    }

    /// <summary>The <c>;</c>-separated entries of a <c>rest_listed</c> cell, with the review's escapes undone.</summary>
    internal static IEnumerable<string> RestEntries(string cell) =>
        cell.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Replace("%3B", ";", StringComparison.Ordinal).Replace("%25", "%", StringComparison.Ordinal));

    private static CsvTable Parse(string file, byte[] bytes, string[] columns)
    {
        var text = Encoding.UTF8.GetString(bytes);
        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text[1..];
        }

        var records = Records(text);
        if (records.Count == 0)
        {
            throw new InvalidOperationException($"{file} has no header row.");
        }

        var header = records[0];
        var missing = columns.Where(column => !header.Contains(column)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"{file} has no {string.Join(", ", missing)} column.");
        }

        var rows = new List<CsvRow>();
        for (var index = 1; index < records.Count; index++)
        {
            var record = records[index];
            if (record.Count != header.Count)
            {
                throw new InvalidOperationException($"{file} record {index} has {record.Count} fields, not {header.Count}.");
            }

            var cells = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var column = 0; column < header.Count; column++)
            {
                cells[header[column]] = record[column];
            }

            rows.Add(new CsvRow(cells));
        }

        return new(file, Convert.ToHexStringLower(SHA256.HashData(bytes)), rows);
    }

    /// <summary>RFC 4180 records: quoted fields may hold commas, quotes and line breaks.</summary>
    private static List<List<string>> Records(string text)
    {
        var records = new List<List<string>>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var index = 0;
        while (index < text.Length)
        {
            var character = text[index];
            if (quoted)
            {
                if (character == '"' && index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    index += 2;
                    continue;
                }

                if (character == '"')
                {
                    quoted = false;
                }
                else
                {
                    field.Append(character);
                }

                index++;
                continue;
            }

            switch (character)
            {
                case '"' when field.Length == 0:
                    quoted = true;
                    index++;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    index++;
                    break;
                case '\r' or '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    AddRecord(records, fields);
                    fields = [];
                    index += character == '\r' && index + 1 < text.Length && text[index + 1] == '\n' ? 2 : 1;
                    break;
                default:
                    field.Append(character);
                    index++;
                    break;
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            AddRecord(records, fields);
        }

        return records;
    }

    private static void AddRecord(List<List<string>> records, List<string> fields)
    {
        // A blank line is no record.
        if (fields.Count > 1 || fields[0].Length > 0)
        {
            records.Add(fields);
        }
    }
}
