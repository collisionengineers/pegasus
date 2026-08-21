---
id: PLAT-019
type: ticket
title: Strip unapproved copy from the shared reason dialog
status: implementing
area: platform-operations
assignee: claude-code
profile: fix
stageEntered:
  implementing: '2026-08-21T10:43:07.556Z'
taken_at: '2026-08-21T10:42:20.084Z'
branch: task/mail-006-inbox-message-page
worktree: ../pegasus-worktrees/mail-006
labels:
  - ui
  - web
  - design-authority
links: []
blocks:
  - MAIL-006
docs_todo: true
archived: false
created: '2026-08-21T09:37:37.987Z'
updated: '2026-08-21T10:43:07.556Z'
---

## What

Remove the guidance copy from `src/Pegasus.Web/Pages/Shared/_ReasonDialog.cshtml`
and from every call site that supplies it:

- the partial's `DialogConsequence` default — *"A reason is required to perform
  this permanent business action."*
- the `Required.` hint under the reason textarea
- the `placeholder="Enter clear business reason…"`
- the four `DialogConsequence` values passed on `/Inbox/{id}` — the folder-move
  sentence, the link sentence, the unlink sentence, and any other call site's

## Why

None of it is on the approved necessary-copy list at
`docs/design/README.md:400`, and that list is closed — only the operator adds
to it (operator direction, 2026-08-21). `README:426` separately forbids a hint
sentence under a field and any "Required." text; required state is shown by
the required-marker styling and `aria-required`, never as prose.

The partial is shared, so this is not a page-local change: every reasoned
action in the product renders it. That is exactly why it is its own ticket
rather than a line in [[MAIL-006]] — the diff reaches screens that redesign
never touches.

The dialog does not become unclear without them. A dialog titled *Move to
Instructions* with a labelled reason field and a Move button already says what
it does; a title naming the target is the design's replacement for the
sentence.

No governing PRD/FRD/ADR — this is a design-authority rule applied to shared
markup, and `CLAUDE.md` places repository rules outside the ADR log.
`docs_todo` satisfies the gate; the honest answer is that this class of change
has no governing doc by design.

## Approach

- Delete the four elements above from the partial. Keep the label, the control,
  the required marker, and the buttons.
- Sweep every `DialogConsequence` in `src/Pegasus.Web/Pages/**` and remove it;
  where the sentence carried the only statement of the target, move that into
  the dialog **title** (`Move to Instructions`, `Link to QDOS/2026/001`), which
  the call sites already do for link and unlink.
- Check whether any existing test asserts the removed strings and update those
  assertions rather than the markup.
- Leave `_ReasonDialog`'s focus trap, Escape handling and focus return exactly
  as they are.

## Verification

- [ ] `grep -r DialogConsequence src/Pegasus.Web` returns nothing.
- [ ] No `Required.`, no placeholder, no consequence sentence in the partial.
- [ ] Every dialog still names its target in the title.
- [ ] Accessibility tests still pass for the routes that open a dialog; the
      required field is still announced.
- [ ] `dotnet test` green for `Pegasus.IntegrationTests`.
