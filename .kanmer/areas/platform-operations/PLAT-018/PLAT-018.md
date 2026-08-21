---
id: PLAT-018
type: ticket
title: Correct two self-contradictory rules in the design authority
status: backlog
area: platform-operations
assignee: ''
profile: chore
labels:
  - docs
  - design-authority
links:
  - MAIL-006
docs_todo: true
archived: false
created: '2026-08-21T08:25:11.433Z'
updated: '2026-08-21T08:25:20.962Z'
---

## What

Two edits to `docs/design/README.md`, both fixing places where the document
contradicts itself and misleads a reader following it in good faith.

1. Strike `queue` from the banned-words list at line 413.
2. Rewrite line 433 so the one-consequence-sentence rule reads as a pointer to
   the approved list, not as a test a reader applies.

## Why

**`queue`.** Line 413 bans the word from operator-facing copy. Line 464 sets
the approved shell as `Dashboard | Inbox | Upload | Queues | Cases |
Administration`, and line 472 calls that route order approved. The same
document bans a word it mandates as a navigation label. The narrower rule at
line 170 — "Do not expose … queue mechanics … in operator copy" — is what the
ban was reaching for: the mechanism, not the operator's ordinary word for a
list of work waiting. Six operator-visible uses exist today and all are the
legitimate sense: `_Layout.cshtml:74`, `Triage/Index.cshtml:5`, `:26`, `:31`,
`Triage/Details.cshtml:16`, `_MetricCard.cshtml:16`, and the screen-reader
caption at `Triage/Index.cshtml:267`. The other 58 matches are class names,
route parameters and comments, which the existing code-identifier exemption
already covers.

**The consequence sentence.** Line 433 reads "The one exception stands above:
a single consequence sentence on a destructive or irreversible action." Read
alone it is a test: judge an action irreversible, write a sentence, ship it.
The intent is a pointer back to line 400, "Approved necessary copy includes:",
whose three sentences are approved by the operator individually. The list is
closed; the reader does not add to it. Operator direction, 2026-08-21. This
misreading has already produced unapproved copy on a design under review, so
it is a real trap, not a tidy-up.

No governing PRD/FRD/ADR: `CLAUDE.md` puts repository rules and conventions in
`CLAUDE.md` and the design authority itself, explicitly not in an ADR, and
`repoDocs` globs only cover `docs/prd|frd|adr`. `docs_todo` is set to satisfy
the gate; the honest resolution is that this class of change has no governing
doc by design.

## Approach

- Delete `queue` from the list at line 413. Leave line 170 alone — it already
  carries the real rule.
- Reword line 433 to name the approved list at line 400 as the source of
  permitted guidance copy, and to say the list is closed.
- Docs-only. No code, no `site.css`, no markup. The `Queue` mislabel on the
  Inbox message page is [[MAIL-006]], not this ticket.

## Verification

- [ ] `docs/design/README.md` no longer bans a word it mandates at line 464.
- [ ] Line 433 cannot be read as a test the author applies to their own copy.
- [ ] No other file changes in the diff.
