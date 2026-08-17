# Plan — Claude Design UI implementation

## Scope

Fold the Claude Design project's 21 screen prototypes back into `Pegasus.Web`.
Presentation only: no file outside `src/Pegasus.Web` changes except the design
authority file, no business policy moves out of `Pegasus.Core`, no new project,
store, runtime or deployment unit, so no ADR is required.

Because the design system's stylesheet is byte-identical to our `site.css`
(research Finding 1), this is markup work plus one new CSS block for the rail.

## Governing docs

- **FRD-12 Operator experience** — the ticket's `refs`. Owns the route set, the
  state vocabulary, freshness and reconciliation, and the required state family
  (loading, empty, current, stale, unavailable, partial, failed, validation,
  conflict, access-denied). Nothing in the design contradicts it.
- **`docs/design/README.md`** — binding UI authority. Two divergences are created
  and must be written into it in this same task: the shell change, and Lucide
  over the prototype's PNG marks.
- **`docs/capabilities.md`** — read-only here. Consulted to classify which
  prototype sections are deferred; no allocation changes.

## Sequence

Ordered so the shell lands first — every screen sits inside it, and doing the
screens first would mean laying them out twice.

### 1. Shell

1. Add the `.app-rail` block to `wwwroot/css/site.css`: the 236px grid, sticky
   full-height aside, `.rail-link` rows, the `aria-current="page"` treatment
   (2px `--ce-red` left border + `--ce-red-tint` ground + `font-weight:700`), the
   count badge, and a narrow-viewport collapse to a horizontal row so nothing is
   hidden. Reuse existing tokens only — no new custom properties.
2. Rewrite `Pages/Shared/_Layout.cshtml` around it. Preserve exactly: the skip
   link, `_LucideSprite`, the `inboxEnabled` composition gate (an uncomposed
   capability is absent, never a disabled span), `CurrentWhen`, the
   Administrator-only Administration item, the authenticated/anonymous branch,
   and the `TempData["Confirmation"]` status card.
3. Confirm `_LayoutAuth` and `_LayoutExternal` already match `ChangePassword.html`
   and `UploadLink.html`. Do not put the rail on either — both are deliberately
   navless or brand-only.

### 2. Screens, in dependency order

Simple, self-contained screens first to settle the idiom, then the record
screens, then the ones with unbound sections.

4. `Pages/Index.cshtml` (Dashboard) — sets the `PageHeading` + `Refresh` +
   `MetricStrip` idiom the rest reuse.
5. `Pages/Upload.cshtml`, `Pages/Uploads/Request.cshtml`,
   `Pages/Account/PasswordChange.cshtml` — small and independent.
6. `Pages/Triage/Index.cshtml` (Queues), `Pages/Cases/Index.cshtml` (Cases) and
   `Pages/Search/Index.cshtml` — settle the table/filter/pager idiom. Cases and
   Search share a backing query, so they change together or they drift.
7. `Pages/Mail/Index.cshtml`, `Pages/Mail/Message.cshtml`,
   `Pages/Operations/Index.cshtml`.
8. `Pages/Administration/Index.cshtml`, then the eight administration screens.
9. `Pages/Cases/Create.cshtml`.
10. `Pages/Cases/Details.cshtml` + the four `Cases/Shared/_Case*.cshtml` partials
    — the biggest single screen.
11. `Pages/Cases/Assessment/Index.cshtml` — last, because it carries the most
    unbound markup and benefits from every idiom being settled.

### 3. Unbound sections

12. Add the deferred sections listed in `files.md`. Each one:
    - is static markup with **no** `asp-for`, no model binding and no handler;
    - carries a leading Razor comment naming the capability ID and its allocation
      (e.g. `@* EXT-09 (Later/1.0.0): unbound design markup … *@`);
    - shows no fabricated operator data — no invented cases, claimants,
      registrations, valuations or e-mail addresses. Inputs render empty, and
      read-only figures render as an em dash or an `EmptyState`, never as a
      plausible-looking number. This is the `corpus`/no-fabrication rule and it is
      not negotiable even inside unbound markup.

### 4. Documentation and verification

13. Update `docs/design/README.md` with the two divergences and a statement that
    the unbound sections prove nothing (implemented ≠ caller-proved).
14. `dotnet build --configuration Release`.
15. `dotnet test` on the Web-facing suites; full suite if time allows (the
    integration suite runs long, so chunk it and keep the log).
16. Run locally under `DevelopmentOffline` and capture visual proof of the rail
    and a representative screen from each family.

## Acceptance conditions

- Release build clean; no new warnings.
- Web tests green. Any test asserting the old `.app-nav`/`.nav-links` DOM is
  updated to the rail, not deleted.
- Every one of the 21 screens renders in the new shell with the design's
  structure and the design system's real class names.
- `aria-current="page"` present on the active rail route, with a weight change,
  so the route is not signalled by colour alone. Skip link still reaches
  `#main-content`.
- No fabricated operator data anywhere, including unbound markup.
- Every unbound section names its capability ID in the markup.
- `docs/design/README.md` records both divergences.
- No change under `Pegasus.Core`, `Pegasus.Infrastructure`, `workspaces/` or
  `corpus/`.

## Risks

- **Test coupling to the old shell.** Web tests may assert `.nav-links`. Expected
  and handled in step 15 — update assertions to the rail.
- **Breadth.** 21 screens in one PR is large to review. Mitigated by committing in
  small logical slices (shell, then one commit per screen family) so the diff
  reads in order.
- **Unbound markup misread as working.** Mitigated by the mandatory capability-ID
  comment and by refusing to populate it with plausible data.
- **Rail regression on narrow viewports.** FRD-12 requires responsive use without
  hiding required evidence; the collapse in step 1 is part of the acceptance, not
  an afterthought.
