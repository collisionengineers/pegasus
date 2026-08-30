## 2026-08-30 — merged last by design; the gate is proven, three ways

This PR was held to the end of the EPIC-011 closeout deliberately: it adds the
Test UI snapshot gate to CI, and merging it before the Razor-changing lanes
would have turned every one of them red, because none carried a regenerated
corpus.

### The gate gates — proven, not asserted

Three independent demonstrations, the third the strongest:

1. **Perturbation.** One character changed in a committed page
   (`"No cases match these filters."` → `"...filterZ."`) →
   `Update-TestUiSnapshots.ps1 -Verify -SkipCapture` exits **1**,
   "Generated Test UI file is stale: pages/cases--empty.html".
2. **Orphan injection.** A copied prototype → snapshot verify exits **1**
   ("Committed Test UI pages no state generates"), and `Test-UiCatalogue.ps1`
   exits **1** ("Prototype is not linked by the inventory").
3. **Real CI.** On commit `c4217eb9` the **only** failing job of twelve was
   `test-ui` — it caught a stale corpus on GitHub unaided — and it passes on
   `3d84fce9`. The gate demonstrably works where it has to work.

Exit codes were read from redirected files, never through a pipe. That mattered
elsewhere in this programme: `Test-UiCatalogue.ps1` writes errors and then
throws, so a piped read reports the pipe's status and shows 0 on a real failure.

### The finding that was not in the brief

**The corpus was embedding wall-clock time.** `_Layout.cshtml` renders both
freshness clocks from render time, and one capture host
(`AutomationConnectorAuthorizationTests`) deliberately uses
`TimeProvider.System`, so it cannot be pinned. Every captured page therefore
carried the minute it was generated, and the corpus was stale the instant it was
written — which is exactly why the first regeneration failed `test-ui` in CI.

Normalised to `{{office-clock}}` through a `LayoutClockRegex` scoped to
`<span>Current · HH:MM</span>`, deliberately narrow so the mail freshness banner
(`<time datetime=…>`, a genuine last-sync value that *should* differ between
pages) is untouched. Two `TriageQueuesWebTests` fixtures were also stamping
receipts with `DateTimeOffset.UtcNow` while their host runs on the fixed
2031-05-06 clock; a third such stamp, missed by the first sweep, was found by
verification and fixed.

**A snapshot gate that bakes in the clock is self-defeating** — it would have
been red on every run, and read as a stale corpus rather than as its own defect.

### No lost work — refuted at three levels

The merge touched ten hand-written test files with substantial changes on both
sides, and a dropped test fails nothing. Checked independently, twice:

- **File level:** 260 `.cs` files under `tests/` on both `origin/dev` and HEAD;
  `git diff --name-status` shows ten `M` and zero `A`/`D`.
- **Method level:** every `public async Task` declaration extracted from both
  sides and the sorted sets diffed. None lost. `CaseDetailsWebTests` 21 → 22.
- **Body level:** `ASaveCarriesTheClaimantContactNumberAndAddressThroughToTheCommand`
  — the test pinning the production data-loss fix — confirmed present.

`OrganizationAdministrationWebTests.cs` deserves note: git **auto-merged it into
broken code**, because both sides added an EVA submission fetch to the same
method, producing a duplicate local and a compile error. An auto-merge is not a
resolution.

### Verification findings, disposed

- *medium* — the new job had **4.5% timeout headroom** (30 minutes, observed at
  28m38s). It would have begun failing on timeout and been read as a stale
  corpus. **Fixed**, raised to 45.
- *low* — a dead `StateMatches` entry for a page `dev` deleted. Rule 21.
  **Fixed**, removed.
- *low* — one more `DateTimeOffset.UtcNow` in the fixture already pinned twenty
  lines above. **Fixed.**
- *low* — `inbox--unavailable`'s marker matches the mailbox freshness chip
  rather than a failed mail query, so its prototype documents the behaviour its
  own catalogue branch says must not happen. **Pre-existing on `dev`**, not this
  ticket's scope, recorded here so it is not lost.
- *low* — the lane's report claimed the catalogue exit code came from
  `$LASTEXITCODE`, but in-process `&` invocation leaves that unset. An error in
  the stated **evidence method**, not in the code; the conclusion was confirmed
  three other ways. Worth remembering as a reporting trap.

Re-verified after those fixes rather than assumed: full fresh capture and verify
exit 0 with no drift, catalogue gate exit 0 (54 routed sources, 58 prototypes),
Release build clean, and `git status` showed **no snapshot file changed** — so
the fixture clock fix altered no captured page.
