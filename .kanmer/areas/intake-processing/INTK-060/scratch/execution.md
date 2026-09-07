# Execution resume index

The complete prior execution log is preserved verbatim in
`scratch/execution-archive-part-1.md` through
`scratch/execution-archive-part-4.md`. Concatenate their payload sections in
numeric order. Original SHA-256:
`e6b834160b41638637d9ffd1115c7ebc1cc502987578eb36c6711e474485bdc7`.

## Current workspace

- Ticket: `INTK-060`
- Branch: `task/pegasus-v1-intake`
- Worktree: `../pegasus-worktrees/v1-intake`
- Previous controller: `antigravity-stream-c`
- Previous controller run: `antigravity-stream-c-20260907`

## Latest implementation checkpoint

- Owner head recorded pushed: `d4496a838`.
- C01 review PASS recorded at `d57383b2b`.
- C08 review PASS recorded at `729b284e1`.
- C07 public-upload corrections and C02 binary Word reader work remained in
  subsidiary worktrees.
- Combined verification awaited an advanced A-owned verification ref.
- PRs 672, 673 and 674 were recorded open and unmerged; this ticket had no PR.
- No deployment, mailbox/provider write, mail send, reset or force-push occurred.

## Preservation transition

The oversized files, plan and execution documents were losslessly archived in
bounded scratch parts. Their active documents were compacted only to restore
Kanmer execution-packet collection. No product workspace or Git history was
changed by this preservation step.

## Transitions

- Original transitions remain in the archived parts.
- Resume must validate the recorded branch/worktree, renew through Kanmer,
  preserve dirty work, and push only reachable Stream C commits.

- 2026-09-07T05:03:05.248Z claim-transfer antigravity-stream-c → codex-astra-a-c (live; operator: User reassigned full command of Stream C to this A controller; prior agent published clean head 49f05128abf840195cd587f8a14c1d1bb39493fd and preservation handoff. Restored exact recorded relative worktree and branch on this host from that remote head.; lease 9ae60d65-c40a-473e-a5ec-041f1ac5b5a5 → 6af494d4-13a5-4866-a54a-03cf407d3ee1 rev 84; branch task/pegasus-v1-intake; worktree ../pegasus-worktrees/v1-intake; expires 2026-09-07T05:33:05.241Z; evidence: workspace clean (matches-claim), pr open, commits 1, proof absent)

- 2026-09-07T05:06:36.457Z lease-phase implementing → running-command (lease 6af494d4-13a5-4866-a54a-03cf407d3ee1 rev 85; expires 2026-09-07T07:06:36.450Z)

2026-09-07 A controller takeover: user explicitly reassigned C to A while B remains independently owned. Restored exact recorded relative worktree/branch at published49f05128a; transferred lease and obtained ready execution packet. Adopted exact sharedG25 (2838712cc) and exact existing custody-test handoff e4d2409f/c28e8f33 (577f6b0e9); no duplicate combined patch. Preserved helper ddd2da4fe is source-method relocation only, not missing behavior. Independent helper audit found remaining old helpers contained, patch-equivalent or superseded; retain refs. C fixes now published3ea092bf61bfc06c0419de1a0af3408fcc8af54a: exact original operation-key custody reconciliation, real persisted chaser evaluation fixtures, parsed ClaimNumber matcher, reservation/final-slot assertions. WIP49 used incoming arrival identity as custody occurrence; earlier reviewer PASS was explicitly retracted after checking distinct SQL identities. New corrections awaiting independent review and runtime. Combined efb3a3ae3 contains C3ea, A b175f52a6, B87484b2e9; requiredG25 context plus existing A recovery fields retained. Locked restore passed, solution build running. No merge acceptance, dev/main merge, deployment or external provider write.

2026-09-07 05:17 UTC: complete combined8dacad7023a99c80721ff48e70fa81ee2157daf1 locked restore PASS and Release solution build PASS0W0E49.06s. Before that, preserve efb3 six fixture compile errors, 47d85d28 one ReadOnlyMemory.LongLength compile error, and stale MSBuild node32608 file-lock failure10W2E16.73s; shutdown build servers resolved lock, source corrections5752c962d/d90f4458b resolved compile errors. Focused Core55PASS0SKIP160ms at prior same-source Core build (v1-ac-g24-g25-core.trx). Current actualSQL/HTTP run v1-ac-recovered-custody-mail-chaser.trx in progress, no result yet. C3ea source correction PASS and real custody final-slot regression a3bc with d90 compile correction included. New C02 source blocker: no production call to IIntakeOcrOperationStore.BeginAsync; bounded producer fix underway. Existing six scannedMP strict corpus failures preserved separately as genuine-output acceptance pending approved OCR activation, per explicit C02 plan. No dev/main merge.

Checkpoint 2026-09-07, all streams now owned on this host. Combined e800b8789a980d549ab3739cd2ccbaa057139cac Release build PASS 0 warnings/errors 44.74s. Focused B/C v1-bc-final-recovery.trx: exit1, 89 passed/7 failed/0 skipped, 2m26. Failures preserved: three authority-fixture expectedVersion lookups; expired resume and callback incorrectly acquire CallbackConsumedAtUtc (real store provenance defect under correction); provider-refusal rendered text assertion; genuine QDOS shared-VRM formal source has no classification attachment, so allocation is withheld despite extracted draft. No assertions waived. Report HTTP five cases now pass after bc45 Engineer fixture. Earlier e800 predecessor 934b build failed two missing Workflow exception usings, fixed f989. Earlier combined8d9 Core1823 passed/3 missing-pack skips; Architecture109 passed0skip. C round3 d395 had54 passed/1 failed0skip, all selected custody/chaser/OCR checks passed. Final UI capture, full exact-head rails, review, dev merge/proof and main PR remain outstanding. No deployment/live provider activity.

## Genuine PNG evidence recovered 2026-09-07

Operator identified the formerly missing pinned PNG now present in the source checkout at artifacts/intake/sha256/01/01039929CBDDFB193D88B155AEE12C9EF144081CA5EFD0BF22371B2813D9B7DA. Root verified SHA256 equals that exact pinned hash. The source has no extension, whereas GenuineMultiFormatCorpus enumerates .png files. Original left unchanged; a byte-identical local copy under v1-platform/artifacts/pinned-png-corpus with .png extension was used via PEGASUS_CORPUS_ROOT.

At built head f5d1f1b126fd729158afb913c7ebb7f78f9486e5, dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~GenuinePngIsRetainedInNeedsSortingWithoutOcrOrReference --logger trx;LogFilePrefix=v1-closeout-genuine-png-f5d1f1b -- xUnit.MaxParallelThreads=1 exited0: 1PASS,0SKIP,29s. TRX v1-closeout-genuine-png-f5d1f1b_net10.0_20260907141317.trx. Prior missing-file conditionalSKIP retained as history; this resolves the PNG evidence gap without waiver or fixture changes. Six scan-only MP INCONCLUSIVE release gates remain separate.
