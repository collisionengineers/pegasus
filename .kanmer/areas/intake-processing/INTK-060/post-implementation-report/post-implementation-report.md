# Post-implementation report — provisional

## Status and authority

This is a provisional implementation report for INTK-060 at candidate head
`b8d7cd325f7ce76e442706237b8a3424d84accfe`. It records the current
Stream C implementation and evidence only; it is not a review attestation,
proof, stage move, checklist completion, merge authorization or deployment
claim.

The governing packet remains `plan/plan.md`, its closeout record, and
`pegasus_pack/astra_output/v1_implementation_plans/streams/C-intake.md`.
The ticket remains in Implementing while final CI run 34126704865, formal
independent review and integration proof are completed.

## Source and delivery boundary

PR 674 is the sole integration PR against `dev`. PRs 672 and 673 were closed
as superseded, not merged; their branches and histories remain preserved.
Nothing has been merged to `dev`, no `dev` to `main` PR is claimed, and
no cloud, provider, mailbox, Box, Glass's or EVA write occurred.

## Implemented scope and caller evidence

C01-C08 were checked against the current plan, Stream C file ownership and the
existing C source-review records. The reachable production paths retain one
Core policy owner: intake analysis persists source-linked unresolved candidates
and withholds allocation; structured extraction and OCR remain on the existing
external-work path; profile and QDOS policy keep distinct business roles and
fail closed on ambiguity; pre-case Triage, custody and upload paths retain
exact source identity; report extraction and directory administration retain
provenance; and the operator shell, correspondence, notifications, Inbox,
Search and Work Centre call their typed production surfaces.

The focused source and caller audits remain in `scratch/review-c01.md`,
`review-c02.md`, `review-c05.md`, `review-c06.md`,
`review-c07.md`, `review-c07b.md`, `review-c07c.md` and
`review-c08.md`. They are implementation evidence, not the required final
independent exact-head review.

The one-worker Core CI correction at `12f45d193` preserves production regex
grammar, time budgets, filters and assertions. The SQL job correction at
`2506f85d18` only raises the existing allowance from 20 to 30 minutes while
retaining sharding, four-thread concurrency, artifacts and coverage. The
allocation selector correction at `401d` and the generated snapshot at
`b8d7cd325` each received an independent A source PASS. The Glass fixture
changes preserve all inputs and assertions while making every row serializable
and uniquely discoverable.

## Recorded validation evidence

Current retained evidence includes:

- Release build at `401d`: 0 warnings, 0 errors, 94.15 seconds;
- exact local and hosted one-worker Core at `12f45d193`: 1,818 passed,
  0 failed and 14 conditional skips; Architecture: 109 passed, 0 failed;
- hosted browser at `12f45d193`: 139 passed, 0 failed;
- affected custody, public-upload and Glass classes: 131 passed, 0 failed,
  followed by the conditional mapping-photo method at the correct corpus root:
  1 passed, 0 skipped;
- Glass-focused verification at `f5d1f1b12`: 69 passed, 0 skipped; discovery
  and partition verification found 1,848 unique tests with exact one-shard
  coverage;
- exact-replay correction at `565`: 3 passed;
- hosted SQL shard 3: 597 passed and 1 conditional skip, exactly matching all
  598 assigned tests; and
- fresh UI group capture: 10 passed, 0 skipped in 85 seconds; the generated
  `b8d7cd325` snapshot received an independent A PASS; retained plus fresh
  full verification passed 2 tests in 66 seconds; the catalogue reports
  62 routed sources, 69 prototypes and 0 broken references.

Earlier failures remain part of the record. SQL shard 2 previously recorded
607 passed, 1 failed and 1 skipped before the focused three-test replay
correction passed. SQL shard 1 exceeded its 20-minute job limit, produced no
completed TRX and is not a PASS; the 30-minute allowance still requires the
pending CI rerun. Four earlier SQL shard 3 terminal-case fixture failures were
followed by the focused 131-test and mapping-photo passes.

Earlier corpus evidence at `886f94df9` recorded 41 passed, one strict failure
for the separately approved six-MP release gate, and one missing-sample PNG
skip. The operator later supplied the pinned extensionless source object. Its
SHA-256 was verified, and a byte-identical ignored `.png` copy was selected
through `PEGASUS_CORPUS_ROOT`; at built `f5d1f1b12`,
`GenuinePngIsRetainedInNeedsSortingWithoutOcrOrReference` passed 1 test,
0 skipped in 29 seconds. The earlier skip remains historical non-PASS evidence.
The six scan-only MP samples remain INCONCLUSIVE release evidence because
genuine OCR results are unavailable. Mapping checks passed 8, labelled mapping
checks passed 2, DOCX passed 1, and pack Core passed 64.

## Outstanding gates

Final CI run 34126704865 must finish on the exact candidate. A fresh whole-PR
independent review must bind the final head, plan and ticket versions, comments,
threads and dispositions. Combined validation, exact-`dev` merge
verification, and the required post-merge proof remain outstanding.

Consequently this report makes no final PASS, release, deployment or completion
claim.
