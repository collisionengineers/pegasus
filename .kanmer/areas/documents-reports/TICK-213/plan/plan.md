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


## Re-plan — 2026-08-19: close the missing stress-evidence slice

Merged source and the existing four-outcome Chromium test prove normal active-template markup, no caller density option, and the single `PdfAsync` path, but the existing snapshot has only one entry in each work list and one photo. It cannot prove the checklist's long-list/multi-photo continuation claim.

Add one verification-only Browser test beside the existing renderer integration test. Reuse its composed application path and real supplied image fixture; create long uniquely labelled new-part/repair/operation lists and multiple independently hashed photo evidence entries. Assert the assessment spans additional pages, every terminal work-list label remains extractable, the final Statement of Truth remains present, every page retains the report reference/page furniture, multiple images are embedded, and no unresolved placeholder appears. The multi-page result is the behavioural proof that content flows normally instead of global shrink-to-fit. Source inspection remains the proportional proof that rendering calls Chromium once per fixed assessment/fee-note artifact and exposes no density selector.

No production/CSS/template change is authorized. If the stress test exposes clipping or missing furniture, stop and create a blocking defect ticket rather than widening this ticket.

## Simplification pass

Run after the focused test change. The expected disposition is test-only reuse of the existing snapshot/composition helpers, with no new production abstraction.


## Blocking result — 2026-08-19

The real-Chromium stress reproduction exposed a production defect before TICK-213 could reach Review. With 80 uniquely labelled entries in each of the three work lists and 8 accepted hashed photos, the multi-page assessment retained all terminal `080` list entries but omitted the later `Statement of Truth` section from extracted PDF text. The representative one-item/one-photo suite still passes.

[[PR-009]] now blocks TICK-213 and owns diagnosis/correction under FRD-11. Per the re-plan, no renderer, template, CSS, or other production change was made here. The uncommitted failing reproduction remains in this ticket worktree as exact hand-off evidence. TICK-213 stays Implementing until the blocker lands, after which the stress test can be rerun and completed.


## Blocker resolution and final scope — 2026-08-19

[[PR-009]] merged the proven Scriban output-limit correction as `4f67a83e22f0b994d5a5f6dbf08d53eec7808a6a`. TICK-213 merged current `origin/dev` into its existing branch and reconciled the overlapping test into one regression: the upstream strong tail/image/signature/furniture assertions remain, while the test name, reference and failure message explicitly state the normal-density acceptance being decided here. The branch changes only this test naming/evidence surface relative to current `dev`; it adds no duplicate Chromium case.

## Simplification pass — 2026-08-19

- **Reuse:** retained PR-009's single stress fixture and assertions rather than adding a second equivalent Browser test.
- **Simplification:** resolved the overlap to three intent-naming changes; no extra helper, fixture, production path or repository document.
- **Efficiency:** one Chromium stress execution covers both complete-tail and normal-density acceptance; no duplicate 80×3/8-photo render.
- **Altitude:** test-only wording makes the acceptance intent explicit. Core, Infrastructure, templates, CSS and caller contracts are unchanged.

No finding was deferred.
