# Research

Verified at current branch: `GraphDeletedMailSearchSource.SearchAsync` maps access-denied, throttling, HTTP, and provider timeout failures to `Unavailable`, but `GraphMailClient.SendAsync` acquires the token before HTTP and Azure Identity's `AuthenticationFailedException` is not in that catch. Add that established SDK exception to the same external-boundary policy. The existing conditional `TaskCanceledException` branch continues to preserve caller cancellation; no retry or new taxonomy is needed.
