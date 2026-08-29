# Plan — AUTO-013

## Defect 1 — a Provider API case records no Work Provider

`CaseDataSnapshotFactory.AddProviderFact` returned early unless the receipt
carried an accepted `MailRouteDecision` with a work-provider code. A Provider
API receipt has no mail route by design — its Principal comes from the
credential — so `WorkProviderCode` was never written, and the EVA export
reported Work Provider as unrecorded even though allocation had established the
Principal from the submission binding. The system knew who the work provider was
and did not write it down.

**Reuse, not build.** `CaseDataSourceKind.ProviderApi` already exists, is parsed
by `EfCaseDataStore.ParseSourceKind`, rendered by `OperatorLabels.Provenance`,
and already permitted by the `CK_CaseDataFields_SourceKind` constraint. So the
new row needs **no migration and no grant**. The value is already in hand:
`IntakeAllocation.EstablishedPrincipalCode(receipt, binding)` resolves it and
`AcceptIntake` normalises it into `CaseAcceptanceRequest.PrincipalCode`. The
channel test uses `EfIntakeReceiptStore.ParseSourceChannel`, the one owner of
that mapping.

The two branches are exclusive by construction, so two rows for one field are
unreachable.

**Terminology.** `docs/operator-notes.md:219` — "| Work Provider | Also referred
to as the principal. |" Principal *is* the work provider. There is no "provider
principal"; the phrase appears nowhere in this change.

## Defect 2 — a paused credential was refused only after the body was read

`ProviderApiEndpoints` enforced 413 and 415 before the read, but `MaySubmit` was
checked inside `SubmitProviderInstruction` after the body had been read and
parsed. Bounded, not unbounded — but the check belongs before the read. Moved,
with the refusal's status code held constant.

## The provenance guard — added in review, and the substance of this ticket

Only **automatic** allocation's `PrincipalCode` is the credential binding's.
`AttemptAutomaticAsync` derives it from `EstablishedPrincipalCode(receipt,
binding)` and acts as `ActionActor.SystemWorker` (`IntakeAllocation.cs:259,283`).
The **staff create** path takes whatever an operator keyed —
`Create.cshtml.cs:457-466` only trims, uppercases and length-checks — and staff
may key a *different* principal to correct a provider that posted under the
wrong account.

That path is reachable: a declared instruction omitting the vehicle registration
is permitted, answers 201, becomes `NeedsSorting`, and no create path carries a
`provider_api` guard, so the UI offers "Create a case".

Writing `"authenticated credential binding"` there would export a provenance to
the EVA archive that no credential supplied — the same falsehood
`AddExtractedValue` avoids forty lines below by mapping a person-keyed value to
`StaffCorrection`. The branch therefore also requires
`request.Actor.Kind == ActorKind.SystemWorker`.

## Verification

Real persistence, both directions, because a fake store bypassing Core proves
only that a value is *constructed*:

- `AcceptanceRecordsWorkProviderFromAuthenticatedCredentialBinding` — automatic
  path records the fact with the right kind, label, policy key and version.
- `AStaffCreatedCaseDoesNotClaimTheCredentialBindingAsItsWorkProvider` — staff
  path records nothing.

Both discrimination-checked: guard removed → the staff test fails
`Assert.Null() Failure: Value is not null`; snapshot branch disabled → both
positive assertions fail on `Assert.NotNull`; the paused-credential guard moved
back below the read → "Expected: Forbidden, Actual: BadRequest".

Full solution `Category!=Corpus`: **2,483 passed, 0 failed**. All ten CI jobs
green on #634.

## Not done, and why

The ticket's clause "and the EVA export reports it" is proven by tracing rather
than assertion. The path was followed end to end and works, but nothing pins it.
Filed as [[DOCS-016]] — a missing regression pin, deferred with reason rather
than silently accepted.

The open question about whether a declared instruction should match an existing
case is untouched: it needs an operator answer recorded in FRD-09 and is not
this lane's to decide.

## Simplification pass — 2026-08-29

Ran. Applied: the lane's own `refactor(provider-api): simplify work provider fact
creation` collapsed the two branches onto a single construction site, so the row
is built once rather than twice. Reuse recorded above — `CaseDataCodes.ProviderApi`,
`ParseSourceChannel`, `EstablishedPrincipalCode` — with nothing new introduced.
No unapplied findings.
