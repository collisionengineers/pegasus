# TICK-039 plan

INT-14 is implemented by [[SIMPLI-013]] (one PR, one branch, shared with INT-15/TICK-040); that ticket's plan owns sequencing, commands, and the simplification pass. This ticket tracks the capability to review alongside it.

- Contract: `.doc` sources extract text through `IIntakeSourceReader` (existing port, no second pipeline); `.doc` attachments inside emails and `.msg` files route through the same dispatch.
- Failure behaviour: unreadable/encrypted/oversized containers fall back to the manual-sorting outcome (no crash, no new failure codes).
- Acceptance: SIMPLI-013's PR merges with the `.doc` web tests green and existing PDF/DOCX/EML behaviour unchanged.

## Simplification pass

Carried by SIMPLI-013's plan (same diff).
