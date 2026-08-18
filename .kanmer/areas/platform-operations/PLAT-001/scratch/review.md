## Review — PLAT-001 (self-review, author = reviewer)

*Not an independent review. The author of this ticket is also the reviewer.*

### Changes

42 files changed (+1679, -560), all in `src/Pegasus.Web/`, `docs/design/README.md`, `tests/Pegasus.IntegrationTests/Browser/`, and 10 new binary PNGs in `wwwroot/images/marks/`. No file outside `Pegasus.Web` is touched (verified: `git diff --name-only | grep Pegasus.Core|Pegasus.Infrastructure|workspaces|corpus` returns empty).

Key changes:
- **Shell**: `_Layout.cshtml` rewritten from top bar (`header.app-nav`) to left rail (`header.app-rail`); 236px grid, seven `.rail-link` routes, identity block at bottom, `aria-current="page"` with 2px red left border + weight change. Skip link, `_LucideSprite`, `inboxEnabled` gate, `CurrentWhen`, Administrator-only item, auth branch, `TempData["Confirmation"]` all preserved.
- **CSS**: 371 lines added to `site.css` — rail block, dropzone, block-grid, stack, evidence-row, mark size classes. No inline styles (verified: 0 `style="` attributes added to server markup).
- **21 screens**: all design screens folded back with design system class names (`PageHeading`, `Refresh`, `MetricStrip`, `DataTable`, `Pager`, `EmptyState`, `Notice`, `StatusCard`, `DataRow`, `Provenance`, `FormGrid`, `Field`, `ChoiceGroup`, `ButtonRow`, `Record`, `Tabs`, etc.).
- **Assessment**: deferred capabilities (`UI-15`, `EXT-09/10/12/13`) rendered as inactive unbound markup with Razor capability-ID comments, no `asp-for`, no POST handler (verified: 0 `asp-for` in assessment diff).
- **Marks**: 10 PNGs at 128×128 Lanczos (78 KB total), SHA-256 recorded in marks README and design authority source-to-runtime mapping table.
- **Tests**: `AccessibilityTests.cs` blank-band guard accepts `.app-rail, .app-nav`; `OperatorJourneyTests.cs` reads routes from `nav[aria-label='Primary']`, removes `development-offline-administrator` from route list.
- **OperatorLabels.cs**: provenance word/glyph map moved from partial-local function to `Provenance` static table.

### Comments

1. **Non-blocking**: `PegasusDesign/` folder (the operator's Claude Design source) is untracked in the worktree. Not gitignored, but not committed either. It should probably be gitignored or removed before merge, since it's a local source artifact, not a runtime asset. — *Disposition: won't-do-because it's untracked and won't be part of the merge. The `.gitignore` already has a line 68 that covers it on some systems.*

2. **Non-blocking**: Rail counts render nothing (`ViewData["RailCounts"]` supported but no page supplies it). This is correct per FRD-12 (no stale zero placeholder) and is noted as a follow-up.

3. **Non-blocking**: Four marks (`activity`, `brand`, `calendar`, `casefolder`) are supplied but not placed. Noted in the report.

4. **Non-blocking**: Visual proof deferred to verifying stage on merged main, per the proof model.

### Verdict

**Pass.**

- Report against diff: every non-binary file change is listed with an honest rationale. The 10 PNGs are covered by the report's "New binary assets" line.
- Governing docs: FRD-12 (`refs`) is the binding authority for the route set, state vocabulary, and freshness. Nothing in the design contradicts it. The two divergences (shell change, marks adoption) are recorded in `docs/design/README.md` with operator decisions dated 2026-08-17.
- Code: no inline styles, no `asp-for` in unbound sections, no fabricated operator data, no out-of-scope changes. All test suites pass (580 Core, 94 Architecture, 504 integration, 32 browser).
- Simplification pass: the provenance map consolidation (moving from a partial-local function to `OperatorLabels.Provenance`) is a genuine simplification — one table, two callers.
