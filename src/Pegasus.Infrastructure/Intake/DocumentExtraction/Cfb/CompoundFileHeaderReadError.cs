namespace Pegasus.Infrastructure.Intake.DocumentExtraction.Cfb;

internal enum CompoundFileHeaderReadError
{
    Uninitialized = 0,
    None,
    HeaderTooShort,
    FileTooSmall,
    FileLengthNotSectorAligned,
    InvalidSignature,
    NonZeroClassIdentifier,
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
