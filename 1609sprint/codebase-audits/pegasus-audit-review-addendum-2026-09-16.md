# Pegasus audit review — implementation addendum

Date: 16 September 2026

## Basis and limits

Reviewed inputs: `frontend-code-audit.md`, `codebase-audit..md`, and
`docs-audit.md`. This addendum preserves their finding IDs. Recommendations
below are proposals, not authorization to change source, delete files, alter
configuration, run tests, or deploy.

GitHub `dev` resolved to `9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396`.
That is the pinned revision used for the source checks in this review.
GitHub `main` resolved separately to
`8b9d358f71ac02363a3e9fa50549d4fef0fcdac8`.
The Pegasus MCP checkout reported `main` at
`32f8679d3695e0dcab8f310a1c20f8b129d20190`, with an explicit freshness
mismatch. Its attempted revision-pinned reads returned
`COMMAND_NOT_ADMITTED`; the substantive source checks therefore used GitHub.
The mixed U50-branch findings in the supplied codebase audit have not all been
independently reverified. No build, test suite, browser session, production
request, database inspection, or cloud change was performed for this review.

This is a targeted verification and improvement review, not a claim that every
reference count or every supplied finding has been independently reproduced.

## Overall decision

Retain the audits as useful evidence. Do not execute their complete `/simplify`
backlog unchanged. Separate confirmed defects, architectural hardening,
behaviour-changing decisions, performance hypotheses, and deletion candidates.

The desirable end state remains Pegasus's existing modular application:
Core owns business decisions and use cases; Infrastructure owns persistence and
external-service implementations; Web owns HTTP and presentation; hosts wire
these together. This review does not recommend a new framework, additional
application layer, generic repository, mediator, or distributed-services split.

## Corrections before implementation

| Original item | Correction |
| --- | --- |
| Frontend headline: JavaScript is clean | Narrow to what was actually established. The same audit reports a damage-summary discrepancy and a JavaScript-dependent settings submission. A static review is not a blanket security or accessibility certification. |
| H1: User can generate through any non-page route | The Core/Web authorization discrepancy is confirmed. A reachable alternate route was not established by this review. Distinguish missing inner-layer enforcement from a demonstrated external bypass. Split report generation from report delivery. |
| H2: scopes exist only in Web | `AutomationActorResolver` currently checks authentication, registration enabled state, required scope and grant identity. Retain those protections. The question is whether business permissions also need transport-independent enforcement, not whether MCP has no authorization. |
| H4: contact number/address never patchable | `contactPhoneNumber` is accepted and merged. The omitted fields are specifically `Claimant.ContactNumber` and `Claimant.Address`. Do not conflate claimant fields with contact fields. |
| H6: move to Core and add atomicity | The existing group workflow explicitly handles a committed prefix, operation-key replay, partial-failure messages and later reconciliation. Moving ownership can preserve that behaviour. All-or-nothing semantics are a separate decision. |
| H8–H10 all ranked HIGH | Separate runtime risk from file placement. HTTP response construction and application startup wiring legitimately belong in Web. A reusable adapter/factory can still improve maintainability and credential consistency. |
| Architecture tests cover only Core/project direction | `DependencyDirectionTests` also covers Web actor/operation-key ownership, custody page dependencies, Core policy ownership and adapter boundaries. Extend the actual gaps rather than describing the suite as empty. |
| S-07: mirror Export's NotFound handler | `ExportModel.OnGet` returns NotFound only for an empty ID; otherwise it redirects to the Case. Choose a deliberate GET contract for action pages rather than copying an inaccurate description. |
| Docs: performance is both planned and implemented | PR #764 was open and unmerged when read. Its head was `e4fc0ea05762aab67dd6ec3c8aaf35a280d5f2aa`. Label planning, implementation branch, merge, verification and deployment separately. Do not mark dev implemented merely because a PR is implemented. |
| Docs: index 1609sprint or move it | Indexing alone does not satisfy the current placement gate, whose allow-list excludes `1609sprint/`. Align the selected tracked location, routing and gate together. |
| Codebase introduction says none of the earlier audit was implemented | Section D itself identifies BL-06, BL-15 and BL-16 as fixed. Reconcile the introduction with the detailed status. |
| S-03 references a C9 list | There is no section C9 in the supplied report. Supply an explicit symbol/file manifest instead. |

## Required finding record

For each retained finding record its original ID; exact source SHA and full
repository-relative path; intended requirement and owning document; observed
behaviour; evidence status; affected callers and runtime profiles; proposed
change; behaviour-preserving or behaviour-changing classification; tests to
add; dependencies; and recovery implications.

Use evidence statuses such as `source-confirmed`, `runtime-reproduced`,
`test-passed-at-SHA`, `hypothesis`, `decision-required`, and
`not-independently-rechecked`. A named test class is a proposed verification
location, not a passing result.

## Recommended bounded work items

### R01 — Repair upload navigation

Original IDs: A1 / S-01. Priority: immediate confirmed defect.

Both upload-success paths construct `/Cases/Details/{id}`, while the Case page
is routed at `/Cases/{id:guid}`. Generate the URL through Razor page routing or
an existing Web URL-generation seam. Do not put Razor URL generation into Core.

Acceptance: an authenticated test follows each rendered success link, reaches
the expected Case identity and does not end on a login or status page. Cover
both direct receipt association and an image-intake Case merged into an
instruction Case. Do not merely assert a replacement string. Verify a non-root
PathBase where that deployment mode is supported.

### R02 — Make professional and automation write permissions explicit

Original IDs: H1–H3 and relevant M13. Priority: high, with policy decisions.

Specify the permission matrix before implementation. Treat report generation,
report preparation, delivery, signatory selection, estimate editing and
ordinary unconfirmed automation fields as separate operations. Do not assume
that all staff roles have professional authority, or that every automated
report-related workflow must be prohibited.

Keep OAuth token parsing, HTTP authentication and the immediate client-disable
check at ingress. If Core needs automation permissions, translate verified
claims into a small transport-independent authorization context. It must not
accept a client-supplied permission list as trusted authority. Preserve the
current per-request registration check rather than replacing it with a cached
scope set. Consider keeping transient permissions separate from the actor
identity persisted in business history.

Move generic-write restrictions on estimate/signatory fields to the shared
Core command policy without blocking their legitimate named commands. Preserve
normalization, expected version, lease and unconfirmed-value semantics.

Acceptance: parameterized role and actor tests; direct Core calls without a Web
wrapper; missing/wrong scope; disabled client; forged field paths; estimate and
signatory fields through generic versus named commands; and a denied request
that produces no rendering, retention, persistence or send side effect. Prove
reachable external bypasses separately before describing one as exploitable.

### R03 — Consolidate mailbox eligibility without activating Outlook writes

Original IDs: H5, M5, A2 / S-02. Priority: near-term operational correctness.

Triage's selected-mailbox predicate is less complete than Compose's. Centralize
shared readiness rules, but keep caller intent explicit: an originating
mailbox and the unique default mailbox are different selection requirements.
Apply the authoritative decision again at execution; a rendered button is not
permission. Return enough structured information to render the existing error
language without adding unnecessary explanation boxes to the UI.

Separately, gate the folder-move action on the composed mover's availability.
Do not register the Graph mover merely to make an unavailable button work. The
repository explicitly restricts external mutations during local alpha work.

Acceptance: no mutation is offered or executed when the mover is unavailable;
consistent handling of each missing mailbox prerequisite; zero/multiple
defaults; correct originating mailbox; and refusal after configuration changes
between display and submission.

### R04 — Align document-download policy across transports

Original ID: B5. Priority: high; missing from the ranked S backlog.

The MCP inline path uses `IDownloadCaseDocument`; its large-content fallback
returns a streaming URL whose endpoint uses metadata lookup and
`IReadLogicalDocumentVersion`. The latter already performs a required-scope
check and a Case/occurrence/version metadata query. It must not be described as
an unauthenticated endpoint.

Define one deliberate-download authorization and audit contract, with small
transport adapters for inline encoding and HTTP streaming. Preserve the
intentional difference between previews, deliberate downloads, an EVA Case
bundle and selective document export. In particular, do not make every
thumbnail or byte-range request create a separate business download event.

Acceptance: below/above-inline-threshold parity; direct calls to the fallback
URL; wrong Case, occurrence or version; revocation; safe filename and media
type; intended cache and nosniff behaviour; authorization before a conditional
response; bounded streaming; cancellation; disposal; and an explicit policy for
audit cardinality during retries and range requests. Also inspect global HTTP
middleware before asserting that a header is absent from the final response.

### R05 — Correct the two frontend contract discrepancies

Original IDs: M6 and M7. Priority: small, focused corrections.

Damage: Core derives `multiple` from more than one impact; Razor derives its
headline from distinct mapped locations. Decide whether these are the same
field or intentionally different summaries. If the same field, use a shared
semantic rule and equivalent client-preview fixtures. If different, name them
accordingly rather than presenting both as the same result.

Test zero, one and multiple impacts; multiple impacts mapping to one location;
different locations; severity ordering; and save/reload consistency. Do not
change professional meanings merely to remove duplicated code.

Principal report settings: the visible selects have no names and the posted
enum is hidden. Prefer one native, named authoritative field, with JavaScript
as an enhancement. Another valid implementation posts named inputs and maps
them server-side. Test every policy choice with JavaScript disabled as well as
normal script behaviour and invalid inputs.

### R06 — Move multi-step workflows to Core without accidental redesign

Original IDs: H4, H6, H7, M1, M2, D2. Priority: high-value structural work.

Create focused use cases for partial Case editing, grouped attachment,
reviewed-intake Case creation and retained estimate import where warranted.
Core owns sequencing and outcomes; Infrastructure implements persistence.
Presentation translates results into the existing page behaviour.

For partial Case edits, explicitly represent absent, clear and set. Specify
empty-string handling, field eligibility, post-merge pair validation and
confirmed/unconfirmed provenance. A null-clear contract is a deliberate change
from the current null-means-keep contract.

For grouped attachment, preserve exact replay identity, reviewed member
versions and known committed-prefix behaviour first. Do not silently refresh
an expected version to make a stale command succeed. Validate group membership,
selection identity and per-member eligibility server-side. Prefer returned
version/results over Web-owned arithmetic. Give partial completion a typed
result with completed and remaining members.

If the business requires atomic metadata attachment, separately design one
bounded database transaction and any durable external-work records. Do not
hold a database transaction open across Box, Graph, file rendering or queue
network calls, and do not imply SQL can roll back an external side effect.

Acceptance: fail before the first write, after each intermediate write and
after the last commit; retry the same request; change an actor or member
selection; lose a lease; introduce a concurrent writer; cancel a request; and
fail reconciliation. Expect no duplicate Case, link, report or reference.

### R07 — Batch known reads and measure the actual gain

Original IDs: S-09, S-10, D1–D8 and relevant older BL items.

`ActorDisplayNames.ResolveStaffNamesAsync` definitely performs one account read
per distinct staff ID. Add a bounded batch query, preserving disabled/deleted
account naming and missing-account fallbacks. It is a credible low-risk target,
not proof of the largest end-user latency saving.

Batch upload/group, list-row and workflow projections at their owning query
boundaries. Select needed fields, preserve deterministic ordering and avoid
unbounded joins or materialization. Do not replace sequential operations with
unbounded parallel database or provider calls. In particular, concurrent use of
one EF DbContext is unsupported.

Acceptance: count actual database commands for increasing fixture sizes, not
only calls on a fake interface. Record provider calls, bytes, memory and p50/p95
request timings as appropriate. Distinguish cold process, warm process, cache
hit/miss and real authenticated browser paths. Ordinary existing web tests alone
do not prove removal of N+1 queries.

Reconcile with PR #764 before touching frame activation, authentication cookies,
thumbnail caching, decode concurrency or request-timing instrumentation. The
PR's implementation and functional evidence do not establish measured candidate
performance gains on deployed Linux.

### R08 — Centralize SQL classification, not indiscriminate retrying

Original IDs: S-11 / E2. Priority: separate persistence work.

Extract shared recognition of SQL failure categories. Keep the operation's
response to a category explicit. A unique-key violation can be a successful
idempotent replay, a genuine competing business operation or invalid data; it
is not automatically transient. Distinguish deadlock, transient connection
failure, optimistic conflict and unknown commit outcome.

Avoid nested retry amplification. Preserve cancellation, bounded attempts and
business identity. Do not globally turn on retries without reviewing the manual
transaction boundaries and commit-unknown behaviour. A high transaction count
or use of Serializable isolation alone is not evidence of over-engineering.

Acceptance: classifier fixtures, same/different idempotency identities,
competing writers, deadlock recovery, cancellation and failure at commit. Ensure
external effects are not repeated just because a database retry runs again.

### R09 — Consolidate edit-lifecycle mechanics narrowly

Original IDs: A3, B7, S-08 and related M2.

Prefer a small shared helper/component and shared Razor partials where the
semantics really match. A focused base class can be appropriate, but avoid a
large configurable base that owns page-specific policy and numerous callbacks.

Specify one success/error contract. A 200/204 difference is not automatically a
user-visible failure if all clients accept both; prove the problematic cases.
Retain case-specific and administrative authorization differences.

Acceptance: expiry, takeover, concurrent tabs, old release-beacon arrival after
a new lease, navigation away, cancellation, antiforgery and script re-binding
following fragment replacement. Render and exercise the actual scripts rather
than relying exclusively on HTML string assertions.

### R10 — Admit deletions symbol by symbol

Original IDs: S-03 to S-06, C1–C6, E1 and B9.

Do not delete all candidate registrations, use cases, handlers and their tests
in one action. For each candidate establish its intended requirement, endpoint
reachability, all composition profiles, enumerable/hosted-service consumption,
factory or reflection resolution, dynamic markup and relevant active PRs.

Classify it as obsolete, intentionally dormant, not fully delivered, replaced,
or genuinely unused. Remove confirmed obsolete items without inventing
compatibility machinery for hypothetical consumers. If a requirement survives,
an unwired handler or use case is a delivery gap rather than disposable code.

Retain behavioural assertions or move them to the replacement; tests must not
be deleted solely to allow deletion to compile. A single implementation and no
handwritten test double do not make an interface useless. Collapse measured
pure-forwarding seams without erasing meaningful application contracts.

CSS/JS deletion requires rendered-state and script checks: normal/read-only,
edit/takeover, empty/error, lazy-loaded section, viewer/crop and responsive
states. Distinguish a dead launch hook from a dead viewer engine. Preserve the
approved logo, assets and desktop layout. Do not relabel this as a visual
redesign.

### R11 — Repair documentation routing, status and semantics

Original basis: docs audit; applicable C5/C6 and release documentation.

Use `docs/design/README.md` as the index-routed visual owner. Treat root
`design/planning-and-old-designs/` as historical reference. Label `.stitch` as a
derived tool-facing specification and link its owner; add a targeted token
consistency check only for values meant to be exact copies.

Keep current operational facts distinct from the release ledger. Correct the
standing Box-secret paragraph that still describes Container Apps, using the
accepted deployment record/current authorized observation. Do not delete
historical release facts because they describe retired infrastructure.

Remove or archive completed temporary documents only after checking all unique
requirements, decisions and inbound links. Move completed one-time cutover
instructions out of the normally loaded release path while preserving necessary
recovery constraints and provenance. A tiny agent-specific file that only
points to the canonical skill is not inherently a competing source of truth.

Reconcile `.codex/PLAN.md` and sprint PROGRESS records with PR #764 using
separate fields for implementation SHA, merge state, verification and release.
Pick a valid tracked home for durable work records or keep genuinely ephemeral
reports as artifacts with durable PR pointers. Update the index and placement
gate consistently; do not simply permit every root. Retire obsolete gate
exceptions when their corresponding locations are retired.

For Cazana/EVA evidence, preserve supplied-source fidelity. A byte-identical copy
can have one canonical home, but two different PDF transcriptions need a quality
and provenance comparison before either is discarded.

Repair escaped headings, duplicated passages and copied conversation material.
Use full paths and stable evidence IDs. Link existence checks cannot detect a
self-link with an inaccurate description or contradictory operational prose.

The transcript's `git ls-files --deleted` output is only a snapshot of missing
tracked files, not proof that no earlier deletion occurred. Do not carry that
stronger claim into the audit. A future read-only execution record needs its
actual action log and appropriately scoped before/after evidence, including
untracked material where relevant.

### R12 — Add post-commit audit-failure coverage

New source-derived risk, not runtime reproduced. Priority: reliability review.

`AutomationMcpAuditor.RecordAsync` runs the action, appends `Succeeded`, and
catches exceptions from either step in the same catch block. If an action has
committed but the success-history append fails, the method attempts a `Failed`
append and rethrows. A cancelled request can similarly affect the append.

Specify how command outcome and ingress-audit delivery are separated. Do not
pretend that a committed action failed and is safe to repeat with a fresh key.
Also do not silently lose a required audit event. Core's business history,
recoverable ingress attribution and operator-visible failure handling need a
consistent contract; use existing durable mechanisms before creating a new
outbox solely for this purpose.

Acceptance: simulate a committed action followed by audit-store failure and
cancellation; retry using the same business operation key; assert one mutation,
honest outcome reporting and detectable/recoverable audit failure. Preserve the
original error when the error-reporting append itself fails.

## Guardrails worth adding

Target behaviour and meaningful dependencies, not filenames alone.

- Pages and MCP command adapters must not perform database writes or implement
  multi-step business workflows directly; allow narrowly named composition and
  framework integration locations.
- Core must stay independent of HTTP, OAuth/OpenIddict, SDK and persistence
  types. Add actor/permission parity tests for sensitive commands.
- Prevent new direct provider clients and policy copies in handlers, while
  preserving legitimate Web-owned model binding, redirects, response headers,
  antiforgery, middleware and cursor protection.
- Use reviewed existing exceptions with an owner and reason; fail new violations
  rather than requiring an unrelated repository-wide cleanup first.
- Add actual navigation, mutation-negative-path, query-growth and routed-script
  tests. Do not replace them with broad source-string tests or file-length caps.

## Sequencing

First correct navigation and the narrowly defined display/form defects. Resolve
and enforce report/assessment/mailbox permission decisions, and address the
missing document-download parity work. Documentation status/gate repair can run
as a separate, non-overlapping change.

Next move workflow ownership with failure/replay tests and batch independently
confirmed reads, taking PR #764 overlap into account. Keep SQL retry work
separate. Consolidate edit mechanics after defining their shared contract.
Finally remove approved dead-code/asset candidates in small groups. Routine
adapter/factory relocation is lower priority unless it fixes a demonstrated
credential or reliability fault.

## Principal source evidence read in this review

Repository: `collisionengineers/pegasus`.
Unless stated otherwise, all paths below were read at
`9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396`.

| Source | Evidence used |
| --- | --- |
| `AGENTS.md`, lines 1–145 | Layer ownership, verification, alpha mutation restrictions, documentation placement. |
| `docs/index.md`, lines 1–225 requested | Canonical routing, temporary documents, source fidelity and authority. |
| `src/Pegasus.Core/Reports/CaseReportGeneration.cs`, 620–755 | GenerateCaseReport authorization and freeze/render/retain sequence. |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs`, 1850–1985 | Engineer-only Web report command guard. |
| `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs`, 395–580 | Generic field restrictions, nullable Case merge and claimant/contact distinction. |
| `src/Pegasus.Core/Assessment/AssessmentPolicy.cs`, 65–190 | Core writable-field gates and impact derivation. |
| `src/Pegasus.Web/Mcp/AutomationActorResolver.cs` | Current ingress authorization and audit append control flow. |
| `src/Pegasus.Web/Mcp/AutomationDocumentStreaming.cs` | Fallback stream authorization, metadata/content sequence and exports. |
| `src/Pegasus.Web/Mcp/DocumentMcpTools.cs`, 165–238 | Different small/large download paths. |
| `src/Pegasus.Web/Presentation/UploadOutcome.cs`, 185–250 | Two incorrectly constructed Case links. |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml`, 1–12 | Absolute Case route. |
| `src/Pegasus.Web/Presentation/UploadCaseDecision.cs`, 265–630 | Single/group links, version arithmetic, replay and partial recovery. |
| `src/Pegasus.Web/Pages/Mail/Compose.cshtml.cs`, 345–405 | Complete default-sender readiness predicate. |
| `src/Pegasus.Web/Pages/Triage/Details.cshtml.cs`, 995–1045 | Shorter originating-sender predicate. |
| `src/Pegasus.Core/Actors/ActorDisplayNames.cs` | Per-distinct-account reads and display fallbacks. |
| `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs`, especially 395–650 | Existing Web and policy-boundary tests beyond project direction. |
| `scripts/Test-MarkdownPlacement.ps1` | Added/copied/renamed Markdown path allow-list. |
| `src/Pegasus.Web/Pages/Administration/Contacts/Edit.cshtml`, 48–105 requested | Unnamed visible report-policy selectors and hidden posted enum. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseDamage.cshtml`, 35–70 | Distinct-location display derivation. |
| `src/Pegasus.Web/Pages/Cases/Documents/Export.cshtml.cs`, 1–130 | GET redirect, stale comments, separate EVA bundle semantics. |
| `src/Pegasus.Web/wwwroot/js/case-workspace.js`, 2105–2190 | Distinct tile hooks and viewer machinery; deletion needs fuller reachability evidence. |
| `docs/operations.md`, 510–550 | Stale standing Container Apps secret wording. |
| GitHub PR #764 metadata/body | Open/unmerged state, exact head, reported implementation and acknowledged performance-evidence gaps. |

External technical checks used Microsoft Learn's documentation titled
“Common web application architectures”, “Connection Resiliency — EF Core”,
“DbContext Lifetime, Configuration, and Initialization”, and
“Razor Pages architecture and concepts in ASP.NET Core”. These support the
composition-root exception, transaction/retry caveats and DbContext concurrency
restriction; they do not establish Pegasus's business requirements or prove
runtime behaviour.
