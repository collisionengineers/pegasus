using System.Collections.Immutable;

namespace Pegasus.Infrastructure.Intake.DocumentExtraction.Cfb;

internal sealed record CompoundFile(
    CompoundFileHeader Header,
    ImmutableArray<uint> FatSectorIds,
    ImmutableArray<uint> Fat,
    ImmutableArray<uint> MiniFat,
    ImmutableArray<CompoundFileDirectoryEntry> DirectoryEntries);
