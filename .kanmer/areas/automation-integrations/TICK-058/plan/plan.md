# Plan — TICK-058: Principal-scoped provider submission API

## Approach

Build on TICK-061's `IAuthenticatePrincipalCredential`. Add one Core owner
(`Pegasus.Core.ProviderApi`) that turns an authenticated Principal's
multipart envelope into the existing grouped durable intake on a new
`provider_api` source channel, records a `ProviderSubmissions` row that is
both the idempotency record and the Principal binding processing reads, and
returns the durable receipt immediately. Expose it through one gated,
bearer-only machine surface in Web. Diff estimate before coding: ~1,100
lines of hand-written code across Core/Infrastructure/Web/tests plus two
generated migration designers; actual: 49 files, ~1,250 hand-written lines.

## Steps (each names what it reuses)

1. Core vocabulary: `ActorKind.Provider` + `ActionActor.Provider`,
   `StaffAccessRight.SubmitProviderInstruction`, `IntakeSourceChannel.ProviderApi`
   — extending the existing single maps (`ActorDisplayNames`,
   `MailClassificationActor.Prefixes`, `ReceiveIntake` size switch,
   `SubmitGroupedIntake.OperationPrefix`).
2. `ProviderSubmission.cs`: `SubmitProviderInstruction` (reuses
   `IGroupedIntakeSubmission`, `IIntakeSubmissionGroupStore.FindAsync` for
   replay detection, `IActionHistoryWriter`, `IntakeEnvelopeLimits`) and
   `GetProviderSubmissionResult` (reuses `IQueuedIntakeStatusQueries`,
   `IIntakeReceiptQueries`; vocabulary is `QueuedIntakeStatusKind`,
   `IntakeDecision`, `IntakeAllocationFailureKind`).
3. Binding into processing: `ProcessIntake` establishes the principal from
   `IProviderSubmissionBindings` for the provider channel, skips mail-route
   selection, classifies by the established principal; `AllocateIntake`
   resolves the automatic principal the same way. A principal without an
   extraction policy → NeedsSorting.
4. Infrastructure: `ProviderSubmissionEntity` + configuration +
   `EfProviderSubmissionStore` (also the bindings port), migrations
   `20260828111707_ProviderSubmissions` and
   `20260828111732_GrantProviderSubmissions` (Web SELECT/INSERT, Worker
   SELECT), bootstrap census, migration list in
   `IntakePersistenceIntegrationTests`.
5. Web: `ProviderApi` constants, `ProviderApiAuthenticationHandler`
   (`Bearer pgs_…`, security events `provider_credential_missing|rejected`),
   `ProviderApiEndpoints` (`POST/GET /api/provider/v1/submissions`,
   problem details, 201/200/409/403/413/401/404), `Program.cs` gate
   `Features:ProviderApi`, per-key rate-limit policy, `IsMachineSurface`.
6. FRD-09 § Accepted API-01 submission contract.
7. Tests: `ProviderSubmissionTests` (Core) and `ProviderApiSubmissionTests`
   (SqlServer, through `WebApplicationFactory`, draining with
   `IntakeWebDriver.DrainStagedAsync`).

## Verification

`dotnet build ./Pegasus.slnx --configuration Release` green;
`pwsh ./scripts/Test-MigrationGrants.ps1` (82 files, all granted);
`pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` passed. Tests are
run by the orchestrator's wave loop (no `dotnet test` in this lane).

## Deferred activation

Named provider, hostname, live throttle values, capacity target and any
credential issuance need exact-target approval (capabilities.md boundary).
`docs/current-architecture.md` / `docs/operations.md` are DELIV-030's.

## Simplification pass — 2026-08-28

Lenses run over the branch diff (reuse, simplification, efficiency, altitude):

- Reuse: the first cut of the endpoint reused `IntakeMcpTools.DecisionCode`
  for snake-case decision codes; dropped in favour of the Core enum names
  serialized with `JsonStringEnumConverter`, so the API carries one
  vocabulary and touches no MCP file. Applied.
- Simplification: a `CreatedJson` `IResult` wrapper for the Location header
  was replaced by setting the header on the response before `Results.Json`.
  Applied. `replayed` detection is one `FindAsync` on the group store
  rather than a stored flag. Applied.
- Efficiency: files are buffered once per request (bounded by the envelope
  limit); the bindings lookup is one query per member during processing.
  No change.
- Altitude: the channel code/parse maps stay duplicated per EF store
  (pre-existing); not consolidated here — out of scope, noted in research.
  `ProcessIntake`'s optional `IProviderSubmissionBindings` parameter follows
  its existing optional-collaborator pattern rather than a new abstraction.
  Not applied by design.

## Simplification pass — 2026-08-28

Run over this branch's own diff after the contract rewrite, across the four
lenses. Findings and dispositions:

| Finding | Lens | Disposition |
| --- | --- | --- |
| `ProviderInstructionParty`, `ProviderInstructionClaimant` and `ProviderInstructionAsset` were left behind by the earlier manifest-based design and had no callers | simplification | **Fixed** — deleted (387f5e26) |
| `IntakeEvidenceSource` had two persisted code maps (`EfIntakeReceiptStore`, `InspectionAddressResolutionStore`) that had already drifted | reuse | **Fixed** — one owner, `IntakeEvidenceSourceCodes` (2804ebb6). This is not cosmetic: the drift failed case allocation with an unclassified fault |
| The intake field labels now have four users — the QDOS extraction policy that produces them, `InstructionDraftCompleteness` that reports them, `CaseDataSnapshotFactory` that looks values up by them, and this declaration | reuse | **Pinned, not fixed.** A test asserts every required label has a matching declared review field, so a rename fails there rather than in allocation. Extracting one owner touches the QDOS policy and the snapshot factory and is outside this brief — the duplication pre-dates it (three copies before this ticket) |
| The declared path could have been a second creation pipeline | altitude | **Avoided by design** — one substitution in `ProcessIntake.AssessAsync`; allocation, Triage creation, custody, action history and the Worker path are untouched |
| The provider arm of the extraction-policy mismatch branch, and `ProviderApiPrincipalPolicyKey`/`Version`, became unreachable once the substitution returns earlier | simplification | **Fixed** — removed (2804ebb6) |
| `GetProviderSubmissionResult` needed a staged-receipt lookup, which would have dragged the whole `IIntakeWorkStore` into it and its fakes | efficiency | **Fixed** — the submission records its own `StagedReceiptId`, so the result is one indexed read of our own row |
| Bounds could have been tighter wire-only numbers | simplification | **Rejected** — the case store's own bounds are used. A contract that refuses a fifty-character claimant name the database would have stored refuses real work |
| The "wrong secret" in `RefusedCredentialsAre401…` was `secret[..^1] + "A"`, which is the *same* secret when the issued one ends in `A` | correctness | **Fixed** — deterministic mutation. Pre-existing, and the likely cause of the single failure seen under the full parallel run |
| Migrations scaffolded after a `dev` merge re-add columns from earlier-timestamped migrations | correctness | **Deferred to a ticket** — hand-fixed here with the reason recorded in the migration; **DELIV-032** owns the guard, because it will recur on any branch that merges `dev` then scaffolds |
