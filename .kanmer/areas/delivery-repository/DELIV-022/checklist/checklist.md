# Release 31 checklist

- [ ] Create and take the dedicated release worktree/branch at pinned SHA `7dbb7c39`.
- [ ] Revalidate remote ancestry, PR checks, merge trees and deployed release-30 baseline.
- [ ] Obtain one clean canonical Release restore/build/test result and focused overlap-test result.
- [ ] Pass Local and enabled-estate PreProvision validation.
- [ ] Obtain `MERGE AUTH GRANTED`, atomically promote the pinned SHA, and verify both remote refs.
- [ ] Build immutable release artifacts; validate and record the manifest SHA-256, digest and migration.
- [ ] Obtain exact-target production-write approval for ACR, azd, SQL, Web and Worker.
- [ ] Pass PreUpload; publish the OCI image and verify its digest.
- [ ] Pass PreMigration/PreProvision; apply the migration and bootstrap permissions before packages.
- [ ] Deploy Web by digest and Worker by config-zip; pass exact release smoke and traffic readback.
- [ ] Complete approved mailbox-image/manual-upload checks and non-destructive recovery/telemetry observations.
- [ ] Update current-state docs, record simplification disposition, verify the diff and open the evidence PR.
- [ ] Complete independent review, merge, verify merged-main/production, write proof and close out.
