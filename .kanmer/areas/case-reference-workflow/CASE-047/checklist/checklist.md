# Stream B checklist

- [x] B01 complete full named plan step and its residual acceptance, callers and checks.
- [x] B02 complete full named plan step and its residual acceptance, callers and checks.
- [x] B03 complete full named plan step and its residual acceptance, callers and checks.
- [x] B04 complete full named plan step and its residual acceptance, callers and checks.
- [x] B05 complete full named plan step and its residual acceptance, callers and checks.
- [x] B06 complete full named plan step and its residual acceptance, callers and checks.
- [x] B07 complete full named plan step and its residual acceptance, callers and checks.
- [x] B08 complete full named plan step and its residual acceptance, callers and checks.
- [ ] B09 complete full named plan step and its residual acceptance, callers and checks.
- [ ] Record exact standalone and combined heads, independent findings/dispositions and applicable verification.
- [ ] Integrate reviewed and verified unique work into dev under plan/closeout, preserve superseded histories, and open the dev-to-main PR unmerged with auto-merge disabled.

## Provisional closeout evidence

Common source head `886f94df90fdbfce870f22d71065d12a5fb0fc60` is clean and
published on all three owner branches; frozen B tip `83e875022a1f732af3bbbb2c60f431cd2323bfa6`
is its ancestor. Independent B source review passed through `b49e8c14c`, and
independent delta review found no B regression through the common head. The
fixture-only final delta retains every estimate-total value assertion and
compares anomalies by sequence.

Observed exact-head checks are local Core 1,818 passed / 0 failed / 3 skipped,
Architecture 109 passed / 0 failed, separate pack Core 64 passed / 0 failed /
0 skipped, and two hosted unit results of 1,818 passed / 0 failed / 14 skipped
with Architecture 109 passed. A third hosted unit attempt ended in a transient
framework-regex timeout and awaits rerun. Hosted SQL shard 3 currently records
605 passed / 4 failed / 1 skipped, including a terminal-case exact-replay
fixture authorization failure under diagnosis. The 19-row discovery gap is
addressed by candidate commit
`a3769c1ac3f98cc5300da8bdb4504d7c3995aa35`, which preserves all 17
launch-stage and 4 relay cases as serializable MemberData rows without changing
the strict shard guard; root-owned validation remains pending.

The three unchecked markers remain open deliberately. They require final green
applicable CI, an exact-head formal review and dispositions, integration into
`dev`, merged-SHA proof, and the open unmerged `dev` to `main` PR required by
`plan/closeout`. Corpus/provider evidence explicitly left INCONCLUSIVE by the
approved closeout is not promoted to PASS.
