# v40 discussion log

Chronological. Not design authority.

## 13 October 2026: the round is asked for

The operator asked for a Stage 1 round on the Contacts administration pages, to have them "organised better". The rulings that came with the request are recorded in [v40-notes.md](v40-notes.md#0-the-brief-and-the-rulings-it-rests-on): every contact has exactly one type, fixed at creation and shown read-only, with no type grid and no adding a type later (a separate PR changes the domain model); the list is organised by type with columns that fit each type, using only existing fields ("Principal shows Code"); the contact's page shows only its type's sections; the Principal panels keep FRD-17's order, Report generation → Report sending → Salvage matrix; v39's open T and U are carried over or settled; and the SOP text seeded into Notes on every Case, up to about 1,300 characters, must show well on the Principal's page.

## 13 October 2026: what was built

The live pages were read from `origin/dev` (`5d2338708`). The usability walk found that the Principal code and Inspection mode inputs on a contact can be edited, but the save ignores them. Every Principal panel's save reloads at the top of the page, and its refusal appears in another panel's summary. The panel heading has drifted from FRD-17's "Report generation" to "Report settings", and Guidance for new Cases is live but not in FRD-04.

No .NET build was run, because another lane owns the build on this host. The shell and the Principal panels come from v39's 6 October capture, and the list and the contact form were written from dev's Razor. The type tabs on the list were not drawn, because the design authority bars pill tabs as filters, so the alternative is one type at a time through the existing Type select. The Principal's tabs layout is offered as an alternative because the design authority describes single-record pages with "sections as tabs".

The self-check passed with 262 checks and no console error. Points that needed a choice were drawn plainly and listed rather than decided: per-type columns (B), what a possible match does now (F), the duplicate-name sentence and name uniqueness (G), the Type box (H), the read-only code and inspection mode (I, J), folding Principal settings (K), tabs (L), the fitted notes box (M), where a save lands (N, O), the links grid (P), three live defects (Q) and Guidance for new Cases (R). v39 T and U are carried as S and T.

Open: every lettered item in `v40-notes.md` section 6.

## 13 October 2026: the rulings and approval

The operator settled every item and asked for Stage 2. A, B, D to F, H to K and M to T were confirmed. C: each panel sorted by name, with no Sort control. G: names unique per type, refused with "A contact with that name already exists." on Add and Edit. L: tabs, working without script, with Replace code below them and the heading "Report generation". R: Guidance for new Cases removed. The outcomes are in [v40-notes.md](v40-notes.md#6-sign-off-list).

## 13 October 2026: Stage 2

Implemented on `task/v40-contacts-stage2`, stacked on PRs #1314 and #1315; see [v40-notes.md](v40-notes.md#10-stage-2-13-october-2026). The routed pages were shot into `stage2-shots/` and checked: 77 checks, none failing.
