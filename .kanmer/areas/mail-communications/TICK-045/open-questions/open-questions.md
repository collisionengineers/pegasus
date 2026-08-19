# Open questions — TICK-045

No unresolved user-only question remains.

- [x] **Is the allocated post-alpha capability activated for implementation?** — Yes. The operator's 2026-08-18 instruction is to drive all EPIC-006 email-workspace tickets through functional completion. This authorizes repository implementation and local verification, not external Outlook/cloud writes.
- [x] **Does “one shared policy” replace ADR-0008's route-owned automatic policies?** — No. FRD-08 and ADR-0008 together establish one shared taxonomy/manual-decision owner across approved mailboxes while automated predicates remain provider/intermediary-route-owned and versioned.
- [x] **Should MAIL-03 create a separate classification command beside MAIL-04?** — No. Reuse the exact-message Core command/current-decision transaction implemented for [[TICK-046]] and add only missing cross-mailbox contract/evidence. A second business implementation is a stop condition.

## Parked (explicitly deferred)

- [x] **Does MAIL-03 own exact automatic predicates, confidence/precedence rules, or holdout acceptance for additional routes?** — No. Resolved with the operator on 2026-08-19. [[TICK-035]] is the clarified owner of each additional provider/intermediary route's predicates, exclusions, ambiguity/precedence, labelled cohort and untouched holdout, thresholds, activation, and rollback. [[TICK-036]], [[TICK-037]], and [[TICK-038]] own named-mailbox ingestion after that gate. MAIL-03 owns only the shared cross-mailbox classification contract; no duplicate ticket is needed.
- [ ] Real Outlook/cloud activation and live verification — reopen only after explicit approval for exact targets and operations.
