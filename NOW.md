# NOW — updated 2026-08-03

(Anything here older than 14 days is stale: delete it, don't investigate it.)

## Doing (one line per live claim)

Claim format: `- <IDs and/or goal> (branch task/<slug>, taken YYYY-MM-DD, by
<agent>)`. Nothing is in flight unless it is claimed here on `origin/dev`.

- Image-led intake: build the pre-Case Image intake record (manual VRM entry,
  manual link/unlink/relink to a Case, origin preservation, search by Image
  Intake Reference — INT-13/27/29/30, UI-07), and research open decision 1
  (VRM recognition engine) to write an evidence-backed recommendation for
  operator decision, unblocking INT-17/28/32; no vendor selection,
  credential, or automatic-matching activation without operator sign-off
  (branch task/image-led-intake, taken 2026-08-03, by claude).
- Cut `repository-check` wall clock (agreed 2026-08-03): shard validate into
  parallel unit / SQL-integration / browser jobs, replace migrate-per-test
  LocalDB setup with a per-run migrated template database, and cache NuGet
  packages and the pinned Playwright Chromium (branch
  task/repository-check-speed, taken 2026-08-03, by claude).
- UI alpha design pass: build the visual/interaction layer for the
  Operations-first `0.1.0-alpha.1` shell against fixture data only — UI-01
  (Operations dashboard), UI-02 (Not ready/Review/Held queues), UI-03
  (Needs sorting/Blocked intake queues), UI-04 (activity counters), UI-05
  (click-through filtered queues), UI-06 (freshness/reconciliation states),
  UI-08 (three-column intake workbench), UI-09 (full case workspace), UI-11
  (accounts/principals/mailbox allowlist/configuration), UI-13
  (accessibility); no Core wiring, no real case/reference mutation, no
  business-rule resolution ahead of open decisions; excludes UI-07 (already
  in task/image-led-intake), UI-10 (`Next / 0.3.0`, out of scope), and UI-12
  (`Not planned`). Widened 2026-08-03 by operator decision to also carry the
  design route for UI-15 (Engineer assessment workbench, `Later / 1.0.0`)
  and its `Send to Claude` action surface (AI-09, `Later / 1.3.0`): design
  and markup only, built unlinked from navigation, satisfying the
  design/README.md rule that a deferred UI capability re-enters
  specification and review before implementation — it activates no route,
  Core field, or transport (branch task/ui-alpha-design-pass, taken
  2026-08-03, by claude).
- Box Case/PO document custody: remove internal `caseId` values from the Box
  folder hierarchy; store retained intake sources and managed document versions
  under the allocated Case/PO-named case folder, reshaping the Core
  content-store contract rather than duplicating policy in infrastructure;
  preserve version identity, replay, hash/length verification, and lifecycle
  gates, with focused local/in-memory custody proof. No Box/Azure read, write,
  credential/configuration change, or migration of existing Box content without
  separately approved target and inventory (DOC-02/03) (branch
  task/box-casepo-document-custody, taken 2026-08-03, by codex).

- Vault consolidation: copy the Box/DVLA/DVSA secrets into the Pegasus Key
  Vault, repoint the Worker's and Web's references, prove resolution, then
  retire the two adopted vaults and `rg-collisionspike-dev` (branch
  task/vault-consolidation, taken 2026-08-03, by codex).
- MCP Automation Actor ingress: build the management/development-controlled
  MCP ingress for one named, vendor-neutral Automation Actor invoking
  existing Core use cases through its own authentication and identity
  (ADR-0011/ADR-0013) — Case actions, intake-queue actions, and document
  actions (MCP-01/02/03/04); reuse the existing ActionActor/ExecuteAsync/
  IActionHistoryWriter pattern rather than the deleted per-staff-OAuth MCP
  surface; no per-staff MCP access, no Administrator/config/credential/
  cloud/release/deletion authority, no AI proposal transport (AI-09 stays
  separate), MCP-05's broader email-workspace actions out of scope pending
  the email workspace itself (branch task/mcp-automation-actor, taken
  2026-08-03, by claude).

## Next (ordered queue — take from the top)

- Assemble the operator-reviewed extraction cohort + untouched holdout and
  accept the per-field thresholds (INT-21, open-decisions) — blocks Path
  step 3.

## Waiting (each line names its unblock condition)

- Obsolete predecessor vault purge — platform-scheduled 2026-08-09, no action
  unless it fails.

## Path (decided 2026-08-02: full QDOS cutover — every new QDOS instruction is worked in Pegasus through to the EVA handoff; EVA keeps engineering and reports. Box custody root decided 2026-08-02: all case folders under the pegasus folder `405543781910` only.)

1. Green `main` through a PR with a passing `repository-check` run.
2. Prove the spine on one genuine QDOS email in production: mailbox intake → custody → extraction draft → principal → Case/PO minted → Box folder (INT-02/08/09/19/22/25, CASE-07, DOC-01/02) — needs the composition fix deployed.
3. Accept extraction thresholds from the reviewed cohort + holdout (INT-21); zero false case creation.
4. Production document content store live (DOC-02), then staff review path live: completeness gates and Review/Not ready/Held queues (CASE-13/14/15/16, UI-02/08).
5. EVA bundle from a real case: exact 13-key JSON + images + SHA-256 manifest (EXT-03), the `First sent to Engineer` proxy event (CASE-21), operator accepts every field mapping via a real drag-and-drop run.
6. Chasing live: due-by, 7-day chase schedule, copyable chasers (CASE-17/18, MAIL-18).
7. Web telemetry exporter (OPS-07) and minimum cutover alerts (Box custody failure, intake poison, chaser sweep), then the cutover date: all new QDOS instructions enter Pegasus; watch alerts and telemetry daily for the first week.
8. Record operator acceptance and management approval (OPS-23, OPS-25) — this is what closes `0.1.0-alpha.1`.

Explicitly NOT on the path (allocated but non-blocking): MCP-01–04, INT-17 VRM reading, INT-31 upload links, the EVAL evaluator cluster, live DVLA/DVSA adapters (approved replay/`Unavailable` is fine), MAIL-14/16 report-sent detection (post-report tracking starts manual via MAIL-15), and OPS-09 recovery proof (removed as a release gate 2026-08-03).

---

Roadmap: [docs/capabilities.md](docs/capabilities.md) · Questions: [docs/open-decisions.md](docs/open-decisions.md) · How-to: [docs/operations.md](docs/operations.md)

Rules: the claimable unit is a task line — goal text first, capability IDs
when they apply, several small features may share one line; one task = one
worktree = one PR. The authoritative copy of this file is the one on
`origin/dev` after a fetch. Claiming, releasing, abandoning, and stale-claim
removal happen through the task workflow owned by
[engineering](docs/engineering.md#task-workflow); claim and maintenance
pushes to `dev` touch only this file and `docs/temp-plans/` deletions.
Staleness ladder, removable by anyone: a claim whose `task/<slug>` branch was
never pushed within 48 hours; a `Doing` line older than 14 days with no
branch activity; an orphaned `docs/temp-plans/` file with no matching `Doing`
line. The date above is bumped whenever this file is touched. No other file
may contain a "current status" or "next steps" section; transient task plans
under `docs/temp-plans/` are the one exception.
