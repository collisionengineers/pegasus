using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Pegasus.Infrastructure.Intake.DocumentExtraction.Word;

internal static class WordPieceTableParser
{
    internal static bool TryRead(
        ReadOnlySpan<byte> tableStream,
        ReadOnlySpan<byte> wordDocument,
        WordFib fib,
        WordBinaryExtractionLimits limits,
        CancellationToken cancellationToken,
        out ImmutableArray<WordPiece> pieces,
        out ImmutableArray<WordBinaryIssue> issues)
    {
        pieces = [];
        var issueBuilder = ImmutableArray.CreateBuilder<WordBinaryIssue>();
        issues = [];
        if (fib.ClxLength == 0)
        {
            issues = [new("doc-clx-missing", "The declared subset requires a CLX piece table; simple-file fallback is not implemented.")];
            return false;
        }

        if (!RangeFits(fib.ClxOffset, fib.ClxLength, tableStream.Length))
        {
            issues = [new("doc-clx-range", "The CLX range is outside the selected table stream.", fib.ClxOffset)];
            return false;
        }

        // EXT-DOC-003: text must lie within both the physical stream and the
        // FIB-declared cbMac extent (MS-DOC 2.5.3).
        int textBound = (int)Math.Min(fib.ByteCountLimit, (uint)wordDocument.Length);
        int cursor = checked((int)fib.ClxOffset);
        int end = checked(cursor + (int)fib.ClxLength);
        while (cursor < end)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte clxt = tableStream[cursor++];
            if (clxt == 0x01)
            {
                if (cursor > end - sizeof(ushort))
                {
                    issues = [new("doc-clx-prc-header", "A CLX property record header is truncated.", cursor - 1)];
                    return false;
                }

                ushort length = U16(tableStream, cursor);
                cursor += sizeof(ushort);
                if (cursor > end - length)
                {
                    issues = [new("doc-clx-prc-range", "A CLX property record exceeds the declared CLX range.", cursor - 3)];
                    return false;
                }

                cursor += length;
                issueBuilder.Add(new("doc-clx-prc-unapplied", "A CLX property record was retained as an unsupported formatting branch.", cursor - length));
                continue;
            }

            if (clxt != 0x02)
            {
                issues = [new("doc-clx-record-type", "The CLX contains an invalid record type.", cursor - 1)];
                return false;
            }

            if (cursor > end - sizeof(uint))
            {
                issues = [new("doc-pcdt-header", "The Pcdt length is truncated.", cursor - 1)];
                return false;
            }

            uint plcLength = U32(tableStream, cursor);
            cursor += sizeof(uint);
            if (plcLength < sizeof(uint) || (plcLength - sizeof(uint)) % 12 != 0 ||
                plcLength > int.MaxValue || cursor > end - (int)plcLength)
            {
                issues = [new("doc-plcpcd-length", "The PlcPcd length is invalid or exceeds the CLX range.", cursor - 4)];
                return false;
            }

            int pieceCount = ((int)plcLength - sizeof(uint)) / 12;
            if (pieceCount == 0)
            {
                issues = [new("doc-piece-count-zero", "The PlcPcd contains no pieces.", cursor)];
                return false;
            }
            if (pieceCount > limits.MaximumPieces)
            {
                issues = [new("doc-piece-count-limit", "The piece count exceeds the configured bound.", cursor)];
                return false;
            }

            int cpArrayOffset = cursor;
            int pcdArrayOffset = checked(cpArrayOffset + ((pieceCount + 1) * sizeof(uint)));
            var builder = ImmutableArray.CreateBuilder<WordPiece>(pieceCount);
            uint previousCp = U32(tableStream, cpArrayOffset);
            if (previousCp != 0)
            {
                issues = [new("doc-piece-first-cp", "The PlcPcd does not begin at global CP zero.", cpArrayOffset)];
                return false;
            }

            for (int index = 0; index < pieceCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                uint nextCp = U32(tableStream, cpArrayOffset + ((index + 1) * sizeof(uint)));
                if (nextCp <= previousCp)
                {
                    issues = [new("doc-piece-cp-order", "Piece CP boundaries are not strictly increasing.", cpArrayOffset + ((index + 1) * 4))];
                    return false;
                }
                if (nextCp > limits.MaximumCharacters)
                {
                    issues = [new("doc-character-count-limit", "The piece-table character extent exceeds the configured bound.", cpArrayOffset + ((index + 1) * 4))];
                    return false;
                }

                int pcdOffset = pcdArrayOffset + (index * 8);
                uint encodedFc = U32(tableStream, pcdOffset + 2);
                if ((encodedFc & 0x80000000u) != 0)
                {
                    issues = [new("doc-piece-fc-reserved", "A piece sets the reserved high FC bit.", pcdOffset + 2)];
                    return false;
                }

                bool unicode = (encodedFc & 0x40000000u) == 0;
                uint fileOffset = unicode ? encodedFc : (encodedFc & 0x3fffffffu) / 2;
                uint characterCount = nextCp - previousCp;
                uint byteLength;
                try
                {
                    byteLength = checked(characterCount * (unicode ? 2u : 1u));
                }
                catch (OverflowException)
                {
                    issues = [new("doc-piece-byte-overflow", "A piece byte length overflows.", pcdOffset + 2)];
                    return false;
                }

                if (!RangeFits(fileOffset, byteLength, textBound))
                {
                    issues = [new("doc-piece-byte-range", "A piece maps outside the WordDocument stream or its declared cbMac extent.", pcdOffset + 2)];
                    return false;
                }

                ushort prm = U16(tableStream, pcdOffset + 6);
                builder.Add(new(index, previousCp, nextCp, fileOffset, byteLength, unicode, prm));
                previousCp = nextCp;
            }

            cursor += (int)plcLength;
            if (cursor != end)
            {
                issues = [new("doc-clx-trailing-data", "The Pcdt is not the final CLX record.", cursor)];
                return false;
            }

            pieces = builder.MoveToImmutable();
            issues = issueBuilder.ToImmutable();
            return true;
        }

        issues = [new("doc-pcdt-missing", "The CLX contains no Pcdt record.", fib.ClxOffset)];
        return false;
    }

    internal static bool RangeFits(uint offset, uint length, int containerLength) =>
        offset <= (uint)containerLength && length <= (uint)containerLength - offset;

    private static ushort U16(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
    private static uint U32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}
