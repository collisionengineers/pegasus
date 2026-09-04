# Open questions — CASE-032

- [ ] What immutable, operator-facing business reference must every Triage row display — its exact format and authoritative source? The current Triage model has only its storage ID and registration; no operator-facing Triage reference exists.
- [ ] What provider value must display for each supported Triage origin (selected mail route, Provider API, and manual classification), and what must the row show when no provider is known? The current Core value is only a mail-route work-provider code, not a provider display name, and it does not cover Provider API or manual Triage.
