# Research — INTK-059

## Question

Does current Triage creation capture a principal, and is that relationship
stored on the Triage record?

## Verified findings

- **No.** `TriageRecord` and `TriageSummary` have no `PrincipalId`, and
  `CreateTriageFromIntakeRequest` accepts only the origin, registration,
  accepted-match evidence, actor and operation key
  (`src/Pegasus.Core/Triage/TriageContracts.cs:34-41,79-84,271-278`).
- **No.** `TriageEntity` and the `Triage` table mapping have no principal
  column or foreign key. They persist origin receipt, source identity,
  evaluation revision, registration, state, assignee and linked Case only
  (`src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs:740-767,
  1236-1256`). `EfTriageStore` consequently cannot map a principal.
- The normal durable creation path reads a registration plus exactly one
  strong `AcceptedTriageMatch` evidence item, then passes neither a principal
  code nor identifier to `CreateTriageFromIntakeRequest`
  (`src/Pegasus.Core/Intake/DurableIntake.cs:1094-1130`).
- A mail route establishes only `EstablishedPrincipalContext.PrincipalCode`.
  The current registered mail-classification and extraction policies are QDOS
  implementations (`src/Pegasus.Core/Intake/ProcessIntake.cs:801-810`,
  `src/Pegasus.Infrastructure/DependencyInjection.cs:151-156`). The accepted
  match is derived from that route's classification decision, not from a
  separate Triage matcher (`src/Pegasus.Core/Intake/ProcessIntake.cs:651-696`).
- There is also a non-QDOS source: an authenticated Provider API declaration
  builds an instruction draft with `binding.PrincipalCode` and adds the same
  accepted-match evidence (`src/Pegasus.Core/Intake/ProcessIntake.cs:753-786`).
  In both paths the origin receipt can carry a suggested principal *code*, but
  that is not a principal relationship on Triage.
- The Triage page currently loads the Triage detail and its origin receipt for
  images, but has no principal projection (`src/Pegasus.Web/Pages/Triage/
  Details.cshtml.cs:288-331`).
- FRD-03 expressly permits manual Triage classification without inventing a
  Principal identity (`docs/frd/frd-03-triage.md:20-22`). Therefore a principal
  field on Triage must be optional, not a creation prerequisite.

## Implications

The original display-only scope was incorrect. INTK-059 must persist an
optional, authoritative principal relationship at Triage creation, propagate it
through the Core contracts and queries, and render it read-only. It must not
infer identity from QDOS text, introduce another classifier, or make manual
Triage invalid.

## Sources and limits

No project-declared external research sources apply to Intake & Processing
(`get_sources`, 2026-09-04). Findings above are read-only checks of the current
repository and the two governing FRDs.

## Origin-receipt clarification (2026-09-04)

**Yes, the origin receipt persists a suggested principal code when extraction
or a Provider API declaration supplies one.** `InstructionDraftEntity` is a
one-to-one child of `IntakeReceiptEntity`, with
`SuggestedPrincipalCode` persisted in `InstructionDrafts`
(`src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs:247-272,
1398,1419-1429`). `EfIntakeReceiptStore` writes and reads that exact value
(`src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs:439-451,
593-600,857-882`).

**Yes, QDOS extraction obtains it — from the already established route
context, not from the message body.** `QdosInstructionExtractionPolicy` passes
`principalContext.PrincipalCode` into `CreateInstructionDraft`, which becomes
the draft's first (`SuggestedPrincipalCode`) value
(`src/Pegasus.Core/Intake/DirectProviders/Qdos/
QdosInstructionExtractionPolicy.cs:137-178,700-718`). `ProcessIntake`
rejects a draft whose code differs from the established route principal
(`src/Pegasus.Core/Intake/ProcessIntake.cs:961-986`). The Provider API path
likewise supplies `binding.PrincipalCode` to its draft
(`src/Pegasus.Core/Intake/ProcessIntake.cs:753-786`).

This is a durable **code on the origin receipt**, not a `PrincipalId` foreign
key or a Triage-owned principal relationship. INTK-059 remains correctly
scoped to preserve an optional authoritative relationship on the Triage record
rather than re-derive a value at rendering time.

## Email-origin Triage compared with standard Case creation (2026-09-04)

### Result

An e-mail-origin Triage can have a principal **code on its origin receipt**,
but no principal relationship on Triage. This is intentionally a different
path from standard Case creation.

### E-mail extraction and receipt retention

- The QDOS mail route accepts the sender and declares `QDOS` as both the
  route owner and work provider
  (`src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailRoutePolicy.cs:223-230`).
- `ProcessIntake` creates `EstablishedPrincipalContext` only from an
  accepted route's `WorkProviderCode`
  (`src/Pegasus.Core/Intake/ProcessIntake.cs:532-539,801-809`).
- QDOS extraction requires that already-established QDOS context; it adds
  principal-supporting evidence and passes the context code to
  `CreateInstructionDraft`
  (`src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs:137-178,700-720`).
  Therefore the extractor does **not** read a principal from message content:
  it carries forward the sender-route decision.
- The receipt persists both the route's `WorkProviderCode`
  (`src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs:615-634`)
  and the draft's optional `SuggestedPrincipalCode`
  (`src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs:439-456`;
  `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs:247-272`).

### Why it is not standard Case creation

- A classified Triage request is changed to `NeedsSorting` expressly because
  it is pre-Case work and should create no Case
  (`src/Pegasus.Core/Intake/ProcessIntake.cs:616-625`; see also
  `docs/frd/frd-03-triage.md:20-30`). Automatic allocation consequently
  returns without allocating unless the receipt decision is `CaseCreated`
  (`src/Pegasus.Core/Intake/IntakeAllocation.cs:238-248`).
- After that allocation attempt, the durable flow creates a qualifying Triage
  (`src/Pegasus.Core/Intake/DurableIntake.cs:766-771`). Its creation method
  reads only vehicle registration and exactly one strong
  `AcceptedTriageMatch`, then supplies origin, registration, evidence, actor
  and operation key—no principal code or identifier
  (`src/Pegasus.Core/Intake/DurableIntake.cs:1094-1127`;
  `src/Pegasus.Core/Triage/TriageContracts.cs:79-84`).
- Standard automatic Case creation instead obtains the established principal
  code, requires it to equal the receipt draft's suggested code, and carries it
  into the acceptance command
  (`src/Pegasus.Core/Intake/IntakeAllocation.cs:259-291,301-320`).
  Case acceptance resolves an active `PrincipalEntity` by that code and
  assigns `CaseEntity.PrincipalId = principal.Id`
  (`src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs:213-217,266-279`).
  The Case mapping has the restrictive Principal foreign key
  (`src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs:508-511`).
- By contrast, `TriageRecord` has no principal member
  (`src/Pegasus.Core/Triage/TriageContracts.cs:34-41`) and `TriageEntity`
  has no principal column, identifier or navigation
  (`src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs:740-765,1236-1256`).

### Ticket consequence

INTK-059 should persist a separately owned, optional authoritative Principal
relationship when the accepted route or Provider API declaration identifies one.
It must not treat the receipt's suggested code as a Triage relationship, infer
one from QDOS text, add a second classifier, or make manually classified Triage
invalid. The existing page already loads the origin receipt for evidence images
but projects no principal
(`src/Pegasus.Web/Pages/Triage/Details.cshtml.cs:292-310`).
