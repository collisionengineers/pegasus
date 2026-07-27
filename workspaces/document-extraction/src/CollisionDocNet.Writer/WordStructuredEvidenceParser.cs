using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CollisionDocNet.Storage.CompoundFile;

namespace CollisionDocNet.Writer;

internal sealed record WordStructuredEvidence(
    ImmutableArray<WordPropertyRun> PropertyRuns,
    ImmutableArray<WordStructureRecord> Structures,
    ImmutableArray<WordPassiveAsset> Assets,
    ImmutableArray<WordMetadataProperty> Metadata,
    ImmutableArray<WordBinaryIssue> Issues);

internal static class WordStructuredEvidenceParser
{
    private const int CharacterBinTableIndex = 12;
    private const int ParagraphBinTableIndex = 13;
    private const int MaximumRetainedRecordBytes = 4096;

    private static readonly FrozenDictionary<int, (WordStructureKind Kind, int PlcDataSize)> KnownRanges = new Dictionary<int, (WordStructureKind Kind, int PlcDataSize)>
    {
        [1] = (WordStructureKind.StyleSheet, -1),
        [2] = (WordStructureKind.Footnote, 2),
        [4] = (WordStructureKind.Comment, 2),
        [6] = (WordStructureKind.Section, 12),
        [11] = (WordStructureKind.HeaderFooter, 0),
        [15] = (WordStructureKind.FontTable, -1),
        [16] = (WordStructureKind.Field, 2),
        [17] = (WordStructureKind.Field, 2),
        [18] = (WordStructureKind.Field, 2),
        [19] = (WordStructureKind.Field, 2),
        [20] = (WordStructureKind.Field, 2),
        [21] = (WordStructureKind.Bookmark, 4),
        [22] = (WordStructureKind.Bookmark, 0),
        [28] = (WordStructureKind.Settings, -1),
        [29] = (WordStructureKind.ExternalReference, -1),
        [46] = (WordStructureKind.Endnote, 2),
        [48] = (WordStructureKind.Field, 2),
        [49] = (WordStructureKind.Field, 2),
        [54] = (WordStructureKind.Drawing, -1),
        [55] = (WordStructureKind.Drawing, -1),
        [56] = (WordStructureKind.Textbox, -1),
        [57] = (WordStructureKind.Textbox, -1),
        [73] = (WordStructureKind.ListDefinition, -1),
        [74] = (WordStructureKind.ListDefinition, -1),
        [75] = (WordStructureKind.ListDefinition, -1),
        [85] = (WordStructureKind.Revision, -1),
        [89] = (WordStructureKind.Form, -1),
        [93] = (WordStructureKind.Signature, -1),
        [98] = (WordStructureKind.CustomData, -1),
    }.ToFrozenDictionary();

    internal static WordStructuredEvidence Extract(
        CompoundFile compoundFile,
        ReadOnlySpan<byte> wordDocument,
        ReadOnlySpan<byte> table,
        string tableName,
        WordFib fib,
        ImmutableArray<WordPiece> pieces,
        WordBinaryExtractionLimits limits,
        CancellationToken cancellationToken)
    {
        var runs = ImmutableArray.CreateBuilder<WordPropertyRun>();
        var structures = ImmutableArray.CreateBuilder<WordStructureRecord>();
        var assets = ImmutableArray.CreateBuilder<WordPassiveAsset>();
        var metadata = ImmutableArray.CreateBuilder<WordMetadataProperty>();
        var issues = ImmutableArray.CreateBuilder<WordBinaryIssue>();

        ParsePieceProperties(table, fib, pieces, limits, runs, issues, cancellationToken);
        ParseFkpBinTable(table, wordDocument, tableName, fib, pieces, CharacterBinTableIndex,
            WordPropertyRunKind.Character, 1, limits, runs, issues, cancellationToken);
        ParseFkpBinTable(table, wordDocument, tableName, fib, pieces, ParagraphBinTableIndex,
            WordPropertyRunKind.Paragraph, 13, limits, runs, issues, cancellationToken);
        ParseStructureRanges(table, tableName, fib, limits, structures, issues, cancellationToken);
        InventoryStreams(compoundFile, fib, limits, assets, metadata, issues, cancellationToken);

        return new(runs.ToImmutable(), structures.ToImmutable(), assets.ToImmutable(), metadata.ToImmutable(), issues.ToImmutable());
    }

    private static void ParsePieceProperties(
        ReadOnlySpan<byte> table,
        WordFib fib,
        ImmutableArray<WordPiece> pieces,
        WordBinaryExtractionLimits limits,
        ImmutableArray<WordPropertyRun>.Builder runs,
        ImmutableArray<WordBinaryIssue>.Builder issues,
        CancellationToken cancellationToken)
    {
        List<ReadOnlyMemory<byte>> propertyRecords = ReadClxPropertyRecords(table, fib, issues);
        foreach (WordPiece piece in pieces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (piece.PropertyModifier == 0)
            {
                continue;
            }

            ImmutableArray<WordSprm> sprms;
            string origin;
            if ((piece.PropertyModifier & 1) == 0)
            {
                byte isprm = (byte)((piece.PropertyModifier >> 1) & 0x7f);
                byte value = (byte)(piece.PropertyModifier >> 8);
                sprms = [WordSprmParser.FromSimplePrm(isprm, value)];
                origin = "Pcd.Prm0";
            }
            else
            {
                int index = piece.PropertyModifier >> 1;
                if ((uint)index >= (uint)propertyRecords.Count)
                {
                    issues.Add(new("doc-prm-reference-invalid", "A complex piece PRM references an unavailable CLX property record.", fib.ClxOffset));
                    continue;
                }

                sprms = WordSprmParser.Parse(propertyRecords[index].Span, limits.MaximumSprmsPerRun, out WordBinaryIssue? issue);
                if (issue is not null)
                {
                    issues.Add(issue);
                }
                origin = $"CLX.Prc[{index.ToString(CultureInfo.InvariantCulture)}]";
            }

            AddRun(runs, limits, new(
                StableId("property", origin, piece.FileOffset, piece.ByteLength, SerializeSprms(sprms)),
                WordPropertyRunKind.Piece, piece.CpStart, piece.CpEnd, piece.FileOffset,
                checked(piece.FileOffset + piece.ByteLength), origin, checked((int)fib.ClxOffset), null, sprms), issues);
        }
    }

    private static List<ReadOnlyMemory<byte>> ReadClxPropertyRecords(
        ReadOnlySpan<byte> table,
        WordFib fib,
        ImmutableArray<WordBinaryIssue>.Builder issues)
    {
        var records = new List<ReadOnlyMemory<byte>>();
        if (!WordPieceTableParser.RangeFits(fib.ClxOffset, fib.ClxLength, table.Length))
        {
            return records;
        }

        int cursor = checked((int)fib.ClxOffset);
        int end = checked(cursor + (int)fib.ClxLength);
        while (cursor < end && table[cursor++] == 0x01)
        {
            if (cursor > end - 2)
            {
                break;
            }
            ushort length = U16(table, cursor);
            cursor += 2;
            if (cursor > end - length)
            {
                issues.Add(new("doc-clx-prc-range", "A CLX property record exceeds the declared CLX range.", cursor - 3));
                break;
            }
            records.Add(table.Slice(cursor, length).ToArray());
            cursor += length;
        }
        return records;
    }

    private static void ParseFkpBinTable(
        ReadOnlySpan<byte> table,
        ReadOnlySpan<byte> wordDocument,
        string tableName,
        WordFib fib,
        ImmutableArray<WordPiece> pieces,
        int rangeIndex,
        WordPropertyRunKind kind,
        int descriptorSize,
        WordBinaryExtractionLimits limits,
        ImmutableArray<WordPropertyRun>.Builder runs,
        ImmutableArray<WordBinaryIssue>.Builder issues,
        CancellationToken cancellationToken)
    {
        if ((uint)rangeIndex >= (uint)fib.RangeCatalogue.Length)
        {
            return;
        }
        WordFibRange range = fib.RangeCatalogue[rangeIndex];
        if (range.Length == 0)
        {
            return;
        }
        if (!TryReadPlc(table, range, 4, limits.MaximumStructureRecords, out uint[] positions, out byte[][] records))
        {
            issues.Add(new("doc-bte-plc-invalid", $"The {kind} bin table is malformed.", range.Offset));
            return;
        }

        var pageNumbers = new HashSet<uint>();
        for (int pageIndex = 0; pageIndex < records.Length; pageIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (positions[pageIndex + 1] <= positions[pageIndex])
            {
                issues.Add(new("doc-bte-fc-order", "BTE FC boundaries are not strictly increasing.", range.Offset + (pageIndex * 4L)));
                continue;
            }
            uint pageNumber = U32(records[pageIndex]);
            if (!pageNumbers.Add(pageNumber))
            {
                issues.Add(new("doc-bte-fkp-alias", "Multiple BTE entries reference the same FKP page.", range.Offset + (pageIndex * 4L)));
                continue;
            }
            uint pageOffset;
            try
            {
                pageOffset = checked(pageNumber * 512u);
            }
            catch (OverflowException)
            {
                issues.Add(new("doc-fkp-page-overflow", "An FKP page address overflows.", range.Offset));
                continue;
            }
            if (!WordPieceTableParser.RangeFits(pageOffset, 512, wordDocument.Length))
            {
                issues.Add(new("doc-fkp-page-range", "An FKP page lies outside WordDocument.", pageOffset));
                continue;
            }

            ReadOnlySpan<byte> page = wordDocument.Slice(checked((int)pageOffset), 512);
            int count = page[511];
            int boundaryBytes = checked((count + 1) * 4);
            int descriptorEnd = checked(boundaryBytes + (count * descriptorSize));
            if (count == 0 || descriptorEnd > 511)
            {
                issues.Add(new("doc-fkp-layout-invalid", $"A {kind} FKP has an invalid run table.", pageOffset));
                continue;
            }

            uint fkpFirstFc = U32(page, 0);
            uint fkpLastFc = U32(page, count * 4);
            if (fkpFirstFc != positions[pageIndex] || fkpLastFc != positions[pageIndex + 1])
            {
                issues.Add(new("doc-bte-fkp-extent-mismatch", "The BTE FC interval does not match its referenced FKP extent.", pageOffset));
                continue;
            }

            uint previousFc = fkpFirstFc;
            for (int runIndex = 0; runIndex < count; runIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                uint nextFc = U32(page, (runIndex + 1) * 4);
                if (nextFc <= previousFc)
                {
                    issues.Add(new("doc-fkp-fc-order", "FKP FC boundaries are not strictly increasing.", pageOffset + (uint)((runIndex + 1) * 4)));
                    break;
                }

                int descriptorOffset = boundaryBytes + (runIndex * descriptorSize);
                int propertyOffset = page[descriptorOffset] * 2;
                ushort? style = null;
                ReadOnlySpan<byte> grpprl = [];
                if (propertyOffset != 0)
                {
                    if (propertyOffset >= 511)
                    {
                        issues.Add(new("doc-fkp-property-offset", "An FKP property offset is outside the page.", pageOffset + (uint)descriptorOffset));
                        previousFc = nextFc;
                        continue;
                    }

                    int byteLength;
                    int dataOffset;
                    if (kind == WordPropertyRunKind.Character)
                    {
                        byteLength = page[propertyOffset];
                        dataOffset = propertyOffset + 1;
                    }
                    else
                    {
                        byteLength = checked(page[propertyOffset] * 2);
                        dataOffset = propertyOffset + 1;
                        if (byteLength >= 2 && dataOffset <= 510 - 1)
                        {
                            style = U16(page, dataOffset);
                            dataOffset += 2;
                            byteLength -= 2;
                        }
                    }
                    if (byteLength < 0 || dataOffset > 512 - byteLength)
                    {
                        issues.Add(new("doc-fkp-property-range", "An FKP property sequence exceeds the page.", pageOffset + (uint)propertyOffset));
                        previousFc = nextFc;
                        continue;
                    }
                    grpprl = page.Slice(dataOffset, byteLength);
                }

                uint? cpStart = FileOffsetToCp(previousFc, pieces);
                uint? cpEnd = FileOffsetToCp(nextFc, pieces);
                if (cpStart is null || cpEnd is null)
                {
                    issues.Add(new("doc-fkp-fc-unmapped", "An FKP run could not be mapped to the piece table.", pageOffset));
                    previousFc = nextFc;
                    continue;
                }
                ImmutableArray<WordSprm> sprms = WordSprmParser.Parse(grpprl, limits.MaximumSprmsPerRun, out WordBinaryIssue? sprmIssue);
                if (sprmIssue is not null)
                {
                    issues.Add(sprmIssue with { Offset = pageOffset + (uint)propertyOffset });
                }
                string origin = $"{tableName}.BTE[{rangeIndex.ToString(CultureInfo.InvariantCulture)}].FKP[{pageNumber.ToString(CultureInfo.InvariantCulture)}]";
                AddRun(runs, limits, new(
                    StableId("property", origin, previousFc, nextFc - previousFc, SerializeSprms(sprms)),
                    kind, cpStart.Value, cpEnd.Value, previousFc, nextFc, origin,
                    checked((int)pageOffset + propertyOffset), style, sprms), issues);
                previousFc = nextFc;
            }
        }
    }

    private static void ParseStructureRanges(
        ReadOnlySpan<byte> table,
        string tableName,
        WordFib fib,
        WordBinaryExtractionLimits limits,
        ImmutableArray<WordStructureRecord>.Builder structures,
        ImmutableArray<WordBinaryIssue>.Builder issues,
        CancellationToken cancellationToken)
    {
        foreach (WordFibRange range in fib.RangeCatalogue)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!range.IsOffsetLengthPair || range.Length == 0 || range.Index is 12 or 13 or 33)
            {
                continue;
            }
            (WordStructureKind kind, int plcDataSize) = KnownRanges.GetValueOrDefault(range.Index, (WordStructureKind.Unknown, -1));
            ReadOnlySpan<byte> bytes = table.Slice(checked((int)range.Offset), checked((int)range.Length));
            if (plcDataSize >= 0 && TryReadPlc(table, range, plcDataSize, limits.MaximumStructureRecords, out uint[] positions, out byte[][] records))
            {
                for (int index = 0; index < records.Length; index++)
                {
                    AddStructure(structures, limits, new(
                        StableId("structure", $"{tableName}:{range.Index}:{index}", positions[index], positions[index + 1] - positions[index], records[index]),
                        kind, tableName, range.Index, range.Offset, range.Length, positions[index], positions[index + 1],
                        ImmutableArray.Create(records[index]), false), issues);
                    AddIssueOnce(issues, "doc-structure-semantic-unimplemented", "Generic PLC framing was decoded, but type-specific record semantics and anchor pairing remain unimplemented.");
                }
            }
            else
            {
                byte[] retained = bytes[..Math.Min(bytes.Length, MaximumRetainedRecordBytes)].ToArray();
                AddStructure(structures, limits, new(
                    StableId("structure", $"{tableName}:{range.Index}", range.Offset, range.Length, bytes),
                    kind, tableName, range.Index, range.Offset, range.Length, null, null,
                    ImmutableArray.Create(retained), false), issues);
                issues.Add(new("doc-structure-passive", $"FIB range {range.Index.ToString(CultureInfo.InvariantCulture)} ({kind}) was retained as passive evidence but is not semantically complete.", range.Offset));
            }
        }
    }

    private static bool TryReadPlc(
        ReadOnlySpan<byte> container,
        WordFibRange range,
        int dataSize,
        int maximumRecords,
        out uint[] positions,
        out byte[][] records)
    {
        positions = [];
        records = [];
        int divisor = checked(4 + dataSize);
        if (!WordPieceTableParser.RangeFits(range.Offset, range.Length, container.Length) || range.Length < 4 ||
            (range.Length - 4) % divisor != 0)
        {
            return false;
        }
        int count = checked((int)((range.Length - 4) / divisor));
        if (count <= 0 || count > maximumRecords)
        {
            return false;
        }
        int start = checked((int)range.Offset);
        positions = new uint[count + 1];
        records = new byte[count][];
        uint previous = U32(container, start);
        positions[0] = previous;
        for (int index = 0; index < count; index++)
        {
            uint next = U32(container, start + ((index + 1) * 4));
            if (next < previous)
            {
                return false;
            }
            positions[index + 1] = next;
            previous = next;
        }
        int recordsOffset = start + ((count + 1) * 4);
        for (int index = 0; index < count; index++)
        {
            records[index] = container.Slice(recordsOffset + (index * dataSize), dataSize).ToArray();
        }
        return true;
    }

    private static void InventoryStreams(
        CompoundFile compoundFile,
        WordFib fib,
        WordBinaryExtractionLimits limits,
        ImmutableArray<WordPassiveAsset>.Builder assets,
        ImmutableArray<WordMetadataProperty>.Builder metadata,
        ImmutableArray<WordBinaryIssue>.Builder issues,
        CancellationToken cancellationToken)
    {
        var reportedKinds = new HashSet<WordPassiveAssetKind>();
        long retainedBytes = 0;
        foreach (CompoundFileDirectoryEntry entry in compoundFile.DirectoryEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.ObjectType != CompoundFileObjectType.Stream || entry.Name is "WordDocument" or "0Table" or "1Table")
            {
                continue;
            }
            WordPassiveAssetKind kind = ClassifyAsset(compoundFile, entry, fib);
            (uint ownerStreamId, Guid ownerClassId, string sourcePath) = ResolveAssetProvenance(compoundFile, entry);
            byte[] hash = SHA256.HashData(entry.Content.AsSpan());
            string sha = Convert.ToHexStringLower(hash);
            AddAsset(assets, limits, ref retainedBytes, new(
                StableId("asset", entry.Name, entry.StreamId, checked((uint)Math.Min(entry.StreamSize, uint.MaxValue)), hash),
                kind, entry.Name, entry.StreamId, entry.StreamSize, sha, ownerClassId, entry.ParentStreamId,
                ownerStreamId, sourcePath, entry.Content), issues);
            if (reportedKinds.Add(kind))
            {
                issues.Add(new("doc-passive-asset-branch", $"A {kind} stream was retained and hashed without activation; its full semantics are outside this implemented subset."));
            }

            if (kind == WordPassiveAssetKind.PropertySet)
            {
                ParsePropertySet(entry, metadata, issues);
            }
        }
    }

    private static (uint OwnerStreamId, Guid OwnerClassId, string SourcePath) ResolveAssetProvenance(
        CompoundFile file,
        CompoundFileDirectoryEntry entry)
    {
        var path = new Stack<string>();
        path.Push(entry.Name);
        uint ownerStreamId = 0;
        Guid ownerClassId = Guid.Empty;
        uint? parent = entry.ParentStreamId;
        int depth = 0;
        while (parent is uint id && id < file.DirectoryEntries.Length && depth++ < 32)
        {
            CompoundFileDirectoryEntry ancestor = file.DirectoryEntries[(int)id];
            path.Push(ancestor.Name);
            if (ownerStreamId == 0 && ancestor.ObjectType is CompoundFileObjectType.Storage or CompoundFileObjectType.RootStorage)
            {
                ownerStreamId = ancestor.StreamId;
                ownerClassId = ancestor.ClassId;
            }
            parent = ancestor.ParentStreamId;
        }
        return (ownerStreamId, ownerClassId, string.Join('/', path));
    }

    private static WordPassiveAssetKind ClassifyAsset(CompoundFile file, CompoundFileDirectoryEntry entry, WordFib fib)
    {
        string ancestry = entry.Name;
        uint? parent = entry.ParentStreamId;
        int depth = 0;
        while (parent is uint id && id < file.DirectoryEntries.Length && depth++ < 32)
        {
            CompoundFileDirectoryEntry ancestor = file.DirectoryEntries[(int)id];
            ancestry = ancestor.Name + "/" + ancestry;
            parent = ancestor.ParentStreamId;
        }
        if (entry.Name is "\u0005SummaryInformation" or "\u0005DocumentSummaryInformation") return WordPassiveAssetKind.PropertySet;
        if (ancestry.Contains("VBA", StringComparison.OrdinalIgnoreCase) || ancestry.Contains("Macros", StringComparison.OrdinalIgnoreCase)) return WordPassiveAssetKind.MacroProject;
        if (ancestry.Contains("ObjectPool", StringComparison.OrdinalIgnoreCase) || ancestry.Contains("CompObj", StringComparison.OrdinalIgnoreCase)) return WordPassiveAssetKind.OleObject;
        if (ancestry.Contains("Forms", StringComparison.OrdinalIgnoreCase) || ancestry.Contains("ActiveX", StringComparison.OrdinalIgnoreCase)) return WordPassiveAssetKind.OfficeForm;
        if (entry.Name.Equals("Data", StringComparison.Ordinal) && fib.HasPictures) return WordPassiveAssetKind.PictureData;
        if (ancestry.Contains("Xml", StringComparison.OrdinalIgnoreCase) || ancestry.Contains("Custom", StringComparison.OrdinalIgnoreCase)) return WordPassiveAssetKind.CustomData;
        return WordPassiveAssetKind.UnknownStream;
    }

    private static void ParsePropertySet(
        CompoundFileDirectoryEntry entry,
        ImmutableArray<WordMetadataProperty>.Builder metadata,
        ImmutableArray<WordBinaryIssue>.Builder issues)
    {
        ReadOnlySpan<byte> bytes = entry.Content.AsSpan();
        if (bytes.Length < 48 || U16(bytes, 0) != 0xfffe || U32(bytes, 24) == 0)
        {
            issues.Add(new("doc-property-set-invalid", "An OLE property set header is invalid or unsupported."));
            return;
        }
        uint sectionOffset = U32(bytes, 44);
        if (sectionOffset > int.MaxValue || !RangeFits(sectionOffset, 8, bytes.Length))
        {
            issues.Add(new("doc-property-section-range", "An OLE property section lies outside its stream."));
            return;
        }
        int section = (int)sectionOffset;
        uint count = U32(bytes, section + 4);
        if (count > 4096 || !RangeFits(sectionOffset + 8, checked(count * 8), bytes.Length))
        {
            issues.Add(new("doc-property-count", "An OLE property table exceeds its bound."));
            return;
        }
        for (uint index = 0; index < count; index++)
        {
            int entryOffset = checked(section + 8 + ((int)index * 8));
            uint propertyId = U32(bytes, entryOffset);
            uint relative = U32(bytes, entryOffset + 4);
            uint absolute = checked(sectionOffset + relative);
            if (!RangeFits(absolute, 4, bytes.Length))
            {
                continue;
            }
            int valueOffset = (int)absolute;
            uint type = U32(bytes, valueOffset) & 0xffff;
            string? value = ReadPropertyValue(bytes, valueOffset + 4, type);
            if (value is null)
            {
                issues.Add(new("doc-property-type-passive", $"OLE property {propertyId.ToString(CultureInfo.InvariantCulture)} has unsupported type 0x{type:x}.", valueOffset));
                continue;
            }
            string name = PropertyName(entry.Name, propertyId);
            metadata.Add(new(StableId("metadata", entry.Name, propertyId, type, Encoding.UTF8.GetBytes(value)), entry.Name, propertyId, name, value, valueOffset));
        }
    }

    private static string? ReadPropertyValue(ReadOnlySpan<byte> bytes, int offset, uint type)
    {
        if (type == 2 && offset <= bytes.Length - 2) return BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..]).ToString(CultureInfo.InvariantCulture);
        if (type == 3 && offset <= bytes.Length - 4) return BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]).ToString(CultureInfo.InvariantCulture);
        if (type == 11 && offset <= bytes.Length - 2) return U16(bytes, offset) == 0 ? "false" : "true";
        if (type == 64 && offset <= bytes.Length - 8)
        {
            long fileTime = BinaryPrimitives.ReadInt64LittleEndian(bytes[offset..]);
            try { return DateTime.FromFileTimeUtc(fileTime).ToString("O", CultureInfo.InvariantCulture); }
            catch (ArgumentOutOfRangeException) { return null; }
        }
        if (type is 30 or 31 && offset <= bytes.Length - 4)
        {
            uint count = U32(bytes, offset);
            int unit = type == 31 ? 2 : 1;
            if (count > int.MaxValue / unit || !RangeFits((uint)(offset + 4), checked(count * (uint)unit), bytes.Length)) return null;
            ReadOnlySpan<byte> value = bytes.Slice(offset + 4, checked((int)count * unit));
            string text = type == 31 ? Encoding.Unicode.GetString(value) : Encoding.Latin1.GetString(value);
            return text.TrimEnd('\0');
        }
        return null;
    }

    private static string PropertyName(string set, uint id) => set == "\u0005SummaryInformation" ? id switch
    {
        2 => "Title",
        3 => "Subject",
        4 => "Author",
        5 => "Keywords",
        6 => "Comments",
        8 => "LastSavedBy",
        9 => "Revision",
        12 => "Created",
        13 => "Modified",
        14 => "LastPrinted",
        18 => "Application",
        _ => $"Property-{id.ToString(CultureInfo.InvariantCulture)}",
    } : id switch
    {
        14 => "Manager",
        15 => "Company",
        _ => $"Property-{id.ToString(CultureInfo.InvariantCulture)}",
    };

    private static uint? FileOffsetToCp(uint fileOffset, ImmutableArray<WordPiece> pieces)
    {
        foreach (WordPiece piece in pieces)
        {
            uint end = checked(piece.FileOffset + piece.ByteLength);
            if (fileOffset >= piece.FileOffset && fileOffset <= end)
            {
                uint unit = piece.IsUnicode ? 2u : 1u;
                uint delta = fileOffset - piece.FileOffset;
                if (delta % unit == 0)
                {
                    return checked(piece.CpStart + (delta / unit));
                }
            }
        }
        return null;
    }

    private static void AddRun(ImmutableArray<WordPropertyRun>.Builder builder, WordBinaryExtractionLimits limits, WordPropertyRun item, ImmutableArray<WordBinaryIssue>.Builder issues)
    {
        if (builder.Count >= limits.MaximumPropertyRuns) AddIssueOnce(issues, "doc-property-run-limit", "The property-run count exceeded its configured bound.");
        else builder.Add(item);
    }

    private static void AddStructure(ImmutableArray<WordStructureRecord>.Builder builder, WordBinaryExtractionLimits limits, WordStructureRecord item, ImmutableArray<WordBinaryIssue>.Builder issues)
    {
        if (builder.Count >= limits.MaximumStructureRecords) AddIssueOnce(issues, "doc-structure-limit", "The structure-record count exceeded its configured bound.");
        else builder.Add(item);
    }

    private static void AddAsset(
        ImmutableArray<WordPassiveAsset>.Builder builder,
        WordBinaryExtractionLimits limits,
        ref long retainedBytes,
        WordPassiveAsset item,
        ImmutableArray<WordBinaryIssue>.Builder issues)
    {
        if (builder.Count >= limits.MaximumPassiveAssets)
        {
            AddIssueOnce(issues, "doc-passive-asset-limit", "The passive-asset count exceeded its configured bound.");
            return;
        }
        if (item.Content.Length > limits.MaximumPassiveAssetBytes - retainedBytes)
        {
            AddIssueOnce(issues, "doc-passive-asset-byte-limit", "The retained passive-asset bytes exceeded their configured cumulative bound.");
            return;
        }
        retainedBytes = checked(retainedBytes + item.Content.Length);
        builder.Add(item);
    }

    private static void AddIssueOnce(ImmutableArray<WordBinaryIssue>.Builder issues, string code, string message)
    {
        foreach (WordBinaryIssue issue in issues)
        {
            if (issue.Code.Equals(code, StringComparison.Ordinal))
            {
                return;
            }
        }
        issues.Add(new(code, message));
    }

    private static string StableId(string category, string origin, uint offset, uint length, ReadOnlySpan<byte> content)
    {
        byte[] prefix = Encoding.UTF8.GetBytes($"{category}\0{origin}\0{offset.ToString(CultureInfo.InvariantCulture)}\0{length.ToString(CultureInfo.InvariantCulture)}\0");
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(prefix);
        hash.AppendData(content);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static byte[] SerializeSprms(ImmutableArray<WordSprm> sprms)
    {
        using var stream = new MemoryStream();
        foreach (WordSprm sprm in sprms)
        {
            stream.WriteByte((byte)sprm.Opcode);
            stream.WriteByte((byte)(sprm.Opcode >> 8));
            stream.Write(sprm.Operand.AsSpan());
        }
        return stream.ToArray();
    }

    private static bool RangeFits(uint offset, uint length, int total) => offset <= (uint)total && length <= (uint)total - offset;
    private static ushort U16(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
    private static uint U32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static uint U32(ReadOnlySpan<byte> bytes) => BinaryPrimitives.ReadUInt32LittleEndian(bytes);
}

public static class WordSprmParser
{
    internal static WordSprm FromSimplePrm(byte isprm, byte value)
    {
        ushort opcode = isprm switch
        {
            5 => 0x2403,
            12 => 0x260a,
            24 => 0x2416,
            85 => 0x0835,
            86 => 0x0836,
            87 => 0x0837,
            92 => 0x083c,
            _ => isprm,
        };
        WordSprmMeaning meaning = Meaning(opcode);
        return new(opcode, meaning, [value], 0, meaning != WordSprmMeaning.Unknown);
    }

    public static ImmutableArray<WordSprm> Parse(ReadOnlySpan<byte> bytes, int maximumSprms, out WordBinaryIssue? issue)
    {
        issue = null;
        if (maximumSprms <= 0)
        {
            issue = new("doc-sprm-limit", "The SPRM count bound must be positive.");
            return [];
        }
        var result = ImmutableArray.CreateBuilder<WordSprm>();
        int cursor = 0;
        while (cursor < bytes.Length)
        {
            if (result.Count >= maximumSprms)
            {
                issue = new("doc-sprm-limit", "The SPRM sequence exceeds its configured count bound.", cursor);
                break;
            }
            if (cursor > bytes.Length - 2)
            {
                issue = new("doc-sprm-truncated", "A SPRM opcode is truncated.", cursor);
                break;
            }
            int recordOffset = cursor;
            ushort opcode = BinaryPrimitives.ReadUInt16LittleEndian(bytes[cursor..]);
            cursor += 2;
            int spra = opcode >> 13;
            int operandLength;
            if (spra == 6)
            {
                if (cursor >= bytes.Length)
                {
                    issue = new("doc-sprm-length-truncated", "A variable-length SPRM has no length byte.", recordOffset);
                    break;
                }
                operandLength = bytes[cursor++];
            }
            else
            {
                operandLength = spra switch { 0 or 1 => 1, 2 or 4 or 5 => 2, 3 => 4, 7 => 3, _ => 0 };
            }
            if (cursor > bytes.Length - operandLength)
            {
                issue = new("doc-sprm-operand-truncated", "A SPRM operand exceeds its containing record.", recordOffset);
                break;
            }
            ImmutableArray<byte> operand = ImmutableArray.Create(bytes.Slice(cursor, operandLength).ToArray());
            cursor += operandLength;
            WordSprmMeaning meaning = Meaning(opcode);
            result.Add(new(opcode, meaning, operand, recordOffset, meaning != WordSprmMeaning.Unknown));
        }
        return result.ToImmutable();
    }

    private static WordSprmMeaning Meaning(ushort opcode) => opcode switch
    {
        0x4600 => WordSprmMeaning.ParagraphStyle,
        0x2403 => WordSprmMeaning.ParagraphAlignment,
        0x2416 => WordSprmMeaning.InTable,
        0x260a or 0x460b => WordSprmMeaning.ListLevel,
        0x4a30 => WordSprmMeaning.Font,
        0x486d or 0x4873 => WordSprmMeaning.Language,
        0x0835 => WordSprmMeaning.Bold,
        0x0836 => WordSprmMeaning.Italic,
        0x0837 => WordSprmMeaning.Strike,
        0x083c => WordSprmMeaning.Hidden,
        _ => WordSprmMeaning.Unknown,
    };
}
