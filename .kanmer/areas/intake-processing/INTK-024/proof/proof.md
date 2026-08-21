# Proof — INTK-024 (delivered and operator-approved, closed at release 16, 2026-08-21)

Type: command-log. The ticket's deliverable was research: consolidate the corpus and present a full QDOS mapping + methodology for approval.

- Corpus consolidated locally (never committed): `reference/qdosmapping/` moved into `corpus/qdosmapping/`; 2,287 collisionsuite reference files (documentexamples, cereference, cereference/instructions) swept and integrated; git history searched across all refs — no larger recoverable corpus exists (proven by `git log --all --diff-filter=AD -- corpus/` + object-name scan).
- The full per-file mapping + methodology was presented on the operator-facing artifact (https://claude.ai/code/artifact/abb2c56d-a857-474a-add5-0b6c7e1875b0) and **approved** with two operator rulings honoured: EREF10 = Inspection + Audit provable from the letter alone (no third-party report with the email), and all mapping rules made QDOS-specific (the TP guard and report grammar live in `QdosInstructionExtractionPolicy`, not the neutral engine).
- The methodology's durable form now lives in the repository: `docs/principal-rules-and-mappings/qdos.md` (committed to dev e9cdf2b2, promoted to main with release 16 — [[DELIV-015]] proof), and the per-file expectation table is enforced by `QdosMappingExtractionTests` over the real corpus (skip-if-absent), green at the deployed SHA.
