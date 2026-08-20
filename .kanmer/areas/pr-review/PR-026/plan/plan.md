# Plan — PR-026

## Approach

Document the already-authorized narrow local route as an explicit exception using the existing Administration pattern, then render it locally and record manual desktop/zoom evidence. This is smaller and safer than removing approved work or inventing a new design.

## Steps

1. Amend `docs/design/README.md` and `docs/capabilities.md` with the exact activation, alternatives, independent-review, visual/manual-review and undelivered boundaries.
2. Run the local authenticated route, inspect the Administration card and category page at desktop and 200% zoom, and record observations.
3. Update MAIL-004 and PR-026 reports, run docs checks and four simplicity lenses, commit and push to PR #473.

## Governing docs

- `docs/design/README.md`: canonical UI re-entry and visual evidence owner; amended under explicit operator authorization.
- `docs/frd/frd-12-operator-experience.md`: preserved Administrator-only access and acceptance evidence boundary.

## Verification

Rendered authenticated local route, manual visual inspection, documentation link/placement checks, and diff check. No external writes.

## Risks

Avoid wording that treats local/test evidence as deployment, Outlook mutation, Graph permission, or operator release acceptance.

## Simplification pass — 2026-08-20

- Reuse: retained the existing Administration card/form pattern and amended only its canonical design/capability owners.
- Simplification: no new design system, route, asset or generic rules surface.
- Efficiency: documentation-only disposition; no runtime path changed.
- Altitude: design owns UI re-entry; capabilities owns allocation/evidence status.
- Applied finding: corrected an interim sentence that could imply the visual check had passed; it now records the remaining gate honestly.
- Unapplied finding/blocker: the authenticated local app was prepared at the dedicated `PegasusMail004Visual` database, but the in-app Browser runtime exposed no browser instance. Desktop/200%-zoom manual inspection remains required before PR-026 can leave Implementing.
