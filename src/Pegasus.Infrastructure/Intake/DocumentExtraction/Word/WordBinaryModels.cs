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

/// <summary>
/// What <see cref="WordBinaryOutcome.Partial"/> is allowed to mean, and only
/// that: visible document text may be missing, truncated or unattributable.
///
/// Every condition the extractor meets is still recorded as an issue so a
/// reviewer can see it. Only the content-loss class degrades the outcome. A
/// style sheet, a font table, a document-properties range, a fast-save flag, a
/// header story that WAS decoded, a passively recorded embedded-object marker
/// and a stray control byte all remove no text, so a read carrying nothing but
/// those is complete and has to be reported as complete — a legacy Word file
/// saved by real Word carries several of them every time.
/// </summary>
internal static class WordBinaryIssueClassification
{
    internal const string ControlSemanticCodePrefix = "doc-control-semantic-partial";

    /// <summary>
    /// One issue code per control-marker kind, so a classification decision is
    /// made per kind rather than for the whole family at once.
    /// </summary>
    internal static string ControlCode(WordTextSegmentKind kind) =>
        $"{ControlSemanticCodePrefix}:{kind}";

    /// <summary>
    /// The conditions that record a structure this extractor deliberately does
    /// not open, or a semantic anchor it does not resolve, while every
    /// character of visible text was still decoded. Anything absent from this
    /// list counts as content loss, so a condition added later degrades the
    /// outcome until somebody classifies it deliberately.
    /// </summary>
    private static readonly HashSet<string> InformationalCodes = new(StringComparer.Ordinal)
    {
        // A CLX property record carries formatting; it removes no characters.
        "doc-clx-prc-unapplied",
        // Header, footnote, textbox and annotation text IS decoded; the reader
        // emits each such story as its own labelled fragment.
        "doc-secondary-story-unanchored",
        // Style sheets, font tables, section descriptors and document
        // properties: metadata ranges every genuine Word 97 file carries.
        "doc-fib-ranges-unprocessed",
        // fComplex records fast-saved (incremental) status, not the presence of
        // a piece table. A clean single save writes a CLX with the flag unset.
        "doc-complex-flag-unset",
        // Pictures and embedded or active-content storages are never opened, by
        // design (ADR-0025, passive extraction). They are not document text.
        "doc-pictures-unprocessed",
        "doc-active-or-embedded-storage-passive",
        ControlCode(WordTextSegmentKind.Picture),
        ControlCode(WordTextSegmentKind.EmbeddedObjectMarker),
        // Cell and row marks are modelled as table cells beside the flattened
        // text, so the marker is structure the reader now reports.
        ControlCode(WordTextSegmentKind.CellOrRowMark),
        // A page or section break projects to a form feed; nothing is dropped.
        ControlCode(WordTextSegmentKind.PageOrSectionBreak),
        // The footnote or endnote text itself is decoded as its own story; only
        // the reference mark's anchor is unresolved.
        ControlCode(WordTextSegmentKind.FootnoteOrEndnoteReference),
        // The field RESULT text is what the reader sees and keeps; the field
        // instruction is formatting, not document text.
        ControlCode(WordTextSegmentKind.FieldBegin),
        ControlCode(WordTextSegmentKind.FieldSeparator),
        ControlCode(WordTextSegmentKind.FieldEnd),
        // A stray low-ASCII control byte carries no printable character; it is
        // stripped from the projected text rather than shown.
        ControlCode(WordTextSegmentKind.UnsupportedControl),
    };

    internal static bool IsContentLoss(WordBinaryIssue issue) =>
        !InformationalCodes.Contains(issue.Code);
}

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
