# Checklist — INTK-008

- [ ] Re-read files.md and record implementation boundary in scratch.
- [ ] Add Core Image-initiated lifecycle states, records, commands, and ports.
- [ ] Add transition policy, actor/reason validation, terminal/replay/conflict rules.
- [ ] Add persistence projection and append-only lifecycle events.
- [ ] Add DbContext mapping and additive migration with AwaitingInstruction backfill.
- [ ] Implement transactional lifecycle transitions and query projections.
- [ ] Invoke merge projection from successful reverse formal-Case pairing.
- [ ] Add VRM-reference custody target through existing Box/local adapter boundary.
- [ ] Update ImageIntake list/search labels and lifecycle filters.
- [ ] Update details with state, filenames/group evidence, custody, merge history, and staff-close form.
- [ ] Update formal Case search/details with Image-initiated reference and merge history.
- [ ] Add Core transition/replay/pairing tests.
- [ ] Add SQL/web/authorization/search/merge/closure tests.
- [ ] Add architecture/composition custody test and prove no formal Case row.
- [ ] Amend PRD, FRD-01/02/05/06/12, design, capabilities, index, CONTEXT.
- [ ] Add ADR-0029 and supersede ADR-0013 without editing its accepted body.
- [ ] Run simplification pass and record dispositions.
- [ ] Run restore, Release build, focused tests, integration tests, architecture tests, and full test.
- [ ] Write post-implementation-report with governing-doc traceability and verification commands.
- [ ] Push branch, open PR targeting dev with Kanmer: INTK-008, record PR, and move to Review.

## Implementation progress — 2026-08-19

- [x] Re-read files.md and kept the implementation on the existing ImageIntake/Formal Case seams.
- [x] Added Core Image-initiated states, merge/close commands, history records, and transition validation.
- [x] Added SQL lifecycle projection/events, migration 20260819112914_ImageInitiatedLifecycle, replay/CAS transition, and formal Case merge history.
- [x] Wired reverse accepted-Case pairing to record the Image-initiated merge projection.
- [x] Updated Image-initiated list/detail labels, state/history presentation, search wording, and reasoned staff closure.
- [x] Reconciled PRD, FRD-01/02/05/06/12, design, capabilities, index, CONTEXT, operator notes, and ADR index; added ADR-0029 and superseded ADR-0013 frontmatter.
- [x] Core lifecycle/pairing tests pass: 40 tests.
- [ ] VRM-keyed Box adapter invocation and custody state presentation still need final implementation/verification before PR.
