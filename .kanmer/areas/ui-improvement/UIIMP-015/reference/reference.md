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

---

# Review record — UIIMP-015 (PR https://github.com/collisionengineers/pegasus/pull/658) — re-review

Head reviewed: `b7fa4f70c19baae6f93ca30ca52ac75363185868`
(branch `task/uiimp-015-scoped-test-ui-capture`, confirmed by `git rev-parse
HEAD` in the detached review worktree `.worktrees/uiimp-015-review`; the
branch had not moved since the fix round).

Reviewers: gpt-5.6-terra (xhigh) read the diff independently; Claude Opus
dispositioned, ran the verification below and gated the merge. Built by
gpt-5.6-sol.

Fix-round diff under re-review: `git diff 364ae208d..b7fa4f70c` — 15 lines in
`TestUiSnapshotTests.cs` and 256 lines in the regenerated
`case-details--default.html`. Nothing else changed.

## Verdict

**Approve.** Both first-round blockers are closed by the mechanism claimed,
independently reproduced; no regression introduced by the fix commit. One nit
accepted.

## Earlier findings — closure confirmed at this head

| # | First-round finding | Closure evidence |
| --- | --- | --- |
| 1 | `case-details--default` matcher pinned the snapshot to a `Not ready` / `Recover editing` page while `catalogue.json` declares "Case Overview in Review with the edit lease held". | **Closed.** `AlsoRequired2: "status status--navy\">Review<"` added (`TestUiSnapshotTests.cs:31-34`). Artifact opened directly: 51,251 bytes (git blob), begins `<!DOCTYPE html>`, `<title>Case QDOS3100042 · Pegasus</title>`; identity-ribbon value at line 203 is `<span class="status status--navy">Review</span>` and the decision row at line 474 repeats it (2 matches); `You are editing this case.` (1), `case-overview-panel` (1), `Case Overview` section heading at line 284 (1); `Recover editing` now **0 matches** (was the wrong page's marker); no `<img src="#">`. The two remaining `Not ready` hits are a queue label (line 317) and the pre-existing one-sentence consequence on save (line 376), not the case's own state. `class="case-sticky"` and `id="section-"` are 0 — correct, the single-scroll frame is CASE-038 (D29) and has not merged. |
| 2 | `ParseScope` on an all-separator value (`-Scope ","`) returned an empty non-null array, so `ValidateScope` passed vacuously and update/verify silently did nothing. | **Closed.** `Assert.True(scope.Length > 0, ...)` in `ParseScope` (`TestUiSnapshotTests.cs:317`). Exercised in this checkout: `-Verify -SkipCapture -Scope ","` → exit **1**, message `Test UI scope contains no usable prefixes: ','`. An absent `-Scope` still returns `null` (full unscoped run). |
| 3 | Minor: an unmatched prefix still runs both capture phases before `ValidateScope` fails. | **Still rejected**, unchanged reasoning: a PowerShell preflight would duplicate the `pages/<prefix>--` vocabulary in a second place. gpt-5.6-terra did not re-raise it. |

## Independent verification (review checkout, exit codes captured)

| Command | Exit |
| --- | ---: |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 (0 warnings, 0 errors) |
| `dotnet test ./tests/Pegasus.Core.Tests/... --configuration Release --no-build` | 0 (1,225 passed) |
| `dotnet test ./tests/Pegasus.ArchitectureTests/... --configuration Release --no-build` | 0 (100 passed) |
| `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Scope case-details -CaptureFilter "FullyQualifiedName~CaseDetailsWebTests"` | 0 (browser phase matched 0, exit 0; non-browser 58 passed in 4 m 23 s; snapshot update 1 passed) |
| `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope case-details` | 0 (1 passed) |
| `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope ","` | 1 (expected: "Test UI scope contains no usable prefixes: ','") |
| `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope deliberately-wrong-prefix` | 1 (expected: "Test UI scope prefixes matched no catalogue state:") |
| `pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1` | 0 |

Scope rationale: the diff changes only the snapshot tooling
(`TestUiSnapshotTests.cs`, `Update-TestUiSnapshots.ps1`), two documentation
files and one regenerated artifact, so `TestUiSnapshotTests` is the one class
covering the changed type — exercised here in all four of its real paths
(scoped update, scoped verify, the empty-prefix guard, the unmatched-prefix
guard) rather than a discovery-only filter run. Core and Architecture prove
nothing else regressed. No migration, so `Test-MigrationGrants.ps1` does not
apply. No unscoped capture, whole integration suite or browser suite was run,
per EPIC-012 §Build policy; GitHub CI on the exact head is the full-suite
gate.

**Artifact reproduced independently.** Running the scoped capture in this
clean review worktree regenerated `case-details--{default,unavailable,
conflict}.html` and `index.html` **byte-identical to the committed content**
(`git -c core.autocrlf=false diff --stat -- docs/design/test-ui` empty;
`git diff --numstat` on the default page empty — the four `M` entries in
`git status` are CRLF working-copy normalization only) and touched no other
page. So the tightened matcher is deterministic, not merely lucky once.

## Findings and dispositions

| # | Severity | Location | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | Nit | `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs:405-417` | `StateMatch` now carries `AlsoRequired` and `AlsoRequired2` as two numbered optional fields rather than one collection — a mild one-list-per-concept smell, and a third would force `AlsoRequired3`. | **Accepted risk.** The record already modelled "one required text plus one optional additional text"; this is the first state needing two, and a collection would be an abstraction without a second concrete need (the no-speculative-abstraction rail). Recorded so a third occurrence converts it to a collection instead of extending the numbering. |

gpt-5.6-terra (xhigh) returned **APPROVE with no findings**, confirming
independently: the matcher is deterministic and the artifact is the declared
Review state; `AlsoRequired2` is proportionate; empty separator-only scopes
fail explicitly; the diff is limited to the five owned paths; no product UI,
Core policy, labels, migration or handler changed; the reported byte size,
doctype and markers match the artifact; the simplification dispositions match
the diff.

Re-confirmed by me in the diff: the unscoped path still selects exactly the
same tests (`Category!=Corpus` moved into the phase builder, the
focused-render clause appended once and removed from the default value, so
the text differs but the set does not); scoping is applied only at
`state.File`, leaving `manifest`, `entry.States` and `otherMatches` intact;
`VerifyOfflineBrowserRenderAsync` iterates the already-scoped `generated`, so
the offline render is scoped without a second matcher; orphan detection and
update-mode deletion share the one `MatchesScopePrefix` helper; `index.html`
is always rebuilt from the full catalogue; `PEGASUS_TEST_UI_SCOPE` is saved,
explicitly cleared when `-Scope` is omitted and restored in `finally`; the
`--` delimiter prevents prefix collisions. No assertion was weakened or
deleted — two were added. Owned paths only (`AGENTS.md` and its `CLAUDE.md`
symlink, `docs/runbook.md`, `scripts/Update-TestUiSnapshots.ps1`,
`TestUiSnapshotTests.cs`, `pages/case-details--default.html`); the AGENTS.md
edit sits in the Commands section, outside the Kanmer-managed block; no
`.github/workflows/ci.yml`, package, Core, route, `OperatorLabels` or
migration change; no operator-facing copy added (this is tooling). The report
and the 16-item checklist match the diff, the `StateMatches` deviation is
declared, and the simplification pass's three applied fixes are present with
the rejected one honestly reasoned.
