# Independent review — PR #415 — 2026-08-19

Reviewer did not implement SIMPLI-014.

## Changes

- Adds a Core-owned four-outcome assessment snapshot, repair-cost arithmetic, accepted-source and engineer-tuple validation, typed assessment/fee-note draft artifacts, and renderer port/use case.
- Adds one Infrastructure Scriban/Playwright/PDFsharp implementation with exact embedded assessment/fee-note templates, CSS, logo, and Andy Patterson signature, plus a serialized reusable Chromium instance.
- Composes report rendering only from Pegasus.Web through AddPegasusReportRendering; Worker keeps the shared Infrastructure registration without the renderer.
- Adds focused Core, architecture, embedded-resource, Web-composition, and real-Chromium tests.
- Deletes the standalone report-renderer workspace, API, CLI, MCP/MCPB, Docker and independent workspace CI job, and updates selected FRD/current-state/provenance/build files.

## Comments and disposition

1. **Blocking — rendererref1 functional and visual parity is not proved or implemented.** The active assessment template is a short structural document and does not render the required ordered custodied images or much of the approved rendererref1 assessment structure. The fee-note template displays the agreed fee as a single total; it omits the Core-computed fee VAT/total required by the design, VAT number, payment details, terms, and the approved subtotal/VAT/TOTAL DUE structure. PhotoCustodyReferences are validated but never consumed. The real-Chromium test proves only PDF signature, page count, hash length, template version, and engine string for one Repairable sample; it does not cover all four adapter outcomes, assert representative content, or compare with the supplied PDFs. **Disposition: filed as [[PR-006]], blocking [[SIMPLI-014]].**
2. **Blocking — repository documentation is inconsistent with workspace retirement.** docs/runbook.md still names the removed workspace scripts/solution/CLI as supported commands and dependencies; docs/design/README.md still says the deleted CollisionRenderer.Core project embeds/owns the renderer assets. These are live instructions/ownership claims, not merely CHANGELOG/ADR history, and the PIR omits the stale ripple effect. **Disposition: filed as [[PR-007]], blocking [[SIMPLI-014]].**
3. **Non-blocking / accepted — closed identity boundary.** Core contains the four outcomes and repair arithmetic; the complete accepted engineer map contains only A Patterson / M.Inst.IAEA / andy_patterson. Ed and Neil assets are neither embedded nor selectable and tuple mismatch tests fail before the adapter. No absent qualification was invented.
4. **Non-blocking / accepted — architecture boundary.** The workspace and standalone API/CLI/MCP/MCPB/container sources are deleted; no fifth production project appears. AddPegasusReportRendering is called only by Web, consistent with ADR-0028. Historical references in ADRs/CHANGELOG are legitimate.
5. **Non-blocking / accepted — build/simplification record.** Package locks were regenerated for transitively affected projects; root analyzers/warnings-as-errors apply and the recorded Release build is clean. The plan's simplification section honestly records the independent findings and the diff reflects the applied dispositions: Core arithmetic/tuple ownership, typed artifacts, exact resources, one template-version constant, caching, serialized Chromium, opt-in evidence writes, and Web-only composition.
6. **Non-blocking / qualified — local full-suite evidence.** The PIR clearly states that the monolithic Integration invocation was stopped after exceeding its documented silent baseline, while focused real-Chromium tests passed. This qualification is honest; sharded GitHub SQL/browser jobs remain required before any eventual merge.

## CI observed

At review time, documentation, reference-data, changes, and source-workspaces were green. Unit, browser, and three SQL-integration shards were still pending; infrastructure was skipped by change detection. The PR was mergeable but UNSTABLE. A needs-changes verdict does not wait on pending CI and the PR was not merged.

## Governing docs

- ADR-0025 and ADR-0028 are implemented correctly at the project/composition boundary.
- FRD-11's closed family, four outcomes, Andy-only complete tuple, draft-versus-issue boundary, and fail-closed unsupported identities are reflected.
- FRD-11 and the plan also require the approved rendererref1 assessment/fee-note behavior and representative parity; the current templates/tests do not satisfy that portion.

## Verdict

**Needs changes.** [[PR-006]] and [[PR-007]] block SIMPLI-014. PR #415 must not merge or move to Verifying until both findings are resolved in the owning PR, its PIR/evidence are updated, independent re-review passes, and CI is green.

GitHub traceability: formal `--request-changes` was rejected because the connected GitHub account is the PR author. Posted the same needs-changes verdict as PR comment https://github.com/collisionengineers/pegasus/pull/415#issuecomment-5340533305. This does not alter the independent Kanmer review verdict or blocker links.

## Independent re-review — 2026-08-19 — head `cdb50cd2bbeb84fe69172407adaca06298a437a2`

### Changes

- The correction extends the Core report snapshot with hash-checked ordered image bytes, Core-computed fee net/VAT/total, exact accepted Statement of Truth/payment terms, and the VAT-inclusive contract-repair cap.
- The Infrastructure adapter now composes the full active rendererref1 assessment/fee-note sections from the fixed embedded templates, including vehicle/cost/work-list content, photos, signature, fee rows, payment details and terms.
- The Browser integration proof exercises all four approved outcomes through application composition and real Chromium, extracts representative assessment and fee-note text, and verifies the only active embedded engineer resource.
- `docs/runbook.md` and `docs/design/README.md` now name the integrated Infrastructure/Web path and supported Playwright test route rather than the retired workspace/CLI/scripts.

### Comments and disposition

- **Blocking PR-006 — fixed-in-PR.** Core is the single arithmetic/custody/presentation-policy owner; the adapter formats accepted values and bytes. Only the assessment and fee-note resources plus Andy's complete tuple are active. All four outcomes have real-Chromium content evidence; unsupported category wording, engineers and catalogue families remain fail closed.
- **Blocking PR-007 — fixed-in-PR.** Focused live-document searches are empty for `workspaces/report-renderer`, `CollisionRenderer.Core/Cli/Api/Mcp`, `render-starters` and `visual-regression`; current instructions point to the monolithic path.
- **CI stability — passed.** Run 32242081373 is green: unit 3m20s, browser 7m51s, SQL shards 1/2/3 7m43s/8m55s/8m11s, SQL coverage 8s, documentation/reference/source-workspaces green; infrastructure correctly skipped by change detection. The prior shard-3 LocalDB teardown lock did not recur.
- No new blocking or non-blocking findings.

### Verdict

**Pass.** The corrected diff, plan, checklist, PIR, open questions, FRD-11, ADR-0025 and ADR-0028 were checked. PR-006 and PR-007 are resolved at this head, the simplification dispositions remain honest, and every required CI lane is green. Merge to `dev` is authorized by the standing delegation for this review.

Merged PR #415 to `dev` after the passing independent re-review and fully green required CI. Head: `cdb50cd2bbeb84fe69172407adaca06298a437a2`; merge commit: `b548b674e31d05de6f43eeb285a25dedd7d2a768`; merged at 2026-08-19T10:29:20Z. Next stage is Verifying; no main or cloud write was performed.
