# Proof

**Shipped:** PR #506, commit `79bf3f86` · **Deployed:** Release 18 (`1f3be493`),
carried to Release 22 (`191ddf33`), the serving revision.

## The operator's direction, asserted end to end

> "It should be a.QDOS26009 … **There is no Case/PO AND audit identity. They are
> all just Case/PO.**"

`AnAutomaticAuditReachesReviewWithOneIdentityAndItsDocuments` (PR #515) drives a
real audit through `ProcessIntake` → acceptance → custody against real SQL, then
asserts both halves of that sentence on the persisted row:

```csharp
Assert.StartsWith("a.", identity.Reference, StringComparison.Ordinal);
Assert.Null(identity.AuditReference);
```

The reference **is** the audit identity, and nothing allocates a second one
beside it. Both assertions matter: the first alone would pass on a case that
still carried a redundant `AuditReference`, which is precisely what the operator
objected to.

```
dotnet test … ~CustodyOutboxIntegrationTests (Release)   21 passed, 0 failed
CI on PR #515                                            10 checks green
```

## What changed

`EfCaseAcceptanceStore` allocates one reference and applies the audit prefix to
it:

```csharp
var allocated = $"{principal.Code}{year % 100:00}{allocatedSequence:000}";
var reference = standaloneAuditAssessment is { } assessment
    ? AuditIdentity.Create(allocated, assessment) : allocated;
string? auditReference = null;
```

This also closed a split where the Box root was created under the audit identity
while lookups used the case reference — one identity means one folder name.

The `a.` / `ap.` choice comes from the original report's Repairable / Total Loss
outcome, and the fixture asserts the `repairable` path. Extracting and
**confirming** that outcome per issuing firm, including abstaining rather than
guessing, is [[INTK-031]] — it must be, because a reference is immutable once
allocated and a wrong reading cannot be corrected afterwards.

## Why the live cases still show two identities

```
Reference   AuditReference   Created
QDOS26010   a.QDOS26010      2026-08-22T02:01:04Z
QDOS26009   a.QDOS26009      2026-08-21T23:00:16Z
```

Both were created **before** this fix reached the estate — QDOS26010 at 02:01Z,
release 18 deployed at roughly 03:2xZ. Those rows are evidence the fix had not
run, and the product invariant forbids revising an allocated reference, so they
stay as they are.

## Evidence tier

**End-to-end against real SQL**, through the production acceptance and custody
chain. Not yet observed on a live case; the first instruction accepted after
release 18 shows it.
