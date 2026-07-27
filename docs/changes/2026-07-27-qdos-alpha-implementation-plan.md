# Finish `workflow/20260727-deliver-qdos-alpha`

## Context

The branch is clean at `b2f40a2`, identical to `main`, with no implementation commits. Issue #3 and merged PR #4 are planning evidence only. This revision does not authorize implementation, an Azure read/write, deployment, or retirement.

The finish line remains the complete `0.1.0-alpha.1` outcome in `docs/changes/2026-07-27-qdos-alpha-reference-corpora.md`: all 127 `Now` capabilities, with only QDOS activated for case creation, through the shared evaluator, durable intake, authenticated Operations-first Web caller, real Worker triggers, staff MCP, immutable references, case/Triage/lifecycle work, document custody, EVA handoff, report evidence, recovery, and operator acceptance.

Delivery now has two hard stages:

1. **Offline development acceptance.** Build and document the complete application locally first. It runs with LocalDB, Azurite, the actual Functions host, local durable mailbox/custody adapters, local HTTPS authentication/MCP, approved local evidence, and no cloud credentials or external-service calls.
2. **Approved live integration and release.** Only after the offline acceptance gate is green may the delivery add/enable Graph, Box, DVLA/DVSA, Azure, or other live adapters, reconcile infrastructure, deploy, and obtain live/operator acceptance.

Offline acceptance is a mandatory implementation checkpoint, not a reduced alpha or substitute for live-service evidence. Issue #3 remains open until both stages complete.

## Approach

### Fixed decisions

1. **Tool-neutral repository workflow.** Remove current guidance that requires or names the `azure-workflow` plugin. Preserve the useful repository-native authority, issue/change-record, exact-head review, exact-target approval, and verification rules without plugin routes. Supersede decision 0010; preserve its historical facts rather than rewriting history. Azure Skills, MCP tools, Microsoft Learn, and the language server are execution aids, not repository authority or authorization.
2. **Offline first means no live implementation drift.** Before offline acceptance, do not add a live Graph/Box/DVLA client, modify Bicep/`azure.yaml`, read or mutate Azure, deploy, or touch the stale estate. Production ports and contracts are defined, but Development selects only local implementations. No production code may silently fall back to a local adapter.
3. **Keep the four-project modular monolith.** Core owns policy and ports; Infrastructure owns persistence and adapters; Web and Worker are composition roots. Add no project, top-level store, runtime, migration stream, deployment unit, generic rule engine, or second classifier.
4. **One organization may hold both business roles.** Model an organization once, then assign `WorkProvider` and/or `InstructionIntermediary` roles. A route result separately records the route owner, route kind, and resolved work provider. Those organization IDs may be equal. The same organization may have independent direct-provider and intermediary policies; an individual message that matches both remains ambiguous and fails closed.
5. **One active inspection workflow state.** Active case states are `Not ready`, `Review`, `Report preparation`, and `Post report`, plus the reasoned `Held` overlay. Remove `Inspection` as a separate state: inspection work and report preparation are activities within `Report preparation`, represented by typed completeness/work data rather than a duplicate lifecycle state. Terminal states remain `Post-report complete`, `Provider cancelled`, `Collision Engineers rejected`, and `Created in error`.
6. **One Core email decision owner.** Transport adapters normalize evidence only. A versioned Core policy owns route selection, provider/type/case evidence, received/sent classification, and the specifically approved report/Triage matchers. The current QDOS extractor remains an inner typed extractor, not a competing orchestrator.
7. **Triage identification is evidence-gated.** `Triage` is a business pre-case record, distinct from mailbox category, folder destination, and case association. Exact Triage predicates must be learned and approved from genuine positive, negative, ambiguous, forwarded, reply-chain, and untouched holdout examples in the local evaluator. No sender-only, subject-keyword-only, or universal fallback may create Triage.
8. **The evaluator is a local Web UI.** Replace the planned Worker-only evaluator command with an authenticated Development-only Razor Pages workbench backed by the same parser and Core evaluator used by intake. It supports genuine `.eml` review, human labels, batch comparison, route/policy evidence, Triage analysis, and ignored review artifacts. It never allocates a reference or writes into `corpus/`.
9. **The existing deployment is stale predecessor state and has no restart window.** Treat `docs/azure/current-inventory.md` as a dated 2026-07-23 snapshot, not a deployment target. The user has confirmed it is not in active use and may be rebuilt from deployment evidence if ever needed. Author a staged teardown runbook now; after offline acceptance, execution may proceed as a separately approved exact-resource operation without waiting for `Next`/`unallocated` cutover. Refresh inventory first, preserve or separately decide shared/data-bearing assets, record redeployment provenance, and never begin with broad resource-group deletion.
10. **Case identity and external effects remain immutable.** Split intake into durable receive/process/resolve/accept operations, use ID-only queue messages and a SQL outbox, allocate principal/reference once in the acceptance transaction, append policy/history revisions, and never reuse or rewrite an allocated reference.

Evidence-dependent choices remain hard holds: every provider/intermediary policy and disposition, automatic report and Triage predicates, ordinary-image VRM engine, DVLA/DVSA contract and mileage rule, focused-`0.1.0-alpha.1` EVA mapping, Graph scope, Box identity/root/operations, and live Azure targets. A missing hold does not justify a placeholder, fabricated fixture, dormant flag, or reduced release scope.

### Offline development environment contract

#### Required tools and pins

The default developer profile requires only local tooling:

| Dependency | Contract |
|---|---|
| Windows / PowerShell | Windows 11 and PowerShell `7.6.3`; repository commands remain PowerShell-first |
| Git | Required for repository/path guards and exact working-copy checks |
| .NET SDK | `global.json` pin `10.0.302`; restore/build all four existing projects |
| Node/npm | Node 24 / npm 11; `npm ci` installs repository-pinned dependencies |
| Python | `3.11+`; standard library only for the offline `.xlsx` provider-domain authoring command; no Python application runtime |
| Azurite | npm package `3.36.0`; Blob and Queue services use run-scoped loopback ports/state |
| Azure Functions Core Tools | v4, pinned/checked at `4.12.1`; starts the actual isolated Worker host |
| SQL Server Express LocalDB | Supported LocalDB runtime; full offline app uses committed SQL Server migrations, transactions, constraints, and outbox |
| SqlServer PowerShell module | `22.4.5.1`, CurrentUser scope; local diagnostics and approved Entra-token post-provision grants |
| Development HTTPS | trusted `dotnet dev-certs` certificate for Web, OAuth, and MCP |
| Browser evidence | pinned Microsoft Playwright for .NET browser binaries plus the repository accessibility dependency |

`Azure CLI`, `azd`, Bicep, GitHub CLI, Infisical, Box CLI, cloud login, live credentials, and Docker are not prerequisites for the offline profile. They belong to the separately selected `Cloud` profile after offline acceptance. That profile pins the already supported Azure CLI `2.88`, Azure Developer CLI `1.28.0`, Bicep `0.45.15`, GitHub CLI `2.88`, Infisical `0.43.104`, Box CLI `4.9.2`, SqlServer `22.4.5.1`, and adds `ExchangeOnlineManagement` `3.10.0` at CurrentUser scope. The application uses the .NET Graph SDK; `Microsoft.Graph.Authentication` is not a required PowerShell module. Initial package/browser restore may use package feeds; normal `Start` and `Smoke` must run without cloud or vendor access.

#### Commands and ownership

Implement these exact developer entry points:

```powershell
pwsh ./scripts/Invoke-Doctor.ps1 -Profile Offline
pwsh ./scripts/Initialize-LocalDevelopment.ps1
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Start
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Status
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Smoke
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Stop
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Reset
```

- `Invoke-Doctor -Profile Offline` checks only the local table above and prints exact install/repair commands. `-Profile Cloud` adds the pinned cloud/vendor tools and modules but does not log in or mutate an external service. The workstation runbook gives explicit CurrentUser `Install-Module -RequiredVersion` commands. A separate approval-gated `Invoke-LivePreflight.ps1` proves each authorized login/scope; for Exchange it imports `ExchangeOnlineManagement`, connects with the approved operator, and runs `Test-ServicePrincipalAuthorization` against the one allowed mailbox and one denied control mailbox before Graph activation.
- `Initialize-LocalDevelopment` runs `npm ci`, installs pinned Playwright browsers, verifies/trusts Development HTTPS, validates LocalDB/Functions/Azurite, and creates only ignored local state. It never installs a global/system package silently and never retrieves a secret.
- `Invoke-LocalDevelopment` allocates a run ID, loopback ports, GUID-named LocalDB database, Azurite state, local mailbox, local case-file root, logs, and process manifest beneath ignored `artifacts/local-development/<run-id>/`. It starts dependencies in order, waits for real readiness, invokes a Development-only migration command, starts Worker and Web, and stops/resets only resources whose ownership manifest and run ID match.
- First start prompts securely for an at-least-eight-character bootstrap Administrator password, exposes it only as a child-process environment value to the one-shot command, forces change at first sign-in, and does not store or echo it. Later starts refuse bootstrap when an Administrator exists.
- Failure retains content-safe diagnostics. `Reset` refuses ambiguous database/process/path ownership and never touches corpus, tracked files, another run, Azure, or the stale deployment.

#### Offline adapter boundary

| Production boundary | Offline implementation | Honest limit |
|---|---|---|
| Azure SQL | GUID-named LocalDB database using the production migration stream | Does not prove Entra, Azure throttling, PITR, or managed identity |
| Blob/Queue/Functions | Azure SDK against run-scoped Azurite plus the actual Functions host | Does not prove Azure RBAC, scale, durability, or platform poison behavior |
| Outlook/Graph | durable local mailbox store containing genuine working-copy `.eml`, immutable IDs, Inbox/Sent folders, delta cursor, `sentDateTime`, and reply-chain metadata | Does not prove Graph permissions, delta edge cases, throttling, or mailbox policy |
| Box | guarded local case-file store through the same Core custody port: approved root descriptor, descendant proof, file/folder/version IDs, hashes, versioning, logical removal, and loopback development file requests | Does not prove Box identity, scopes, SDK behavior, retention, or recipient delivery |
| DVLA/DVSA | explicit Development replay adapter that accepts only owner-approved ignored response fixtures and typed failure scenarios | Missing evidence returns `Unavailable`; it never invents a successful vehicle/MOT result |
| VRM | selected local engine after the genuine labelled benchmark | No engine is registered until accepted; no generated images |
| EVA | the real deterministic local JSON/image/manifest bundle | Does not prove EVA import/receipt or named assignment |
| OAuth/MCP | local HTTPS OpenIddict server, pre-registered local public PKCE client, and actual Streamable HTTP `/mcp` calls | Claude-hosted callback and production key custody remain live gates |
| Telemetry | structured console/test exporter and optional local OTLP collector | Does not prove Azure Monitor ingestion, alerts, retention, or cost |

Development registrations are explicit under a single `DevelopmentOffline` profile and fail startup outside Development. Production registrations fail closed when required configuration is absent; they never substitute the local mailbox, filesystem, fixture replay, development key, or loopback endpoint.

#### Documentation deliverable

- Root `README.md`: five-minute offline quick start and links only.
- `docs/runbooks/developer-workstation.md`: supported versions, install/repair commands, optional Cloud profile, and no-Docker boundary.
- New `docs/runbooks/local-development.md`: bootstrap, commands, URLs, first Administrator, state layout, evaluator workflow, local mailbox/custody use, debugging, reset/recovery, network/security boundaries, and proof limitations.
- `docs/runbooks/testing/local-testing.md`: make `Offline` the default baseline; split live profiles; record exact smoke/evidence lanes and what they do not prove.
- New `docs/runbooks/predecessor-teardown.md`: exact-resource inventory and approval manifest, no-active-caller proof, redeployment provenance, shared/data-bearing asset disposition, dependency-ordered deletion, credential/RBAC cleanup, orphan/cost verification, and the explicit no-restart-window recovery posture.
- `docs/architecture.md`, `docs/operations.md`, design source/runtime mapping, capability owners, and the existing change record: document local versus live callers without treating local registration as live acceptance.
- Documentation checks validate commands, pins, links, absence of current `$azure-workflow:` route guidance, and consistency between scripts and runbooks.

### Repository change map

- **Workflow/governance:** `AGENTS.md`, nested current guidance, `docs/index.md`, `docs/agent-guidance/*`, `docs/operations.md`, current product/runbook ownership text, ADR index, and documentation tests. Add one superseding tool-neutral ADR; retain onboarding records as historical/superseded evidence.
- **Developer tooling:** `package.json`/lock, `scripts/Invoke-Doctor.ps1`, new `scripts/Initialize-LocalDevelopment.ps1`, new `scripts/Invoke-LocalDevelopment.ps1`, dependency-free `scripts/Build-ProviderReferenceData.ps1`, approval-gated `scripts/Invoke-LivePreflight.ps1`, renamed tool-neutral documentation check, `scripts/Invoke-RepoCheck.ps1`, and CI callsites.
- **Core:** extend `IntakeContracts.cs`; refactor `ProcessIntake`; add receive/process/resolve/accept, organization/route policy, email classification/Triage matching, case/reference/lifecycle, custody, outbox, identity/actor, and MCP-facing use cases. Remove the single-policy path after all callers move.
- **Infrastructure:** extend the existing `PegasusDbContext` and single migration stream; add reference seed generation, LocalDB/Azurite stores, local mailbox/case-file/vehicle-replay adapters, then live adapters only after the offline gate.
- **Web:** replace scaffold routes/shell; add Identity/OpenIddict, Operations-first UI, Development-only email evaluation UI, case/Triage/intake/admin workbenches, OAuth metadata/endpoints, and `/mcp`.
- **Worker:** keep the existing project; add actual timer/queue triggers and outbox processing. No second executable project or Worker evaluator mode.
- **Tests/evidence:** Core contracts, LocalDB contention, Azurite/Functions host, authenticated Web/MCP, Playwright/accessibility, genuine local evaluator cohort/holdout, adapter contract parity, and negative/recovery lanes.
- **Azure/IaC:** author `docs/runbooks/predecessor-teardown.md` without touching Azure; defer `infra/`, `azure.yaml`, `.azure/deployment-plan.md`, live settings, and runbook execution until offline acceptance; then refresh approved inventory, tear down exact predecessor-only resources under a separate change/approval, and provision only an isolated `Next`/`unallocated` target.

### Delivery sequence

#### 0. Activate delivery and remove plugin-specific guidance

- After explicit implementation approval, update issue #3 to the delivery outcome and change the existing record from `planned` to `in_progress`; attach the future delivery PR. Do not create another status ledger.
- Add a canonical decision superseding `docs/decisions/0010-adopt-azure-workflow.md`: preserve repository-native change records, authority order, exact-target cloud approval, proportional validation, independent exact-head review, and no-agent-merge; remove all current plugin route tokens and plugin ownership claims.
- Replace `docs/agent-guidance/agent-routing.md` with tool-neutral request/change/review/cloud-operation routing. Rewrite current source-role wording from “Azure Workflow-maintained” to role-based maintainership. Rename `Test-AzureWorkflowDocumentation.ps1` to a tool-neutral name and update `Invoke-RepoCheck`, CI, and documentation callsites.
- Keep dated onboarding change/ADR/history as superseded historical evidence with clear current links. The guard must fail if `$azure-workflow:` tokens reappear in active guidance, while allowing quoted historical evidence.
- Re-run the pinned provider-domain source hash/counts and freeze the 127-capability-to-evidence index in the existing change record. No cloud or vendor read occurs in this step.

#### 1. Make the offline platform reproducible before feature code

- Implement the tool/profile/command contract above. The default doctor no longer requires Azure/Box/Infisical tools or login.
- Add fail-closed `DevelopmentOffline` configuration: loopback-only endpoints, no production credentials/client construction, run-scoped paths/names, content-safe logging, and startup rejection when Development adapters appear in another environment.
- Move Development migration from incidental Web startup to the explicit orchestrator-owned Development command. Web/Worker never migrate on normal startup.
- Start current Web, Functions host, LocalDB, and Azurite; prove `Status`, readiness, `Stop`, failed-run diagnostics, parallel run isolation, and ownership-safe `Reset` before building further behavior.
- Publish the quick start and full local runbook in the same slice; stale instructions are a failing acceptance condition.

#### 2. Publish the first cumulative provider-domain evidence snapshot

- Run `pwsh ./scripts/Build-ProviderReferenceData.ps1` from the repository root
  with `docs/reference/workproviders-and-repairers/initial.xlsx` closed. The
  wrapper rejects the selected workbook's exact sibling Office lock marker and
  an exclusive-read failure as `source-locked` before Python discovery, source
  hashing/parsing, staging, or output work.
- The source contract is intentionally narrow: SHA-256
  `e4bf89b0aeef3f1106bf34ed50f74dffc44c5ed748e0ad0811b66ee099b6cd29`,
  worksheet `Sheet1`, 11 headerless rows, provider code in column A, and
  semicolon-separated email observations in column E. Columns B-D and later
  columns are opaque and ignored. Only the lowercase suffix after the final
  `@` is retained; local parts and full addresses are discarded immediately
  and never emitted, persisted, tested, documented, or logged.
- Use Python 3.11+ standard library only (`zipfile` and
  `xml.etree.ElementTree`) to read the one `.xlsx` file. There is no recursive
  workbook discovery, pip dependency, authoring virtual environment, package
  cache, network operation, requirements lock, second manifest, or runtime
  workbook reader.
- Publish one canonical UTF-8 JSON package with a final newline at
  `src/Pegasus.Infrastructure/Persistence/ReferenceData/provider-domains.v1.json`.
  The `0.1.0-alpha.1` package is `provider-domains-v1` and contains exactly 11 provider
  codes, 16 code/suffix associations, and 16 distinct suffixes with source-row
  provenance. The package is embedded by Infrastructure and imported through
  one reviewed migration; application runtime reads SQL only.
- Core owns generic byte/hash/schema/version/source/provider/suffix validation,
  transient suffix extraction, deterministic `Unknown`/`Found`/`Ambiguous`
  candidate semantics, and the exact-version catalog port. Infrastructure owns
  immutable package/provider/evidence tables and the EF adapter. Neither owner
  contains source-specific global count constants.
- Provider-domain evidence does not activate a route. Direct-provider and
  intermediary route identity remains code-versioned policy under Decision
  0011. Only the separately accepted QDOS direct trait
  `@qdosassist.co.uk` may support the current route; every other imported
  suffix remains inactive evidence.
- Publication is append-only. The pinned `0.1.0-alpha.1` source/version/output is the only
  bootstrap without a previous package. A later version uses a new immutable
  cumulative workbook, a different package version/output, and the previous
  validated package; every prior provider/suffix pair must remain. Existing
  different outputs are never replaced. Corrections or removals require new
  accepted authority and a new explicit contract while old snapshots remain.
- `DATA-02` inspection-address/repairer reference data moves to
  `Next`/`unallocated`. Stable provider code plus package/source version is the
  preserved join seam. Inspection locations, history, defaults, Case-ID
  mapping, and authoring of those shapes are excluded until separately accepted
  evidence, authority, schema/package, migration, and caller proof exist.
- Verify generation with `pwsh ./scripts/Build-ProviderReferenceData.ps1 -Verify`,
  focused Core and integration tests, plus exact source/package/migration
  equality and suffix-only shape/count checks. No repository-wide email scan is
  required; retaining only the identifying suffix is a simplicity boundary.

#### 3. Establish staff identity, authorization, leases, and history

- Add ASP.NET Core Identity/OpenIddict tables to the same DbContext/migrations with `Administrator`, `Engineer`, and `User`; no public registration or MFA. All active staff may perform case/intake/document work; only Administrators manage accounts, principals, workflow configuration, approved mailboxes, and evaluator approvals.
- Configure ASP.NET Core Identity with minimum eight-character passwords and all digit/upper/lower/non-alphanumeric requirements disabled. Disable persistent account lockout entirely. Apply an ASP.NET Core fixed-window limiter to the login endpoint—10 attempts per trusted client IP per minute, zero queue, generic failure/`429` plus `Retry-After`—and a separate 100-attempts/minute global partition; production accepts forwarded client IP only from configured trusted proxies. Use a two-hour sliding idle cookie plus an immutable original-issue claim enforcing an eight-hour absolute maximum, security-stamp/account-enabled revalidation, antiforgery, secure cookies, forced first-password change, and no password/token/secret telemetry or business history.
- Add the one-shot `bootstrap-admin` and release-owned `register-mcp-client` command modes. The latter permits only pre-approved public S256 PKCE clients, exact callback/scopes/resource, no secret, wildcard callback, or Dynamic Client Registration.
- Map claims to explicit Core `StaffActor`; Core enforces policy and writes immutable business `ActionHistory` separately from security events. Account disable/role change revokes later browser/MCP use.
- Add five-minute hashed edit leases, 60-second heartbeat, holder display, optimistic versions, no Administrator override, and no routine view/heartbeat noise in permanent history.

#### 4. Build the shared email evaluator and local graphical evidence workbench

- LSP evidence shows the current real product caller is Development `/Intake/Upload`; Worker has no trigger. Refactor `ProcessIntake` without duplicating it: one source reader and one Core `EmailDecisionPolicy`/`InstructionRouteCatalog` serve the evaluator, Web intake, future Worker, and MCP.
- Evidence includes outer transport sender, normalized source sender, proved forwarded sender, recipients, subject, body/document/attachment fragments, occurrence labels, thread/reply identifiers, extraction completeness, and retained source references.
- Non-CE mail uses the root sender. A CE staff forward uses only an observed, approved forwarded-message shape with one consistent external sender; zero/conflicting/malformed original senders produce `Needs sorting`. Arbitrary quoted `From:` text is never authority.
- Evaluate direct-provider and intermediary predicates independently but select exactly one route. The same organization can own both policy types and can resolve to itself as work provider through an intermediary route. One message matching both route predicates is `multiple routes`, not an arbitrary precedence win.
- Keep QDOS as an inner extractor. Exact `@qdosassist.co.uk` can make the direct route applicable but never alone proves instruction type, case association, or activation.
- Map `/Development/EmailEvaluation` only in Development and require Administrator authentication. It imports genuine `.eml` into an ignored, path-guarded run workspace or reviews configured `unchecked` items; it refuses `corpus/`, tracked paths, symlinks/reparse escapes, and arbitrary server paths.
- The UI shows MIME/source hash, sender chain, extracted occurrences, completeness/issues, all matched/nonmatched route predicates, selected organization roles, resolved work provider, instruction type, case evidence, received/sent family, Triage decision, policy key/version, ambiguity, and evaluation duration. It supports one-at-a-time and batch review, keyboard use, filters, and side-by-side automated versus human outcome.
- Human review uses the settled received/sent taxonomy plus `Other` with required name/reason; route/Triage/provider/type/case corrections require reason. It appends JSONL/CSV review evidence and moves a working copy to `checked` only after durable review. It does not create a product intake/case/reference or mutate the source corpus.
- For Triage, the evaluator records separate labels for mailbox family (`pre-instruction-emails`), business Triage yes/no/ambiguous, provider/route, conversation identity, VRM evidence, and case association. Alex approves deterministic predicates only after genuine positive/negative/ambiguous/forward/reply and untouched holdout results. Ambiguous/no-match remains `Needs sorting`; no fallback creates Triage.
- Build the 88-provider route disposition inventory outside Git. Every proved route needs positive, negative, ambiguity, forward/intermediary, retry, and holdout evidence. Approve automatic report and Triage predicates in this same evidence program before those callers are activated. Commit only aggregate counts, hashes, policy versions, and limits.

#### 5. Make intake durable and idempotent through LocalDB, Azurite, and Functions

- Refactor Core into `ReceiveIntake`, `ProcessIntake`, `ResolveIntake`, and `AcceptIntake`; persist staged identity, processing state, immutable evaluation revisions/current revision, manual blocking, retry, and SQL outbox.
- Web/local-mail adapter stages bytes to run-scoped Azurite, then atomically commits receipt + processing-outbox. Worker dispatches only receipt IDs to `intake-work`; an actual isolated `[QueueTrigger]` loads retained bytes and calls the same Core process use case.
- Source identity is channel + immutable external token; same identity/different hash fails closed. Ordinary retry reuses the first completed policy revision. Explicit staff re-evaluation appends actor/reason/version and cannot bypass QDOS or standalone-Audit gates.
- Unreadable, encrypted, corrupt, unsupported DOC/MSG, oversized, bounded-out, incomplete, unknown-route, multiple-route, contradictory, non-QDOS, and dependency-unavailable inputs remain reviewable in `Needs sorting` or reasoned `Blocked intake`, with no case/reference.
- Use renewable SQL outbox claims and unique operation keys. Five attempts use 30 seconds, 2 minutes, 10 minutes, 30 minutes, and 2 hours, honoring a longer approved `Retry-After`; exhaustion is terminal and visible.
- Prove duplicate delivery, hash conflict, SQL-after-Blob failure, queue outage/replay, poison exhaustion, crash/restart, corrupt/oversized data, and one durable outcome with no source bytes in queue/logs.

#### 6. Implement case, reference, Triage, task, custody, and lifecycle policy

- Add provider principal/successor and shared sequence-lineage models; typed case/provenance; parties; intake links; documents/versions; tasks/chasers; completeness; lifecycle; Triage/findings; external evidence; outbox attempts; and immutable action history.
- Allocate one transaction with acceptance, case/intake link, history, and custody/external outbox. Use one atomic principal-lineage + Europe/London year counter across QDOS types. Base `{principalCode}{yy}{nnn}`; standalone Audit display `a.`/`ap.` only from retained Repairable/Total-loss evidence; Inspection + Audit adds the later Audit display reference without a second sequence. At 999, allocate nothing.
- Principal code becomes immutable after first allocation. Successor creation is one Administrator transaction: close predecessor for new work, share sequence lineage, preserve both IDs, and prohibit alias/in-place rename/overlap/reference rewrite.
- `AcceptIntake` creates exactly one incomplete `Not ready` QDOS case for definitive accepted instructions and records the applicable inspection-mode source: explicit evidence first, otherwise the versioned provider default above. `Review` requires separate instruction/image completeness confirmations. `Report preparation` begins after review and owns inspection/report work; typed activities/findings distinguish work without an `Inspection` lifecycle state. Update every canonical state list, dashboard query, transition, migration, test, and UI label; leave no current alias for `Inspection` state.
- Standalone Audit requires retained original Engineer report and staff-confirmed Repairable/Total-loss assessment before allocation. Inspection + Audit adds derived Audit identity only when the later Engineer finding exists.
- Wrong principal closes the original as `Created in error`, requires reason, allocates a normal corrected replacement, links both, preserves folders/references, and never reopens either identity incorrectly.
- Triage stays pre-case: normalized VRM; Open/Awaiting information/Finding recorded/Completed/Cancelled; Roadworthy/Unroadworthy findings; superseding findings; exact reply-chain Sent evidence for completion; reasoned correction/reopen; optional assignee/link; no due date/chaser; no subject/VRM/manual-message fallback.
- Chasers use Europe/London wall time seven calendar days after `Not ready` and every seven days. `Held` stores remaining interval; return to `Not ready` resumes; return to `Review` ends it. Preparing/copying sends nothing; manual send recording is an actor assertion, not delivery evidence.
- Report evidence is one immutable exact Sent item from an approved mailbox with `sentDateTime`; accepted automatic predicates or reasoned staff link only. It moves to `Post report`, never proves receipt or closes automatically. Revisions/unlinks preserve history and recompute through Core.
- Terminal commands are explicit. Cancellation/rejection require reasons; reopen requires reason and a normally valid nonterminal destination, never direct `Held`, never `Created in error`. Archive is reversible read-only after closure, not deletion.
- SQL constraints cover source acceptance, sequence output, external identity, operation keys, first-Sent-to-Engineer, report/Triage evidence, and replacement relation. Verify sequence/outbox/lease concurrency on LocalDB, not only SQLite.

#### 7. Complete every external boundary locally

- **Local mailbox:** durable Inbox/Sent working store with immutable IDs/delta/reply-chain semantics; only genuine imported EML. Exercise QDOS, CE forwards, Triage, report evidence, duplicates, moves/deletes after accepted evidence, and typed throttle/transient/terminal scenarios without pretending Graph was tested.
- **Local custody:** guarded root and descendant proof before every operation, stable folder/file/version identity, ancestry, SHA-1/SHA-256, etag, semantic role, operation key, version-only writes, logical removal, and a loopback development file-request flow. Same use cases/contract tests later bind Box.
- **VRM:** benchmark candidate local engines against approved genuine ordinary vehicle photos; select on exact-read/false-positive/uncertainty/latency/licence/security/operator evidence. Persist suggestion provenance; staff acceptance creates provisional identity. No engine is added before selection.
- **Vehicle/MOT:** replay only approved ignored DVLA/DVSA contract material. Results are suggestions and never overwrite confirmed values. Missing fixtures visibly return unavailable. No valuation behavior.
- **Inspection address/mode:** deterministic precedence is explicit accepted physical address or explicit accepted mode, then the versioned provider historical default, then ambiguity/no match. No geocoding/AI. Surface source counts/version in the case; staff confirmation or reasoned correction remains authoritative and never mutates the reference package.
- **EVA:** approve focused-`0.1.0-alpha.1` mapping/readiness/image order/names/recovery using genuine cases, then produce deterministic JSON/images/SHA-256 manifest. First successful generation records `First sent to Engineer` once; regeneration never duplicates or claims EVA receipt/assignment.
- For all local implementations, run the exact Core port contract suite. A future live adapter must pass the same suite; any contract change sends the work back through the offline gate.

#### 8. Add real Worker callers against offline dependencies

- In the existing Worker add local-mail Inbox poll timer, SQL outbox dispatch timer, `intake-work` queue trigger, external-work recovery, due-work sweep, and Sent-evidence poll. Each claims persisted work, calls one Core use case, and acknowledges only after durable outcome.
- Ensure one mailbox poller through SQL lease/cursor. Align Functions poison handling with the five application attempts. No trigger creates a case/reference, changes lifecycle, calls a vendor, or carries bytes without Core.
- Worker host configuration points only to LocalDB/Azurite/local ports under `DevelopmentOffline`; startup proves no external client was constructed.

#### 9. Build the complete authenticated Operations-first Web and local MCP

- Replace the scaffold shell/fake `CE` CSS logo with the approved `design/brand/logos/logo_no_margin.png` and source/checksum mapping. Remove Privacy/scaffold navigation.
- Product routes: `/` Operations; `/Intake` and `/Intake/{id}` plus authenticated upload; `/Triage` and `/Triage/{id}`; `/Cases` and `/Cases/{id}`; Administrator account/principal/configuration/mailbox pages; sign-in/password-change/sign-out/access-denied; OAuth metadata/endpoints; `/mcp`. `/Development/EmailEvaluation` is local-only and absent from production routing/navigation.
- Header order is `Operations | Intake | Triage | Cases | Administration | Search | User`. Operations exposes exact Not ready/Review/Held/Needs sorting/Blocked intake/Triage/Due today/In today/Sent to Engineer/Reports sent queries with London day/week boundaries and explicit freshness/zero/loading/stale/partial/unavailable/failure states.
- Case workbench contains typed fields/provenance, parties, documents/images, vehicle/MOT suggestions, address, tasks/chasers/file request, EVA export, report evidence, `Report preparation` work, lifecycle/history, immutable identity, lease/conflict/retry, and reasons. No case/file permanent delete.
- PageModels bind/authorize/translate only. Use approved design tokens, desktop/constrained-desktop/200%-zoom behavior, keyboard/focus/error semantics, forced colours, reduced motion, and no mobile/saved-view/bulk/calendar/`Next`/`unallocated` mailbox scope.
- Configure OpenIddict authorization code + S256 PKCE, exact resource/audience, short access/rotating refresh tokens, protected-resource metadata, local development keys, and one Streamable HTTP `/mcp`. Production refuses development keys.
- MCP exposes only case/intake/Triage/document/EVA/report actions already owned by Core. No accounts, roles, principals, configuration, OAuth clients, cloud actions, arbitrary custody IDs, generic email, or deletion. Prove browser/MCP parity, actor attribution, stale/lease outcomes, and immediate disable/role-change enforcement.

#### 10. Offline acceptance gate — stop here before live work

Run from a fresh clone/setup path and record exact results/limits:

1. `Invoke-Doctor -Profile Offline`, initialization, start/status/smoke/stop/reset, second parallel run, failed-run recovery, and no cloud credentials/login.
2. Provider-domain reference generation from the pinned `initial.xlsx`: exact
   source hash and A/E contract, immutable suffix-only package, 11 providers,
   16 associations, repeat-byte equality, no copied local part/full address,
   exact migration equality, idempotent fresh SQLite/LocalDB migrations, tuple
   and suffix fail-closed precedence, and monotonic synthetic `Next`/`unallocated` growth.
3. Genuine local evaluator cohort + untouched holdout for every route selected
   for activation. Provider-domain presence alone is not a route disposition or
   policy. Exercise QDOS, direct/intermediary conflicts, malformed forwards,
   Triage positives/negatives/ambiguities/replies, automatic report candidates,
   human correction, repeat-byte determinism, and no source/corpus mutation.
4. Actual Web + actual Functions host + Azurite + LocalDB + local
   mailbox/custody smoke for QDOS Inspection, standalone Audit repairable/total
   loss, and Inspection + Audit. Assert one immutable
   case/reference/evaluation/custody/outbox result under duplicate/retry/crash.
   Inspection-location/default behavior remains deferred under `DATA-02` and is
   not inferred from the provider-domain package.
5. Negative/recovery smoke: unsupported/corrupt/oversized/incomplete, unknown/non-QDOS, route overlap, missing Audit assessment, dependency unavailable, hash conflict, sequence 999, poison exhaustion, stale lease/version, unauthorized actor, and terminal external failure all preserve pre-case and identity invariants.
6. Full local lifecycle through Not ready/chase/Held/Review/Report preparation, custody, selected VRM/address/vehicle suggestions, EVA once-only event, exact local Sent evidence, Post report, terminal outcomes, valid reopen, Created-in-error replacement, archive/read-only, and Triage exact-reply completion/correction.
7. Identity and local OAuth/MCP through real HTTP endpoints: seven-character password rejected, eight-character composition-free password accepted, repeated failures never persist account lockout, per-IP/global rate partitions return generic `429`/`Retry-After`, two-hour idle and eight-hour absolute expiry are clock-tested, disabled/role-changed users are rejected, and the public PKCE client completes actual MCP calls. No direct service invocation counts as caller proof.
8. Playwright visual/browser acceptance for Operations, evaluator, intake, Triage, case, admin, auth, and MCP-visible effects at 1280+, constrained desktop, 200% zoom, keyboard-only, focus/errors, forced colours, reduced motion, and multi-session lease/conflict behavior.
9. Canonical `pwsh ./scripts/Invoke-RepoCheck.ps1`, exact-head CI, independent implementation review, runbook-following by a clean operator/developer, and proof that no external hostname/client/credential or stale Azure resource was touched.

The offline gate passes only when every implemented live port has a contract-equivalent local implementation, every locally exercisable `Now` capability has caller-backed evidence, and every live-only capability is explicitly marked `offline implementation complete / live evidence pending` in the existing change record. Do not tag, release, deploy, or call this alpha accepted at this point.

#### 11. Acquire approvals and add live adapters after offline acceptance

- Obtain exact approved evidence/targets before client construction. Use official Microsoft Learn/API documentation and the Microsoft code-reference tooling for current .NET/Graph/Azure signatures; use LSP definitions/references/diagnostics for exported-symbol changes. Use Azure Skills/MCP best-practice and validation tools only within their authorization boundary. Record source URLs, versions, and limits; never copy tool names into product authority.
- **Graph/Exchange:** `Invoke-Doctor -Profile Cloud` must find `ExchangeOnlineManagement` `3.10.0`. Under exact tenant/mailbox approval, register the app service-principal pointer in Exchange Online and assign only scoped `Application Mail.Read` through Application RBAC for `instructions@collisionengineers.co.uk`; do not also grant an unscoped Entra `Mail.Read` application permission because authorization sources are additive. `Test-ServicePrincipalAuthorization` must report the role in-scope for that mailbox and out-of-scope for one approved control mailbox after propagation. Exchange scope is mailbox-level, so the .NET Graph adapter and its contract tests enforce the narrower Inbox-ingestion and Sent-Items-evidence allowlist. Permit MIME/attachment reads only—no move/delete/mark/category/send—using immutable IDs, a durable delta cursor, and bounded throttling/retry. Pass the local mailbox port suite plus approved live permitted/denied mailbox and folder fixtures.
- **Box:** exact approved enterprise/user/root descriptor and operations; guard root/descendant scope before every SDK call; persist remote identities/hashes/versions; no destructive delete or arbitrary ID; custody failure blocks progression but never rolls back identity. Pass local parity plus one approved permitted and one denied live fixture.
- **DVLA/DVSA:** accepted provider/API, licence, fields, credentials, limits, errors, target, and mileage rule; suggestions only. Pass replay parity and approved live evidence.
- **VRM:** activate only the already selected engine; if live/external, obtain separate image egress/security/cost approval and rerun the same cohort/holdout.
- **Graph/Box/vehicle secrets:** approved secret boundary only; no source, local settings, deployment output, prompts, telemetry, or business history.
- Enable one live dependency at a time in an approved Development deployment; a failure cannot route to the local adapter or silently degrade to success.

#### 12. Reconcile Azure and tear down the stale deployment

- After the offline gate, obtain explicit read approval for the exact subscription and resource groups, then refresh `docs/azure/current-inventory.md` by resource ID. Classify every resource and dependency as predecessor-only, shared, data-bearing/undecided, or `Next`/`unallocated`; names/tags are not ownership proof and no secret values or application data are read.
- Publish `docs/runbooks/predecessor-teardown.md` before cloud execution and open one separate linked teardown change record for the destructive operation. The runbook produces a reviewed exact-resource evidence artifact, never a wildcard/computed delete list or second repository status ledger. Every read, stop, credential change, and deletion remains separately approval-gated to the listed IDs.
- Preflight records the maximum retained traffic window (at least 30 days where telemetry exists) for public endpoints, Functions, queues, schedules, DNS, and downstream callers; checks locks, policies, backups, managed-resource ownership, role assignments, and cross-resource dependencies; and proves the user-confirmed no-active-use claim. Missing telemetry is an explicit risk in the approval, not inferred zero use.
- Record rebuild provenance before deletion: predecessor source/IaC/package location and revision, deployment history/template, package hashes where retrievable, non-secret configuration names, domains/certificates, identity and RBAC shape, and required secret names/issuers. Recovery is a fresh redeployment from this evidence; there is no restart/cooldown window and no promise to restore predecessor application state.
- Give every data-bearing or potentially shared asset an explicit `delete`, `retain in place`, or `move/replace then delete` disposition. The accepted fresh-`Next`/`unallocated` decision permits deletion of predecessor PostgreSQL case/queue state, but it does not silently authorize capture/evidence storage, Foundry, shared ACR/ValuationBot images, default workspace, Visual Studio accounts, or any other undecided asset.
- After exact write approval, fence ingress and schedules, stop callers, resolve queued work according to the approved disposable-state decision, revoke predecessor credentials/role assignments, and delete small dependency-ordered leaf batches: event subscriptions/webhooks and compute callers; app-specific endpoints/compute/plans; approved data stores; app-specific monitoring/alerts; private/network attachments; then identities and residual role assignments. Verify absence and business/platform health after every batch before continuing.
- Managed child resources are removed only through their owning service. Delete `rg-collisionspike-dev` or the OCR managed child group only as the final separately approved action when the reviewed manifest proves the group contains no retained/shared/undecided resource; otherwise leave the group with only its explicitly retained assets. Never start with resource-group deletion.
- Post-teardown verification re-runs Resource Graph, role assignments, DNS/endpoints, Key Vault secret-name/expiry inventory, managed identities/service principals, scheduled/event sources, orphan network/storage/monitoring resources, and cost views. Record deleted IDs, retained IDs with owners, failures/retries, irrecoverable state, and the rebuild procedure in the one teardown change record.
- Select a distinct `Next`/`unallocated` Development target and naming boundary. Update `.azure/deployment-plan.md`, existing Bicep modules, parameters, and `azure.yaml` only after the refreshed inventory and teardown disposition. Bicep what-if must show no mutation of retained predecessor/shared assets or any unapproved resource.
- Remove Document Intelligence and its roles/configuration from the new `0.0.0-development`/`0.1.0-alpha.1` output. Configure Azure SQL Entra-only with distinct deployment/migrator/Web/Worker identities; Web/Worker have no DDL, deployment has no standing app-data role, Web Blob access is only `intake-temporary`, and Worker receives only justified host/business-storage roles.
- Build immutable Web, Worker, and Linux-x64 migration bundles once with a machine-readable release manifest containing source revision, package/tool provenance, paths, and SHA-256. Shared Development deploy consumes those bytes without rebuild; an authorized migrator applies schema before app packages.
- After exact-target write approval, run policy/quota checks and Bicep what-if, deploy isolated Development, enable dependencies incrementally, and smoke identity, intake, Functions queue, Blob denial, SQL, Graph/Box/vehicle scopes, health, alerts, recovery, restore, and compatible package rollback. Prove Azure SQL 15-minute RPO/four-hour RTO in an approved temporary target; Production remains a later exact-target approval.

#### 13. Live acceptance and release

- Run approved live Development smokes, then Alex/relevant staff perform the genuine QDOS operator journey and management approves production release. Record what local, live adapter, deployment, and operator evidence each proves and cannot prove.
- Run canonical full checks, green exact-head CI, and independent exact-head implementation review with no unresolved blocker/required finding. Then set the change record accepted, close issue #3, tag `0.1.0-alpha.1`, and perform production deployment/cutover only under its separate exact-target approval. Never merge from the agent workflow.

## Critical files & anchors

- `docs/changes/2026-07-27-qdos-alpha-reference-corpora.md`: one delivery record, 127-capability evidence matrix, status, blockers, approvals, and outcome.
- `docs/product/capabilities.md`, `docs/product/qdos-alpha-gap.md`, and `docs/roadmap.md`: current `0.1.0-alpha.1` allocation and the explicit `DATA-02` deferral; no capability may disappear because live evidence is pending.
- `docs/operator-notes/business-process/intake-and-work-instructions.md`, `inspection-address.md`, `case-types-and-references.md`, and `case-lifecycle.md`: business wording and fail-closed invariants.
- `docs/decisions/0011-separate-direct-provider-and-intermediary-email-policies.md` plus the superseding tool-neutral ADR: route identity and repository workflow decisions.
- `src/Pegasus.Core/Intake/IntakeContracts.cs`, `ProcessIntake.cs`, and `QdosInstructionExtractionPolicy.cs`: preserve the real Core caller/policy seam while splitting durable operations and adding route/default provenance.
- `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`, `DependencyInjection.cs`, and the one existing migration stream: all reference, identity, intake, case, history, lease, and outbox persistence.
- `src/Pegasus.Infrastructure/Persistence/ReferenceData/`: immutable cumulative provider-domain packages only; no manifest, full address, location/default data, or workbook access.
- `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs`: retained source parsing and forwarded-message evidence, not route policy.
- `src/Pegasus.Web/Program.cs`, Razor Pages/layout/assets, and `design/product/ui-spec.md` plus `design/brand/logos/logo_no_margin.png`: authenticated Operations-first caller and graphical evaluator.
- `src/Pegasus.Worker/Program.cs`: actual timer/queue callers against the same Core use cases.
- `scripts/Build-ProviderReferenceData.ps1` and `scripts/reference_data/build_provider_reference_data.py`: dependency-free suffix-only provider package authoring; `Invoke-Doctor.ps1`, `Initialize-LocalDevelopment.ps1`, `Invoke-LocalDevelopment.ps1`, and `Invoke-LivePreflight.ps1` retain their separate setup/orchestration/live-preflight roles.
- `docs/reference/workproviders-and-repairers/initial.xlsx`: immutable current Step 2 evidence only; later growth uses new cumulative immutable workbooks. Application runtime never reads a workbook.
- `docs/runbooks/developer-workstation.md`, `local-development.md`, `testing/local-testing.md`, and `predecessor-teardown.md`: operator-executable setup, proof limits, and destructive-operation safety.
- `docs/azure/current-inventory.md`, `docs/azure/replacement-and-retirement-plan.md`, `.azure/deployment-plan.md`, `infra/`, and `azure.yaml`: dated evidence and post-offline live changes only.
- Existing Core, Integration, Architecture, and Web test projects remain the test owners; add no test/runtime project.
- Provider-domain authoring uses only Python 3.11+ standard-library APIs. Microsoft and Azure API choices for later delivery remain grounded through their official documentation and separately approved live-work tooling.

## Verification

1. **Fresh offline setup:** on a clean working copy, run `Invoke-Doctor -Profile Offline`, `Initialize-LocalDevelopment`, then `Invoke-LocalDevelopment` `Start`, `Status`, `Smoke`, `Stop`, and ownership-safe `Reset`; repeat with two parallel run IDs and one induced startup failure.
2. **Provider-domain reference proof:** run `Build-ProviderReferenceData.ps1` and `-Verify`; assert the pinned source metadata, 11 providers, 16 suffix associations, suffix-only strings, exact package hash, append-only synthetic-`Next`/`unallocated` behavior, embedded-resource/migration equality, idempotent migrations, and deterministic exact-version catalog outcomes.
3. **Behavioral proof:** execute the Step 10 evaluator, actual Web/Functions/Azurite/LocalDB, identity/session, MCP, QDOS type, lifecycle, Triage, custody, EVA, concurrency, negative, retry, and recovery scenarios. Each check must observe the real caller and persisted/operator-visible result.
4. **UI proof:** drive the running Razor Pages application with Playwright at the specified desktop/constrained/200%-zoom/accessibility/multi-session states; screenshots alone do not replace interaction and persisted-result evidence.
5. **Repository proof:** run `pwsh ./scripts/Invoke-RepoCheck.ps1`, exact-head CI, and an independent exact-head implementation review. The change record maps each of the 127 `Now` capabilities to local proof, live proof pending, or the named unsatisfied release blocker.
6. **Approved live proof:** after the offline gate and exact approvals, run `Invoke-Doctor -Profile Cloud`, the permitted/denied Exchange scope preflight, every live adapter contract/smoke pair, Bicep validation/what-if, deployment/health/restore checks, and genuine operator journeys. Local parity never substitutes for live scope or delivery evidence.
7. **Teardown proof:** before deletion, compare the reviewed manifest to fresh exact IDs and capture no-active-use/rebuild/data-disposition evidence; after each batch and at completion, prove deleted IDs are absent, retained IDs still have owners, no caller/role/DNS/scheduled/orphan/cost path remains, and redeployment instructions identify exact source/package/configuration provenance.

## Assumptions & contingencies

Settled assumptions from this revision:

- `initial.xlsx` is the immutable `0.1.0-alpha.1` provider-domain source. Later growth uses a new cumulative immutable workbook and new package/migration version; it never edits `0.1.0-alpha.1`. The provider-domain package excludes inspection locations, defaults, and Case-ID mapping, and application runtime never reads a workbook.
- The authoring command rejects the selected workbook's exact sibling Office lock marker and an exclusive-read failure before Python discovery, source hashing/parsing, staging, or output work.
- Passwords have an eight-character minimum with no composition requirement or persistent account lockout. Login throttling is transient and IP/global, idle expiry is two hours, and original-session absolute expiry is eight hours.
- The predecessor deployment is not in active use and needs no restart window. That is current user direction, not live telemetry evidence; the runbook must verify it before deletion. Recovery means redeploying from recorded provenance, not retaining predecessor state.
- The current local module inventory contains SqlServer `22.4.5.1` but not `ExchangeOnlineManagement`; offline work proceeds without it, while Cloud preflight installs/checks `3.10.0` before any approved mailbox configuration or Graph activation.
- Cloud reads, teardown, Azure writes, credential/RBAC changes, live service calls, deployment, and cutover remain exact-target approval gates even though they are sequenced here.

Release cannot be called complete while any item remains absent:

- explicit implementation activation of issue #3;
- green, documented offline development acceptance gate from a clean setup;
- exact source/hash/package/migration/suffix-only proof for the immutable `0.1.0-alpha.1` provider-domain snapshot: 11 provider codes, 16 suffix associations, and no retained local part or full address;
- executable evidence and explicit approval for every provider/intermediary route selected for activation; provider-domain presence never substitutes for a route disposition;
- accepted Triage and report predicates with genuine holdout results;
- selected/approved VRM engine with representative accuracy and false-positive evidence;
- accepted DVLA/DVSA contract/licence/target and mileage rule;
- accepted focused-`0.1.0-alpha.1` EVA mapping/readiness/image/recovery contract;
- approved Graph mailbox permission target, installed/pinned Exchange module, positive/negative Application RBAC scope proof, application-enforced Inbox/Sent allowlist, and exact Box identity/root/operations;
- refreshed approved Azure inventory, completed exact-resource teardown of predecessor-only application assets, explicit owners/dispositions for every retained/shared/data-bearing asset, and an isolated `Next`/`unallocated` target;
- green local/full/exact-head CI and independent review;
- separately approved live Development operations, operator/management acceptance, then production target/migration/deployment/cutover.

If a checkpoint cannot be satisfied, keep the corresponding live caller absent/disabled and the release blocked. Do not infer a rule, fabricate data, silently fall back to a local adapter, revive plugin-specific guidance, expose the stale deployment, or reduce the 127-capability contract.