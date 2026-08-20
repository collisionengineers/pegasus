# Open questions — TICK-081

No operator-only question about EXT-08's caller topology remains.

- [x] **Must every document/report type use one shared caller/service?** — Yes. Resolved by the Collision Engineers operator on 2026-08-20: every type reaches the same Core-owned function/service in the Pegasus .NET monolith. A type may supply a different approved template as typed input to that service; it must not introduce a type-specific caller, service, renderer family, host, or deployment unit.
- [x] **Do Audit and Inspection use that same caller?** — Yes. Their accepted workflow/reference provenance may differ, but both reach the same shared service. RPT-03 additionally requires the same approved physical Inspection presentation for Audit.
- [x] **Does TICK-081 itself perform a separate Azure deployment?** — No. It consumes the existing Web Container App boundary and exact deployment proof from the owning release/deployment task. Any future cloud write still needs explicit exact-target approval.

## Parked (explicitly deferred)

- [ ] **Which rate-card owner and EXT-09 derivation formulas are accepted?** — Safe to defer from this ticket because [[TICK-082]] owns the unresolved product/calculation authority recorded in `docs/open-decisions.md`. It reopens TICK-081 planning only after TICK-082 has accepted evidence; TICK-081 must not guess it.
- [ ] **Which template/content activates for diminution, addendum, valuation evidence, letters, or other future families?** — Safe to defer because [[DOCS-003]], [[DOCS-004]], and each owning capability require their own supplied/accepted template and real workflow. The shared service seam is preserved without exposing dormant selections.
- [ ] **How is final Sent evidence associated through later correction?** — Safe to defer from base generation mechanics to [[TICK-208]], provided the durable report/version identity created by DOCS-001/TICK-081 is stable and linkable.
