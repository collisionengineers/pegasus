# Open questions — MAIL-004

No unresolved operator/product question remains. The newly filed ticket and TICK-054's 2026-08-20 decision settle the concrete need: Administrators maintain the names MAIL-13 may assign; MAIL-13 never accepts an arbitrary category string.

- [x] **Who owns the catalogue?** — One global Pegasus catalogue, maintained only by Administrators through a named Core authorization right. It is not per mailbox and is not the Pegasus mail-classification taxonomy.
- [x] **What does one entry contain?** — Internal id, exact trimmed display name, Active/Disabled state and version. Graph id and color remain Outlook facts and are not copied. Duplicate normalized names are refused; entries are disabled, not deleted.
- [x] **How does MAIL-13 consume it?** — The exact-message form carries an internal catalogue id. Core reloads an Active entry and supplies its server-owned display name to the message action. The action preserves unrelated current categories and fails closed for absent/disabled/stale entries.
- [x] **Does Pegasus synchronize Outlook master categories?** — No. MAIL-004 performs no Graph call. MAIL-13 may separately validate exact-mailbox master-list presence using approved `MailboxSettings.Read`; Pegasus does not create, rename, recolor or delete Outlook master categories.
- [x] **Are search or Case linking catalogue callers?** — No. MAIL-11 has no accepted category search predicate or retained category projection. MAIL-09/10 association evidence is unique VRM/exact thread or deliberate Case search, never an Outlook label. Add no dormant fields, filters, indexes or matching rules.
- [x] **Should the ticket proceed if MAIL-13 loses category assignment?** — No. Re-check immediately before planning/take. Without MAIL-13, no concrete downstream consumer exists; close/archive MAIL-004 without code rather than ship administration-only dormant configuration.

## Parked (explicitly deferred)

- [x] **Future category search/filtering** — Deferred until a governing behavior and real Web/Automation caller are accepted. It is not implied by a catalogue.
- [x] **Automatic Outlook master-list synchronization** — Excluded. It would require separately approved `MailboxSettings.ReadWrite`, cross-mailbox reconciliation and external writes, none of which MAIL-13 requires.
- [x] **Live/cloud work** — MAIL-004 has none. Any later Graph master-list read or message mutation follows its owning ticket's exact permission, RBAC, negative-scope and live-operation approval gates.
