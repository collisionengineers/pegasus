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
