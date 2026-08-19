# Proof — DELIV-012: release 12 deployed and verified

Written on merged `main` after the deployment, as the ticket requires. The
proof **is** the successful deployment; every line below is a readback from
GitHub, Azure, the production database, or the signed-in production UI —
none is a claim from a build log alone.

## Promotion

- `origin/main == origin/dev == ed3be51c95bc2a055606e5210131d37de9de2dd1`,
  produced by the authorised atomic exact-SHA fast-forward
  (`d8de29cb..ed3be51c`), after **MERGE AUTH GRANTED** for that exact SHA.
- PR #410's lane set: **11/11 SUCCESS** on that head. Main-push run
  `32309456172` concluded **success**, including the
  "Require main history to be contained in dev" guard.
- 21 PR merges since release 10 (12 pre-existing + the 9 this ticket produced
  and integrated: #416, #417, #422–#428), every first-parent commit a PR merge.

## Deployment (all targets exactly as approved)

| Item | Evidence |
|---|---|
| Image | `pegasusprodacr252ow37gij.azurecr.io/pegasus/web@sha256:6dcf3ca134052ebf4f52d5062f1e28944b47615332e555e5146b2ac838626034` — ACR digest readback equals the release manifest digest; the image carries the pinned Chromium layer (ADR-0028) |
| Web revision | `pegasus-prod-web-252ow37gij--ed3be51c95bc`, Active, **100 % traffic, Healthy**, `RunningAtMaxScale`, created `2026-08-19T22:42:44Z`, **1.0 vCPU / 2 GiB** (operator-approved raise) |
| `/diagnostics/version` | `{"version":"0.1.0-alpha.1","sourceSha":"ed3be51c95bc2a055606e5210131d37de9de2dd1"}`; `/health/live` and `/health/ready` 200 |
| Migrations | `efbundle` applied all **8** pending migrations; `__EFMigrationsHistory` head readback `20260819180000_GrantEvaHandoffDownloadOperations` (transcript retained with the release artifacts) |
| Grants | `sys.database_permissions` readback: `CaseRepairSpecifications` → Web SELECT/INSERT/UPDATE + DENY DELETE; `EvaHandoffDownloadOperations` → Web SELECT/INSERT + DENY DELETE, Worker DENY DELETE — **this table had zero permission rows before the release**, a live production defect since 2026-08-11, now closed |
| Bootstrap | `Invoke-AzureDatabaseBootstrap` verified **496 catalogued permission/denial rows and 332 effective runtime DML rows**, manifest-SHA-gated |
| Provision preview | Diffed against release 10's stored preview: byte-identical property changes except `revisionSuffix d8de29cb94f3 → ed3be51c95bc` — the approved sizing change rides inside the same containers entry both previews carry |
| Worker | config-zip deployment `4ac36bca-65ec-42cb-a5ca-80eec955756c`, az_cli, `22:44:24–22:45:32Z`, status success, **active**; the release-10 deployment now inactive |
| Smoke | `Invoke-ProductionSmoke.ps1` **exit 0** — health, exact version and source-SHA match, anonymous `/Cases` → https sign-in 302, nine-function census `approved-live-worker` |
| Manifest | release manifest SHA-256 `863602260A58FA421C9150122B417721B6C03BABE7BCE3D810013DC936AFFAA7`; artifacts copied to the main checkout under `artifacts/releases/release-12-ed3be51c/` at closeout |

## The approved production data change (Q4)

Applied **through the application** — `/Administration/Mailboxes` as a
signed-in administrator, not SQL: mailbox row version 3→4, `AllowSentEvidence`
true, Sent folder identity bound, reason recorded. Verified effect:
`ApprovedSentPollStates.LastCompletedAtUtc` advanced to `2026-08-19T22:52:15Z`
(stuck at 2026-08-07 before) and `LastFailureCode` cleared (was
`sent_mailbox_not_approved`). The once-a-minute
`UnauthorizedAccessException` stream that was consuming the telemetry quota is
over.

## User-visible verification (signed-in production browser session)

- **Dashboard, 1920px**: centred content region beside the rail; new
  **Unidentified** navigation entry; "Unidentified 5" live count.
- **/Upload**: centred redesigned page, whole-area drop target, "Drag files
  here, or choose them", "up to 20 files per submission".
- **/Unidentified**: **U1–U5** allocated by the migration backfill from real
  retained receipts (12–19 Aug), each with the "No usable identification"
  reason and its intake-receipt identity — never-reused references live
  against real data.
- **Case assessment**: the **Report draft** panel present and fail-closed with
  22 enumerated readiness reasons including "Repair cost figures" (estimate
  import is ENG-002; no figures fabricated).
- **/Inbox/{id}** on a real classified e-mail: "Operational destination:
  Receiving work — Destination policy: mail_operational_destination version 1"
  — the formerly dark MAIL-02 policy computing in production.
- **/Administration/Mailboxes**: both route scopes ticked, Sent folder bound.

Screenshots: `release12-dashboard-1920.png`, `release12-upload-1920.png`,
`release12-unidentified-1920.png`, `release12-assessment-reportdraft.png`,
`release12-inbox-destination.png`, `release12-mailboxes-sentevidence.png`
(session scratchpad; referenced from the ticket).

## End state (the original request's checklist)

- **0 open PRs**: #410 closes as the promotion vehicle (its head is on `main`);
  #429 (docs refresh) merges on green and is the last, after which the list is
  empty.
- **Remote branches**: `main`, `dev`, `kanmer-board` (+ the docs-refresh branch
  until #429 merges, then deleted).
- **Local branches / worktrees**: reduced to the same three plus the release
  worktree, removed at closeout after the artifacts are copied out.
- `.worktrees/kanmer` and the board branch untouched throughout, except via the
  Kanmer tools for ticket lifecycle.

## Rollback position (not needed)

Previous digest `sha256:4bd50f66…` and suffix `d8de29cb94f3` re-provision the
release-10 revision; the release-10 worker package redeploys by config-zip; the
eight migrations are additive with no-op backfills verified on production data
beforehand (0 estimate lines, 0 duplicate canonical Message-IDs, 12 receipts).
