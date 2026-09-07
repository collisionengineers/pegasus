# Stream A post-implementation report

## Status

This is the provisional implementation handoff for PLAT-075 at common head
`12f45d193919c4cac7c0563c40338e2f33f38c71`. Product/test behavior is frozen at `a3769c1ac3f98cc5300da8bdb4504d7c3995aa35`; the later change
only serializes the existing Core CI command with one xUnit worker and updates
the matching repository convention. All three owner branches are pushed at the
current common head.

Implementation is complete through A08 and the A09 whole-source audit has no
unresolved source or caller finding. Applicable CI, formal exact-head review,
integration into `dev`, merged-head proof and the open `dev` to `main` PR
remain outstanding. This report does not claim final verification,
integration, deployment, release, or Done.

## Scope delivered

The implementation follows the original A01-A09 stream map, shared decisions
and contracts, file map version `471744370c55b553`, plan version
`ea13be5650fe37f9`, and approved closeout version `adfb276e7412137b`.

Delivered scope includes:

- staff identity, authorization, last-administrator protection, sessions and
  credential generations;
- approved Inbox/Sent mailbox state, leases, Graph delta recovery,
  direct-message loading and truthful synchronization status;
- authorized mail preparation/sending, immutable replay, attachment
  revalidation, Unknown/no-resend handling and Sent evidence;
- case and holding custody, verified bytes, durable intents, operation-key
  convergence, request-link authority and reconciliation;
- named-job/MCP paging and durable external-work lifecycle;
- administrator account, mailbox, connector, action-log, health and report
  queries with routed callers; and
- deployment/configuration validation, migration grants, bootstrap checks,
  current-state docs, runbook, UI snapshots and supplied registers.

The 29-row documentation and 44-entry connector registers distinguish source
from deployment/live evidence; deferred mutations are not delivered.

## Source and caller review

Independent A01-A09 review covered Core policy, Infrastructure adapters, Web
and Worker composition, callers, tests and docs. Attachment findings are corrected: Worker resolves holding retention without public-upload limits;
holding Unknown replay uses the same verified bytes and stable operation key;
selections are authorized and revalidated before Graph is touched; and
retention survives replay, lost claims and concurrency.

The fixture correction
`b30e01bdecc9bf78b4397074a6dffa4eb97c5eb1` independently passed source
review against `EfCaseArtifactCustody`. Its worker/null-Case branch checks
`ExecuteSystemWork` and exact length/hash. Request-link authorization, replay
fencing and the public-upload failure controls are unchanged.

The Glass delta at `a3769c1ac` also independently passed. Its 17 launch and
four relay refusal rows now carry only scalar, serializable MemberData values
and reconstruct the existing `Reply` in the test. Routes, responses,
locations, expected failures, states and assertions are preserved. Discovery
reports 21 expanded rows and no unexpanded methods. No product behavior or
shard-coverage rule changed.

No new runtime, store, policy owner, compatibility path or provider operation was added. Original worktrees, branches and recovery refs are preserved.

## Verification observed

Current evidence is:

- Release build at `a3769c1ac`: PASS, 0 warnings, 0 errors, 113.91 seconds;
- affected custody, upload and Glass classes: 131 passed, 0 failed and one
  conditional corpus mapping skip; the photo method reran with the correct
  corpus root as 1 passed and 0 skipped;
- exact local CI command at `12f45d193`: Core 1,818 passed, 0 failed,
  14 conditional skips; Architecture 109 passed, 0 failed;
- PR 674 unit lane: Core 1,818 passed with the same 14 conditional skips and
  Architecture 109 passed; and
- prior focused A attachment, custody, Graph, Worker and documentation evidence
  retained in `scratch/execution.md` and the A source-review records.

The absent pinned genuine PNG is a deliberate per-machine conditional SKIP
under `GenuineFormatCorpusFact`. It is neither PASS nor a waiver. The six
scan-only MP samples remain INCONCLUSIVE release evidence because genuine OCR
results are unavailable; they are not a development-integration failure and
are not claimed as passing.

## Remaining steps

PR 674 is the sole integration PR. PRs 672 and 673 were closed as superseded,
not merged; their branches and history are preserved. PR 674 has no current review threads.

Before PLAT-075 enters Review, the root verifier must finish the pending SQL,
browser and fresh Test UI lanes on the exact head and retain every result. A
fresh independent formal review must bind PR 674, the head, plan and ticket
versions, comments, threads and pushed board tip.

After that pass, the authorized closeout may merge once into `dev`, verify its
SHA, write proof, and open the unmerged `dev` to `main` PR with auto-merge off.
No deployment, provider/mailbox/Box write, mail send, reset, force-push,
deletion or cleanup is authorized.
