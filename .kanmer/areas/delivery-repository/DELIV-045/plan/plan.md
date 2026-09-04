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

   Re-check `docs/design/README.md`'s source-and-runtime map against those
   callers without editing it. Stop and report an unsupported claim, missing
   caller, incorrect route status, stale map, or any required change outside
   `docs/current-architecture.md`.

3. **Update the as-built snapshot and validate documentation.** Reuse the
   existing `Current callers and entry points`, `Database and migration
   boundary`, and `Implementation map` sections in
   `docs/current-architecture.md`; no new architecture taxonomy or parallel
   list is needed. Replace the superseded Case/Assessment wording with
   concise caller-and-store facts established in step 2. Keep
   deployment/runtime claims out of the document and leave them to
   `docs/operations.md`. Reuse `scripts/Test-DocumentationLinks.ps1` and its
   `Remove-CodeSpans` link-scanning logic.

   Run:

   ```powershell
   dotnet restore ./Pegasus.slnx --locked-mode
   dotnet build ./Pegasus.slnx --configuration Release --no-restore
   dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build
   dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build
   pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1
   ```

   This docs-only ticket changes no integration-test class, so no
   `FullyQualifiedName~<Class>` integration filter is applicable. Do not run
   the full integration or browser suites locally; GitHub CI is the
   full-suite gate.

4. **Record the unreconciled release boundary and open the PR.** Reuse
   `scripts/Test-MainBranchHistory.ps1`/its `Invoke-Git` helper as a
   diagnostic, plus `gh pr create` and `gh pr view`. Confirm `origin/main`
   is still `32f8679d…`, identify its two commits absent from `dev`, and
   record that the history guard cannot pass until an administrator
   reconciles `main`. The expected failing diagnostic is evidence of the
   pre-merge condition, not a passing release check.

   Create the `dev` → `main` PR body with one row per EPIC-012 ticket:
   ticket ID, Outcome summary, PR number, and merge SHA; include UIIMP-014's
   final verification SHA and proof location. State both pre-merge
   conditions verbatim:
   1. an administrator must reconcile the two direct `origin/main` commits
      absent from `dev`; and
   2. merge requires explicit `MERGE AUTH GRANTED`.

   Verify the PR is `OPEN`, has base `main`, head `dev`, contains that table
   and both conditions, and has no `mergedAt` value. Write the
   post-implementation report through the existing Kanmer ticket-document
   mechanism, then move DELIV-045 one boundary to Review. Never invoke
   `gh pr merge`.

**Acceptance conditions**

- `docs/current-architecture.md` reports only claims proven from the final
  merged tree.
- The design source/runtime map has been checked; any mismatch is reported,
  not silently changed.
- Documentation links pass locally and the documentation CI lane is green on
  the exact PR head.
- The release PR is open, unmerged, targets `main` from `dev`, includes
  every EPIC-012 ticket/PR/SHA plus the final proof pointer, and states both
  conditions.
- DELIV-045 is in Review.

**Binding design and engineering rules:** no explanatory copy; presentation
labels remain solely in `Presentation/OperatorLabels.cs`; preserve exact
states and absent-versus-disabled distinctions; Core remains the policy
owner; one list per concept; tests prove claims without weakened assertions;
no packages; keep the documentation diff proportional.

**Stop condition:** the PR is open and unmerged, CI is green, the
post-implementation report is recorded, and DELIV-045 is in Review. Stop
there — do not merge the release PR or begin deployment work.
