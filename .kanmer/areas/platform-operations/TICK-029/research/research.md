# Research — OPS-14 rollback procedure (retrospective backfill, VERIFY2 lane, 2026-08-20)

**This is a read-only verification backfill.** Capability OPS-14 was checked against `origin/dev` docs and local release artifacts. Verdict: **PARTIAL — ticket stays at preparing.**

## What exists (verified read-only)

- The release route itself is real and exercised: 13 releases shipped through `docs/runbook.md`'s locked route; release 13 = `2325ed4a` is live.
- Release artifacts are retained **locally** at `artifacts/releases/` (verified on this workstation: `release-13-2325ed4a/` contains `web.zip`, `worker.zip`, `release-manifest.json`, `azd-preview.txt`, `azd-provision.txt`, `migration-transcript.txt`). Also `release-10-d8de29cb/`, `release-12-ed3be51c/`, `0.1.0-alpha.1/`.
- `docs/operations.md` release table records per-release digest + revision (e.g. release 13 row: `sha256:7efa46fd…`, revision `pegasus-prod-web-252ow37gij--2325ed4a31d7`) — but with **truncated** digests and no worker.zip hash.
- `docs/runbook.md` §"Durable Worker activation and rollback" (~line 946) covers only the `PEGASUS_WORKER_ACTIVATION` flag rollback (enabled→disabled), not artifact rollback.
- `docs/runbook.md` §"Production recovery" (~line 1112) is prose obligations only — no commands.

## The gap (why this is not done)

The capability is a **previous-artifact rollback procedure**. No such procedure is written anywhere in the repo:

1. No image-digest rollback steps (no `az`/`azd` commands to repoint the web app at a previous container digest / revision; no ACR tag-history lookup step).
2. No worker config-zip rollback steps (redeploying a prior retained `worker.zip`).
3. No revision/traffic-swap steps.
4. `artifacts/releases/` is **gitignored** (`**/artifacts/`) — retention is workstation-local, single-copy, not a documented durable store; `docs/operations.md` digests are truncated and can't be pasted into a rollback command.
5. The capability row's cited canonical owner (`docs/frd/frd-12-operator-experience.md#operator-experience`) contains **no cutover/rollback content at all** — the ownership pointer is wrong and needs correcting when this is implemented.
6. `docs/operations.md` states no recovery exercise has ever completed.

The capability row's own remark — "implementation/recovery detail remains open" — is accurate.

## What implementation needs (for the eventual plan)

- Write the artifact-rollback section into `docs/runbook.md` (previous digest lookup via ACR, web revision repoint, worker.zip redeploy, migration-compatibility check, smoke).
- Record full (untruncated) digests + worker.zip hash per release, in a durable location.
- Fix the OPS-14 canonical-owner pointer (FRD-12 does not own this; likely runbook/ops-owned with the row updated).
- A rollback exercise (or a reasoned paper walkthrough accepted by the operator) as acceptance evidence.

Premises verified by read-only checks: runbook/FRD/operations content read in full on origin/dev; local artifact listing taken 2026-08-20. Assumed (not verified): ACR still holds prior release image tags.
