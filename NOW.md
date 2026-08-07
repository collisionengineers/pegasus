# NOW — updated 2026-08-06

(Anything here older than 14 days is stale: delete it, don't investigate it.)

## Doing (one line per live claim)

Claim format: `- <IDs and/or goal> (branch task/<slug>, taken YYYY-MM-DD, by
<agent>)`. Nothing is in flight unless it is claimed here on `origin/dev`.

- Manual upload creates a case, and the Inbox becomes a mail viewer
  (INT-01/INT-19/INT-26, UI-10 pulled forward): a manual upload must be
  processed on the spot and land on a prefilled, editable case-creation
  screen. Today `src/Pegasus.Web/Program.cs:567` binds `IIntakeSubmission` to
  the queue-only `ReceiveIntake`, so `Upload.cshtml.cs` always returns
  "being processed" and both of its other branches are unreachable; the file
  then waits on a Worker timer and only becomes a case if the QDOS extraction
  policy returns `Applicable` with a principal. Retire the
  `QdosAlphaCaseActivationPolicy` activated-principal gate in favour of "the
  principal must exist and be active" so staff can allocate against any
  registered principal — automatic non-QDOS identification stays out of
  scope. Then separate the two surfaces the nav conflates
  (`_Layout.cshtml:42` points "Inbox" at the intake receipt list): build the
  mail workspace over retained messages and give the received-items list an
  honest name. Administration gains additional mailboxes and per-mailbox
  enable/disable driving the intake poll, which is single-mailbox
  config-bound today, by extending the existing `ApprovedMailbox`
  administration to the intake route scope (branch
  task/upload-case-creation-and-inbox, taken 2026-08-05, by claude).
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
- A QDOS email forwarded to the instructions mailbox did not create a case:
  diagnose it against production read-only along the intake chain (Graph poll →
  staged artifact → dispatch → `ProcessIntake` decision → allocation), name the
  failing link with live evidence, and fix what is genuinely defective. Reads
  are authorised for `pegasus-prod-appi-252ow37gij`,
  `pegasus-prod-worker-252ow37gij`, `pegtrans252ow37gij`, the `pegasus`
  database, and the `instructions@collisionengineers.co.uk` message; every
  production mutation stops for separate approval. No edits to the files
  `task/upload-case-creation-and-inbox` owns and no widening of an accepted
  fail-closed policy — findings that are operator decisions go to
  `docs/open-decisions.md` (branch task/qdos-forward-intake-failure, taken
  2026-08-05, by claude).

## Merged, not deployed

Nothing that needs a release. The estate serves **release 7** (2026-08-05,
revision `32feefa…`), which carries every source change in `dev` and `main`.
`dev` and `main` have since advanced by documentation commits and by
`workspaces/` source that no application build compiles; they change no built
artifact, so they ride the next functional release rather than justifying one.
The deployed-evidence record is owned by
[operations § Production environment](docs/operations.md#production-environment).

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

Two things are deployed as code without being active, and neither is a
release claim:

- **AI-09 Send to AI round trip and the Automation Actor assessment toolset
  (MCP-06)** (PR 332, merge `5555440`) reached production in release 6, but
  the whole surface is composition-gated off by default
  (`Features:SendToAi`, `Features:AutomationMcp`) and DevelopmentOffline-only,
  so no environment runs it and no activation, navigation-link, or acceptance
  claim is made. Production would additionally need a non-preview transport
  decision. Evidence is tier 2–4 local only — the tier-5 external-client
  round trip is still queued below.
- **`claudeuiverification`**, an enabled Administrator seeded into production
  by release 6 from a credential committed to `appsettings.json`, at the
  operator's request and on their stated risk assessment. **It must be
  removed before go-live**; replacing the `Bootstrap:VerificationAccount`
  block with `{ "Removed": "claudeuiverification" }` deletes it on next
  start.

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
- Restore Standalone Audit case creation end-to-end:
  `EfStandaloneAuditEvidenceStore.cs:73` still gates original-report
  evidence confirmation on the retired `draft_ready` decision code, which
  nothing has written since `9393c98`, and `ProcessIntake.cs:328-333`
  routes every standalone-Audit instruction to `Needs sorting` — so the
  `StandaloneAuditEvidenceId` that `AcceptIntake.cs:61-67` requires can
  never exist and every Audit acceptance is refused. Also migrate the
  remaining legacy `draft_ready` readers (`EfCaseAcceptanceStore.cs:170`,
  `EfOperationsStore.cs:658`), fix the wrong legacy-mapping comment at
  `IntakeContracts.cs:28-30` (the store maps `draft_ready` to
  `CaseCreated`, not `NeedsSorting`), and the stale filter description at
  `IntakeMcpTools.cs:58` (repository audit, 2026-08-06).
- Make intake refusal fail-closed and visible (INT-25 follow-through): no
  `ProcessIntake` branch can emit `BlockedIntake`, although
  `requirements.md:190-193` defines it as the fail-closed outcome with a
  reason and reasoned resolve/retry actions — the named conditions reach
  quarantine, silent allocation failure, or `Needs sorting` instead;
  `IntakeWorkItems` states (pending/dispatching/retry/poisoned) have no
  Web surface at all; poison and quarantine rows project with null retry
  fields so a refused message cannot be re-driven from the product; and
  `DurableIntake.cs:647-653` swallows every allocation failure with
  `catch (Exception)` — no log, no persisted event. Automatic allocation
  also hard-codes `CaseType.Inspection` (`DurableIntake.cs:633`).
  Coordinate with task/qdos-forward-intake-failure: PR 356's "Create the
  case from this instruction" copy points at a form rendered only for
  `Needs sorting` (`Details.cshtml:392`) and a handler that refuses
  anything else (`Details.cshtml.cs:352`) (repository audit, 2026-08-06).
- Triage reserved-meaning edges (INT-21 adjacency):
  `DurableIntake.CreateTriageIfQualifyingAsync:774` requires the
  `CaseCreated` decision — the decision, not an existing Case: it runs
  after the silently-swallowed allocation in the same pass (`:538-540`),
  so it inverts the pre-case property (`operator-notes.md:37`,
  `requirements.md:264`) and can also run for a receipt whose allocation
  failed. Latent while `NoAcceptedIntakeTriageMatcher` is the only
  matcher; fix as an eligibility/ordering decision proven by behaviour
  tests before any matcher is accepted; the manual staff Triage origin
  (`requirements.md:264`) has no Core command; decide the `/Triage`
  route/folder/namespace overload — the Queues screen kept them while
  `design/README.md:352` claims the route was renamed; and rename the
  three general-sense Audit identifiers (`EfIdentityAuditStore`,
  `AutomationMcpAuditor`, migration `MailboxRouteAudit`) (repository
  audit, 2026-08-06).
- Decide and fix the Assessment surface exposure (UI-15/AI-09):
  `/Cases/{id:guid}/Assessment` is routed unconditionally in Production —
  any authenticated staff member on releases 6/7 can open unbound section
  forms with dead `type="button"` Save controls. Close the panel-state
  gaps: `Cancelled` and `Expired` fall through `EvaluatePanelStateAsync`
  and render as `available` (`Index.cshtml.cs:181-192`), the panel never
  returns to `available` after a terminal request so a case can be sent
  exactly once ever, `unavailable` (Administrator switch off, ineligible
  state) is unreachable behind any terminal request, and the send confirm
  posts only via `form.submit()` script that the production CSP discards
  (repository audit, 2026-08-06).
- Send to AI traceability and PAV follow-ups (AI-09/MCP-06, extends the
  PR 332 review items above): bind `workRequestId` on the eleven tools
  that lack it or record why not; enforce or drop the write-only
  `CapabilityScope` and `CaseVersionAtSend` stamps; persist the reply
  timestamp (`requirements.md:964` — parsed then dropped); read connector
  `/status` for the promised diagnostic view or descope it; treat the
  connector's documented `duplicate: true` replay as success rather than
  `Failed`; surface the request id on the completed panel; make the
  ≤500-char operator instruction reachable or remove the mechanism; PAV
  slider: span guide evidence rather than the chosen figures, add the
  equity-in-repair row, gate on a costed repair total, clamp the paired
  numeric input; add the plan's idempotent-create and duplicate
  operation-key tests (repository audit, 2026-08-06).
- Guard `main` without paid branch protection (operator decision
  2026-08-05: no paid GitHub features): add a push-triggered CI job on
  `main` that fails loudly on any direct or non-merge push — detection,
  not prevention. `440ab5c` reached the deployment branch without a PR and
  carried a defect CI would have caught (fixed in PR 335); PR 324 was
  opened against `main` before redirection; the dual-branch history cost
  again at PR 348 with ninety add/add conflicts in a merge that had no
  source conflict. Add CI checks for new-Markdown-file creation and NOW.md
  claim-format/staleness while in the workflow file.

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
  (`_ReasonDialog` focus trap and the Assessment page's three inline
  scripts; the `_FreshnessBanner` inline script is already gone, and the
  Assessment page is routed in Production, not unrouted as this item
  previously said): external script files versus CSP hashes.
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
- Decide the unbuilt approved UI packages (audit total 17; the recovered
  set carries a page-5 numbering collision, so work from the named list
  rather than the count): the operator-approved
  `docs/ui-work` review (deleted in `4c89669`, recoverable at `4c89669^`)
  shipped 8 of its 31 page packages; pages 8 (receipt review), 9-10
  (image intake), 13 (public-upload body), 5 (administration hub) and
  19-31 (all thirteen administration sub-screens) were never built and
  have no successor claim — queue, re-scope, or descope each explicitly.
  Includes the banned-terms sweep: seventeen shipped `.cshtml` files
  violate `design/README.md:309-316`, led by `Intake/Details.cshtml`
  (51 hits) (repository audit, 2026-08-06).
- Absorb the approved UI divergences into the design authority (operator
  decision 2026-08-05: absorb, not revert): `design/README.md` still
  asserts pre-divergence values — 2px radius against the shipped 6px/5px,
  stale colour, spacing and token claims, "no ledes" while nine pages set
  one — and `site.css:1-12` cites the deleted `docs/ui-work` standards.
  Reconcile the unapproved extras (modal scrim/shadow, 10px/14px spacing
  tokens, the retained eyebrow/lede mechanism, `site.js`, the generic
  `/status/{code}` route) into the authority or remove them, and apply
  the ui-spec state-matrix additions the durable-rules proposal specified
  but never landed (repository audit, 2026-08-06). Restructure wave 3:
  once the reorg lands, the merge target is the new `docs/design.md`.
- Correct the stale documentation claims from the accuracy audit:
  `architecture.md` L87/L307/L503 still describe the removed
  `ReceiveIntake` handler on `/Intake` and its orphaned
  `Program.cs:731-758` gate (coordinate with
  task/upload-case-creation-and-inbox), omit `/diagnostics/version` and
  the `Bootstrap:VerificationAccount` reconciliation from the
  authentication boundary, and lag the UI rework in the implementation
  map; `capabilities.md` header arithmetic says 228 IDs and 199 planned
  against a 230-row table and 201 with targets, INT-25 says "not yet
  deployed" though its merge is inside release 7 (`32feefa`), and INT-01
  says "accepted" while upload creates no case (repository audit,
  2026-08-06).
- Restructure ADR and reorg execution (operator decisions 2026-08-06;
  **gated on PRs 340, 342 and 356 merging first** — they edit
  docs/architecture.md and temp-plans): record the settled target
  structure as an accepted ADR, then execute it — `docs/operations.md`
  splits into a current-state record plus a new `docs/runbook.md`;
  `design/README.md` and the surviving `design/product/` content become
  one `docs/design.md` (design/ keeps assets only); `docs/reference/`
  moves to top-level `reference/` with `.gitattributes` and link updates
  in the same commit; rule dedupe to single owners happens during the
  move (the evidence-tier ladder lands in engineering.md and every
  referrer updates; the ADR index collapses to its blanket qualifier);
  `docs/index.md`'s router updates to the new file set. `CLAUDE.md`
  stays a symlink to `AGENTS.md` — Claude does not read AGENTS.md by
  default. Absorbs the rule-dedupe line below and the
  PerformanceTests/Python-split/git-hygiene/duplicated-asset parts of
  the hygiene line below.
- Deduplicate the process rules to single owners: at least twelve rules
  are stated in two or more files (~30 sites) — collapse the ADR index's
  twelve per-row "proves no…" repeats to its own blanket rule, the
  git-safety allow/ban list to one owner, the evidence ladder to
  `operations.md#required-evidence-tiers` (three tables carry it today),
  and the 34-line CI prose duplicate in `engineering.md:25-62`; ownership
  pointers stop restating what they point to (repository audit,
  2026-08-06). **Absorbed into the restructure reorg task above — do not
  claim separately.**
- Repository hygiene sweep: `tests/Pegasus.PerformanceTests/` has no
  `.csproj` — two test files that have never compiled; adopt or delete.
  `scripts/reference_data` and `tests/reference_data` split one Python
  component across two trees with no CI. Stale `.gitattributes` rule for
  the absent `docs/reference/imp-docs/**`; `.gitignore` carries
  predecessor paths (`CollisionSpike`, `/p17/`), a stray `l` line and
  contradictory `.obsidian` rules. Brand logo and three signature PNGs
  are duplicated between `design/brand/` and
  `docs/reference/rendererref1/` with differing bytes. Decide
  `.obsidian/` and `.infisical.json` keep or drop. Add an `infra/`
  validation lane or record why not. `scripts/email-eval-desktop/` stays
  — it is the operator's own tool — but note it references
  Core/Infrastructure while nothing builds it (repository audit,
  2026-08-06). **The PerformanceTests, Python-split, `.gitattributes`/
  `.gitignore` and duplicated-asset items execute inside the restructure
  reorg task above; still claimable here: the `.obsidian` keep, the
  `.infisical.json` confirm-then-delete, and the `infra/` validation-lane
  decision.**
- CI wall-clock second pass: PR 321's reduction (sharded lanes, per-run
  template database, caches) under-delivered on the operator's intent —
  measure what still dominates (five of six jobs are Windows-pinned, the
  browser lane reinstalls Playwright on every run despite the cache, lane
  structure) and cut further, building on PR 321 rather than repeating it
  (operator, 2026-08-06).
- Complete the canonical-docs accuracy audit: the 2026-08-06 audit
  claim-checked `architecture.md`, `design/README.md` and the roadmap
  forms only; `requirements.md`, `operations.md`, `operator-notes.md`,
  `open-decisions.md` and `engineering.md` have had no claim-level
  verification against code (artifact critique, 2026-08-06).
- Queue-and-claims hygiene: the report-renderer planning Doing line is
  superseded and closed by its successor line yet still listed; the
  CASE-27 Doing line's branch has already released its claim (`c02d75a` —
  coordinate with open PR 342); remote branch `task/vault-consolidation`
  has no Doing line; and the two live `docs/temp-plans/` files have no
  matching Doing line (staleness ladder applies — confirm ADR-0021
  carries their durable content before deletion) (repository audit,
  2026-08-06).

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
