namespace CollisionDocNet.Core;

public enum ExtractionControlState
{
    Continue = 0,
    Cancelled,
    TimedOut,
}

/// <summary>Provides cooperative caller cancellation and a monotonic elapsed deadline.</summary>
public sealed class ExtractionControl
{
    private readonly CancellationToken _cancellationToken;
    private readonly TimeProvider _timeProvider;
    private readonly long _startTimestamp;
    private readonly TimeSpan _maximumElapsed;

    public ExtractionControl(
        TimeSpan maximumElapsed,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumElapsed, TimeSpan.Zero);

        _cancellationToken = cancellationToken;
        _maximumElapsed = maximumElapsed;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _startTimestamp = _timeProvider.GetTimestamp();
    }

    public ExtractionControlState Check()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            return ExtractionControlState.Cancelled;
        }

        return _timeProvider.GetElapsedTime(_startTimestamp) >= _maximumElapsed
            ? ExtractionControlState.TimedOut
            : ExtractionControlState.Continue;
    }

    /// <summary>
    /// Creates an operation-scoped token which is cancelled by either the caller or the
    /// remaining monotonic deadline. The returned scope must be disposed after the I/O.
    /// </summary>
    internal ExtractionInterruptScope CreateInterruptScope()
    {
        TimeSpan elapsed = _timeProvider.GetElapsedTime(_startTimestamp);
        TimeSpan remaining = _maximumElapsed - elapsed;
        return new ExtractionInterruptScope(
            remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero,
            _timeProvider,
            _cancellationToken);
    }
}

internal sealed class ExtractionInterruptScope : IDisposable
{
    private readonly CancellationTokenSource _deadlineSource;
    private readonly CancellationTokenSource _linkedSource;

    internal ExtractionInterruptScope(
        TimeSpan remaining,
        TimeProvider timeProvider,
        CancellationToken callerCancellation)
    {
        _deadlineSource = new CancellationTokenSource(remaining, timeProvider);
        _linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            callerCancellation,
            _deadlineSource.Token);
    }

    internal CancellationToken Token => _linkedSource.Token;

    public void Dispose()
    {
        _linkedSource.Dispose();
        _deadlineSource.Dispose();
    }
}
