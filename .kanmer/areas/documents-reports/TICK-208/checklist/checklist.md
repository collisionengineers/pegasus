# Checklist — TICK-208

- [ ] After SIMPLI-014 and DOCS-001 merge, re-read their exact merged report-version contracts/persistence/proof; name the reused types/stores and either narrow TICK-208 to missing Sent association work or close no-code if all acceptance is already met.
- [ ] Add the minimal Core version-specific approval/Sent association and ordered history contract using DOCS-001 immutable report identities and existing exact approved-mailbox evidence types, without a second report aggregate or CASE-23 states.
- [ ] Add append-only persistence/configuration/migration for version→approval→Sent evidence and reasoned association history, preserving all legacy rows with explicit unresolved provenance and no fabricated artifact match.
- [ ] Make staff link/reassociate and Worker auto-link version-aware, chronological, idempotent and fail-closed on missing/ambiguous/mismatched/duplicate evidence while preserving former associations.
- [ ] Prove correction/addendum creates an unsent successor, preserves the predecessor's final Sent evidence/time, and binds a new exact Sent item only to the successor without changing CASE-23 lifecycle.
- [ ] Adapt existing Case/detail/report-evidence projections and mutations to expose exact version history/current status and explicit legacy ambiguity, adding no send operation or external-delivery claim.
- [ ] Add focused Core/integration/migration/concurrency tests covering retention, lineage, replay, stale version, ambiguity/hash mismatch, reassociation history, conservative legacy migration, and staff/Worker races.
- [ ] Run locked restore, Release build, focused/full tests, migration inspection and negative source checks; run the simplification pass, update current-state docs only at the proved tier, and write the post-implementation report.

## Progress notes

(append with set_ticket_doc(doc: "checklist", append: true))
