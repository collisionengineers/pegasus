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
| Those `EngineerActivityReportPersistenceTests` helpers are `private static` and cannot be called from another test class. | VERIFIED (plan review) — `grep -n 'private static .*Query(\|SeedEstateAsync\|SeedCaseAsync' tests/Pegasus.IntegrationTests/EngineerActivityReportPersistenceTests.cs` |
| `RetainedMailboxMessageEntity.ExternalReceiptToken` carries a **non-unique** index, so one token may map to several retained rows. | VERIFIED (plan review) — `sed -n '75,90p' src/Pegasus.Infrastructure/Persistence/MailboxModelConfiguration.cs` (line 82) |
| `IntakeAssociations.Current` holds only active manual and accepted links; a never-associated or reversed receipt is absent from the dictionary. | VERIFIED (plan review) — `sed -n '40,80p' src/Pegasus.Infrastructure/Persistence/CurrentIntakeAssociations.cs` |
| The Inbox sender rule is `EffectiveSenderAddress ?? SenderDisplayName ?? SenderAddress ?? "Sender not recorded"`. | VERIFIED (plan review) — `sed -n '300,310p' src/Pegasus.Web/Pages/Mail/Index.cshtml.cs` |
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
- **Selection is settled**: the existing Inbox `Queries` destination set
  through `MailOperationalDestinationPolicy.Query` (open question resolved
  2026-09-03). No branch remains, and no new classification list.
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
| verify only | `docs/design/test-ui/**` | Not committed by this lane ([[UIIMP-014]] / [[CASE-038]] own it); `-Verify` must show no diff |

Not touched: `Details.cshtml(.cs)`, `_CaseWorkspaceNav.cshtml`,
`Pages/Mail/**`, `DependencyInjection.cs`, `Persistence/Migrations/**`,
`site.css`/`site.js`, `docs/design/test-ui/**`, every other lane's file in
`files.md`. Dependency: [[CASE-038]] (single-scroll frame) holds the
`Pages/Cases/Shared/*` and `OperatorLabels.cs` locks while in flight; the frame
will change how `Details.cshtml` includes `_CaseFiles`, but `_CaseFiles.cshtml`
stays the caller of `_CaseCorrespondence`, so steps 1–2 can proceed now and
steps 3–5 run after CASE-038 merges, after `git merge --no-edit origin/dev`.

**Shared files beyond the CASE-038 lock [review finding 2].** Two files in this
plan also appear in other wave-3 lanes' file maps and must be serialized, not
merely sequenced behind CASE-038:

| Shared file | Also claimed by |
| --- | --- |
| `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs` | [[CASE-029]] (Recipient/Reason projection), [[CASE-040]] (sign-off identity projection) |
| `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | [[CASE-038]], [[CASE-029]], [[CASE-040]] |

Before editing either, refresh with `git merge --no-edit origin/dev` and confirm
with the orchestrator that no other lane holds the file in flight. The additions
here are new members and new test methods, so they merge cleanly only while the
lanes do not land simultaneously.

## Steps

1. **Core read model** — `src/Pegasus.Core/Cases/CaseQueries.cs`.
   Reuses `CaseDetails` and its existing init-list convention (`Tasks`,
   `Custody`) and the Core `MailCategory` type. Add
   `public sealed record CaseQueryEmail(Guid RetainedMessageId,
   DateTimeOffset ReceivedAtUtc, string? EffectiveSenderAddress,
   string? SenderDisplayName, string? SenderAddress, string? Subject,
   MailCategory Classification)` — `EffectiveSenderAddress` so the row can
   apply the Inbox's own sender precedence rather than a second rule
   (review finding 5); and
   `public IReadOnlyList<CaseQueryEmail> QueryEmails { get; init; } = [];`
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
   reply-context); (b) `CurrentIntakeAssociations.ReadAsync(context, ids)`,
   keeping only receipts whose association **is present and** names this case —
   `associations.Current.TryGetValue(receiptId, out var current)
   && current.CaseId == query.CaseId`. Never the indexer: `Current` holds only
   active manual and accepted links, so a qualifying receipt that was never
   associated is simply absent and an indexer read would throw
   (`CurrentIntakeAssociations.cs` lines 54-76; review finding 1). A reversed
   association is likewise absent, so it excludes the row — no allocation
   stand-in. (c) load the retained rows for those tokens (`Id`,
   `ReceivedAtUtc`, `SenderDisplayName`, `SenderAddress`, `Subject`) and the
   receipt's `MailRouteDecision.EffectiveSenderAddress`.
   **Cardinality [review finding 1]:** `HasIndex(item => item.ExternalReceiptToken)`
   on `RetainedMailboxMessageEntity` is **not** unique
   (`MailboxModelConfiguration.cs` line 82), so one token can carry more than
   one retained row. The table lists retained messages — one row per retained
   row, each with its own `/Inbox/{id}`; no dedupe rule is invented. (d) order
   newest first (`ReceivedAtUtc` descending, then `Id`); (e) set `QueryEmails`
   on the returned `CaseDetails`.
   **Selection (settled, no branch remains):** every Queries-destination
   message — `MailOperationalDestinationPolicy.Query(MailOperationalDestination.Queries)`,
   translated exactly as `EfRetainedMailboxMessageStore.ApplyClassificationFilter`
   does (`Families` via `MailTaxonomy.CategoryName`, plus `ExactClassification`
   direction, family and subtype), so `Billing/billing-query` is included. That
   method is `private static` on `EfRetainedMailboxMessageStore`, so it cannot
   be called: the plan repeats its EF translation only, while Core's
   `MailOperationalDestinationPolicy` stays the one owner of the classification
   set — no second list (review finding 6). The post-report-only alternative is
   deleted (open question resolved 2026-09-03; review finding 4).
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
   Sender applies the Inbox's own precedence —
   `EffectiveSenderAddress ?? SenderDisplayName ?? SenderAddress` — the rule in
   `Pages/Mail/IndexModel.SenderLine` (`Pages/Mail/Index.cshtml.cs` lines
   305-309). **No callable helper fits [review finding 5]:** `SenderLine` is
   bound to `RetainedMailSummary` and lives in `Pages/Mail/**`, which is
   [[MAIL-026]]'s tree, so it is neither callable nor extractable from this
   lane; the plan reuses the precedence, not the code, and does **not** copy
   its `Sender not recorded` literal — a wholly unrecorded sender renders an
   empty cell (no invented copy, no second label). Classification cell
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
   `EngineerActivityReportPersistenceTests.Query(...)`. **That file's `Query`,
   `SeedEstateAsync` and `SeedCaseAsync` are `private static` and are not
   callable from `RetainedMailPersistenceTests` [review finding 6]:** the new
   test builds its own local case-and-association fixture from
   `RetainedMailPersistenceTests`' existing `Message`, `RetainAsync` and
   `StoreClassifiedReceiptAsync` helpers and `LocalDbTestDatabase`; a shared
   extraction across the two test classes is reported as a follow-up, not done
   here.
   - `CaseFilesRendersQueriesTableForLinkedQueryMailAndNoManualControls`:
     one `CaseQueryEmail` → asserts heading text `Queries`, the four column
     headers, the received time, sender, subject, classification label, an
     `href` of `/Inbox/{id}`; covers a staff-forward row (effective sender
     preferred over the forwarding envelope) and a row with no sender recorded
     at all (empty cell, no invented text) — review finding 5; asserts no
     `<form>`, `<button>` or `disabled`
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
     reversed (`IsActive = false`), (vi) `Billing/billing-query` linked to the
     case (unconditional — the destination set is settled; review finding 4),
     (vii) a qualifying receipt with **no** association at all, and (viii) two
     retained rows sharing one external receipt token (review finding 1);
     asserts
     `EfCaseQueryStore.GetAsync` returns exactly the qualifying rows newest
     first with retained-message id, received time, sender, subject and
     classification, includes (vi), excludes (iii), (iv), (v) and (vii)
     without throwing, and returns both rows of (viii).
   No existing assertion is weakened; fixture values follow the file's own
   estate (D43 permits the mockup's values; no `corpus/` file is committed).

5. **Snapshots and verification [review finding 2].** `docs/design/test-ui/**`
   is [[UIIMP-014]]'s (and `case-details--default` is [[CASE-038]]'s single
   regenerate row); CASE-009's own `files.md` lists it under "not to touch", so
   **this lane commits no snapshot**. No existing snapshot state carries linked
   Query mail, so the Queries section is absent from every captured state and
   the routed output must stay byte-identical: run
   `./scripts/Update-TestUiSnapshots.ps1 -Verify` and
   `./scripts/Test-UiCatalogue.ps1` to prove it. If a diff does appear, stop and
   report for a UIIMP-014 handoff rather than committing the file. No migration,
   so `Test-MigrationGrants.ps1` is not applicable.

## Commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
./scripts/Update-TestUiSnapshots.ps1 -Verify
./scripts/Test-UiCatalogue.ps1
```

## Acceptance conditions

- `/Cases/{id}?section=case-files` (or the Files section of the CASE-038
  frame) shows a `Queries` table when at least one retained email currently
  linked to that Case matches the selected classification set; columns are
  Received, Sender, Subject, Classification and an Open message link to
  `/Inbox/{retainedMessageId}`.
- With no qualifying linked email the Queries heading and table are absent.
- Rows for another Case, a non-Query classification, a reversed association or
  no association at all never appear; ordering is newest first.
- No Raise a Query, Reply, Resolve, Mark resolved, Reason or association
  control exists in the section, enabled or disabled; no mailbox write path
  is added.
- The three named tests pass; every command above exits 0; no migration, DI,
  `Details.cshtml` or `docs/design/test-ui/**` change is in the diff.

## Stop condition

Stop and report (do not improvise) if CASE-038 still holds the shared lock when
step 3 is reached, if CASE-029 or CASE-040 is in flight on `EfCaseQueryStore.cs`
or `CaseDetailsWebTests.cs`, if `Update-TestUiSnapshots.ps1 -Verify` reports a
snapshot diff (a UIIMP-014 handoff, not a commit here), if a needed change falls
outside the owned files, or if any command fails. Otherwise: PR open against
`dev` titled with `Kanmer: CASE-009`, post-implementation report written,
ticket in Review. Never merge; never start the next ticket.

## Resolutions (2026-09-03)

- Controller: the Queries table is filled from the existing Inbox `Queries` destination set through the existing policy.
- Controller: heading and table absent when nothing qualifies; no `No queries` label.

## Plan review (2026-09-03, gpt-5.6-sol xhigh; dispositions Claude Opus)

Reviewer verdict: REQUEST CHANGES. Every finding is dispositioned below; the
plan text above already carries the fixes.

| # | Severity | Step | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | 2, 4 | The `Current[...]` indexer assumes an association always exists, and "one row per receipt" assumes `ExternalReceiptToken` is unique. | **Fixed.** Verified: `IntakeAssociations.Current` holds only active manual and accepted links (`CurrentIntakeAssociations.cs` 54-76), and `HasIndex(item => item.ExternalReceiptToken)` is non-unique (`MailboxModelConfiguration.cs` 82). Step 2 now uses `TryGetValue` and states one row per retained row; step 4 adds a never-associated receipt and a shared-token pair. |
| 2 | blocker | 5, Files | Ownership unreconciled: the plan commits `docs/design/test-ui/**`, which its own `files.md` forbids and [[UIIMP-014]] owns; `EfCaseQueryStore.cs` and `CaseDetailsWebTests.cs` also sit in CASE-029/CASE-040/CASE-038 file maps. | **Fixed.** Verified against `UIIMP-014/files/files.md`, `CASE-029/files/files.md`, `CASE-040/files/files.md`, `CASE-038/files/files.md`. Step 5 commits no snapshot and verifies a nil diff (stop and hand off otherwise); a shared-files table and the stop condition serialize the two shared source files. |
| 3 | blocker | 3 | The design authority requires column headers to be server-side sort links; the Queries table has none. | **Rejected.** That rule governs filterable index/list tables with a page-model handler. No table in `Pages/Cases/Shared/*` carries sort links (`grep -rn "sort" src/Pegasus.Web/Pages/Cases/Shared/*.cshtml` returns nothing across `_CaseDocuments`, `_CaseWorkflow`, `_CaseVehicle`), and a sort parameter would need a `Details.cshtml.cs` handler owned by another lane. The existing convention wins; the newest-first default is honoured. |
| 4 | should-fix | 2, Resolutions | The classification choice is resolved, yet step 2 still offers two branches and step 4 tests Billing conditionally. | **Fixed.** The post-report-only branch is deleted and the `Billing/billing-query` case is unconditional. |
| 5 | should-fix | 3 | The claimed Inbox sender reuse is inaccurate: the real rule is `EffectiveSenderAddress → SenderDisplayName → SenderAddress → "Sender not recorded"`. | **Fixed.** Verified at `Pages/Mail/Index.cshtml.cs` 305-309. `CaseQueryEmail` gains `EffectiveSenderAddress`, the view applies the same precedence, and the missing-value literal is not copied (empty cell). `SenderLine` is bound to `RetainedMailSummary` inside MAIL-026's `Pages/Mail/**`, so no callable helper fits — the plan now says so. Tests cover a staff forward and a wholly missing sender. |
| 6 | should-fix | 2, 4 | Named "reuses" are private and not callable (`ApplyClassificationFilter`; `Query` / `SeedEstateAsync` / `SeedCaseAsync`). | **Fixed.** Verified. Step 2 now states that only the EF translation is repeated while Core's policy stays the one owner of the set; step 4 states no callable fixture helper fits, builds a local one, and reports a shared extraction as a follow-up. |
| 7 | should-fix | Commands | `Category!=Browser` weakens the canonical delivery gate. | **Fixed.** Verified `AGENTS.md` line 161 and `docs/runbook.md` line 313: the solution-wide gate is `--filter "Category!=Corpus"`. The commands now use it, and the redundant capture step is replaced by `-Verify`. |

The reviewer also confirmed that no staff review flag or action (D44), no damage
type (D45) and no D46 surface appear anywhere in this plan, and that it adds no
package, migration, grant, DI registration, mailbox mutation, explanatory copy
or second classification list.
