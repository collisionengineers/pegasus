using Xunit;
using System.Buffers.Binary;
using System.Text;
using Pegasus.Infrastructure.Intake.DocumentExtraction.Msg;

namespace Pegasus.IntegrationTests.DocumentExtraction;

public sealed class RtfCompressionTests
{
    [Fact]
    public void TryDecompressMelaPayloadReturnsExactBytes()
    {
        byte[] raw = Encoding.ASCII.GetBytes("{\\rtf1 Synthetic}");
        byte[] input = CreateMela(raw);

        bool success = RtfCompression.TryDecompress(input, 1024, out byte[] output, out string? error);

        Assert.True(success, error);
        Assert.Equal(raw, output);
    }

    [Fact]
    public void TryDecompressLzFuLiteralTokensReturnsExactBytes()
    {
        byte[] raw = Encoding.ASCII.GetBytes("12345678");
        byte[] payload = [0, .. raw];
        byte[] input = CreateContainer(raw.Length, 0x75465A4C, payload);

        bool success = RtfCompression.TryDecompress(input, 1024, out byte[] output, out string? error);

        Assert.True(success, error);
        Assert.Equal(raw, output);
    }

    [Fact]
    public void TryDecompressCrcMismatchRejectsPayload()
    {
        byte[] input = CreateMela(Encoding.ASCII.GetBytes("data"));
        input[^1] ^= 1;

        bool success = RtfCompression.TryDecompress(input, 1024, out byte[] output, out string? error);

        Assert.False(success);
        Assert.Empty(output);
        Assert.NotNull(error);
        Assert.Contains("CRC", error);
    }

    [Fact]
    public void TryDecompressDeclaredOutputExceedsBoundRejectsBeforeAllocation()
    {
        byte[] input = CreateMela(Encoding.ASCII.GetBytes("bounded"));

        bool success = RtfCompression.TryDecompress(input, 2, out byte[] output, out string? error);

        Assert.False(success);
        Assert.Empty(output);
        Assert.NotNull(error);
        Assert.Contains("bound", error);
    }

    [Fact]
    public void TryDecompressLzFuOverlappingBackReferenceCopiesFromSlidingDictionary()
    {
        const int initialDictionaryLength = 207;
        ushort token = (ushort)((initialDictionaryLength << 4) | 3);
        byte[] payload = [0b0000_0010, (byte)'A', (byte)(token >> 8), (byte)token];
        byte[] input = CreateContainer(6, 0x75465A4C, payload);

        bool success = RtfCompression.TryDecompress(input, 64, out byte[] output, out string? error);

        Assert.True(success, error);
        Assert.Equal(Encoding.ASCII.GetBytes("AAAAAA"), output);
    }

    [Fact]
    public void TryDecompressLzFuTruncatedBackReferenceIsRejected()
    {
        byte[] input = CreateContainer(2, 0x75465A4C, [1, 0]);

        bool success = RtfCompression.TryDecompress(input, 64, out byte[] output, out string? error);

        Assert.False(success);
        Assert.Empty(output);
        Assert.Contains("truncated", error!);
    }

    [Fact]
    public void TryDecompressLzFuBackReferenceWrapsDictionaryAt4096Bytes()
    {
        byte[] input = CreateContainer(2, 0x75465A4C, [1, 0xFF, 0xF0]);

        bool success = RtfCompression.TryDecompress(input, 64, out byte[] output, out string? error);

        Assert.True(success, error);
        Assert.Equal(new byte[] { 0, (byte)'{' }, output);
    }

    [Fact]
    public void TryDecompressCancelledTokenStopsMelaAndLzFuLoops()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        byte[] mela = CreateMela(Encoding.ASCII.GetBytes("cancel"));
        byte[] lzfu = CreateContainer(1, 0x75465A4C, [0, (byte)'x']);

        Assert.Throws<OperationCanceledException>(() =>
            RtfCompression.TryDecompress(mela, 64, out _, out _, source.Token));
        Assert.Throws<OperationCanceledException>(() =>
            RtfCompression.TryDecompress(lzfu, 64, out _, out _, source.Token));
    }

    private static byte[] CreateMela(byte[] raw)
        => CreateContainer(raw.Length, 0x414C454D, raw);

    private static byte[] CreateContainer(int rawLength, uint magic, byte[] payload)
    {
        byte[] result = new byte[16 + payload.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(result, (uint)(result.Length - 4));
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), (uint)rawLength);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8), magic);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12), ComputeCrc32(payload));
        payload.CopyTo(result, 16);
        return result;
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0;
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return crc;
    }
}
