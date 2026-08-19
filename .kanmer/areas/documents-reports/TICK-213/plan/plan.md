# Plan — TICK-213: Decide whether density applies to all rendered document bodies

## Approach

Treat TICK-213 as a decision-only prerequisite already subsumed by [[SIMPLI-014]], not as a separate renderer change. The existing engine proves density is per-template: only the inactive market-valuation template has a one-page `FitToPages` target, while all other templates resolve Auto to Normal and flow naturally. The approved initial application surface is only the rendererref1 assessment and fee-note family, whose design specifies fixed house styling and photo continuation but no universal page-count target. SIMPLI-014 already owns the exact overlapping Core contract, migrated renderer descriptor/algorithm, CSS/templates, visual tests, and representative Chromium render. It must implement normal/default styling with clean page flow, omit caller-selectable density, and retain auto-fit mechanics only if useful as an internal per-template facility—not activate it speculatively.

## Governing docs

- **Meets — `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`.** The initial assessment/fee-note behaviour uses accepted design assets and fails closed on incomplete or unaccepted content. Normal styling with clean page continuation preserves that contract; automatic compaction without an accepted per-template target would invent behaviour. SIMPLI-014 owns any authorized FRD-11 activation wording; TICK-213 makes no separate FRD change.
- **Meets — `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md`.** Per-template density remains Infrastructure rendering mechanics behind the Core-owned port. Density does not leak into a Core business contract, UI, API, MCP, or separate renderer surface. No new ADR or ADR modification is required.
- **Shared EPIC-004 constraint.** `reference/rendererref1/` is acceptance evidence rather than runtime policy. Its normal house style and samples guide parity, while future page targets require separately accepted evidence.

## Steps

1. Confirm that SIMPLI-014's final plan/checklist retains the TICK-213 disposition: approved assessment/fee-note templates render at Normal/default styling and flow cleanly across pages; Core and all callers have no density/fit option; no global shrink-to-fit rule is introduced; and internal per-template auto-fit is activated only by an accepted descriptor with a tested page target. Reuse SIMPLI-014 as the sole implementation owner.
2. After SIMPLI-014's independently reviewed PR is merged, inspect its exact merged Core/Infrastructure/template/test diff for this acceptance slice: no density control crosses the Core port or composition boundary, assessment/fee-note descriptors have no fit target, fixed typography remains aligned with rendererref1, and overflow is continued rather than clipped or silently compacted.
3. Inspect SIMPLI-014's representative visual/PDF evidence and stress cases for long repair lists and supplied photos. Confirm normal density, stable furniture/traceability, clean additional pages, no clipping, and no unnecessary multi-pass Chromium rendering for the active family. Any future template-specific fit target remains deferred to its own accepted evidence.
4. Record a no-code post-implementation report and outcome linking the SIMPLI-014 PR, merge commit, visual/render evidence, and proof. State that TICK-213 was subsumed and created no repository branch, worktree, commit, PR, deployment, or cloud action; then complete its remaining Kanmer gates from that evidence.

## Verification

The post-implementation report and eventual proof will cite SIMPLI-014's exact merged PR/commit and record read-only checks on merged `dev`:

- focused source checks proving the Core report contract and application callers expose no density or fit parameter;
- descriptor/renderer checks proving the active assessment/fee-note family uses Normal/default styling with no page target and no global auto-fit;
- focused tests and real Chromium evidence for representative rendererref1 variants plus long repair-list/photo overflow, asserting clean continuation, no clipping/placeholders, retained page furniture, and reported page count;
- negative evidence that no UI/API/MCP/caller-selectable density surface exists;
- confirmation that TICK-213 itself has no repository commit, PR, worktree, deployment, or cloud action.

The final visual/mechanical acceptance cannot be proved until SIMPLI-014's migrated renderer is merged. TICK-213 owns only the decision and acceptance slice; SIMPLI-014 owns all source, template, CSS, test, and artifact changes.

## Risks / open questions

- **Active overlap:** all expected TICK-213 change files are inside SIMPLI-014's claimed Core/Infrastructure/template/test surface. Mitigation: no independent worktree or diff.
- **Silent visual regression:** output might remain valid while typography is compacted or content clips. Mitigation: inspect structural assertions and representative/stress PDF evidence, not build success alone.
- **Unnecessary render cost:** global auto-fit can cause up to three Chromium passes. Mitigation: assert no fit target for the active family and one normal render path.
- **Future template targets:** a later capability may legitimately need fit-to-pages. Mitigation: preserve a small internal per-template seam only where useful; require accepted visual evidence before activation.
- **Operator questions:** none remain; current evidence and the approved activation subset resolve the decision.
