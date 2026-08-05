# UI defects found by live verification of release 6

Six defects that the local verification pass could not see, found by driving
the deployed instance through a browser after release 6 (source revision
`474a0924…`) went live on 2026-08-05.

## Why the local pass missed them

Two blind spots, both properties of the local environment rather than of the
code:

- **The local database was empty.** Every count rendered `0`, and `0` is what
  a correct empty dashboard shows, so a count that can only ever return `0`
  was indistinguishable from a correct one. Production had a `Needs sorting`
  receipt in it, and the tile still read `0` with that receipt one click away
  in the Inbox.
- **The local server clock is Europe/London.** `ToLocalTime()` resolves
  against the server's zone, which on the developer workstation *is* the
  office zone. The deployed Linux container runs UTC, so the same code that
  looked right locally is an hour early through British Summer Time.

Neither is a reason to distrust local verification; both are reasons that
local verification is not sufficient on its own. The lesson recorded here is
that count queries and time rendering need evidence from a populated database
and a non-London clock — which is what the regression test below supplies for
the first, and what a single conversion point supplies for the second.

## The defects

### 1. The Dashboard "Needs sorting" tile is permanently zero

`EfDashboardQueries.GetMailActivityCountsAsync` compared
`item.Decision == IntakeDecision.NeedsSorting.ToString()` — the string
`"NeedsSorting"`. The column holds the snake_case code `"needs_sorting"`,
which `EfIntakeReceiptStore.ToCode` owns. Nothing ever matched.

Fixed by asking the store for the code instead of spelling it a second time,
so the two cannot drift apart again.

Covered by a new integration test,
`DashboardNeedsSortingCountSeesAStoredNeedsSortingReceipt`: it stores two
`NeedsSorting` receipts and one `BlockedIntake`, and asserts the count is 2.
There was no test holding `EfDashboardQueries` against a real database at all;
that absence is why the bug shipped.

### 2. Around forty date surfaces render the server's zone, not the office's

`OperatorLabels.OfficeTime` existed and its own remark claimed that a
`ToLocalTime()` against the server clock was one of only two places that did
not render Europe/London. In fact forty call sites across Cases, Intake,
Triage, Image intake and Operations still called `ToLocalTime()` directly.

Every one now goes through `OperatorLabels`, which gains the shapes the call
sites actually needed so that none of them has a reason to reach past it
again:

- `OfficeTime(DateTimeOffset?, string absent)` for the four nullable sites,
  each keeping the absent wording it already had ("Not recorded", "Not
  scheduled", "not supplied");
- `OfficeDate` for the four date-only sites;
- `OfficeClock` for the two that print the time under a date.

All four delegate to one private `InOffice` conversion. The remark that
claimed the problem was already solved is corrected to say what was actually
true.

`Pegasus.Web.Presentation` is imported in `_ViewImports.cshtml` rather than
qualified per use, because the point of the change is that this is the only
route.

### 3–6. Four raw identifiers the register names by name

`ui-standards-and-review.md` rule 4 and the defect register list these
explicitly; they survived the page PRs.

- **Intake review, "Source receipt"** printed the Graph message identifier in
  full. Removed: it is the transport's handle on the message, not the
  business's, and the "Channel" row above already says where the message came
  from.
- **Intake review, failure code** led the failure block in its persisted
  snake_case form. The first attempt removed it outright, and three
  integration tests caught that as the mistake it was: their names —
  `MalformedDocxProducesExplicitVisibleTerminalFailure`,
  `DocxWithMoreThan512ZipEntriesIsVisiblyResourceLimited` — say the
  distinction between one terminal outcome and another has to reach the
  screen. "It failed" is not something an operator can act on. So the
  distinction stays and only the spelling goes: a new
  `OperatorLabels.IntakeFailure` maps each code to what happened ("The Word
  document could not be read", "The Word document is larger than the
  processing limit allows"), and the three tests now assert the words are
  present *and* the code is not.
- **Replace principal** printed the sequence-lineage GUID and the concurrency
  version. Both removed: the version is carried by the hidden field that makes
  the post safe, and the consequence paragraph below already promises in words
  that the successor continues the same numbering.
- **Case documents** printed raw byte counts (`@version.ContentLength bytes`)
  and raw enum values for two different upload states. Byte counts now go
  through `OperatorLabels.FileSize`; the states through `CustodyState`,
  `UploadLinkState`, and a new `UploadRequestState` for `RequestUploadStatus`
  — a distinct enum from `BoxFileRequestStatus`, sharing several member names
  and no members, so one label method cannot serve both.

## Deliberately not in scope

The three decisions the programme could not take for itself are unchanged and
still recorded in `NOW.md`: the two provenance glyphs that would re-checksum
an approved sprite, the committed `claudeuiverification` credential, and the
two features that were not shipped rather than faked.

## Verification

Local: `dotnet build --configuration Release`, then the full suite. The new
integration test fails against the previous `EfDashboardQueries` and passes
against the fixed one.

Live: re-verified against the deployed instance after the redeploy, with the
production `Needs sorting` receipt still in place — which is the only evidence
that can prove defect 1 is actually fixed.
