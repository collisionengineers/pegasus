# Proof — INTK-023 (deployed at release 16; live-tier pending fresh mail)

Type: test-output + command-log. Deployment evidence bundle: [[DELIV-015]] proof.

**Proven now:**
- Deployed to production at release 16 (`4111ad29`); the smoke asserted the exact SHA, so extraction runs the fixed engine + `QdosInstructionExtractionPolicy` Version 4 for every message processed from now on.
- The behaviour claims are proven on the **real letters** at the deployed SHA: 5 unit facts (apostrophe normalization, TP guard, typed-date dedupe, wrapped-line prefix subsumption, ordinal dates) plus `QdosMappingExtractionTests`' per-file expectation table over the operator's corpus (claimant, claim number, registration, make, model, mileage, incident date per mapped EREF file) — green in the merge CI and in the local full run.

**Not claimed yet (honest tier):** live extraction on a production-processed message. The four live test emails were received 08:02–09:18, before this deploy, so their drafts carry Version 2 results. The designed live path for older receipts — "Re-evaluate with current policy" — was exercised during verification and is **broken in production** ([[INTK-027]]: staged source deleted after processing → `staged_artifact_integrity_failure`), so no retroactive live run is possible.

**Completes when:** the operator's first post-wipe QDOS instruction email lands claimant / vehicle / incident date as Facts on the fresh receipt. This ticket stays at verifying until that evidence exists.

---

## Live confirmation, 2026-08-22 — the post-wipe mail this was waiting on

Two real QDOS instructions arrived after the storage clear. Production `CaseDataFields`:

**QDOS26010**

```
claimant_name         fact  Mr James Ainsworth
claim_number          fact  LEB//47837/1
incident_date         fact  2026-08-18
vehicle_registration  fact  LG64JAU
vehicle_make          fact  RENAULT
vehicle_model         fact  TRAFIC SL27 SPORT DCI
instruction_date      fact  2026-08-22
work_provider_code    fact  QDOS
```

**QDOS26009**

```
claimant_name         fact  Mr David Smith
claim_number          fact  SCL/ND/47620/1
incident_date         fact  2026-08-10
vehicle_registration  fact  DF18FEJ
vehicle_make          fact  BMW
vehicle_model         fact  420D M SPORT
```

Claimant, vehicle and incident date extracted from the real letter shapes, on two
independent instructions, both classified `fact` rather than suggestion. That is the
operator's original regression — *"Claimant name not being extracted from documents"* —
closed with live evidence rather than corpus replay.

The registration parsed cleanly on both, which was the specific shape [[INTK-028]] later
had to correct for the report grammar; the letter grammar this ticket owns was already
right.

## Evidence tier

**Observed in production.** Read directly from the deployed database for cases created by
the live pipeline after Release 16 and again after Release 17. No corpus fixture involved.
