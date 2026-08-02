# Engineering workflow

How repository work is done. Product behavior lives in
[requirements](requirements.md), the roadmap in [capabilities](capabilities.md),
procedures and evidence in [operations](operations.md), and current work in
[`NOW.md`](../NOW.md). Authority order is defined once in the
[documentation index](index.md).

## Branches and delivery

- Feature branches merge to `main` through a PR; `main` is the sole revision
  eligible for an authorised deployment.
- Commit subjects are imperative and name a capability ID from
  [capabilities](capabilities.md) when one applies.
- A durable decision that constrains future architecture gets an ADR under
  [docs/adr/](adr/README.md). Everything else is a commit message.
- CI green (build, tests, link check) is the merge bar.

## Evidence

Prove the actual caller — a registration, a file, a green build, a deployment,
and an accepted feature are different claims. The evidence tiers are defined
once in [operations](operations.md#required-evidence-tiers). Never collapse
them into "done": name what was traversed and what remains unproved. A green
test written from the same mistaken interpretation as the implementation proves
only self-consistency — material business rules get an independent literal
comparison against the authoritative rule.

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
