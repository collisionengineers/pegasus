using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using Pegasus.Infrastructure.Intake.DocumentExtraction.Cfb;

namespace Pegasus.Infrastructure.Intake.DocumentExtraction.Msg;

internal static class MsgReader
{
    private const string Specification = "MS-OXMSG 18.0; MS-OXPROPS 30.0; MS-OXRTFCP 14.0 (2025-05-20)";

    public static MsgDocument Read(
        ReadOnlyMemory<byte> bytes,
        MsgReadLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        limits ??= MsgReadLimits.Default;
        CompoundFileReadResult compound = CompoundFileReader.Read(bytes, cancellationToken: cancellationToken);
        if (!compound.IsSuccess)
        {
            MsgReadOutcome outcome = compound.Error == CompoundFileReadError.Cancelled
                ? MsgReadOutcome.Cancelled
                : MsgReadOutcome.Corrupt;
            return Empty(outcome, new("MSG_CFB_INVALID", $"Compound storage rejected input with {compound.Error}."));
        }

        return Read(compound.File!, limits, cancellationToken);
    }

    public static MsgDocument Read(
        CompoundFile file,
        MsgReadLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        limits ??= MsgReadLimits.Default;
        ValidateLimits(limits);
        var issues = new List<MsgIssue>();
        var state = new MsgReadState(limits);
        try
        {
            var properties = new MapiPropertyReader(file, limits, issues, state, cancellationToken);
            properties.ReadNamedProperties(0);
            MsgDocument result = ReadStorage(0, PropertyStreamContext.RootMessage, 0, properties, limits, issues, cancellationToken);
            return result with { Issues = SortIssues(issues) };
        }
        catch (OperationCanceledException)
        {
            return Empty(MsgReadOutcome.Cancelled, new("MSG_CANCELLED", "MSG parsing was cancelled."));
        }
        catch (OverflowException)
        {
            return Empty(MsgReadOutcome.ResourceLimitExceeded, new("MSG_CHECKED_ARITHMETIC", "MSG parsing exceeded a checked numeric bound."));
        }
    }

    private static MsgDocument ReadStorage(
        uint storageId,
        PropertyStreamContext context,
        int depth,
        MapiPropertyReader reader,
        MsgReadLimits limits,
        List<MsgIssue> issues,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int issueStart = issues.Count;
        if (depth > limits.MaximumNestingDepth)
        {
            issues.Add(new("MSG_RESOURCE_LIMIT", "The cumulative nesting-depth limit was exceeded.", storageId));
            return Empty(MsgReadOutcome.ResourceLimitExceeded, issues[^1]);
        }

        ImmutableArray<MapiProperty> propertyBag = reader.ReadPropertyBag(storageId, context);
        int? codePage = GetInt32(propertyBag, 0x3FFD);

        string messageClass = GetString(propertyBag, 0x001A) ?? "<missing>";
        MsgSemanticProjection projection = Project(messageClass, propertyBag, issues, storageId);
        ImmutableArray<MsgRecipient> recipients = ReadRecipients(storageId, codePage, reader, issues, cancellationToken);
        MsgBodySet bodies = ReadBodies(propertyBag, limits, issues, cancellationToken);
        ImmutableArray<MsgAttachment> attachments = ReadAttachments(
            storageId, depth, codePage, reader, limits, issues, cancellationToken);

        if (IsProtected(messageClass))
        {
            issues.Add(new("MSG_PROTECTED_CONTENT", "Protected or opaque S/MIME content was classified without decryption.", storageId));
        }
        ImmutableArray<MsgIssue> localIssues = SortIssues(issues.Skip(issueStart));
        MsgReadOutcome outcome = DetermineOutcome(messageClass, projection.Kind, localIssues, reader.State.ResourceLimitExceeded);
        return new(outcome, projection, propertyBag, recipients, bodies, attachments, localIssues);
    }

    private static ImmutableArray<MsgRecipient> ReadRecipients(
        uint storageId,
        int? codePage,
        MapiPropertyReader reader,
        List<MsgIssue> issues,
        CancellationToken cancellationToken)
    {
        CompoundFileDirectoryEntry[] storages = CollectStorages(
            reader.Children(storageId),
            "__recip_version1.0_#",
            cancellationToken);
        var result = ImmutableArray.CreateBuilder<MsgRecipient>(storages.Length);
        var suffixes = new HashSet<uint>();
        for (int index = 0; index < storages.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CompoundFileDirectoryEntry storage = storages[index];
            if (!reader.State.TakeChildStorage() || !reader.State.TakeRecipient())
            {
                issues.Add(new("MSG_RESOURCE_LIMIT", "The cumulative recipient, model-item or child-storage limit was exceeded.", storage.StreamId));
                break;
            }
            uint sourceOrder = ParseStorageSuffix(storage.Name);
            if (!suffixes.Add(sourceOrder))
                issues.Add(new("MSG_RECIPIENT_STORAGE_DUPLICATE", "Recipient storages have duplicate source-order suffixes.", storage.StreamId));
            ImmutableArray<MapiProperty> bag = reader.ReadPropertyBag(storage.StreamId, PropertyStreamContext.RecipientOrAttachment, codePage);
            int roleValue = GetInt32(bag, 0x0C15) ?? 0;
            string role = roleValue switch { 1 => "To", 2 => "Cc", 3 => "Bcc", _ => "Unknown" };
            if (role == "Unknown") issues.Add(new("MSG_RECIPIENT_ROLE_UNKNOWN", "Recipient type is missing or unsupported.", storage.StreamId, 0x0C15));
            result.Add(new(
                storage.StreamId,
                sourceOrder,
                role,
                GetString(bag, 0x3001),
                GetString(bag, 0x39FE) ?? GetString(bag, 0x3003),
                bag));
        }
        return result.ToImmutable();
    }

    private static ImmutableArray<MsgAttachment> ReadAttachments(
        uint storageId,
        int depth,
        int? codePage,
        MapiPropertyReader reader,
        MsgReadLimits limits,
        List<MsgIssue> issues,
        CancellationToken cancellationToken)
    {
        CompoundFileDirectoryEntry[] storages = CollectStorages(
            reader.Children(storageId),
            "__attach_version1.0_#",
            cancellationToken);
        var result = ImmutableArray.CreateBuilder<MsgAttachment>(storages.Length);
        var suffixes = new HashSet<uint>();
        foreach (CompoundFileDirectoryEntry storage in storages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!reader.State.TakeChildStorage() || !reader.State.TakeAttachment())
            {
                issues.Add(new("MSG_RESOURCE_LIMIT", "The cumulative attachment, model-item or child-storage limit was exceeded.", storage.StreamId));
                break;
            }
            uint sourceOrder = ParseStorageSuffix(storage.Name);
            if (!suffixes.Add(sourceOrder))
                issues.Add(new("MSG_ATTACHMENT_STORAGE_DUPLICATE", "Attachment storages have duplicate source-order suffixes.", storage.StreamId));
            ImmutableArray<MapiProperty> bag = reader.ReadPropertyBag(storage.StreamId, PropertyStreamContext.RecipientOrAttachment, codePage);
            int method = GetInt32(bag, 0x3705) ?? 0;
            byte[] content = GetBinaryBytes(bag, 0x3701);
            string? passiveReference = GetString(bag, 0x370D) ?? GetString(bag, 0x3708);
            MsgDocument? embedded = null;
            CompoundFileDirectoryEntry? embeddedStorage = reader.Storage(storage.StreamId, "__substg1.0_3701000d");
            if (method == 5 && embeddedStorage is not null)
            {
                embedded = ReadStorage(embeddedStorage.StreamId, PropertyStreamContext.EmbeddedMessage, depth + 1, reader, limits, issues, cancellationToken);
            }
            else if (method == 5)
            {
                issues.Add(new("MSG_EMBEDDED_MESSAGE_MISSING", "Embedded-message attachment lacks its message storage.", storage.StreamId, 0x3701));
            }

            if (method is 2 or 4 or 7 || passiveReference is not null)
            {
                issues.Add(new("MSG_ATTACHMENT_REFERENCE_PASSIVE", "External, path, URL or reference-only attachment was recorded without retrieval.", storage.StreamId, 0x3705));
            }
            if (method is 6)
            {
                issues.Add(new("MSG_ATTACHMENT_OLE_PASSIVE", "OLE attachment remains passive and was not activated.", storage.StreamId, 0x3705));
            }
            if (method is not (1 or 2 or 4 or 5 or 6 or 7))
            {
                issues.Add(new("MSG_ATTACHMENT_METHOD_UNSUPPORTED", $"Attachment method {method} is retained but not semantically supported.", storage.StreamId, 0x3705));
            }

            int renderingPosition = GetInt32(bag, 0x370B) ?? -1;
            bool hidden = GetBoolean(bag, 0x7FFE) ?? false;
            string? contentId = GetString(bag, 0x3712);
            ImmutableArray<MsgPassiveStorage> passiveStorages = ReadPassiveStorages(
                storage,
                embedded is null ? null : embeddedStorage,
                reader,
                issues,
                cancellationToken);
            result.Add(new(
                storage.StreamId,
                sourceOrder,
                method,
                GetString(bag, 0x3707) ?? GetString(bag, 0x3704),
                GetString(bag, 0x3001),
                GetString(bag, 0x370E),
                contentId,
                GetString(bag, 0x3713),
                hidden || renderingPosition >= 0 || contentId is not null,
                ImmutableArray.Create(content),
                passiveReference,
                embedded,
                passiveStorages,
                bag));
        }
        return result.ToImmutable();
    }

    private static ImmutableArray<MsgPassiveStorage> ReadPassiveStorages(
        CompoundFileDirectoryEntry attachmentStorage,
        CompoundFileDirectoryEntry? embeddedMessageStorage,
        MapiPropertyReader reader,
        List<MsgIssue> issues,
        CancellationToken cancellationToken)
    {
        var result = ImmutableArray.CreateBuilder<MsgPassiveStorage>();
        foreach (CompoundFileDirectoryEntry child in reader.Children(attachmentStorage.StreamId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (child.ObjectType != CompoundFileObjectType.Storage ||
                child.StreamId == embeddedMessageStorage?.StreamId)
            {
                continue;
            }

            if (!reader.State.TakeChildStorage() || !reader.State.TakeModelItem())
            {
                issues.Add(new("MSG_RESOURCE_LIMIT", "The cumulative passive-storage or model-item limit was exceeded.", child.StreamId));
                break;
            }

            result.Add(new(child.StreamId, child.Name, reader.Children(child.StreamId).Length));
            issues.Add(new("MSG_ATTACHMENT_CHILD_STORAGE_PASSIVE", "An attachment child storage was retained as passive structural evidence.", child.StreamId));
        }
        return result.ToImmutable();
    }

    private static MsgBodySet ReadBodies(
        ImmutableArray<MapiProperty> properties,
        MsgReadLimits limits,
        List<MsgIssue> issues,
        CancellationToken cancellationToken)
    {
        string? plain = GetString(properties, 0x1000);
        byte[] htmlBytes = GetBinaryBytes(properties, 0x1013);
        string? html = htmlBytes.Length == 0 ? GetString(properties, 0x1013) : DecodeHtml(htmlBytes, properties, issues);
        byte[] compressedRtf = GetBinaryBytes(properties, 0x1009);
        byte[] rtf = [];
        string? rtfText = null;
        if (compressedRtf.Length != 0)
        {
            if (RtfCompression.TryDecompress(compressedRtf, limits.MaximumRtfBytes, out rtf, out string? error, cancellationToken))
            {
                rtfText = PassiveRtfText.Extract(rtf, issues, cancellationToken: cancellationToken);
            }
            else
            {
                issues.Add(new("MSG_RTF_INVALID", error ?? "Compressed RTF is invalid."));
            }
        }

        string? canonical;
        string source;
        if (!string.IsNullOrEmpty(plain)) { canonical = plain; source = "plain"; }
        else if (!string.IsNullOrEmpty(html)) { canonical = HtmlToText(html, cancellationToken); source = "html-inert-text"; }
        else if (!string.IsNullOrEmpty(rtfText)) { canonical = rtfText; source = "rtf-passive-text"; }
        else { canonical = null; source = "none"; }

        if (plain is not null && html is not null && !string.Equals(Normalize(plain), Normalize(HtmlToText(html, cancellationToken)), StringComparison.Ordinal))
        {
            issues.Add(new("MSG_BODY_VARIANTS_DIVERGE", "Plain and HTML body representations differ; both were retained."));
        }
        return new(plain, html, ImmutableArray.Create(htmlBytes), rtfText, ImmutableArray.Create(rtf), canonical, source);
    }

    private static string DecodeHtml(byte[] bytes, ImmutableArray<MapiProperty> properties, List<MsgIssue> issues)
    {
        int codePage = GetInt32(properties, 0x3FDE) ?? GetInt32(properties, 0x3FFD) ?? 1252;
        if (codePage == 65001) return System.Text.Encoding.UTF8.GetString(bytes).TrimEnd('\0');
        if (codePage == 1200) return System.Text.Encoding.Unicode.GetString(bytes).TrimEnd('\0');
        if (codePage == 20127) return System.Text.Encoding.ASCII.GetString(bytes).TrimEnd('\0');
        if (codePage != 1252) issues.Add(new("MSG_HTML_CODEPAGE_FALLBACK", $"HTML code page {codePage} is unsupported; deterministic Latin-1 fallback was used."));
        return System.Text.Encoding.Latin1.GetString(bytes).TrimEnd('\0');
    }

    private static string HtmlToText(string html, CancellationToken cancellationToken)
    {
        var text = new System.Text.StringBuilder(html.Length);
        bool inTag = false;
        for (int index = 0; index < html.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            char current = html[index];
            if (current == '<') { inTag = true; continue; }
            if (current == '>') { inTag = false; text.Append(' '); continue; }
            if (!inTag) text.Append(current);
        }
        return WebUtility.HtmlDecode(text.ToString()).Trim();
    }

    private static MsgSemanticProjection Project(
        string messageClass,
        ImmutableArray<MapiProperty> properties,
        List<MsgIssue> issues,
        uint storageId)
    {
        MsgItemKind kind = Classify(messageClass);
        if (kind == MsgItemKind.Generic)
        {
            issues.Add(new("MSG_CLASS_GENERIC", "The item class is preserved through the generic property bag without a class-specific claim.", storageId, 0x001A));
        }
        else if (kind != MsgItemKind.Mail)
        {
            issues.Add(new("MSG_ITEM_CLASS_PARTIAL", "This item class is passively projected but does not have complete class-specific semantic coverage.", storageId, 0x001A));
        }

        var fields = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        Add(fields, "subject", GetString(properties, 0x0037));
        Add(fields, "senderName", GetString(properties, 0x0C1A));
        Add(fields, "senderAddress", GetString(properties, 0x5D01) ?? GetString(properties, 0x0C1F));
        Add(fields, "representingName", GetString(properties, 0x0042));
        Add(fields, "transportHeaders", GetString(properties, 0x007D));
        Add(fields, "conversationTopic", GetString(properties, 0x0070));
        Add(fields, "location", GetString(properties, 0x8208));
        Add(fields, "company", GetString(properties, 0x3A16));
        Add(fields, "displayName", GetString(properties, 0x3001));
        Add(fields, "taskOwner", GetString(properties, 0x811F));
        Add(fields, "noteColour", GetInt32(properties, 0x8B00)?.ToString(CultureInfo.InvariantCulture));
        foreach (ushort id in new ushort[] { 0x0039, 0x0E06, 0x3007, 0x3008, 0x820D, 0x820E, 0x8104, 0x8105 })
        {
            DateTimeOffset? time = GetDate(properties, id);
            Add(fields, $"date-{id:x4}", time?.ToString("O", CultureInfo.InvariantCulture));
        }
        return new(kind, messageClass, fields.ToImmutable());
    }

    private static MsgItemKind Classify(string value)
    {
        if (value.StartsWith("REPORT.", StringComparison.OrdinalIgnoreCase) || value.Contains(".Report", StringComparison.OrdinalIgnoreCase)) return MsgItemKind.Report;
        if (value.Contains("Meeting", StringComparison.OrdinalIgnoreCase)) return MsgItemKind.Meeting;
        if (value.Contains("Appointment", StringComparison.OrdinalIgnoreCase)) return MsgItemKind.Calendar;
        if (value.Contains("DistList", StringComparison.OrdinalIgnoreCase)) return MsgItemKind.DistributionList;
        if (value.Contains("Contact", StringComparison.OrdinalIgnoreCase)) return MsgItemKind.Contact;
        if (value.Contains("Task", StringComparison.OrdinalIgnoreCase)) return MsgItemKind.Task;
        if (value.Contains("StickyNote", StringComparison.OrdinalIgnoreCase)) return MsgItemKind.Note;
        if (value.Contains("Journal", StringComparison.OrdinalIgnoreCase)) return MsgItemKind.Journal;
        if (value.StartsWith("IPM.Note", StringComparison.OrdinalIgnoreCase) || value.Contains(".Post", StringComparison.OrdinalIgnoreCase)) return MsgItemKind.Mail;
        return MsgItemKind.Generic;
    }

    private static MsgReadOutcome DetermineOutcome(
        string messageClass,
        MsgItemKind kind,
        ImmutableArray<MsgIssue> issues,
        bool resourceLimitExceeded)
    {
        if (resourceLimitExceeded || issues.Any(static issue => issue.Code == "MSG_RESOURCE_LIMIT"))
            return MsgReadOutcome.ResourceLimitExceeded;
        if (IsProtected(messageClass))
        {
            return MsgReadOutcome.Encrypted;
        }
        return issues.IsEmpty && kind == MsgItemKind.Mail ? MsgReadOutcome.Complete : MsgReadOutcome.Partial;
    }

    private static bool IsProtected(string messageClass) =>
        messageClass.Contains("rpmsg", StringComparison.OrdinalIgnoreCase) ||
        messageClass.Contains("SMIME", StringComparison.OrdinalIgnoreCase) &&
        !messageClass.Contains("MultipartSigned", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value) => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static void Add(ImmutableDictionary<string, string>.Builder fields, string name, string? value) { if (!string.IsNullOrEmpty(value)) fields[name] = value; }
    private static uint ParseStorageSuffix(string name)
    {
        int marker = name.LastIndexOf('#');
        return marker >= 0 && uint.TryParse(name.AsSpan(marker + 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value)
            ? value
            : uint.MaxValue;
    }

    private static CompoundFileDirectoryEntry[] CollectStorages(
        CompoundFileDirectoryEntry[] children,
        string prefix,
        CancellationToken cancellationToken)
    {
        var storages = new List<CompoundFileDirectoryEntry>();
        storages.EnsureCapacity(children.Length);
        foreach (CompoundFileDirectoryEntry entry in children)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.ObjectType == CompoundFileObjectType.Storage &&
                entry.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                storages.Add(entry);
            }
        }
        storages.Sort(static (left, right) =>
        {
            int suffix = ParseStorageSuffix(left.Name).CompareTo(ParseStorageSuffix(right.Name));
            return suffix != 0 ? suffix : left.StreamId.CompareTo(right.StreamId);
        });
        return [.. storages];
    }
    private static MapiProperty? Find(ImmutableArray<MapiProperty> properties, ushort id) => properties.FirstOrDefault(property => property.PropertyId == id);
    private static MapiValue? First(ImmutableArray<MapiProperty> properties, ushort id) => Find(properties, id)?.Values.FirstOrDefault();
    private static string? GetString(ImmutableArray<MapiProperty> properties, ushort id) => First(properties, id)?.Decoded as string;
    private static int? GetInt32(ImmutableArray<MapiProperty> properties, ushort id) => First(properties, id)?.Decoded is int value ? value : null;
    private static bool? GetBoolean(ImmutableArray<MapiProperty> properties, ushort id) => First(properties, id)?.Decoded is bool value ? value : null;
    private static DateTimeOffset? GetDate(ImmutableArray<MapiProperty> properties, ushort id) => First(properties, id)?.Decoded is DateTimeOffset value ? value : null;
    private static byte[] GetBinaryBytes(ImmutableArray<MapiProperty> properties, ushort id)
    {
        MapiValue? value = First(properties, id);
        return value?.Kind is MapiValueKind.Binary or MapiValueKind.OpaqueObject
            ? value.RawBytes.ToArray()
            : [];
    }
    private static ImmutableArray<MsgIssue> SortIssues(IEnumerable<MsgIssue> issues) => [.. issues.OrderBy(static issue => issue.StorageId).ThenBy(static issue => issue.PropertyId).ThenBy(static issue => issue.Code, StringComparer.Ordinal).ThenBy(static issue => issue.Message, StringComparer.Ordinal)];

    private static void ValidateLimits(MsgReadLimits limits)
    {
        if (limits.MaximumProperties <= 0 || limits.MaximumRecipients < 0 || limits.MaximumAttachments < 0 || limits.MaximumNestingDepth < 0 || limits.MaximumDecodedBytes <= 0 || limits.MaximumRtfBytes < 0 || limits.MaximumValues <= 0 || limits.MaximumModelItems <= 0 || limits.MaximumChildStorages <= 0)
            throw new ArgumentOutOfRangeException(nameof(limits));
    }

    private static MsgDocument Empty(MsgReadOutcome outcome, MsgIssue issue) => new(
        outcome,
        new(MsgItemKind.Generic, "<unavailable>", ImmutableDictionary<string, string>.Empty),
        [], [], new(null, null, [], null, [], null, "none"), [], [issue]);

    public static string SpecificationIdentity => Specification;
}
