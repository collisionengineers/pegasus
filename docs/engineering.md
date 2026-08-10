# Engineering workflow

How repository work is done. Product behavior lives in
[requirements](requirements.md), the roadmap in [capabilities](capabilities.md),
procedures in the [runbook](runbook.md), operational evidence in
[operations](operations.md), and current work in [`NOW.md`](../NOW.md).
Authority order is defined once in the
[documentation index](index.md).

## Branches and delivery

- Task branches are cut from `dev` and merge into `dev` through a PR; `dev`
  merges into `main` through a PR as a merge commit; `main` is the active
  deployment and the sole revision eligible for an authorised one. `dev` and
  `main` are never rebased, reset, or force-pushed. Claim lines riding into
  `main`'s `NOW.md` at release are accepted cosmetics.
- A task PR merges into `dev` when it is green and its plan review passed;
  the implementer may perform that merge. `MERGE AUTH GRANTED` from the
  operator is required only for `dev` → `main`. Both rules are honor-system:
  no branch protection enforces them ([ADR-0017](adr/0017-multi-agent-task-workflow.md)).
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

## Task workflow

Multiple agents work in parallel; each takes one task, works it in its own
worktree, and other agents see the claim. The claimable unit is a task line
in [`NOW.md`](../NOW.md) (see its rules footer for the format); the
authoritative copy of `NOW.md` is `origin/dev`'s after a fetch — never the
checkout you happen to be in.

1. **Take.** `git fetch origin`, read `origin/dev:NOW.md`. Skip any task
   whose capability IDs or files overlap an in-flight `Doing` line, and any
   slug colliding with an existing `task/*` branch or `docs/temp-plans/`
   file. Awareness on the same machine: `git worktree list` and
   `git branch --list 'task/*'`.
2. **Worktree.** `git worktree add ../pegasus-worktrees/<slug> -b
   task/<slug> origin/dev`.
3. **Claim.** In the worktree, commit the `NOW.md` claim line alone — move
   the task line into `Doing` with branch, date, and agent — then
   `git push origin HEAD:dev`. A rejected push means `dev` moved (a claim, a
   merge, or maintenance — not necessarily your task being taken): fetch,
   reset your worktree to `origin/dev` (discarding only your own unpushed
   claim commit), re-read `Doing`, and re-commit — the same line if still
   free, another task if not.
4. **Plan.** Write `docs/temp-plans/<slug>.md` on the task branch: what the
   task will change and how it will be verified
   ([contract](temp-plans/README.md)). Docs-only carve-out (ADR-0017
   addendum, 2026-08-06): a task whose every changed path in the final PR
   diff is a Markdown file outside `src/`, `tests/`, `infra/`, and
   `scripts/` skips the plan file. If the diff stops qualifying mid-work,
   write the plan before review.
5. **Work and PR.** Implement, verify locally, open the PR into `dev`. The
   PR's `NOW.md` edit removes its own claim line. If `NOW.md` conflicts,
   merge `origin/dev` into the task branch, take `dev`'s `NOW.md` wholesale,
   and reapply only your own line change.
6. **Review.** Before merge, an agent that did not implement the task
   answers two questions against the plan file on the PR: did the plan miss
   anything the task line implied, and did the implementation miss anything
   from the plan. For a docs-only task (no plan file), the two questions
   run against the PR diff and description instead: did the PR miss
   anything the task line implied, and did the diff change anything the
   task line did not authorise. Merge only after that review passes and CI
   is green.
7. **Release.** After merge: one maintenance push to `dev` deletes
   `docs/temp-plans/<slug>.md`, then remove the worktree and delete the
   branch. A docs-only task has no plan file, so its release step is just
   the worktree and branch cleanup. Abandoning instead: reset your
   worktree to fresh `origin/dev`, commit only your claim-line removal,
   and push it as a maintenance push —
   the plan file exists only on your task branch and dies with it — then
   delete branch and worktree; only the claiming agent abandons its own
   claim. Stale claims fall under `NOW.md`'s staleness ladder and are
   removable by anyone.

Git safety inside this workflow: never touch work that is not yours.
Allowed: discarding your own unpushed commits in your own task worktree,
merging `origin/dev` into your own task branch, merging your green and
reviewed task PR into `dev`, deleting your own merged `task/*` branch and
worktree, and maintenance pushes to `dev` limited to `NOW.md` task lines and
`docs/temp-plans/` deletions. Still banned: force-pushing anywhere,
rewriting `dev` or `main`, stash/reset/clean that touches anyone else's
work, and broadening staging beyond your task.

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

Topology and accepted boundaries are owned by [architecture](architecture.md).

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
