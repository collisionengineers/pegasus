# Plan — TICK-211: Decide report-renderer analyzer strictness

## Approach

Treat TICK-211 as a decision-only prerequisite already subsumed by [[SIMPLI-014]], not as an independent repository implementation. Current `dev` proves the authoritative production convention in root `Directory.Build.props`: `AnalysisLevel=latest-recommended` and `TreatWarningsAsErrors=true`. The workspace-local exception disables warnings-as-errors, suppresses CS1591, and carries standalone product metadata only because the renderer is still an independently built non-caller import. Once migrated, that boundary disappears. SIMPLI-014's active plan and checklist already own removal of the workspace props, inheritance of root policy, warning fixes, dependency reconciliation, locked restore, Release build, and CI proof. A separate TICK-211 branch would edit the same project/source/build files and is prohibited overlap.

## Governing docs

- **Meets — `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md`.** Integrated renderer source becomes application code in the existing repository/project boundary, not a standalone product or package. Inheriting the root analyzer, warning, version, and repository metadata policy is the direct consequence; no new ADR or ADR modification is required.
- **Meets — `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`.** FRD-11 governs report behaviour, accepted inputs, finality, and failure boundaries. Analyzer strictness changes none of that behaviour; SIMPLI-014 must preserve it while fixing migration warnings. No FRD modification is authorized or needed.
- **Shared EPIC-004 constraint.** Infrastructure adapts the renderer inside the monolith. A renderer-specific quality-policy enclave or surviving standalone product identity would contradict the binding integration direction.

## Steps

1. Confirm that SIMPLI-014's final plan/checklist retains the TICK-211 disposition: migrated production code inherits root `latest-recommended` analysis and warnings-as-errors; surfaced warnings are fixed; broad renderer-specific relaxations, CS1591 carry-over, and standalone CollisionRenderer version/product/repository metadata are not introduced into the application. Reuse SIMPLI-014 as the sole implementation owner.
2. After SIMPLI-014's independently reviewed PR is merged, inspect its exact merged diff and build evidence for this acceptance slice: the workspace `Directory.Build.props` is retired with the workspace, application projects continue to inherit the root policy unchanged, any narrow suppression is tied to a concrete false positive at the smallest scope, and locked Release build/CI succeeds without a renderer-wide relaxation.
3. Record a no-code post-implementation report and outcome linking the SIMPLI-014 PR, merge commit, relevant build output, and proof. Explicitly state that TICK-211 was subsumed and created no repository branch, worktree, commit, PR, deployment, or cloud action; then use that evidence to complete its remaining Kanmer gates.

## Verification

The post-implementation report and eventual proof will cite SIMPLI-014's exact merged PR/commit and record read-only checks on merged `dev`:

- inspect root `Directory.Build.props` for `AnalysisLevel=latest-recommended` and `TreatWarningsAsErrors=true`;
- confirm the integrated renderer projects do not override either setting to a weaker value and do not retain broad CS1591/renderer-wide suppression or standalone product/version/repository metadata;
- inspect any new suppression with its concrete diagnostic and smallest-scope justification;
- cite SIMPLI-014's successful locked restore, Release build, focused tests, full tests, and CI evidence;
- confirm TICK-211 itself has no repository commit, PR, worktree, deployment, or cloud action.

The final analyzer acceptance cannot be proved until SIMPLI-014's migrated code is merged. TICK-211 owns the decision and acceptance slice only; SIMPLI-014 owns every source/build change.

## Risks / open questions

- **Active overlap:** TICK-211's surveyed change files are all inside SIMPLI-014's claimed workspace removal, Infrastructure migration, source-warning, project, and CI surface. Mitigation: no independent diff or worktree.
- **Hidden warning debt:** migration may surface warnings. Mitigation: fix them within SIMPLI-014; permit only concrete, narrow false-positive suppressions with recorded rationale.
- **Accidental policy weakening:** a project-local override could make CI green while evading root policy. Mitigation: inspect effective project settings and the merged diff, not merely a successful build.
- **Operator questions:** none remain; repository governance resolves the technical choice.
