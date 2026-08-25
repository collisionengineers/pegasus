# Release 31 execution plan

## Governing docs

- `docs/engineering.md`: promote the reviewed `dev` SHA by exact-SHA atomic fast-forward only, with a fresh literal `MERGE AUTH GRANTED`.
- `docs/runbook.md`: use the immutable artifact, manifest, guarded migration/bootstrap, enabled-Worker and smoke procedures against the named production estate.
- `docs/operations.md`: follow the executed release order: immutable validation, ACR upload, migration and permissions before application packages, then smoke and truthful current-state evidence.

## Pinned scope

Release candidate: `7dbb7c3952fba74cab2d65a2971ee30b9bc8d273`.
Deployed baseline: release 30, source `eaabf31130bee9073a1b2e450a24d8fe6d83ce22`.
Product version remains `0.1.0-alpha.1`; release record is 31.
If `origin/dev` changes, stop and repeat the merge/CI/diff/migration audit.

## Steps

1. Create a dedicated release worktree and branch from the pinned `origin/dev`, take this ticket, and preserve release 30 manifest/package identities as rollback evidence.
2. Fetch and recheck main ancestry, PR checks, exact merge trees, clean source, canonical Release restore/build/test, focused overlap tests and Local/PreProvision guards.
3. Immediately before the shared-branch push, obtain `MERGE AUTH GRANTED`; atomically fast-forward `main` and `dev` to the pinned SHA and verify both remote refs.
4. Build immutable artifacts from the exact clean SHA; run Artifact validation and record the manifest SHA-256, migration identity and Web digest.
5. Obtain exact production-write approval for ACR, azd environment, Azure SQL database/bootstrap, Container App and Function App.
6. Run PreUpload, publish the OCI image with ORAS, and verify the ACR digest equals the manifest.
7. Set only the release digest and 12-character revision suffix while retaining `approved-live-worker`; run PreMigration and PreProvision guards.
8. Apply `20260825145216_MailboxImageIntake`, run the manifest-gated runtime-role bootstrap, and read back migration history, schema and permissions before package activation.
9. Provision the digest-pinned Web revision, deploy Worker only by config-zip, then run exact-SHA/version/traffic/nine-function smoke.
10. With separate operator approval for real intake, verify one mailbox-image journey and one manual upload; query normal work-state recovery without destructive failure injection. Check telemetry volume after the reset window and record latency/cost as not proved by this release.
11. Update `docs/current-architecture.md` and `docs/operations.md`, run documentation/diff checks, complete the simplification disposition as n/a for release evidence, and open the evidence PR to `dev`.
12. After independent review and green CI, merge the evidence PR. If promoting the documentation-only merge to `main`, obtain a second fresh `MERGE AUTH GRANTED`. Verify merged-main and production readback, write proof and close the ticket.

## Rollback

Before package activation, stop and leave the additive migration applied. For a Web regression restore release 30's digest/revision; for a Worker regression redeploy release 30's verified Worker ZIP through config-zip. Disabling all nine functions requires separate exact-target approval. Do not down-migrate during incident recovery.

## Proof

Retain command transcripts, manifest and artifact hashes, ACR digest readback, migration/permission census, active revision and traffic, Worker definitions/settings, smoke output and approved behavioral observations. State explicit non-claims for INTK-042 immediate publication, forced queue-loss recovery, full-day telemetry and seven-day cost/latency.
