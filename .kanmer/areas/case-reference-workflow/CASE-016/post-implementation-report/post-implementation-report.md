# Post-implementation report

**Branch:** `task/qdos26009-operator-fixes` · **PR:** #506 · **Commit:** `3d7f87d6`

## What was built

Five labels lose the word; three sentences are deleted outright.

The three Administration occurrences were not labels — they were explanatory sentences, and
one was a field hint. `docs/design/README.md` bans both independently of the banned word,
so rewording them would have swapped one defect for another. A principal identity being
immutable is a rule the system enforces; a page does not need to narrate it.

Nothing was written to replace anything. The approved necessary-copy list is closed.

## Checked before changing

Grepped the tests for all eight strings first — none asserted them. That check exists
because CI caught exactly this on [[MAIL-010]] the day before: two `MailWorkspaceWebTests`
were pinned to copy that had just been deleted, and both were testing wording where the
behaviour was the point.

## Untouched

`ImmutableItemIdentity` — Outlook's own term, and a code identifier rather than something
an operator reads. The ban is on operator-facing copy.

## Evidence

- A scan of `src/Pegasus.Web/Pages/**/*.cshtml` for the word now returns only Razor
  comments and code identifiers
- `Pegasus.Web` builds clean
- Live: the case and Administration pages — Phase 6

## Interaction with CASE-017

`_CaseHistory.cshtml`'s heading became "Case history" here and then **Notes** under
[[CASE-017]] in the same branch. Sequenced deliberately: this ticket owns the vocabulary,
that one owns the surface.
