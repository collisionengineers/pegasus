# B04 review round 3 — durable callback claim, G23–G25 (2026-09-07)

## Commits on `task/pegasus-v1-casework`

- `bcca08adc` / `3c7c32d95` — G23 `db7809af6` (custody result carries the
  minted `OccurrenceId`) and G24 `711eeab4b` (custody status lookup takes
  the occurrence id) merged as the same objects.
- `8d7259405` — durable callback claim (A 5564520110 item 1): `CompleteAsync`
  persists `Importing` + the verbatim query's digest through the store's
  version CAS before the relay; a CAS loser re-reads the record (same digest
  → the session as it stands, different digest → `Callback` conflict); a
  lost answer after the claim settles `Unknown` with the claim kept; a
  resume of a claimed `Importing`/`Unknown` session signs in, selects the
  vehicle and looks the export up again (`ExportAsync`, the read-only half
  of completion) — the relay is never repeated. Callback page catches only
  `GlassRepairEstimateSessionConflictException` (A 5564823199); unrelated
  faults surface as 500, proved over HTTP with the real host. G23/G24: the
  protected `Artifact.OccurrenceId` names the import and the status lookup;
  B05's Pending/Unknown retry reads by `FindByOperationKeyAsync(artifact
  .OperationKey)` (G15 recovery identity) because the report artifact record
  holds no occurrence id; B-owned doubles and constructor slots adapted.
  Five memory-store claim proofs.
- `f10fc6d9e` — two real-SQL race proofs over `EfGlassRepairEstimateSessionStore`
  on LocalDB with the gate holding both claims (A 5565056030 evidence
  correction: the five earlier proofs are memory-store).
- `960b262a3` / `53d22ec86` — G25 `2a70e55df` merged; B07 send doubles build
  the returned `StaffMailOperation` from the command's own mail context.

## Evidence

- Standalone (53d22ec86): build 0/0; Core 1489/1489; Architecture 100/100;
  `GlassRepairEstimateGatewayTests` 68/68; Gateway + Persistence + XmlParser
  + CaseReportGenerationPersistence 157/158 at 8d7259405 (the one is the
  cross-store key proof, A's store only).
- Combined (isolated tree: shared ref 8121d80b5 + G24 + A's custody commits
  dfe10e543/fe3535cda/697e455b3 + B delta at f10fc6d9e; C's
  `PublicUploadRetentionWebTests`/`RetainIncomingArtifact` shimmed locally
  for G24 compilation only, never published): Glass's suites + Case page +
  report persistence + `CaseArtifactCustodyRecoveryTests` +
  `ProductionCompositionTests` **267 PASS / 0 FAIL / 0 SKIP**, 7m17s, TRX
  `v1-b-f10fc6d9e-ref.trx` (copy in `/tmp`). The former occurrence-id
  failures pass end to end on the real host with A's adapter.
- PR 672 comments 5565031279, 5565125321, 5565158910, 5565220874.

## Dispositions

- A's custody implementation commits cannot be cherry-picked onto B (the
  adapter files exist only on the shared ref → DU conflict); A confirmed:
  use the complete verification checkout for runtime proof and keep the
  standalone qualification.
- Case page launch/resume keep catching `InvalidOperationException`: the
  frozen contract has no typed refusal for "no enabled account" / "not
  resumable"; offered to add one if A wants it.

## Open

- Test UI snapshots for the changed Case page and the callback route
  (combined host), B09 fresh review at the exact head, simplification-pass
  record for the B04/B08 slices.
