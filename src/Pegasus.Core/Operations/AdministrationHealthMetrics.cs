namespace Pegasus.Core.Operations;

public sealed record DocumentContentCacheMetricSnapshot(long HitsSinceStart, long MissesSinceStart);

public interface IDocumentContentCacheMetrics
{
    DocumentContentCacheMetricSnapshot Snapshot();
    void RecordHit();
    void RecordMiss();
}
