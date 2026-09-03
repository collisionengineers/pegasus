# Research — CASE-009 (2026-09-02, gpt-5.6-terra medium, wrapper-checked)

Supersedes the 2026-08-21 research, which described an "Engineer queries"
heading and a disabled "Raise a query" button in `_CaseSummary.cshtml`. That
surface no longer exists on `origin/dev` (wrapper: `git grep -n -i "engineer
quer\|raise a quer" -- src tests docs/design` returns nothing). The Case
workspace was re-cut by [[CASE-012]] and [[CASE-027]]; the correct placement
is the Files section (EPIC-012 D30 section order), under Correspondence.

## Wrapper corrections to the model output

- Codex reported the retained-message route as `/Mail/Message/{id}`. The
  Razor page is `Pages/Mail/Message.cshtml` (link with
  `asp-page="/Mail/Message" asp-route-id="..."`), but its URL is
  `@page "/Inbox/{id:guid}"`; the mockup's `/inbox/{mailId}` matches it.
  VERIFIED — `head -1 src/Pegasus.Web/Pages/Mail/Message.cshtml`.
- Codex marked the subtype strings `query`, `dispute`, `amendment-request`
  ASSUMED. They are the `ReceivedMailFamily.PostReportEmails` entries of
  `MailTaxonomy.ConfirmedReceivedSubtypes`. VERIFIED — `sed -n '36,60p'
  src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs`.
- Codex marked CASE-027's file ownership ASSUMED. Board read: CASE-027 is
  Verifying, PR #631 merged, so `_CaseFiles.cshtml` is no longer held by a
  wave-2 lane; it remains inside the EPIC-012 capacity-one shared-lock path
  `Pages/Cases/Shared/*`. VERIFIED — board file
  `areas/case-reference-workflow/CASE-027/CASE-027.md`.
- The research checkout was at `cad00be9`; Codex ran `git checkout
  origin/dev`, moving it to `897db953` (DELIV-041, #647). Working tree clean
  afterwards (`git status --porcelain` empty). Findings below are against
  `897db953`.

## Scope and verification ledger

| Premise | Status |
| --- | --- |
| Checkout is detached at `origin/dev`, commit `897db953`. | VERIFIED — `git rev-parse --verify HEAD; git log -1 --oneline; git branch -a --contains HEAD` |
| The ticket's current board state and EPIC documents could be read live by the model. | ASSUMED by the model (no Kanmer access); the wrapper supplied the verbatim ticket body, scratch notes and D-decisions from the board files. |
| The local SDKs are .NET 10.0.204 and 10.0.303. | VERIFIED — `dotnet --list-sdks` |
| No build or test was run. | VERIFIED — command history for this pass contains only read-only `git`, `rg`, `Get-Content`, and `dotnet --list-sdks` commands. |

## Current behaviour

`CaseDetails` is the existing Core read model. `ICaseQueryStore.GetAsync` is
implemented by `EfCaseQueryStore`; it already projects
`AvailableReportSentEvidence` into `CaseDetails`, but that list is explicitly
unlinked Sent evidence (`CaseId == null`), not inbound correspondence for the
currently viewed case.

| Premise | Status |
| --- | --- |
| `CaseDetails` has `AvailableReportSentEvidence`, and `IGetCase` calls the existing query store. | VERIFIED — `rg -n -C 3 'AvailableReportSentEvidence|IGetCase|ICaseQueryStore' src/Pegasus.Core/Cases/CaseQueries.cs` |
| `EfCaseQueryStore.GetAsync` loads unlinked, system-worker Sent evidence only. | VERIFIED — `Get-Content src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs \| Select-Object -Skip 135 -First 110` |
| DI already composes `EfCaseQueryStore`; no new port registration should be required. | VERIFIED — `rg -n -C 5 'EfCaseQueryStore|ICaseQueryStore' src/Pegasus.Infrastructure/DependencyInjection.cs` |

Inbound retained-mail rows live in `RetainedMailboxMessages`. They contain the
message identifier, external receipt token, sender, subject, and received time,
but no case link or classification. The retained row joins to its
`IntakeReceipts` row by `ExternalReceiptToken`; that receipt holds the
classification decision. Current association is resolved through
`CurrentIntakeAssociations.ReadAsync`, which honours an explicit association or
reversal rather than silently substituting an obsolete allocation.

| Premise | Status |
| --- | --- |
| Retained rows carry ID, external receipt token, sender, subject, and received date. | VERIFIED — `rg -n -C 8 'class RetainedMailboxMessageEntity|ExternalReceiptToken|Sender|Subject|ReceivedAtUtc' src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs` |
| Retained rows do not carry a `CaseId` or classification field. | VERIFIED — same command; the complete entity declaration has neither field (wrapper re-read lines 45-75). |
| Classification is persisted on `IntakeMailClassificationDecisions`, related to an intake receipt. | VERIFIED — `rg -n -C 6 'IntakeMailClassificationDecisionEntity|MailClassificationDecision' src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs src/Pegasus.Infrastructure/Persistence/MailboxModelConfiguration.cs` |
| Current case association is a receipt-level relation resolved by `CurrentIntakeAssociations`. | VERIFIED — `Get-Content src/Pegasus.Infrastructure/Persistence/CurrentIntakeAssociations.cs` |
| The Mail workspace already joins retained mail to its receipt through `ExternalReceiptToken`. | VERIFIED — `Get-Content src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs \| Select-Object -Skip 177 -First 120`; wrapper: lines 138-142 and 213. |

The closest read precedent is `EfEngineerActivityQueries`: it selects mailbox
receipts classified `PostReportEmails`, resolves their current associations,
then relates associated cases to their engineers. CASE-009 can reuse that
association sequence, but must additionally join each receipt back to its
retained message to render operator-visible message data.

| Premise | Status |
| --- | --- |
| Engineer-report "Queries received" reads mailbox receipts with the post-report family and current associations. | VERIFIED — `Get-Content src/Pegasus.Infrastructure/Persistence/EfEngineerActivityQueries.cs \| Select-Object -First 150` |
| `MailOperationalDestinationPolicy` maps the Queries destination to the post-report family plus exact `billing-query`. | VERIFIED — `rg -n -C 8 'MailOperationalDestination\\.Queries|PostReportEmails|billing-query' src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs` |
| The mockup spellings `query`, `dispute`, `amendment-request` are the persisted `PostReportEmails` subtype names. | VERIFIED (wrapper) — `MailTaxonomy.ConfirmedReceivedSubtypes[ReceivedMailFamily.PostReportEmails]` in `MailClassificationContracts.cs` lines 51-52. |

The Case Details page currently uses `?section=` to select exactly one Razor
partial. `case-files` invokes `_CaseFiles.cshtml`, providing a production caller
for a new `_CaseCorrespondence.cshtml` without changing the EPIC-012 frame.
`_CaseFiles.cshtml` currently states that correspondence is absent because no
case-retained-mail query exists.

| Premise | Status |
| --- | --- |
| `?section=case-files` invokes `_CaseFiles.cshtml`. | VERIFIED — `rg -n -C 12 '_CaseFiles|case-files' src/Pegasus.Web/Pages/Cases/Details.cshtml` (line 316-318) |
| `_CaseFiles.cshtml` explicitly records the missing correspondence projection. | VERIFIED — `Get-Content src/Pegasus.Web/Pages/Cases/Shared/_CaseFiles.cshtml` (comment lines 14-15) |
| CASE-027 added `_CaseFiles.cshtml` and its commit is reachable on this checkout. | VERIFIED — `git log --oneline --all -- src/Pegasus.Web/Pages/Cases/Shared/_CaseFiles.cshtml` |
| CASE-027's ownership of `_CaseFiles.cshtml` is released. | VERIFIED (wrapper) — board: CASE-027 status `verifying`, PR #631 merged. |

The presentation label convention is centralized in `OperatorLabels`.
`MailOperationalDestination.Queries` already renders as `Queries`; the
`CaseWorkspace` nested class is the established place for Case-files labels.
This change should add the section/table labels there only after the EPIC-012
capacity-one lock permits it.

| Premise | Status |
| --- | --- |
| `MailOperationalDestination.Queries => "Queries"` exists. | VERIFIED — `rg -n -C 3 'MailOperationalDestination\\.Queries' src/Pegasus.Web/Presentation/OperatorLabels.cs` (line 365) |
| `CaseWorkspace` is the existing Case section-label collection (`FilesPanel = "Files"` etc.). | VERIFIED — `rg -n -C 3 'public static class CaseWorkspace' src/Pegasus.Web/Presentation/OperatorLabels.cs` |
| CASE-009 may change `OperatorLabels.cs` in its own branch. | ASSUMED — the EPIC-012 lock policy reserves this shared path (capacity one) to the frame lane N2 until the frame merges; CASE-009 edits it only sequenced after that. |

`CaseDetailsWebTests` supplies a replaceable `RecordingCaseDetailsStore` and
already asserts section rendering. Retained-mail persistence tests construct
real retained messages and receipt classifications, but there is no existing
Case Details test fixture that joins a retained message, classification, and
case association.

| Premise | Status |
| --- | --- |
| `CaseDetailsWebTests` has `RecordingCaseDetailsStore`, an `IGetCase` fake, and section tests. | VERIFIED — `rg -n -C 5 'class RecordingCaseDetailsStore' tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` (line 1456) |
| Retained-mail integration tests create retained messages and classified receipts using the external receipt token. | VERIFIED — `rg -n -C 5 'RetainedMailboxMessage|ExternalReceiptToken|StoreClassifiedReceiptAsync' tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` (10 uses) |
| A reusable existing Case Details fixture already creates qualifying linked Query correspondence. | VERIFIED false — `rg -n -l 'RetainedMailboxMessage|MailClassificationDecision|CurrentIntakeAssociation' tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` returns no match. |

## Mockup and required read-only interpretation

The mockup (`21-case-sections.js` line 133) renders a Correspondence block
followed by "Post-report queries": columns `Raised`, `Resolved`, `Reason`, an
Open message action, and a Mark resolved control (`ACTIONS['query-resolve']`,
line 137). Its fixture's `m13` has classification `query` and a case ID
(`04-fixtures.js` lines 410, 503). The notes (backend gap 9) identify
resolution, reason, and AI drafting as a backend gap.

| Premise | Status |
| --- | --- |
| The mockup contains the correspondence block, post-report query columns, and Open/Mark-resolved actions. | VERIFIED — `rg -n -C 5 'Post-report queries|query-resolve|Open message|Mark resolved' C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/21-case-sections.js` |
| The fixture has QDOS26012 query `m13`. | VERIFIED — `rg -n -C 4 'QDOS26012|mailId: .m13.|classification: .query.' C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/04-fixtures.js` |
| The notes call out post-report-query resolution, reason, linked message, and AI drafting as missing backend work. | VERIFIED — `rg -n -C 4 'Post-report queries' C:/Users/PC/Downloads/Pegasus_UI_v2_notes.md` |

The ticket controls over the mockup where they conflict. CASE-009 renders a
read-only **Queries** table beneath Files → Correspondence, with a truthful
no-rows state and an Open message link to the `/Mail/Message` page
(URL `/Inbox/{id:guid}`, the retained-message id).

Truthful fields available now are: received date, sender, subject,
classification/family, and the retained-message ID for the link. Do not render
`Resolved`, `Reason`, or `Mark resolved`: no supporting storage exists in the
verified retained-message entity, and the ticket forbids resolution and every
mutation. No "Raise a Query" or reply control is to be added (Compose, Reply
and Forward are MAIL-026).

## Gaps and implementation direction

- Add a query-email read-row record to the existing Case Details read model,
  plus a list on `CaseDetails` (empty when none).
- Extend `EfCaseQueryStore.GetAsync` with one read-only projection:
  qualifying classified mailbox receipts → current case association
  (`CurrentIntakeAssociations`) → matching retained message by external
  receipt token. Order by received date; project only the facts the table
  shows.
- Change `_CaseFiles.cshtml` to render the new correspondence partial. Its
  existing Details-page inclusion (`?section=case-files`) is the production
  caller.
- Create `_CaseCorrespondence.cshtml` for the `Queries` panel and no-rows
  state. It contains only the message link; no form, mutation handler, mail
  action, or manual association control.
- Add integration coverage for qualifying rows, exclusion of another case and
  another classification, empty state, correct heading, and absence of all
  manual/resolve controls.
- No migration is indicated: existing tables provide all required projection
  keys and display fields.

## Reuse and risks

Reuse `CaseDetails`, `ICaseQueryStore`, `EfCaseQueryStore.GetAsync`,
`CurrentIntakeAssociations.ReadAsync`, `MailOperationalDestinationPolicy.Query`,
`MailTaxonomy.CategoryName(ReceivedMailFamily.PostReportEmails)`,
`EfEngineerActivityQueries`' association sequence,
`RecordingCaseDetailsStore`, and the `CaseDetailsWebTests` section-query
pattern. Labels: `OperatorLabels.MailOperationalDestination.Queries` and the
`CaseWorkspace` class.

Primary risk: `MailOperationalDestination.Queries` includes both post-report
mail and `billing-query`, while the ticket calls for Query-classified linked
email and D12 refers specifically to post-report mail. The implementation must
not create a second taxonomy or duplicate classification rules; it selects
through the existing policy or family.

A second risk is sequencing: `Pages/Cases/Shared/*` and `OperatorLabels.cs`
are EPIC-012 capacity-one paths. The lane runs after the frame (N2) merges
and refreshes with `git merge --no-edit origin/dev`.

## Operator-only open questions

- Should this Case Details panel include every message in the existing
  `MailOperationalDestination.Queries` destination (post-report `query`,
  `dispute`, `amendment-request` plus Billing `billing-query`), or only the
  `PostReportEmails` family as D12 describes?
