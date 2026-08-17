# Open questions

No operator decisions remain. Defaults chosen from repository evidence:

- Nightly QDOS pressure runs at 03:00 UTC and remains manually dispatchable.
- SQL deadlock retry is test-local, limited to error 1205 and three total attempts.
- Cancellation already requested at a resource-limit decision point takes precedence; uncancelled limits are unchanged.
- Deterministic Worker contract failures are not retried; diagnostics include exit code, stdout, and stderr.
