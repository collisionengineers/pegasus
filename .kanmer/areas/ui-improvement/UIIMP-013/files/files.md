# Files — UIIMP-013: The test-ui gate costs 50 minutes of every build-affecting PR

Surveyed read-only from `origin/dev` at `fbf8ee40983ee30030b296d9e61274b238c80b04`
(the run's recorded `9b8f78a3…` has since advanced; every file below is identical on
`origin/dev` and `origin/main` at survey time — `git diff --name-only origin/dev main`
lists none of them).

## Where the change lands

| Path | Why |
| --- | --- |
| `scripts/Update-TestUiSnapshots.ps1` | The capture cost lives here. Line 33 pins `-- xUnit.MaxParallelThreads=2` across the **whole** capture filter, not only its browser part; lines 29-33 and 40-43 both omit `--no-build`, so the solution is built twice. The split and the `--no-build` land here so local and CI share one behaviour. |
| `.github/workflows/ci.yml` | The `test-ui` job (`timeout-minutes: 75`) and its comment block. Two statements in that comment are false against the script and must be corrected; the step needs its own budget below the job's; and a failure diagnostic must name the budget so a killed run no longer reads as a stale corpus. |
| `docs/runbook.md` | § Locked restore, build, and test (lines 306-334) is the named owner of the test parallelism model. It documents the project cap of four and why the browser selection halves it; after this change the capture applies that same split and the section says so. |
| `AGENTS.md` | Lines 168-174 are the Test UI command list. **Only if** a documented command name or switch changes (rule 24). The planned change alters neither, so the expected diff here is zero. |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| `tests/Pegasus.IntegrationTests/xunit.runner.json` | `maxParallelThreads: 4` — the project's own cap, and the value the non-browser capture half inherits once the command-line override is removed. Do not change it: the runbook explains it bounds concurrent LocalDB template restores across simultaneous agent runs. |
| `.github/workflows/ci.yml` § `sql-integration` | Runs `Category!=Corpus&Category!=Browser` — a strict **superset** of the capture's non-browser half — at the project default, and its comment records the measurement: "the whole lane parallel on one runner is 11m55s of tests". That is the proven ceiling for the non-browser half after the split. |
| `.github/workflows/ci.yml` § `browser` | Runs `Category=Browser&Category!=Corpus` at `MaxParallelThreads=2`. Intersecting the capture filter with `Category=Browser` yields **exactly** this selection, so this lane's measured 11-15 minutes is the capture's browser half, unchanged. Its comment states why 2 is required (a Chromium, a Kestrel host and a restored database per test on four vCPUs) — that cap stays. |
| `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs` | The verify. One `[Fact]`, `Category=SqlServer`, that returns immediately unless `PEGASUS_TEST_UI_MODE` is set. `Generate` (l.115-169) asserts **every** visual state in `catalogue.json` matched a captured response; the orphan check (l.89-93) fails any committed page no state generates; `VerifyOfflineBrowserRenderAsync` (l.97-113) renders each committed page in one Chromium. This is the gate's guarantee and nothing here may weaken it. It is also why a *partial* capture cannot work: an uncaptured state fails `Generate`, and its committed page then fails the orphan check. |
| `tests/Pegasus.IntegrationTests/TestUiResponseCapture.cs` | The capture middleware. Content-addressed by `SHA256(request + html)`, with `WriteOnceAsync` (l.91-109) staging into a GUID directory and `Directory.Move`-ing it into place, dropping an identical second arrival. That is what makes two sequential test invocations writing into one capture directory safe, and why the directory is wiped once before the first half and never between them. |
| `scripts/Test-UiCatalogue.ps1` | The other half of the gate — manifest, prototype, orphan and broken-reference validation with no build and no tests, running in the `documentation` lane in seconds. Unaffected, but it is the cheap check the runner re-runs. |
| `scripts/Invoke-TestShard.ps1` | The existing sharding port: enumerate the project's tests for a filter, assign whole classes to shards, fail a shard that ran fewer than it was assigned. Read it to understand what a matrix capture would cost — it is the rejected option's machinery, not this plan's. |
| `docs/design/README.md` § Test UI (l.43-64) | The governing statement of what the catalogue is, what regenerating it means, and the documented local commands. Every sentence there must remain true after the change. |

## Ripple effects

- The `test-ui` job's wall clock and `timeout-minutes` change. No other lane's selection changes: `sql-integration`, `browser` and `documentation` are untouched.
- Nothing under `docs/design/test-ui/` is regenerated. This ticket changes no Razor page, so the committed catalogue must be byte-identical before and after; a diff there is a defect, not an output.
- `docs/runbook.md` gains one sentence in the parallelism paragraph. No other document's statements are falsified.
- Two false comment statements in `ci.yml` are corrected: the capture does **not** run "at processor-count parallelism" (it is pinned to 2), and verify is **not** "a second pass on top" of the 414-test suite (it is a single `[Fact]` selected by `FullyQualifiedName~TestUiSnapshotTests`). The same two claims appear in the ticket body and are corrected in the plan's Starting state.

## Out of scope

- Every file under `docs/design/test-ui/` — the `test_ui_catalogue` lock's files. No page change, so no re-capture.
- Every file under `src/` and `tests/`, including `tests/Pegasus.IntegrationTests/xunit.runner.json`. The saving comes from *removing* an override, not from adding a knob.
- Narrowing the capture filter to a minimal covering set: architecturally incompatible with the all-states assertion and the orphan check (see Context files); evaluated and rejected in the plan.
- Scoping the capture from the `changes` job's outputs, sharding it across a matrix, and reusing the `browser` lane's run — all evaluated and rejected in the plan with reasons.
- Raising the timeout. The ticket forbids it; this plan lowers it from a measurement.
