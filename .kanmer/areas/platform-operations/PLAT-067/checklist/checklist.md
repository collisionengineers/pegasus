# Checklist

- [x] Take PLAT-067 on the recorded branch and isolated worktree.
- [x] Run the fresh wipe dry run and record Blob, byte, SQL table/row, preserved-row, and sequence figures.
- [x] Obtain exact wipe-write approval.
- [x] Execute the wipe and verify storage, SQL transaction, preserved state, sequences, untouched systems, and authenticated empty Web UI.
- [x] Re-fetch and validate the exact Git candidate, ancestry, direct-commit waiver, PRs, and checks.
- [x] Inventory live Azure state, migration head, and rollback position, accounting for any external deployment.
- [x] Obtain fresh literal MERGE AUTH GRANTED and atomically promote the frozen candidate with equality read-back.
- [x] Build and validate immutable release artifacts in an exact-SHA detached worktree.
- [x] Confirm the expected unchanged migration identity and absence of a database write.
- [x] Obtain exact manifest-bound Azure-write approval.
- [x] Upload the Web image, validate remote digest, provision Web, deploy Worker by config-zip, and read back exact state.
- [x] Run production smoke and focused non-destructive Inbox, Worker-poll, and QDOS evidence.
- [x] Retain artifacts and update docs/operations.md and docs/current-architecture.md precisely.
- [x] Record the operator waiver of documentation-branch testing.
- [x] Record the docs-only simplification disposition, commit, push, and open the evidence PR.
- [x] Merge evidence PR #645 under the operator's explicit no-review waiver.
- [x] Perform the independently authorized final docs-only promotion without redeployment.
- [x] Verify merged main, write proof, and close out PLAT-067.

## Progress notes

- 2026-09-02 wipe PASS: dry run found 36 blobs / 3,932,690 bytes and 147 rows across 70 non-preserved tables. Execution left zero target blobs and rows, retained 354 preserved rows, and preserved sequences 31/7/1. Excluded systems were untouched; the operator confirmed the authenticated UI was clear.
- 2026-09-02 release PASS: exact source `0f0e90ae44ffda7339ca2a460310deeb98121afa`; manifest `52E1A5AC23C2491594E79EA89740D9B5D826A3DD94258347DB91A16896F986AE`; Web digest `sha256:b791d9587224d30d68fd6abcbd1e1d5f389f2baefc3702d9ec2d2f37398eef15`; Worker deployment `01ed553a-b6cd-4652-b043-72c88b9ca2e6`. No migration or database write; head unchanged.
- 2026-09-02 production verification PASS: canonical smoke passed; the 12:50 UTC Graph poll completed, cleared `LastFailureCode`, advanced the cursor, and released blocked emails. Authenticated Inbox preview persistence passed.
- 2026-09-02 evidence closeout PASS: immutable artifacts retained; current-state docs merged through PR #645. Operator explicitly waived review and testing for the documentation-only change. Simplification: n/a — current-state documentation and release evidence only.
- 2026-09-02 docs-only promotion PASS: fresh `MERGE AUTH GRANTED` applied to `1b705bd01d88109b21affddd014fbaa06c82b1ce`; atomic push and fresh read-back showed both `origin/main` and `origin/dev` at that SHA. No rebuild or redeployment.
