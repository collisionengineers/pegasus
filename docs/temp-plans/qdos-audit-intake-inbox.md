# Root Plan — `task/qdos-audit-intake-inbox`

## Overview

A Collision Engineers (CE) staff member forwarded a genuine QDOS **Audit**
work-provider instruction to the approved inbox. Three confirmed defects prevent
the correct outcome:

- **Defect A (primary, persistence):** the durable receipt never
  persists/rehydrates `MailClassificationResult.StandaloneAuditReport`, so the
  retry-from-durable-receipt replay records **zero** Audit evidence, and Audit
  acceptance (which *requires* a `StandaloneAuditEvidenceId`) never mints a case.
- **Defect B (structural, allocation):** `AttemptAutomaticAsync` runs after the
  work item is marked complete and outside the try/catch, and is never re-driven
  from the completed-work replay branch, so a single recoverable throw after
  completion permanently loses allocation → no case, no `create_case_custody`,
  no Box folder.
- **Defect C (display):** the Inbox renders a flattened top-level body with
  literal `cid:` tokens and the CE forwarder signature, and never descends into
  the attached `message/rfc822` to surface the provider's original.

Two additional confirmed prerequisites for the end-to-end fix in production:
- The **QDOS Organization + Principal are unseeded** in production. Migration
  `20260803014608_ProviderInspectionModeSetting` seeds only via
  `UPDATE [Principals] SET [InspectionMode]='image_based_assessment' WHERE
  [Code]='QDOS'` (`ProviderInspectionModeSetting.cs:44-45`) — a **no-op** when no
  `QDOS` Principal row exists. Confirmed live 2026-08-13: `Principals`,
  `Organizations`, `Cases` all count 0. Allocation/acceptance cannot mint a QDOS
  case reference without an active `QDOS` Principal.
- **Deployment currently needs local Docker** (`azure.yaml`
  `remoteBuild: false`), and the Defect A schema change must reach the production
  `pegasus` database through the established explicit-before-deploy migration
  route (EF `efbundle`).

Live production evidence (read-only, 2026-08-13): the forwarded message
`2026-08-13 00:19:29` (sender `desk@collisionengineers.co.uk`, subject
`Fw: (EREF18) RTA ...`) classified `accepted` / `WorkProviderCode=QDOS`,
effective sender `nduncombe@qdosassist.co.uk`, receipt `Decision=case_created`,
`CaseType=audit` (matched predicate `attachment.audit-report-notification`); yet
`StandaloneAuditEvidence`, `IntakeAllocationAttempts`, and `Cases` all had 0
rows, the work item was `completed` (`AttemptCount=2`) with the single event
`intake_receipt_recorded`.

The fixes are additive and safe: Defect A is a nullable-column schema change plus
serialize/rehydrate wiring; Defect B is an idempotent re-drive on the replay
branch; Defect C is a shared, signal-gated body cleaner applied at ingestion and
display.

## Supporting-file inventory

**None.** This is the root plan; there are no sub-plan files. All work described
here is self-contained in this document.

---

## Defect A — persist + rehydrate `StandaloneAuditReport`

Payload type: `StandaloneAuditReportEvaluation(string AssetSourceLabel,
AuditAssessment Assessment)` (`MailClassificationContracts.cs:176-178`). It is
the 9th positional parameter of `MailClassificationResult`
(`MailClassificationContracts.cs:186-195`) and is set by classification only for
`CaseType.Audit` (`QdosMailClassificationPolicy.cs:148-177`, evaluated at
`:180-215`). Enum `AuditAssessment { Repairable, TotalLoss }`
(`Pegasus.Core/Cases/CaseContracts.cs`), whose settled persisted codes are
`"repairable"`/`"total_loss"` (mirror of `EfStandaloneAuditEvidenceStore.cs`).

### A1. Entity — `src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs`
Add two nullable columns to `IntakeMailClassificationDecisionEntity` (after
`PolicyVersion`):
```csharp
public string? StandaloneAuditReportAssetSourceLabel { get; set; }
public string? StandaloneAuditReportAssessment { get; set; }   // "repairable" | "total_loss"
```
Both nullable: the fields are populated only for `CaseType.Audit`
classifications; every other decision row leaves them null.

### A2. Model config — `src/Pegasus.Infrastructure/Persistence/MailboxModelConfiguration.cs`
Inside the `IntakeMailClassificationDecisionEntity` builder block, add:
```csharp
entity.Property(item => item.StandaloneAuditReportAssetSourceLabel).HasMaxLength(500);
entity.Property(item => item.StandaloneAuditReportAssessment).HasMaxLength(40);
```
`AssetSourceLabel` mirrors the source-label width used elsewhere
(`EffectiveSenderSourceLabel` is `nvarchar(500)`); the assessment column matches
the 40-char code width used for `CaseType`/`Outcome`.

### A3. To-entity map — `EfIntakeReceiptStore.cs` (`MapMailClassificationDecision(MailClassificationResult, ...)`)
Add to the object initializer:
```csharp
StandaloneAuditReportAssetSourceLabel = decision.StandaloneAuditReport?.AssetSourceLabel,
StandaloneAuditReportAssessment = decision.StandaloneAuditReport is { } report
    ? ToCode(report.Assessment)
    : null,
```

### A4. From-entity map — `EfIntakeReceiptStore.cs` (`MapMailClassificationDecision(IntakeMailClassificationDecisionEntity)`)
Currently builds `MailClassificationResult` with only 8 positional args,
defaulting the 9th to null. Add a both-or-neither guard (matching the
route-selection guard style already present in the file) and pass the 9th
argument:
```csharp
var hasAnyAuditReportValue = entity.StandaloneAuditReportAssetSourceLabel is not null
    || entity.StandaloneAuditReportAssessment is not null;
var hasCompleteAuditReport = entity.StandaloneAuditReportAssetSourceLabel is not null
    && entity.StandaloneAuditReportAssessment is not null;
if (hasAnyAuditReportValue != hasCompleteAuditReport)
{
    throw new InvalidDataException("The persisted standalone Audit report evaluation is incomplete.");
}
// ... pass as the 9th arg:
hasCompleteAuditReport
    ? new(entity.StandaloneAuditReportAssetSourceLabel!, ParseAuditAssessment(entity.StandaloneAuditReportAssessment!))
    : null
```

### A5. In-place update — `EfIntakeReceiptStore.cs` (`ApplyMailClassificationDecision`)
The in-place branch copies each field from `replacement`. Add:
```csharp
entity.StandaloneAuditReportAssetSourceLabel = replacement.StandaloneAuditReportAssetSourceLabel;
entity.StandaloneAuditReportAssessment = replacement.StandaloneAuditReportAssessment;
```

### A6. Code maps — add to `EfIntakeReceiptStore.cs`
```csharp
private static string ToCode(AuditAssessment value) => value switch
{
    AuditAssessment.Repairable => "repairable",
    AuditAssessment.TotalLoss => "total_loss",
    _ => throw ...
};

private static AuditAssessment ParseAuditAssessment(string value) => value switch
{
    "repairable" => AuditAssessment.Repairable,
    "total_loss" => AuditAssessment.TotalLoss,
    _ => throw ...
};
```
Codes must match `EfStandaloneAuditEvidenceStore.cs` exactly (settled
`"repairable"`/`"total_loss"`).

**Effect:** every DB read-back now carries `StandaloneAuditReport`. The replay
path `ProcessIntake.cs:60-65 → RecordAutomaticAuditEvidenceAsync (:243-269)` no
longer no-ops at `:248-251`; it records `StandaloneAuditEvidence`, which
`AllocateIntake.AttemptAutomaticAsync` (`IntakeAllocation.cs:228-232`) then looks
up so Audit acceptance (`AcceptIntake.cs:60-66`) succeeds. No Core changes are
needed — the ports (`IRecordAutomaticStandaloneAuditEvidence`,
`IStandaloneAuditEvidenceQueries`) are already registered
(`Infrastructure/DependencyInjection.cs:172-176`).

### A7. EF migration + snapshot — see the dedicated section below.

---

## Defect B — re-drive `AttemptAutomaticAsync` from the completed-work replay branch

### Confirmed shape
- Normal path: `ProcessQueuedIntake.ExecuteAsync` completes the work item at
  `DurableIntake.cs:685` (`CompleteProcessingAsync`), the `try` ends at `:691`,
  and `allocateIntake.AttemptAutomaticAsync` runs at **`:722`, outside the
  try/catch**. A recoverable throw at allocation's Serializable `BeginAsync`
  (`EfIntakeAllocationStore.cs:35-36`) after `:685` permanently loses allocation.
- The completed-work **replay branch** (`DurableIntake.cs:618-650`, entered when
  `ClaimProcessingAsync` returns null but a completed evaluation exists)
  re-drives association, triage, and image automation — **but never allocation**.

### Fix — `src/Pegasus.Core/Intake/DurableIntake.cs:618-650`
In the replay branch, after `AssociateCaseIfUnambiguousAsync` and around
`CreateTriageIfQualifyingAsync`, re-drive allocation using the completed
evaluation, mirroring the normal path (`:716-733`):
```csharp
var replayAllocation = await allocateIntake.AttemptAutomaticAsync(
    completedReceipt.Id, completedEvaluation.Id, cancellationToken);
if (replayAllocation?.State.Status == IntakeAllocationProjectionStatus.Succeeded)
{
    completedReceipt = await receiptQueries.GetAsync(
        completedEvaluation.ProcessedReceiptId, cancellationToken) ?? completedReceipt;
}
```
Keep `ApplyImageIntakeAutomationAsync` last so it observes the
associated/allocated state (as the normal path does at `:735`).

### Idempotency (must not double-allocate) — verified
`AttemptAutomaticAsync` early-returns when the receipt already has a case or is
not `CaseCreated` (`IntakeAllocation.cs:220-223`). At the store, `BeginAsync`
runs Serializable and, for `Kind == Automatic` when any prior attempt exists,
returns `IsReplay: true, IsSuppressed: true` **without** inserting a new attempt
or invoking acceptance (`EfIntakeAllocationStore.cs:52-59`);
`AllocateIntake.ExecuteAsync` then returns the suppressed state without calling
`acceptIntake` (`IntakeAllocation.cs:366-377`). Re-drive on replay is safe.

> Ordering dependency with Defect A: on the replay branch, allocation reads the
> rehydrated `StandaloneAuditEvidenceId` (via
> `IStandaloneAuditEvidenceQueries.GetForReceiptAsync`,
> `IntakeAllocation.cs:228-232`). Without A, that evidence is null for an Audit
> and `AcceptIntake.cs:60-66` throws. **A must land with (or before) B for the
> Audit path.**

---

## Defect C — display cleaner (cid strip, forwarder signature, nested provider original)

### Confirmed shape
- `LocalEmailDisplayReader.ReadAsync` (`LocalEmailDisplayReader.cs:39-73`) stores
  one flattened **top-level** body: `message.TextBody`, or
  `ToInertText(message.HtmlBody)` (`:45-49`, `:128-135`). `ToInertText` strips
  tags but does **not** strip `cid:`/inline-image placeholders, remove the CE
  signature, or descend into an attached `message/rfc822`.
- That body flows to `RetainedMailboxMessageEntity.BodyPlainText`/`BodyExcerpt`,
  written **once** (`EfRetainedMailboxMessageStore.RetainAsync:19-95`, body/
  excerpt at `:55-56`; rows are write-once). Rendered verbatim in
  `Mail/Message.cshtml:110-119` and as an excerpt in `Mail/Index.cshtml:97-100`
  (read at `EfRetainedMailboxMessageStore.GetAsync:210` and
  `MapSummariesAsync`; excerpt computed at `Excerpt:417-455`).
- The staff-forward signal already exists: `EffectiveSenderAddress !=
  SenderAddress` and the `ForwarderLine` helper (`Index.cshtml.cs:114-129`). The
  inline-forward boundary is expressed by `InlineForwardedHeaderRegex`
  (`MimeKitPdfPigOpenXmlIntakeSourceReader.cs:995-998`).

### C1. New shared Core cleaner
Add `Pegasus.Core.Intake.StaffForwardBodyCleaner` (new file
`src/Pegasus.Core/Intake/StaffForwardBodyCleaner.cs`) — a pure, testable text
policy: `public static string Clean(string body, bool isStaffForward)`.
Responsibilities:
1. Strip `cid:` inline-image placeholders (`[cid:image001.png@...]`, bare
   `cid:...` tokens).
2. When `isStaffForward`, locate the inline-forward boundary (a Core-owned copy
   of the `From:/Sent:/To:/Subject:` header-block regex) and **prefer the
   provider's original below the boundary**, keeping the CE forwarder header only
   as a short provenance line rather than the full signature/preamble.
3. Collapse the residual whitespace the removals leave behind.

Core owns this because it is business presentation policy and Infrastructure
depends on Core (no upward reference). It carries no MIME dependency — it
operates on already-decoded text.

### C2. Ingestion — `LocalEmailDisplayReader.cs:39-73`
- Descend into an attached `message/rfc822` (`MessagePart`) to surface the
  **nested** provider body when the top-level body is a bare CE forward. Reuse
  the existing `MeasureDecodedLength` switch shape (`:106-123`) which already
  recognises `MessagePart rfc822`.
- Run `StaffForwardBodyCleaner.Clean(body, isStaffForward)` before returning,
  where `isStaffForward` is true when an attached `message/rfc822` part is
  present (the structural forward signal available at read time).
- Fixes **new** rows (both `BodyPlainText` and the derived `BodyExcerpt`).

### C3. Display — `EfRetainedMailboxMessageStore` read path
Because rows are write-once, apply the same cleaner at **read** so existing rows
are fixed too:
- In `GetAsync` (`:206-224`), pass `entity.BodyPlainText` through
  `StaffForwardBodyCleaner.Clean(...)` with `isStaffForward = receipt?.
  EffectiveSenderAddress` present and not equal to `entity.SenderAddress`.
- In `MapSummariesAsync`, clean the excerpt equivalently (`EffectiveSenderAddress`
  is already fetched there).
- Razor views unchanged (`Message.cshtml:110-119`, `Index.cshtml:97-100`) — they
  render the cleaned read-model text.

> Scope note for already-retained rows: cid-stripping and CE-signature
> de-emphasis apply at display via C3. Surfacing the *nested* `.eml` provider
> body for **existing** rows is not possible from the stored top-level
> `BodyPlainText` alone (the nested body was never retained; rows are write-once).
> C2 delivers nested-body surfacing for **new** forwards; existing rows get the
> cid/signature cleanup. State this explicitly in the PR.

### C4. De-duplicate the forward-boundary regex
Promote the `From:/Sent:/To:/Subject:` boundary pattern into a Core constant used
by both `StaffForwardBodyCleaner` and (by reference)
`MimeKitPdfPigOpenXmlIntakeSourceReader.InlineForwardedHeaderRegex:995-998`, so
the inline-forward boundary has one definition (no-duplicate-business-logic
constraint). If lifting the reader's regex is too invasive, keep the reader as-is
and add a Core-owned equivalent with a cross-reference comment plus a parity
test — but promotion is preferred.

---

## EF migration details

### Migration
```
dotnet ef migrations add StandaloneAuditReportDecision \
  --project ./src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj \
  --startup-project ./src/Pegasus.Web/Pegasus.Web.csproj
```
`Up` adds two nullable columns to the existing `IntakeMailClassificationDecisions`
table (pattern per `ProviderInspectionModeSetting.cs` `AddColumn`); `Down` drops
both.

- **No runtime-role GRANT/THROW block.** That block appears only in *new-table*
  migrations; column-add migrations carry none. Grants are table-level and the
  Worker/Web already hold grants on `IntakeMailClassificationDecisions`, so the
  new columns are covered automatically and `Invoke-AzureDatabaseBootstrap.ps1`'s
  expected matrix needs **no** change.

### Snapshot
`dotnet ef migrations add` regenerates `PegasusDbContextModelSnapshot.cs`. Verify
the two new `b.Property<string>("StandaloneAuditReport...")` entries inside the
`IntakeMailClassificationDecisionEntity` block have `.HasMaxLength(500)` /
`.HasMaxLength(40)` and no `.IsRequired()`. Commit the generated snapshot with
the migration; do not hand-edit.

---

## Test additions

### T1. Defect A — real EF round-trip (required; the existing unit test masks A)
`tests/Pegasus.Core.Tests/Intake/ProcessIntakeTests.cs:338-389`
(`AuditWithSeparateOriginalReportRecordsLiteralOutcomeBeforeAllocation`) uses
`RecordingStore`, whose `FindBySourceIdentityAsync` returns the **same in-memory
`IntakeReceipt`** — so `StandaloneAuditReport` is never round-tripped and Defect A
is invisible. Add an **integration** test in `tests/Pegasus.IntegrationTests`
(trait `Category=SqlServer`, `LocalDbTestDatabase`/`StoreAsync` pattern):
- Persist an `IntakeReceiptDraft` whose `MailClassificationDecision` is
  `Classified` with `CaseType.Audit` and `StandaloneAuditReport(assetSourceLabel,
  AuditAssessment.Repairable)`.
- Read back via `GetAsync`/`FindBySourceIdentityAsync`; assert
  `MailClassificationDecision.StandaloneAuditReport` is **non-null** with the
  exact label and `Repairable`.
- Drive the `ProcessIntake` replay against the persisted receipt and assert
  `RecordAutomaticStandaloneAuditEvidence` is invoked (evidence recorded).
- Both-or-neither guard test: a row with exactly one of the two columns set
  throws `InvalidDataException`.

### T2. Defect B — allocation re-driven on replay, no double-allocate
Extend `tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs`:
- First allocation threw after `CompleteProcessingAsync` (0 attempts persisted);
  invoke `ProcessQueuedIntake.ExecuteAsync` again → completed-work replay branch
  (`DurableIntake.cs:618-650`); assert a case is now minted.
- Already-succeeded allocation; invoke replay again → exactly **one** attempt /
  one case (idempotent suppression, `EfIntakeAllocationStore.cs:56-59`).

### T3. Defect C — display cleaning
`tests/Pegasus.Core.Tests` for `StaffForwardBodyCleaner`: `cid:` tokens removed;
CE signature/preamble de-emphasised and provider original focused when
`isStaffForward` true; untouched when false. Plus an infrastructure test asserting
`GetAsync`/excerpt return cleaned text for a staff-forward row, and that
`LocalEmailDisplayReader` surfaces the nested `message/rfc822` body for a new
attached-`.eml` forward.

### T4. Behaviour — fresh QDOS Audit forward mints Case/PO and enqueues custody
End-to-end test proving: a fresh forwarded QDOS Audit `.eml` (CE transport, QDOS
original, one instruction attachment + one original report stating Repairable/
Total loss) → `case_created` → `StandaloneAuditEvidence` recorded → allocation
attempted → Case/PO minted → `create_case_custody` enqueued.

### Local verification
`dotnet restore`, `dotnet build --configuration Release`, focused `dotnet test`
on the new/changed tests, then full `dotnet test` green (incl. `Category=SqlServer`
integration tests against LocalDB).

---

## Production QDOS seed sub-plan

**Goal:** create the missing QDOS Organization + Principal so allocation/
acceptance can mint QDOS case references.

**Target state (invariants enforced by `EfOrganizationAdministration.cs`):**
- `Organizations`: `Name="QDOS"`, `NormalizedName="QDOS"` (UPPER), `Version=0`.
- `OrganizationRoles`: one row `work_provider`.
- `PrincipalSequenceLineages`: one row.
- `Principals`: `Code="QDOS"`, `SequenceLineageId`=new lineage, `IsActive=true`,
  `Version=0`, `InspectionMode="image_based_assessment"`.
- `OrganizationAdministrationOperations` idempotency receipt + `ActionHistory`
  rows for both creates.

**Recommended method — drive the deployed Admin UI (uses the app's own create
logic):**
1. Sign in as an Administrator on the deployed Web app.
2. Organizations → create "QDOS" (`Organizations/Index.cshtml.cs`
   `OnPostCreateAsync`, `ICreateOrganization`). Ensure the **WorkProvider** role
   is set (via `Organizations/Edit.cshtml.cs` `OnPostUpdateAsync` if the create
   form does not set roles) — a Principal cannot be created under an org without
   WorkProvider (`OrganizationAdministrationPolicy.RequireOrganizationCanOwnPrincipals`).
3. Principals → create Principal `QDOS` under the QDOS org
   (`Principals/Create.cshtml.cs`, `ICreatePrincipal`), inspection mode **Image
   Based Assessment**.

This path satisfies every invariant automatically and is idempotent through the
operation-key receipt.

**Acceptable alternative:** a guarded, Administrator-authenticated one-off seeding
path that invokes the same Core use cases (`ICreateOrganization` →
`IUpdateOrganizationRoles` → `ICreatePrincipal`) with fixed operation keys. It
must reuse the Core commands — not new persistence.

**Warning — avoid raw-SQL seed.** A raw insert must replicate, exactly and
atomically, `Organizations` (incl. `NormalizedName` UPPER, `Version=0`),
`OrganizationRoles`, `PrincipalSequenceLineages`, `Principals`, **and** the
`OrganizationAdministrationOperations` receipt + `ActionHistory` rows. Any
omission bypasses invariants/idempotency and risks a divergent estate.

**Ordering:** the seed must exist **before** verifying end-to-end Audit case
creation in production.

---

## Infra + migration-application + deploy sub-plan

### Infra — `azure.yaml`
Switch both services to remote build so deployment does not require local Docker:
```yaml
services:
  web:
    ...
    remoteBuild: true      # was false
  worker:
    ...
    remoteBuild: true      # was false
```

### How production applies migrations (verified)
- Application startup **never** migrates a non-Development database (the
  `Program.cs` `MigrateAsync` call is gated on the `migrateDevelopment` flag /
  `DevelopmentOfflineInitialization`, dev-only).
- Production applies migrations **explicitly, before the application packages**,
  using an EF **migrations bundle** (`efbundle[.exe]`) produced by
  `scripts/Build-ReleaseArtifacts.ps1:69-70` (`dotnet ef migrations bundle …
  --startup-project ./src/Pegasus.Web`). Release route: build immutable artifacts
  from clean HEAD → validate plan (`Test-AzureDeploymentPlan.ps1`
  Artifact/PreUpload/PreMigration) → push digest-pinned image → **apply the
  pending migration** against production `pegasus` via the bundle → activate the
  single Web revision → redeploy the Worker → smoke.
- `Invoke-AzureDatabaseBootstrap.ps1` manages runtime principals/role membership,
  not schema migration; no change needed here (no new grants).

### Sequence for this change
1. Build release artifacts from clean exact HEAD (`efbundle`, `web-image.tar.gz`,
   `web.zip`, `worker.zip`).
2. Validate the deployment plan (Artifact/PreUpload/PreMigration).
3. Push the digest-pinned image to production ACR (now via `remoteBuild: true`).
4. **Apply the migration** (`StandaloneAuditReportDecision`) to production
   `pegasus` with the bundle, **before** activating the new revision. Columns are
   nullable → existing rows valid; no backfill.
5. Activate the new Web revision; redeploy the Worker package.
6. Smoke: `/health/live` + `/health/ready` 200, version/source-SHA match, and
   `__EFMigrationsHistory` contains the new migration.

---

## Sequencing / dependencies

1. **A before B (for the Audit path).** Without A, even a re-driven allocation
   (B) finds `StandaloneAuditEvidenceId == null` and Audit acceptance throws.
2. **Migration before deploy.** The Defect A columns must exist in production
   before the new code reads/writes them.
3. **QDOS seed before production acceptance verification.**
4. **C is independent** (display-only) and ships in the same PR.

---

## Acceptance criteria

- A fresh forwarded QDOS **Audit** email (CE transport, QDOS original,
  instruction attachment + original report stating Repairable or Total loss)
  produces, end-to-end: `case_created` → `StandaloneAuditEvidence` recorded →
  allocation **attempted** → **Case/PO minted** → `create_case_custody`
  **enqueued** → **Box folder** created.
- The persisted receipt round-trips `StandaloneAuditReport` non-null through the
  real EF store.
- Allocation is re-driven from the completed-work replay branch and never
  double-allocates (single attempt / single case).
- The Inbox shows the **provider's original** with **no `cid:` tokens and no CE
  forwarder signature clutter**; the CE forwarder appears only as a provenance
  line.
- `dotnet restore`, `dotnet build --configuration Release`, and focused + full
  `dotnet test` are green.

---

## Risks / rollback

- **Schema change:** additive and nullable → forward-safe; existing rows need no
  backfill. Rollback = migration `Down` drops the two columns.
- **Defect B double-allocation:** mitigated by the Serializable,
  `Automatic`-suppressed `BeginAsync` guard and the early-return in
  `AttemptAutomaticAsync`; T2 asserts single-attempt idempotency. Rollback =
  revert the replay-branch edit.
- **Defect C over-stripping:** gate strictly on the staff-forward signal and keep
  the forwarder provenance line; the cleaner is a pure function with unit tests.
  Existing-row nested-body limitation is documented, not silently accepted.
- **QDOS seed divergence:** avoided by using the app's own create logic (Admin UI
  / Core commands); raw-SQL seeding discouraged.
- **`remoteBuild: true` first use:** validate the ACR remote build succeeds in
  preflight before relying on it for the production push.

### Critical files
- `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs`
- `src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs`,
  `MailboxModelConfiguration.cs`
- `src/Pegasus.Core/Intake/DurableIntake.cs`
- `src/Pegasus.Infrastructure/Intake/LocalEmailDisplayReader.cs`, new
  `src/Pegasus.Core/Intake/StaffForwardBodyCleaner.cs`,
  `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs`
- `azure.yaml`, new migration under
  `src/Pegasus.Infrastructure/Persistence/Migrations/` +
  `PegasusDbContextModelSnapshot.cs`
