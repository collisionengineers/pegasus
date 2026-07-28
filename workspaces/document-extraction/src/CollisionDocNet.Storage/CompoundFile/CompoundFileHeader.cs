using System.Collections.Immutable;

namespace CollisionDocNet.Storage.CompoundFile;

/// <summary>
/// The decoded 512-byte header of a version 3 or version 4 Compound Binary File.
/// Sector chains have not yet been validated when this value is returned.
/// </summary>
public sealed record CompoundFileHeader(
    ushort MinorVersion,
    ushort MajorVersion,
    int SectorSize,
    int MiniSectorSize,
    uint DirectorySectorCount,
    uint FatSectorCount,
    uint FirstDirectorySector,
    uint TransactionSignature,
    uint MiniStreamCutoff,
    uint FirstMiniFatSector,
    uint MiniFatSectorCount,
    uint FirstDifatSector,
    uint DifatSectorCount,
    ImmutableArray<uint> HeaderDifat);
