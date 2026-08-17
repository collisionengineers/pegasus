# Checklist — TICK-009

*The checklist. Not the plan — every line is **independently tickable**; the reasoning lives in the plan.*

Derived from plan.md, one box per step. Tick with `set_ticket_doc` as you complete them (the GUI renders these as live checkboxes); append progress notes below rather than rewriting.

- [ ] Volume roots include a flat `corpus/*.eml` tree without double-counting `emailevals` / labelled trees
- [ ] `IsPresent` is true when any discovered root contains an `.eml`
- [ ] Labelled accuracy and claim-token facts skip when labelled folders are absent
- [ ] Volume cohort still writes `artifacts/evaluation/qdos-classification/cohort-results.csv` and asserts `processed > 0`
- [ ] Local `QdosEmailCohortTests` run: labelled skipped, volume processes this machine's dump
- [ ] Dated content-safe observation added to `docs/operations.md`
- [ ] MAIL-21 activation note in `docs/capabilities.md` distinguishes volume-cohort / holdout / deploy / live
- [ ] Focused tests: `QdosMailClassificationPolicyTests`, `QdosEmailCohortTests`, classification facts in `ProcessIntakeTests`

## Progress notes

(append with `set_ticket_doc(doc: "checklist", append: true)`)
