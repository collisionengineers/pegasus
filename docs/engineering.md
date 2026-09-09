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

The [simplicity rails](../AGENTS.md#project-principles) in `AGENTS.md` are the
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

Select checks by affected behavior and executable inputs, using the existing
CI change classifier as a routing aid. Ordinary prose and links do not require
.NET restore/build/test. Embedded renderer assets, snapshots and other actual
application/test inputs require the checks appropriate to their effects.

Run focused checks first for affected code. Full solution checks apply when
shared impact, explicit acceptance criteria or the release procedure requires
them; serialize heavy work or reuse qualifying exact-head CI evidence. An
explicit broader regression requirement is not a requirement to rebuild after
each prose amendment. The [verification procedures](runbook.md)
owns commands and test-environment setup.

Documentation correctness takes priority over obsolete parser/placement rules.
Record those consumers for amendment or retirement, without preserving a wrong
documentation contract to make them pass. Do not report their failure as an
application regression or conceal an actual runtime regression.
