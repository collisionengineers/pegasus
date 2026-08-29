## 2026-08-29 — built by Codex, refuted by Claude, fixed, merged as #634

A Codex `gpt-5.6-sol` (high) lane built both defects' fixes; independent Claude
verification returned **DEFECTIVE** on two findings. Both are now disposed and
the PR merged at `dev` `8b6d4134`.

The lane's own work was strong and worth recording: it ran **discrimination
checks** rather than just asserting green — reverting each fix in place and
confirming the new tests go red (`PausedCredentialIsRefusedBeforeTheBodyIsParsed`
→ "Expected: Forbidden, Actual: BadRequest"; the snapshot branch disabled → both
persistence assertions fail on `Assert.NotNull`). It also reused
`CaseDataCodes.ProviderApi`, which is already parsed by
`EfCaseDataStore.ParseSourceKind`, already rendered by
`OperatorLabels.Provenance`, and already inside the
`CK_CaseDataFields_SourceKind` constraint — **so no migration was needed.** And
it corrected the ticket's own wrong path: `CaseDataSnapshotFactory` lives in
`src/Pegasus.Infrastructure/Persistence/`, not `src/Pegasus.Core/Cases/`.

### Finding 1, medium — FIXED. The fact claimed a provenance it could not support

`AddProviderFact` fired on `route is null && channel == ProviderApi &&
PrincipalCode present`, writing `SourceKind = provider_api` with
`SourceLabel = "authenticated credential binding"`.

But `PrincipalCode` is only the binding's on the **automatic** path.
`AttemptAutomaticAsync` derives it from
`EstablishedPrincipalCode(receipt, binding)` and acts as
`ActionActor.SystemWorker` (`IntakeAllocation.cs:259,283`). The **staff create**
path takes whatever an operator keyed — `Create.cshtml.cs:457-466` only trims,
uppercases and length-checks it, so staff can key a different principal
entirely, which is exactly how a provider that posted under the wrong account
gets corrected.

**The path is reachable, not theoretical.** A declared instruction omitting the
vehicle registration is permitted, answers 201, and becomes `NeedsSorting`;
`IntakeDecisionPolicy.CanBecomeCase(NeedsSorting)` is true and **no create path
carries a `provider_api` channel guard** — not `Intake/Details.cshtml.cs:82`,
`Cases/Create.cshtml.cs:584`, `ResolveIntake`,
`AllocateIntake.AttemptStaffCreateAsync`, nor `EfCaseAcceptanceStore.AcceptAsync`
— so the operator UI offers "Create a case" for it. The case, the provenance
chip, and then `EvaCaseEvidenceReader.FromCaseValue` would export
`case-data:ProviderApi:{receiptId}:authenticated credential binding` to the EVA
archive for a value no credential supplied.

That is the same falsehood the file already avoids forty lines below, where
`AddExtractedValue` maps a person-keyed value to `CaseDataCodes.StaffCorrection`
precisely so a case never claims evidence it does not have.

**Fixed** by adding `request.Actor.Kind == ActorKind.SystemWorker` to the
branch. A staff-created case records no work provider fact and keeps today's
behaviour.

**The existing test was exercising the wrong path.** It passed
`harness.StaffActor`, so it proved the behaviour on the very path that must not
claim this provenance. It now uses a `SystemWorker` actor — the real automatic
path — and `AStaffCreatedCaseDoesNotClaimTheCredentialBindingAsItsWorkProvider`
pins the staff path. This is a corrected fixture, not a weakened assertion: the
positive claim is unchanged and a second, stricter claim is added. Confirmed by
discrimination check — with the guard removed the new test fails
`Assert.Null() Failure: Value is not null`.

`AcceptIntake.ExecuteAsync` accepts `Staff or SystemWorker`
(`AcceptIntake.cs:32`), so the automatic path is genuinely reachable through the
same harness; the fix did not make the capability untestable.

### Finding 2, low — DEFERRED WITH REASON to [[DOCS-016]]

The ticket's clause "and the EVA export reports it" is proven by tracing, not by
assertion — every new assertion is on the `CaseDataProjection` from
`ICaseDataQueries`, none on `EvaCaseEvidenceReader.Build` or
`EvaOperatorExport.UnrecordedFields`.

The verifier traced it and **it does work**: `EvaHandoffStore.CreateAsync:66`
reads the same projection, `EvaCaseEvidenceReader.Build:49` passes
`caseData.Provider.WorkProviderCode` into `FromCaseField`, `Accepted()` takes the
`Fact` because `CaseDataValue.IsAccepted` covers `Fact`, so "Work Provider"
leaves `UnrecordedFields`. So this is a missing regression pin, not a broken
behaviour — but a silent one: a future change to `Accepted()`, to the
`Fact`/`Confirmed` precedence, or to `NotableWorkProvider` would restore the
original defect with every test still green.

Filed as [[DOCS-016]] rather than fixed in the lane, because building the export
fixture was more than the lane's remaining scope on the eve of release 37.

### Rule 14 — confirmed by the verifier, not asserted

`CaseDataSnapshotFactory.cs:54 AddProviderFact(snapshot, receipt, request)`,
reached from `CaseDataSnapshotFactory.Create`, whose only production caller is
`EfCaseAcceptanceStore.cs:293`. Real callers, not registrations or tests.

### Independent re-run

Full solution, `Category!=Corpus`: **2,483 passed, 0 failed** — Core 1167,
Architecture 100, Integration 1216 with 2 pre-existing corpus-dependent skips.
All ten CI jobs green on #634.
