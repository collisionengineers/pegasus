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

---

# Appendix — Release 13 (the operator-review remediation release)

The operator reviewed deployed release 12 live and reported six defects (their
verbatim words are in the tickets). All six were fixed, independently reviewed,
merged, and deployed as release 13 the same night, with the same authorisation
discipline: **MERGE AUTH GRANTED** for `2325ed4a31d7dad65a00a7ae5ea0c41ca869bfa5`
and the four Azure writes approved via the question tool.

## Promotion and deployment readbacks

| Item | Evidence |
|---|---|
| Promotion | `ed3be51c..2325ed4a`; readback `main == dev == 2325ed4a…`; PR #435 lane set 11/11 SUCCESS on that exact SHA |
| Image | digest `sha256:7efa46fdc21d6c308a516038cf726b19c922e0c6fc5af0e506496c0c6d0846e3` — ACR readback equals the manifest |
| Migration | one, grant-only: head readback `20260819234014_GrantWorkerIntakeSubmissionGroupRead`; `sys.database_permissions` readback shows `pegasus_worker_runtime_role` SELECT on `IntakeSubmissionGroups` (the gap INTK-011 proved) |
| Bootstrap | **498 catalogued rows / 334 effective DML rows verified** |
| Provision preview | **byte-identical to release 12's stored preview except `revisionSuffix ed3be51c95bc → 2325ed4a31d7`** |
| Web revision | `--2325ed4a31d7` Healthy, 100 % traffic; `/diagnostics/version` → `2325ed4a…` |
| Worker | deployment `5d8da582` active/success `01:16:33–01:17:41Z`; Sent poll readback advancing on the new package (`Last completed 2026-08-20 01:19:45Z` on the admin page) |
| Smoke | `Invoke-ProductionSmoke` **exit 0** |
| Manifest | SHA-256 `E40933DE331BB92765B94E1D8FA25FA5E613E437FAB634C84545100F2D88E628` |

## The operator's six defects, verified fixed in production (signed-in browser)

1. **Mailboxes giant box** — compact one-row policy table, edit panel below,
   no banner, route-scope labels de-jargoned ("New instructions and Triage
   mail (Inbox)"). Screenshot `release13-mailboxes.png`.
2. **UI narration** — swept estate-wide; the Upload page now returns
   `hasReceiptCopy=false, hasIntake=false` from a DOM text probe.
3. **Unidentified slop** — now a **Queues tab** (`Not ready 1 · Review 0 ·
   Held 0 · Triage 0 · Unidentified 6`) with **All/Images/E-mails** filters and
   one-line rows: `U1 | E-mail | (No subject) — from nduncombe@qdosassist.co.uk
   | 12 Aug 2026 15:26 | No usable identification`. No GUIDs, no "intake" —
   and both properties are regression-tested. Screenshot
   `release13-queues-unidentified.png`.
4. **Not-ready origin filters** — Instruction-initiated / Image-initiated,
   live on the tab.
5. **Drag-and-drop** — the whole upload panel is the drop target
   (`panelIsDropTarget=true` verified in the deployed DOM); a stray drop can no
   longer navigate the tab away.
6. **The vanished images** — root cause was a swallowed sequence-contention
   race (INTK-011, 36/36 concurrency trials green). **The stranded production
   JPEG was recovered by the product's own reconciliation as `U6`**
   (`OriginId = 5b4c8cbd…` readback), via the >2h escalation branch — honest
   note: the ticket's ideal was absorption into `G6KDL-01`, but this member
   predated the fix by more than the escalation bound; it is now visible,
   referenced, and staff-resolvable into the case, where before it was
   invisible. Future race victims within the bound are re-driven into their
   group's outcome.

## End state (the original request, finally exact)

`origin`: `main`, `dev`, `kanmer-board`. Local: the same three. Worktrees: the
main checkout and `.worktrees/kanmer` (the release-13 worktree removed at
closeout). Open PRs: 0 after the release-13 docs PR (#436) merges. `main` =
release 13; `dev` = `main` + the docs row, riding the next release by policy.
