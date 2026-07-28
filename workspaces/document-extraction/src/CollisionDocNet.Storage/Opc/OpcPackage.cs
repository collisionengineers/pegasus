using System.Collections.Immutable;
using CollisionDocNet.Storage.Xml;
using CollisionDocNet.Storage.Zip;

namespace CollisionDocNet.Storage.Opc;

public sealed record OpcLimits
{
    public static OpcLimits Default { get; } = new();

    public BoundedZipLimits Zip { get; init; } = BoundedZipLimits.Default;

    public BoundedXmlLimits Xml { get; init; } = BoundedXmlLimits.Default;

    public int MaximumRelationships { get; init; } = 100_000;
}

public enum OpcReadError
{
    None = 0,
    ZipInvalid,
    ContentTypesMissing,
    ContentTypesInvalid,
    InvalidPartName,
    PartContentTypeMissing,
    RelationshipPartInvalid,
    RelationshipLimitExceeded,
    DuplicateRelationshipId,
    InvalidRelationshipTarget,
    MissingRelationshipTarget,
    Cancelled,
}

public sealed record OpcPart(
    string Name,
    string ContentType,
    ImmutableArray<byte> Content);

public sealed record OpcRelationship(
    string SourcePart,
    string Id,
    string Type,
    string Target,
    string? ResolvedPart,
    bool IsExternal);

public sealed record OpcPackage(
    BoundedZipArchive Archive,
    ImmutableArray<OpcPart> Parts,
    ImmutableArray<OpcRelationship> Relationships);

public readonly record struct OpcReadResult(
    OpcPackage? Package,
    OpcReadError Error,
    BoundedZipReadError ZipError,
    string? PartName)
{
    public bool IsSuccess => Error == OpcReadError.None && Package is not null;
}

/// <summary>
/// Builds the passive OPC content-type and relationship graph. External
/// relationships are recorded but never resolved. Digital signatures,
/// interleaving and package mutation are outside this read-only subset.
/// </summary>
public static class OpcPackageReader
{
    private const string ContentTypesNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";
    private const string RelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static OpcReadResult Read(
        ReadOnlyMemory<byte> bytes,
        OpcLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        limits ??= OpcLimits.Default;
        if (limits.MaximumRelationships <= 0)
        {
            return Failure(OpcReadError.RelationshipLimitExceeded);
        }

        BoundedZipReadResult zipResult = BoundedZipReader.Read(bytes, limits.Zip, cancellationToken);
        if (!zipResult.IsSuccess)
        {
            return new(null, OpcReadError.ZipInvalid, zipResult.Error, null);
        }

        try
        {
            return Build(zipResult.Archive!, limits, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Failure(OpcReadError.Cancelled);
        }
    }

    private static OpcReadResult Build(
        BoundedZipArchive archive,
        OpcLimits limits,
        CancellationToken cancellationToken)
    {
        BoundedZipEntry? contentTypesEntry = archive.Entries.FirstOrDefault(
            static entry => string.Equals(entry.Name, "[Content_Types].xml", StringComparison.Ordinal));
        if (contentTypesEntry is null)
        {
            return Failure(OpcReadError.ContentTypesMissing);
        }

        BoundedXmlReadResult contentTypesXml = BoundedXmlReader.Read(
            contentTypesEntry.Content.AsMemory(), limits.Xml, cancellationToken);
        if (!contentTypesXml.IsSuccess || !TryReadContentTypes(
            contentTypesXml.Document!, out Dictionary<string, string> defaults,
            out Dictionary<string, string> overrides))
        {
            return Failure(OpcReadError.ContentTypesInvalid, "[Content_Types].xml");
        }

        var parts = ImmutableArray.CreateBuilder<OpcPart>();
        var partNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (BoundedZipEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.Name.EndsWith('/') ||
                string.Equals(entry.Name, "[Content_Types].xml", StringComparison.Ordinal))
            {
                continue;
            }

            string partName = "/" + entry.Name;
            if (!IsCanonicalPartName(partName) || !partNames.Add(partName))
            {
                return Failure(OpcReadError.InvalidPartName, entry.Name);
            }

            if (IsRelationshipPart(entry.Name))
            {
                continue;
            }

            string extension = Path.GetExtension(entry.Name);
            string? contentType = overrides.GetValueOrDefault(partName);
            if (contentType is null && extension.Length > 1)
            {
                contentType = defaults.GetValueOrDefault(extension[1..]);
            }

            if (contentType is null)
            {
                return Failure(OpcReadError.PartContentTypeMissing, entry.Name);
            }

            parts.Add(new(partName, contentType, entry.Content));
        }

        var relationships = ImmutableArray.CreateBuilder<OpcRelationship>();
        foreach (BoundedZipEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsRelationshipPart(entry.Name))
            {
                continue;
            }

            if (!TryGetRelationshipSource(entry.Name, out string sourcePart))
            {
                return Failure(OpcReadError.RelationshipPartInvalid, entry.Name);
            }

            if (sourcePart != "/" && !partNames.Contains(sourcePart))
            {
                return Failure(OpcReadError.RelationshipPartInvalid, entry.Name);
            }

            BoundedXmlReadResult xml = BoundedXmlReader.Read(entry.Content.AsMemory(), limits.Xml, cancellationToken);
            if (!xml.IsSuccess)
            {
                return Failure(OpcReadError.RelationshipPartInvalid, entry.Name);
            }

            if (!HasValidRelationshipsRoot(xml.Document!))
            {
                return Failure(OpcReadError.RelationshipPartInvalid, entry.Name);
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (BoundedXmlNode node in xml.Document!.Nodes)
            {
                if (node.Kind != BoundedXmlNodeKind.ElementStart || node.Depth == 0)
                {
                    continue;
                }

                if (node.Depth != 1 || node.LocalName != "Relationship" ||
                    node.NamespaceUri != RelationshipsNamespace)
                {
                    return Failure(OpcReadError.RelationshipPartInvalid, entry.Name);
                }

                if (relationships.Count >= limits.MaximumRelationships)
                {
                    return Failure(OpcReadError.RelationshipLimitExceeded, entry.Name);
                }

                if (HasForeignAttribute(node, "Id", "Type", "Target", "TargetMode"))
                {
                    return Failure(OpcReadError.RelationshipPartInvalid, entry.Name);
                }

                string? id = UnqualifiedAttribute(node, "Id");
                string? type = UnqualifiedAttribute(node, "Type");
                string? target = UnqualifiedAttribute(node, "Target");
                string? mode = UnqualifiedAttribute(node, "TargetMode");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(type) ||
                    string.IsNullOrWhiteSpace(target))
                {
                    return Failure(OpcReadError.RelationshipPartInvalid, entry.Name);
                }

                if (!ids.Add(id))
                {
                    return Failure(OpcReadError.DuplicateRelationshipId, entry.Name);
                }

                if (mode is not null && mode is not ("Internal" or "External"))
                {
                    return Failure(OpcReadError.RelationshipPartInvalid, entry.Name);
                }

                bool external = mode == "External";
                string? resolved = null;
                if (!external)
                {
                    if (!TryResolvePart(sourcePart, target, out resolved))
                    {
                        return Failure(OpcReadError.InvalidRelationshipTarget, entry.Name);
                    }

                    if (!partNames.Contains(resolved))
                    {
                        return Failure(OpcReadError.MissingRelationshipTarget, entry.Name);
                    }
                }

                relationships.Add(new(sourcePart, id, type, target, resolved, external));
            }
        }

        return new(
            new(archive, parts.ToImmutable(), relationships.ToImmutable()),
            OpcReadError.None,
            BoundedZipReadError.None,
            null);
    }

    private static bool TryReadContentTypes(
        BoundedXmlDocument document,
        out Dictionary<string, string> defaults,
        out Dictionary<string, string> overrides)
    {
        defaults = new(StringComparer.OrdinalIgnoreCase);
        overrides = new(StringComparer.Ordinal);
        bool rootSeen = false;
        bool rootClosed = false;
        foreach (BoundedXmlNode node in document.Nodes)
        {
            if (node.Kind == BoundedXmlNodeKind.ElementEnd && node.Depth == 0)
            {
                rootClosed = true;
                continue;
            }

            if (node.Kind != BoundedXmlNodeKind.ElementStart)
            {
                continue;
            }

            if (!rootSeen)
            {
                rootSeen = node.Depth == 0 && node.LocalName == "Types" &&
                    node.NamespaceUri == ContentTypesNamespace;
                if (!rootSeen)
                {
                    return false;
                }

                continue;
            }

            if (rootClosed || node.Depth != 1 || node.NamespaceUri != ContentTypesNamespace)
            {
                return false;
            }

            if (HasForeignAttribute(node, "ContentType", "Extension", "PartName"))
            {
                return false;
            }

            string? contentType = UnqualifiedAttribute(node, "ContentType");
            if (string.IsNullOrWhiteSpace(contentType))
            {
                return false;
            }

            if (node.LocalName == "Default")
            {
                string? extension = UnqualifiedAttribute(node, "Extension");
                if (string.IsNullOrWhiteSpace(extension) || extension.Contains('.', StringComparison.Ordinal) ||
                    !defaults.TryAdd(extension, contentType))
                {
                    return false;
                }
            }
            else if (node.LocalName == "Override")
            {
                string? partName = UnqualifiedAttribute(node, "PartName");
                if (partName is null || !IsCanonicalPartName(partName) ||
                    !overrides.TryAdd(partName, contentType))
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        return rootSeen && rootClosed;
    }

    private static bool HasValidRelationshipsRoot(BoundedXmlDocument document)
    {
        bool rootSeen = false;
        bool rootClosed = false;
        foreach (BoundedXmlNode node in document.Nodes)
        {
            if (node.Kind == BoundedXmlNodeKind.ElementEnd && node.Depth == 0)
            {
                rootClosed = rootSeen;
                continue;
            }

            if (node.Kind != BoundedXmlNodeKind.ElementStart)
            {
                continue;
            }

            if (!rootSeen)
            {
                rootSeen = node.Depth == 0 && node.LocalName == "Relationships" &&
                    node.NamespaceUri == RelationshipsNamespace;
                if (!rootSeen)
                {
                    return false;
                }
            }
            else if (rootClosed)
            {
                return false;
            }
        }

        return rootSeen && rootClosed;
    }

    private static string? UnqualifiedAttribute(BoundedXmlNode node, string localName) =>
        node.Attributes.FirstOrDefault(attribute =>
            attribute.LocalName == localName && attribute.NamespaceUri.Length == 0)?.Value;

    private static bool HasForeignAttribute(BoundedXmlNode node, params ReadOnlySpan<string> names)
    {
        foreach (BoundedXmlAttributeValue attribute in node.Attributes)
        {
            foreach (string name in names)
            {
                if (attribute.LocalName == name && attribute.NamespaceUri.Length != 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsRelationshipPart(string name) =>
        name.EndsWith(".rels", StringComparison.Ordinal) &&
        (name.StartsWith("_rels/", StringComparison.Ordinal) ||
         name.Contains("/_rels/", StringComparison.Ordinal));

    private static bool TryGetRelationshipSource(string relationshipPart, out string source)
    {
        if (relationshipPart == "_rels/.rels")
        {
            source = "/";
            return true;
        }

        int marker = relationshipPart.LastIndexOf("/_rels/", StringComparison.Ordinal);
        if (marker <= 0)
        {
            source = string.Empty;
            return false;
        }

        string directory = relationshipPart[..marker];
        string filename = relationshipPart[(marker + 7)..];
        if (!filename.EndsWith(".rels", StringComparison.Ordinal) || filename.Length == 5)
        {
            source = string.Empty;
            return false;
        }

        source = "/" + directory + "/" + filename[..^5];
        return IsCanonicalPartName(source);
    }

    private static bool TryResolvePart(string sourcePart, string target, out string resolved)
    {
        resolved = string.Empty;
        if (target.Length == 0 || target.Contains('\\', StringComparison.Ordinal) ||
            Uri.TryCreate(target, UriKind.Absolute, out _))
        {
            return false;
        }

        int suffix = target.IndexOfAny(['?', '#']);
        string path = suffix >= 0 ? target[..suffix] : target;
        try
        {
            path = Uri.UnescapeDataString(path);
        }
        catch (UriFormatException)
        {
            return false;
        }

        string baseDirectory = sourcePart == "/"
            ? "/"
            : sourcePart[..(sourcePart.LastIndexOf('/') + 1)];
        string combined = path.StartsWith('/')
            ? path
            : baseDirectory + path;
        var stack = new List<string>();
        foreach (string segment in combined.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (stack.Count == 0)
                {
                    return false;
                }

                stack.RemoveAt(stack.Count - 1);
                continue;
            }

            if (segment.Contains(':', StringComparison.Ordinal) || segment.Contains('\0'))
            {
                return false;
            }

            stack.Add(segment);
        }

        resolved = "/" + string.Join('/', stack);
        return IsCanonicalPartName(resolved);
    }

    private static bool IsCanonicalPartName(string name)
    {
        if (name.Length <= 1 || name[0] != '/' || name.EndsWith('/') ||
            name.Contains("//", StringComparison.Ordinal) || name.Contains('\\') ||
            name.Contains('?') || name.Contains('#'))
        {
            return false;
        }

        for (int index = 0; index < name.Length; index++)
        {
            char character = name[index];
            if (char.IsControl(character))
            {
                return false;
            }

            if (character != '%')
            {
                continue;
            }

            if (index > name.Length - 3 || !Uri.IsHexDigit(name[index + 1]) ||
                !Uri.IsHexDigit(name[index + 2]))
            {
                return false;
            }

            int decoded = HexValue(name[index + 1]) * 16 + HexValue(name[index + 2]);
            if (decoded is '/' or '\\' or 0 || IsUnreserved((char)decoded))
            {
                return false;
            }

            index += 2;
        }

        foreach (Range range in name.AsSpan().Split('/'))
        {
            ReadOnlySpan<char> segment = name.AsSpan(range);
            if (segment is "." or ".." || segment.EndsWith('.'))
            {
                return false;
            }
        }

        return true;
    }

    private static int HexValue(char value) =>
        value is >= '0' and <= '9' ? value - '0' :
        value is >= 'A' and <= 'F' ? value - 'A' + 10 : value - 'a' + 10;

    private static bool IsUnreserved(char value) =>
        value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '.' or '_' or '~';

    private static OpcReadResult Failure(OpcReadError error, string? partName = null) =>
        new(null, error, BoundedZipReadError.None, partName);
}
