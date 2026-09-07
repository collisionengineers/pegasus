# Stream B post-implementation report

## Status

This is a provisional implementation handoff for CASE-047 at common source
head `886f94df90fdbfce870f22d71065d12a5fb0fc60`. The three existing
owner branches are clean on their hosts and their remote tips resolve to this
same head. The frozen B source tip
`83e875022a1f732af3bbbb2c60f431cd2323bfa6` is an ancestor, so the
common head preserves the B history.

Implementation is complete through B08. B09 source review is complete, but its
final exact-head CI evidence and formal Kanmer review attestation remain
outstanding. This report does not claim final verification, integration,
deployment, or Done.

## Scope delivered

The implementation follows the original B01-B09 file map and residual
acceptance in
`pegasus_pack/astra_output/v1_implementation_plans/streams/B-casework.md`,
the authoritative file-ownership register, canonical plan version
`57789d3887881907`, and approved closeout version
`adfb276e7412137b`.

The delivered B scope includes:

- the routed Case workspace and its bounded header, document, history and
  estimate queries;
- named estimate creation, duplication, version selection, comparison,
  discard and explicit current-version behavior;
- vehicle, valuation and sourced engineering evidence;
- Glass's credential administration, launch, resume, callback, durable session
  claim, custody import, replay and expiry behavior;
- immutable report and fee-note generation, signatory selection, prepared
  image evidence and routed report delivery;
- Case file viewing, upload-request metadata and asset preparation;
- VAT, discount and estimate preparation controls; and
- authorization, lease, concurrency, failure and persistence coverage for the
  production callers above.

The complete changed-path ownership is the exact B01-B09 map referenced by
`files/files.md`; shared A/C files at the common head are retained under their
own owner records. The final B-specific correction after the frozen
Accepted/Superseded projection work changes only
`tests/Pegasus.Core.Tests/Assessment/EstimateTests.cs`: it compares every
`EstimateTotals` value member and compares `OffPattern` by sequence, avoiding
record reference equality without changing production behavior or weakening
the assertion.

## Caller and source review

The prior independent B review passed the B01-B09 production sources through
`b49e8c14c97237183f651b86cd75b99f3ebc94a7`, including frozen raw and
printed totals for Accepted and Superseded estimates, confirmed report image
sources, Glass's authorization/session/custody, signatories, report
preparation/delivery, and transactional workspace, valuation and asset paths.

The later delta to the current common head was reviewed independently. Estimate
and report production paths are unchanged from that pass. The only direct
B-adjacent production correction makes generated report attachment version
identities compare as nullable values in `EfStaffMailSendStore`; the remaining
attachment, mailbox and holding-custody work belongs to the shared A/C closeout
and introduces no observed B regression. The exact
`886f94df90fdbfce870f22d71065d12a5fb0fc60` fixture-only delta also
received an independent source PASS with no finding.

Recovery backups and preserved source histories were checked. No original
dirty checkout or reference was removed or rewritten.

## Verification observed

Evidence currently available for exact head
`886f94df90fdbfce870f22d71065d12a5fb0fc60` is:

- local Core: 1,818 passed, 0 failed, 3 skipped;
- local Architecture: 109 passed, 0 failed;
- separate pack Core checks: 64 passed, 0 failed, 0 skipped;
- two hosted unit runs: 1,818 passed, 0 failed, 14 skipped, with Architecture
  109 passed; and
- the latest hosted SQL integration shard 3 result: 605 passed, 4 failed,
  1 skipped. A terminal-case exact-replay fixture authorization failure is
  under diagnosis; the complete four-failure disposition is still pending.

The same SQL shard assigned 591 discovered rows but executed 610. The 19-row
difference comes from two `GlassRepairEstimateGatewayTests` MemberData methods
whose custom Reply argument is not serializable during discovery, while each
method expands its cases at execution. The retained evidence is
`artifacts/ci-34119182388-shard3/assigned-3.txt` and
`artifacts/ci-34119182388-shard3/shard-3.trx`. The bounded fixture remedy is committed and pushed on the platform branch as
`a3769c1ac3f98cc5300da8bdb4504d7c3995aa35`: its 17 launch-stage and 4
relay cases now expose serializable scalar parameters and construct the
existing Reply inside each test. The strict shard coverage guard is unchanged.
This candidate head still requires the root-owned build and CI rerun, followed
by strict fast-forward of the other owner branches.

A third hosted unit attempt ended in a transient framework-regex timeout and is
pending rerun. Previous failures remain recorded; a later pass will not erase
them. Corpus/provider cases without genuine external evidence remain
INCONCLUSIVE release evidence under the approved closeout and are not presented
as PASS.

## Remaining terminal steps

Before CASE-047 may leave Implementing, B09 still requires validation of the
MemberData correction, disposition of every current SQL failure, final green
applicable CI, and an exact-head independent review that binds the current
plan, ticket revision, PR, comments, threads and pushed board tip. Required
checks must be green and every finding must have a terminal disposition.

After review, the authorized closeout still requires one normal integration of
the common head into `dev`, exact merge-SHA verification, the member-owned
post-merge proof, and an open `dev` to `main` PR with auto-merge disabled.
The `main` PR must remain unmerged. No deployment, provider write, mailbox
mutation, mail send, Box write, reset, force-push or cleanup is authorized.
