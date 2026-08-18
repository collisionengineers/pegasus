# Open questions — TICK-045

No unresolved user-only question remains.

- [x] **Is the allocated post-alpha capability activated for implementation?** — Yes. The operator's 2026-08-18 instruction is to drive all EPIC-006 email-workspace tickets through functional completion. This authorizes repository implementation and local verification, not external Outlook/cloud writes.
- [x] **Does “one shared policy” replace ADR-0008's route-owned automatic policies?** — No. FRD-08 and ADR-0008 together establish one shared taxonomy/manual-decision owner across approved mailboxes while automated predicates remain provider/intermediary-route-owned and versioned.
- [x] **Should MAIL-03 create a separate classification command beside MAIL-04?** — No. Reuse the exact-message Core command/current-decision transaction implemented for [[TICK-046]] and add only missing cross-mailbox contract/evidence. A second business implementation is a stop condition.

## Parked (explicitly deferred)

- [ ] Exact automatic rule predicates, confidence thresholds, precedence, and holdout acceptance for additional provider/intermediary routes — reopen only with the source-labelled cohort and operator acceptance required by ADR-0008 and `docs/open-decisions.md`.
- [ ] Real Outlook/cloud activation and live verification — reopen only after explicit approval for exact targets and operations.
