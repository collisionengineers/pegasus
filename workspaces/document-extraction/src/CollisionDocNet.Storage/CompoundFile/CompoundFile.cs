using System.Collections.Immutable;

namespace CollisionDocNet.Storage.CompoundFile;

public sealed record CompoundFile(
    CompoundFileHeader Header,
    ImmutableArray<uint> FatSectorIds,
    ImmutableArray<uint> Fat,
    ImmutableArray<uint> MiniFat,
    ImmutableArray<CompoundFileDirectoryEntry> DirectoryEntries);
