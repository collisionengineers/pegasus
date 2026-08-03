# NOW — updated 2026-08-03

(Anything here older than 14 days is stale: delete it, don't investigate it.)

## Doing (one line per live claim)

Claim format: `- <IDs and/or goal> (branch task/<slug>, taken YYYY-MM-DD, by
<agent>)`. Nothing is in flight unless it is claimed here on `origin/dev`.

- Vault consolidation: copy the Box/DVLA/DVSA secrets into the Pegasus Key
  Vault, repoint the Worker's and Web's references, prove resolution, then
  retire the two adopted vaults and `rg-collisionspike-dev` (branch
  task/vault-consolidation, taken 2026-08-03, by codex).
- Report renderer integration planning: plan the retirement of the
  `workspaces/report-renderer/` source import into the monolith — locate the
  Core render port seam and Infrastructure adapter placement that RPT-01–05
  and EXT-08 would activate through, fold the workspace's own documentation
  into the canonical docs, plan the renderer's .NET 8 → repository-TFM
  uplift, plan the `docs/reference/rendererref1` blueprint and report-template
  intake, plan promotion of the renderer's pre-existing MCP server as the
  replacement for the current `.mcpb` packaging (MCP-01–04 follow-ups), and
  plan removal of any remaining renderer desktop/UI elements. Draft planning
  documents under `docs/temp-plans/` only: no activation, no `Pegasus.slnx`
  change, no caller, no workspace deletion, and no acceptance in this task —
  every integration stays behind the workspace register's activation
  conditions and needs its own ADR and implementation task (branch
  task/report-renderer-integration, taken 2026-08-03, by claude).
- AI-09 Send to AI round trip and the Automation Actor assessment toolset:
  implement `docs/temp-plans/mcp-assessment-toolset.md` and
  `docs/temp-plans/send-to-claude-channel-integration.md` under the
  operator's 2026-08-03 direct-write decision — Core assessment model and
  AiWork work-request lifecycle, `automation.assessment` scope and the five
  new Automation Actor tools, `Features:SendToAi` gate/adapter/panel wiring,
  PAV slider, the Automation Actor ADR carrying the AI-09 contract
  rewording, and the `pegasus-claude-channel` 0.2.0 close-out in the sibling
  repo. Everything composition-gated DevelopmentOffline-only; estimate
  derivation stays D2-gated; no activation or tier-5 claim (branch
  task/send-to-ai-round-trip, taken 2026-08-03, by claude).
- Production CSP inline-style hotfix: the deployed CSP (`default-src 'self'`,
  no `style-src`) discards every inline `style` attribute, so the Lucide
  sprite's `display:none` is ignored and a ~1,900px sprite pushes every
  page's content below the fold on the live deploy (found in release-4 live
  checks). Replace all nine static inline styles with site.css classes —
  sprite first. Inline `<script>` blocks are a separate queued decision
  (branch task/csp-inline-style-hotfix, taken 2026-08-04, by claude).
## Next (ordered queue — take from the top)

- Assemble the operator-reviewed extraction cohort + untouched holdout and
  accept the per-field thresholds (INT-21, open-decisions) — blocks Path
  step 3.
- Before the next production deploy, verify the ADR-0020 premise that no
  environment holds an accepted case (CaseMatchIndex ships empty with no
  backfill); if any accepted QDOS case exists, add a one-shot reprojection
  (task/qdos-email-classification review, 2026-08-03).
- Before the production spine proof: a read-only inventory of the approved
  Box custody root confirming no pre-existing `{reference}-{caseId}` case
  folders exist (the Case/PO custody layout fails closed on legacy roots and
  authorises no migration) (task/box-casepo-document-custody review,
  2026-08-03).
- Operator decision: the pre-scrub commits of task/qdos-email-classification
  still carry corpus-derived names/references in GitHub PR refs — decide
  whether to request a history purge; and decide handling of the real staff
  addresses and case data in the operator-supplied
  docs/reference/workproviders-and-repairers files
  (task/qdos-email-classification review, 2026-08-03).
- UI polish follow-ups from the design-pass review: Send-confirm focus drop,
  focus-trap escape edge case, sparkle glyph clipping, and the freshness
  banner's London label falling back to a UTC value without IANA data
  (task/ui-alpha-design-pass review, 2026-08-03).
- Relabel the Operations dashboard's DraftReady intake tile from `Review` to
  the design authority's `Instruction draft` mapping: `DraftReady` is the
  internal intake-receipt decision (a route-accepted instruction whose
  extraction produced a complete reviewable draft, pre-Case), and the
  internal wording leaked into the UI where `Review` is reserved for the
  Case state (operator decision 2026-08-03: ship as-is, fix later).
- Prove the per-run template database's server-side BACKUP/RESTORE against a
  PEGASUS_TEST_SQL_DATASOURCE container (Linux workstation or a CI job) and
  lift the review gate that disables the template for external servers
  (task/repository-check-speed review, 2026-08-03).
- After task/repository-check-speed merges, observe the hardened
  abandoned-database sweep on a shared LocalDB instance: every sweep failure
  is now swallowed, so confirm abandoned Pegasus_Test_* databases and .bak
  files still get reclaimed rather than accumulating
  (task/repository-check-speed review, 2026-08-03).
- Record the MCP Automation Actor tier-5 evidence: a real external client
  (Claude Code over a bearer token) against the locally enabled `/mcp`
  surface, evidence recorded per operations.md, before any activation claim
  (task/mcp-automation-actor review, 2026-08-03).
- Promote the settled Automation Actor identity/authentication/tool-inventory
  contract to an ADR — with the temp plan deleted it is owned only by
  architecture.md/operations.md prose
  (task/mcp-automation-actor review, 2026-08-03).

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
