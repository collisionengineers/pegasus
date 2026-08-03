# NOW — updated 2026-08-03

(Anything here older than 14 days is stale: delete it, don't investigate it.)

## Doing (one line per live claim)

Claim format: `- <IDs and/or goal> (branch task/<slug>, taken YYYY-MM-DD, by
<agent>)`. Nothing is in flight unless it is claimed here on `origin/dev`.

- QDOS email identification, classification, and case matching: build the
  shared Core classification foundation — the settled Received/Sent families
  and subtypes, Reply as mirrored context, validated `Other` name and reason,
  versioned policy key, decision evidence, explicit ambiguity outcome, and
  the acceptance cohort, keeping category separate from queue, Triage
  routing, and Outlook destination (MAIL-21/22); split the QDOS policy into
  route and extraction parts, activate the operator-accepted three-domain
  QDOS route set (`qdos_mail_route` v3), and add operator-accepted QDOS
  automatic case matching and association — eliminator predicates over claim
  reference, VRM, and claimant name with incident-date elimination, pulling
  the MAIL-09 QDOS-direct subset forward (operator decisions 2026-08-03,
  recorded by ADR in the task PR); still no confidence scores or generic
  rule engine (the multi-rule precedence open decision stays open beyond the
  accepted QDOS predicates), no evaluator surface (EVAL-01–05, MAIL-20,
  OPS-22 are separately owned), no folder move, mailbox mutation, or AI
  classifier (branch task/qdos-email-classification, taken 2026-08-03, by
  claude).
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
## Next (ordered queue — take from the top)

- Assemble the operator-reviewed extraction cohort + untouched holdout and
  accept the per-field thresholds (INT-21, open-decisions) — blocks Path
  step 3.
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
