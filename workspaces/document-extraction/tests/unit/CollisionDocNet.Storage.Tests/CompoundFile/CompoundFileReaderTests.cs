using CollisionDocNet.Storage.CompoundFile;

namespace CollisionDocNet.Storage.Tests.CompoundFile;

[TestClass]
public sealed class CompoundFileReaderTests
{
    [TestMethod]
    public void Read_RootDirectoryEntryIsRed_ReturnsValidatedDirectory()
    {
        byte[] bytes = CompoundFileFixture.CreateEmpty(3);
        CompoundFileFixture.GetDirectoryEntry(bytes, 512, 0)[67] =
            (byte)CompoundFileNodeColor.Red;

        CompoundFileReadResult result = CompoundFileReader.Read(bytes);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.File);
        Assert.AreEqual(
            CompoundFileNodeColor.Red,
            result.File.DirectoryEntries[0].Color);
    }

    [TestMethod]
    public void Read_RootDirectoryEntryHasUnknownColour_ReturnsInvalidDirectoryEntry()
    {
        byte[] bytes = CompoundFileFixture.CreateEmpty(3);
        CompoundFileFixture.GetDirectoryEntry(bytes, 512, 0)[67] = 2;

        CompoundFileReadResult result = CompoundFileReader.Read(bytes);

        AssertFailure(result, CompoundFileReadError.InvalidDirectoryEntry, location: 0);
    }

    [TestMethod]
    public void Read_ChildTreeRootIsRed_ReturnsInvalidDirectoryTree()
    {
        byte[] bytes = CompoundFileFixture.CreateWithRegularStream(3);
        CompoundFileFixture.GetDirectoryEntry(bytes, 512, 1)[67] =
            (byte)CompoundFileNodeColor.Red;

        CompoundFileReadResult result = CompoundFileReader.Read(bytes);

        AssertFailure(result, CompoundFileReadError.InvalidDirectoryTree, location: 1);
    }

    [TestMethod]
    [DataRow((ushort)3, 512)]
    [DataRow((ushort)4, 4096)]
    public void Read_EmptyVersion3Or4File_ReturnsValidatedDirectory(
        ushort version,
        int sectorSize)
    {
        byte[] bytes = CompoundFileFixture.CreateEmpty(version);

        CompoundFileReadResult result = CompoundFileReader.Read(bytes);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(CompoundFileReadError.None, result.Error);
        Assert.IsNotNull(result.File);
        Assert.AreEqual(version, result.File.Header.MajorVersion);
        Assert.AreEqual(sectorSize, result.File.Header.SectorSize);
        Assert.AreEqual("Root Entry", result.File.DirectoryEntries[0].Name);
        Assert.AreEqual(CompoundFileObjectType.RootStorage, result.File.DirectoryEntries[0].ObjectType);
    }

    [TestMethod]
    [DataRow((ushort)3)]
    [DataRow((ushort)4)]
    public void Read_RegularStream_ReturnsExactContent(ushort version)
    {
        byte[] bytes = CompoundFileFixture.CreateWithRegularStream(version, fill: 0xA7);

        CompoundFileReadResult result = CompoundFileReader.Read(bytes);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.File);
        CompoundFileDirectoryEntry stream = result.File.DirectoryEntries[1];
        Assert.AreEqual("Regular", stream.Name);
        Assert.AreEqual((ulong)4096, stream.StreamSize);
        Assert.AreEqual((uint)0, stream.ParentStreamId);
        Assert.HasCount(4096, stream.Content);
        Assert.AreEqual((byte)0xA7, stream.Content[0]);
        Assert.AreEqual((byte)0xA7, stream.Content[^1]);
    }

    [TestMethod]
    public void Read_MiniStream_ReturnsExactContent()
    {
        byte[] expected = [0x10, 0x20, 0x30, 0x40, 0x50];
        byte[] bytes = CompoundFileFixture.CreateWithMiniStream(expected);

        CompoundFileReadResult result = CompoundFileReader.Read(bytes);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.File);
        CollectionAssert.AreEqual(expected, result.File.DirectoryEntries[1].Content.ToArray());
        Assert.HasCount(128, result.File.MiniFat);
    }

    [TestMethod]
    public void Read_DifatSector_ReturnsAllFatSectorIds()
    {
        byte[] bytes = CompoundFileFixture.CreateWithDifat();

        CompoundFileReadResult result = CompoundFileReader.Read(bytes);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.File);
        Assert.HasCount(110, result.File.FatSectorIds);
        Assert.AreEqual((uint)0, result.File.FatSectorIds[0]);
        Assert.AreEqual((uint)109, result.File.FatSectorIds[^1]);
    }

    [TestMethod]
    public void Read_DifatCycle_ReturnsInvalidDifat()
    {
        byte[] bytes = CompoundFileFixture.CreateWithDifat();
        Span<byte> difat = CompoundFileFixture.GetSector(bytes, 512, 111);
        CompoundFileFixture.WriteUInt32(difat, difat.Length - sizeof(uint), 111);

        CompoundFileReadResult result = CompoundFileReader.Read(bytes);

        AssertFailure(result, CompoundFileReadError.InvalidDifat, location: 111);
    }

    [TestMethod]
    public void Read_CancelledToken_ReturnsCancelledWithoutThrowing()
    {
        byte[] bytes = CompoundFileFixture.CreateEmpty(3);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        CompoundFileReadResult result = CompoundFileReader.Read(
            bytes,
            cancellationToken: cancellation.Token);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(CompoundFileReadError.Cancelled, result.Error);
    }

    [TestMethod]
    public void Read_InputAboveConfiguredLimit_ReturnsInputLimitExceeded()
    {
        byte[] bytes = CompoundFileFixture.CreateEmpty(3);
        var limits = CompoundFileReadLimits.Default with { MaximumInputBytes = bytes.Length - 1 };

        CompoundFileReadResult result = CompoundFileReader.Read(bytes, limits);

        AssertFailure(result, CompoundFileReadError.InputLimitExceeded);
    }

    [TestMethod]
    public void Read_FatCycle_ReturnsFatCycle()
    {
        byte[] bytes = CompoundFileFixture.CreateWithRegularStream(3);
        Span<byte> fat = CompoundFileFixture.GetSector(bytes, 512, CompoundFileFixture.FatSector);
        CompoundFileFixture.WriteUInt32(fat, 2 * sizeof(uint), 2);

        CompoundFileReadResult result = CompoundFileReader.Read(bytes);

        AssertFailure(result, CompoundFileReadError.FatCycle, location: 2);
    }

    [TestMethod]
    public void Read_TwoStreamsSharingARegularSector_ReturnsCrossLink()
    {
        byte[] bytes = CompoundFileFixture.CreateWithRegularStream(3);
        Span<byte> directory = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 2);
        WriteSecondStream(directory, startingSector: 2, size: 4096);
        Span<byte> root = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 0);
        Span<byte> first = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 1);
        CompoundFileFixture.WriteUInt32(root, 76, 1);
        CompoundFileFixture.WriteUInt32(first, 72, 2);

        CompoundFileReadResult result = CompoundFileReader.Read(bytes);

        AssertFailure(result, CompoundFileReadError.SectorCrossLinked, location: 2);
    }

    [TestMethod]
    public void Read_DirectorySiblingCycle_ReturnsDirectoryTreeCycle()
    {
        byte[] bytes = CompoundFileFixture.CreateWithRegularStream(3);
        Span<byte> first = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 1);
        CompoundFileFixture.WriteUInt32(first, 68, 1);

        CompoundFileReadResult result = CompoundFileReader.Read(bytes);

        AssertFailure(result, CompoundFileReadError.DirectoryTreeCycle, location: 1);
    }

    [TestMethod]
    public void Read_StreamChainShorterThanDeclaredSize_ReturnsLengthMismatch()
    {
        byte[] bytes = CompoundFileFixture.CreateWithRegularStream(3);
        Span<byte> first = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 1);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(first[120..], 4608);

        CompoundFileReadResult result = CompoundFileReader.Read(bytes);

        AssertFailure(result, CompoundFileReadError.StreamChainLengthMismatch, location: 2);
    }

    [TestMethod]
    public void Read_MiniStreamCrossLink_ReturnsCrossLink()
    {
        byte[] bytes = CompoundFileFixture.CreateWithMiniStream([1, 2, 3]);
        Span<byte> second = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 2);
        WriteSecondStream(second, startingSector: 0, size: 3);
        Span<byte> first = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 1);
        CompoundFileFixture.WriteUInt32(first, 72, 2);

        CompoundFileReadResult result = CompoundFileReader.Read(bytes);

        AssertFailure(result, CompoundFileReadError.SectorCrossLinked, location: 0);
    }

    [TestMethod]
    public void Read_MiniFatCycle_ReturnsMiniFatCycle()
    {
        byte[] bytes = CompoundFileFixture.CreateWithMiniStream([1, 2, 3]);
        Span<byte> root = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(root[120..], 128);
        Span<byte> stream = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 1);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(stream[120..], 65);
        Span<byte> miniFat = CompoundFileFixture.GetSector(bytes, 512, CompoundFileFixture.MiniFatSector);
        CompoundFileFixture.WriteUInt32(miniFat, 0, 0);

        CompoundFileReadResult result = CompoundFileReader.Read(bytes);

        AssertFailure(result, CompoundFileReadError.MiniFatCycle, location: 0);
    }

    [TestMethod]
    public void Read_RightSiblingWithLowerSortKey_ReturnsInvalidDirectoryTree()
    {
        byte[] bytes = CompoundFileFixture.CreateWithRegularStream(3);
        Span<byte> second = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 2);
        WriteSecondStream(second, startingSector: CompoundFileConstants.EndOfChain, size: 0, name: "A");
        Span<byte> first = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 1);
        CompoundFileFixture.WriteUInt32(first, 72, 2);

        CompoundFileReadResult result = CompoundFileReader.Read(bytes);

        AssertFailure(result, CompoundFileReadError.InvalidDirectoryTree, location: 2);
    }

    [TestMethod]
    public void Read_UnallocatedDirectoryEntryWithData_ReturnsInvalidDirectoryEntry()
    {
        byte[] bytes = CompoundFileFixture.CreateWithRegularStream(3);
        Span<byte> orphan = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 2);
        orphan[0] = 1;

        CompoundFileReadResult result = CompoundFileReader.Read(bytes);

        AssertFailure(result, CompoundFileReadError.InvalidDirectoryEntry, location: 2);
    }

    [TestMethod]
    public void Read_UnreachableAllocatedDirectoryEntry_ReturnsInvalidDirectoryTree()
    {
        byte[] bytes = CompoundFileFixture.CreateWithRegularStream(3);
        Span<byte> orphan = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 2);
        WriteSecondStream(orphan, CompoundFileConstants.EndOfChain, 0, "Orphan");

        CompoundFileReadResult result = CompoundFileReader.Read(bytes);

        AssertFailure(result, CompoundFileReadError.InvalidDirectoryTree, location: 2);
    }

    [TestMethod]
    public void Read_DirectoryTreeWithUnequalBlackHeightButValidMsCfbRules_ReturnsDirectory()
    {
        byte[] bytes = CompoundFileFixture.CreateWithRegularStream(3);
        Span<byte> first = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 1);
        Span<byte> second = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 2);
        WriteSecondStream(second, CompoundFileConstants.EndOfChain, 0, "Regular2");
        second[67] = (byte)CompoundFileNodeColor.Black;
        CompoundFileFixture.WriteUInt32(first, 72, 2);

        CompoundFileReadResult result = CompoundFileReader.Read(bytes);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.File);
        Assert.HasCount(3, result.File.DirectoryEntries.Where(static entry =>
            entry.ObjectType != CompoundFileObjectType.Unallocated));
        Assert.AreEqual("Regular", result.File.DirectoryEntries[1].Name);
        Assert.AreEqual("Regular2", result.File.DirectoryEntries[2].Name);
    }

    [TestMethod]
    public void Read_ValidInput_DoesNotModifyCallerOwnedBytes()
    {
        byte[] bytes = CompoundFileFixture.CreateWithMiniStream([1, 2, 3]);
        byte[] original = (byte[])bytes.Clone();

        _ = CompoundFileReader.Read(bytes);

        CollectionAssert.AreEqual(original, bytes);
    }

    private static void WriteSecondStream(
        Span<byte> entry,
        uint startingSector,
        ulong size,
        string name = "Regular2")
    {
        int byteCount = System.Text.Encoding.Unicode.GetBytes(name, entry);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(
            entry[64..],
            checked((ushort)(byteCount + 2)));
        entry[66] = (byte)CompoundFileObjectType.Stream;
        entry[67] = (byte)CompoundFileNodeColor.Red;
        CompoundFileFixture.WriteUInt32(entry, 68, CompoundFileConstants.NoStream);
        CompoundFileFixture.WriteUInt32(entry, 72, CompoundFileConstants.NoStream);
        CompoundFileFixture.WriteUInt32(entry, 76, CompoundFileConstants.NoStream);
        CompoundFileFixture.WriteUInt32(entry, 116, startingSector);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(entry[120..], size);
    }

    private static void AssertFailure(
        CompoundFileReadResult result,
        CompoundFileReadError error,
        uint? location = null)
    {
        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.File);
        Assert.AreEqual(error, result.Error);
        Assert.AreEqual(location, result.Location);
    }
}
