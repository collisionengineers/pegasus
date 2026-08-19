# Plan — PR-009: preserve post-work-list sections under long pagination

## Approach

Repair the existing rendererref1 assessment flow at the exact Chromium fragmentation seam. Reuse the current Core snapshot/custody contract, Infrastructure renderer, fixed templates, CSS, reusable browser and Browser test fixture. First port the strong TICK-213 stress reproduction into PR-009 unchanged in intent. Then experimentally isolate the photo-grid/forced-break boundary and apply the smallest semantic markup/CSS adjustment that preserves normal rendererref1 presentation while ensuring later Statement of Truth/signature content remains printable. Prefer explicit two-photo block rows and a forced break on the real following section over an unbounded fragmented CSS grid plus empty sentinel. Do not shrink, truncate, cap, retry, add density, or introduce another render pass.

## Governing docs

- **Meets, does not modify — `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`.** The fix preserves the complete approved assessment content, accepted ordered photo bytes, exact wording/signature and fixed no-density surface. It corrects omission without changing inputs, outcomes, arithmetic, finality or activation scope.
- **Meets, does not modify — `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md`.** Work stays inside the existing Infrastructure/template adapter and its existing integration tests; no project, service, runtime, API or policy owner is added.
- **EPIC-004 / rendererref1.** Preserve normal styling, two-column ordered photos, no captions and page furniture. TICK-213 supplies reproduction context only and its worktree remains untouched.

## Steps

1. Create PR-009's `origin/dev` task branch/worktree and take the ticket after gates pass.
2. Add the real-Chromium stress regression to the owning integration test: 80 unique entries per work-list family and 8 custody-validated photos; assert multi-page output, each terminal item, at least 8 embedded images, Statement of Truth, accepted signature identity, reference/footer on every page and no placeholders.
3. Run the test unchanged to record the failing baseline on PR-009.
4. Adjust only assessment photo/tail markup and scoped CSS (and the photo-row formatter only if markup requires it) so Chromium fragments between explicit two-photo rows and forces the Statement of Truth break on the semantic section. Preserve source order and 48mm/two-column normal styling.
5. Run the new regression and existing complete Browser renderer suite through real Chromium, plus Core report and dependency/Release build checks.
6. Run the mandatory simplification pass over the diff: reuse, simplification, efficiency and altitude. Apply behaviour-preserving findings; record all dispositions.
7. Tick the checklist, write the PIR with exact evidence/deviations, commit/push, open a PR to `dev`, record traceability and move Review.

## Verification

- failing-before/passing-after real-Chromium regression;
- `dotnet build --configuration Release` with zero warnings/errors;
- focused `AssessmentReportRendererTests` Browser suite;
- focused Core report tests and dependency-direction tests if the renderer formatter changes;
- PDF assertions for terminal list items, all accepted images, Statement of Truth, `A Patterson`, every-page case reference/footer, page count/hash metadata and no unresolved placeholders;
- diff/search proof that no density selector, compact assessment class, content cap, truncation, multipass or second renderer was introduced;
- CI required lanes after PR creation.

## Risks and mitigations

- **Chromium print quirks:** prove with real Chromium and structural PDF extraction, not HTML-only assertions.
- **Layout regression:** scope CSS to assessment photo/tail classes and retain exact dimensions/two-column layout.
- **False image count:** use PdfPig image enumeration across all pages and unique accepted custody inputs.
- **Duplicating TICK-213 work:** port only its uncommitted test intent into PR-009; never edit or commit its worktree.
- **Over-solving via density:** explicitly prohibited; pagination correctness must hold at normal style.

## Course correction — confirmed during execution

The planned semantic photo-row/CSS correction was tested and rejected: grid, table, flex and block-row variants all truncated at the same byte boundary. Captured HTML and Scriban source proved the actual cause was `TemplateContext.LimitToString`'s 1 MiB default, which truncated the composed document before Chromium received it. The final implementation therefore keeps the rendererref1 template/CSS byte-for-byte unchanged and sets the existing context to Scriban's documented unlimited mode. This is smaller, preserves all accepted content and avoids introducing a second layout convention.

## Simplification pass — 2026-08-19

- **Reuse:** retained the existing template/context/render pipeline and existing Browser fixture/provider convention; no new renderer or layout path.
- **Simplification:** reverted every exploratory template/CSS/photo formatter change once the 1 MiB cause was proven. Final production change is one setting on the existing context.
- **Efficiency:** removed diagnostic HTML/text artifact writes and duplicated stress-test evidence code; CI keeps structural PDF assertions without extra filesystem work.
- **Altitude:** the Infrastructure boundary changes only renderer mechanics. Core policy, inputs, density, content limits and templates remain unchanged.

No finding was deferred.
