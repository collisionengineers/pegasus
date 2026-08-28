# Post-implementation report — INTK-046 (lane C2)

Wave 2 lane C2 of [[EPIC-011]]: the Triage, Unidentified, Received and
image-record pages ported onto the design system, plus the round-2
regression fix that made PR #605's `sql-integration` shard green.

## What shipped

| Page | Contract | Outcome |
| --- | --- | --- |
| `/Triage/{id}` | §1.5 | Ported; determinations, source, response evidence, images, permanent history |
| `/Unidentified/{id}` | §1.6 | Ported; retained source, history, resolve dialog |
| `/Received/{id}` | restyle | Restyled; every handler binding unchanged |
| `/VehicleImages/{id}` | D1 | Restyled as the image record; gallery retained |
| `OperatorLabels.cs` | one list | One `UnidentifiedResolutionTarget` map added |

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

## Resolution, judged against the rules

D7 forbids an **inert** control — one drawn against no handler
(Experian, Glass's, Audatex, Cazana). Complete posts the same
`complete` action whether or not the state currently permits it, so it
is a **state gate**, not an inert seam; the epic's §1.5 requirement
that the server-side transitions "stay available through the
determinations flow" points the same way. Show/hide remains correct for
transitions Core forbids outright from a state (Await, Reopen).

Fixes, all in `src/Pegasus.Web/Pages/Triage/Details.cshtml`:

- **~186** — Complete restored to the `.gated` shape: permanently
  rendered inside `mutable` (matching the pre-port guard), `is-disabled`
  + `disabled` when `!canComplete`, `data-condition` naming the
  condition, `data-dialog-open` nulled when disabled so it cannot
  reference a dialog that is not rendered. Uses the repo's explicit
  `disabled="@(cond ? null : "disabled")"` form.
- **~104** — the determinations panel is titled "Post-send correction"
  when `correction` is true. This keeps §1.5's single panel rather than
  restoring the pre-port second panel.
- **~392** — the history panel is "Permanent history" again. It renders
  `Model.Triage.History` — retained business events with actor, time and
  reason. Triage has no note entity, so "Notes" named a concept that
  does not exist here, and `docs/frd/frd-03-triage.md:43` (one of this
  ticket's own `refs`) owns the term. §1.5's Date/Time/ID entry shape is
  unchanged; only the heading is.

**No assertion was weakened, skipped or deleted.** The tests were right
and the markup was wrong in all three cases.

Deliberately **not** restored: the pre-port sentence "A correction
supersedes the current finding, removes its response link, and requires
a new exact reply before completion." It is three consequences, and
EPIC-011 bans explanatory copy; the heading plus the "Record correction"
button label carry the meaning. No test pins it.

## Verification

- `git merge origin/dev` (9868cf58, PR #602) — clean, no conflicts.
- `dotnet build ./Pegasus.slnx --configuration Release` — **exit 0**,
  0 warnings, 0 errors.
- `dotnet test ./Pegasus.slnx --configuration Release --no-build
  --filter "FullyQualifiedName~QdosTriageIntegrationTests"` —
  **9 passed, 0 failed, 0 skipped**.
- The lane's other owned classes (`TriageEvidenceImagesWebTests`,
  `ShellAndStatusPageWebTests`, `ImageIntakeWebTests`,
  `ImageViewingWebTests`, `QdosIntakeWebTests`,
  `GroupedIntakeWebTests`) — **15 passed, 0 failed, 6 skipped**.

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
