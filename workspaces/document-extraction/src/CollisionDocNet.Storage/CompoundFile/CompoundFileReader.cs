using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;

namespace CollisionDocNet.Storage.CompoundFile;

/// <summary>
/// Strict, read-only MS-CFB v3/v4 parser. It validates the complete allocation
/// graph before exposing directory entries or stream bytes.
/// </summary>
public static class CompoundFileReader
{
    public static CompoundFileReadResult Read(
        ReadOnlyMemory<byte> fileBytes,
        CompoundFileReadLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        limits ??= CompoundFileReadLimits.Default;
        if (!AreLimitsValid(limits) || fileBytes.Length > limits.MaximumInputBytes)
        {
            return CompoundFileReadResult.Failure(CompoundFileReadError.InputLimitExceeded);
        }

        try
        {
            return new Parser(fileBytes, limits, cancellationToken).Parse();
        }
        catch (OperationCanceledException)
        {
            return CompoundFileReadResult.Failure(CompoundFileReadError.Cancelled);
        }
    }

    private static bool AreLimitsValid(CompoundFileReadLimits limits) =>
        limits.MaximumInputBytes >= CompoundFileConstants.HeaderLength &&
        limits.MaximumSectors > 0 &&
        limits.MaximumDirectoryEntries > 0 &&
        limits.MaximumStreamBytes >= 0 &&
        limits.MaximumTotalStreamBytes >= 0;

    private sealed class Parser(
        ReadOnlyMemory<byte> bytes,
        CompoundFileReadLimits limits,
        CancellationToken cancellationToken)
    {
        private readonly HashSet<uint> _claimedSectors = [];
        private readonly HashSet<uint> _claimedMiniSectors = [];
        private long _totalStreamBytes;
        private CompoundFileHeader _header = null!;
        private uint[] _fat = [];
        private uint[] _miniFat = [];
        private int _sectorCount;

        internal CompoundFileReadResult Parse()
        {
            cancellationToken.ThrowIfCancellationRequested();
            CompoundFileHeaderReadResult headerResult = CompoundFileHeaderReader.Read(bytes.Span);
            if (!headerResult.IsSuccess)
            {
                return CompoundFileReadResult.HeaderFailure(headerResult.Error);
            }

            _header = headerResult.Header!;
            _sectorCount = (bytes.Length / _header.SectorSize) - 1;
            if (_sectorCount > limits.MaximumSectors)
            {
                return Fail(CompoundFileReadError.SectorCountLimitExceeded);
            }

            CompoundFileReadResult? failure = ReadAllocationTables(out uint[] fatSectorIds);
            if (failure is not null)
            {
                return failure.Value;
            }

            failure = ReadDirectory(out DirectoryEntryData[] entries);
            if (failure is not null)
            {
                return failure.Value;
            }

            failure = ValidateDirectoryTree(entries, out uint?[] parents);
            if (failure is not null)
            {
                return failure.Value;
            }

            failure = ReadStreams(entries, parents, out ImmutableArray<CompoundFileDirectoryEntry> publicEntries);
            if (failure is not null)
            {
                return failure.Value;
            }

            failure = ValidateEveryAllocatedSectorWasReferenced();
            if (failure is not null)
            {
                return failure.Value;
            }

            var file = new CompoundFile(
                _header,
                [.. fatSectorIds],
                [.. _fat],
                [.. _miniFat],
                publicEntries);
            return CompoundFileReadResult.Success(file);
        }

        private CompoundFileReadResult? ReadAllocationTables(out uint[] fatSectorIds)
        {
            fatSectorIds = [];
            if (_header.FatSectorCount == 0 || _header.FatSectorCount > (uint)_sectorCount)
            {
                return Fail(CompoundFileReadError.InvalidDifat);
            }

            var ids = new List<uint>(checked((int)_header.FatSectorCount));
            var seenFat = new HashSet<uint>();
            int headerCount = Math.Min((int)_header.FatSectorCount, _header.HeaderDifat.Length);
            for (int index = 0; index < _header.HeaderDifat.Length; index++)
            {
                uint sector = _header.HeaderDifat[index];
                if (index < headerCount)
                {
                    if (!IsRegularSector(sector) || !seenFat.Add(sector))
                    {
                        return Fail(CompoundFileReadError.InvalidDifat, sector);
                    }

                    ids.Add(sector);
                }
                else if (sector != CompoundFileConstants.FreeSector)
                {
                    return Fail(CompoundFileReadError.InvalidDifat, sector);
                }
            }

            uint remainingFatSectors = _header.FatSectorCount - (uint)headerCount;
            int entriesPerDifatSector = (_header.SectorSize / sizeof(uint)) - 1;
            ulong requiredDifatSectors = remainingFatSectors == 0
                ? 0
                : ((ulong)remainingFatSectors + (uint)entriesPerDifatSector - 1) /
                    (uint)entriesPerDifatSector;
            if (_header.DifatSectorCount != requiredDifatSectors ||
                (_header.DifatSectorCount == 0) !=
                    (_header.FirstDifatSector == CompoundFileConstants.EndOfChain))
            {
                return Fail(CompoundFileReadError.InvalidDifat);
            }

            var difatSectors = new HashSet<uint>();
            uint current = _header.FirstDifatSector;
            for (uint chainIndex = 0; chainIndex < _header.DifatSectorCount; chainIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsRegularSector(current))
                {
                    return Fail(CompoundFileReadError.SectorOutOfRange, current);
                }

                if (!difatSectors.Add(current))
                {
                    return Fail(CompoundFileReadError.DifatCycle, current);
                }

                ReadOnlySpan<byte> sectorBytes = GetSector(current);
                for (int index = 0; index < entriesPerDifatSector; index++)
                {
                    uint fatSector = ReadUInt32(sectorBytes, index * sizeof(uint));
                    if (ids.Count < _header.FatSectorCount)
                    {
                        if (!IsRegularSector(fatSector) || !seenFat.Add(fatSector))
                        {
                            return Fail(CompoundFileReadError.InvalidDifat, fatSector);
                        }

                        ids.Add(fatSector);
                    }
                    else if (fatSector != CompoundFileConstants.FreeSector)
                    {
                        return Fail(CompoundFileReadError.InvalidDifat, fatSector);
                    }
                }

                uint next = ReadUInt32(sectorBytes, _header.SectorSize - sizeof(uint));
                bool isLast = chainIndex + 1 == _header.DifatSectorCount;
                if (isLast != (next == CompoundFileConstants.EndOfChain))
                {
                    return Fail(CompoundFileReadError.InvalidDifat, current);
                }

                current = next;
            }

            if (ids.Count != _header.FatSectorCount)
            {
                return Fail(CompoundFileReadError.InvalidDifat);
            }

            foreach (uint sector in ids)
            {
                if (difatSectors.Contains(sector) || !_claimedSectors.Add(sector))
                {
                    return Fail(CompoundFileReadError.SectorCrossLinked, sector);
                }
            }

            foreach (uint sector in difatSectors)
            {
                if (!_claimedSectors.Add(sector))
                {
                    return Fail(CompoundFileReadError.SectorCrossLinked, sector);
                }
            }

            int fatEntryCount = checked(ids.Count * (_header.SectorSize / sizeof(uint)));
            _fat = new uint[fatEntryCount];
            int destination = 0;
            foreach (uint sector in ids)
            {
                ReadOnlySpan<byte> sectorBytes = GetSector(sector);
                for (int offset = 0; offset < sectorBytes.Length; offset += sizeof(uint))
                {
                    _fat[destination++] = ReadUInt32(sectorBytes, offset);
                }
            }

            if (_fat.Length < _sectorCount)
            {
                return Fail(CompoundFileReadError.InvalidFat);
            }

            foreach (uint sector in ids)
            {
                if (_fat[sector] != CompoundFileConstants.FatSector)
                {
                    return Fail(CompoundFileReadError.InvalidFat, sector);
                }
            }

            foreach (uint sector in difatSectors)
            {
                if (_fat[sector] != CompoundFileConstants.DifatSector)
                {
                    return Fail(CompoundFileReadError.InvalidFat, sector);
                }
            }

            for (int index = _sectorCount; index < _fat.Length; index++)
            {
                if (_fat[index] != CompoundFileConstants.FreeSector)
                {
                    return Fail(CompoundFileReadError.InvalidFat, (uint)index);
                }
            }

            fatSectorIds = [.. ids];
            return null;
        }

        private CompoundFileReadResult? ReadDirectory(out DirectoryEntryData[] entries)
        {
            entries = [];
            uint? expectedCount = _header.MajorVersion == 4
                ? _header.DirectorySectorCount
                : null;
            CompoundFileReadResult? failure = ReadFatChain(
                _header.FirstDirectorySector,
                expectedCount,
                CompoundFileReadError.InvalidDirectoryChain,
                out uint[] directorySectors);
            if (failure is not null)
            {
                return failure.Value;
            }

            int entriesPerSector = _header.SectorSize / CompoundFileConstants.DirectoryEntryLength;
            long entryCount = (long)directorySectors.Length * entriesPerSector;
            if (entryCount > limits.MaximumDirectoryEntries)
            {
                return Fail(CompoundFileReadError.DirectoryEntryLimitExceeded);
            }

            entries = new DirectoryEntryData[entryCount];
            int streamId = 0;
            foreach (uint sector in directorySectors)
            {
                ReadOnlySpan<byte> sectorBytes = GetSector(sector);
                for (int offset = 0; offset < sectorBytes.Length; offset += CompoundFileConstants.DirectoryEntryLength)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    CompoundFileReadResult? parseFailure = ParseDirectoryEntry(
                        sectorBytes.Slice(offset, CompoundFileConstants.DirectoryEntryLength),
                        (uint)streamId,
                        out DirectoryEntryData entry);
                    if (parseFailure is not null)
                    {
                        return parseFailure.Value;
                    }

                    entries[streamId++] = entry;
                }
            }

            if (entries.Length == 0 ||
                entries[0].ObjectType != CompoundFileObjectType.RootStorage ||
                entries[0].Name != "Root Entry" ||
                entries[0].NameLength != 22 ||
                entries[0].LeftSiblingId != CompoundFileConstants.NoStream ||
                entries[0].RightSiblingId != CompoundFileConstants.NoStream ||
                entries[0].CreationTime != 0)
            {
                return Fail(CompoundFileReadError.InvalidDirectoryEntry, 0);
            }

            return null;
        }

        private CompoundFileReadResult? ParseDirectoryEntry(
            ReadOnlySpan<byte> bytes,
            uint streamId,
            out DirectoryEntryData entry)
        {
            entry = default;
            byte objectTypeValue = bytes[66];
            if (objectTypeValue is not (0 or 1 or 2 or 5))
            {
                return Fail(CompoundFileReadError.InvalidDirectoryEntry, streamId);
            }

            var objectType = (CompoundFileObjectType)objectTypeValue;
            uint left = ReadUInt32(bytes, 68);
            uint right = ReadUInt32(bytes, 72);
            uint child = ReadUInt32(bytes, 76);
            if (objectType == CompoundFileObjectType.Unallocated)
            {
                if (left != CompoundFileConstants.NoStream ||
                    right != CompoundFileConstants.NoStream ||
                    child != CompoundFileConstants.NoStream ||
                    ContainsNonZeroByte(bytes[..66]) ||
                    ContainsNonZeroByte(bytes[80..]))
                {
                    return Fail(CompoundFileReadError.InvalidDirectoryEntry, streamId);
                }

                entry = new(streamId, string.Empty, 0, objectType, CompoundFileNodeColor.Red,
                    left, right, child, Guid.Empty, 0, 0, 0,
                    CompoundFileConstants.EndOfChain, 0);
                return null;
            }

            ushort nameLength = ReadUInt16(bytes, 64);
            if (nameLength is < 2 or > 64 || (nameLength & 1) != 0 ||
                bytes[nameLength - 2] != 0 || bytes[nameLength - 1] != 0 ||
                ContainsNonZeroByte(bytes.Slice(nameLength, 64 - nameLength)))
            {
                return Fail(CompoundFileReadError.InvalidDirectoryEntry, streamId);
            }

            string name = Encoding.Unicode.GetString(bytes[..(nameLength - 2)]);
            if (name.IndexOfAny(['/', '\\', ':', '!']) >= 0 || bytes[67] > 1 ||
                !IsStreamIdOrNoStream(left) || !IsStreamIdOrNoStream(right) ||
                !IsStreamIdOrNoStream(child))
            {
                return Fail(CompoundFileReadError.InvalidDirectoryEntry, streamId);
            }

            Guid classId = new(bytes.Slice(80, 16));
            long creationTime = ReadInt64(bytes, 100);
            long modifiedTime = ReadInt64(bytes, 108);
            ulong rawSize = ReadUInt64(bytes, 120);
            ulong streamSize = _header.MajorVersion == 3 ? (uint)rawSize : rawSize;

            if ((objectType == CompoundFileObjectType.Stream &&
                 (child != CompoundFileConstants.NoStream || classId != Guid.Empty ||
                  creationTime != 0 || modifiedTime != 0)) ||
                (objectType == CompoundFileObjectType.Storage && streamSize != 0) ||
                (objectType == CompoundFileObjectType.RootStorage && streamId != 0) ||
                (objectType != CompoundFileObjectType.RootStorage && streamId == 0))
            {
                return Fail(CompoundFileReadError.InvalidDirectoryEntry, streamId);
            }

            entry = new(
                streamId,
                name,
                nameLength,
                objectType,
                (CompoundFileNodeColor)bytes[67],
                left,
                right,
                child,
                classId,
                ReadUInt32(bytes, 96),
                creationTime,
                modifiedTime,
                ReadUInt32(bytes, 116),
                streamSize);
            return null;
        }

        private CompoundFileReadResult? ValidateDirectoryTree(
            DirectoryEntryData[] entries,
            out uint?[] parents)
        {
            parents = new uint?[entries.Length];
            var reached = new HashSet<uint> { 0 };
            var storageStack = new Stack<uint>();
            storageStack.Push(0);

            while (storageStack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                uint parentId = storageStack.Pop();
                DirectoryEntryData parent = entries[parentId];
                if (parent.ChildId == CompoundFileConstants.NoStream)
                {
                    continue;
                }

                if (!IsValidEntryId(parent.ChildId, entries) ||
                    entries[parent.ChildId].Color != CompoundFileNodeColor.Black)
                {
                    return Fail(CompoundFileReadError.InvalidDirectoryTree, parent.ChildId);
                }

                var nodeStack = new Stack<TreeFrame>();
                nodeStack.Push(new(parent.ChildId, null, null, false));
                var local = new HashSet<uint>();
                while (nodeStack.Count > 0)
                {
                    TreeFrame frame = nodeStack.Pop();
                    if (!IsValidEntryId(frame.StreamId, entries))
                    {
                        return Fail(CompoundFileReadError.InvalidDirectoryTree, frame.StreamId);
                    }

                    if (!local.Add(frame.StreamId))
                    {
                        return Fail(CompoundFileReadError.DirectoryTreeCycle, frame.StreamId);
                    }

                    if (!reached.Add(frame.StreamId))
                    {
                        return Fail(CompoundFileReadError.DirectoryEntryCrossLinked, frame.StreamId);
                    }

                    DirectoryEntryData node = entries[frame.StreamId];
                    if (node.ObjectType is not (CompoundFileObjectType.Storage or CompoundFileObjectType.Stream) ||
                        (frame.ParentIsRed && node.Color == CompoundFileNodeColor.Red) ||
                        (frame.LowerBound is not null && CompareNames(entries[frame.LowerBound.Value], node) >= 0) ||
                        (frame.UpperBound is not null && CompareNames(node, entries[frame.UpperBound.Value]) >= 0))
                    {
                        return Fail(CompoundFileReadError.InvalidDirectoryTree, frame.StreamId);
                    }

                    parents[frame.StreamId] = parentId;
                    if (node.ObjectType == CompoundFileObjectType.Storage)
                    {
                        storageStack.Push(frame.StreamId);
                    }

                    bool isRed = node.Color == CompoundFileNodeColor.Red;
                    if (node.RightSiblingId != CompoundFileConstants.NoStream)
                    {
                        nodeStack.Push(new(node.RightSiblingId, frame.StreamId, frame.UpperBound, isRed));
                    }

                    if (node.LeftSiblingId != CompoundFileConstants.NoStream)
                    {
                        nodeStack.Push(new(node.LeftSiblingId, frame.LowerBound, frame.StreamId, isRed));
                    }
                }

            }

            for (int index = 0; index < entries.Length; index++)
            {
                if (entries[index].ObjectType != CompoundFileObjectType.Unallocated &&
                    !reached.Contains((uint)index))
                {
                    return Fail(CompoundFileReadError.InvalidDirectoryTree, (uint)index);
                }
            }

            return null;
        }

        private CompoundFileReadResult? ReadStreams(
            DirectoryEntryData[] entries,
            uint?[] parents,
            out ImmutableArray<CompoundFileDirectoryEntry> publicEntries)
        {
            publicEntries = [];
            DirectoryEntryData root = entries[0];
            if (root.StreamSize > int.MaxValue ||
                root.StreamSize > (ulong)limits.MaximumStreamBytes)
            {
                return Fail(CompoundFileReadError.StreamLimitExceeded, 0);
            }

            CompoundFileReadResult? failure = ReadRegularStream(
                root.StartingSector,
                root.StreamSize,
                out byte[] miniStream);
            if (failure is not null)
            {
                return failure.Value;
            }

            failure = ReadMiniFat();
            if (failure is not null)
            {
                return failure.Value;
            }

            var builder = ImmutableArray.CreateBuilder<CompoundFileDirectoryEntry>(entries.Length);
            foreach (DirectoryEntryData entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] content = [];
                if (entry.ObjectType == CompoundFileObjectType.Stream)
                {
                    if (entry.StreamSize > (ulong)limits.MaximumStreamBytes)
                    {
                        return Fail(CompoundFileReadError.StreamLimitExceeded, entry.StreamId);
                    }

                    if (entry.StreamSize > (ulong)(limits.MaximumTotalStreamBytes - _totalStreamBytes))
                    {
                        return Fail(CompoundFileReadError.TotalStreamLimitExceeded, entry.StreamId);
                    }

                    failure = entry.StreamSize < _header.MiniStreamCutoff
                        ? ReadMiniStream(entry.StartingSector, entry.StreamSize, miniStream, out content)
                        : ReadRegularStream(entry.StartingSector, entry.StreamSize, out content);
                    if (failure is not null)
                    {
                        return failure.Value;
                    }

                    _totalStreamBytes += checked((long)entry.StreamSize);
                }

                builder.Add(new(
                    entry.StreamId,
                    entry.Name,
                    entry.NameLength,
                    entry.ObjectType,
                    entry.Color,
                    entry.LeftSiblingId,
                    entry.RightSiblingId,
                    entry.ChildId,
                    entry.ClassId,
                    entry.StateBits,
                    entry.CreationTime,
                    entry.ModifiedTime,
                    entry.StartingSector,
                    entry.StreamSize,
                    parents[entry.StreamId],
                    [.. content]));
            }

            publicEntries = builder.MoveToImmutable();
            return ValidateEveryAllocatedMiniSectorWasReferenced(miniStream.Length);
        }

        private CompoundFileReadResult? ReadMiniFat()
        {
            if (_header.MiniFatSectorCount == 0)
            {
                if (_header.FirstMiniFatSector != CompoundFileConstants.EndOfChain)
                {
                    return Fail(CompoundFileReadError.InvalidMiniFat);
                }

                _miniFat = [];
                return null;
            }

            CompoundFileReadResult? failure = ReadFatChain(
                _header.FirstMiniFatSector,
                _header.MiniFatSectorCount,
                CompoundFileReadError.InvalidMiniFat,
                out uint[] sectors);
            if (failure is not null)
            {
                return failure.Value;
            }

            _miniFat = new uint[checked(sectors.Length * (_header.SectorSize / sizeof(uint)))];
            int index = 0;
            foreach (uint sector in sectors)
            {
                ReadOnlySpan<byte> sectorBytes = GetSector(sector);
                for (int offset = 0; offset < sectorBytes.Length; offset += sizeof(uint))
                {
                    _miniFat[index++] = ReadUInt32(sectorBytes, offset);
                }
            }

            return null;
        }

        private CompoundFileReadResult? ReadRegularStream(
            uint startingSector,
            ulong size,
            out byte[] content)
        {
            content = [];
            uint sectorCount = RequiredUnitCount(size, _header.SectorSize);
            if (sectorCount == 0)
            {
                return startingSector == CompoundFileConstants.EndOfChain
                    ? null
                    : Fail(CompoundFileReadError.StreamChainLengthMismatch, startingSector);
            }

            CompoundFileReadResult? failure = ReadFatChain(
                startingSector,
                sectorCount,
                CompoundFileReadError.StreamChainLengthMismatch,
                out uint[] sectors);
            if (failure is not null)
            {
                return failure.Value;
            }

            content = new byte[checked((int)size)];
            int written = 0;
            foreach (uint sector in sectors)
            {
                ReadOnlySpan<byte> source = GetSector(sector);
                int count = Math.Min(source.Length, content.Length - written);
                source[..count].CopyTo(content.AsSpan(written));
                written += count;
            }

            return null;
        }

        private CompoundFileReadResult? ReadMiniStream(
            uint startingSector,
            ulong size,
            ReadOnlySpan<byte> miniStream,
            out byte[] content)
        {
            content = [];
            uint miniSectorCount = RequiredUnitCount(size, _header.MiniSectorSize);
            if (miniSectorCount == 0)
            {
                return startingSector == CompoundFileConstants.EndOfChain
                    ? null
                    : Fail(CompoundFileReadError.StreamChainLengthMismatch, startingSector);
            }

            content = new byte[checked((int)size)];
            int written = 0;
            uint current = startingSector;
            var local = new HashSet<uint>();
            for (uint index = 0; index < miniSectorCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (current >= _miniFat.Length ||
                    (ulong)current * (uint)_header.MiniSectorSize >= (ulong)miniStream.Length)
                {
                    return Fail(CompoundFileReadError.InvalidMiniFat, current);
                }

                if (!local.Add(current))
                {
                    return Fail(CompoundFileReadError.MiniFatCycle, current);
                }

                if (!_claimedMiniSectors.Add(current))
                {
                    return Fail(CompoundFileReadError.SectorCrossLinked, current);
                }

                int sourceOffset = checked((int)current * _header.MiniSectorSize);
                int count = Math.Min(_header.MiniSectorSize, content.Length - written);
                miniStream.Slice(sourceOffset, count).CopyTo(content.AsSpan(written));
                written += count;

                uint next = _miniFat[current];
                bool isLast = index + 1 == miniSectorCount;
                if (isLast != (next == CompoundFileConstants.EndOfChain))
                {
                    return Fail(CompoundFileReadError.StreamChainLengthMismatch, current);
                }

                current = next;
            }

            return null;
        }

        private CompoundFileReadResult? ReadFatChain(
            uint startingSector,
            uint? expectedCount,
            CompoundFileReadError chainError,
            out uint[] sectors)
        {
            if (expectedCount is not null && expectedCount.Value > _sectorCount)
            {
                sectors = [];
                return Fail(chainError, startingSector);
            }

            var chain = expectedCount is null
                ? new List<uint>()
                : new List<uint>(checked((int)expectedCount.Value));
            var local = new HashSet<uint>();
            uint current = startingSector;
            while (current != CompoundFileConstants.EndOfChain)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsRegularSector(current) || current >= _fat.Length)
                {
                    sectors = [];
                    return Fail(CompoundFileReadError.SectorOutOfRange, current);
                }

                if (!local.Add(current))
                {
                    sectors = [];
                    return Fail(CompoundFileReadError.FatCycle, current);
                }

                if (!_claimedSectors.Add(current))
                {
                    sectors = [];
                    return Fail(CompoundFileReadError.SectorCrossLinked, current);
                }

                chain.Add(current);
                if (chain.Count > _sectorCount ||
                    (expectedCount is not null && chain.Count > expectedCount.Value))
                {
                    sectors = [];
                    return Fail(chainError, current);
                }

                uint next = _fat[current];
                if (next is CompoundFileConstants.FreeSector or
                    CompoundFileConstants.FatSector or
                    CompoundFileConstants.DifatSector)
                {
                    sectors = [];
                    return Fail(CompoundFileReadError.UnallocatedSectorReferenced, current);
                }

                current = next;
            }

            if (expectedCount is not null && chain.Count != expectedCount.Value)
            {
                sectors = [];
                return Fail(chainError, startingSector);
            }

            sectors = [.. chain];
            return null;
        }

        private CompoundFileReadResult? ValidateEveryAllocatedSectorWasReferenced()
        {
            for (uint sector = 0; sector < _sectorCount; sector++)
            {
                bool allocated = _fat[sector] != CompoundFileConstants.FreeSector;
                if (allocated != _claimedSectors.Contains(sector))
                {
                    return Fail(
                        allocated
                            ? CompoundFileReadError.AllocatedSectorUnreferenced
                            : CompoundFileReadError.UnallocatedSectorReferenced,
                        sector);
                }
            }

            return null;
        }

        private CompoundFileReadResult? ValidateEveryAllocatedMiniSectorWasReferenced(int miniStreamLength)
        {
            int availableMiniSectors = miniStreamLength / _header.MiniSectorSize;
            for (uint sector = 0; sector < _miniFat.Length; sector++)
            {
                bool allocated = _miniFat[sector] != CompoundFileConstants.FreeSector;
                if (sector >= availableMiniSectors && allocated)
                {
                    return Fail(CompoundFileReadError.InvalidMiniFat, sector);
                }

                if (sector < availableMiniSectors &&
                    allocated != _claimedMiniSectors.Contains(sector))
                {
                    return Fail(
                        allocated
                            ? CompoundFileReadError.AllocatedSectorUnreferenced
                            : CompoundFileReadError.UnallocatedSectorReferenced,
                        sector);
                }
            }

            return null;
        }

        private bool IsRegularSector(uint sector) =>
            sector <= CompoundFileConstants.MaximumRegularSector && sector < _sectorCount;

        private static bool IsStreamIdOrNoStream(uint streamId) =>
            streamId <= CompoundFileConstants.MaximumRegularSector ||
            streamId == CompoundFileConstants.NoStream;

        private static bool IsValidEntryId(uint streamId, DirectoryEntryData[] entries) =>
            streamId < entries.Length &&
            entries[streamId].ObjectType != CompoundFileObjectType.Unallocated &&
            streamId != 0;

        private ReadOnlySpan<byte> GetSector(uint sector)
        {
            int offset = checked((int)(sector + 1) * _header.SectorSize);
            return bytes.Span.Slice(offset, _header.SectorSize);
        }

        private static int CompareNames(DirectoryEntryData left, DirectoryEntryData right)
        {
            int lengthComparison = left.NameLength.CompareTo(right.NameLength);
            if (lengthComparison != 0)
            {
                return lengthComparison;
            }

            for (int index = 0; index < left.Name.Length; index++)
            {
                int comparison = char.ToUpperInvariant(left.Name[index])
                    .CompareTo(char.ToUpperInvariant(right.Name[index]));
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return 0;
        }

        private static uint RequiredUnitCount(ulong size, int unitSize) =>
            size == 0 ? 0 : checked((uint)(((size - 1) / (uint)unitSize) + 1));

        private static bool ContainsNonZeroByte(ReadOnlySpan<byte> source)
        {
            foreach (byte value in source)
            {
                if (value != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static ushort ReadUInt16(ReadOnlySpan<byte> source, int offset) =>
            BinaryPrimitives.ReadUInt16LittleEndian(source[offset..]);

        private static uint ReadUInt32(ReadOnlySpan<byte> source, int offset) =>
            BinaryPrimitives.ReadUInt32LittleEndian(source[offset..]);

        private static ulong ReadUInt64(ReadOnlySpan<byte> source, int offset) =>
            BinaryPrimitives.ReadUInt64LittleEndian(source[offset..]);

        private static long ReadInt64(ReadOnlySpan<byte> source, int offset) =>
            BinaryPrimitives.ReadInt64LittleEndian(source[offset..]);

        private static CompoundFileReadResult Fail(
            CompoundFileReadError error,
            uint? location = null) =>
            CompoundFileReadResult.Failure(error, location);

        private readonly record struct TreeFrame(
            uint StreamId,
            uint? LowerBound,
            uint? UpperBound,
            bool ParentIsRed);

        private readonly record struct DirectoryEntryData(
            uint StreamId,
            string Name,
            ushort NameLength,
            CompoundFileObjectType ObjectType,
            CompoundFileNodeColor Color,
            uint LeftSiblingId,
            uint RightSiblingId,
            uint ChildId,
            Guid ClassId,
            uint StateBits,
            long CreationTime,
            long ModifiedTime,
            uint StartingSector,
            ulong StreamSize);
    }
}
