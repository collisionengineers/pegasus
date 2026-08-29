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

## Pre-merge review dispositions — 2026-08-29

An independent `gpt-5.6-sol` cross-model reviewer returned `REQUEST_CHANGES` with
four blockers and two findings. Every one is disposed here per rule 22. The
orchestrator re-derived the disputed number empirically rather than taking either
side on assertion.

### Blocker 1 — the comment states a false flake rate · **FIXED**

`PrincipalCredentialPersistenceTests.cs` said the no-op tamper happened "roughly
one run in four". That is wrong, and so was the pre-existing "one run in
sixty-four" in `ProviderApiSubmissionTests.cs`.

**Measured, not argued** — 200,000 sampled secrets built the same way the
production issuer builds them (`PrincipalCredentials.cs:293-297`, 32 random bytes
→ base64url → `TrimEnd('=')`):

```
distinct final characters: 16
alphabet:                  048AEIMQUYcgkosw
P(last == 'A')             6.175 %   ->  1 in 16.19
secret tail length         43 characters
```

The tail is 43 characters because 32 bytes is 256 bits and base64 carries 6 bits
per character: the 43rd character encodes only the remaining **4 bits**, so it is
drawn from 16 values — and `'A'` is one of them. **One run in sixteen.**

Both comments now state that, with the reason, so the next reader does not have
to re-derive it. (The reviewer attributed the 6.25 % figure to "decision D25";
D25 is the release-cadence decision — the figure is in the flake table of
`decisions-2026-08-29-closeout.md`. The number is right; the citation was not.)

### Blocker 2 — the report's build claim is stale · **FIXED**

The post-implementation report said the solution build "breaks on `origin/dev`
today". True when written, stale now: [[DELIV-035]] (PR #625, merge `55e23b02`)
fixed the CS1739 break and this branch has merged that `dev` forward. Re-run by
the orchestrator: **Build succeeded**. Recorded in the report with the history
preserved rather than overwritten.

### Blocker 3 — the sweep missed real matches · **FIXED, and none has the defect**

The `files` document claimed the broader grep returned "only" the Provider API
match. It did not. The orchestrator re-ran the sweep across `src/` and `tests/`;
here is every hit with a real disposition:

| Site | Shape | Disposition |
| --- | --- | --- |
| `tests/…/PrincipalCredentialPersistenceTests.cs:70` | `[..^1] + (x[^1]=='A' ? 'B' : 'A')` | **This ticket's fix.** Guaranteed to differ. |
| `tests/…/ProviderApiSubmissionTests.cs:69` | same conditional shape | Already safe; only its rate comment was wrong, now corrected. |
| `tests/Pegasus.Core.Tests/Cases/PrincipalCredentialsTests.cs:32` | `IsWellFormed(keyId, secret[..^1])` | **No defect.** It truncates rather than substitutes, so the result differs in *length* and can never equal the original. |
| `tests/…/Qdos/EvaSubmissionPolicyTests.cs:116,119,120` | `delays[..^1]` | **Not applicable.** A range over a collection of delays, not a secret mutation. |
| `src/Pegasus.Core/Eva/CaseEvaMapping.cs:303` | `parts[..^1]` | **Not applicable.** Drops a postcode element from a split address. |
| `tests/…/BoxDocumentContentStoreTests.cs:81,84` | `Sha256("different content")` vs `"actual content"` | **No defect.** Two distinct literals; the wrong hash can never equal the right one. |

No other instance of the defect exists. That claim is now evidenced rather than
asserted.

### Blocker 4 — the simplification pass was marked "n/a" · **FIXED**

"n/a — the diff is a two-line test fix" was not honest for a +9/−1 code change,
and the pass missed the false rate claim it introduced. The real pass:

- **Reuse** — the conditional-flip shape is taken from
  `ProviderApiSubmissionTests.cs:69`, which already had it. The existing
  convention wins; nothing new was designed.
- **Simplification** — none available; the fix is one expression.
- **Efficiency** — not applicable.
- **Altitude** — test-only; no policy moved layer.
- **Finding, applied:** the new six-line comment stated a false rate. Corrected
  above, and the sibling's pre-existing false rate corrected with it.

### Finding (medium) — no Governing docs section for FRD-09 · **ACCEPTED, with reason**

The plan links `docs/frd/frd-09-provider-and-intermediary-routes.md` but has no
Governing docs section. The reviewer confirmed the implementation nevertheless
preserves FRD-09's wrong-secret refusal (`Assert.Null` on the tampered secret,
lines 81-84 of its reading). The omission is a documentation gap in a test-only
fix, not a behaviour risk. Noted rather than back-filled.

### Finding (medium) — the sibling's false rate was not dispositioned · **FIXED**

`ProviderApiSubmissionTests.cs` is owned by the merged TICK-058 lineage and no
in-flight lane owns it, so this is D19 case 2 — **fixed anyway, and said loudly
here** so the ownership call can be confirmed.
