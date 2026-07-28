namespace CollisionDocNet.Storage.CompoundFile;

public enum CompoundFileHeaderReadError
{
    Uninitialized = 0,
    None,
    HeaderTooShort,
    FileTooSmall,
    FileLengthNotSectorAligned,
    InvalidSignature,
    NonZeroClassIdentifier,
    UnsupportedMinorVersion,
    UnsupportedMajorVersion,
    InvalidByteOrder,
    InvalidSectorShift,
    InvalidMiniSectorShift,
    NonZeroReservedBytes,
    InvalidVersion3DirectorySectorCount,
    InvalidVersion4DirectorySectorCount,
    NonZeroVersion4HeaderPadding,
    InvalidMiniStreamCutoff,
}
