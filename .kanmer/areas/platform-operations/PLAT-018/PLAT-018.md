---
id: PLAT-018
type: ticket
title: Correct two self-contradictory rules in the design authority
status: done
area: platform-operations
order: 1830
assignee: codex-mcp-client
profile: chore
stageEntered:
  preparing: '2026-08-21T13:57:30.823Z'
  review: '2026-08-21T14:02:38.758Z'
  verifying: '2026-08-21T14:06:42.690Z'
  done: '2026-08-21T14:25:32.625Z'
labels:
  - docs
  - design-authority
links:
  - MAIL-006
  - PLAT-019
docs_todo: true
commits:
  - 892fe6a798c808dc110fdf91fbaeeb3140f577aa
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/502'
archived: false
created: '2026-08-21T08:25:11.433Z'
updated: '2026-08-25T06:38:05.544Z'
---

## What

Two edits to `docs/design/README.md`, both fixing places where the document
contradicts itself and misleads a reader following it in good faith.

1. Strike `queue` from the banned-words list.
2. Reword the one-consequence-sentence rule so it reads as a pointer to the
   approved list, not as a test a reader applies to their own copy.

Line numbers below are `origin/dev`, which is the base — the second rule sits
one line lower there than on `main`. Locate both by their quoted text rather
than by number if the file has moved again.

## Why

**`queue`.** The banned-words list (dev:410-418) forbids the word in
operator-facing copy. Line 464 sets the approved shell as `Dashboard | Inbox |
Upload | Queues | Cases | Administration`, and line 472 calls that route order
approved. The same document bans a word it mandates as a navigation label. The
narrower rule at line 170 — "Do not expose … queue mechanics … in operator
copy" — is what the ban was reaching for: the mechanism, not the operator's
ordinary word for a list of work waiting.

Six operator-visible uses exist today and all are the legitimate sense:
`_Layout.cshtml:74`, `Triage/Index.cshtml:5`, `:26`, `:31`,
`Triage/Details.cshtml:16`, `_MetricCard.cshtml:16`, and the screen-reader
caption at `Triage/Index.cshtml:267`. The other 58 matches are class names,
route parameters and comments, which the existing code-identifier exemption
already covers.

**The consequence sentence.** dev:434 reads "The one exception stands above: a
single consequence sentence on a destructive or irreversible action." Read
alone it is a test: judge an action irreversible, write a sentence, ship it.
The intent is a pointer back to line 400, "Approved necessary copy includes:",
whose three sentences the operator approved individually. The list is closed;
the reader does not add to it. Operator direction, 2026-08-21.

This misreading has already produced unapproved copy twice on a design under
review, and unapproved copy is live in the product — see [[PLAT-019]], which
removes it from the shared reason dialog. It is a real trap, not a tidy-up.

No governing PRD/FRD/ADR: `CLAUDE.md` puts repository rules and conventions in
`CLAUDE.md` and the design authority itself, explicitly not in an ADR, and
`repoDocs` globs only cover `docs/prd|frd|adr`. `docs_todo` satisfies the gate;
the honest answer is that this class of change has no governing doc by design.

## Approach

- Delete `queue` from the banned-words list. Leave line 170 alone — it already
  carries the real rule.
- Reword the exception sentence to name the approved list at line 400 as the
  only source of permitted guidance copy, and to say that list is closed.
- Docs-only. No code, no `site.css`, no markup. The `Queue` mislabel on the
  Inbox message page is [[MAIL-006]]; the shared dialog copy is [[PLAT-019]].

## Verification

- [ ] The document no longer bans a word it mandates at line 464.
- [ ] The exception sentence cannot be read as a test the author applies to
      their own copy.
- [ ] No other file changes in the diff.
