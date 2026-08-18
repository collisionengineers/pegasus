# Checklist — TICK-009

*The checklist. Not the plan — every line is **independently tickable**; the reasoning lives in the plan.*

Derived from plan.md, one box per step. Tick with `set_ticket_doc` as you complete them (the GUI renders these as live checkboxes); append progress notes below rather than rewriting.

- [x] Volume roots include a flat `corpus/*.eml` tree without double-counting `emailevals` / labelled trees
- [x] `IsPresent` is true when any discovered root contains an `.eml`
- [x] Labelled accuracy and claim-token facts skip when labelled folders are absent
- [x] Volume cohort still writes `artifacts/evaluation/qdos-classification/cohort-results.csv` and asserts `processed > 0`
- [x] Local `QdosEmailCohortTests` run: labelled skipped, volume processes this machine's dump
- [x] Dated content-safe observation added to `docs/operations.md`
- [x] MAIL-21 activation note in `docs/capabilities.md` distinguishes volume-cohort / holdout / deploy / live
- [x] Focused tests: `QdosMailClassificationPolicyTests`, `QdosEmailCohortTests`, classification facts in `ProcessIntakeTests`

## Progress notes

- Harness updated: volume roots fall back to a flat `corpus/*.eml` dump; labelled facts use `QdosLabelledCorpusFact`.
- Worktrees do not copy ignored `corpus/`; discovery walks the common git dir to the primary checkout. Corpus is read-only.
- 2026-08-17 volume run: 256 EML; routes 75 accepted / 167 no-match / 13 needs-sorting / 1 unreadable; accepted-route 14 classified (8 pre-instruction, 3 audit, 3 inspection) / 61 unclassified / 0 ambiguous. Labelled facts skipped. Core focused tests: 29 passed.

## Closeout — TICK-009 (2026-08-18)

- [x] PR #391 MERGED 2026-08-17T13:59:38Z
- [x] proof.md written on merged `main` `f1e116c6`; moved to Done; Outcome recorded; deployment = production (release 9)
- [x] Worktree `../pegasus-worktrees/tick-009-mail-21-classification-foundation` removed; local + remote branch deleted; prune
- [x] Released
