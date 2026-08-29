## Round 2 remediation (Claude), 2026-08-29

Facts checked read-only rather than reasoned about, for whoever reviews the
scope-reduction rejection:

- `origin/task/eng-027-case-valuations` currently carries an **unmerged**
  migration (`20260829095336_CaseValuations`) plus `PegasusDbContextModelSnapshot.cs`.
  waves.md wave 3 = "one unmerged migration at a time"; a page lane adding a
  second would conflict on that shared snapshot file.
- `EfCaseDueChaserStore.cs:253` re-derives and compares the chase schedule on
  write against the static `CaseChaseSchedule`. An editable interval breaks that
  comparison for every already-scheduled row — it needs a versioned chase policy
  identity and an operator decision, not a checkbox.
- `CaseLifecycle.cs:555` and `:573-576` hard-require `InstructionsComplete &&
  ImagesComplete` with no configuration term. "Instruction document required" /
  "Eligible images required" would turn a fail-closed invariant into an
  administrator toggle.
- Exact prototype labels for the missing groups, from the final render layer
  (`Pegasus_UI_Assessment_Refined.html:1546`): "Instruction document required",
  "Eligible images required", "Chase interval" (number, 7). Recorded here so
  PLAT-062 does not have to re-derive them.
- The prototype's own `document.title` for every `/admin/<area>` route is
  `'Administration'` (line 1885) — the evidence behind making the h1
  "Administration" rather than restating the area label the panel h2 carries.

Left for the orchestrator, not actioned (would mutate another ticket): slot
PLAT-062 into the wave-3 migration order.
