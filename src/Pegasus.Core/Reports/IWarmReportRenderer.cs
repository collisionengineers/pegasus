namespace Pegasus.Core.Reports;

/// <summary>
/// Pays the report renderer's first-use cost (the embedded fonts, the layout
/// engine and the graphics library) before a staff request has to. It renders
/// no Case: the smallest possible document goes through the same admission
/// gate as a real render, so it counts against the same bound and never
/// queues more than one render's worth of work.
/// </summary>
public interface IWarmReportRenderer
{
    Task WarmAsync(CancellationToken cancellationToken = default);
}
