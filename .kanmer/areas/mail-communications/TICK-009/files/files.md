# Files — TICK-009

*The files document. Not the research — this is the **surface area** of the change, not the findings behind it.*

Surveyed BEFORE planning. Two tables, and the second is the one that earns its keep.

## Where the change lands

What this ticket will modify, and why each file is in scope.

| Path | Why |
| --- | --- |
| `tests/Pegasus.IntegrationTests/QdosEmailCohortTests.cs` | Volume roots currently miss this machine's flat `corpus/*.eml` layout, so `IsPresent` is false and the MAIL-21 cohort never runs. Teach volume discovery to include the corpus root when it contains `.eml` files; skip the labelled tests cleanly when labelled folders are absent. Read-only; do not write into `corpus/`. |
| `docs/operations.md` | Dated-evidence owner. Add a content-safe local volume-cohort observation after the run (counts/outcomes only). |
| `docs/capabilities.md` | MAIL-21 activation note must distinguish local volume-cohort evidence from labelled holdout, deployment, and live verification. |

## Context files

What an implementer must **read** to avoid a trap — files they will not necessarily edit.

| Path | What it tells the implementer |
| --- | --- |
| `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailClassificationPolicy.cs` | The only policy. Version 3. Do not add predicates. Nested `, attached email` fragments must stay ignored. |
| `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs` | Outcome/evidence contract the cohort records. |
| `src/Pegasus.Core/Intake/ProcessIntake.cs` | Real caller. Classification runs only after an accepted mailbox route. |
| `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs` (`GenuineQdosCorpus`) | Precedent: per-machine corpus may be absent or differently shaped; skip, do not fail, and never invent files. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Behaviour owner. Ambiguity is the accepted multi-match outcome. |
| `docs/runbook.md` § Corpus safety | Immutable corpus; artifacts under `artifacts/evaluation/`; commit only content-safe summaries. |
| `docs/operations.md` § Dated evidence qualifications | Style for a dated observation that does not claim acceptance. |
| `docs/boundaries.md` | Automated full-taxonomy application is deferred. |

## Ripple effects

- CI without a corpus continues to skip (`QdosCorpusFact`). No CI job should start requiring `corpus/`.
- Local `artifacts/evaluation/qdos-classification/*.csv` are generated evidence and stay gitignored.
- `docs/current-architecture.md` already names the QDOS classification policy; no topology change.
- Do not touch `QdosMailClassificationPolicy` predicates, Worker, Graph, or the Inbox UI.

## Out of scope

- Deploying or live-verifying classification (separate evidence states; need approval).
- Labelling or rearranging `corpus/`.
- Operator acceptance of thresholds.
- Staff confirmation UI, correction history, folder moves, queue mapping (MAIL-04/05/02/23, UI-10/14).
- New QDOS predicates or multi-rule precedence.
- MAIL-22 taxonomy persist tests ([[TICK-010]]).
