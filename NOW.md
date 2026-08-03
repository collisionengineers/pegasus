# NOW — updated 2026-08-03

(Anything here older than 14 days is stale: delete it, don't investigate it.)

## Doing (one line per live claim)

Claim format: `- <IDs and/or goal> (branch task/<slug>, taken YYYY-MM-DD, by
<agent>)`. Nothing is in flight unless it is claimed here on `origin/dev`.

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
## Next (ordered queue — take from the top)

- Assemble the operator-reviewed extraction cohort + untouched holdout and
  accept the per-field thresholds (INT-21, open-decisions) — blocks Path
  step 3.
- Before the next production deploy, verify the ADR-0020 premise that no
  environment holds an accepted case (CaseMatchIndex ships empty with no
  backfill); if any accepted QDOS case exists, add a one-shot reprojection
  (task/qdos-email-classification review, 2026-08-03).
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
- Upgrade the renderer workspace's Scriban 5.12.1 to 7.2.6 with rasterised
  render-parity evidence. Measured 2026-08-03: 5.12.1 carries 14 advisories —
  one Critical (`GHSA-5wr9-m6jw-xx44`, CVSS 9.1, a `TemplateContext` sandbox
  escape from type accessors cached by `Type` alone, patched in 7.0.0) — and
  7.2.6 reports none. Two major versions, so it needs its own parity proof;
  do it before any relocation, while the workspace still has its relaxed
  build settings and its own visual-regression script. This retires the
  `NoWarn NU1901-NU1904` suppression rather than carrying it into `src/`
  (task/report-renderer-integration, 2026-08-03).
- Remove the renderer's WinUI 3 desktop host (22 tracked files) and its 12
  package assets under `design/assets/report-renderer/gui/` — pre-authorised
  by `design/README.md`'s renderer boundary table. Keep `PreviewComposer` and
  its tests (operator decision 2026-08-03: the HTML preview is wanted,
  separated from the GUI), and keep every template, stylesheet, logo and
  signature asset (task/report-renderer-integration, 2026-08-03).
- Uplift the renderer workspace to `net10.0` after the desktop host is
  removed, and repair the Dockerfile: its runtime base
  `mcr.microsoft.com/playwright/dotnet:v1.61.0-jammy` is a tag that does not
  exist — jammy publication stops at v1.59.0 — so the container build is
  already broken. `v1.61.0-noble` is SDK-10-based and is both the fix and the
  uplift enabler (task/report-renderer-integration, 2026-08-03).
- Fix `docs/operations.md`'s Windows-only capability row, which states that
  `scripts/email-eval-desktop` and `CollisionRenderer.Gui` both target
  `net10.0-windows`. Only the first does; the renderer GUI is
  `net8.0-windows10.0.19041.0`
  (task/report-renderer-integration, 2026-08-03).
- Reconcile the report-renderer MCP plan with `task/send-to-ai-round-trip`:
  that task adds an `automation.assessment` scope, five Automation Actor
  tools and the Automation Actor ADR, so the renderer plan's assumed 9-tool
  inventory and its recommendation to write that ADR are both superseded. Any
  later render tool joins the inventory that task leaves behind
  (task/report-renderer-integration, 2026-08-03).
- Capability-inventory questions raised by the renderer planning, each needing
  an operator answer before the work it gates can start: RPT-02 requires a
  four-member outcome enum that no accepted source defines (requirements name
  two Assessment findings); ENG-01's "one canonical repair specification"
  contradicts RPT-03's conservative-and-maximised pair, and resolving it
  changes what ENG-01 must build; four new rows are proposed for the five
  templates that map to no capability, and two templates are proposed for
  retirement; and RPT-03 has no renderer template at all
  (task/report-renderer-integration, 2026-08-03).
- Lifecycle defect found while planning the renderer consumer chain:
  correcting a sent report requires reopening to `ReportPreparation`, which is
  exactly the state in which `UnlinkReportEvidence` becomes permitted, so a
  correction makes final send evidence unlinkable — contradicting the rule
  that the report-sent event stays final. Nothing hits it today because no
  workflow returns a post-report case to report preparation. Sits inside the
  CASE-23 open decision (task/report-renderer-integration, 2026-08-03).

## Waiting (each line names its unblock condition)

- Obsolete predecessor vault purge — platform-scheduled 2026-08-09, no action
  unless it fails.
- Report-renderer relocation into the monolith — blocked on three operator
  decisions recorded in the task's plan set: whether the `.mcpb` stdio host is
  frozen as a built artefact, kept in a reduced workspace, or republished,
  since parity-first and workspace retirement cannot both be executed at once;
  where rendering executes in production, given there is no Web Dockerfile and
  the default `aspnet:10.0` base has neither Chromium nor the Liberation fonts
  and `PublishContainer` cannot install them; and whether unaccepted report
  wording and the three provenance-sensitive signature images may ship in the
  production assembly behind a closed gate. The Core render contract, the
  ADR and the staged route are drafted and waiting
  (task/report-renderer-integration, 2026-08-03).
- Report rendering capabilities RPT-01–05 and EXT-08 — blocked on accepted
  CASE-31, ENG-01 and ENG-02 data, which requirements sequences ahead of them
  and none of which exists, and on the open report-wording decision. With
  `DESIGN_SPEC` superseded by the 2026-08-03 operator decision, the RPT
  specification must now come from those three capabilities plus operator
  answers; the field-level contract the renderer will demand of them is
  drafted (task/report-renderer-integration, 2026-08-03).

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
