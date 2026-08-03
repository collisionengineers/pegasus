# NOW — updated 2026-08-03

(Anything here older than 14 days is stale: delete it, don't investigate it.)

## Doing (one line per live claim)

Claim format: `- <IDs and/or goal> (branch task/<slug>, taken YYYY-MM-DD, by
<agent>)`. Nothing is in flight unless it is claimed here on `origin/dev`.

- Delete `design/product/traceability-matrix.md` and repoint its inbound
  links — capabilities.md and design/README.md already own every rule it
  restates (operator-decided 2026-08-03); Markdown-only, so it also proves
  the docs-only CI path (branch task/delete-traceability-matrix, taken
  2026-08-03, by Claude Fable)

## Next (ordered queue — take from the top)

- Compose the production staff surface: register the document/EVA/upload
  services under the Production profile with a Box-backed production document
  content store, lift the `/Intake` 404 for authenticated staff, replace the
  no-op Triage matcher, and add a production-composition activation test —
  blocks Path steps 2–6.
- Ship with the composition-fix release: a forwarded-headers fix (redirects
  currently emit `http://`) and vault consolidation (copy the Box/DVLA/DVSA
  secrets into the Pegasus Key Vault, repoint the Worker's references, prove
  resolution, then retire the two adopted vaults and `rg-collisionspike-dev`).
- Assemble the operator-reviewed extraction cohort + untouched holdout and
  accept the per-field thresholds (INT-21, open-decisions) — blocks Path
  step 3.
- Fix `ProjectReferencesFollowTheModularMonolithDirection` backslash parsing
  and open the PR so the branch gets `repository-check` evidence.

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
