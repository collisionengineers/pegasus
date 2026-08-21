# Proof

**Shipped:** PR #477, merge `e4d56d9e` · **Deployed:** ancestor of `4111ad29` → true (Release 16, active revision `…--4111ad291779`).

## The finding

> FRD-08 requires a later reclassification with a different designated folder to offer
> another separate confirmation. PR #477 permanently refuses any message with one succeeded
> move and hides the action whenever the latest move succeeded, regardless of the current
> classification/recommendation.

## Verified in the shipped code

`src/Pegasus.Core/Intake/RetainedMail.cs:61` carries
`MailLogicalFolderType? CurrentFolderType` on the retained-mail record. That is the
structural fix the finding asked for: the latest successful destination is held as **current
location**, a value that can be compared, rather than a permanent "moved" flag that can only
latch.

Because the recommendation is recomputed from the current classification
(`MailLogicalFolderPolicy.Map(dossier.Current)`, `:579`) and compared against
`CurrentFolderType`, a reclassification whose approved destination differs from where the
message now sits offers a new confirmation, and one that agrees does not.

The **arrival** folder is a separate, immutable field — the retained message's original
folder is not overwritten by a move, so "the original retained arrival folder remains
immutable across multiple moves" holds by there being two fields rather than one being
mutated.

## Not claimed

Multi-move sequences are covered by `RetainedMailPersistenceTests`. No live message has been
reclassified and re-moved in production, and this proof does not claim one has.
