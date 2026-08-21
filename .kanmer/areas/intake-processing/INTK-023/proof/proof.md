# Proof — INTK-023 (deployed at release 16; live-tier pending fresh mail)

Type: test-output + command-log. Deployment evidence bundle: [[DELIV-015]] proof.

**Proven now:**
- Deployed to production at release 16 (`4111ad29`); the smoke asserted the exact SHA, so extraction runs the fixed engine + `QdosInstructionExtractionPolicy` Version 4 for every message processed from now on.
- The behaviour claims are proven on the **real letters** at the deployed SHA: 5 unit facts (apostrophe normalization, TP guard, typed-date dedupe, wrapped-line prefix subsumption, ordinal dates) plus `QdosMappingExtractionTests`' per-file expectation table over the operator's corpus (claimant, claim number, registration, make, model, mileage, incident date per mapped EREF file) — green in the merge CI and in the local full run.

**Not claimed yet (honest tier):** live extraction on a production-processed message. The four live test emails were received 08:02–09:18, before this deploy, so their drafts carry Version 2 results. The designed live path for older receipts — "Re-evaluate with current policy" — was exercised during verification and is **broken in production** ([[INTK-027]]: staged source deleted after processing → `staged_artifact_integrity_failure`), so no retroactive live run is possible.

**Completes when:** the operator's first post-wipe QDOS instruction email lands claimant / vehicle / incident date as Facts on the fresh receipt. This ticket stays at verifying until that evidence exists.
