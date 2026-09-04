# Plan — DELIV-045 (2026-09-04, gpt-5.6-terra high)

Plan only; no repository or board files were changed. Current board state
confirms DELIV-045 must wait for UIIMP-014 and all EPIC-012 work to reach
`origin/dev`.

## DELIV-045 implementation plan

**Objective:** Refresh only `docs/current-architecture.md` from the fully
merged EPIC-012 tree, then open — but never merge — the `dev` → `main`
release PR.

**Expected repository file**

| Action | Path | Purpose |
| --- | --- | --- |
| Modify | `docs/current-architecture.md` | As-built v2 Case workspace snapshot |

**Do not modify:** `docs/operations.md`, `docs/design/README.md`, source,
tests, migrations, scripts, CI, `OperatorLabels.cs`, or any new Markdown
file. The design README is inspection-only. A discovered defect is a
dependency/finding, not this ticket's work.

1. **Freeze the final evidence set before editing.** Reuse the Kanmer
   group/ticket records (`get_group EPIC-012`, each ticket's Outcome and
   proof documents) and the release verification blocker UIIMP-014. Require
   every EPIC-012 ticket to be Done, merged to the same reachable
   `origin/dev` history, and to provide its PR number and merge SHA. Take
   UIIMP-014's recorded final verification SHA and `proof/proof.md` pointer;
   stop if either is absent or disagrees with `origin/dev`. Use
   `git ls-remote --heads origin`, `git merge-base --is-ancestor`,
   `git show`, and targeted `rg` against that merged tree. No repository
   files change in this step.

2. **Prove each architecture claim from its production caller before
   writing it.** Reuse the existing Case Web caller family under
   `src/Pegasus.Web/Pages/Cases/`, the section-route registration in
   `src/Pegasus.Web/Program.cs`, `OperatorLabels`, Core's
   `AiJobKind.MarketResearch`, and the committed migration stream under
   `src/Pegasus.Infrastructure/Persistence/Migrations/`. Verify with
   targeted `rg`/`Get-Content` that the merged tree contains:
   - the one-scroll `/Cases/{id}` record and ordered eleven sections;
   - `/Cases/{id}/Section` fragment behaviour and `?section=` navigation;
   - the permanent Assessment 301 to `?section=estimate`;
   - the Engineer-notes, sign-off, storage-location, valuation guide-month,
     report-image-curation, and vehicle-record persistence;
   - MarketResearch's job/valuation/custody path;
   - Awaiting instruction as a Pre-Case Cases queue; and
   - Operations without the Service health table, retaining only the
     required Administration link/notice state.

   Re-check `docs/design/README.md` without editing it. Its
   source-to-runtime maps are the three asset tables — `#### Pegasus marks
   source-to-runtime mapping`, `#### Logo source-to-runtime mapping` and
   `#### Lucide icons source-to-runtime mapping` — which pair an upstream
   source path and SHA-256 with a runtime path under
   `src/Pegasus.Web/wwwroot/`; the check is that each recorded runtime file
   still exists at that path with the recorded SHA-256 (`Get-FileHash`).
   Separately confirm the README's `## Routes` table and its Case-record
   sections already state the D29/D30 shape (they do on `origin/dev`
   80f0ca26: `/Cases/{id}` one scrolling page, `/Cases/{id}/Assessment` a
   301 to `?section=estimate`), so no README edit is implied. Stop and
   report an unsupported claim, missing caller, incorrect route status,
   stale map, or any required change outside `docs/current-architecture.md`.

3. **Update the as-built snapshot and validate documentation.** Reuse the
   existing `Current callers and entry points`, `Database and migration
   boundary`, and `Implementation map` sections in
   `docs/current-architecture.md`; no new architecture taxonomy or parallel
   list is needed. Replace the superseded Case/Assessment wording with
   concise caller-and-store facts established in step 2 — the stale text on
   `origin/dev` is at least the `Case workspace and its capability pages`
   row of the implementation map (still describing
   `Pages/Cases/Assessment/Index.cshtml.cs` as a live page) and the
   `Operations workspace subsystems` and EVA paragraphs. Keep
   deployment/runtime claims out of the document and leave them to
   `docs/operations.md`. Reuse `scripts/Test-DocumentationLinks.ps1` and its
   `Remove-CodeSpans` link-scanning logic.

   Run:

   ```powershell
   pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1
   ```

   The diff is one Markdown file, so `Get-CiChangeFlags.ps1` sets
   `build=false` and CI path-skips every build and test lane; a local
   locked restore, Release build or `dotnet test` would prove nothing about
   this diff and is not run. The CI `documentation` lane is the gate and
   runs on every change set: `Test-TestMarkdownPlacement.ps1`,
   `Test-DocumentationLinks.ps1` and `Test-UiCatalogue.ps1`. This ticket
   adds no Markdown file and touches no catalogue, so those two need no
   local run. Do not run the integration or browser suites locally.

4. **Land the docs change on `dev`, then record the unreconciled release
   boundary and open the release PR.** The task branch's own PR into `dev`
   is opened and reviewed first under the normal workflow; DELIV-045 does
   not merge it. Because the `dev` → `main` PR shows a live diff, it may be
   opened before that merge — the ticket table carries a DELIV-045 row
   marked open-and-pending, and no promotion SHA is asserted in it. Reuse
   `scripts/Test-MainBranchHistory.ps1`/its `Invoke-Git` helper as a
   diagnostic, plus `gh pr create` and `gh pr view`. Confirm `origin/main`
   is still `32f8679d…` (two commits `origin/dev` lacks, read-only check:
   `git rev-list --count origin/dev..origin/main` → 2) and record that the
   history guard — which requires the pushed `main` head to be an ancestor
   of `origin/dev` — cannot pass until an administrator reconciles `main`.
   The expected failing diagnostic is evidence of the pre-merge condition,
   not a passing release check.

   Create the `dev` → `main` PR body with one row per EPIC-012 ticket:
   ticket ID, Outcome summary, PR number, and merge SHA; include UIIMP-014's
   final verification SHA and proof location. State all three pre-merge
   conditions verbatim:
   1. an administrator must reconcile the two direct `origin/main` commits
      absent from `dev`;
   2. merge requires explicit `MERGE AUTH GRANTED`; and
   3. the promotion itself is the exact-SHA atomic fast-forward push of
      `docs/engineering.md` § *Branches and delivery* — a GitHub PR, rebase
      or squash merge is explicitly **not** an exact-SHA promotion, so this
      PR is the review and record vehicle only and is never merged through
      the GitHub button.

   Verify the PR is `OPEN`, has base `main`, head `dev`, contains that table
   and all three conditions, and has no `mergedAt` value. Write the
   post-implementation report through the existing Kanmer ticket-document
   mechanism, then move DELIV-045 one boundary to Review. Never invoke
   `gh pr merge`.

**Acceptance conditions**

- `docs/current-architecture.md` reports only claims proven from the final
  merged tree.
- The design source/runtime maps and route table have been checked; any
  mismatch is reported, not silently changed.
- Documentation links pass locally and the documentation CI lane is green on
  the exact PR head.
- The release PR is open, unmerged, targets `main` from `dev`, includes
  every EPIC-012 ticket/PR/SHA plus the final proof pointer, and states all
  three conditions.
- DELIV-045 is in Review.

**Binding design and engineering rules:** no explanatory copy; presentation
labels remain solely in `Presentation/OperatorLabels.cs`; preserve exact
states and absent-versus-disabled distinctions; Core remains the policy
owner; one list per concept; tests prove claims without weakened assertions;
no packages; keep the documentation diff proportional. This ticket changes
no command or convention, so rule 24 requires no AGENTS.md/CLAUDE.md edit.

**Stop condition:** the PR is open and unmerged, CI is green, the
post-implementation report is recorded, and DELIV-045 is in Review. Stop
there — do not merge the release PR or begin deployment work.

## Plan review (2026-09-04, Claude Opus)

Read: ticket body, this plan, EPIC-012 `context.md` (§ Build policy, D29–D50),
and on `origin/dev` 80f0ca26 — `docs/current-architecture.md`,
`docs/design/README.md`, `docs/engineering.md` § Branches and delivery,
`.github/workflows/ci.yml`, `scripts/Get-CiChangeFlags.ps1`,
`scripts/Test-DocumentationLinks.ps1`, `scripts/Test-MainBranchHistory.ps1`.

| # | Finding | Evidence | Disposition |
| --- | --- | --- | --- |
| 1 | The PR body's two conditions implied the release could be merged by the GitHub button. `docs/engineering.md` states a PR/rebase/squash merge "is not an exact-SHA promotion"; the promotion is the atomic `--force-with-lease` fast-forward push. | `docs/engineering.md` § Branches and delivery | Fixed — step 4 now states a third condition naming that procedure. |
| 2 | Step 2 described the design README's "source-and-runtime map" as something to check "against those callers". Its three `source-to-runtime mapping` tables are asset maps (marks, logo, Lucide sprite) pairing paths with SHA-256, unrelated to Case callers. | `docs/design/README.md` L223, L492, L528 | Fixed — step 2 names the three tables and the `Get-FileHash` check, and adds the route-table confirmation the ticket's "check only" actually implies. |
| 3 | Step 3 required a locked restore, Release build and two test projects for a one-Markdown-file diff. `Get-CiChangeFlags.ps1`'s build pattern matches neither `docs/current-architecture.md` nor `docs/`, so CI path-skips every build lane; the ticket's own Verification lists only `Test-DocumentationLinks.ps1`. | `scripts/Get-CiChangeFlags.ps1` `$buildPattern`; `.github/workflows/ci.yml` L79–98 | Fixed — dropped; local run is `Test-DocumentationLinks.ps1`, with the documentation lane's full contents named. |
| 4 | Ordering gap: the `dev` → `main` PR cannot contain this ticket's own docs change until the task PR merges to `dev`, and DELIV-045 may not merge its own PR. | Repository task workflow § 5 | Fixed — step 4 states the docs PR lands first under normal review, the release PR may be opened before that merge because its diff is live, and the DELIV-045 row is marked open-and-pending with no asserted promotion SHA. |
| 5 | Step 3 said "replace the superseded Case/Assessment wording" without naming a location. | `docs/current-architecture.md` L690 still describes `Pages/Cases/Assessment/Index.cshtml.cs` as a live page; L328, L580 carry Assessment-workspace prose | Fixed — step 3 names those anchors as the minimum stale set. |
| 6 | Facts checked, not argued: `origin/main` = `32f8679d3`, `git rev-list --count origin/dev..origin/main` = 2; `Test-MainBranchHistory.ps1` fails because the pushed `main` head must be an ancestor of `origin/dev`. Ticket premise confirmed. | read-only `git ls-remote` / `rev-list` | No change — premise holds; the failure mode is now stated precisely in step 4. |

Not findings: scope stays inside the owned paths (one file edited, README
inspected); every step names what it reuses; no package, no new abstraction,
no new Markdown file; rule 24 does not bite because no command or convention
changes. No operator-only question arose — the `main` reconciliation is
already an administrator action recorded by the PR, not a blocker on this
plan.

**Verdict:** approved with the fixes above applied.
