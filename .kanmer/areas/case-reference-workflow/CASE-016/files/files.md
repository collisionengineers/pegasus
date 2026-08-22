# Files

Committed in `3d7f87d6`.

| File | Was | Now |
| --- | --- | --- |
| `Cases/Shared/_CaseHistory.cshtml` | Immutable case history | Case history (becomes **Notes** under [[CASE-017]]) |
| `Cases/Shared/_CaseSummary.cshtml` | Immutable item | Message item |
| `Cases/Shared/_CaseWorkflow.cshtml` | Immutable report approval | Report approval |
| `Cases/Shared/_CaseWorkflow.cshtml` | Immutable report identity | Report identity |
| `Cases/Shared/_CaseWorkflow.cshtml` | Approve immutable report | Approve report |
| `Administration/Index.cshtml` | "Immutable identities, replaced only through a linked successor." | *deleted* |
| `Administration/Principals/Index.cshtml` | "A principal identity is immutable once created; …" | *deleted* |
| `Administration/Principals/Replace.cshtml` | "Letters and numbers only. The new code is normalized to uppercase and is immutable." | *deleted* |

## Why the last three are deletions rather than rewordings

They are not labels — they are explanatory sentences, and one is a field hint. The design
authority bans both independently of the banned word, so replacing them with reworded prose
would have swapped one defect for another. A principal identity being immutable is a rule
the system enforces; a page does not need to narrate it.

## Untouched

`ImmutableItemIdentity` — Outlook's own term for the identifier, and a code identifier
rather than something an operator reads. The ban is on operator-facing copy.
