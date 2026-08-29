# Post-implementation report — INTK-046 (lane C2)

Wave 2 lane C2 of [[EPIC-011]]: the Triage, Unidentified, Received and
image-record pages ported onto the design system, plus the round-2
regression fix that made PR #605's `sql-integration` shard green, plus
the round-3 corrections made after adversarial verification.

## What shipped

| Page | Contract | Outcome |
| --- | --- | --- |
| `/Triage/{id}` | §1.5 | Ported; determinations, source, response evidence, images, permanent history |
| `/Unidentified/{id}` | §1.6 | Ported; retained source, history, resolve dialog |
| `/Received/{id}` | restyle | Restyled; every handler binding unchanged |
| `/VehicleImages/{id}` | D1 | Restyled as the image record; gallery retained |
| `OperatorLabels.cs` | one list | One `UnidentifiedResolutionTarget` map appended at the end of the class |

No new CSS, JS, package, project or top-level directory. No Core or
Infrastructure change — these are composition-root views over handlers
that already existed.

## The regression, and why it happened

PR #605 was git-mergeable but `sql-integration` shard 1 was
deterministically red — not the DELIV-031 flake.
`QdosTriageIntegrationTests
.AuthenticatedTriagePageExecutesLifecycleWithVersionsAndPermanentHistory`
failed on three separate assertions, found one at a time as each was
fixed:

| # | Line | Pinned string | Cause |
| --- | --- | --- | --- |
| 1 | 221 | `Available once a finding is recorded` | Complete rendered only when `canComplete` |
| 2 | 317 | `Post-send correction` | Name lost when the three determination forms were unified |
| 3 | 477 | `Permanent history` | Panel renamed "Notes" |

All three trace to one root cause: the simplification pass's form
unification (a sound dedup) also renamed and re-scoped operator-facing
labels, and round-1 review accepted the first as "the per-state control
convention used across the workspace pages". That premise was false —
see the plan's round-2 correction. `Pages/Cases/Details.cshtml:269` and
`Pages/Cases/Assessment/Index.cshtml:765`, both already merged, use the
`.gated`/`data-condition` disabled-with-named-condition shape that
`site.css:1893` defines. The port had dropped a live convention, not
replaced it.

## The Complete control, judged against D7 as it is actually written

Rounds 1 and 2 of this document argued the Complete control against a
**misquotation** of D7. See "Corrections after adversarial verification"
below for what was wrong. The honest argument is this.

Two clauses of EPIC-011 bear on a drawn control:

- The Rules bullet: *"Every drawn control maps to a named handler or an
  approved disabled seam (D7). Never render an inert control."*
- The D7 table row: *"Uncomposed integrations (Experian, Glass's,
  Audatex, Cazana) render disabled as drawn; a disabled control is
  permitted only for a named, ticketed integration seam."*

The Complete control **satisfies the Rules bullet**: it posts the same
`complete` action to `OnPostActionAsync` whether or not the record's
state currently permits it, so it is not inert.

The Complete control **does not satisfy D7's second clause read
literally**, because it is a state gate and not an integration seam.
That is a real contradiction and this lane does not paper over it.
Three verified facts show the contradiction is not this lane's to
resolve:

1. `QdosTriageIntegrationTests.cs:216-221` — a pre-existing, unmodified
   `dev` assertion (`git diff origin/dev HEAD -- tests/` is empty)
   requiring `"Available once a finding is recorded"` in the page,
   under the comment *"Completion keeps its place with its condition
   named, rather than disappearing until it happens to work."* That
   file is outside this ticket's owned paths, and weakening its
   assertion is banned outright.
2. Two merged EPIC-011 pages already gate non-integration controls the
   same way: `Pages/Cases/Details.cshtml:269` (state gate) and
   `Pages/Cases/Assessment/Index.cshtml:765` (role gate).
3. `site.css:1893-1911` defines `.gated`/`data-condition` as a general
   design-system rule with its own forced-colours case at 1961 — not an
   integration-seam-specific rule.

So D7's second clause is already contradicted by merged work and by
`dev`'s own test. Narrowing that clause, or ruling the merged `.gated`
convention out, is the epic owner's call: raised as **[[UIIMP-012]]**.
Show/hide remains correct for transitions Core forbids outright from a
state (Await, Reopen).

Fixes, all in `src/Pegasus.Web/Pages/Triage/Details.cshtml`:

- **~192** — Complete restored to the `.gated` shape: permanently
  rendered inside `mutable` (matching the pre-port guard), `is-disabled`
  + `disabled` when `!canComplete`, `data-condition` naming the
  condition, `data-dialog-open` nulled when disabled so it cannot
  reference a dialog that is not rendered. Uses the repo's explicit
  `disabled="@(cond ? null : "disabled")"` form.
- **~104** — the determinations panel is titled "Post-send correction"
  when `correction` is true. This keeps §1.5's single panel rather than
  restoring the pre-port second panel.
- **~398** — the history panel is "Permanent history" again.

**No assertion was weakened, skipped or deleted.** The tests were right
and the markup was wrong in all three cases.

Deliberately **not** restored: the pre-port sentence "A correction
supersedes the current finding, removes its response link, and requires
a new exact reply before completion." It is three consequences, and
EPIC-011 bans explanatory copy; the heading plus the "Record correction"
button label carry the meaning. No test pins it.

## The history panel name, stated plainly

EPIC-011 §1.5 **names this panel "Notes"**: `Notes panel (entries
Date/Time/ID + text)`. The shipped heading is "Permanent history". Round
2 of this document softened that to "§1.5's entry shape is unchanged;
only the heading is", which understated the conflict. Stated plainly:

- §1.5's **entry shape** is shipped exactly as specified (Date/Time/ID +
  text).
- §1.5's **panel name** is not. The binding document is stale against
  shipped code, and the ticket body still says "notes panel".

"Permanent history" is not a lane invention. It is what
`origin/dev:src/Pegasus.Web/Pages/Triage/Details.cshtml:348` already
said before this port, what `QdosTriageIntegrationTests.cs:477` pins,
and the term `docs/frd/frd-03-triage.md` owns.

Renaming the heading to "Notes" was **measured, not argued**. Applied,
built green, then:

```
dotnet test ./Pegasus.slnx -c Release --no-build \
  --filter "FullyQualifiedName~QdosTriageIntegrationTests"
Failed: 1, Passed: 8, Skipped: 0, Total: 9
  QdosTriageIntegrationTests.cs:line 477 — Not found: "Permanent history"
```

The experiment was reverted. Restoring the contract's name inside this
lane would either ship a red pre-existing assertion into `dev`, or
require editing `QdosTriageIntegrationTests.cs` — a file this ticket
does not own, and an assertion change made to pass a test. Both are
banned. The reconciliation is **[[UIIMP-012]]**'s.

## Corrections after adversarial verification (2026-08-29, round 3)

| What was wrong | Where it was written | What was done |
| --- | --- | --- |
| D7 quoted as "forbids an **inert** control — one drawn against no handler". That is the Rules bullet's second sentence, not D7's row. D7's row adds a stricter clause about disabled controls generally, and the misquote was the load-bearing justification for the Complete control. | This report ("Resolution, judged against the rules"); the plan's round-2 correction; `Details.cshtml`'s Complete comment | All three rewritten to quote D7's actual wording and to concede the shipped shape does not satisfy it literally; the contradiction raised as [[UIIMP-012]] |
| "§1.5's Date/Time/ID entry shape is unchanged; only the heading and id" — true, but it never said §1.5 *names the panel* "Notes" | This report; the plan | Stated plainly above, with the failing-test measurement, and raised as [[UIIMP-012]] |
| `UnidentifiedResolutionTarget` inserted mid-file at `OperatorLabels.cs:54` rather than appended, against the lane brief | `OperatorLabels.cs` | Moved to the end of the class; the diff against `origin/dev` is now one hunk at `@@ -881,6 +881,30 @@`, `+24/-0`, no existing member reordered |
| The ticket's "no inert control" verification item left unticked — the contested question | Ticket body | Audited and ticked; see below |

Reported, not fixed — outside this lane's files:

- `site.css`'s `.gated::after` has no `[data-condition]` guard, so an
  enabled gated control paints an empty tooltip pill on hover. `site.css`
  is [[PLAT-029]]'s. Raised as **[[PLAT-061]]**, which also carries the
  `:focus-within` gap on `<button disabled>`.

## Verification — real numbers, re-run 2026-08-29

- `dotnet build ./Pegasus.slnx --configuration Release` — **exit 0**,
  **0 Warning(s), 0 Error(s)**.
- `--filter "FullyQualifiedName~QdosTriageIntegrationTests"` —
  **Failed: 0, Passed: 9, Skipped: 0, Total: 9**, 1 m 21 s.
- `--filter` over the lane's other owned classes
  (`TriageEvidenceImagesWebTests`, `ShellAndStatusPageWebTests`,
  `ImageIntakeWebTests`, `ImageViewingWebTests`, `QdosIntakeWebTests`,
  `GroupedIntakeWebTests`) — **Failed: 0, Passed: 15, Skipped: 6,
  Total: 21**, 50 s.
- Control audit for the ticket's verification item: every
  `data-dialog-open` target across the four owned pages resolves to a
  declared dialog (`triage-assign/await/complete/cancel/unassign/
  unlink-case/reopen` via `_ReasonDialog` `["DialogId"]`,
  `triage-link-case-dialog` and `unidentified-resolve-dialog` inline,
  `image-intake-close-dialog` via `_ReasonDialog`); every form posts a
  handler that exists on the page model (`OnPostActionAsync`,
  `OnPostResolveAsync`, `OnPostCloseAsync`, and the ten named Intake
  handlers). No orphan target, no control without a handler.

Evidence tier: green integration tests against the real page pipeline —
not a live-caller proof. The browser walk at 1580/1100/760 and the full
suite stay the orchestrator's gates (EPIC-011 rule); `proof.md` is
written on merged `main`.

## Carried forward

- `QdosTriageIntegrationTests.cs` added to the ticket's owned-files
  record — it asserts on markup this lane owns, so it is this lane's
  regression gate. Recorded in the ticket's `files` document, not in the
  epic's `waves.md`, which this ticket does not own.
- Still open from round 1: superseded findings' recorded values are
  visible nowhere in the UI (Core retains them). Not in §1.5; a
  follow-up ticket if operators need the supersession trail.
- Lesson for the remaining EPIC-011 lanes: a dedup that also renames is
  two changes. Three of this lane's regressions were renames riding
  inside one accepted simplification.
- Second lesson, from round 3: when a contract clause and a merged test
  disagree, quote **both** verbatim in the record and raise the conflict.
  Paraphrasing the clause into the shape that fits the code is how a
  defensible decision becomes an undisclosed one.
