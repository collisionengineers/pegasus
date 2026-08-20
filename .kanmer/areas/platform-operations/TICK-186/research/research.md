# Research — TICK-186: extraction cohort and untouched holdout

## Where this sits

- MAIL-21 (`[[TICK-009]]`, done) shipped the shared Core classification
  foundation and its own volume cohort over the local corpus. This ticket is
  a separate step: it owns INT-21 — "Human-reviewed extraction cohort,
  holdout, and field-level accuracy reporting" (`docs/capabilities.md:107`,
  owned by `docs/frd/frd-02-intake-and-source-identity.md`), i.e. the
  QDOS field-extraction acceptance path, not mail classification.
- `[[TICK-217]]` ("Accept per-field extraction thresholds with zero false
  case creation") was archived and consolidated into this ticket. Its
  acceptance steps now live in this ticket's `checklist.md` under "Migrated
  validation". Those steps (review cohort evidence, evaluate holdout at
  fixed thresholds, confirm zero false case creation, record accepted
  thresholds) require: (a) a human-reviewed, labelled ground-truth cohort,
  and (b) an operator acceptance decision. Neither exists yet on this
  machine — see Findings below. They remain unchecked and are explicitly
  parked, not guessed.

## What this ticket's own text asks for

The ticket body only asks to: re-check the current source/caller/evidence
state at activation, and write a task-level plan without inferring authority
for live/credential/external operations. It does not itself claim the
labelled-accuracy work — that is TICK-217's migrated scope, sitting in the
checklist as follow-on. This ticket's actual, doable scope is: assemble a
deterministic extraction-development cohort and an untouched holdout from
the documents actually present locally, with a manifest that fences the
holdout from use.

## Findings — what `corpus/` actually contains on this machine (verified 2026-08-20)

- `corpus/` (repo root, gitignored, immutable) contains **256 `.eml` files**
  directly at its top level, plus two non-corpus tooling files
  (`test_explode_eml.py`, `test_explode_eml.cpython-314-pytest-9.0.3.pyc`) —
  excluded, not instruction documents.
- **No PDFs, `.doc`, or `.msg` files** exist at the corpus top level on this
  machine. QDOS instruction PDFs, where present, would only be as
  attachments inside the `.eml` files — decomposing attachments is
  extraction work itself, out of this ticket's "assemble a cohort of
  documents" scope.
- **No labelled `extraction-corpus/QDOS/{audits,inspections,
  inspection-and-audit,triage}` tree exists.** This is the exact tree
  `tests/Pegasus.IntegrationTests/QdosEmailCohortTests.cs`
  (`QdosCorpus.HasLabelledFolders`, `QdosLabelledCorpusFactAttribute`) is
  already built to consume when present, and it already skips gracefully
  when absent — confirming this is an expected, not novel, per-machine gap.
  `docs/operations.md#dated-evidence-qualifications`'s 2026-08-17 MAIL-21
  entry recorded the identical gap on the same corpus population.
- `docs/runbook.md#corpus-safety-and-evaluation` records a much richer,
  **dated (2026-07-23), other-workstation** corpus observation (9,443 files,
  `emailevals`/`qdos-email-corpus`/`test folder`, multi-format including
  PDF/DOC/MSG) — explicitly flagged there as "dated observations, not an
  evergreen inventory." Corpus contents are per-machine by design
  (`docs/runbook.md`, `QdosCorpus.DiscoverCorpusRoot`). This machine's 256
  flat `.eml` files are what is actually here, and match what the
  2026-08-17 MAIL-21 volume-cohort run used.

## Consequence for this ticket

Assembling a cohort/holdout split over the 256 present `.eml` files is fully
supported by what exists locally. Producing a *labelled*, ground-truth cohort
and running TICK-217's threshold-acceptance checklist against it is not
possible on this machine today — it needs the `extraction-corpus/QDOS/{...}`
tree and an operator decision. That work is left parked in the checklist,
not attempted here, per the "report honestly and stop" instruction rather
than fabricating labels or guessing thresholds.

## Existing convention reused

- `docs/runbook.md#corpus-safety-and-evaluation`: "Write manifests, extracted
  content, hashes, predictions, screenshots, and detailed reports beneath
  `artifacts/evaluation/`." — reused directly; the new manifest lives under
  `artifacts/evaluation/extraction-cohort/20260820/`, alongside the existing
  `artifacts/evaluation/qdos-classification/` precedent from the MAIL-21
  cohort.
- Split methodology mirrors the accepted INT-17/VRM precedent
  (`docs/operations.md#dated-evidence-qualifications`): an ~80:20
  cohort:holdout ratio, and a holdout meant for a one-time confirmation run,
  never iterated against.
- Deterministic, seed-free split: sort files by their own SHA-256 content
  digest and cut at the ratio boundary — reproducible without an external
  RNG/seed value, and the same content-identity convention the existing
  corpus tests already use (`MultiFormatGenuineCorpusWebTests.cs` pins files
  by SHA-256).
