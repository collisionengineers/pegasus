# Engineering guidance

How repository work is done. Product behavior lives in
[requirements](prd/README.md), stable identities in [capabilities](capabilities.md),
procedures in the [runbook](runbook.md), operational evidence in
[operations](operations.md), and current work in the operator task and PRs.
Authority order is defined once in the
[documentation index](index.md).

## Delivery evidence

Integrate reviewed work into `dev`. The primary agent records the candidate SHA
and assigns one owner for heavy verification on the host.
CI routing and required jobs are defined in `.github/workflows/ci.yml`; exact-head
success or a justified path skip is evidence for that job only.

## Documentation authoring

Follow the [index](index.md#markdown-convention) for formatting and placement.

## Evidence

Prove the actual caller — a registration, a file, a green build, a deployment,
and an accepted feature are different claims. Never collapse them into
"done": name what was traversed and what remains unproved. A green test written
from the same mistaken interpretation as the implementation proves only
self-consistency; material business rules get an independent literal comparison
against the authoritative rule.

### Required evidence tiers

Choose evidence for the claim:

| Claim | Evidence |
| --- | --- |
| Policy behaves correctly | Focused meaningful tests against the governing FRD, including supported failures. |
| Adapter or persistence behavior works | Relevant boundary, authorization, concurrency and recovery evidence. |
| Feature is wired | A named application caller traverses the intended policy and adapter. |
| Artifact works in an environment | Exact artifact identity, configuration and observed runtime behavior. |
| Operator workflow is accepted | Explicit acceptance by its designated authority. |

Registration, a green build and deployment do not establish the next tier.
This table is not a sequence of universal gates for every documentation edit.

## Engineering invariants

Topology and accepted boundaries are owned by [architecture](current-architecture.md).

### One Core owner

- Every business policy belongs to one named Core use case or query; Web and
  Worker translate requests or events and orchestrate only their own boundary.
- A business rule, classifier, allocator, parser, workflow transition, or
  external effect has one implementation. Shared code is consumed through
  project references, never by copying source.
- A second business-policy implementation is duplication. Consolidate affected
  owners within the requested scope; do not wait for a third copy.

### Capability organization

Organize by business capability using Collision Engineers' business language.
No horizontal `Common`, `Helpers`, `Utilities`, or undifferentiated `Services`
folders; `V2`, `New`, `Manager`, `Helper`, or `Util` do not justify another
layer. `Audit` and `Triage` keep their reserved business meanings.

### Abstractions and deferred capabilities

Add an interface only for a real external boundary, a second concrete caller,
or an accepted ADR. A deferred capability belongs in
the current work plan for allocation; unresolved behavior belongs in open decisions. Do not
build dormant registration, unused endpoints or dark destructive code for
hypothetical future work. Wire required implementation to its real caller and
remove superseded code within the affected scope.

### Classifiers and failure semantics

- Evidence ordering in extraction is explicit and tested against contradictions.
  Accepted mailbox route predicates must be mutually exclusive (FRD-09); an
  overlap is a defect to correct, not a winner chosen by precedence or score.
  Recheck the complete affected rule set whenever a predicate is added.
- Every external client and catch path distinguishes `terminal`, `transient`,
  and `unknown`; terminal outcomes park the work and stop retries; exceptions
  are never converted into business truth.
- Metrics count successful effects, not attempts; a zero-error signal is
  meaningful only beside a heartbeat proving work occurred.

## Simplicity

The [project principles](../AGENTS.md#project-principles) in `AGENTS.md` are
the rules; these are the mechanics.

### Subtract before you add

Before a PR adds a mechanism, it names the existing owner of the same job and
either reuses it or removes it in the same PR. The owners that already exist:
`IMemoryCache` through `EvictableCacheEntry` for in-process caching; the Case
edit lease and the record edit scope for edit authority; `OperatorLabels` and
`CaseWorkspaceLabels` for operator-facing text; the FRD clause for a
behaviour rule; [rulings](rulings.md) for a decision. A second implementation
of any of these is a defect to consolidate within the PR's scope, not a third
copy to wait for.

### Page budgets

Server time at p95 over a week of real use, read from the request timing
filter's rows in Application Insights with one query a week and recorded in
the current-estate part of [operations](operations.md):

| Page | Budget |
| --- | --- |
| Work Centre, Inbox list, Cases list, Case page open | 500 ms |
| Save, handler fetches, dialog loads, refresh | 300 ms |
| Everything else (administration, reports, logs) | 1 second |
| Report PDF render, exports | 3 seconds, behind a busy state |

A page within budget gets no efficiency work. A change is built only when it
brings a page under its budget or saves at least 100 ms at p95 on one, and
its PR records the before and after figures. A reviewer flags a regression
only when it adds 50 ms or more to a hot page's p95. A new query-count test
asserts an upper bound or a no-growth pair (six rows cost the same statements
as three), never an exact total; an exact total breaks every other PR that
touches the page.

### Ports

A Core use case is a class registered by its own type. It gets an interface
only when a test fakes it or a second implementation exists. External
boundaries (SQL stores, Box, Graph, the valuation providers, the queue) keep
their ports, and so do the use cases the integration tests fake today.

### Decisions

A ruling is appended to [rulings](rulings.md) as one row and cited by id from
the FRD clause, code comment or test it governs. An ADR records only a choice
that is expensive to reverse: hosting, schema policy, an authentication
boundary, an external contract. An ADR reversed within two weeks is a signal
that the decision was not ripe; try the change first.

### Balance

Never trade clarity for compactness. Prefer explicit code; avoid nested
ternaries and clever one-liners; keep abstractions that improve organisation;
do not combine unrelated concerns into one function or component; do not
remove a name, a type, or a step a reader relies on. Comments that narrate
what the code visibly does are removed; comments that carry a reason stay.

### Fault handling shape

- Adapters name faults (`IntakeDependencyUnavailableException`); Core matches
  intake types, plus BCL types only where no adapter sits in between.
- One classifier per decision, looking through `InnerException` — EF wraps a
  SQL deadlock in `DbUpdateException`, and a store's retry helper rethrows the
  last attempt.
- A designed refusal (a lost edit lease, a stale version, a closed case) is
  logged as a Warning with its type and message and no exception object, so it
  does not reach the exception index that pages on-call
  (`DesignedCaseRefusal`). Only an unexpected fault logs its payload.
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

A new assertion on text an operator reads uses the label constant the page
renders (`OperatorLabels`, `CaseWorkspaceLabels`) where one exists.

### Case Workspace v2 fixture values

Test fixtures may use the Case Workspace v2 mockup's corpus-derived fixture
values.
These values include real claimant names and telephone numbers. The operator
decision of 2026-09-02 supersedes the earlier rule that the prototype's
fixture data is not domain data, for this fixture set only. `corpus/` stays local, ignored
and immutable, and no corpus file is committed, renamed or modified.

### Plan sizing

A plan is one page, written against the head it is built on, at the moment it
is built; a plan older than a week is re-derived from the issue and the code,
not re-verified. It states its diff estimate first. Six real steps beat
thirteen procedural ones; a step that only re-runs what CI runs, or re-checks
what `git diff` shows, is deleted. Research separates verified facts
(read-only checks, with the command) from assumptions; an assumption a
five-minute query would settle is run, not defended.

## Migrations

Two rules a migration carries, moved here from the release skill's migration
reference (30 October 2026):

- A migration or SQL that changes a user's roles, claims or enabled state also
  sets a new `SecurityStamp` for that user. Signed-in sessions are refused only
  when the stamp changes.
- A migration that creates a table, or changes what a runtime role may do on
  one, also changes that table's rows in
  `src/Pegasus.Infrastructure/Persistence/RuntimePermissions.csv`, the
  runtime-role permission record (one row per grant or denial for each role; a
  `none` row for a table neither role touches). `RuntimePermissionRecordTests`
  proves a fresh database holds exactly the recorded rows, and the release's
  database bootstrap proves production does (R482).

`Invoke-ProductionAdministratorBootstrap.ps1` runs only when a release changes
administrator bootstrap behaviour or the approved task requires administrator
reconciliation; it is not a routine migration step.

## Destructive operations

Before a destructive or bulk operation, enumerate exact targets, inspect the
baseline read-only under the intended identity and role, and confirm that the
current authorization covers the operation. Limited permissions are not proof
that a database is empty. Where real data or consumers must be preserved,
verify the recovery source and compatible artifact before acting. An explicitly
authorized reset of disposable test data requires no invented preservation
machinery. Stop if observed targets or consequences differ from the plan.

## Verification and failure handling

- Exercise representative retained input through the actual caller before
  claiming behavior. Idealized fixtures and registrations alone are insufficient.
- Derive evidence precedence explicitly and test realistic contradictions.
- Review literal business values against their accepted source independently
  of tests that may repeat the implementation's mistake.
- Keep one business-policy owner. Reuse the existing client and custody boundary.
- Keep a guard when it protects a supported failure mode; remove one whose
  requirement is obsolete. Absence of a recorded failure does not prove redundancy.
- Review broad edits against their exact base and resulting tree for lost behavior.
- Rehearse destructive operations against identified targets, with recovery
  evidence and the authorization required for the actual operation.
- Classify dependency failures at their boundary; expose exhausted or poison work
  with a supported recovery action rather than repeated opaque exceptions.



## Verification policy

The workflow selects checks by the paths a change set touches. Ordinary prose
and links do not require .NET restore/build/test. Embedded renderer assets and
other actual application/test inputs require the checks appropriate to their
effects.

Exact-head CI evidence is reused, never repeated locally. Full solution checks
apply when shared impact, explicit acceptance criteria or the release procedure
requires them. An explicit broader regression requirement is not a requirement
to rebuild after each prose amendment. The [verification procedures](runbook.md)
owns commands and test-environment setup.

Documentation correctness takes priority over obsolete parser/placement rules.
Record those consumers for amendment or retirement, without preserving a wrong
documentation contract to make them pass. Do not report their failure as an
application regression or conceal an actual runtime regression.
