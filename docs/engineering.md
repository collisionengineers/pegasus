# Engineering guidance

How repository work is done. Product behavior lives in
[requirements](prd/README.md), the roadmap in [capabilities](capabilities.md),
procedures in the [runbook](runbook.md), operational evidence in
[operations](operations.md), and current work on the Kanmer board (`.kanmer/`).
Authority order is defined once in the
[documentation index](index.md).

## Branches and delivery

- Task branches are cut from `dev` and merge into `dev` through a PR; `dev`
  merges into `main` through a PR as a merge commit; `main` is the active
  deployment and the sole revision eligible for an authorised one. `dev` and
  `main` are never rebased, reset, or force-pushed. Claim lines riding into
  `main`'s `NOW.md` at release are accepted cosmetics.
- Commit subjects are imperative and name a capability ID from
  [capabilities](capabilities.md) when one applies; otherwise they name the
  task.
- A durable decision that constrains future architecture gets an ADR under
  [docs/adr/](adr/README.md). Everything else is a commit message.
- Green means every `repository-check` job for the PR's head revision
  succeeded or was path-skipped. The executable CI behavior, path filters,
  lane selection, timeouts, and runner choices are defined and explained in
  [`.github/workflows/ci.yml`](../.github/workflows/ci.yml);
  [`.github/workflows/workspaces.yml`](../.github/workflows/workspaces.yml)
  separately gates imported workspace changes.

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
3. **Parser/adapter contracts** — EML/PDF/DOCX and later approved DOC/MSG handling; corruption, encryption, expansion/resource limits, cancellation, path/integrity safety, stable contract codes, and deterministic external failures.
4. **LocalDB persistence** — fresh and incompatible schemas, committed SQL Server migrations, rollback, state/action-history/outbox atomicity, reference allocation, constraints, pagination, leases, stale versions, concurrency, and backup/restore.
5. **Web/API/MCP caller** — actual routes reach Core; authentication, antiforgery, validation, scope, idempotency, exception translation, and action-history actor are observable.
6. **Functions/Azurite caller** — actual timer/queue trigger, Blob staging, identifier-only messages, duplicate/retry/poison/restart behavior, and delete-after-Box-confirmation.
7. **Browser/accessibility** — authenticated workflows, dashboard/queue agreement, two-session editing, keyboard, focus and error behavior, semantic labels, text-plus-colour states, 200% zoom, and supported-browser coverage. Automated axe results do not replace manual keyboard or assistive-technology review.
8. **Genuine corpus** — immutable reviewed cohort and untouched holdout through the real caller, including field-level accuracy, conflicts, unreadable pages, and false case/reference outcomes. Detailed evidence remains ignored and local.
9. **Security/observability** — role matrix, secure cookies, transient authentication throttling, request forgery, denial before client construction/call, dependency and dynamic scanning, correlation, health, redaction, and bounded failure metrics.
10. **Performance/concurrency** — eight concurrent operators, 2,000 cases per month, 2–20+ files per case, the one-file 10 MiB limit and 10 MiB-plus-64-KiB multipart envelope, burst/soak behavior, and 48,000–480,000+ annual asset-metadata shapes. Do not invent a release latency threshold without an explicit decision.
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
