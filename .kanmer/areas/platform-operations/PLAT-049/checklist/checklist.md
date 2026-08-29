# PLAT-049 checklist

- [x] Merge `origin/dev` into the lane branch before any edit (`b92cb9a7`, clean)
- [x] `OperatorLabels.AiJobs` nested class appended, nothing existing reordered (72 insertions, 0 deletions)
- [x] Page model injects the four AI ports and the Unidentified store
- [x] `OnGetAsync` loads non-terminal jobs plus today's terminal jobs, newest first
- [x] `OnPostSendUnidentifiedToAiAsync` calls `ICreateAiJob` with `UnidentifiedResolution`
- [x] `OnPostCompleteAiJobAsync` calls `IConfirmAiJob`
- [x] `OnPostCancelAiJobAsync` calls `ICancelAiJob` with a required reason
- [x] Every handler surfaces its refusal; no catch-all, no empty catch
- [x] AI Job List panel renders first, Service health second (§1.11 order)
- [x] Every rendered action resolves to a real route or a real handler; otherwise `—`
- [x] Send control renders only when an open Unidentified item exists
- [x] Service health action cell renders `—` where Core names no retry target
- [x] No explanatory copy added beyond the contract's partial-data notice
- [x] New tests added; no existing assertion weakened, inverted or deleted
- [x] `dotnet build ./Pegasus.slnx --configuration Release` green — 0 warnings, 0 errors
- [x] `dotnet test --filter "FullyQualifiedName~OperationsWebTests"` green — 19 passed, 0 failed (9 pre-existing + 10 new)
- [x] `dotnet test tests/Pegasus.ArchitectureTests` green — 100 passed, 0 failed
- [x] Simplification pass recorded in the plan (5 findings, 4 fixed, 1 rejected)
- [x] Core gaps 1-5 reported precisely, none silently absorbed
- [x] Commits pushed, PR #617 opened against `dev`, not merged

Not run, and therefore not claimed: the full suite, the `Browser` category,
and the snapshot capture script — all three are forbidden by the lane brief.
`docs/design/test-ui/catalogue.json`'s two `/Operations` states are stale and
need the once-per-merge regeneration on the merging branch.

## Adversarial verifier corrections - 2026-08-29

The following checked items supersede the earlier conflicting picker,
assertion, label-count and simplification entries.

- [x] Effective `Expired` rows use `ExpiresAtUtc` and remain visible on their
  terminal office date
- [x] Operations GET performs no `ListQueueAsync`; the global rail owns the one
  remaining queue enumeration
- [x] Send uses a canonical U-reference input and indexed point lookup on POST
- [x] `OperatorLabels.AiJobs` is 67 insertions / 0 deletions against `origin/dev`
- [x] One existing PLAT-049 assertion was intentionally inverted and renamed to
  assert the new bounded-input behaviour; it was not weakened to obtain green
- [x] Verifier dispositions and the missed queue-query efficiency finding are
  recorded in the plan
- [x] `dotnet build ./Pegasus.slnx --configuration Release` - exit 0, 0
  warnings, 0 errors
- [x] Focused `OperationsWebTests` solution filter - exit 0, 19 passed, 0
  failed, 0 skipped
- [x] Remediation commits `7df75798` and `3d5cdbb9` pushed to PR #617's branch
