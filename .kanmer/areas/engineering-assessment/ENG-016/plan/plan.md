# Plan

Ordered so the build is broken for as short a span as possible: Core first (it
defines what disappears), then Infrastructure, then Web, then tests, then the
schema, then docs.

## 1. Core — remove the hand-off vocabulary

**Reuses:** nothing new. `EvaBundleSchema.CreateOfflineReplay`,
`EvaHandoffPolicy.SelectEligibleImages`, `NoRetainedImagesReason`,
`CaseEvaMapping.MapForOperatorExport`, `IEvaHandoffProxy` all stay exactly as
they are.

Delete from `EvaBundleSchema.cs` the request/result/port/policy types listed in
`files`. `MapForProduction` and `ValidateAcceptedEvidence` go from
`CaseEvaMapping.cs` — research confirmed one caller, itself deleted.

**On `DecideRevision`, which the ticket asks me to reuse — I did not, and here
is why.** It returns `(ReuseExisting, BusinessRevision, RecordFirstProxy)`. Two
of those three are revision concepts that die with `EvaHandoffRevisions`; what
survives is `RecordFirstProxy = !firstProxyAlreadyRecorded`. Keeping a
three-field decision record to carry one negated boolean is the
"abstraction with no second caller" smell the repo bans. **The rule it encodes
is kept exactly** — first success records the proxy, later ones do not — and it
is now enforced where it is strongest: `EvaFirstHandoffProxies` already has
`CaseId` as its **primary key**, so the database itself refuses a second row
per case. The store reads before writing inside the export's transaction, and
the PK is the backstop under a race. That is a stronger guarantee than the
Core predicate was, not a weaker one.

**On `EvaHandoffPolicyAuthority`, which the ticket does not name.** It is
passed only to `IEvaHandoffPersistence`'s two methods, both deleted, and
constructed only by `GenerateEvaHandoff`/`DownloadEvaHandoff`, both deleted.
Deleting it is a consequence of the named deletions, not added scope; leaving
it would be dead architecture.

`CaseQueries.cs` drops `EvaHandoff` from `CaseDetails` and the
`IEvaHandoffQueries` dependency.

## 2. Infrastructure — one act in the store

**Reuses:** `LoadEligibleImagesAsync` and `BuildEvidence` unchanged — both
already exist and are already shared. `IEvaHandoffProxy.RecordFirstGenerationAsync`
and its `ClaimsExternalDelivery`/`ClaimsEngineerAssignment` rejection are
lifted verbatim out of the deleted `GenerateAsync` into the export path, so the
no-delivery-claim guarantee moves without being rewritten.

`IExportCaseBundle.ExecuteAsync` gains, after the bundle is successfully built
and only then:

1. open a transaction,
2. `AnyAsync(item => item.CaseId == …)` on `EvaFirstHandoffProxies`,
3. if absent: call the proxy port, reject a receipt claiming delivery or
   Engineer assignment exactly as the hand-off did, insert the row, commit,
4. if present: commit nothing and return the bundle.

Ordering matters and is deliberate: **the proxy is recorded only after the
archive exists**, so a failed export records nothing. "First success only" is
literal.

The export still does **not** move the case version, take an edit lease, or
require an operation key. It is not a case mutation; it records a once-per-case
fact in a side table. The proxy row (`RecordedAtUtc`, `ActorSubjectId`,
`AdapterKey`, `AdapterVersion`) is the evidence, which is what that table was
built to be.

`EvaFirstHandoffProxyEntity` loses `RevisionId` (points at a deleted table) and
`OperationKey` (an export has none, and the `CaseId` PK is the idempotency).
The two `CK_EvaFirstHandoffProxies_*` check constraints touch neither column
and are carried through untouched.

**Deliberately not renamed:** `EvaHandoffStore`, `EvaHandoffEntities.cs`,
`EvaHandoffModelConfiguration.cs`. The names now describe a deleted act, and a
rename is a real clarity win — but this branch is third in a four-deep stack
and a rename would widen the conflict surface across all four for no
behavioural gain. Its class doc-comment is corrected to say what it now is, and
the rename is named in the PR as skipped with this reason.

## 3. Web — GET becomes POST

**Reuses:** `Details.cshtml:103-109`, the `ClaimLease` control on the same
action bar — `<form method="post" asp-page-handler="…" class="record__bar-form">`
with `<button type="submit" class="btn">`. Razor Pages' form tag helper emits
the antiforgery token and the framework validates it automatically; research
confirmed the app adds no explicit filter and every other POST relies on this.
No new mechanism.

The handler must be **named** — `OnPostBundleAsync`, reached by
`asp-page-handler="Bundle"` — because plain `OnPostAsync` on that page is
already the selective document export. The `OnGetAsync` disappears entirely, so
the route answers 405 to a GET: a prefetch or a refresh cannot fire it.

No copy is added. `docs/design/README.md:422-445` bans how-it-works prose and
allows at most one approved consequence sentence on a *destructive* action;
export is not destructive, so the control stays a label and a control. The
existing `Available in Review` gated affordance is preserved as-is.

Delete the `Eva/Download` page, the `GenerateEvaHandoff` handler, the EVA panel
and the two MCP tools.

## 4. Tests

Delete the hand-off suite. **Invert** `CustodyOutboxIntegrationTests`'s
proxy assertion — it currently proves an export records none. Add: a second
export of the same case records no second row. Fix the four composition and
web suites that resolve or link deleted things. Rewrite the Eva half of
`DependencyDirectionTests` around what survives (Core owns `EvaHandoffPolicy`
and `IExportCaseBundle`; `EvaHandoffStore` is Infrastructure and references
neither Web nor Worker).

`AzureSqlRuntimeRoleMigrationTests` is **not** touched — verified pinned to a
historic migration, not HEAD.

## 5. Schema

`dotnet ef migrations add DropEvaHandoffTables` so the `.Designer.cs` and the
snapshot are generated, not hand-written, and no historic Designer moves.
`Up()`: drop the FK, the `RevisionId` unique index and the two proxy columns,
then `EvaHandoffDownloadOperations`, `EvaHandoffOperations`,
`EvaHandoffRevisions` — child-first. `Down()` restores all of it empty.

Non-additive, under the rule as it stands **on this branch** — PLAT-042's
amendment is not in this history, verified, so the unamended
`docs/runbook.md:1140` applies and needs a recovery strategy. It has one: the
hand-off is switched off in production and its tables are empty
(`docs/operations.md:410-411`, `:572-573`), so rolling the application back
behind this migration degrades only a capability that is off and has never
produced a row. Affected capability, named as the rule requires: **EXT-03**.

`scripts/Invoke-AzureDatabaseBootstrap.ps1` joins the migration to its existing
`$removedTables` list — the mechanism three earlier drop-migrations already
use. The hand-edited `EvaHandoffDownloadOperations` block must keep naming
`20260819180000_GrantEvaHandoffDownloadOperations`, because
`Test-AzureDeploymentPlan.ps1:295-309` scans for every post-baseline
`GRANT`-carrying migration by name and that file still contains one.

`scripts/Test-MigrationGrants.ps1` needs no edit — verified it only inspects
`CreateTable(` inside `Up()`.

## 6. Docs

FRD-07's `First sent to Engineer` trigger and its download sentence.
`capabilities.md` EXT-03, CASE-21, CASE-30, MCP-06. `current-architecture.md`
`:142`, `:514`, `:526` (F6 lives here too), `:634`. **Neither FRD-07 `###`
heading is renamed** — ten capability rows and ADR-0013 use them as anchors.

`docs/operations.md` is left alone: it records the deployed estate, and this
ticket deploys nothing.

## Answer to F2 — `EvaEvidenceStatus.Corrected` is removed

Every reader was found. `IsAccepted` (`CaseEvaMapping.cs:32`) treats `Corrected`
and `Accepted` identically. The four sites that produce it are all in
`EvaHandoffStore`. It is copied into `EvaFieldProvenance.Status`, which after
F3 nothing reads and which no longer reaches any file. Once `MapForProduction`
goes, the last code that could branch on the difference goes with it. A status
that no consumer can observe is not a distinction — it is a second name for
`Accepted`, and the repo's "one list per concept" rail says a state vocabulary
lives in one place with no redundant members. **Removed.** If provenance ever
becomes observable again, the fact it recorded — that a staff correction
produced the value — still exists upstream in `CaseDataSourceKind.StaffCorrection`
and `CaseDataCodes.StaffCorrection`, which is where it is authoritative; nothing
is lost that cannot be re-derived.

## Findings recorded, not fixed here

- **The Review gate on Export is UI-only.** `Details.cshtml:36`'s own comment
  claims it is a Core precondition; `IExportCaseBundle.ExecuteAsync` has no
  lifecycle check. Pre-existing. It matters more now that Export records a
  business event, but closing it means re-imposing part of the bar this ticket
  deletes, and which part is a product decision. PR finding + ticket.
- **ADR-0021:55-58 names two MCP tools this ticket deletes.** ADR bodies are
  immutable; the repair is a superseding ADR, which this ticket was not asked
  to write. PR finding.
- **`current-architecture.md:157` lists "EVA export" as absent** while `:526`
  and `:634` describe it as implemented. Pre-existing contradiction in a file I
  am editing; corrected only where my own edits already touch, and named.
- **F4, F5 from ENG-014's review are skipped** — F4 is outside this diff, F5 is
  a new test asset and suite, not a cheap fold-in.
- **The CRLF pin is not guarded by CI** (every .NET test job is Windows;
  production is Linux). Carried into the PR body as a caution.

## Simplification pass

_To be completed before the PR._

---

## Simplification pass — 2026-08-24

Run over this branch's own diff (`git diff task/eng-015-eva-field-values...HEAD`)
with `/simplify`'s four lenses via the `code-simplifier` agent, plus my own
review of the same diff.

### Applied

1. **Two dead `using`s in `EvaBundleSchema.cs`** — `System.Buffers.Binary`
   (only consumer was the deleted `EvaHandoffCommandPolicy.Append`) and
   `Pegasus.Core.Workflow` (only consumer was `CaseLifecycleState` in the
   deleted `EvaHandoffEligibility`/`Evaluate`).
2. **Dead `using Pegasus.Infrastructure.Eva`** in `EvaHandoffStore.cs` — the
   store takes the Core port `IEvaHandoffProxy` and never names
   `LocalEvaHandoffProxy`.
3. **`EvaBundleSchema.SchemaVersion` removed.** Its only two callers —
   `EvaHandoffStore` stamping `EvaHandoffRevisionEntity.SchemaVersion`, and
   `EvaHandoffPersistenceTests` — are both deleted by this ticket. Confirmed by
   grep that no doc, script or migration cites the literal.
4. **`ValidateSource` narrowed to return `EvaReplayFields`.** F3 removed the
   dead provenance-array rebuild but left the method reassembling a whole
   `EvaBundleSource` for one member `CreateOfflineReplay` reads. Every throw is
   unchanged; the output bytes are unchanged.
5. **`BuildEvidence`'s `includeSuggestions` flag removed** (mine, during
   implementation). Its own doc-comment called it "the whole difference between
   the hand-off and an operator export"; with one act there is one answer. This
   simplified `FromCaseField`, `Fallback` and `VehicleModel` with it.
6. **Two stale comments corrected** — `ToReplayFields`' claim that it exists so
   "the hand-off and an operator export can never drift into two orders", and
   `CaseOperatorExportTests`' class doc still describing "both halves" after
   both hand-off halves were deleted.
7. **Three stray double blank lines** left by the deletions.
8. **`Details.cshtml` uses `.btn:disabled`, not `.btn.is-disabled`** (mine).
   The CSS documents `is-disabled` as the fallback "for a disabled action
   rendered as a link"; the control is now a real `<button>`, so the native
   state is the convention.

### Applied — a real defect the pass surfaced

9. **A failed proxy write would have reached the generic 500 page.**
   `DbUpdateException` derives straight from `Exception`, so it missed the
   Export page's catch filter — and this route had never written anything
   before, so nothing had ever needed to. Fixed by translating it in
   `EvaHandoffStore`, which is where the deleted hand-off already translated
   `DbUpdateConcurrencyException`, rather than by importing EF into a page. A
   failed record now fails the whole export instead of handing over a file
   whose "first sent to Engineer" fact was silently lost.
10. **The once-per-case race** (mine, found while reviewing my own code). A
    double-pressed Export could have both requests read "no proxy" and both
    insert. Rather than hold a `Serializable` transaction — which converts the
    race into a deadlock rather than removing it — the primary key on `CaseId`
    is now the enforcement, and losing the race is treated as the success it is,
    but only after re-reading and confirming the row is present.

### Found, not applied — with reasons

- **`EvaAcceptedCaseEvidence.CaseId/CaseVersion/CaseAccepted/InstructionComplete/ImagesComplete`
  are now write-only**, their only reader having been `ValidateAcceptedEvidence`.
  Not removed: `QdosBoundaryContractTests.AnIncompleteCaseWithAnUnacceptedAddressStillExports`
  deliberately sets `InstructionComplete = false` as the regression pin for this
  ticket's central behaviour change, and removing the member would gut that
  test's meaning. It is also a public Core contract change for zero behavioural
  gain. **A ticket decision, not a cleanup** — raised in the PR.
- **`EvaAddressResolution.Mode`, and transitively `EvaInspectionMode`, have no
  remaining reader** — `Mode`'s only consumer was the deleted `IsResolved`.
  The largest remaining orphan. Not applied for the same reason: collapsing
  `EvaAddressResolution` to a bare `EvaEvidenceValue` reshapes a Core contract
  and rewrites about five test construction sites. **Raised in the PR.**
- **`SelectedDocument.CaseId` / `VersionDocumentId`, `ImageEntry.Sha256`,
  `ExportCaseBundleResult.IsExported`** — all unread, all unread on the base
  branch too. Pre-existing, outside this diff.
- **`LoadEligibleImagesAsync` filters `ContentLength <= int.MaxValue` after
  eligibility**, so a case whose every eligible image exceeded that would be
  told "At least one stored vehicle image is required", which would be untrue.
  Pre-existing, not introduced here. **Raised in the PR.**
- **F4 and F5 from ENG-014's review** — F4 is a test comment outside this diff;
  F5 is a new golden-file suite, not a cheap fold-in. Both named in the PR.
- **`EvaHandoffStore`, `EvaHandoffEntities.cs` and `EvaHandoffModelConfiguration.cs`
  keep names describing a deleted act.** A rename is a real clarity win and
  should happen; not done here because this branch is third in a four-deep
  stack and a rename widens the conflict surface across all four for no
  behavioural gain. The class doc-comment says what the type now is and records
  the rename as outstanding.
- **`CaseEvaMapping.ActivationGateReason` still reads "EVA hand-off is not
  switched on."** Operator-facing message text is a closed, operator-approved
  list; rewording one is not this ticket's authority. Named in the PR.
