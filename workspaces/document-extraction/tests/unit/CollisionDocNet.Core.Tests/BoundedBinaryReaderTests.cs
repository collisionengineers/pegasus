using CollisionDocNet.Core;

namespace CollisionDocNet.Core.Tests;

[TestClass]
public sealed class BoundedBinaryReaderTests
{
    [TestMethod]
    public void IntegerReads_InBounds_UseLittleEndian()
    {
        var reader = new BoundedBinaryReader(new byte[] { 0x01, 0x02, 0x03, 0x04 });

        Assert.IsTrue(reader.TryReadByte(3, out byte single));
        Assert.IsTrue(reader.TryReadUInt16LittleEndian(1, out ushort twoBytes));
        Assert.IsTrue(reader.TryReadUInt32LittleEndian(0, out uint fourBytes));
        Assert.AreEqual((byte)0x04, single);
        Assert.AreEqual((ushort)0x0302, twoBytes);
        Assert.AreEqual(0x04030201u, fourBytes);
    }

    [TestMethod]
    [DataRow(-1L, 1L)]
    [DataRow(0L, -1L)]
    [DataRow(3L, 2L)]
    [DataRow(long.MaxValue, 1L)]
    public void TrySlice_OutOfBounds_ReturnsFalse(long offset, long length)
    {
        var reader = new BoundedBinaryReader(new byte[] { 1, 2, 3, 4 });

        Assert.IsFalse(reader.TrySlice(offset, length, out _));
    }

    [TestMethod]
    public void TryReadUInt32LittleEndian_TruncatedValue_ReturnsFalse()
    {
        var reader = new BoundedBinaryReader(new byte[] { 1, 2, 3 });

        Assert.IsFalse(reader.TryReadUInt32LittleEndian(0, out _));
    }
}
