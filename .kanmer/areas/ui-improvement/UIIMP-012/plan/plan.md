# Plan — UIIMP-012: Rename the Triage history panel to "Notes" and narrow D7 to uncomposed integrations

*The plan. Not the checklist — reasoning establishes bounded work; the checklist distils it into independently observable actions.*

**Diff estimate: 3 hand-edited files, ≈ +20 / −11 lines, plus 1 committed generated
artifact expected to come back byte-identical. No new file, no new dependency, no
schema, no route, no handler.** If the hand-edited diff exceeds ~35 changed lines or
reaches a fourth source file, stop and report.

## Objective

The Triage record's panel heading reads **Notes** — the name EPIC-011 §1.5 and D25
give it — with the string owned by `OperatorLabels`, the pinned assertion retargeted
in the same diff, and the Test UI artifact reconciled. Entries keep their shape and
their permanence.

## Starting state

- `origin/dev` @ `9b8f78a36151313bc6d48625edee7f13a2173127`.
- `src/Pegasus.Web/Pages/Triage/Details.cshtml:400` renders
  `<div class="panel-head"><h2 id="triage-history-title">Permanent history</h2></div>`
  inside `<section class="panel section-gap" aria-labelledby="triage-history-title">`
  (line 399). Lines 392–398 are a Razor comment recording the divergence as
  "UIIMP-012's to settle". Entries below are Date / Time / ID + text, newest first.
- The string exists **only** in that markup: `git grep -n "Permanent history" origin/dev`
  returns `Details.cshtml:393` (the comment), `Details.cshtml:400` (the heading),
  `QdosTriageIntegrationTests.cs:477` (the pinned assertion), and
  `ProviderSubmissionTests.cs:432` (a comment about the business concept).
  `OperatorLabels.cs` has no panel label for this surface.
- `docs/design/test-ui/pages/triage-details--default.html` on `dev` is the styled
  not-found page, not a rendered Triage record (see Files → Ripple effects).
- EPIC-011 `context.md` D7 already carries "**Narrowed 2026-09-01 (UIIMP-012)**"; §1.5
  already names the panel Notes. `docs/design/README.md` §Absent versus disabled already
  draws the same distinction and is being rewritten by DELIV-040's open PR #643.
- Evidence: this ticket's `files` document (`files`, written this session, same run);
  EPIC-011 `context.md` as read on 2026-09-02; PR #643 head
  `task/deliv-040-governing-docs` as fetched on 2026-09-02.
- Workspace: worktree `C:\Users\PGUSER\Documents\github\pegasus-worktrees\uiimp-012-notes-panel`
  on `task/uiimp-012-notes-panel`, cut from `origin/dev`.

## Governing docs

- `docs/frd/frd-03-triage.md` — **Meets.** FRD-03 speaks of the record's *permanent
  history* as the durable event record ("it never overwrites history"; "remain in
  permanent history"). The rename touches a heading, not the record: every entry stays
  durable, ordered and undeleteable. No FRD-03 sentence names a heading, so nothing is
  modified.
- `docs/frd/frd-12-operator-experience.md` — **Meets.** Line 184: "Triage detail carries
  the determinations (roadworthiness, repair outcome), the source facts and **notes**".
  The heading "Notes" is what FRD-12 already describes; "Permanent history" was the
  divergence.
- `docs/design/README.md` §Absent versus disabled and rule 6 — **Meets, unmodified.**
  The narrowed D7 (uncomposed integrations only; a state- or permission-gated control
  with a real handler and a named condition is legal) is already the README's text, and
  DELIV-040 holds the `governing_docs` lock on that file until PR #643 merges. The
  docs half of this ticket is therefore **already covered — by the Phase 0 `context.md`
  edit (D7 line 110, D21, §1.5) and by DELIV-040**; this ticket edits no document.
- EPIC-011 `context.md` §1.5 / D25 — **Meets.** The panel is named Notes; the entry
  shape is unchanged; staff notes remain INTK-054's.
- **No new ADR.** Nothing here is a design decision: the operator ruled on 2026-09-01
  and the contract already records it.

## Required changes

1. `OperatorLabels` gains one surface class, `TriageRecord`, with a single member
   `NotesPanel = "Notes"` — the one list for this concept, per EPIC-011's
   "labels live in `Presentation/OperatorLabels.cs`". Named `TriageRecord`, not
   `Triage`, because `docs/design/README.md`'s route table calls the surface "Triage
   record" and because a nested `Triage` would shadow the `Pegasus.Core.Triage`
   namespace this same file qualifies against. Appended at the end of the class; no
   existing member is reordered or edited.
2. `Pages/Triage/Details.cshtml` renders that label in the existing `<h2>`; the string
   literal disappears from markup, leaving exactly one copy in the codebase. The stale
   divergence comment (lines 392–398) is replaced by one line naming §1.5 / D25 and
   INTK-054. The `<section>`, its `aria-labelledby`, the `id`, the panel classes and
   every entry stay exactly as they are.
3. `QdosTriageIntegrationTests.cs:477` asserts the rendered heading element with the new
   name and additionally asserts the old name is gone. The surrounding assertions
   (status 200, "Case unlinked", the ten-event `Assert.Collection`) are untouched.
4. `docs/design/test-ui/` is regenerated by the repository script and whatever it
   produces is committed with the page change, because CI's Test UI lane runs the same
   verify. The expected outcome is *no change* to any artifact.

There is nothing to investigate, decide or choose: the name, the file that owns it, and
the ownership of the docs half are all settled above. Trivial defaults taken without
asking, recorded here: keep `id="triage-history-title"` (a DOM id is not operator-visible
and INTK-054 owns the panel's structure); keep the `notes-list` / `note-entry` classes;
do not harmonise the sibling `History` headings on other pages.

### UI/UX overlay

- **States:** the panel has one state — the entry list. An empty `Model.Triage.History`
  renders the heading with an empty list, exactly as today; this change adds no empty,
  loading, error or disabled state and removes none.
- **Access:** `aria-labelledby="triage-history-title"` must keep pointing at the `<h2>`,
  so the section's accessible name becomes "Notes". No focus order, tab stop or
  keyboard interaction changes. No new control.
- **Responsive constraints:** heading level, panel padding and 40px row rhythm are
  untouched; the string is shorter than the one it replaces, so no reflow risk.
- **Visual proof:** the rendered `/Triage/{id}` HTML contains
  `<h2 id="triage-history-title">Notes</h2>` and no "Permanent history" — proved by
  `QdosTriageIntegrationTests` against a populated record, which is stronger than the
  captured artifact (that route's artifact is the not-found page).
- **Scope boundary:** no redesign travels with the rename — no Add note control, no
  Files view, no sibling-page heading change.

## Expected files

| Action | Repo-root-relative path | Responsibility |
|---|---|---|
| Modify | `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Add the `TriageRecord` nested class with `NotesPanel`. Hand-written. |
| Modify | `src/Pegasus.Web/Pages/Triage/Details.cshtml` | Render the label; delete the stale divergence comment. Hand-written. |
| Modify | `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs` | Retarget the pinned heading assertion. Hand-written. |
| Modify | `docs/design/test-ui/pages/triage-details--default.html` | Committed generated artifact; regenerated by the script, expected byte-identical. |
| Modify | `docs/design/test-ui/index.html` | Committed generated artifact; regenerated from the untouched catalogue, expected byte-identical. |

## Do not modify

- `docs/design/README.md`, `docs/frd/**`, `docs/adr/**`, `docs/open-decisions.md`,
  `docs/capabilities.md`, `docs/boundaries.md` — DELIV-040 / PR #643 owns the governing
  documents until it merges.
- `docs/operator-notes.md`, `AGENTS.md`, `docs/index.md`.
- `docs/design/test-ui/catalogue.json` — the route and its state are unchanged.
- `src/Pegasus.Web/Pages/Unidentified/Details.cshtml`,
  `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml` — other lanes' `History` headings.
- `src/Pegasus.Web/Pages/Triage/Details.cshtml.cs` — no page-model change is needed.
- `tests/Pegasus.Core.Tests/ProviderApi/ProviderSubmissionTests.cs` — its comment uses
  the business concept correctly.
- `src/Pegasus.Web/wwwroot/**` — no style or script change.

## Constraints

- EPIC-011: one list per concept (labels in `OperatorLabels.cs`); no explanatory copy;
  a ticket owns whole files and never touches a neighbour lane's; report what belongs to
  another ticket rather than fixing it.
- `OperatorLabels` is append-only in practice: add the nested class at the end, reorder
  nothing, and give it a `<summary>` naming the EPIC-011 section, as its siblings do.
- The `triage_page` lock: this ticket holds `Pages/Triage/Details.cshtml` for wave A and
  must land before INTK-054 starts.
- CI verifies `docs/design/test-ui/` against a fresh capture, so the artifact directory
  is committed in the same change set as the page.
- LocalDB is absent on this workstation: the SqlServer and Browser lanes cannot run
  locally and are evidenced by CI at the PR head.
- Subagents do not run tests or capture scripts; the controller's test runner does.

## Ordered steps

### Step 1 — Add the Notes panel label to `OperatorLabels`
- Preconditions: worktree on `task/uiimp-012-notes-panel` cut from `origin/dev`; no other
  edit in the tree.
- Files: `src/Pegasus.Web/Presentation/OperatorLabels.cs`
- Change: append, after the last existing nested class, a nested
  `public static class TriageRecord` carrying a `<summary>` that names EPIC-011 §1.5 and
  D25, and one member: `public const string NotesPanel = "Notes";`. Reuse the existing
  nested-surface convention (`CaseWorkspace` at line 1297 is the model) — no new file, no
  new helper, no resource system.
- Preserved behaviour: every existing member keeps its name, value, order and XML
  documentation; the file still compiles for every current caller.
- Forbidden: renaming or re-ordering an existing member; introducing a second copy of
  the string; adding a label for any other surface.
- Negative cases: a nested class named `Triage` (would shadow `Pegasus.Core.Triage`,
  which this file qualifies against) must not be introduced.
- Tests: none of its own — Step 3 proves the rendered value.
- Commands: `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- Expected output: build succeeds, 0 errors.
- Done when: `OperatorLabels.TriageRecord.NotesPanel` exists and the solution builds.
- Deviation stop: any pre-existing `Triage`-named nested class, or a build error outside
  this file.

### Step 2 — Render the label on the Triage record page
- Preconditions: Step 1 done and building.
- Files: `src/Pegasus.Web/Pages/Triage/Details.cshtml`
- Change: line 400 becomes
  `<div class="panel-head"><h2 id="triage-history-title">@OperatorLabels.TriageRecord.NotesPanel</h2></div>`;
  replace the Razor comment at lines 392–398 with a single line recording that the panel
  is the Notes panel of EPIC-011 §1.5 / D25 and that INTK-054 adds staff notes to it.
  Reuse the `OperatorLabels` reference already available in this page (used at lines 59,
  239, 331, 368, 407–408) — no new `@using`.
- Preserved behaviour: the `<section class="panel section-gap"
  aria-labelledby="triage-history-title">` wrapper, the `id`, the `notes-list` /
  `note-entry` markup, the `OrderByDescending` ordering and every entry field.
- Forbidden: any explanatory sentence, empty-state prose, new control, new class, or
  change to the entry rows; changing the `id` or the `aria-labelledby` pairing.
- Negative cases: no rendered output may still contain "Permanent history"; no second
  literal "Notes" heading may be introduced.
- Tests: `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs` (Step 3).
- Commands: `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- Expected output: build succeeds, 0 errors (Razor compiles in the Web project).
- Done when: `git grep -n "Permanent history" -- src` returns nothing.
- Deviation stop: the heading is not where the plan says, or another page renders the
  same string.

### Step 3 — Retarget the pinned assertion
- Preconditions: Steps 1–2 done.
- Files: `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs`
- Change: at line 477 replace
  `Assert.Contains("Permanent history", finalHtml, StringComparison.Ordinal);` with an
  assertion on the rendered element —
  `Assert.Contains("<h2 id=\"triage-history-title\">Notes</h2>", finalHtml, StringComparison.Ordinal);`
  — and add
  `Assert.DoesNotContain("Permanent history", finalHtml, StringComparison.Ordinal);`
  immediately after it. Reuse the existing `IntakeWebApplicationFactory` /
  `IntakeWebDriver` fixtures and the existing test method; add no fixture, no fake, no
  `using`.
- Preserved behaviour: the status-200 assertion, the "Case unlinked" assertion and the
  ten-event `Assert.Collection` above stay exactly as they are.
- Forbidden: weakening to a bare `Assert.Contains("Notes", …)` (the markup already
  contains `notes-list`, so it would pass without the rename); deleting the assertion;
  adding a new test class.
- Negative cases: the test must fail if the heading reverts, and fail if the old string
  reappears anywhere in the response.
- Tests: this file.
- Commands:
  `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~QdosTriageIntegrationTests"`
- Expected output: all tests in the class pass. **Runner only**, and only where LocalDB
  exists; otherwise CI at the PR head is the evidence.
- Done when: the assertion names the new heading and the class is green in CI.
- Deviation stop: the test needs any change beyond these two lines.

### Step 4 — Reconcile the Test UI artifact
- Preconditions: Steps 1–3 committed on the task branch.
- Files: `docs/design/test-ui/pages/triage-details--default.html`,
  `docs/design/test-ui/index.html`
- Change: the runner regenerates the catalogue artifacts with the repository script and
  the implementer commits exactly what it writes. Reuse `scripts/Update-TestUiSnapshots.ps1`
  and `TestUiSnapshotTests` — write no new capture code and add no test to the capture
  filter.
- Preserved behaviour: every other page artifact and `catalogue.json` stay byte-identical.
- Forbidden: hand-editing a generated artifact; adding `QdosTriageIntegrationTests` (or
  any test) to the capture filter to make the Triage route render; changing
  `catalogue.json`.
- Negative cases: if any artifact **other than** the Triage page changes, stop — that is
  another lane's drift.
- Tests: the catalogue verify and check named in Commands.
- Commands: the capture, the verify and the catalogue check in the Commands section
  below (runner only).
- Expected output: the verify passes. The Triage artifact is expected **unchanged**,
  because no test inside the capture filter renders a populated Triage record and the
  committed artifact for that route is the styled not-found page; an unchanged file is a
  pass, not a miss.
- Done when: `git status` is clean after the verify run, and CI's Test UI lane is green
  at the PR head.
- Deviation stop: the verify fails, or an artifact outside `triage-details--default.html`
  changes.

## Acceptance checks

- Production caller: the routed Razor page `/Triage/{id:guid}`
  (`src/Pegasus.Web/Pages/Triage/Details.cshtml`) is the only caller of the new label;
  no registration, DI entry or route changes.
- No runtime dependency, packaging, schema, migration, grant or role change — the diff
  is one label, one heading and one assertion.
- The assertion proves the claim without weakening: it pins the rendered element, not a
  substring that other markup already satisfies, and pairs with a `DoesNotContain` on the
  retired name.
- `git grep -n "Permanent history" -- src tests` returns only
  `tests/Pegasus.Core.Tests/ProviderApi/ProviderSubmissionTests.cs:432` (a comment about
  the business concept, deliberately unchanged).
- The string "Notes" exists once as a Triage panel label, in `OperatorLabels.cs`.
- `docs/design/test-ui/` is committed in the same change set as the page; CI's Test UI
  lane is green at the PR head.
- No governing document is in the diff.

## Commands

Run from the task worktree
`C:\Users\PGUSER\Documents\github\pegasus-worktrees\uiimp-012-notes-panel`, in `pwsh`.
The implementer runs only the build; every `dotnet test` and every capture script below
is **the test runner's**.

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~QdosTriageIntegrationTests"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Browser&Category!=Corpus" -- xUnit.MaxParallelThreads=2
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
```

- There is no Triage-specific Browser test class (`tests/Pegasus.IntegrationTests/Browser/`
  holds Accessibility, LayoutIntegrity, OperatorJourney, Mail, Upload and Assessment
  classes only), so the Browser lane runs unfiltered; the focused Triage evidence is the
  `QdosTriageIntegrationTests` filter above, whose class is `Category=SqlServer`.
- **LocalDB is absent on this workstation**, so the SqlServer, Browser and capture lanes
  cannot run here: their evidence is CI at the PR head. Record them
  `NOT_APPLICABLE (no LocalDB locally; CI-evidenced at <head sha>)`, never `PASS`.
- Post-merge: none beyond the merged `dev` build and Test UI lane.

## Failure and deviation rules

Stop and report — do not improvise — on: a failing build or test; the heading, the
comment or the assertion not being where Starting state says; any need to touch a file
outside Expected files (including a governing document, `catalogue.json`, a sibling
page's `History` heading, or the capture filter); a Test UI artifact other than the
Triage page changing; a merge conflict in `Pages/Triage/Details.cshtml` (the
`triage_page` lock is held for wave A); or a request to fix why the Triage route's
captured artifact is the not-found page. Deviations are reported, never silent
redesigns. Refresh the branch only with `git merge --no-edit origin/dev`.

## Simplification pass

Required before the PR opens (AGENTS.md, Repository task workflow §4): run the pass over
the branch's own diff — reuse, simplification, efficiency, altitude — apply the
behaviour-preserving fixes, and record findings and dispositions here under a dated
`### <YYYY-MM-DD>` heading, naming any unapplied finding with its reason or ticket. This
is a code change, so "n/a — docs-only" does not apply. Expect it to be short: the
diff is one label, one heading and one assertion.

### <YYYY-MM-DD> — to be completed by the implementer

## Stop condition

PR_OPEN: PR to `dev` titled "Rename the Triage history panel to "Notes" and narrow D7 to uncomposed integrations (UIIMP-012)", footer `Kanmer: UIIMP-012`, ticket moved implementing → review. Do not merge, do not start another ticket, do not begin INTK-054.
