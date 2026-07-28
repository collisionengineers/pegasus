using System.Collections.Immutable;

namespace CollisionDocNet.Writer;

public enum WordPropertyRunKind
{
    Piece,
    Character,
    Paragraph,
    Section,
}

public enum WordSprmMeaning
{
    Unknown,
    ParagraphStyle,
    ParagraphAlignment,
    InTable,
    ListLevel,
    Font,
    Language,
    Bold,
    Italic,
    Strike,
    Hidden,
}

public sealed record WordSprm(
    ushort Opcode,
    WordSprmMeaning Meaning,
    ImmutableArray<byte> Operand,
    int RecordOffset,
    bool IsKnown);

public sealed record WordPropertyRun(
    string StableId,
    WordPropertyRunKind Kind,
    uint CpStart,
    uint CpEnd,
    uint FcStart,
    uint FcEnd,
    string Origin,
    int OriginOffset,
    ushort? StyleIndex,
    ImmutableArray<WordSprm> Sprms);

public enum WordStructureKind
{
    StyleSheet,
    FontTable,
    ListDefinition,
    Section,
    Field,
    Bookmark,
    HeaderFooter,
    Footnote,
    Endnote,
    Comment,
    Revision,
    Textbox,
    Drawing,
    Form,
    ExternalReference,
    CustomData,
    Settings,
    Signature,
    Unknown,
}

public sealed record WordStructureRecord(
    string StableId,
    WordStructureKind Kind,
    string OriginStream,
    int FibRangeIndex,
    uint Offset,
    uint Length,
    uint? CpStart,
    uint? CpEnd,
    ImmutableArray<byte> RecordBytes,
    bool SemanticallyDecoded);

public enum WordPassiveAssetKind
{
    PictureData,
    OleObject,
    EmbeddedPackage,
    MacroProject,
    OfficeForm,
    DrawingData,
    CustomData,
    PropertySet,
    UnknownStream,
}

public sealed record WordPassiveAsset(
    string StableId,
    WordPassiveAssetKind Kind,
    string SourceName,
    uint StreamId,
    ulong Length,
    string Sha256,
    Guid ClassId,
    uint? ParentStreamId,
    uint OwningStorageStreamId,
    string SourcePath,
    ImmutableArray<byte> Content);

public sealed record WordMetadataProperty(
    string StableId,
    string PropertySet,
    uint PropertyId,
    string Name,
    string Value,
    int Offset);
