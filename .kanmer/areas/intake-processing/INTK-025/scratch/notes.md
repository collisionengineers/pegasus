## Live check, 2026-08-22 — partly confirmed, kept in verifying

The post-wipe instructions this was waiting on arrived. Production `CaseDataFields`:

| | QDOS26009 | QDOS26010 |
| --- | --- | --- |
| `vehicle_make` | BMW | RENAULT |
| `vehicle_model` | 420D M SPORT | TRAFIC SL27 SPORT DCI |
| `accident_circumstances` | **absent** | **absent** |

**Vehicle details are extracted.** But this ticket's claim is specifically that they are
**report-sourced**, and the persisted field carries no attribution I can read back — the
QDOS instruction letter also carries make and model, so a value present on the case does not
by itself prove the report rule fired. Attributing it needs the receipt's evidence trail,
not the case projection.

**The accident-circumstances paragraph rule produced nothing on either instruction.** That
may be correct — neither letter may contain the paragraph shape — or it may be the rule not
matching. Not distinguishable from here.

Held in `verifying` rather than moved to done: two of the ticket's claims are unproven, and
its checklist stands at 6/8 for exactly these items. [[INTK-031]] will make issuer
attribution readable on the extracted facts' provenance, which is what would settle the
first question properly.
