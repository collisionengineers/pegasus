# Plan — PR-053

## Governing docs

- `docs/frd/frd-08-email-mailbox-and-background-processing.md`: retain the selected categorised mail view and context.
- `docs/design/README.md`: use the compact read-only selector convention without explanatory hints/panels.

## Steps

1. Remove the two new explanatory blocks and any now-unused label helper.
2. Tighten the authenticated Web assertion around the selected native option, absent copy, and preserved queue key.
3. Run the focused Web proof and record the correction in TICK-057 plan/PIR.

No replacement component or wording is introduced.

## Simplification pass — 2026-08-21

- **Reuse:** Kept the existing labelled native selector and selected option; added no replacement component or explanatory copy.
- **Simplification:** Removed both redundant text blocks and the now-unused `ActiveViewLabel` helper. Applied in full.
- **Efficiency:** Removes rendering and one unused label computation; no query or state change.
- **Altitude:** Presentation-only correction in the existing Inbox and authenticated test. No policy or framework.

No unapplied finding remains.
