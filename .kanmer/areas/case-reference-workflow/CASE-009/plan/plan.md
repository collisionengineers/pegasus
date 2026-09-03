# Plan — CASE-009 (2026-09-02, gpt-5.6-terra high)

Planned against `origin/dev` 897db953 (DELIV-041, #647) in the read-only
research checkout. Wrapper (Claude) corrections to the model output are
marked **[wrapper]**; every premise below was re-checked by the wrapper with
the listed read-only command.

## Verification ledger

| Premise | Status — command |
| --- | --- |
| Planning checkout is detached `origin/dev` at `897db953`; tree clean. | VERIFIED — `git log -1 --oneline`; `git status --porcelain` |
| `GetCase` obtains `CaseDetails` through `ICaseQueryStore.GetAsync`; `EfCaseQueryStore` is the composed adapter. | VERIFIED — `rg -n 'ICaseQueryStore' src/Pegasus.Core/Cases/CaseQueries.cs src/Pegasus.Infrastructure/DependencyInjection.cs` |
| `CaseDetails` is a positional record with `init`-only supplementary lists (`Tasks`, `Custody`); the only `new CaseDetails(` call site is `EfCaseQueryStore`. | VERIFIED — `git grep -n "new CaseDetails(" -- src tests` (1 hit) |
| Retained rows carry `Id`, `ExternalReceiptToken`, `SenderAddress`, `SenderDisplayName`, `Subject`, `ReceivedAtUtc`; no `CaseId`, no classification. | VERIFIED — `sed -n '45,75p' src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs` |
| Classification is `IntakeReceiptEntity.MailClassificationDecision` (`Outcome`, `Direction`, `Family`, `Subtype`, `OtherName`); current case link is `CurrentIntakeAssociations.ReadAsync` (manual association wins, reversed never stands in, else `CaseIntakeLinks`). | VERIFIED — `cat src/Pegasus.Infrastructure/Persistence/CurrentIntakeAssociations.cs` |
| The Inbox already turns `MailOperationalDestinationPolicy.Query(destination)` into an EF predicate over receipts joined by `ExternalReceiptToken`: `EfRetainedMailboxMessageStore.ApplyClassificationFilter` (lines 815–855). | VERIFIED **[wrapper]** — `sed -n '815,855p' src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` |
| `Queries` destination = family `PostReportEmails` (`query`, `dispute`, `amendment-request`) plus exact `Billing/billing-query`. | VERIFIED — `sed -n '94,104p' src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs` |
| `EfEngineerActivityQueries.GetAsync` is the post-report-only precedent (`MailTaxonomy.CategoryName(ReceivedMailFamily.PostReportEmails)` → `CurrentIntakeAssociations.ReadAsync`). | VERIFIED — `sed -n '1,120p' src/Pegasus.Infrastructure/Persistence/EfEngineerActivityQueries.cs` |
| Persisted decision → `MailCategory` is `MailCategory.Received(MailTaxonomy.ParseReceivedFamily(Family), Subtype, IsReplyContext)` (`EfRetainedMailboxMessageStore` line 535). | VERIFIED **[wrapper]** |
| `?section=case-files` renders `_CaseFiles.cshtml`; that partial's comment records "Correspondence is absent". | VERIFIED — `grep -n -C6 case-files src/Pegasus.Web/Pages/Cases/Details.cshtml`; `cat src/Pegasus.Web/Pages/Cases/Shared/_CaseFiles.cshtml` |
| Message page is `/Mail/Message` at URL `/Inbox/{id:guid}`. | VERIFIED — `head -1 src/Pegasus.Web/Pages/Mail/Message.cshtml` |
| Heading label: `OperatorLabels.MailOperationalDestinationLabel(MailOperationalDestination.Queries)` returns exactly `Queries`; classification label: `OperatorLabels.MailClassification(MailCategory)` (line 929); `OperatorLabels.CaseWorkspace` holds the Files labels. | VERIFIED **[wrapper]** — `grep -n 'MailOperationalDestinationLabel\|string MailClassification\|class CaseWorkspace' src/Pegasus.Web/Presentation/OperatorLabels.cs` |
| Design authority: "In read-only view, a section with nothing recorded and no available action is absent — not an empty-state panel"; `.empty` "renders only where an action exists"; "tables sort newest first". | VERIFIED **[wrapper]** — `sed -n '670,676p;798p' docs/design/README.md` |
| `CaseDetailsWebTests` has `RecordingCaseDetailsStore : IGetCase` and the `?section=case-files` theory (line 159). | VERIFIED — `grep -n 'class RecordingCaseDetailsStore\|section=case-files' tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` |
| `RetainedMailPersistenceTests` has `Message(...)` (l.1308), `RetainAsync(...)` (l.1339), `StoreClassifiedReceiptAsync(...)`; `EngineerActivityReportPersistenceTests.Query(...)` (l.103) builds a classified receipt with an `IntakeManualAssociationEntity`, `SeedEstateAsync`/`SeedCaseAsync` seed a case. | VERIFIED **[wrapper]** |
| No `CaseQueryEmail`/`_CaseCorrespondence` name exists yet. | VERIFIED **[wrapper]** — `git grep -n 'CaseQueryEmail\|CaseCorrespondence' -- src tests` (none) |
| No migration and no DI change are needed: every projection key and display field is on existing tables and `EfCaseQueryStore` is already registered. | VERIFIED — entity reads above; `rg -n EfCaseQueryStore src/Pegasus.Infrastructure/DependencyInjection.cs` |
| Shared-lock state: `Pages/Cases/Shared/*` and `Presentation/OperatorLabels.cs` are EPIC-012 capacity-one paths; the frame lane is [[CASE-038]] (preparing); [[CASE-027]] (Verifying, PR #631 merged) no longer holds `_CaseFiles.cshtml`. | VERIFIED **[wrapper]** — board files `areas/case-reference-workflow/CASE-038/CASE-038.md`, `CASE-027/CASE-027.md` |

## Objective

Render a read-only **Queries** table in the Case Files section listing the
retained emails currently linked to this Case whose classification is a
Query. Each row opens the retained message; nothing creates, replies to,
resolves, associates or mutates correspondence.

## Decisions taken by this plan

- **Empty state = absence [wrapper].** The ticket's "truthful empty state" is
  satisfied by the design authority's rule for read-only sections with nothing
  recorded and no action: the Queries block (heading and table) is absent when
  no qualifying row exists. No `No queries` label, no `.empty` panel, no prose.
  The test proves absence of the heading and of every manual control.
  Recorded as an operator confirmation in `open-questions/`.
- **One heading, `Queries` [wrapper].** The partial renders one `<section>`
  whose `<h2>` is `Queries`, a sibling of Instruction photographs and Vehicle
  images. No `Correspondence` wrapper label is added: Compose, Reply, Forward
  and Open Inbox are [[MAIL-026]]'s and are absent here (D21), and MAIL-026
  extends this partial when it lands.
- **Selection is a one-line choice inside step 2**, made once the open board
  question is answered; neither branch adds a classification list.
- **Newest first** (design README) — `ReceivedAtUtc` descending, then `Id`.

## Design rules that bind

No explanatory copy (no hint, empty-state or how-it-works text); labels only
in `Presentation/OperatorLabels.cs`; exact state labels (heading string comes
from `MailOperationalDestinationLabel`, classification from
`MailClassification`, never a literal); absent versus disabled (D7/D21 —
Raise a Query, Reply, Resolve, Mark resolved, Reason and any association
control are absent, never drawn disabled); read-only view renders only
populated sections.

## Files

| Action | Path | Responsibility |
| --- | --- | --- |
| change | `src/Pegasus.Core/Cases/CaseQueries.cs` | `CaseQueryEmail` read row; `CaseDetails.QueryEmails` (init, default `[]`) |
| change | `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs` | Read-only projection inside `GetAsync` |
| change | `src/Pegasus.Web/Pages/Cases/Shared/_CaseFiles.cshtml` | Production caller of the new partial (replaces the "Correspondence is absent" comment) |
| create | `src/Pegasus.Web/Pages/Cases/Shared/_CaseCorrespondence.cshtml` | The Queries section |
| change | `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Column and link labels only |
| change | `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | Rendering, absence and no-control coverage |
| change | `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | EF projection coverage |
| generated | `docs/design/test-ui/**` | Only the output of `Update-TestUiSnapshots.ps1` if the case-files capture changes (capacity-one path) |

Not touched: `Details.cshtml(.cs)`, `_CaseWorkspaceNav.cshtml`,
`Pages/Mail/**`, `DependencyInjection.cs`, `Persistence/Migrations/**`,
`site.css`/`site.js`, every other lane's file in `files.md`. Dependency:
[[CASE-038]] (single-scroll frame) holds the `Pages/Cases/Shared/*` and
`OperatorLabels.cs` locks while in flight; the frame will change how
`Details.cshtml` includes `_CaseFiles`, but `_CaseFiles.cshtml` stays the
caller of `_CaseCorrespondence`, so steps 1–2 can proceed now and steps 3–5
run after CASE-038 merges, after `git merge --no-edit origin/dev`.

## Steps

1. **Core read model** — `src/Pegasus.Core/Cases/CaseQueries.cs`.
   Reuses `CaseDetails` and its existing init-list convention (`Tasks`,
   `Custody`) and the Core `MailCategory` type. Add
   `public sealed record CaseQueryEmail(Guid RetainedMessageId,
   DateTimeOffset ReceivedAtUtc, string? SenderDisplayName,
   string? SenderAddress, string? Subject, MailCategory Classification)`
   and `public IReadOnlyList<CaseQueryEmail> QueryEmails { get; init; } = [];`
   on `CaseDetails`. The positional constructor is unchanged, so no fake or
   caller outside this ticket changes. No new port: `ICaseQueryStore.GetAsync`
   already returns `CaseDetails`.

2. **Projection** — `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs`,
   inside `GetAsync`, alongside the `availableReportSentEvidence` read.
   Reuses `EfRetainedMailboxMessageStore.ApplyClassificationFilter`'s
   predicate shape (receipt `SourceChannel == "mailbox"`, decision
   `Outcome == "classified"`, `Direction == "received"`, family/exact-subtype
   match), `CurrentIntakeAssociations.ReadAsync`, the retained-message join by
   `ExternalReceiptToken`, and the decision→category mapping
   `MailCategory.Received(MailTaxonomy.ParseReceivedFamily(Family), Subtype,
   IsReplyContext)`. Sequence: (a) select classified mailbox receipts matching
   the selection below (`Id`, `ExternalReceiptToken`, family, subtype,
   reply-context); (b) `CurrentIntakeAssociations.ReadAsync(context, ids)` and
   keep only receipts whose `Current[...].CaseId == query.CaseId` (a reversed
   association therefore excludes the row — no allocation stand-in); (c) load
   the retained rows for those tokens (`Id`, `ReceivedAtUtc`,
   `SenderDisplayName`, `SenderAddress`, `Subject`); one row per receipt — the
   implementer verifies the retained-row uniqueness per token with a read-only
   look at `MailboxModelConfiguration` and records it; (d) order newest first;
   (e) set `QueryEmails` on the returned `CaseDetails`.
   **Selection (one line, after the board question is answered):**
   - every Queries-destination message →
     `MailOperationalDestinationPolicy.Query(MailOperationalDestination.Queries)`
     translated exactly as `ApplyClassificationFilter` does (`Families` via
     `MailTaxonomy.CategoryName`, plus `ExactClassification` direction, family
     and subtype);
   - post-report only →
     `decision.Family == MailTaxonomy.CategoryName(ReceivedMailFamily.PostReportEmails)`
     as in `EfEngineerActivityQueries.GetAsync`.
   Not done: any mailbox write, new list, migration, DI change.

3. **View** — `_CaseFiles.cshtml`, new `_CaseCorrespondence.cshtml`,
   `OperatorLabels.cs` (after CASE-038 merges).
   Reuses the `_CaseFiles` sibling-section markup (`<section class="section-gap"
   aria-labelledby>` + `.blockhead` `<h2>`), Razor `<partial>`, the
   `table-wrap` table convention, `OperatorLabels.OfficeTime` for the received
   time, `asp-page="/Mail/Message" asp-route-id="@row.RetainedMessageId"` for
   the link. `_CaseFiles.cshtml`: delete the "Correspondence is absent"
   comment; add `<partial name="Cases/Shared/_CaseCorrespondence" model="Model" />`
   after the documents partial. `_CaseCorrespondence.cshtml`: `@model
   DetailsModel`; `@if (Model.Case!.QueryEmails.Count > 0)` render the section
   (heading `OperatorLabels.MailOperationalDestinationLabel(
   MailOperationalDestination.Queries)`, columns Received, Sender, Subject,
   Classification, and an Open message link cell); otherwise render nothing.
   Sender shows `SenderDisplayName` falling back to `SenderAddress`, the same
   rule the Inbox list applies (implementer names the helper if one exists,
   else the fallback is the one expression in the view). Classification cell
   is `OperatorLabels.MailClassification(row.Classification)`. Labels: grep
   `OperatorLabels.cs` first and reuse an existing constant with the identical
   string; otherwise add to `OperatorLabels.CaseWorkspace`: `Received`,
   `Sender`, `Subject`, `Classification`, `OpenMessage = "Open message"`. No
   form, button, handler, disabled control, or empty-state element.

4. **Tests** — `CaseDetailsWebTests.cs`, `RetainedMailPersistenceTests.cs`.
   Reuses `RecordingCaseDetailsStore` (add a settable `QueryEmails` list
   passed into its `CaseDetails` via `with`/init), `GetHtmlAsync`, the
   `?section=case-files` request pattern; `Message(...)`, `RetainAsync(...)`,
   `StoreClassifiedReceiptAsync(...)`, `LocalDbTestDatabase`, and the
   receipt-with-`IntakeManualAssociationEntity` shape from
   `EngineerActivityReportPersistenceTests.Query(...)` (pattern reused inside
   the owned file; report, do not copy, if a shared helper is warranted).
   - `CaseFilesRendersQueriesTableForLinkedQueryMailAndNoManualControls`:
     one `CaseQueryEmail` → asserts heading text `Queries`, the four column
     headers, the received time, sender, subject, classification label, an
     `href` of `/Inbox/{id}`; asserts no `<form>`, `<button>` or `disabled`
     control inside the section and no "Raise a query", "Reply", "Resolve",
     "Mark resolved" text anywhere on the page.
   - `CaseFilesOmitsQueriesWhenNoLinkedQueryMailExists`: default store →
     `?section=case-files` renders no `Queries` heading, no table and no
     manual control; the existing section theory still reports `Case Files`.
   - `CaseQueryStoreProjectsCurrentlyLinkedQueryMailNewestFirst` (persistence):
     seeds a case; retained+classified receipts for (i) `query` linked to the
     case, (ii) `dispute` linked to the case earlier, (iii) a qualifying
     classification linked to another case, (iv) a non-Query classification
     linked to the case, (v) a qualifying classification whose association was
     reversed (`IsActive = false`), and, under the destination branch only,
     (vi) `Billing/billing-query` linked to the case; asserts
     `EfCaseQueryStore.GetAsync` returns exactly the qualifying rows newest
     first with retained-message id, received time, sender, subject and
     classification, and excludes (iii), (iv), (v).
   No existing assertion is weakened; fixture values follow the file's own
   estate (D43 permits the mockup's values; no `corpus/` file is committed).

5. **Snapshots and verification** — routed page output changes only when a
   snapshot state carries linked Query mail; run the capture, verify and
   catalogue commands and commit only generated `docs/design/test-ui/**`
   differences (capacity-one path; take it after CASE-038 and before
   [[UIIMP-014]] as the orchestrator sequences). No migration, so
   `Test-MigrationGrants.ps1` is not applicable.

## Commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
```

## Acceptance conditions

- `/Cases/{id}?section=case-files` (or the Files section of the CASE-038
  frame) shows a `Queries` table when at least one retained email currently
  linked to that Case matches the selected classification set; columns are
  Received, Sender, Subject, Classification and an Open message link to
  `/Inbox/{retainedMessageId}`.
- With no qualifying linked email the Queries heading and table are absent.
- Rows for another Case, a non-Query classification, or a reversed
  association never appear; ordering is newest first.
- No Raise a Query, Reply, Resolve, Mark resolved, Reason or association
  control exists in the section, enabled or disabled; no mailbox write path
  is added.
- The three named tests pass; every command above exits 0; no migration, DI
  or `Details.cshtml` change is in the diff.

## Stop condition

Stop and report (do not improvise) if the classification selection is still
unanswered when step 2 is reached, if CASE-038 still holds the shared lock
when step 3 is reached, if a needed change falls outside the owned files, or
if any command fails. Otherwise: PR open against `dev` titled with
`Kanmer: CASE-009`, post-implementation report written, ticket in Review.
Never merge; never start the next ticket.

## Resolutions (2026-09-03)

- Controller: the Queries table is filled from the existing Inbox `Queries` destination set through the existing policy.
- Controller: heading and table absent when nothing qualifies; no `No queries` label.
