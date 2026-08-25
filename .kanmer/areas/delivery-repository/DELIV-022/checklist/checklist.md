# Release 31 checklist

- [x] Create and take the dedicated release worktree/branch at pinned SHA `7dbb7c39`.
- [x] Revalidate remote ancestry, PR checks, merge trees and deployed release-30 baseline.
- [x] Obtain one clean canonical Release restore/build/test result and focused overlap-test result.
- [x] Pass Local and enabled-estate PreProvision validation.
- [x] Obtain `MERGE AUTH GRANTED`, atomically promote the pinned SHA, and verify both remote refs.
- [x] Build immutable release artifacts; validate and record the manifest SHA-256, digest and migration.
- [x] Confirm exact-target production-write authorization for ACR, azd, SQL, Web and Worker.
- [x] Pass PreUpload; publish the OCI image and verify its digest.
- [x] Pass PreMigration/PreProvision; apply the migration and bootstrap permissions before packages.
- [x] Deploy Web by digest and Worker by config-zip; pass exact release smoke and traffic readback.
- [ ] Complete an operator-run mailbox-image and manual-upload journey. No destructive recovery injection was authorized; telemetry retention requires later observation.
- [x] Update current-state docs, record simplification disposition, verify the diff and open the evidence PR.
- [ ] Complete independent review, merge, verify merged-main/production, write proof and close out.

## Progress notes

- 2026-08-25: pinned clean worktree created at `7dbb7c3952fba74cab2d65a2971ee30b9bc8d273`.
- 2026-08-25: PRs #546–#551 had zero failed/pending checks; all six merge trees equal Git's automatic result.
- 2026-08-25: canonical validation passed: restore; Release build 0 warnings/0 errors; Core 990/990; Architecture 100/100; Integration/Browser 961 passed, 2 expected skips; focused overlap 59/59.
- 2026-08-25: `main` and `dev` atomically fast-forwarded to `7dbb7c39` and read back exactly.
- 2026-08-25: immutable manifest `2187533DC79954D411919E88FE317F50E3602C7A3BDDC673DE0C77123FBA1358`; ACR read-back matched Web digest `sha256:a10dce4337629db261132a978fe4a08811fc94d4173caf7442f47a11b6b8dd35`.
- 2026-08-25: migration head advanced to `20260825145216_MailboxImageIntake`; bootstrap read back 518 catalogued rows and 355 effective runtime DML rows, including Worker INSERT on both submission-group tables.
- 2026-08-25: Web revision `pegasus-prod-web-252ow37gij--7dbb7c3952fb` is healthy/ready at 100% traffic. Worker config-zip succeeded; all nine function settings and registrations remain enabled. Exact-SHA/version production smoke passed.
- 2026-08-25: simplification pass: n/a — evidence-only documentation diff; no product code or new abstraction.
