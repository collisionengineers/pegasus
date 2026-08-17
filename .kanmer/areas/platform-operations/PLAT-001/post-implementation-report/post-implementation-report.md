# Post-implementation report — Claude Design UI implementation

Branch `task/claude-design-ui`, 11 commits, worktree
`../pegasus-worktrees/claude-design-ui`. No PR opened yet.

## What shipped

All 21 screen prototypes from the Claude Design project
`710bb42f-84ed-4d82-b216-7c5d60fb5aef` folded back into `Pegasus.Web`, plus the
shell they sit in.

| | |
| --- | --- |
| Files changed | 27 in `src/Pegasus.Web`, `docs/design/README.md`, 2 test files |
| Untouched | `Pegasus.Core`, `Pegasus.Infrastructure`, `workspaces/`, `corpus/` — verified empty diff |
| New CSS | one rail block, plus dropzone, block-grid, stack, evidence-row and two modifiers |
| New binary assets | none |

Commits, in order: the rail; Dashboard/Upload/upload-link/password-change;
Queues/Cases/Inbox; message/Operations; administration notices; new case; case
overview; assessment; the design authority and the copy the design had moved;
the landmark correction; the journey-test assertion.

## The finding that decided the shape of the work

`_ds/…/_ds_bundle.css` diffed against `src/Pegasus.Web/wwwroot/css/site.css` is
**two lines**, both stale `docs/design.md` references in comments where the repo
says `docs/design/README.md`. The design system's stylesheet *is* our stylesheet,
and ours is the newer copy.

So there was no token, colour or component-rule work. Everything was markup,
plus one new CSS block for the rail — which is prototype-local `<style>` in
`screens/shared.jsx` and had never been in the design system at all.

## Verification

| Suite | Result |
| --- | --- |
| `dotnet build --configuration Release` | succeeded, 0 warnings, 0 errors |
| `Pegasus.ArchitectureTests` | 94 passed |
| `Pegasus.Core.Tests` | 572 passed |
| Web integration (`*WebTests`) | 42 + 135 passed |
| `Browser` (axe + Playwright journeys) | 32 passed |

Two mechanical checks against this ticket's own rules, both clean:

- inline `style` attributes added to server markup: **0**
- `asp-for` inside a gated or unbound block: **0**
- fabricated operator data (grepped the added lines for the prototypes'
  invented references, registrations, claimants, valuations and Experian
  figures): **none**

## What the tests caught, and what that changed

**The rail's element, three times.** The accessibility suite was right at every
step and the final answer is the obvious one in hindsight:

1. `<aside class="app-rail">` → axe `landmark-unique` on 9 routes. The design's
   `Notice` is also an `<aside>`, so every screen carrying one had two unnamed
   complementary landmarks.
2. `<div class="app-rail">` → axe `region` on 22 routes, worse: the brand, the
   navigation and the signed-in controls were outside every landmark.
3. `<header class="app-rail">` → green. The rail is the page banner turned on
   its side, which is exactly what the top bar already was.

**No inline styles.** The prototypes style almost entirely through inline
`style` attributes. `AccessibilityTests` asserts server markup never carries
one, because the production CSP (`default-src 'self'`, no `style-src`) discards
them — that had already shipped a ~1,900px blank band once. Every prototype
inline style was translated into a named class rather than copied. This was a
deliberate choice before the test confirmed it.

**Three sentences restored against the design.** The Cases empty state, the
Inbox body excerpt and "Not associated with a case." are each separately
asserted by an integration test — two of them by *different* test classes. They
are settled operator copy, not incidental wording, so they were put back rather
than the assertions moved. The Cases empty state now carries both settled
sentences because each is relied on to tell an empty result from an unavailable
one.

**Two test assertions updated, none deleted.** The blank-band guard now accepts
either navigation (`.app-rail, .app-nav`), and the journey test reads the routes
from `nav[aria-label='Primary']` while leaving the identity assertion that
already sits two lines above it. That second change matches the design
authority, which has always listed "authenticated user/sign-out controls" as its
own item **after** the seven routes.

## Two drift fixes found on the way

Neither was asked for; both are corrections to the repository, not imports from
the design.

- **Operations was in the wrong place.** `origin/dev` rendered it third;
  `docs/design/README.md` settles it sixth (operator, 2026-08-04, "shipped in
  releases 6 and 7"). The rail matches the authority.
- **The provenance word/glyph map lived only inside a partial.** Rendering many
  rows from a Razor local function cannot invoke a partial, so the map moved to
  `OperatorLabels.Provenance` and `_Provenance.cshtml` now reads from it. One
  table, two callers, instead of a second copy.

## Deliberate divergences from the prototypes

Each is recorded in `open-questions/` with the rule behind it, and the first two
are now written into `docs/design/README.md`.

1. **The left rail replaces the top bar** (operator decision, 2026-08-17). The
   current route's non-colour signal becomes a 2px red left border rather than
   an underline; `aria-current="page"` and the weight change are unchanged.
   Under 1024px the rail lies down and the border moves to the bottom edge, so
   the signal survives the reflow and nothing is hidden.
2. **The 14 PNG icon marks are not adopted.** They are defined in
   `screens/shared.jsx` — prototype scaffolding, not design-system components —
   and the design system's own README says icons come only from its sixteen
   Lucide glyphs. `docs/design/README.md` agrees more strongly: a checksummed
   register, "no brand or decorative imagery is needed", and decorative or
   generated replacement icons prohibited. The rail brand is the existing
   approved `logo_no_margin.png`.
3. **Deferred capabilities ship as unbound markup** (operator decision,
   2026-08-17), extending the precedent already recorded in
   `Cases/Assessment/Index.cshtml.cs` for UI-15. In practice the honest form
   turned out to be the design system's own disabled-with-condition idiom:
   Open in Glass's, Open in Audatex and Import assessment sit in the assessment
   record bar stating their condition, wired to nothing. EXT-12 and EXT-13 are
   both `Later / 1.0.0` and each needs its own accepted contract.
4. **Operations keeps its informational AI copy.** The prototype shows two live
   panels of automation state; `docs/design/README.md` specifies the section as
   "informational AI operations copy", and that is what shipped.
5. **The public upload screen has no "accepted so far" table.**
   `RequestUploadPublicView` exposes only media types and maximum size, and this
   is the one screen a third party sees — unbound markup there would show a
   claimant an empty table rather than a staff-readable placeholder.

## Known gaps

- **Rail counts render nothing.** `_Layout.cshtml` supports a count per route
  through `ViewData["RailCounts"]`; no page supplies one. Populating them means
  a per-request query in the shell, and FRD-12 forbids a stale zero placeholder,
  so rendering nothing is correct until a real figure exists. Worth a follow-up.
- **No visual proof captured yet.** Proof belongs on merged `main` at the
  verifying stage, not here.
- **The prototypes' Inbox "Category" column is absent.**
  `RetainedMailSummary` carries a processing outcome but no category, and the
  column would have been empty for every row.
- **Case tabs stay Overview / Evidence / History.** The design adds "Inspection"
  and renames History to "Notes & history"; the app has no notes capability and
  no separate inspection section, and renaming the tab would promise one.

## Follow-up tickets worth filing

1. Rail counts: decide the query and wire real outstanding figures.
2. Experian AutoCheck has no capability ID at all. Before it is more than
   markup it needs an inventory entry, a supplier contract and an accepted ADR.
3. Case notes and engineer queries — shown in the prototype, unallocated.
4. The design project's `github.md` screen map is a genuinely useful artefact and
   is currently only in the Claude Design project.
