# Checklist

- [x] Take PLAT-067 on the recorded branch and isolated worktree.
- [x] Run the fresh wipe dry run and record Blob, byte, SQL table/row, preserved-row, and sequence figures.
- [x] Obtain exact wipe-write approval.
- [x] Execute the wipe and verify storage, SQL transaction, preserved state, sequences, untouched systems, and authenticated empty Web UI.
- [x] Re-fetch and validate the exact Git candidate, ancestry, direct-commit waiver, PRs, and checks.
- [ ] Inventory live Azure state, migration head, and rollback position, accounting for any external deployment.
- [ ] Obtain fresh literal MERGE AUTH GRANTED and atomically promote the frozen candidate with equality read-back.
- [ ] Build and validate immutable release artifacts in an exact-SHA detached worktree.
- [ ] Confirm the expected unchanged migration identity and absence of a database write.
- [ ] Obtain exact manifest-bound Azure-write approval.
- [ ] Upload the Web image, validate remote digest, provision Web, deploy Worker by config-zip, and read back exact state.
- [ ] Run production smoke and focused non-destructive Inbox, Worker-poll, and QDOS evidence.
- [ ] Retain artifacts and update docs/operations.md and docs/current-architecture.md precisely.
- [ ] Run canonical validation and documentation checks.
- [ ] Record the docs-only simplification disposition, commit, push, and open the evidence PR.
- [ ] Obtain independent review and merge of the evidence PR.
- [ ] Perform the independently authorized final docs-only promotion without redeployment.
- [ ] Verify merged main, write proof, and close out PLAT-067.

## Progress notes

- 2026-09-02 dry run PASS (exit 0): 36 blobs / 3,932,690 bytes; 102 SQL tables; preserve list 31/31 with no missing tables; 32 effectively preserved tables; 70 tables targeted with 147 rows; sequence values Case 31, Image 7, Unidentified 1.
- 2026-09-02 wipe PASS (exit 0): blobs remaining 0; SQL transaction committed 147 row deletions; wiped tables retaining rows 0; preserved rows 354; sequences unchanged at Case 31, Image 7, Unidentified 1; excluded systems untouched. Operator confirmed authenticated UI empty.
- 2026-09-02 Git preflight PASS: main fb3f07acc8cca8d9d8b57db8a431b607772436dc, dev 0f0e90ae44ffda7339ca2a460310deeb98121afa, valid ancestry; PRs 638/640/641/642/643 merged with successful or path-skipped checks.
- 2026-09-02 live preflight FAIL (exit 1): current release-37 Worker activation passed, but newest inbound poll was 1,662 minutes old; Invoke-ProductionSmoke.ps1 reports the recovery timer is not running. Release promotion stopped.
