---
id: MAIL-034
type: ticket
title: Scope or remove the Inbox selected-row CSS rules that reach the Cases list
status: backlog
area: mail-communications
assignee: ''
profile: fix
labels:
  - ui
  - css
  - review-follow-up
groups:
  - EPIC-011
links:
  - MAIL-032
  - PLAT-029
  - CASE-025
refs:
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-09-02T03:15:02.953Z'
updated: '2026-09-02T03:15:02.953Z'
---

## What

Make the two `.row-button[aria-current="true"]` rules added to `wwwroot/css/site.css` by PR #640 ([[MAIL-032]]) either reach the Inbox row they were written for or disappear: scope the selector to the Inbox trigger (`a.row-title[data-mail-preview-trigger][aria-current="true"]`) if the design contract wants a selected-row state on the Inbox, otherwise delete both rules and let a Cases ticket add a selected-row highlight deliberately. Correct the test comment the reviewer flagged (F-002 in MAIL-032's review).

## Why

MAIL-032's independent review (2026-09-02, finding F-001 = implementer's S1, minor, accepted-risk): the Inbox puts `aria-current="true"` on the inner `a.row-title` trigger, never on the `.row-button` container, so the new rules match nothing on /Inbox and instead restyle the selected row of the Cases list (`Pages/Cases/Index.cshtml`), a surface outside MAIL-032's scope. Behaviour is unchanged (the highlight lands on a row the markup already marks current), so it was merged as accepted risk with this follow-up owed.

## Approach

- Decide from `docs/design/README.md` (selected-row states) and EPIC-011 `context.md` §1.3/§1.4 whether the Inbox selected row gets a visual state; implement the smaller of the two options.
- `site.css` belongs to the `global_shell` lock; keep the change to those two rules and the test comment.
- Regenerate the Inbox/Cases Test UI snapshots only if rendered markup changes (CSS-only changes need none).

## Verification

- [ ] On /Inbox the selected message row's state matches the design contract (or no rule targets it, by decision).
- [ ] On /Cases the selected row's appearance is unchanged from before PR #640 or deliberately restyled by this ticket.
- [ ] `git grep 'row-button\[aria-current="true"\]'` returns only intended selectors; the corrected test comment is true.

## Outcome
