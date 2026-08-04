# Alteration plan — Manage staff account (page 20)

## Review summary

The strongest heading in the Administration section (name + status chip) on top of a page that
still doubles its chrome, prints times as raw UTC sorting strings, states status twice, buries
its one piece of consequence copy below the submit button, and — when the account is disabled —
narrates the actions it does not have. The disable action needs its consequence line moved above
the button and rewritten in business language.

## Container restructure (operator decision, 2026-08-04)

This screen is one record, so it takes the shape every record screen now takes
(`../../ui-standards-and-review.md` §4 rule 13): **one container** holding a header band, an
action bar, and a body with **no tab row** — this record has one section, and tabs are
for alternatives.

The page-12 case detail is the reference implementation. What that changes here, over and
above the numbered changes below:

- The detail/action two-column split is retired. **Disable account** and **More** become the
  action bar; the reason field and its consequence sentence move into a dialog.
- The header band carries the identity that the detail list used to repeat (password state, last
  access review), leaving the body as the record's facts alone.
- The disabled variant drops the action from the bar and adds one line to the body, instead of
  swapping one panel for another.

Nothing in the change list is withdrawn — the copy, vocabulary, state and evidence decisions all
stand; they are re-housed.

## Changes

1. **Navigation.** New global nav; breadcrumb `Administration / Staff accounts / jane.smith`
   replaces the back-link and eyebrow. H1 stays "Manage jane.smith" with the status chip aligned
   right (kept as-is — it works).
2. **Detail list.** Drop the duplicate "Status" row (the heading chip owns it). Rows become:
   - **Password** — "Set" or amber chip "Temporary" (was "First password change:
     Required/Complete").
   - **Last access review** — London time, e.g. "14 Jul 2026 09:42", linking to the Access
     review page; "Not yet reviewed" when absent (was raw `2026-07-14 09:42:00Z` UTC).
3. **Consequence copy.** Old (below the button): "Disabling revokes existing browser sessions.
   The account is retained permanently." → New (between Reason and button): **"Disabling signs
   this person out everywhere and cannot be undone from this screen. The account stays on the
   administration record."**
4. **Disabled-state panel.** Old: "This account is disabled and retained in permanent
   administration history. There is no delete or password-display action." → New: **"This
   account is disabled and stays on the administration record."** (One sentence of fact; no
   catalogue of absent buttons.)
5. **Reason field hint.** "Kept on the administration record." — consistent with page 19.
6. **Section labels.** Keep "Account detail" and "Account action" as the two uppercase labels
   (one per cluster); table-less page otherwise chrome-free.
7. **Help-text markup.** `.hint` class replaces `.empty-state` misuse.
8. **Danger styling.** "Disable account" keeps the primary red button (red is the app's only
   action colour) but the hardened mockup places it alone in its panel with the consequence line
   directly above — proximity does the danger signalling without inventing a second red.

## Dependencies

- Breadcrumb pattern shared with pages 19 and 21–25.
- London-time rendering helper: the `_FreshnessBanner.cshtml` Europe/London conversion needs
  extracting into a shared formatter before this page can use it (shared with page 22).
- `.hint` class in `site.css` (shared).
- Styled not-found page (root review defect) is a dependency for the expired-bookmark path; not
  owned here.

## Open questions

- Is disable genuinely irreversible from the UI? No enable handler exists on this page today.
  If re-enabling is policy-allowed, this page needs the action; if not, the consequence copy
  above already says "cannot be undone from this screen" — confirm with the operator which is
  true before shipping the sentence.
- Should this page also surface the account's role list (currently only on Staff roles)? It
  would complete the "everything about one account" story at the cost of duplicating the roles
  editing surface. Proposed: show roles read-only with a link to Staff roles; not mocked.
