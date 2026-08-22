# Post-implementation report

**Branch:** `task/qdos26009-operator-fixes` · **PR:** #506 · **Commit:** `79bf3f86`

## What was built

An audit's own reference carries its `a.` / `ap.` prefix. No second identity is allocated,
no audit folder-creation token is issued, and the Box root is named by the case reference
for every case.

`AuditIdentity.Create` is unchanged. It was always producing the right string; it was being
applied to a second identity nobody wanted.

## The blocking question, and how it was settled

The ticket parked on whether the Repairable/Total Loss outcome is known at allocation,
because a reference is immutable afterwards. The operator answered directly, and the code
agreed: acceptance already refuses a standalone Audit without its retained original-report
evidence, and `StandaloneAuditEvidence.Assessment` already stores the outcome. Nothing new
was needed to know which prefix applies.

## Something this fixed that was not on the ticket

Custody named an audit's Box root from `AuditReference` while `GetExistingCaseRootAsync`
looked a root up by `CaseReference` — different strings for an audit. Nothing caught it
because **no custody test has ever run an audit case**; every fixture uses
`CaseType.Inspection`. One reference means one folder name and one code path, and that
divergence is a live suspect in [[DOCS-008]].

## What the compiler caught

Two errors, both real leftovers of the two-identity model rather than typos: a
`const string? auditReference = null` made an `is null` check always-true (CS8520), and
removing `isAuditCase` left the audit-folder branch still referring to it. Both were places
the old model was still showing through.

## Evidence

- 916 Core tests, 99 architecture tests, clean build
- Live: a new audit showing one reference and one matching Box folder — Phase 6

## Scope deliberately held

Existing cases are not rewritten — immutability applies to cases already allocated under
the old rule as much as to new ones. The `AuditReference` **column** stays, unset for
audits: dropping it is a migration with no behavioural gain that would erase how those
cases were named. A later Audit reference on a non-audit case is untouched.
