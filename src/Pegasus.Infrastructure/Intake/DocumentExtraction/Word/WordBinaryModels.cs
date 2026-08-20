using System.Collections.Immutable;
using System.Text;

namespace Pegasus.Infrastructure.Intake.DocumentExtraction.Word;

internal enum WordBinaryOutcome
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

internal enum WordStoryKind
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

internal enum WordTextSegmentKind
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

internal sealed record WordBinaryIssue(string Code, string Message, long? Offset = null);

internal sealed record WordFibRange(int Index, uint Offset, uint Length)
{
    // FibRgFcLcb97 entry 87 is dwLowDateTime/dwHighDateTime, a FILETIME
    // value rather than an fc/lcb offset-length pair (MS-DOC 2.5.6).
    public bool IsOffsetLengthPair => Index != 87;
}

internal sealed record WordFib(
    ushort FibBaseVersion,
    ushort EffectiveVersion,
    ushort LanguageId,
    bool IsComplex,
    bool HasPictures,
    bool IsEncrypted,
    bool IsObfuscated,
    bool UsesOneTable,
    short NextFibPage,
    uint EncryptionKey,
    uint ByteCountLimit,
    uint ClxOffset,
    uint ClxLength,
    ImmutableArray<uint> StoryLengths,
    ImmutableArray<WordFibRange> RangeCatalogue);

internal sealed record WordPiece(
    int Index,
    uint CpStart,
    uint CpEnd,
    uint FileOffset,
    uint ByteLength,
    bool IsUnicode,
    ushort PropertyModifier);

internal sealed record WordTextSegment(
    WordTextSegmentKind Kind,
    string Text,
    uint GlobalCpStart,
    uint GlobalCpEnd,
    uint StoryCpStart,
    uint StoryCpEnd,
    uint FileOffset,
    uint ByteLength,
    int PieceIndex);

internal sealed record WordStory(
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

internal sealed record WordBinaryExtractionResult(
    WordBinaryOutcome Outcome,
    string DetectedFamily,
    string? SelectedTableStream,
    WordFib? Fib,
    ImmutableArray<WordPiece> Pieces,
    ImmutableArray<WordStory> Stories,
    ImmutableArray<WordBinaryIssue> Issues)
{
    public bool IsComplete => Outcome == WordBinaryOutcome.Complete;
}
