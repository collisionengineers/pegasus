# Release 31 checklist

- [x] Create and take the dedicated release worktree/branch at pinned SHA `7dbb7c39`.
- [x] Revalidate remote ancestry, PR checks, merge trees and deployed release-30 baseline.
- [x] Obtain one clean canonical Release restore/build/test result and focused overlap-test result.
- [x] Pass Local and enabled-estate PreProvision validation.
- [ ] Obtain `MERGE AUTH GRANTED`, atomically promote the pinned SHA, and verify both remote refs.
- [ ] Build immutable release artifacts; validate and record the manifest SHA-256, digest and migration.
- [ ] Obtain exact-target production-write approval for ACR, azd, SQL, Web and Worker.
- [ ] Pass PreUpload; publish the OCI image and verify its digest.
- [ ] Pass PreMigration/PreProvision; apply the migration and bootstrap permissions before packages.
- [ ] Deploy Web by digest and Worker by config-zip; pass exact release smoke and traffic readback.
- [ ] Complete approved mailbox-image/manual-upload checks and non-destructive recovery/telemetry observations.
- [ ] Update current-state docs, record simplification disposition, verify the diff and open the evidence PR.
- [ ] Complete independent review, merge, verify merged-main/production, write proof and close out.

## Progress notes

- 2026-08-25: pinned clean worktree created at `7dbb7c3952fba74cab2d65a2971ee30b9bc8d273`.
- 2026-08-25: PRs #546–#551 have zero failed/pending checks; all six merge trees equal Git's automatic result.
- 2026-08-25: production baseline is healthy release 30, digest `sha256:40a44edb...`, migration head `20260825121453_GrantWorkerImageIntakeLifecycleEvents`, nine Worker functions enabled. Worker currently has SELECT but not INSERT on the two submission-group tables.
- 2026-08-25: azd environment target identities and Key Vault URIs are correct, Worker activation is enabled, but Web digest/suffix are stale release-28 values; they must be replaced only after exact write approval.
- 2026-08-25: Local and enabled-estate PreProvision guards pass.
- 2026-08-25: clean canonical validation passed: restore; Release build 0 warnings/0 errors; Core 990/990; Architecture 100/100; Integration/Browser 961 passed, 2 expected skips. Focused mailbox/recovery/runtime-role overlap passed 59/59.
