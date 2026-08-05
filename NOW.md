# NOW — updated 2026-08-05

(Anything here older than 14 days is stale: delete it, don't investigate it.)

## Doing (one line per live claim)

Claim format: `- <IDs and/or goal> (branch task/<slug>, taken YYYY-MM-DD, by
<agent>)`. Nothing is in flight unless it is claimed here on `origin/dev`.

- UI implementation programme, Inbox part 1 — remove the manual
  case-acceptance gate and the `DraftReady` decision, and implement
  INT-25/CAP-008 (the queued task below, taken as the behavioural half of the
  Inbox page work because the queue it feeds is that screen). Definitive
  authorised intake allocates at processing time, entering `Not ready` when
  ordinary detail is thin; ambiguity stays `Needs sorting`; fail-closed
  conditions stay `Blocked intake`. Includes the receipt count/list acceptance
  filters that make every intake count cumulative for all time today (branch
  task/intake-allocates-without-a-gate, taken 2026-08-05, by claude).
- CASE-27 edit-lease continuity and conflict recovery for both callers
  (MCP-02/MCP-04): close the gaps between
  `docs/requirements.md` "Case edit authority and recovery" and shipped
  behaviour — expired leases must read as free everywhere they are projected
  (Triage and Operations still narrate a past expiry as held), authorised
  non-holders must see the holder and when edit authority frees, a rejected
  editor must keep their proposed values for comparison instead of losing the
  post to a bare redirect, the Automation Actor must be able to renew rather
  than only begin/end, and the triplicated mutation guard collapses to one
  Core-owned implementation with a single lease-token length contract. Staff
  Web and the Automation Actor exercise the same guard and the same
  reacquisition path; no takeover, no force-save, no Administrator bypass, no
  lease vocabulary in operator copy (branch task/case-edit-lease-continuity,
  taken 2026-08-05, by claude).

## Merged, not deployed

- **Report renderer workspace uplift** closes both the
  `report-renderer-integration` planning claim (PR 331, closed as superseded —
  its plan set lands here unmodified) and the `report-renderer-workspace-uplift`
  claim. Inside `workspaces/report-renderer/` only: the WinUI 3 desktop host and
  its 12 `design/assets/report-renderer/gui/` package assets are gone; Scriban is
  7.2.6 and the `NU1901`–`NU1904` suppression is retired, taking the workspace
  from 14 advisories (one Critical, `GHSA-5wr9-m6jw-xx44`, CVSS 9.1) to none
  under `net10.0`'s full transitive audit; the six remaining projects are
  `net10.0`; the Dockerfile's non-existent `v1.61.0-jammy` base tag is corrected
  to `v1.61.0-noble`; and `Format.Today` takes a `TimeProvider` converted to
  Europe/London instead of reading machine-local `DateTime.Now`. Workspace ADRs
  0012–0014 record the three decisions. Evidence is **tier 1 only** — 236 tests
  passing (216 before), clean build, composed-HTML parity across 12 template
  identifiers × 3 densities. **Nothing is deployed and no capability advanced.**
  There is still no Pegasus caller, no `Pegasus.slnx` entry and no Core render
  port. The container was **not** built (no Docker on the workstation) and the
  `.mcpb` bundle was not launched under .NET 10.

- **AI-09 Send to AI round trip and the Automation Actor assessment toolset
  (MCP-06)** merged into `dev` 2026-08-05 (PR 332, merge `5555440`), all nine
  checks green. **It is not deployed.** Nothing is in `main`, no environment
  runs it, and no activation, navigation-link, or acceptance claim is made.
  The whole surface is composition-gated off by default (`Features:SendToAi`,
  `Features:AutomationMcp`) and DevelopmentOffline-only; production would
  additionally need a non-preview transport decision. Evidence is tier 2–4
  local only — the tier-5 external-client round trip is still queued below.

## Next (ordered queue — take from the top)

- Send to AI work-request integrity (PR 332 review): the reasoned `Cancelled`
  outcome the plan requires has no caller at all — `ICancelAiWorkRequest` is
  registered and unit-tested but nothing in the application invokes it, so
  add the reason-taking action and the visible cancelled state. With it, the
  outcomes the transport cannot currently tell apart: a lost or malformed
  `/send` response is recorded as a definite `Failed` ("Nothing was sent")
  and retry mints a fresh request id, so one case can be forwarded twice;
  a `/events` transport failure is indistinguishable from an empty reply and
  the panel states "No reply has been recorded yet" — both need a typed
  uncertain/unavailable outcome on the Core port rather than a false
  business-facing claim. Also: replay is not attempted before the in-flight
  guard rejects; the in-flight check and the insert are not one transaction,
  so two sends with different operation keys can both reach `/send`; a
  timed-out request is stepped over rather than transitioned to `Expired`,
  leaving it permanently non-terminal; and reconcile expires on wall-clock
  without first reading whether the reply arrived in time.
- Record the Send to AI eligibility decision (PR 332 review): the shipped
  gate admits every `NotReady`/`Review`/`ReportPreparation` case with no
  readiness condition, which silently resolves the channels plan's open
  decision 5 — which outstanding requirements block the panel — in the most
  permissive direction. The rewritten `open-decisions.md` section no longer
  carries the question. The permissive default may well be right; it needs
  recording as a decision rather than as an implementation detail.
- Automation round-trip evidence view (PR 332 review): filtering
  `/Administration/Automation/Activity` by a work-request id does not show
  the round trip the plan promised. The lifecycle rows (created, handed off,
  completed, failed) carry the sending staff actor while the activity
  projection filters strictly on `ActorKind.Automation`, so only the later
  ingress rows appear; and the `case_assessment_saved` row that holds the
  per-field before/after evidence is correlated by operation key even when a
  request id is bound. Extend the projection to the `ai_work_request`
  aggregate and correlate the detailed row by the bound request id.
- Assessment toolset correctness follow-ups (PR 332 review): replay returns a
  fresh current projection with no `IsReplay` marker, so a retry after an
  intervening save reports the later fields — the toolset plan specifies the
  original result plus a replay signal. `MapCaseOwned` falls back to
  `CaseDataCodes.Suggestion`, so an unconfirmed extraction can be served by
  `pegasus_assessment_get` as accepted case data and satisfy readiness.
  `pegasus_case_update_details` and `pegasus_eva_bundle_generate` accept any
  well-formed `workRequestId` as an audit correlation without the existence
  and case-ownership check `pegasus_assessment_update` applies. A partial
  `pegasus_case_update_details` edit re-stamps every omitted field's
  confirmation metadata with the automation actor, because the merge reads
  the current confirmed values and writes the whole record back. Required-when
  pairings are validated only when the governing field is in the save, so
  clearing the dependent value alone persists an invalid merged state.
  Estimate prices and work units accept decimals wider than the mapped
  `decimal(18,2)`/`decimal(9,1)`, reaching a database overflow instead of a
  deterministic refusal. Estimate-line results expose only actor kind and a
  confirmation boolean, dropping the `RecordedBy`/`ConfirmedBy` provenance
  the record retains and scalar fields return.
- **UI implementation programme** (operator decision 2026-08-04, settling the
  earlier "decide what `docs/ui-work/` is for" question): the folder is
  adopted as the specification for a whole-application UI rebuild, then
  deleted. Every page folder's alteration plan is implemented, every entry in
  `docs/ui-work/additions-hidden-features.md` and
  `docs/ui-work/defects-and-non-functional.md` is made visible and functional,
  `docs/ui-work/ui-standards-and-review.md` becomes the enforced presentation
  contract, and the implemented pages match the **refreshed** mockups. The
  work lands as one PR per main-navigation page — sub-pages fold into the
  navigation page that owns them — reviewed and merged into `dev` in
  sequence, then `dev` into `main`. Queue, each claimable on its own line:
  - Shell and design system (pages 14–18, `_Layout`, `site.css`, operator
    label maps, styled status-code pages).
  - Dashboard (pages 1, 7) — needs the Core case-lifecycle and day/week
    count queries that do not exist today (defects B3, M1, M7, M8).
  - Inbox (pages 2, 6, 8, 9, 10) — carries the acceptance-gate/`DraftReady`
    removal and INT-25 below, because the queue it feeds is this screen.
  - Upload (pages 2-split, 13) — carries defect B1, the dead upload handler.
  - Queues (pages 3, 11).
  - Cases (pages 4/5, 12) — the case container, Evidence tabs, provenance
    icons, and the `Review`-only export precondition.
  - Administration (pages 5-administration, 19–31).
  Until a page's PR merges, nothing in its folder is accepted.
- Protect `main` against direct pushes. `440ab5c` reached the deployment
  branch without a PR, so CI never gated it — it carried a defect that
  failed the documentation check the moment it was put in front of CI (fixed
  in PR 335). PR 324 was likewise opened against `main` before being
  redirected. A branch-protection rule stops this being caught by audit.

- Record releases 4 and 5 in operations.md deployed evidence: release 4
  (2026-08-04, revision `8e34078…`, digest `sha256:ae2cc7b8…`, the four
  2026-08-03 migrations applied with the runtime-role matrix re-verified,
  ADR-0020 premise verified — zero accepted cases, CaseMatchIndex shipped
  empty) surfaced the production-CSP blank-band defect; release 5
  (2026-08-04, revision `c6571f7…`, digest `sha256:29d4fcff…`, no new
  migrations) shipped the PR 333 hotfix — live-verified: all 21
  authenticated routes render from the viewport top with zero inline
  styles, zero console errors, zero exceptions/sev3+ traces, smoke passed.
  Also record the 2026-08-04 read-only Box custody-root inventory: the
  pegasus folder `405543781910` has zero children, so no legacy
  `{reference}-{caseId}` folders exist and the Case/PO fail-closed gate is
  satisfied (release-5 live checks, 2026-08-04).
- Assemble the operator-reviewed extraction cohort + untouched holdout and
  accept the per-field thresholds (INT-21, open-decisions) — blocks Path
  step 3.
- Investigate the one failed staged intake artifact
  (`staging/f7d2bdb8…`, 10,378,983 bytes, first seen 2026-08-01 23:56Z):
  it remained `Failed` after the 2026-08-04 Worker re-enable and two
  reconciliation sweeps — decide staff-review disposition. Context: the
  operator ordered all nine Worker functions re-enabled on 2026-08-04
  (they had carried `AzureWebJobs.<name>.Disabled = true` since
  2026-08-03 ~01:08, set under the shared identity during the live
  vault-consolidation window; releases 4 and 5 preserved the flags).
  Re-enable verified live: six timer/dispatch functions executed with
  zero failures and zero exceptions, the Inbox poll succeeded at
  2026-08-04 08:41:45Z, and the one waiting inbox email processed into
  `Needs sorting`; the three queue/poison functions correctly idle with
  no messages. Alert rules and the operations action group were checked
  enabled; nothing else in `rg-pegasus-prod` was disabled
  (worker re-enable live checks, 2026-08-04).
- Decide the production-CSP strategy for the inline `<script>` blocks
  (`_FreshnessBanner` refresh feedback, `_ReasonDialog` focus trap, the
  unrouted Assessment artifacts): external script files versus CSP hashes.
  They are silently discarded by the deployed `default-src 'self'` policy
  today — progressive enhancement only, nothing functional breaks
  (release-4 live checks, 2026-08-04).
- Operator decision: the pre-scrub commits of task/qdos-email-classification
  still carry corpus-derived names/references in GitHub PR refs — decide
  whether to request a history purge; and decide handling of the real staff
  addresses and case data in the operator-supplied
  docs/reference/workproviders-and-repairers files
  (task/qdos-email-classification review, 2026-08-03).
- UI polish follow-ups from the design-pass review: Send-confirm focus drop,
  focus-trap escape edge case, sparkle glyph clipping, and the freshness
  banner's London label falling back to a UTC value without IANA data
  (task/ui-alpha-design-pass review, 2026-08-03); also the Access review
  page renders the `0001-01-01 00:00:00Z` sentinel as "Last reviewed"
  beside a `Recorded` state (release-5 live checks, 2026-08-04).
- Remove the manual case-acceptance gate and the `DraftReady` decision, and
  implement INT-25/CAP-008. `docs/requirements.md:251` requires that
  "definitive authorised intake creates exactly one instructed Case
  idempotently" and that "the allocation decision adds no universal manual
  acceptance gate"; `docs/operator-notes.md:204` sends only ambiguous
  provider, instruction-type, or case evidence — and any unidentified e-mail
  — to `Needs sorting`. Shipped behaviour is the opposite: `IAcceptIntake`
  has one caller, the staff form handler at
  `src/Pegasus.Web/Pages/Intake/Details.cshtml.cs:534`, so every case in
  Pegasus waits on a human pressing "Accept and allocate case reference",
  and `IntakeDecision.DraftReady` exists only to name that wait. Definitive
  intake must allocate at processing time, entering `Not ready` when
  ordinary detail is thin; incomplete detail is not a bar to allocation.
  `Needs sorting` keeps the ambiguity path, including staff-resolved manual
  creation (INT-26); genuine fail-closed conditions (limits, principal
  identity, standalone Audit evidence) stay `Blocked intake` with a reason.
  Scope: Core policy and the acceptance/receipt stores, the Worker/automation
  path, the intake and dashboard surfaces, and a repo-wide correction pass —
  `DraftReady` is referenced across `src/`, `tests/`, `design/README.md:374`,
  `docs/capabilities.md`, and the intake filter/route token `draft_ready`.
  Also correct `EfIntakeReceiptStore.GetCountsAsync:152-164` and
  `ListAsync:166-192`, which never exclude receipts that produced a case, so
  every intake count is cumulative for all time. `Review` and "Ready to
  review" are the Case stage before the report is with an Engineer
  (`CaseWorkflowContracts.cs:15`, `requirements.md:295`) and must never
  label an intake state (operator decision 2026-08-04, superseding the
  2026-08-03 "ship as-is, relabel later" line — the tile was never a
  labelling defect).
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

- Reconcile the report-renderer MCP plan with the merged AI-09 work: PR 332
  landed an `automation.assessment` scope, five Automation Actor tools and the
  Automation Actor ADR, so the renderer plan's assumed 9-tool inventory and its
  recommendation to write that ADR are both superseded. Any later render tool
  joins the inventory PR 332 left behind
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
- Renderer container and `.mcpb` proofs the uplift could not run: build
  `workspaces/report-renderer/Dockerfile` on a Docker-capable host to prove the
  corrected `v1.61.0-noble` base actually publishes and runs a `net10.0` render,
  and build and launch the `.mcpb` bundle once under .NET 10 to prove the
  single-file Playwright driver still resolves. Both are configuration fixes
  today, not proven builds. Moving jammy → noble also shifts Ubuntu 22.04 → 24.04
  with its font and ICU versions, and there is no valid "before" image because
  the `v1.61.0-jammy` tag never existed — so the first noble render is a new
  baseline, not a comparison
  (task/report-renderer-workspace-uplift, 2026-08-05).
- Renderer analyzer strictness and lock files, both deferred by the uplift and
  recorded in workspace ADR-0014: the workspace still sets its own relaxed build
  properties and inherits nothing from the repository root, so
  `TreatWarningsAsErrors` is off and an estimated low-hundreds of diagnostics
  wait behind relocation; and the workspace has no `packages.lock.json` files, so
  its CI lane correctly cannot use `--locked-mode`. Decide both before, not
  during, any move into `src/`
  (task/report-renderer-workspace-uplift, 2026-08-05).
- Renderer density reaches one template only: `HtmlComposer` passes the density
  through to `market-valuation-evidence` and not to the evidence pack, fee note
  or expert report, so eleven of the twelve identifiers compose byte-identically
  at Normal, Compact and UltraCompact. Observed while proving render parity;
  pre-existing, not introduced. Decide whether auto-fit is meant to apply to
  those bodies before density enters any issued-artifact contract
  (task/report-renderer-workspace-uplift, 2026-08-05).

## Waiting (each line names its unblock condition)

- Report-renderer relocation into the monolith — blocked on three operator
  decisions recorded in the plan set: whether the `.mcpb` stdio host is frozen
  as a built artefact, kept in a reduced workspace, or republished, since
  parity-first and workspace retirement cannot both be executed at once; where
  rendering executes in production, given there is no Web Dockerfile and the
  default `aspnet:10.0` base has neither Chromium nor the Liberation fonts and
  `PublishContainer` cannot install them; and whether unaccepted report wording
  and the three provenance-sensitive signature images may ship in the production
  assembly behind a closed gate. The Core render contract, the ADR and the
  staged route are drafted and waiting. The workspace-local uplift that had to
  precede any of this is done
  (task/report-renderer-workspace-uplift, 2026-08-05).
- Report rendering capabilities RPT-01–05 and EXT-08 — blocked on accepted
  CASE-31, ENG-01 and ENG-02 data, which requirements sequences ahead of them
  and none of which exists, and on the open report-wording decision. With
  `DESIGN_SPEC` superseded by the 2026-08-03 operator decision, the RPT
  specification must now come from those three capabilities plus operator
  answers; the field-level contract the renderer will demand of them is
  drafted (task/report-renderer-integration, 2026-08-03).

- Obsolete predecessor vault purge — five soft-deleted `uksouth` vaults on two
  platform-scheduled dates: `cespk-pg-kv-dev`, `cespkevakvufa3ci`, and
  `cespklockva7tzj2` on 2026-08-09, then the two consolidation predecessors
  `cespkboxkvv76a47` and `cespkenrichkvgi62sd` on 2026-08-10 (verified
  read-only 2026-08-04; the earlier single 2026-08-09 date covered only the
  first three). No action unless a purge fails; the watch is not clear until
  both dates pass.

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
