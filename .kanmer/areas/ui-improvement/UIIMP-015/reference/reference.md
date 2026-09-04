# Review record — UIIMP-015 (PR https://github.com/collisionengineers/pegasus/pull/658)

Head reviewed: `364ae208d8bf0cb439c9cc5f474ce961a9aad691`
(branch `task/uiimp-015-scoped-test-ui-capture`, confirmed by
`git rev-parse HEAD` in the detached review worktree
`.worktrees/uiimp-015-review`).

Reviewers: gpt-5.6-terra (xhigh) read the diff independently; Claude Opus
dispositioned every finding, ran the verification below, and gates the merge.
Built by gpt-5.6-sol.

## Verdict

**Request changes — not merged.** Two blocking findings; the change is
otherwise well made and its core mechanism is proven correct. The ticket
returns to Implementing for findings 1 and 2.

## Verification (review checkout, exit codes captured)

| Command | Exit |
| --- | ---: |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 (0 warnings, 0 errors) |
| `dotnet test ./tests/Pegasus.Core.Tests/... --configuration Release --no-build` | 0 (1,225 passed) |
| `dotnet test ./tests/Pegasus.ArchitectureTests/... --configuration Release --no-build` | 0 (100 passed) |
| `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Scope case-details -CaptureFilter "FullyQualifiedName~CaseDetailsWebTests"` | 0 (browser phase matched 0 tests, exit 0; non-browser 58 passed; snapshot update 1 passed) |
| `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope case-details` | 0 (1 passed) |
| `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope deliberately-wrong-prefix` | 1 (expected: "Test UI scope prefixes matched no catalogue state: - deliberately-wrong-prefix") |
| `pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1` | 0 (54 routed sources, 59 prototypes, 0 broken references) |
| `gh run list --branch ... --limit 1` | 0 (run 33869757840, head matches, status `in_progress`) |

Scope rationale: the diff changes only the snapshot tooling
(`TestUiSnapshotTests.cs`, `Update-TestUiSnapshots.ps1`) and documentation, so
the class covering the changed type is `TestUiSnapshotTests` — exercised here
in all three of its real modes (scoped update, scoped verify, guard failure)
rather than the implementer's discovery-only filter run. No unscoped capture,
whole integration suite or browser suite was run, per EPIC-012 §Build policy;
GitHub CI on the exact head is the full-suite gate. No migration is involved,
so `Test-MigrationGrants.ps1` does not apply.

Independent artifact reproduction: running the scoped capture in a clean review
worktree regenerated `docs/design/test-ui/pages/case-details--{default,
unavailable,conflict}.html` and `index.html` **byte-identical to the committed
content** (`git diff --stat` empty; only LF/CRLF working-copy warnings), and
touched no other page. The report's byte sizes (34,879 / 24,390 / 34,691) are
the LF blob sizes and are correct (`git cat-file -s` on the default page blob
returns 34,879). All three files begin `<!DOCTYPE html>`; the default page
contains `Case Overview`, `You are editing this case.` and
`case-overview-panel`, and no `<img src="#">`. (`class="case-sticky"` and the
eleven `id="section-"` hosts are absent — correct: the single-scroll frame is
CASE-038 and has not merged.)

## Findings and dispositions

| # | Severity | Location | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | Blocker | `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs:31` | The new `["case-details--default"]` matcher (`Required` "You are editing this case.", `AlsoRequired` "case-overview-panel") pins the default snapshot to a **`Not ready`** case rendering the **`Recover editing`** branch, while `catalogue.json` declares that state as "Case Overview **in Review** with the edit lease held". | **Fix.** Confirmed against the capture: of 130 case-detail responses produced by the scoped `CaseDetailsWebTests` cohort itself, three carry State `Review` *and* the lease presence strip (≈53.7 KB each), so the declared branch is reachable without widening the capture filter. Among matching candidates `Generate` takes the lexicographically smallest normalised HTML, so a loose matcher silently picks the smallest `Not ready` page. Tighten the matcher to require the `Review` state marker alongside the lease strip, regenerate, and re-record the artifact facts. This is squarely in scope: the ticket's own premise is that the previous artifact was wrong, and EPIC-012 §Build policy says to verify the artifact, not the gate. |
| 2 | Blocker | `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs:305-316` (`ParseScope`) | `-Scope ","` (or any all-separator value) survives `IsNullOrWhiteSpace`, then `Split(..., RemoveEmptyEntries)` yields an **empty, non-null** array. `ValidateScope` then passes vacuously, `Generate` skips every state, update mode writes only `index.html` and deletes nothing, and verify passes having compared and rendered no page at all. | **Fix.** This is exactly the silent no-op the plan's own review finding 2 required to fail explicitly, and the acceptance condition "a `-Scope` prefix matching no catalogue state fails, naming the prefix" does not hold for this input. Make an empty parsed prefix list a named failure (returning `null` would be worse — it would silently unscope). One or two lines in `ParseScope`/`ValidateScope`; no new concept. |
| 3 | Minor | `scripts/Update-TestUiSnapshots.ps1:53-76` | With a genuinely unmatched prefix, an *update* run still clears the retained capture directory and executes both capture phases before `ValidateScope` fails inside the test. The guard is pre-write for committed snapshots but not pre-capture. | **Rejected.** A PowerShell-side preflight would need the `pages/<prefix>--` vocabulary in a second place, breaking the one-list-per-concept rail that the plan and the reviewer both name; the reviewer's own suggested fix concedes it must not be duplicated. The cost is a wasted scoped capture, and the failure is explicit and named, not silent. No correctness impact. |

Everything else the reviewer checked came back clean and I re-confirmed it in
the diff: the unscoped path is behaviour-preserving (the filter text is
rearranged — `Category!=Corpus` moved into the phase builder, the focused-render
clause appended once — but selects the same tests, and every scoped branch is
guarded by `scope is null`); scoping is applied only at `state.File`, leaving
`manifest`, `entry.States` and `otherMatches` intact; `PEGASUS_TEST_UI_SCOPE` is
saved, explicitly cleared when `-Scope` is omitted, and restored in `finally`;
the `--` delimiter prevents prefix collisions; `index.html` is deliberately
rebuilt from the full catalogue; `AlsoRequired` is a minimal second positive
predicate, not a duplicate of `Required`/`Excluded`; no assertion was weakened
or deleted; no Core, product, route, `OperatorLabels`, package, migration or
`.github/workflows/ci.yml` change; the `Recover editing` control has a named
Razor handler. The diff matches the report and the checklist, the deviation is
declared, and the simplification pass's three applied fixes are all present in
the diff and behaviour-preserving, with the rejected one honestly reasoned.

Caveat recorded for the implementer: because `Generate` selects the
lexicographically smallest matching candidate, byte-identity between a scoped
and an unscoped regeneration is a property of matcher tightness, not of the
scoping mechanism. Fixing finding 1 tightens it; the unscoped `-Verify` in CI
on the exact head remains the proof.
