# Open questions — ENG-036

Raised 2026-09-03 during the cross-model plan review (gpt-5.6-sol xhigh read,
Claude Opus dispositioned). Both are resolved by the controller from rules
already recorded; the operator may veto either in review.

- [x] **Test UI snapshot ownership for a routed-page partial change.**
  Resolved 2026-09-03: **option (a), generalised.** Every lane that changes a
  routed Razor page or one of its partials regenerates the snapshots its own
  change affects and commits `docs/design/test-ui/` with the page change, in
  its own PR. That is the repository instruction, not a UIIMP-014 privilege:
  CLAUDE.md requires `./scripts/Update-TestUiSnapshots.ps1`, then
  `-Verify -SkipCapture` and `./scripts/Test-UiCatalogue.ps1`, in the same
  change set, and CI runs the same verify on every change set. The
  capacity-one lock on `docs/design/test-ui/**` means one lane at a time, not
  one lane for the whole epic — ENG-036 is serial in wave 4 and holds the lock
  while it runs. [[UIIMP-014]] owns the **new** snapshot states, the catalogue
  entries and the browser walk in wave 5, not the routine regeneration that
  each page change carries. Option (c) is refused outright: a red catalogue
  check is a failing gate, and a lane never merges on one.

- [x] **`docs/design/README.md` still describes a damage type (D45 residue).**
  Resolved 2026-09-03: **[[PLAT-070]] owns it and already carries it.** Its
  files document maps `docs/design/README.md` as "Remove Review panel from
  configuration list and Type from damage-diagram row (D44/D45)" and its
  checklist has the corresponding step. PLAT-070 is implementing now in wave 1
  and merges long before ENG-036 starts, so ENG-036 changes no governing
  document and simply builds against a design authority that already agrees
  with D45. ENG-035's `files/files.md` residue ("zone/type structures") is
  coordination only: no contract admitting `type` may ship, and ENG-036's
  hand-off 1 already refuses one.

## Parked (explicitly deferred)

none
