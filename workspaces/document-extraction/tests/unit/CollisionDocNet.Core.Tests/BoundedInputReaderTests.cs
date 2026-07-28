using CollisionDocNet.Core;

namespace CollisionDocNet.Core.Tests;

[TestClass]
public sealed class BoundedInputReaderTests
{
    private readonly TestContext _testContext;

    public BoundedInputReaderTests(TestContext testContext) => _testContext = testContext;

    [TestMethod]
    public async Task ReadAllAsync_WithinLimit_ReturnsBytesAndChargesBudget()
    {
        byte[] bytes = [1, 2, 3, 4];
        using var stream = new MemoryStream(bytes);
        var budget = new ResourceBudget(CreateLimits(4));

        BoundedInputReadResult result = await BoundedInputReader.ReadAllAsync(
            stream,
            budget,
            new ExtractionControl(TimeSpan.FromSeconds(1)));

        Assert.AreEqual(BoundedInputReadStatus.Complete, result.Status);
        CollectionAssert.AreEqual(bytes, result.Bytes.ToArray());
        Assert.AreEqual(4, budget.GetSnapshot().InputBytes);
        Assert.IsTrue(stream.CanRead);
    }

    [TestMethod]
    public async Task ReadAllAsync_OverLimit_ReturnsNoPartialBytes()
    {
        using var stream = new MemoryStream([1, 2, 3, 4]);
        var budget = new ResourceBudget(CreateLimits(3));

        BoundedInputReadResult result = await BoundedInputReader.ReadAllAsync(
            stream,
            budget,
            new ExtractionControl(TimeSpan.FromSeconds(1)));

        Assert.AreEqual(BoundedInputReadStatus.ResourceLimitExceeded, result.Status);
        Assert.IsEmpty(result.Bytes);
        Assert.AreEqual(0, budget.GetSnapshot().InputBytes);
    }

    [TestMethod]
    public async Task ReadAllAsync_CancelledBeforeRead_ReturnsCancelled()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        using var stream = new MemoryStream([1]);

        BoundedInputReadResult result = await BoundedInputReader.ReadAllAsync(
            stream,
            new ResourceBudget(CreateLimits(1)),
            new ExtractionControl(TimeSpan.FromSeconds(1), cancellationToken: source.Token));

        Assert.AreEqual(BoundedInputReadStatus.Cancelled, result.Status);
        Assert.IsEmpty(result.Bytes);
    }

    [TestMethod]
    [Timeout(2000, CooperativeCancellation = true)]
    public async Task ReadAllAsync_BlockedRead_CallerCancellationInterruptsRead()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _testContext.CancellationToken);
        using var stream = new BlockingReadStream();
        ValueTask<BoundedInputReadResult> pending = BoundedInputReader.ReadAllAsync(
            stream,
            new ResourceBudget(CreateLimits(1)),
            new ExtractionControl(
                TimeSpan.FromSeconds(10),
                cancellationToken: cancellation.Token));
        await stream.ReadStarted;

        cancellation.Cancel();
        BoundedInputReadResult result = await pending;

        Assert.AreEqual(BoundedInputReadStatus.Cancelled, result.Status);
        Assert.IsEmpty(result.Bytes);
    }

    [TestMethod]
    [Timeout(2000, CooperativeCancellation = true)]
    public async Task ReadAllAsync_BlockedRead_MonotonicDeadlineInterruptsRead()
    {
        using var stream = new BlockingReadStream();

        BoundedInputReadResult result = await BoundedInputReader.ReadAllAsync(
            stream,
            new ResourceBudget(CreateLimits(1)),
            new ExtractionControl(
                TimeSpan.FromMilliseconds(25),
                cancellationToken: _testContext.CancellationToken));

        Assert.AreEqual(BoundedInputReadStatus.TimedOut, result.Status);
        Assert.IsEmpty(result.Bytes);
    }

    [TestMethod]
    public async Task ReadAllAsync_UnreadableStream_Throws()
    {
        using var stream = new MemoryStream();
        stream.Close();

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
            await BoundedInputReader.ReadAllAsync(
                stream,
                new ResourceBudget(CreateLimits(1)),
                new ExtractionControl(TimeSpan.FromSeconds(1))));
    }

    private static ResourceLimits CreateLimits(long maximum) =>
        new(
            "test/1",
            maximum,
            maximum,
            10,
            10,
            10,
            maximum,
            2,
            TimeSpan.FromSeconds(1));

    private sealed class BlockingReadStream : Stream
    {
        private readonly TaskCompletionSource _readStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ReadStarted => _readStarted.Task;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            _readStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
