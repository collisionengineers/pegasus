# Stream A post-implementation report

## Status

This is a provisional implementation handoff for PLAT-075 at candidate head
`b8d7cd325f7ce76e442706237b8a3424d84accfe`. Implementation is
complete through A08, and the A09 source and caller audit has no unresolved
finding. Final CI run 34126704865, the formal exact-head review, integration
into `dev`, merged-head proof, and the open `dev` to `main` PR remain
outstanding.

This report does not claim final verification, integration, deployment,
release, or Done.

## Scope delivered

The implementation follows the original A01-A09 stream map, shared decisions
and contracts, file map version `471744370c55b553`, plan version
`ea13be5650fe37f9`, and approved closeout version
`adfb276e7412137b`.

Delivered scope includes:

- staff identity, authorization, last-administrator protection, sessions and
  credential generations;
- approved Inbox/Sent mailbox state, leases, Graph delta recovery,
  direct-message loading and truthful synchronization status;
- authorized mail preparation and sending, immutable replay, attachment
  revalidation, Unknown/no-resend handling and Sent evidence;
- case and holding custody, verified bytes, durable intents, operation-key
  convergence, request-link authority and reconciliation;
- named-job/MCP paging and durable external-work lifecycle;
- administrator account, mailbox, connector, action-log, health and report
  queries with routed callers; and
- deployment/configuration validation, migration grants, bootstrap checks,
  current-state docs, runbook, UI snapshots and supplied registers.

The 29-row documentation and 44-entry connector registers distinguish source
from deployment and live evidence; deferred mutations are not delivered.

## Source and caller review

Independent A01-A09 review covered Core policy, Infrastructure adapters, Web
and Worker composition, callers, tests and docs. Attachment findings are
corrected: Worker resolves holding retention without public-upload limits;
holding Unknown replay uses the same verified bytes and stable operation key;
selections are authorized and revalidated before Graph is touched; and
retention survives replay, lost claims and concurrency.

The custody fixture correction retains `ExecuteSystemWork`, exact length and
hash checks, replay fencing, request-link authorization and public-upload
failure controls. The later Glass fixtures retain all 17 launch and four relay
inputs and assertions while exposing serializable data and unique scenario
labels for discovery. The strict shard-partition guard is unchanged.

The one-worker Core CI correction at `12f45d193` preserves production regex
grammar, time budgets and assertions. The SQL job change at `2506f85d18`
only raises the existing job allowance from 20 to 30 minutes; an independent
B review found no command, filter, sharding, four-thread cap, artifact,
coverage or assertion change. The allocation selector correction at
`401d` and the generated snapshot at `b8d7cd325` each received an
independent A source PASS.

No new runtime, store, policy owner, compatibility path or provider operation
was added. Original worktrees, branches and recovery refs remain preserved.

## Verification observed

Current retained evidence includes:

- Release build at `401d`: 0 warnings, 0 errors, 94.15 seconds;
- exact local and hosted one-worker Core at `12f45d193`: 1,818 passed,
  0 failed and 14 conditional skips; Architecture: 109 passed, 0 failed;
- hosted browser at `12f45d193`: 139 passed, 0 failed;
- affected custody, public-upload and Glass classes: 131 passed, 0 failed,
  followed by the conditional mapping-photo method at the correct corpus root:
  1 passed, 0 skipped;
- Glass-focused verification at `f5d1f1b12`: 69 passed, 0 skipped; discovery
  and partition verification found 1,848 unique tests, including all refusal
  rows, with exact one-shard coverage;
- exact-replay correction at `565`: 3 passed;
- hosted SQL shard 3: 597 passed and 1 conditional skip, exactly matching all
  598 assigned tests; and
- fresh UI group capture: 10 passed, 0 skipped in 85 seconds; the generated
  `b8d7cd325` snapshot received an independent A PASS; retained plus fresh
  full verification passed 2 tests in 66 seconds; the catalogue reports
  62 routed sources, 69 prototypes and 0 broken references.

Earlier failures remain evidence. SQL shard 2 previously recorded 607 passed,
1 failed and 1 skipped before the focused three-test replay correction passed.
SQL shard 1 exceeded its 20-minute job limit, produced no completed TRX and is
not a PASS; the bounded 30-minute allowance still requires the pending CI rerun.
The earlier four SQL shard 3 terminal-case fixture failures were followed by
the focused 131-test and mapping-photo passes above.

The earlier full-root corpus run recorded 41 passed, one strict failure for the
six operator-dispositioned scan-only MP cases, and one missing-sample PNG skip.
The operator then supplied the pinned extensionless source object. Its SHA-256
was verified, and a byte-identical ignored `.png` copy was selected through
`PEGASUS_CORPUS_ROOT`; at built `f5d1f1b12`,
`GenuinePngIsRetainedInNeedsSortingWithoutOcrOrReference` passed 1 test,
0 skipped in 29 seconds. The earlier skip remains historical non-PASS evidence.
The six MP samples remain INCONCLUSIVE release evidence because genuine OCR
results are unavailable.

## Remaining steps

PR 674 is the sole integration PR. PRs 672 and 673 were closed as superseded,
not merged; their branches and history remain preserved.

Final CI run 34126704865 must complete on the exact candidate and retain every
result. A fresh independent formal review must then bind PR 674, the final
head, plan and ticket versions, comments, threads and pushed board tip.

After that pass, the authorized closeout may merge once into `dev`, verify its
SHA, write proof, and open the unmerged `dev` to `main` PR with auto-merge
off. No deployment, provider/mailbox/Box write, mail send, reset, force-push,
deletion or cleanup is authorized.
