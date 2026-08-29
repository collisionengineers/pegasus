# Post-implementation report — AUTO-013

Merged as PR **#634** into `dev` at `8b6d4134`.

## Defect 1 — the Work Provider is now recorded

`AddProviderFact` writes a `work_provider_code` row for a declared instruction:
value `request.PrincipalCode`, kind `Fact`, source kind
`CaseDataCodes.ProviderApi`, label `authenticated credential binding`, policy
`ProviderInstructionPolicy` v1.

**No migration and no grant were needed.** `CaseDataCodes.ProviderApi` already
existed, was already parsed by `EfCaseDataStore.ParseSourceKind`, already
rendered by `OperatorLabels.Provenance`, and already inside the
`CK_CaseDataFields_SourceKind` constraint. The value was already in hand from
`IntakeAllocation.EstablishedPrincipalCode` via `CaseAcceptanceRequest`. Nothing
new was plumbed.

## Defect 2 — the paused credential is refused before the read

`MaySubmit` moved out of `SubmitProviderInstruction` and into
`ProviderApiEndpoints`, ahead of the body read. The refusal keeps its status
code: the regression asserts `Forbidden`, and moving the check back below the
read makes it fail with "Expected: Forbidden, Actual: BadRequest".

## What review changed, and why it mattered

Independent verification found the fact claimed a provenance it could not always
support. Only **automatic** allocation's `PrincipalCode` is the credential
binding's; the staff create path takes whatever an operator keyed, and staff may
key a different principal to correct a provider that posted under the wrong
account. That path is reachable — a declared instruction missing the vehicle
registration answers 201, becomes `NeedsSorting`, and no create path carries a
`provider_api` guard, so the UI offers "Create a case".

The branch now also requires `request.Actor.Kind == ActorKind.SystemWorker`, so
a staff-created case records no work provider fact rather than exporting
`case-data:ProviderApi:{receiptId}:authenticated credential binding` to the EVA
archive for a value no credential supplied.

The original positive test had been passing a **staff** actor, so it proved the
behaviour on exactly the path that must not claim this provenance. It now uses a
`SystemWorker` actor and a second test pins the staff path.

## Verification, as run

| Claim | Evidence |
| --- | --- |
| Work provider persisted on the automatic path | `AcceptanceRecordsWorkProviderFromAuthenticatedCredentialBinding`, real SQL |
| Staff path records nothing | `AStaffCreatedCaseDoesNotClaimTheCredentialBindingAsItsWorkProvider`, real SQL |
| Paused credential refused before the read | `PausedCredentialIsRefusedBeforeTheBodyIsParsed` |
| The tests are genuine | Discrimination checks: guard removed → `Assert.Null() Failure: Value is not null`; snapshot branch disabled → both positives fail `Assert.NotNull`; guard moved below the read → "Expected: Forbidden, Actual: BadRequest" |
| Nothing else broke | Full `Category!=Corpus`: **2,483 passed, 0 failed** (Core 1167, Architecture 100, Integration 1216, 2 pre-existing corpus skips) |
| CI | All ten jobs green on #634 |

Rule 14, confirmed rather than asserted: `CaseDataSnapshotFactory.cs:54`
`AddProviderFact`, reached from `Create`, whose only production caller is
`EfCaseAcceptanceStore.cs:293`.

## Left open, deliberately

- **"and the EVA export reports it"** is proven by tracing, not assertion. The
  path works — `EvaHandoffStore.CreateAsync:66` → `EvaCaseEvidenceReader.Build:49`
  → `Accepted()` takes the `Fact` — but nothing pins it, so a future change
  could silently restore the defect with every test green. Filed as
  [[DOCS-016]].
- **The existing-case-matching question** is untouched. A repeat declared
  instruction on the same claim still allocates a new case. That needs an
  operator answer recorded in FRD-09 and was not this lane's to decide.
- `SubmitProviderInstruction.ExecuteAsync`'s four-write ordering is untouched —
  [[AUTO-012]] owns it and was worked in parallel.
