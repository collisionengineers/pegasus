# Post-implementation report — TICK-077 (EXT-04)

PR: https://github.com/collisionengineers/pegasus/pull/574
Branch: `task/tick-077-eva-api-submission`

## What shipped

A second send-to-Engineer route: `POST /Instruction/Inspection`, at most once
per case, carrying the mapped fields and every eligible image. Switchable per
Principal for manual and automatic sending, both defaulting off. The case
action bar's Export button became one **Send to EVA** control opening a page
offering the API submission or the unchanged export.

## What is not proved

**Pegasus has never called EVA**, in any environment. The operator decided on
2026-08-27 not to make a first submission as part of this ticket. The contract
is proved against the vendor's own recorded traffic and its published request
model, and nothing establishes that EVA accepts this payload, that images land,
or what it returns.

This is recorded in FRD-07, `capabilities.md`, `current-architecture.md` and
`operations.md` in those terms. Every Principal setting defaults to off, so
merging changes no behaviour for any existing case.

## Deviations from the plan

| Planned | Actual | Why |
| --- | --- | --- |
| `EvaCaseImageReader` extracted | that **and** `EvaCaseEvidenceReader` | Both routes must state the same case, not just send the same images. |
| One submission-side migration | table migration regenerated mid-work | The replay defect needed an `OperationKey` column; nothing was deployed, so one clean migration beat stacking a second. |
| No mention of `Agent` | `Agent` carries the Principal code | Operator direction. It had been omitted because recorded traffic showed it rejected until EVA provisioned it. |
| `InsName` = work provider | `InsName` = claimant name | Operator direction; the work provider moved to a note line. |
| Live test submission before Done | skipped | Operator decision. |

## Defects found and fixed during the work

1. **The token response never parsed.** `access_token` does not bind to
   `AccessToken` by case-insensitive matching — underscores are not bridged.
   Every submission would have failed as `Unknown` without reaching EVA.
2. **The grant migration was never applied.** Hand-written, it had no Designer
   file and so no `[Migration]` attribute for EF to discover. It would have
   shipped `EvaSubmissions` with no grants for either runtime role: green on
   full-privilege LocalDB, refused in production. The exact DOCS-008 failure
   mode the migration's own comment warns about.
3. **The inspection address lost a line.** `MapLocation` read index 0 twice and
   never index 4.
4. **Replay answered from the wrong row.** It matched action history on the
   operation key, then returned the case's most recent submission — so
   replaying an automatic attempt could report a later manual send's outcome.
5. **Core acquired a `System.Net.Http` dependency** by naming
   `HttpRequestException` in a catch filter. Infrastructure now translates the
   custody read's transport failure to `IOException` at its own boundary.
6. **A comment described a lock the code does not take.** The code was right —
   by then the request has reached EVA, and refusing to record it would lose
   the delivery and permit a second claim.

## Verification

- `dotnet restore --locked-mode`, `dotnet build --configuration Release` clean.
- Core **1041/1041**, Architecture **100/100**.
- All EVA tests green, including five new persistence tests proving the
  once-per-case filtered unique index against LocalDB. That rule was previously
  held up by a database constraint no test exercised.
- Eleven integration failures were all real consequences of this change; each
  was fixed and re-run green individually. The full-suite run is left to CI
  (operator decision) after two local runs were interrupted.

## Assertions changed, with reasons

- **CASE-007** asserted the read-only case view contains no "EVA" at all, which
  the operator-directed control label breaks. Narrowed, not deleted: EVA must
  appear exactly once, on that control, with no submission state or reference
  on a case not ready to send.
- **Two tests drove the old Export button.** Updated to the moved control; the
  rules they pin (no link to the export, GET yields no package, the journey is
  keyboard-only) are unchanged.
- **Three architecture tests** pinned facts this change legitimately moved.

## Left for the reviewer

- Two outcome-to-text tables (`SendModel.Describe`, `OutcomeLabel`) that
  `OperatorLabels` could own; consolidating changes snapshot-asserted strings.
- `catch (DbUpdateException)` in the sweep is unfiltered.
- `EvaCaseImageReader.SelectedDocument` carries two never-read members.

## Follow-ups

- [[ENG-019]] live-credential swap — blocked, operator's direct request only.
- [[ENG-020]] real EVA fields for the inspection date and mileage.
