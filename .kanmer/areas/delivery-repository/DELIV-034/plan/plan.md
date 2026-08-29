## Plan

1. Replace the unconditional-append mutation in
   `PrincipalCredentialPersistenceTests.cs:62` with the guaranteed-mutation
   shape already established in `ProviderApiSubmissionTests.cs:67`
   (`secret[..^1] + (secret[^1] == 'A' ? 'B' : 'A')`) — reusing the existing
   convention rather than inventing a new one.
2. Add `Assert.NotEqual(firstSecret, tamperedSecret)` before the
   authenticate call, so a future change that makes the mutation a no-op
   fails loudly instead of passing silently (ticket requirement, and D19 —
   never weaken/delete the proving assertion, only make the setup that feeds
   it correct).
3. Keep the existing `Assert.Null(await authenticate.ExecuteAsync(firstKeyId,
   tamperedSecret, default))` assertion unchanged in strength — it is the
   only proof that a near-miss credential is rejected.
4. Sweep `git grep -n '\[\.\.\^1\]' -- tests/` (and a broader
   wrong/invalid/bad/tampered-secret-or-hash grep) for the same
   unconditional-mutation shape elsewhere; fix every instance found. Result:
   no other instance has the defect — see the `files` document for the
   per-hit disposition.
5. Build the affected project and run
   `IssueResetPauseResumeRevokeAreHashOnlyReplaySafeAndFailClosed` ten
   consecutive times, reporting real pass/fail counts each run.

## Scope

Single-file test change plus a read-only sweep; no production code, no new
package, no new top-level directory. Fix profile — no research/impact docs
required beyond this plan and the files list.

## Simplification pass (2026-08-29)

n/a — the diff is a two-line test fix (one mutation expression, one added
assertion) reusing an existing in-repo convention verbatim; nothing to
simplify, no reuse/efficiency/altitude finding.
