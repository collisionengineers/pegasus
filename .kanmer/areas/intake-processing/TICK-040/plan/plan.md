# TICK-040 plan

INT-15 is implemented by [[SIMPLI-013]] (one PR, one branch, shared with INT-14/TICK-039); that ticket's plan owns sequencing, commands, and the simplification pass. This ticket tracks the capability to review alongside it.

- Contract: `.msg` sources yield body text, sender/subject transport evidence, and by-value attachments through `IIntakeSourceReader`; attachments re-enter the existing per-format pipeline (no second extraction route).
- Failure behaviour: unreadable, protected (S/MIME/rpmsg), or oversized items fall back to the manual-sorting outcome; reference/OLE-only attachments stay passive with an explicit issue.
- Acceptance: SIMPLI-013's PR merges with the `.msg` web tests green and existing PDF/DOCX/EML behaviour unchanged.

## Simplification pass

Carried by SIMPLI-013's plan (same diff).
