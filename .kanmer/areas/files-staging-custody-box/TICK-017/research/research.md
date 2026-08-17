# Research — TICK-017 (DOC-01): Automatic Box case-folder creation using the Case/PO name

## Question

Before planning, establish the current Core policy owner, the real caller, the
persistence/infrastructure boundary, and the acceptance evidence for DOC-01 —
and separate what is already caller-proved locally from what the activation
boundary still lists as pending.

Activation boundary (docs/capabilities.md:167): "Immutable Case/PO naming,
response-loss-safe binding, fail-closed conflict handling and human reasoned
recovery are caller-proved locally. Live controlled Box target proof,
migration, deployment and operator acceptance remain pending."

## Findings

- **Core port owner — IMPLEMENTED.** `ICaseCustody` is the case-scoped Core
  port at `src/Pegasus.Core/Custody/CustodyContracts.cs:64` (`CreateCaseRootAsync`
  :66/:77 + lease-guarded overload :84, `GetExistingCaseRootAsync` :101,
  `RetainAcceptedIntakeSourceAsync` :106, `CreateAuditReferenceFolderAsync` :123).
  Result record `CaseCustodyRoot(CaseId, RemoteId, Reference)` :21. Fail-closed
  policy `CustodyRetryPolicy.Decide` :246. The port doc-comment forbids
  accepting an arbitrary remote id from a caller (:60-63).
- **Infrastructure implementation — IMPLEMENTED.** `BoxCaseCustody : ICaseCustody`
  at `src/Pegasus.Infrastructure/Custody/BoxCaseCustody.cs:423`, over a raw Box
  REST client `BoxContentClient` (:133; `POST /folders` :198-215, item search
  :159, ETag rename :257, upload :278) authenticated by the Box SDK JWT provider
  (:99-114). Alternatives `LocalCaseCustody` (filesystem, DevelopmentOffline)
  and fail-closed `UnavailableCaseCustody` are wired in
  `src/Pegasus.Infrastructure/DependencyInjection.cs:312-338`.
- **Immutable Case/PO naming — IMPLEMENTED.** Folder name IS the reference:
  `CaseFolderName(reference) => CustodyNames.SafeName(reference)`
  (`BoxCaseCustody.cs:886`); `CustodyNames.SafeName`
  (`src/Pegasus.Infrastructure/Custody/CustodyNames.cs:16-31`) is a
  deterministic, host-independent mapping. Every folder carries an immutable
  binding file `pegasus-case-binding.json` `{schemaVersion, caseId, caseReference}`
  (`BoxCaseCustody.cs:825-831`), re-verified byte-for-byte on reuse
  (`VerifyBoundFolderAsync` :753-769, `VerifyFileBytesAsync` :790-801). Test
  asserts a folder literally named `QDOS31001` with `DeleteCount == 0`
  (`ProductionBoxCustodyTests.cs:226-233`).
- **Response-loss-safe binding — IMPLEMENTED.** Two-phase, idempotent,
  predeclared-owner protocol in `GetOrCreateBoundFolderAsync`
  (`BoxCaseCustody.cs:645-731`): a predeclared 26-char creation-owner token is
  stored on the durable outbox row at acceptance
  (`EfCaseAcceptanceStore.cs:390` `CaseRootCreationToken = CustodyCreationOwner.Create()`),
  work is staged under `.pegasus-create-{token}` and promoted to the final name
  by an ETag-guarded rename (:713-719); a lost create/rename response reconciles
  by re-lookup rather than duplicating, and `409 Conflict` re-looks-up by name
  (:206-210). Proven by `TerminationAndLostResponsesReconcileOnlyPredeclared…`
  (`ProductionBoxCustodyTests.cs:294`) and lost-upload reconcile (:251-262).
- **Fail-closed conflict handling — IMPLEMENTED.** Occupied final name
  (:696-711), duplicate children (:185-190), wrong type (:191-194), trashed /
  outside approved root (`EnsureDescendantAsync` :334-370) all throw; root is
  hard-pinned to production root `405543781910` (`BoxCustodyOptions.Create`
  :33-36). Failures stay visible with no background retry
  (`FailProcessingAsync`, `EfQueuedCustodyProcessor.cs:470-491`;
  `BoxFailureRemainsVisibleToTheCallerWithoutBackgroundRetry`). Tests assert
  `MutationCount == 0` on each failure (`ProductionBoxCustodyTests.cs:265-292`).
- **Human reasoned recovery — IMPLEMENTED.** Core use case
  `RetryCaseCustody.ExecuteAsync` (`CustodyContracts.cs:329-379`) requires a
  staff actor with `PerformCasework`, a non-blank reason, an edit-lease token
  and expected Case version; `CustodyRetryPolicy.Decide` (:246-286) only re-arms
  work whose state is `"failed"`. Web caller `Details.cshtml.cs:313
  OnPostRetryCustodyAsync` → `_CaseWorkflow.cshtml:521` "Retry custody" form.
  Proven end-to-end in `CustodyOutboxIntegrationTests.cs:946` and `:1104`.
- **Real caller — IMPLEMENTED and wired (not gated off).** Case
  acceptance/allocation enqueues a `create_case_custody` external-work row with
  the predeclared token (`EfCaseAcceptanceStore.cs:380-394`); the Worker timer
  dispatches pending work (`ExternalWorkProcessing.cs:84`,
  `WorkerDependencyInjection.cs:99-102`) →
  `ProcessQueuedExternalWork.ExecuteAsync` routes to `IProcessQueuedCustody` →
  `EfQueuedCustodyProcessor.ExecuteAsync` calls `CreateCaseRootAsync`
  (`EfQueuedCustodyProcessor.cs:118-124`) then `RetainAcceptedIntakeSourceAsync`
  and (Audit) `CreateAuditReferenceFolderAsync`. Which `ICaseCustody` resolves
  is composition-profile driven: Production → `BoxCaseCustody` (real HTTPS),
  DevelopmentOffline → `LocalCaseCustody`, neither → `UnavailableCaseCustody`
  (`DependencyInjection.cs:312-338`, `Program.cs:121-183`).
- **Tests — all against fakes, none live.** Every Box test injects a fake
  `HttpMessageHandler` / in-memory `StatefulBox` and a stub bearer
  (`RecordingAuthorizationHeaderProvider` returns `"Bearer test-token-N"`,
  `ProductionBoxCustodyTests.cs:469-475`). `ProductionCompositionTests.cs`
  resolves `BoxCaseCustody` from the production composition with no network
  call. `docs/operations.md:96` records the evidence tier: "Box | Fake SDK/HTTP
  contract … | Real custody, permissions, versions, recovery, production
  target, and caller evidence [pending]".
- **Requirement text (verbatim).** `docs/frd/frd-05-documents-extraction-and-custody.md` "Staging and custody"
  (:405-409): Box is the required accepted case-file custody system for day-one
  alpha; every allocated Case/PO uses its immutable reference for its Box case
  folder; a Box failure after allocation retains the Case as `Not ready` with
  explicit failure + staff-initiated retry evidence and "does not roll back,
  reuse, or reallocate the reference, and no background or automatic business
  retry is permitted." Controlled-target scope (:412) and runbook (:754) name
  the approved **disposable test subtree `392761581105`** — documented but NOT
  wired into any composition; only the production root `405543781910` is pinned.

## Implications

- DOC-01's *code* is effectively complete: all four locally-provable behaviours
  in the activation boundary are implemented and exercised through the real
  outbox→worker→processor caller (against Box fakes). This is a
  **plan/research + acceptance** ticket, not a build-the-feature ticket. plan.md
  must not re-implement what exists.
- The entire remaining gap is `requires-live-approval`: (1) a live controlled
  Box call has never run — no composition wires the approved disposable test
  subtree `392761581105`; (2) migration/deployment against a Production-profile
  host with live Box secrets is unproven; (3) operator acceptance is unrecorded.
  None of this can be produced now without explicit user approval naming the
  exact Box test target and operation (CLAUDE.md live-operation approval matrix;
  "Local alpha work must not mutate … any Box location").
- The existing `proof.md` ("Operator confirmed") is a placeholder, not evidence.
  Real DOC-01 proof splits into (a) the already-passing local caller-proof test
  suite, which we can run and cite now, and (b) the live-target/operator
  acceptance tier, which stays pending behind approval.
- DOC-01 is blocked-by INT-25 (TICK-012): the Case/PO reference the Box folder
  is named from is produced by INT-25's automatic allocation. The contract DOC-01
  consumes (reference format `a.`/`ap.`, immutability) is owned there.

## Open questions

- Will the user approve wiring the disposable test subtree `392761581105` into a
  separate Box integration-test profile for a one-off live create/reconcile
  smoke, or does DOC-01's acceptance stay entirely deferred until go-live? (Live
  Box mutation needs explicit per-target approval.)
- Is "operator acceptance" for DOC-01 meant to be recorded here as a Kanmer
  proof once the operator confirms against the live target, or does it belong to
  a separate release/acceptance ticket?
- Does the immutable Case/PO reference format from INT-25 (`a.` / `ap.` and the
  `QDOS…` forms seen in tests) fully match what `CustodyNames.SafeName` accepts
  (≤120 chars, no reserved names)? To confirm against INT-25's final contract.
