using Pegasus.Core.Documents;

namespace Pegasus.Core.Tests.Documents;

/// <summary>
/// An abandoned upload is deleted object by object once each is older than
/// R100's 24 hours, never a younger one; a failed delete is counted and the
/// rest still run; the listing resumes past a page of young objects.
/// </summary>
public sealed class SweepAbandonedDocumentUploadsTests
{
    private static readonly DateTimeOffset Now = new(2031, 5, 7, 11, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Aged = Now - DocumentUploadLimits.Lifetime;

    [Fact]
    public async Task AnObjectAtTheLifetimeIsDeletedAndAYoungerOneIsKept()
    {
        var staging = new InMemoryDocumentUploadStaging();
        staging.Objects.Add(new("uploads/a/manifest", Aged));
        staging.Objects.Add(new("uploads/a/0001", Aged.AddSeconds(1)));
        var sweep = new SweepAbandonedDocumentUploads(staging, new DocumentUploadSweepCursor(), new SettableClock(Now));

        var result = await sweep.ExecuteAsync(10);

        Assert.Equal(new SweepAbandonedDocumentUploadsResult(1, 0), result);
        Assert.Equal("uploads/a/0001", Assert.Single(staging.Objects).Name);
        Assert.Equal(1, staging.Deletes);
    }

    [Fact]
    public async Task OneFailedDeleteIsCountedAndTheRestStillRun()
    {
        var staging = new InMemoryDocumentUploadStaging();
        staging.Objects.Add(new("uploads/a/0001", Aged.AddHours(-1)));
        staging.Objects.Add(new("uploads/a/0002", Aged.AddHours(-1)));
        staging.Objects.Add(new("uploads/a/manifest", Aged.AddHours(-1)));
        staging.FailingDeletes.Add("uploads/a/0002");
        var sweep = new SweepAbandonedDocumentUploads(staging, new DocumentUploadSweepCursor(), new SettableClock(Now));

        var result = await sweep.ExecuteAsync(10);

        Assert.Equal(new SweepAbandonedDocumentUploadsResult(2, 1), result);
        Assert.Equal("uploads/a/0002", Assert.Single(staging.Objects).Name);
    }

    [Fact]
    public async Task TheCursorResumesPastAPageOfYoungerObjects()
    {
        var staging = new InMemoryDocumentUploadStaging();
        staging.Objects.Add(new("uploads/a/0001", Now));
        staging.Objects.Add(new("uploads/a/0002", Now));
        staging.Objects.Add(new("uploads/b/0001", Aged.AddMinutes(-5)));
        var cursor = new DocumentUploadSweepCursor();
        var clock = new SettableClock(Now);

        var first = await new SweepAbandonedDocumentUploads(staging, cursor, clock).ExecuteAsync(2);
        var second = await new SweepAbandonedDocumentUploads(staging, cursor, clock).ExecuteAsync(2);
        var third = await new SweepAbandonedDocumentUploads(staging, cursor, clock).ExecuteAsync(2);

        Assert.Equal(new SweepAbandonedDocumentUploadsResult(0, 0), first);
        Assert.Equal(new SweepAbandonedDocumentUploadsResult(1, 0), second);
        Assert.Equal(new SweepAbandonedDocumentUploadsResult(0, 0), third);
        Assert.Equal([null, "uploads/a/0002", null], staging.Continuations);
        Assert.Equal(2, staging.Objects.Count);
    }
}
