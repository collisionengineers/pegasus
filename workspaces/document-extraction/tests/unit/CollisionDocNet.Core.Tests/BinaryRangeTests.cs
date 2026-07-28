using CollisionDocNet.Core;

namespace CollisionDocNet.Core.Tests;

[TestClass]
public sealed class BinaryRangeTests
{
    [TestMethod]
    [DataRow(0L, 0L, 0L)]
    [DataRow(0L, 10L, 10L)]
    [DataRow(9L, 1L, 10L)]
    [DataRow(10L, 0L, 10L)]
    public void TryCreate_InBounds_ReturnsRange(long offset, long length, long containingLength)
    {
        bool created = BinaryRange.TryCreate(offset, length, containingLength, out BinaryRange range);

        Assert.IsTrue(created);
        Assert.AreEqual(offset, range.Offset);
        Assert.AreEqual(length, range.Length);
        Assert.AreEqual(offset + length, range.End);
    }

    [TestMethod]
    [DataRow(-1L, 0L, 10L)]
    [DataRow(0L, -1L, 10L)]
    [DataRow(0L, 0L, -1L)]
    [DataRow(11L, 0L, 10L)]
    [DataRow(9L, 2L, 10L)]
    [DataRow(long.MaxValue, long.MaxValue, long.MaxValue)]
    public void TryCreate_InvalidOrOverflowingRange_ReturnsFalse(
        long offset,
        long length,
        long containingLength)
    {
        Assert.IsFalse(BinaryRange.TryCreate(offset, length, containingLength, out _));
    }

    [TestMethod]
    public void Create_InvalidRange_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => BinaryRange.Create(5, 6, 10));
    }
}
