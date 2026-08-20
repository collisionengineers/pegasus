using System.Collections.Immutable;

namespace Pegasus.Infrastructure.Intake.DocumentExtraction.Cfb;

internal enum CompoundFileObjectType : byte
{
    Unallocated = 0,
    Storage = 1,
    Stream = 2,
    RootStorage = 5,
}

internal enum CompoundFileNodeColor : byte
{
    Red = 0,
    Black = 1,
}

internal sealed record CompoundFileDirectoryEntry(
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
    ulong StreamSize,
    uint? ParentStreamId,
    ImmutableArray<byte> Content);
