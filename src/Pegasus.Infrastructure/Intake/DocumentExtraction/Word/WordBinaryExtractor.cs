using System.Collections.Immutable;
using System.Text;
using Pegasus.Infrastructure.Intake.DocumentExtraction.Cfb;

namespace Pegasus.Infrastructure.Intake.DocumentExtraction.Word;

/// <summary>
/// Extracts the declared Word 97-family FIB/CLX text subset directly from CFB streams.
/// It performs no rendering, conversion, content execution or external retrieval.
/// </summary>
internal static class WordBinaryExtractor
{
    private static readonly byte[] CompoundFileSignature = [0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1];

    private static readonly ImmutableArray<WordStoryKind> StoryOrder =
    [
        WordStoryKind.Main,
        WordStoryKind.Footnote,
        WordStoryKind.Header,
        WordStoryKind.Macro,
        WordStoryKind.Annotation,
        WordStoryKind.Endnote,
        WordStoryKind.Textbox,
        WordStoryKind.HeaderTextbox,
    ];

    public static WordBinaryExtractionResult Extract(
        ReadOnlyMemory<byte> input,
        WordBinaryExtractionLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        limits ??= WordBinaryExtractionLimits.Default;
        if (!HasValidLimits(limits))
        {
            return Failure(WordBinaryOutcome.ResourceLimitExceeded, "unknown", "doc-structured-limit-invalid", "Structured Word extraction limits must be positive.");
        }
        if (limits.MaximumInputBytes < 0 || input.Length > limits.MaximumInputBytes)
        {
            return Failure(WordBinaryOutcome.ResourceLimitExceeded, "unknown", "doc-input-limit", "The input exceeds the configured Word binary limit.");
        }

        if (!input.Span.StartsWith(CompoundFileSignature))
        {
            return Failure(WordBinaryOutcome.UnsupportedFormat, ClassifyNonCfb(input.Span), "doc-not-cfb", "The input is not a Compound Binary File.");
        }

        CompoundFileReadLimits cfbLimits = limits.CompoundFile with
        {
            MaximumInputBytes = Math.Min(limits.MaximumInputBytes, limits.CompoundFile.MaximumInputBytes),
        };
        CompoundFileReadResult cfb = CompoundFileReader.Read(input, cfbLimits, cancellationToken);
        if (!cfb.IsSuccess)
        {
            WordBinaryOutcome outcome = cfb.Error switch
            {
                CompoundFileReadError.Cancelled => WordBinaryOutcome.Cancelled,
                CompoundFileReadError.InputLimitExceeded or
                CompoundFileReadError.SectorCountLimitExceeded or
                CompoundFileReadError.DirectoryEntryLimitExceeded or
                CompoundFileReadError.StreamLimitExceeded or
                CompoundFileReadError.TotalStreamLimitExceeded => WordBinaryOutcome.ResourceLimitExceeded,
                _ => WordBinaryOutcome.Corrupt,
            };
            return Failure(outcome, "compound-file", "doc-cfb-invalid", $"The CFB container could not be read ({cfb.Error}).", cfb.Location);
        }

        return ExtractParsed(cfb.File!, limits, cancellationToken);
    }

    internal static WordBinaryExtractionResult Extract(
        CompoundFile compoundFile,
        WordBinaryExtractionLimits? limits = null,
        CancellationToken cancellationToken = default) =>
        ExtractParsed(compoundFile, limits ?? WordBinaryExtractionLimits.Default, cancellationToken);

    private static WordBinaryExtractionResult ExtractParsed(
        CompoundFile compoundFile,
        WordBinaryExtractionLimits limits,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(compoundFile);
        if (!HasValidLimits(limits))
        {
            return Failure(WordBinaryOutcome.ResourceLimitExceeded, "word-binary", "doc-structured-limit-invalid", "Structured Word extraction limits must be positive.");
        }
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            CompoundFileDirectoryEntry? wordEntry = FindRootStream(compoundFile, "WordDocument");
            if (wordEntry is null)
            {
                return Failure(WordBinaryOutcome.UnsupportedFormat, "compound-file", "doc-word-stream-missing", "The CFB does not contain a root WordDocument stream.");
            }

            ReadOnlySpan<byte> wordDocument = wordEntry.Content.AsSpan();
            if (!WordFibParser.TryRead(wordDocument, out WordFib? fib, out WordBinaryOutcome fibOutcome, out WordBinaryIssue? fibIssue))
            {
                return Failure(fibOutcome, fibOutcome == WordBinaryOutcome.UnsupportedFeature ? "word-binary-pre97-or-version" : "compound-file", fibIssue!);
            }

            string selectedName = fib!.UsesOneTable ? "1Table" : "0Table";
            CompoundFileDirectoryEntry? tableEntry = FindRootStream(compoundFile, selectedName);
            if (tableEntry is null)
            {
                return Failure(WordBinaryOutcome.Corrupt, "word-binary", "doc-table-stream-missing", "The table stream selected by the FIB is missing.");
            }

            if (fib.IsEncrypted)
            {
                string encryption = fib.IsObfuscated ? "xor-obfuscation" : fib.EncryptionKey == 0 ? "binary-rc4" : "rc4-cryptoapi-or-binary";
                return new(
                    WordBinaryOutcome.Encrypted,
                    "word-binary",
                    selectedName,
                    fib,
                    [],
                    [],
                    [new("doc-encrypted", $"Encrypted Word binary content was classified as {encryption}; decryption was not attempted.")]);
            }

            ReadOnlySpan<byte> table = tableEntry.Content.AsSpan();
            foreach (WordFibRange range in fib.RangeCatalogue)
            {
                if (range.IsOffsetLengthPair && range.Length != 0 &&
                    !WordPieceTableParser.RangeFits(range.Offset, range.Length, table.Length))
                {
                    return Failure(WordBinaryOutcome.Corrupt, "word-binary", "doc-fib-range-outside-table", "A non-empty FIB range lies outside the selected table stream.", range.Offset, selectedName, fib);
                }
            }

            if (!WordPieceTableParser.TryRead(table, wordDocument, fib, limits, cancellationToken,
                    out ImmutableArray<WordPiece> pieces, out ImmutableArray<WordBinaryIssue> pieceIssues))
            {
                WordBinaryOutcome pieceOutcome = HasResourceLimitIssue(pieceIssues)
                    ? WordBinaryOutcome.ResourceLimitExceeded
                    : WordBinaryOutcome.Corrupt;
                return new(pieceOutcome, "word-binary", selectedName, fib, [], [], pieceIssues);
            }

            uint declaredCharacterCount;
            try
            {
                declaredCharacterCount = 0;
                foreach (uint length in fib.StoryLengths)
                {
                    declaredCharacterCount = checked(declaredCharacterCount + length);
                }

                // PlcPcd includes one final guard CP after the last
                // specialised document part when any such part exists
                // (MS-DOC 2.8.35); it belongs to no story.
                if (HasSpecializedStory(fib))
                {
                    declaredCharacterCount = checked(declaredCharacterCount + 1);
                }
            }
            catch (OverflowException)
            {
                return Failure(WordBinaryOutcome.Corrupt, "word-binary", "doc-story-length-overflow", "The sum of FIB story lengths overflows.", null, selectedName, fib);
            }

            if (pieces[^1].CpEnd != declaredCharacterCount)
            {
                return Failure(WordBinaryOutcome.Corrupt, "word-binary", "doc-story-piece-mismatch", "The PlcPcd CP extent does not equal the declared story catalogue.", null, selectedName, fib);
            }

            var issues = pieceIssues.ToBuilder();
            ImmutableArray<WordStory> stories = BuildStories(wordDocument, fib, pieces, issues, cancellationToken);
            AddUnsupportedBranchIssues(compoundFile, fib, issues);
            // Partial is a claim that visible text may be missing, so it is
            // driven by the content-loss class alone. Informational issues stay
            // on the result for review without degrading the outcome.
            WordBinaryOutcome outcome = HasResourceLimitIssue(issues)
                ? WordBinaryOutcome.ResourceLimitExceeded
                : HasContentLossIssue(issues) ? WordBinaryOutcome.Partial : WordBinaryOutcome.Complete;
            return new(outcome, "word-binary", selectedName, fib, pieces, stories, issues.ToImmutable());
        }
        catch (OperationCanceledException)
        {
            return Failure(WordBinaryOutcome.Cancelled, "word-binary", "doc-cancelled", "Word binary extraction was cancelled.");
        }
        catch (OverflowException)
        {
            return Failure(WordBinaryOutcome.Corrupt, "word-binary", "doc-arithmetic-overflow", "Checked Word binary offset arithmetic overflowed.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Failure(WordBinaryOutcome.TechnicalFailure, "word-binary", "doc-technical-failure", "Word binary extraction failed unexpectedly without exposing input content.");
        }
    }

    private static ImmutableArray<WordStory> BuildStories(
        ReadOnlySpan<byte> wordDocument,
        WordFib fib,
        ImmutableArray<WordPiece> pieces,
        ImmutableArray<WordBinaryIssue>.Builder issues,
        CancellationToken cancellationToken)
    {
        var storyBuilder = ImmutableArray.CreateBuilder<WordStory>(StoryOrder.Length);
        uint globalStart = 0;
        for (int storyIndex = 0; storyIndex < StoryOrder.Length; storyIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            uint storyLength = fib.StoryLengths[storyIndex];
            uint globalEnd = checked(globalStart + storyLength);
            var segments = ImmutableArray.CreateBuilder<WordTextSegment>();
            if (storyLength != 0)
            {
                DecodeStory(wordDocument, pieces, StoryOrder[storyIndex], globalStart, globalEnd, segments, issues, cancellationToken);
                if (StoryOrder[storyIndex] != WordStoryKind.Main)
                {
                    issues.Add(new("doc-secondary-story-unanchored", $"The {StoryOrder[storyIndex]} story text was decoded, but its semantic anchors are outside this vertical slice."));
                }
            }

            storyBuilder.Add(new(StoryOrder[storyIndex], globalStart, globalEnd, segments.ToImmutable()));
            globalStart = globalEnd;
        }

        return storyBuilder.MoveToImmutable();
    }

    private static bool HasSpecializedStory(WordFib fib)
    {
        for (int index = 1; index < fib.StoryLengths.Length; index++)
        {
            if (fib.StoryLengths[index] != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void DecodeStory(
        ReadOnlySpan<byte> wordDocument,
        ImmutableArray<WordPiece> pieces,
        WordStoryKind storyKind,
        uint storyStart,
        uint storyEnd,
        ImmutableArray<WordTextSegment>.Builder segments,
        ImmutableArray<WordBinaryIssue>.Builder issues,
        CancellationToken cancellationToken)
    {
        bool codePageIssueReported = false;
        bool surrogateIssueReported = false;
        uint reportedControlKinds = 0;
        foreach (WordPiece piece in pieces)
        {
            uint cpStart = Math.Max(piece.CpStart, storyStart);
            uint cpEnd = Math.Min(piece.CpEnd, storyEnd);
            if (cpStart >= cpEnd)
            {
                continue;
            }

            uint unitSize = piece.IsUnicode ? 2u : 1u;
            uint fileStart = checked(piece.FileOffset + checked((cpStart - piece.CpStart) * unitSize));
            var text = new StringBuilder();
            uint runCpStart = cpStart;
            uint runFileStart = fileStart;
            for (uint cp = cpStart; cp < cpEnd; cp++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                uint fileOffset = checked(fileStart + checked((cp - cpStart) * unitSize));
                char value = DecodeCodeUnit(wordDocument, fileOffset, piece.IsUnicode, out bool unsupportedCodePage);
                if (unsupportedCodePage && !codePageIssueReported)
                {
                    issues.Add(new("doc-codepage-unsupported", "A compressed byte has no Windows-1252 mapping and was replaced.", fileOffset));
                    codePageIssueReported = true;
                }

                WordTextSegmentKind kind = ClassifyControl(value);
                if (kind == WordTextSegmentKind.Text)
                {
                    text.Append(value);
                    continue;
                }

                FlushText(text, runCpStart, cp, storyStart, runFileStart, fileOffset - runFileStart, piece.Index, segments, issues, ref surrogateIssueReported);
                string projection = ControlProjection(kind);
                segments.Add(new(kind, projection, cp, cp + 1, cp - storyStart, cp + 1 - storyStart, fileOffset, unitSize, piece.Index));
                if (kind is WordTextSegmentKind.Picture or WordTextSegmentKind.EmbeddedObjectMarker or WordTextSegmentKind.CellOrRowMark or
                    WordTextSegmentKind.PageOrSectionBreak or WordTextSegmentKind.UnsupportedControl or
                    WordTextSegmentKind.FieldBegin or WordTextSegmentKind.FieldSeparator or WordTextSegmentKind.FieldEnd or
                    WordTextSegmentKind.FootnoteOrEndnoteReference)
                {
                    uint kindBit = 1u << (int)kind;
                    if ((reportedControlKinds & kindBit) == 0)
                    {
                        issues.Add(new(
                            WordBinaryIssueClassification.ControlCode(kind),
                            $"A {kind} marker in the {storyKind} story was recorded rather than resolved to structure outside this text vertical slice.",
                            fileOffset));
                        reportedControlKinds |= kindBit;
                    }
                }

                runCpStart = cp + 1;
                runFileStart = checked(fileOffset + unitSize);
            }

            uint finalFileEnd = checked(fileStart + checked((cpEnd - cpStart) * unitSize));
            FlushText(text, runCpStart, cpEnd, storyStart, runFileStart, finalFileEnd - runFileStart, piece.Index, segments, issues, ref surrogateIssueReported);
        }
    }

    private static void FlushText(
        StringBuilder text,
        uint cpStart,
        uint cpEnd,
        uint storyStart,
        uint fileStart,
        uint byteLength,
        int pieceIndex,
        ImmutableArray<WordTextSegment>.Builder segments,
        ImmutableArray<WordBinaryIssue>.Builder issues,
        ref bool surrogateIssueReported)
    {
        if (text.Length == 0)
        {
            return;
        }

        string sanitized = TextSanitation.ReplaceLoneSurrogates(text.ToString(), out bool replaced);
        if (replaced && !surrogateIssueReported)
        {
            issues.Add(new("doc-lone-surrogate-replaced", "An unpaired UTF-16 surrogate was replaced in decoded text.", fileStart));
            surrogateIssueReported = true;
        }

        segments.Add(new(WordTextSegmentKind.Text, sanitized, cpStart, cpEnd,
            cpStart - storyStart, cpEnd - storyStart, fileStart, byteLength, pieceIndex));
        text.Clear();
    }

    private static char DecodeCodeUnit(ReadOnlySpan<byte> bytes, uint offset, bool unicode, out bool unsupportedCodePage)
    {
        unsupportedCodePage = false;
        if (unicode)
        {
            return (char)(bytes[(int)offset] | (bytes[(int)offset + 1] << 8));
        }

        // Compressed piece bytes map through Windows-1252 with the fixed
        // 0x80-0x9F substitution table, unconditionally (MS-DOC 2.9.73);
        // FibBase byte 20 is reserved and never selects another code page.
        byte value = bytes[(int)offset];
        if (value < 0x80 || value >= 0xa0)
        {
            return (char)value;
        }

        return value switch
        {
            0x80 => '\u20ac',
            0x82 => '\u201a',
            0x83 => '\u0192',
            0x84 => '\u201e',
            0x85 => '\u2026',
            0x86 => '\u2020',
            0x87 => '\u2021',
            0x88 => '\u02c6',
            0x89 => '\u2030',
            0x8a => '\u0160',
            0x8b => '\u2039',
            0x8c => '\u0152',
            0x8e => '\u017d',
            0x91 => '\u2018',
            0x92 => '\u2019',
            0x93 => '\u201c',
            0x94 => '\u201d',
            0x95 => '\u2022',
            0x96 => '\u2013',
            0x97 => '\u2014',
            0x98 => '\u02dc',
            0x99 => '\u2122',
            0x9a => '\u0161',
            0x9b => '\u203a',
            0x9c => '\u0153',
            0x9e => '\u017e',
            0x9f => '\u0178',
            _ => MarkUnsupported(out unsupportedCodePage),
        };
    }

    private static char MarkUnsupported(out bool unsupported)
    {
        unsupported = true;
        return '\ufffd';
    }

    private static WordTextSegmentKind ClassifyControl(char value) => value switch
    {
        '\u0001' => WordTextSegmentKind.Picture,
        '\u0002' => WordTextSegmentKind.FootnoteOrEndnoteReference,
        '\u0007' => WordTextSegmentKind.CellOrRowMark,
        '\u0008' => WordTextSegmentKind.EmbeddedObjectMarker,
        '\u0009' => WordTextSegmentKind.Tab,
        '\u000b' => WordTextSegmentKind.LineBreak,
        '\u000c' => WordTextSegmentKind.PageOrSectionBreak,
        '\u000d' => WordTextSegmentKind.ParagraphMark,
        '\u0013' => WordTextSegmentKind.FieldBegin,
        '\u0014' => WordTextSegmentKind.FieldSeparator,
        '\u0015' => WordTextSegmentKind.FieldEnd,
        '\u001e' => WordTextSegmentKind.NonBreakingHyphen,
        '\u001f' => WordTextSegmentKind.OptionalHyphen,
        < '\u0020' => WordTextSegmentKind.UnsupportedControl,
        _ => WordTextSegmentKind.Text,
    };

    private static string ControlProjection(WordTextSegmentKind kind) => kind switch
    {
        WordTextSegmentKind.Tab => "\t",
        WordTextSegmentKind.LineBreak or WordTextSegmentKind.ParagraphMark => "\n",
        WordTextSegmentKind.PageOrSectionBreak => "\f",
        WordTextSegmentKind.CellOrRowMark => "\t",
        WordTextSegmentKind.NonBreakingHyphen => "\u2011",
        WordTextSegmentKind.OptionalHyphen => "\u00ad",
        // A stray low-ASCII control byte prints nothing in Word and would only
        // corrupt a value it lands inside, so it is stripped rather than shown.
        _ => string.Empty,
    };

    private static void AddUnsupportedBranchIssues(
        CompoundFile compoundFile,
        WordFib fib,
        ImmutableArray<WordBinaryIssue>.Builder issues)
    {
        int unprocessedRanges = 0;
        foreach (WordFibRange range in fib.RangeCatalogue)
        {
            if (range.IsOffsetLengthPair && range.Index != 33 && range.Length != 0)
            {
                unprocessedRanges++;
            }
        }

        if (unprocessedRanges != 0)
        {
            issues.Add(new("doc-fib-ranges-unprocessed", $"{unprocessedRanges} non-CLX FIB range(s) - style sheet, fonts, section descriptors, document properties and the like - are outside this text vertical slice and carry no document text."));
        }

        if (fib.NextFibPage != 0)
        {
            issues.Add(new("doc-secondary-fib-unprocessed", "The FIB points to secondary FIB/AutoText data that was not traversed."));
        }

        if (!fib.IsComplex)
        {
            issues.Add(new("doc-complex-flag-unset", "A CLX was parsed with the FIB complex-file flag unset; the flag records fast-saved status rather than the presence of a piece table, so no text is affected."));
        }

        if (fib.HasPictures)
        {
            issues.Add(new("doc-pictures-unprocessed", "The FIB declares picture content outside this text vertical slice."));
        }

        foreach (CompoundFileDirectoryEntry entry in compoundFile.DirectoryEntries)
        {
            if (entry.ParentStreamId == 0 && entry.ObjectType == CompoundFileObjectType.Storage &&
                (entry.Name.Equals("ObjectPool", StringComparison.Ordinal) ||
                 entry.Name.Equals("Macros", StringComparison.Ordinal) ||
                 entry.Name.Equals("VBA", StringComparison.Ordinal)))
            {
                issues.Add(new("doc-active-or-embedded-storage-passive", "An embedded or active-content storage was detected but never opened or executed."));
            }
        }
    }

    private static CompoundFileDirectoryEntry? FindRootStream(CompoundFile compoundFile, string name)
    {
        foreach (CompoundFileDirectoryEntry entry in compoundFile.DirectoryEntries)
        {
            if (entry.ParentStreamId == 0 && entry.ObjectType == CompoundFileObjectType.Stream &&
                entry.Name.Equals(name, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return null;
    }

    private static bool HasContentLossIssue(IEnumerable<WordBinaryIssue> issues)
    {
        foreach (WordBinaryIssue issue in issues)
        {
            if (WordBinaryIssueClassification.IsContentLoss(issue))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasResourceLimitIssue(IEnumerable<WordBinaryIssue> issues)
    {
        foreach (WordBinaryIssue issue in issues)
        {
            if (issue.Code.EndsWith("-limit", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasValidLimits(WordBinaryExtractionLimits limits) =>
        limits.MaximumInputBytes >= 0 && limits.MaximumCharacters > 0 && limits.MaximumPieces > 0;

    private static string ClassifyNonCfb(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith("{\\rtf"u8)) return "rtf";
        if (bytes.StartsWith("%PDF-"u8)) return "pdf";
        if (bytes.StartsWith("PK\x03\x04"u8)) return "zip-or-ooxml";
        ReadOnlySpan<byte> prefix = bytes[..Math.Min(bytes.Length, 64)];
        while (!prefix.IsEmpty && prefix[0] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            prefix = prefix[1..];
        }
        if (prefix.StartsWith("<html"u8) || prefix.StartsWith("<!DOCTYPE html"u8)) return "html";
        return "unknown";
    }

    private static WordBinaryExtractionResult Failure(
        WordBinaryOutcome outcome,
        string family,
        string code,
        string message,
        long? offset = null,
        string? table = null,
        WordFib? fib = null) =>
        new(outcome, family, table, fib, [], [], [new(code, message, offset)]);

    private static WordBinaryExtractionResult Failure(WordBinaryOutcome outcome, string family, WordBinaryIssue issue) =>
        new(outcome, family, null, null, [], [], [issue]);
}
