using System.Buffers.Binary;
using System.Text;
using CollisionDocNet.Outlook;

namespace CollisionDocNet.Outlook.Tests;

[TestClass]
public sealed class RtfCompressionTests
{
    [TestMethod]
    public void TryDecompress_MelaPayload_ReturnsExactBytes()
    {
        byte[] raw = Encoding.ASCII.GetBytes("{\\rtf1 Synthetic}");
        byte[] input = CreateMela(raw);

        bool success = RtfCompression.TryDecompress(input, 1024, out byte[] output, out string? error);

        Assert.IsTrue(success, error);
        CollectionAssert.AreEqual(raw, output);
    }

    [TestMethod]
    public void TryDecompress_LzFuLiteralTokens_ReturnsExactBytes()
    {
        byte[] raw = Encoding.ASCII.GetBytes("12345678");
        byte[] payload = [0, .. raw];
        byte[] input = CreateContainer(raw.Length, 0x75465A4C, payload);

        bool success = RtfCompression.TryDecompress(input, 1024, out byte[] output, out string? error);

        Assert.IsTrue(success, error);
        CollectionAssert.AreEqual(raw, output);
    }

    [TestMethod]
    public void TryDecompress_CrcMismatch_RejectsPayload()
    {
        byte[] input = CreateMela(Encoding.ASCII.GetBytes("data"));
        input[^1] ^= 1;

        bool success = RtfCompression.TryDecompress(input, 1024, out byte[] output, out string? error);

        Assert.IsFalse(success);
        Assert.IsEmpty(output);
        Assert.IsNotNull(error);
        Assert.Contains("CRC", error);
    }

    [TestMethod]
    public void TryDecompress_DeclaredOutputExceedsBound_RejectsBeforeAllocation()
    {
        byte[] input = CreateMela(Encoding.ASCII.GetBytes("bounded"));

        bool success = RtfCompression.TryDecompress(input, 2, out byte[] output, out string? error);

        Assert.IsFalse(success);
        Assert.IsEmpty(output);
        Assert.IsNotNull(error);
        Assert.Contains("bound", error);
    }

    [TestMethod]
    public void TryDecompress_LzFuOverlappingBackReference_CopiesFromSlidingDictionary()
    {
        const int initialDictionaryLength = 207;
        ushort token = (ushort)((initialDictionaryLength << 4) | 3);
        byte[] payload = [0b0000_0010, (byte)'A', (byte)(token >> 8), (byte)token];
        byte[] input = CreateContainer(6, 0x75465A4C, payload);

        bool success = RtfCompression.TryDecompress(input, 64, out byte[] output, out string? error);

        Assert.IsTrue(success, error);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("AAAAAA"), output);
    }

    [TestMethod]
    public void TryDecompress_LzFuTruncatedBackReference_IsRejected()
    {
        byte[] input = CreateContainer(2, 0x75465A4C, [1, 0]);

        bool success = RtfCompression.TryDecompress(input, 64, out byte[] output, out string? error);

        Assert.IsFalse(success);
        Assert.IsEmpty(output);
        Assert.Contains("truncated", error!);
    }

    [TestMethod]
    public void TryDecompress_LzFuBackReference_WrapsDictionaryAt4096Bytes()
    {
        byte[] input = CreateContainer(2, 0x75465A4C, [1, 0xFF, 0xF0]);

        bool success = RtfCompression.TryDecompress(input, 64, out byte[] output, out string? error);

        Assert.IsTrue(success, error);
        CollectionAssert.AreEqual(new byte[] { 0, (byte)'{' }, output);
    }

    [TestMethod]
    public void TryDecompress_CancelledToken_StopsMelaAndLzFuLoops()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        byte[] mela = CreateMela(Encoding.ASCII.GetBytes("cancel"));
        byte[] lzfu = CreateContainer(1, 0x75465A4C, [0, (byte)'x']);

        Assert.ThrowsExactly<OperationCanceledException>(() =>
            RtfCompression.TryDecompress(mela, 64, out _, out _, source.Token));
        Assert.ThrowsExactly<OperationCanceledException>(() =>
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
