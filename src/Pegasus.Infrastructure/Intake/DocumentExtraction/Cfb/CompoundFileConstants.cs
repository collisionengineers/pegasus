namespace Pegasus.Infrastructure.Intake.DocumentExtraction.Cfb;

/// <summary>
/// Constants defined by the Compound Binary File format.
/// </summary>
internal static class CompoundFileConstants
{
    public const int HeaderLength = 512;
    public const int Version3SectorSize = 512;
    public const int Version4SectorSize = 4096;
    public const int Version3MinimumFileLength = HeaderLength + (2 * Version3SectorSize);
    public const int Version4MinimumFileLength = 3 * Version4SectorSize;
    public const int DirectoryEntryLength = 128;
    public const int HeaderDifatEntryCount = 109;

    public const uint MaximumRegularSector = 0xFFFFFFFA;
    public const uint NoStream = 0xFFFFFFFF;
    public const uint FreeSector = 0xFFFFFFFF;
    public const uint EndOfChain = 0xFFFFFFFE;
    public const uint FatSector = 0xFFFFFFFD;
    public const uint DifatSector = 0xFFFFFFFC;
}
