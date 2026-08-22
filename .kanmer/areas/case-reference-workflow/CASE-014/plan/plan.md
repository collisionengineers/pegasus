# Plan

Committed in `79bf3f86`.

## The sequencing question, answered

The ticket parked on one thing: a reference is immutable once allocated, and the prefix
depends on a fact read from the third-party report — so is that fact known **at**
allocation?

Operator, 2026-08-22: *"Yes, you already know this, its why we are extracting the third
party report for the audit."*

The code agrees. `EfCaseAcceptanceStore` already refuses to allocate a standalone Audit
without its retained original-report evidence, and `StandaloneAuditEvidence.Assessment`
already holds `repairable` / `total loss`. The outcome is in hand before the reference is
minted, so the prefix can go on the reference itself with no risk of needing revision.

## The change

```
before   Reference = QDOS26009    AuditReference = a.QDOS26009
after    Reference = a.QDOS26009  AuditReference = (none)
```

`AuditIdentity.Create` is unchanged — it was always producing the right string, just
applied to a second identity nobody wanted.

## What this fixed that was not on the ticket

Custody named an audit's Box root from `AuditReference` while `GetExistingCaseRootAsync`
looked it up by `CaseReference`. For an audit those were different strings. Every case now
uses one name, which removes a divergence that no test covers — and which is a live
suspect in [[DOCS-008]].

## Acceptance

- An audit allocates `a.<ref>` (Repairable) or `ap.<ref>` (Total Loss). ✅
- No second identity is allocated, stored or displayed for an audit. ✅
- A later Audit reference on a non-audit case is unaffected. ✅
- 916 Core and 99 architecture tests pass. ✅
- Live: a new audit shows one reference everywhere, and its Box folder matches — Phase 6.

## Scope held

Existing cases are not rewritten. Principal and reference are immutable after allocation,
and that applies to cases already allocated under the old rule as much as to new ones.

## Simplification pass

2026-08-22. Removes a concept rather than adding one: one identity per case, one folder
name, one code path for audit and non-audit custody. No findings deferred.
