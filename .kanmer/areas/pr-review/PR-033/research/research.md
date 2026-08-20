# Research

## Verified premises

- At branch head `7932d683`, `GraphDeletedMailSearchSource.SearchAsync` is the existing Deleted Items external-boundary owner.
- `GraphMailClient` already throws `JsonException` for malformed JSON, `InvalidDataException` for missing required fields/time, and `UnauthorizedAccessException` for a foreign folder or escaped page URI.
- The source catch already maps access denial, throttling, Azure authentication, HTTP failures, and provider timeout to `DeletedMailSearchState.Unavailable`, while caller cancellation propagates.
- Therefore the smallest change is to extend that one existing catch filter with the three established response-validation exception types. No retry or exception framework is needed.

## Assumptions

- None. The exception producers and caller catch were checked directly on the shared branch.
