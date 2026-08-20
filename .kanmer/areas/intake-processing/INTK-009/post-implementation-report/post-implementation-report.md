# Post-implementation report — INTK-009

## What shipped

1. **Unidentified is a tab on Queues** (`/Triage?queue=unidentified`), with
   Images/E-mails/All filters, following the exact `queue=`/subtab convention
   `not_ready|review|held|triage` already used.
2. **Not ready gained an Instruction-initiated/Image-initiated/All origin
   filter**, rendering the existing Cases table for the formal-Case origin
   and a new Image-initiated table (VRM reference, registration, lifecycle
   status, received date) for the ImageIntake-backed origin, each labelled
   only when both are shown together.
3. **Row content answers "what is going on"**: each Unidentified row shows
   the U-reference, kind (Image/E-mail/Document), an operator-meaningful
   handle (original filename, or subject + sender for e-mail — never a
   GUID), office-time received, and the reason label, in one line.
4. **`/Unidentified` is a permanent redirect** to the tab (kept for existing
   links/bookmarks); its Details page is rebuilt to the design rule (one H1
   = the U-reference, no eyebrow/lede, a single "Concerns" line, one link to
   the retained file, history, resolve form — no asset/evidence link wall,
   no "Intake receipt — {guid}" origin line).
5. **Nav and dashboard**: the top-level "Unidentified" rail entry is
   removed; the dashboard's "Unidentified N" card now links to the tab.
6. **FRD-12** replaces "Unidentified queue and detail" with a "Queues: tabs
   and filters" section covering all five tabs and both filter sets.

## Where the queries landed

- `Pegasus.Core.Intake.Unidentified.UnidentifiedMediaKindPolicy.Classify`
  (new, pure): mailbox channel → Email; else `image/*` → Image; else
  Document. A nullable-channel overload owns the "no receipt to classify"
  fallback (Image — INTK-007's grouped-VRM-conflict case is the only current
  producer of a receipt-less Unidentified item), so neither caller carries
  that judgement inline.
- `IUnidentifiedStore.ListQueueAsync` (new) / `EfUnidentifiedStore.ListQueueAsync`
  (implementation): a left join of open `UnidentifiedItemEntity` rows against
  `IntakeReceiptEntity` (no FK modelled between them — a plain LINQ join),
  classifying each row and reading its filename or subject/sender off the
  joined receipt. Reuses `EfIntakeReceiptStore.ReadSubject`/`ParseSourceChannel`
  (widened from `private` to `internal`) rather than duplicating the JSON
  evidence/channel parsing.
- The Not-ready origin filter reuses `ISearchCases` (Instruction-initiated,
  untouched) and `IImageIntakeQueries.ListAsync` (Image-initiated, already
  used by `/VehicleImages`) — no new query owner needed there, only a new
  caller and an in-memory `State == AwaitingInstruction` filter.

## Example row

A manual-upload PDF that fails to read registers as Unidentified with
Reason "Unreadable or corrupt content". The tab now shows: `U42 | Document |
unreadable-document.pdf | 20 Aug 2026 09:14 | Unreadable or corrupt content`
— one line, links to `/Unidentified/Details/{id}`, no GUID or "intake"
wording visible.

## `/Unidentified` route disposition

Kept as a permanent redirect (`RedirectPermanent("/Triage?queue=unidentified")`)
rather than deleted — no precedent for a redirect-only page existed in the
repo (the one prior page retirement, "Retire obsolete Operations and
Received lists", deleted its pages outright with no redirect), so this is a
new but minimal pattern: an `IndexModel` with only `OnGet`, and a `.cshtml`
that is never rendered.

## Vocabulary fixes

- `OperatorLabels.UnidentifiedOriginKind` (whose only output was the banned
  "Intake receipt" wording) is removed entirely; nothing needs the origin
  kind/GUID once rows carry media kind + handle.
- New `OperatorLabels.UnidentifiedMediaKind` and `OperatorLabels.EmailHandle`
  are the two new label surfaces, both `OperatorLabels`-owned.
- Confirmed via a targeted integration-test assertion
  (`TriageQueuesWebTests.UnidentifiedTabRendersNoBannedVocabularyOrRawIdentifiers`)
  that the rendered Unidentified tab contains no "intake"/"custody" text and
  no raw GUID in operator-visible markup (hrefs, which legitimately carry a
  GUID route parameter, are excluded from that check).

## Simplification pass (2026-08-20)

Ran `/simplify` (4 parallel review agents: reuse, simplification, efficiency,
altitude) over the full diff. Applied:
- Extracted `OperatorLabels.EmailHandle` to remove a 3-agent-flagged
  duplicated formatting rule between the queue row and the detail page.
- Fixed a composed `OfficeDate`+`OfficeClock` call that reproduced
  `OperatorLabels.OfficeTime` verbatim.
- Moved the "no receipt → Image" fallback into `UnidentifiedMediaKindPolicy`
  (was duplicated in Infrastructure and Web).
- Removed a redundant second `ListQueueAsync` call on the Unidentified tab
  (was querying the same join twice); the two counts every tab always
  fetches now run via `Task.WhenAll`.
- Extracted `LoadNotReadyAsync`, which also runs its two independent
  queries concurrently.

Skipped (with reason, recorded in the ticket plan): pushing the Unidentified
media-kind filter and the Not-ready Image-initiated filter into SQL rather
than filtering in memory after an unbounded/joined fetch — both are
pre-existing or newly-accepted trade-offs for what are bounded exception
queues, and widening `IImageIntakeQueries.ListAsync`'s contract would touch a
shared port used by `/VehicleImages`, outside this ticket's diff.

## Tests

```
dotnet build ./Pegasus.slnx -c Release
  → Build succeeded. 0 Warning(s), 0 Error(s).

dotnet test tests/Pegasus.Core.Tests -c Release
  → Passed! - Failed: 0, Passed: 690, Skipped: 0, Total: 690

dotnet test tests/Pegasus.IntegrationTests -c Release \
  --filter "FullyQualifiedName~Unidentified|FullyQualifiedName~Triage|FullyQualifiedName~Dashboard|FullyQualifiedName~ImageIntake|FullyQualifiedName~Cases|FullyQualifiedName~Shell"
  → Passed! - Failed: 0, Passed: 47, Skipped: 1 (QdosIntakeWebTests.DashboardAndQueueCountsAreBackedByPersistedDecisions,
    a pre-existing Corpus-gated skip unrelated to this change), Total: 48

dotnet test tests/Pegasus.IntegrationTests -c Release --filter "Category=Browser"
  → Passed! - Failed: 0, Passed: 38, Skipped: 0, Total: 38
    (includes AccessibilityTests over the new /Triage?queue=unidentified route
    and the full OperatorJourneyTests suite)

dotnet test tests/Pegasus.ArchitectureTests -c Release
  → Passed! - Failed: 0, Passed: 97, Skipped: 0, Total: 97
```

New/extended tests: `UnidentifiedContractsTests.MediaKindPolicyClassifiesByChannelThenContentType`
(Core, pure); `UnidentifiedPersistenceTests.ListQueueClassifiesEachRowByItsReceiptsChannelAndContentType`
(seeds a mailbox/image/pdf receipt each, asserts the filter and handle
fields); `TriageQueuesWebTests` (new file) —
`NotReadyOriginFilterReturnsOnlyTheMatchingOriginsRows`,
`UnidentifiedRouteRedirectsPermanentlyToTheQueuesTab`,
`UnidentifiedTabRendersNoBannedVocabularyOrRawIdentifiers`; `AccessibilityTests`
gained `/Triage?queue=unidentified`.

**Not run**: a manual 1920px visual pass (the ticket's verification checklist
item) — only the automated Browser/AccessibilityTests suite ran; no
screenshot-based manual review was performed in this session. State this
honestly rather than checking it off.

## Verification checklist (from the ticket body)

- [x] "Unidentified" no longer appears in the primary navigation; the Queues
  page shows it as a tab with image/e-mail filters that actually filter.
- [x] Not ready tab filters by Instruction-initiated / Image-initiated and
  both filters return correct rows.
- [x] No operator-facing GUID, "intake", or "custody" on the queue surfaces;
  rows carry filename/subject+sender, received date, reason.
- [~] Browser + AccessibilityTests green (both green); visual pass at 1920
  **not performed** — automated coverage only.
