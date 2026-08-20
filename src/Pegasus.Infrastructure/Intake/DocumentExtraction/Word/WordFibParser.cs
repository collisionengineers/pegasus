using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Pegasus.Infrastructure.Intake.DocumentExtraction.Word;

internal static class WordFibParser
{
    private const int FibBaseLength = 32;
    private const int StoryCount = 8;
    private const int ClxPairIndex = 33;
    private static readonly ushort[] SupportedVersions = [0x00c1, 0x00d9, 0x0101, 0x010c, 0x0112];

    internal static bool IsPre97Identifier(ushort identifier) =>
        identifier is 0xa59b or 0xa59c or 0xa5db or 0xa5dc;

    internal static bool TryRead(
        ReadOnlySpan<byte> wordDocument,
        out WordFib? fib,
        out WordBinaryOutcome outcome,
        out WordBinaryIssue? issue)
    {
        fib = null;
        outcome = WordBinaryOutcome.Corrupt;
        issue = null;
        if (wordDocument.Length < FibBaseLength)
        {
            issue = new("doc-fib-truncated", "The WordDocument stream does not contain a complete FIB base.", 0);
            return false;
        }

        ushort identifier = U16(wordDocument, 0);
        if (identifier != 0xa5ec)
        {
            bool pre97 = IsPre97Identifier(identifier);
            outcome = pre97 ? WordBinaryOutcome.UnsupportedFeature : WordBinaryOutcome.UnsupportedFormat;
            issue = new(pre97 ? "doc-pre97-unsupported" : "doc-fib-identifier",
                pre97 ? "A pre-Word-97 binary family was identified and is not implemented." : "The CFB WordDocument stream is not a supported Word binary family.", 0);
            return false;
        }

        ushort fibBaseVersion = U16(wordDocument, 2);
        ushort languageId = U16(wordDocument, 6);
        short nextFibPage = I16(wordDocument, 8);
        ushort flags = U16(wordDocument, 10);
        uint encryptionKey = U32(wordDocument, 14);
        // FibBase bytes 20-31 (reserved3-reserved6, historically chs/fcMin/fcMac)
        // MUST be ignored per MS-DOC 2.5.2; they are neither validated nor used.
        int cursor = FibBaseLength;
        if (!TryReadCountedArray(wordDocument, ref cursor, sizeof(ushort), out _))
        {
            issue = new("doc-fib-rgsw-truncated", "The FIB short-word array is truncated.", cursor);
            return false;
        }

        if (cursor > wordDocument.Length - sizeof(ushort))
        {
            issue = new("doc-fib-cslw-truncated", "The FIB long-word count is missing.", cursor);
            return false;
        }

        ushort longCount = U16(wordDocument, cursor);
        cursor += sizeof(ushort);
        int longBytes;
        try
        {
            longBytes = checked(longCount * sizeof(uint));
        }
        catch (OverflowException)
        {
            issue = new("doc-fib-cslw-overflow", "The FIB long-word array length overflows.", cursor - 2);
            return false;
        }

        if (longCount < 11 || cursor > wordDocument.Length - longBytes)
        {
            issue = new("doc-fib-rglw-truncated", "The FIB long-word array is too short for the story catalogue.", cursor);
            return false;
        }

        // FibRgLw97[0] is cbMac, the count of meaningful bytes in the
        // WordDocument stream; text MUST lie below it (MS-DOC 2.5.3).
        uint byteCountLimit = U32(wordDocument, cursor);
        var storyLengths = ImmutableArray.CreateBuilder<uint>(StoryCount);
        for (int index = 0; index < StoryCount; index++)
        {
            storyLengths.Add(U32(wordDocument, cursor + ((3 + index) * sizeof(uint))));
        }

        cursor += longBytes;
        if (cursor > wordDocument.Length - sizeof(ushort))
        {
            issue = new("doc-fib-rgfclcb-count", "The FIB range-catalogue count is missing.", cursor);
            return false;
        }

        ushort rangeCount = U16(wordDocument, cursor);
        cursor += sizeof(ushort);
        int rangeBytes;
        try
        {
            rangeBytes = checked(rangeCount * 8);
        }
        catch (OverflowException)
        {
            issue = new("doc-fib-rgfclcb-overflow", "The FIB range catalogue length overflows.", cursor - 2);
            return false;
        }

        if (rangeCount <= ClxPairIndex || cursor > wordDocument.Length - rangeBytes)
        {
            issue = new("doc-fib-rgfclcb-truncated", "The FIB range catalogue does not contain the CLX pair.", cursor);
            return false;
        }

        var ranges = ImmutableArray.CreateBuilder<WordFibRange>(rangeCount);
        for (int index = 0; index < rangeCount; index++)
        {
            int pairOffset = cursor + (index * 8);
            ranges.Add(new(index, U32(wordDocument, pairOffset), U32(wordDocument, pairOffset + 4)));
        }

        cursor += rangeBytes;
        ushort effectiveVersion = fibBaseVersion;
        if (cursor <= wordDocument.Length - sizeof(ushort))
        {
            ushort newWordCount = U16(wordDocument, cursor);
            cursor += sizeof(ushort);
            if (newWordCount > 0)
            {
                int newWordBytes = checked(newWordCount * sizeof(ushort));
                if (cursor > wordDocument.Length - newWordBytes)
                {
                    issue = new("doc-fib-rgcswnew-truncated", "The extended FIB word array is truncated.", cursor);
                    return false;
                }

                ushort fibNew = U16(wordDocument, cursor);
                if (fibNew != 0)
                {
                    effectiveVersion = fibNew;
                }
            }
        }

        if (Array.IndexOf(SupportedVersions, effectiveVersion) < 0)
        {
            outcome = WordBinaryOutcome.UnsupportedFeature;
            issue = new("doc-fib-version-unsupported", "The Word 97-family FIB version is identified but not supported.", 2);
            return false;
        }

        WordFibRange clx = ranges[ClxPairIndex];
        fib = new(
            fibBaseVersion,
            effectiveVersion,
            languageId,
            (flags & 0x0004) != 0,
            (flags & 0x0008) != 0,
            (flags & 0x0100) != 0,
            (flags & 0x8000) != 0,
            (flags & 0x0200) != 0,
            nextFibPage,
            encryptionKey,
            byteCountLimit,
            clx.Offset,
            clx.Length,
            storyLengths.MoveToImmutable(),
            ranges.MoveToImmutable());
        outcome = WordBinaryOutcome.Complete;
        return true;
    }

    private static bool TryReadCountedArray(ReadOnlySpan<byte> bytes, ref int cursor, int elementSize, out ushort count)
    {
        count = 0;
        if (cursor > bytes.Length - sizeof(ushort))
        {
            return false;
        }

        count = U16(bytes, cursor);
        cursor += sizeof(ushort);
        int length = checked(count * elementSize);
        if (cursor > bytes.Length - length)
        {
            return false;
        }

        cursor += length;
        return true;
    }

    private static ushort U16(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
    private static short I16(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..]);
    private static uint U32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}
