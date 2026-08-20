# Research — PR-020

`HttpClient.Timeout` surfaces as `TaskCanceledException` while the caller token remains uncancelled. Catch that shape as unavailable, but use a catch filter on `!cancellationToken.IsCancellationRequested`; genuine caller cancellation must propagate. Source: current Graph adapter catch policy and .NET cancellation semantics.
