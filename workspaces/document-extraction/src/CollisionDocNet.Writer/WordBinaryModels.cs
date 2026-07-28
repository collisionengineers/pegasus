using System.Collections.Immutable;
using System.Text;

namespace CollisionDocNet.Writer;

public enum WordBinaryOutcome
{
    Complete = 0,
    Partial,
    Encrypted,
    Corrupt,
    UnsupportedFormat,
    UnsupportedFeature,
    ResourceLimitExceeded,
    Cancelled,
    TimedOut,
    TechnicalFailure,
}

public enum WordStoryKind
{
    Main = 0,
    Footnote,
    Header,
    Macro,
    Annotation,
    Endnote,
    Textbox,
    HeaderTextbox,
}

public enum WordTextSegmentKind
{
    Text = 0,
    Picture,
    FootnoteOrEndnoteReference,
    CellOrRowMark,
    Tab,
    LineBreak,
    PageOrSectionBreak,
    ParagraphMark,
    FieldBegin,
    FieldSeparator,
    FieldEnd,
    NonBreakingHyphen,
    OptionalHyphen,
    EmbeddedObjectMarker,
    UnsupportedControl,
}

public sealed record WordBinaryIssue(string Code, string Message, long? Offset = null);

public sealed record WordFibRange(int Index, uint Offset, uint Length)
{
    // FibRgFcLcb97 entry 87 is dwLowDateTime/dwHighDateTime, a FILETIME
    // value rather than an fc/lcb offset-length pair (MS-DOC 2.5.6).
    public bool IsOffsetLengthPair => Index != 87;
}

public sealed record WordFib(
    ushort FibBaseVersion,
    ushort EffectiveVersion,
    ushort LanguageId,
    ushort CharacterSet,
    bool IsComplex,
    bool HasPictures,
    bool IsEncrypted,
    bool IsObfuscated,
    bool UsesOneTable,
    short NextFibPage,
    uint EncryptionKey,
    uint FileCharacterStart,
    uint FileCharacterEnd,
    uint ClxOffset,
    uint ClxLength,
    ImmutableArray<uint> StoryLengths,
    ImmutableArray<WordFibRange> RangeCatalogue);

public sealed record WordPiece(
    int Index,
    uint CpStart,
    uint CpEnd,
    uint FileOffset,
    uint ByteLength,
    bool IsUnicode,
    ushort PropertyModifier);

public sealed record WordTextSegment(
    WordTextSegmentKind Kind,
    string Text,
    uint GlobalCpStart,
    uint GlobalCpEnd,
    uint StoryCpStart,
    uint StoryCpEnd,
    uint FileOffset,
    uint ByteLength,
    int PieceIndex);

public sealed record WordStory(
    WordStoryKind Kind,
    uint GlobalCpStart,
    uint GlobalCpEnd,
    ImmutableArray<WordTextSegment> Segments)
{
    public string Text
    {
        get
        {
            var builder = new StringBuilder();
            foreach (WordTextSegment segment in Segments)
            {
                builder.Append(segment.Text);
            }

            return builder.ToString();
        }
    }
}

public sealed record WordBinaryExtractionResult(
    WordBinaryOutcome Outcome,
    string DetectedFamily,
    string? SelectedTableStream,
    WordFib? Fib,
    ImmutableArray<WordPiece> Pieces,
    ImmutableArray<WordStory> Stories,
    ImmutableArray<WordBinaryIssue> Issues)
{
    public bool IsComplete => Outcome == WordBinaryOutcome.Complete;

    public ImmutableArray<WordPropertyRun> PropertyRuns { get; init; } = [];

    public ImmutableArray<WordStructureRecord> Structures { get; init; } = [];

    public ImmutableArray<WordPassiveAsset> PassiveAssets { get; init; } = [];

    public ImmutableArray<WordMetadataProperty> Metadata { get; init; } = [];
}
