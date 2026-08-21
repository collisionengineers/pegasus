# Proof — INTK-025 (deployed at release 16; live-tier pending fresh mail)

Type: test-output + command-log. Deployment evidence bundle: [[DELIV-015]] proof.

**Proven now:**
- Deployed to production at release 16 (`4111ad29`): `QdosInstructionExtractionPolicy` Version 4 with the operator-approved QDOS-specific rules — report-sourced vehicle facts (report-titled documents only, letter always outranks, digit-guarded `Speedo:`) and the accident-circumstances paragraph (prompt anchor + block terminators).
- Behaviour proven on the real corpus at the deployed SHA: 5 unit facts (report backfill, letter-outranks, non-report `Vehicle:` contributes nothing, circumstances lands and stops at the damage block, promptless letters stay empty) plus the corpus mapping table's `CircumstancesStart` pins (EREF8/10/5/9-Harvey; audit letters provably carry no prompt) and the EREF8 VAUXHALL/ASTRA GS TURBO pin — green in merge CI.

**Not claimed yet (honest tier):** live extraction on a production-processed message — same dependency and same broken re-evaluation path as [[INTK-023]] ([[INTK-027]]).

**Completes when:** the operator's first post-wipe QDOS instruction email with a bodyshop report and/or circumstances prompt lands the report-sourced fields and the circumstances paragraph on the fresh receipt.
