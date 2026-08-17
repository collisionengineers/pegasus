# Research — TICK-009: MAIL-21 classification foundation

*The research. Not the files document — this is what you **learned**, not what you will **touch**.*

## Question

What remains of MAIL-21 now that capabilities.md records the QDOS-route foundation as implemented on `dev`? Which caller, contract, and acceptance-cohort evidence this ticket still owes, versus evidence states that require a separate deploy or operator approval.

## Findings

- MAIL-21 is a QDOS-alpha allocation because it owns shared Core mail policy used by production intake and Graph replay/live callers (`docs/capabilities.md` evaluator-boundary paragraph and MAIL-21 row). The activation note already says: implemented on `dev` for the QDOS route (versioned rules, per-message decision evidence, explicit ambiguity outcome); acceptance cohort, deployment, and live verification remain separate evidence states.
- FRD-08 (`docs/frd/frd-08-email-mailbox-and-background-processing.md`) owns behaviour: classification, queue, Triage routing, and Outlook folder are separate facts; simultaneous category matches are the explicit ambiguity outcome with no invented winner; every decision retains source identity, policy key/version, outcome, evidence, ambiguity facts, actor, and time. Multi-rule precedence and confidence remain an open decision (`docs/open-decisions.md#mailbox-rule-activation-automatic-matching-and-confidence-display`).
- ADR-0008 requires code-versioned route-owned policies, no generic rule engine, persist key/version/evidence, and an acceptance cohort before activating a route. The QDOS classification policy is `QdosMailClassificationPolicy` (`src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailClassificationPolicy.cs`): key `qdos_mail_classification`, version **3**, five always-emitted predicates, fail-closed `Unclassified` / `Ambiguous` / `Classified`.
- `ProcessIntake.EvaluateMailClassification` runs only after an accepted mailbox route and a policy for that `WorkProviderCode`. Manual upload never classifies. Worker has no classifier of its own; it calls `ProcessIntake`. Graph adapters fetch MIME only.
- Decisions persist 1:1 on `IntakeMailClassificationDecisions` (migration `20260803123935_MailClassificationDecisions` plus later `CaseType` and standalone-Audit columns). Re-evaluation snapshots the prior decision into `IntakeReceiptEvents` (`intake_receipt_reevaluated`) then overwrites the current row with the current policy version.
- The QDOS policy classifies only operator-guaranteed tells: body `Triage Only Request`; attachment titles `AUDIT REPORT NOTIFICATION` and `ENGINEER NOTIFICATION` (+ optional `REPORT + AUDIT REPORT` in the same document); subject `Automatic reply:`; reply context from `RE:` only. Nested `, attached email` fragments are ignored so a chaser cannot inherit the original instruction.
- Classification is no longer purely "recorded only": `CaseType` from a classified new-instruction drives `IntakeAllocation`, and a standalone-Audit missing its original report downgrades `CaseCreated` to `NeedsSorting`. Category still does not choose a queue, Triage route, or Outlook folder (`ProcessIntakeTests.ClassificationIsRecordedOnlyAndNeverChangesTheIntakeDecision` still holds for the *intake decision* of a triage-only message).
- Acceptance-cohort **code** exists (`tests/Pegasus.IntegrationTests/QdosEmailCohortTests.cs`, `Category=Corpus`). It expects `corpus/extraction-corpus/QDOS/{audits,inspections,inspection-and-audit,triage}` for labelled accuracy and `corpus/emailevals/{general,received,sent,to-sort}` for volume. This machine's ignored `corpus/` is a **flat** `*.eml` dump (256 emails at the root, no labelled folders, no `emailevals` tree). `QdosCorpus.IsPresent` is therefore false here, so the tests skip. Dated artifacts under `artifacts/evaluation/qdos-classification/` (2026-08-10) came from a different layout. `docs/operations.md` has no MAIL-21 cohort observation.
- `corpus/` is immutable (AGENTS.md / runbook). This ticket must not rename, label, or restructure it. Inventing ground-truth from filenames would fabricate labels.
- Deployment and live verification are separate evidence states. The live-operation approval matrix forbids Outlook/Azure writes without exact-target approval. This ticket does not deploy.
- Downstream UI confirmation, correction history, folder recommendation, and queue mapping are MAIL-04 / MAIL-05 / MAIL-02 / MAIL-23 / UI-10 / UI-14 (`Next / 0.3.0`). `boundaries.md` defers automated application of the settled taxonomy beyond the delivered QDOS-route classification.

## Implications

- Do not re-implement the policy, invent precedence, or activate more families. The remaining MAIL-21 work is the **acceptance-cohort evidence state** that can be produced locally without mutating the corpus.
- Teach the volume cohort to read this machine's flat `corpus/*.eml` tree (and keep the labelled trees when a machine has them). The labelled accuracy test must skip cleanly when those folders are absent, not fail `processed == 0`.
- Record a content-safe dated observation in `docs/operations.md` (counts and outcomes only; no filenames, bodies, or PII). Update the MAIL-21 activation note to distinguish local volume-cohort evidence from labelled-holdout, deployment, and live verification.
- Do not claim operator acceptance, deployment, or production classification from a local volume run.

## Open questions

See `open-questions`. Defaults taken there: no deploy, no invented labels, no new QDOS predicates.
