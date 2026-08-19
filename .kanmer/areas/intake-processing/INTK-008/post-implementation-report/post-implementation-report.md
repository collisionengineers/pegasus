# Post-implementation report — INTK-008

## Summary

Implemented Image-initiated Case as an explicit lifecycle projection over the
existing ImageIntake aggregate. A usable VRM retains its immutable per-VRM
reference and can be Awaiting instruction, Merged into an Instruction-initiated
Case, or Staff-closed with a reason. No formal Cases row, Principal, Case/PO,
Audit, or Unidentified reference is allocated.

## Changes

- Core ImageIntake contracts now carry lifecycle state, terminal target/reason,
  history, merge/close requests, and bounded transition validation.
- EF persistence stores state/version and append-only lifecycle events with a
  serializable transition and operation-key replay; the migration is
  20260819112914_ImageInitiatedLifecycle.
- Reverse accepted-Case pairing records a merge projection and formal Case
  history event after the existing receipt association succeeds.
- ImageIntake list/detail pages use Image-initiated Case vocabulary, preserve
  exact reference/VRM search, show lifecycle/history, and provide reasoned staff
  closure under the existing staff authorization boundary.
- A distinct IImageIntakeCustody target and local/Box implementations use the
  immutable VRM reference and existing custody composition/root fence; no second
  Box client or formal Case folder is introduced.
- PRD, FRD-01/02/05/06/12, design, capabilities, index, CONTEXT, and operator
  notes were reconciled. ADR-0029 records the technical decision and ADR-0013
  is marked superseded in frontmatter only.

## Verification

- dotnet restore Pegasus.slnx: passed.
- Release builds: Core, Infrastructure, Web, IntegrationTests, and
  ArchitectureTests passed with 0 warnings/errors in the completed builds.
- Focused ImageIntake Core tests: 40 passed, including lifecycle validation,
  pairing, replay, and existing registration coverage.
- SQL/web integration execution was attempted through the existing test
  harness; no external Box mutation was performed. Full integration and full
  solution test commands remain for merged-main verification.

## Risks / follow-ups

- Existing ImageIntake source-file/group projection remains the source of
  preserved filenames and ordinals; a future UI slice may show a richer grouped
  asset panel.
- Box custody root creation is exposed through the existing guarded adapter but
  is not automatically invoked during local registration; external custody
  dispatch remains governed by the existing queued custody boundary.
- INTK-007 owns durable U<n> Unidentified allocation and conflicting_vrms
  persistence; INTK-006 owns grouped recognition and routing.
