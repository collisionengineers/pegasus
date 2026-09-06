# Engineering guidance

How repository work is done. Product behavior lives in
[requirements](prd/README.md), the roadmap in [capabilities](capabilities.md),
procedures in the [runbook](runbook.md), operational evidence in
[operations](operations.md), and current work on the Kanmer board (`.kanmer/`).
Authority order is defined once in the
[documentation index](index.md).

## Branches and delivery

- Task branches are cut from `dev` and merge into `dev` through a PR. `main`
  is the active deployment and the sole revision eligible for an authorised
  release. `dev` and `main` are never rebased, reset, or force-pushed. Claims
  and execution evidence live in the owning Kanmer ticket.
- Promote `dev` to `main` only as an exact-SHA fast-forward: fetch both remote
  refs, confirm `git merge-base --is-ancestor origin/main origin/dev`, record
  the reviewed `origin/dev` SHA, then atomically push that SHA to both
  `refs/heads/main` and `refs/heads/dev` with an explicit lease on `dev`:

  ```text
  git push --atomic --force-with-lease=refs/heads/dev:<reviewed-dev-sha> origin <reviewed-dev-sha>:refs/heads/main <reviewed-dev-sha>:refs/heads/dev
  ```

  The second refspec is a no-op only when `dev` still equals the reviewed SHA;
  the transaction rejects a concurrent change instead of partially promoting
  `main`. The lease is an expected-value assertion, not permission to rewrite:
  neither shared ref may be rewritten. Fetch again and require both remote
  heads to equal the recorded SHA. The release actor needs explicit `MERGE AUTH
  GRANTED` before the push. A failed preflight, rejected transaction, or
  unequal read-back stops the release; it is never repaired by a rebase, reset,
  or force push.
- A GitHub PR merge, rebase merge, or squash merge is not an exact-SHA
  promotion and does not replace that procedure. GitHub protection and
  rulesets are intentionally out of scope on subscription grounds, so the
  main-push CI check is detective rather than a server-side prevention.
- One transition only: after DELIV-002's PR reaches `dev` with green CI,
  DELIV-003 may merge `origin/main` into its own branch cut from `origin/dev`
  and deliver it through the normal reviewed PR to `dev`. It must not update
  `dev` directly, and the exception expires when that PR merges. Thereafter
  no routine `main` → `dev` synchronization merge is permitted.
- One repair transition only: DELIV-046 may merge the authorised
  `origin/main` head `32f8679d3695e0dcab8f310a1c20f8b129d20190` into its
  own branch cut from `origin/dev`, then deliver it through a merge-commit PR
  to `dev`. The exception preserves the two main-only commits and expires when
  that PR merges. It grants no direct shared-ref update, squash, rebase, reset,
  force-push, or later `main` → `dev` synchronization.
- Commit subjects are imperative and name a capability ID from
  [capabilities](capabilities.md) when one applies; otherwise they name the
  task.
- A durable decision that constrains future architecture gets an ADR under
  [docs/adr/](adr/README.md). Everything else is a commit message.
- Green means every `repository-check` job for the PR's head revision
  succeeded or was path-skipped. The executable CI behavior, path filters,
  lane selection, timeouts, and runner choices are defined and explained in
  [`.github/workflows/ci.yml`](../.github/workflows/ci.yml). No separate
  workspace CI lane remains: both imported source workspaces were integrated
  and retired under ADR-0025 (see [workspaces](../workspaces/README.md)).

## Markdown convention

- The H1 is line 1 of the file; a blank line precedes every heading.
- Tables use the compact delimiter row `| --- |` without padded alignment.
- Prose in root and `docs/` guidance files is hard-wrapped near 78 columns;
  table rows and link-dense lines may run long.
- The [documentation index](index.md#new-markdown-files) owns where new
  Markdown files may be created.

## Evidence

Prove the actual caller — a registration, a file, a green build, a deployment,
and an accepted feature are different claims. Never collapse them into
"done": name what was traversed and what remains unproved. A green test written
from the same mistaken interpretation as the implementation proves only
self-consistency; material business rules get an independent literal comparison
against the authoritative rule.

### Required evidence tiers

For each delivered capability, identify the authoritative rule, Core policy owner, real production entry point, persisted result, adapter or side effect, operator-visible result, and applicable tier.

1. **Static/build/architecture** — compile the four approved projects, enforce dependency direction and one policy owner, compile Bicep, inspect dependencies, and prevent tracked corpus or secret material. This proves consistency only.
2. **Core/domain** — positive, contradictory, ambiguous, and failure cases for intake, references, matching, lifecycle, roles, completeness, and case invariants.
3. **Parser/adapter contracts** — EML/PDF/DOCX/DOC/MSG handling; corruption, encryption, expansion/resource limits, cancellation, path/integrity safety, stable contract codes, and deterministic external failures. Format recognition, deployed parser capability, OCR activation and genuine-sample acceptance remain separate claims.
4. **LocalDB persistence** — fresh and incompatible schemas, committed SQL Server migrations, rollback, state/action-history/outbox atomicity, reference allocation, constraints, pagination, leases, stale versions, concurrency, and backup/restore.
5. **Web/API/MCP caller** — actual routes reach Core; authentication, antiforgery, validation, scope, idempotency, exception translation, and action-history actor are observable.
6. **Functions/Azurite caller** — actual timer/queue trigger, Blob staging, identifier-only messages, duplicate/retry/poison/restart behavior, and delete-after-Box-confirmation.
7. **Browser/accessibility** — the package-pinned Playwright Chromium lane
   drives authenticated workflows, dashboard/queue agreement, two-session
   editing, keyboard, focus and error behavior, semantic labels,
   text-plus-colour states, 200%-equivalent reflow, forced colours, reduced
   motion, and axe rules. It is the selected release evidence for those named
   checks. It does not prove screen-reader interoperability, complete WCAG
   conformance, subjective usability, or operator acceptance.
8. **Genuine corpus** — immutable reviewed cohort and untouched holdout through the real caller, including field-level accuracy, conflicts, unreadable pages, and false case/reference outcomes. Detailed evidence remains ignored and local.
9. **Security/observability** — role matrix, secure cookies, transient authentication throttling, request forgery, denial before client construction/call, dependency and dynamic scanning, correlation, health, redaction, and bounded failure metrics.
10. **Performance/concurrency** — eight concurrent operators, 2–20+ files per case, the 2,000-cases-per-month cohort and soak tier (**not run by this programme and never represented as passing** — D27, 2026-09-01; its evidence spike `PLAT-066` sits outside EPIC-011, while per-ticket concurrency tests still run), the current 10 MiB manual-upload bound and future intake bounds only after `INTK-052` research and operator decision (the Provider API envelope stays 30 MB and is owned by [FRD-09](frd/frd-09-provider-and-intermediary-routes.md#provider-api-principal-and-contract-boundary)), burst/soak behavior, and 48,000–480,000+ annual asset-metadata shapes. Do not invent a release latency threshold without an explicit decision.
11. **Migration/recovery** — every supported prior schema, idempotent migration scripts, previous-artifact compatibility, restore into a new database, and reconciliation by stable Outlook/Box identities.
12. **Integrated workflow** — authenticated source receipt through Core, SQL/outbox, actual Worker trigger, adapter outcome, persisted operator view, telemetry, and safe replay. Registration or mock-only paths do not satisfy this tier.

Run policy tests first, adapter contracts second, persistence/transaction tests third, actual HTTP/Functions caller tests fourth, genuine cohort/holdout evidence where relevant, then separately approved live-service and operator-acceptance gates.

## Engineering invariants

Topology and accepted boundaries are owned by [architecture](current-architecture.md).

### One Core owner

- Every business policy belongs to one named Core use case or query; Web and
  Worker translate requests or events and orchestrate only their own boundary.
- A business rule, classifier, allocator, parser, workflow transition, or
  external effect has one implementation. Shared code is consumed through
  project references, never by copying source.
- On encountering a third implementation, stop and consolidate; migrate or
  delete the replaced code, registrations, tests, and documentation in the same
  slice.

### Capability organization

Organize by business capability using Collision Engineers' business language.
No horizontal `Common`, `Helpers`, `Utilities`, or undifferentiated `Services`
folders; `V2`, `New`, `Manager`, `Helper`, or `Util` do not justify another
layer. `Audit` and `Triage` keep their reserved business meanings.

### Abstractions and deferred capabilities

Add an interface only for a real external boundary, a second concrete caller,
or an accepted ADR. A deferred capability belongs in
[capabilities](capabilities.md) or [open decisions](open-decisions.md) — never
as dormant registration, an unused endpoint, a disabled flag, or dark
destructive code. Anything built but unwired for two weeks gains a real caller
or is deleted; a dangerous superseded capability is deleted immediately.

### Classifiers and failure semantics

- Classifier and extraction precedence is explicit, ordered, and covered by
  contradiction tests; re-derive the complete precedence model whenever a rule
  is added.
- Every external client and catch path distinguishes `terminal`, `transient`,
  and `unknown`; terminal outcomes park the work and stop retries; exceptions
  are never converted into business truth.
- Metrics count successful effects, not attempts; a zero-error signal is
  meaningful only beside a heartbeat proving work occurred.

## Simplicity

The [simplicity rails](../AGENTS.md#simplicity-rails) in `AGENTS.md` are the
rules; these are the mechanics.

### The four lenses

Run each over the branch's own diff before the PR opens; each answers one
question and returns `file:line`, the concrete cost, and the concrete
alternative:

| Lens | Question | Typical find |
| --- | --- | --- |
| Reuse | Does the codebase already have this? | a second inner-exception unwrapper; a hand-rolled page header beside `_PageHeader`; a third copy of a test fake |
| Simplification | What does the diff add that nothing reads, or could be plainer? | enum values with no reader; a forwarder whose only reason left; a `?? default` hiding which path names a value; nested ternaries |
| Efficiency | What work is repeated or blocking? | two round-trips one correlated subquery would do; sequential independent I/O; a fixed 2 s reload against a 60 s dispatcher; blocking work on startup or a hot path; a long-lived closure capturing a large scope (prefer a class or record copying only the fields it needs) |
| Altitude | Is this a special case bolted onto a shared mechanism? | a result record carrying an `Exception` to a composition root; Core matching BCL exception types instead of adapters naming faults |

Findings are recorded in the ticket plan with a disposition each — applied,
skipped (see below), or deferred to a named ticket. Nothing evaporates.

### Skip rules

A finding is skipped, and the skip recorded, when its fix would (a) change
intended behaviour, (b) require changes well outside the reviewed diff, or
(c) is a false positive on inspection. "Skipped — behaviour change, see
INTK-00x" beats silence. The pass hunts quality, not bugs; a suspected bug goes
to review.

### Balance

Never trade clarity for compactness. Prefer explicit code; avoid nested
ternaries and clever one-liners; keep abstractions that improve organisation;
do not combine unrelated concerns into one function or component; do not
remove a name, a type, or a step a reader relies on. Comments that narrate
what the code visibly does are removed; comments that carry a reason stay.
Only refinements that change how a reader understands the code are called out
in the report; the rest is just the diff.

### Scope and timing

The pass runs over the code the branch changed and its immediate
surroundings, proactively — right after the code is written and before the PR
opens — not over the whole repository and not as a later review stage.

### Fault handling shape

- Adapters name faults (`IntakeDependencyUnavailableException`); Core matches
  intake types, plus BCL types only where no adapter sits in between.
- One classifier per decision, looking through `InnerException` — EF wraps a
  SQL deadlock in `DbUpdateException`, and a store's retry helper rethrows the
  last attempt.
- The catch-all is the shared safety policy
  (`IntakeExceptionPolicy.IsRecoverable`), never a local
  `is not OperationCanceledException`.
- Persist a terminal state before rethrowing an unexpected fault, so the host
  logs it in full and a redelivery finds the work settled; do not swallow it
  into a bounded outcome or carry an `Exception` in a Core result. The
  operator-visible behaviour (a failed item reads as failed) is owned by the
  FRD — for queued intake, [FRD-02](frd/frd-02-intake-and-source-identity.md).

### Test support

One fake per concept, in the shared driver, `internal`; one helper for each
composition fact tests must repeat ("Web does not register the processor" →
`IntakeWebDriver.CreateProcessor`); one drain loop. A fake or helper copied
into a second test file is the third-copy rule applied to tests.

### Case Workspace v2 fixture values (D43)

Test fixtures and snapshot states may use the Case Workspace v2 mockup's
corpus-derived fixture values (`Pegasus_UI_v2_src/src/04-fixtures.js`).
These values include real claimant names and telephone numbers. D43
(2026-09-02) supersedes the EPIC-011 rule that the prototype's fixture data
is not domain data for this fixture set only. `corpus/` stays local, ignored
and immutable, and no corpus file is committed, renamed or modified.

### Plan sizing

A plan states its diff estimate first. Six real steps beat thirteen procedural
ones; a step that only re-runs what CI runs, or re-checks what `git diff`
shows, is deleted. Research separates verified facts (read-only checks, with
the command) from assumptions; an assumption a five-minute query would settle
is run, not defended.

## Destructive operations

Before any wipe, drop, purge, rebuild, migrate, replay, or bulk update:
enumerate exact targets, rehearse read-only, verify the baseline under the
correct identity and role (row-level security once made a live database look
wiped), prove the recovery source is complete, obtain the required approval,
and stop if observations differ from the plan.

## Lessons from the predecessor

CollisionSpike (2,039 process/doc files vs 1,173 product files, a 128,427-line
generated ledger, ~20 CI gates, and a first live email that failed within four
hours) is failure evidence, not a source tree. The rules above compress what it
demonstrated:

| Demonstrated failure | Rule |
| --- | --- |
| First real forwarded email misclassified; no case minted | Exercise genuine traffic through the actual caller before claiming completion |
| Sender identity and filenames outranked stronger content evidence | Explicit, re-derived precedence with contradiction tests |
| Rebuilt engine registered with no caller; fixture `From:` lines decorative | Registration and idealized fixtures are not caller proof |
| Nine token-mint paths, four HTTP wrappers, three Box-folder implementations | Search first; stop at the third copy |
| Implementer swapped mapping values and wrote tests asserting the swap | Independent review of literal business values |
| Guards encoded defects as allowed divergence; never watched to fail | A guard that has never fired is deleted |
| Repo reset silently reverted five tables while checks stayed green | Broad cleanups get adversarial exact-base/head review |
| Planned wipe-and-replay would have destroyed ~150 cases; dry run caught it | Rehearse destructive work read-only and prove recovery first |
| One bad Box folder reference produced 1,896 exceptions in a day | Classify failures at the client boundary; park poison work visibly |
| ~30 consecutive governance PRs while the intake engine stayed untrusted | Process is not a product; delete controls whose triggers never occur |
| 17-ticket misclassification wave found via operator screenshots, not CI | Weekly human review of real operator-visible output |

## v1 shared development contracts

PLAT-075 owns the common Foundation commit consumed unchanged by CASE-047 and
INTK-060. This is an incomplete development checkpoint, not runtime or release
evidence. The owner-ticket plan records the exact Foundation SHA and subsequent
shared corrections. The C# definitions are the signature authority:

| Consumer | Contract owner and signature |
| --- | --- |
| Accounts and Case editing | `IClearCaseEditLease.ExecuteAsync(ClearCaseEditLeaseRequest, CancellationToken)` validates an Administrator, selected Case, expected staff holder and independent lease generation; returns the retained clearance result on exact replay. |
| B Glass's repair estimates | `IPerUserExternalCredentialReader.GetEnabledAsync(ActionActor, ExternalCredentialProvider, CancellationToken)` returns server-only enabled credential material for that actor; administrative status never includes its password. |
| B generated and imported artifacts; C retained sources | `ICaseArtifactCustody.RetainAsync(CaseArtifactCustodyRequest, CancellationToken)` accepts one Case or receipt destination and returns confirmed logical identity or an explicit pending/failed/unknown result. |
| B/C logical reads and A downloads | `IReadLogicalDocumentVersion.OpenAsync(ReadLogicalDocumentVersionRequest, CancellationToken)` selects exactly one complete document/version pair or retained intake asset; Case and receipt contexts are checked against persisted associations. Storage addresses never come from callers. |
| C correspondence | `IStaffMailSend.SendAsync(StaffMailSendCommand, CancellationToken)` owns the single mail operation and its server-computed payload hash. |
| B report delivery | `IStaffReportSend.SendAsync(StaffReportSendCommand, CancellationToken)` uses the same mail operation; B's `IReportSendReadiness.RequireReadyAsync` rechecks persisted versions and exact artifacts at the side effect. |
| B source adoption | `ISourceCandidateQueries` and `IThirdPartyReportCandidateQueries` return C-owned source observations. Typed report leaves retain raw value, unit, currency, document/version/hash and locator; B alone accepts CE findings. |
| B location selection | `IInspectionLocationChoices.SearchAsync` returns a bounded choice with postcode, source kind, source record and version. A location never implies CE attendance. |
| B raw estimate import via Web/MCP | `IImportRawEstimate.ExecuteAsync` accepts logical source identity, SHA-256, typed actor, expected Case version, lease and operation key. |

The mail state vocabulary is `StaffMailState`; attempt stages use
`StaffMailAttemptStage`. Submitted means provider acceptance. Only matching
observed Sent evidence establishes Sent. Recovery projections contain safe
state, attempt time, mailbox generation, payload hash and expiry; upload URLs,
credential material and provider-session secrets stay inside the adapter.

Foundation itself registered the administrative lease implementation. The
Stream A source now adds the account/credential, mailbox, staff-mail,
logical-content/cache and administration query implementations. B/C domain
implementations and their A-authored composition patches arrive together on
the owning branches. The combined checkout must prove those callers;
contracts and registrations alone do not establish a working user journey.
