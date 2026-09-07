# Post-implementation report — provisional

## Status and authority

This is a provisional implementation report for INTK-060. It records the
current Stream C implementation and evidence only; it is not a review
attestation, proof, stage move, checklist completion, merge authorization or
deployment claim.

The governing packet remains `plan/plan.md`, its closeout record, and
`pegasus_pack/astra_output/v1_implementation_plans/streams/C-intake.md`.
The ticket remains in Implementing while the remaining checks, formal
independent review and integration proof are completed.

## Source and delivery boundary

The common pushed source head is
`12f45d193919c4cac7c0563c40338e2f33f38c71`. PR [#674] is the sole
integration PR and is open against `dev`. PRs #672 and #673 were closed as
superseded, not merged; their branches and histories remain preserved. No
commit has been merged to `dev`, no `dev` to `main` PR is claimed, and no
cloud, provider, mailbox, Box, Glass's or EVA write occurred.

## Implemented scope and caller evidence

C01--C08 were checked against the current plan, Stream C file ownership and
the existing C source-review records. The reachable production paths retain one
Core policy owner: intake analysis persists source-linked unresolved candidates
and withholds allocation; structured extraction/OCR remains on the existing
external-work path; profile and QDOS policy keep distinct business roles and
fail closed on ambiguity; pre-case Triage/custody/upload paths retain exact
source identity; report extraction and directory administration retain
provenance; and the operator shell, correspondence, notifications, Inbox,
Search and Work Centre call their typed production surfaces.

The prior focused source/caller audits are retained in
`scratch/review-c01.md`, `review-c02.md`, `review-c05.md`,
`review-c06.md`, `review-c07.md`, `review-c07b.md`,
`review-c07c.md` and `review-c08.md`. They are implementation evidence,
not the required final independent exact-head review.

## Recorded validation evidence

- At `a3769c1ac3f98cc5300da8bdb4504d7c3995aa35`, the build passed with
  zero warnings and errors.
- CI run 34122468605 on the current head reports Core 1818 passed, 14 skipped,
  and Architecture 109 passed. The CI-only one-worker Core setting at the
  current head addresses the earlier parallel Windows 100 ms regex timeouts;
  it does not alter production extraction grammar or its timeout budget.
- The custody, public-upload and Glass focused classes passed 131 tests with
  one skip. The corrected mapping-photo test passed 1 test with no skips.
  Glass discovery produced 21 rows and no unexpanded methods.
- Earlier corpus evidence at `886f94df9`: 41 passed, one failure associated
  with the separately approved six-MP release gate, and one conditional PNG
  skip. The PNG test remains intact: its fact attribute skips only when this
  machine lacks the pinned immutable sample, explicitly because corpora differ
  per system. It is neither a pass nor an integration-waiver requirement.
  Mapping checks passed 8; labelled mapping checks passed 2; DOCX passed 1;
  pack Core passed 64.

Earlier failures remain part of the record: stale estimate and custody test
fixtures were aligned to recorded/persisted contracts, and the intermittent
CI failures were different tiny regex fixtures under parallel cold execution.
Their later passes do not erase those outcomes.

## Outstanding gates

SQL, browser and Test UI verification are still pending at this head. A
fresh whole-PR independent review, review-thread dispositions, combined
validation, exact-`dev` merge verification, and the required proof remain
outstanding. Consequently this report makes no PASS, release, deployment or
completion claim.
