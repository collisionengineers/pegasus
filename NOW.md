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
- QDOS email identification and classification: build the shared Core
  classification foundation — the settled Received/Sent families and
  subtypes, Reply as mirrored context, validated `Other` name and reason,
  versioned policy key, decision evidence, explicit ambiguity outcome, and
  the acceptance cohort, keeping category separate from queue, Triage
  routing, and Outlook destination (MAIL-21/22); no rule-precedence or
  confidence-threshold invention (open decision: mailbox rule activation),
  no evaluator surface (EVAL-01–05, MAIL-20, OPS-22 are separately owned),
  no folder move, mailbox mutation, or AI classifier (branch
  task/qdos-email-classification, taken 2026-08-03, by claude).
- Cut `repository-check` wall clock (agreed 2026-08-03): shard validate into
  parallel unit / SQL-integration / browser jobs, replace migrate-per-test
  LocalDB setup with a per-run migrated template database, and cache NuGet
  packages and the pinned Playwright Chromium (branch
  task/repository-check-speed, taken 2026-08-03, by claude).

## Next (ordered queue — take from the top)

- Ship with the composition-fix release: the Web identity's Key Vault Secrets
  User grant for the two Box secrets the Web container app now references,
  and vault consolidation (copy the Box/DVLA/DVSA secrets into the Pegasus
  Key Vault, repoint the Worker's references, prove resolution, then retire
  the two adopted vaults and `rg-collisionspike-dev`).
- Assemble the operator-reviewed extraction cohort + untouched holdout and
  accept the per-field thresholds (INT-21, open-decisions) — blocks Path
  step 3.
- Accept the Box managed-document layout from operator review
  (open-decisions) before the document surface carries real case work.

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
