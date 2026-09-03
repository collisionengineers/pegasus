# Open questions — ENG-036

Raised 2026-09-03 during the cross-model plan review (gpt-5.6-sol xhigh read,
Claude Opus dispositioned). Both are operator/coordination decisions ENG-036
cannot take alone.

- [ ] **Test UI snapshot ownership for a routed-page partial change.**
  `_CaseDamage.cshtml` is a partial of the routed `/Cases/{id}` page, so
  `AGENTS.md` requires regenerated `docs/design/test-ui/**` snapshots in the
  same change set as the page change, and CI runs the same verify on every
  change set. But `docs/design/test-ui/**` is an EPIC-012 capacity-one
  shared lock held by UIIMP-014, and `./scripts/Update-TestUiSnapshots.ps1`
  in update mode rewrites that directory (`TestUiSnapshotTests` writes the
  catalogue root in `update` mode). ENG-036 therefore cannot both satisfy the
  same-change-set rule and respect the lock. Decide one: (a) transfer the
  Case-record snapshot artefacts to ENG-036 for this PR, (b) make UIIMP-014
  merge first and own the snapshot update for ENG-036's page change, or
  (c) accept a red catalogue check on ENG-036's PR until UIIMP-014 lands, and
  say so explicitly. Until this is answered, Step 1 stops.

- [ ] **`docs/design/README.md` still describes a damage type (D45 residue).**
  At `origin/dev` the design authority's Damage bullet reads "a marker per
  zone — … — each with Severity, Type, Note", which D45 supersedes. ENG-036
  does not edit governing documents and the file is a capacity-one shared
  lock. Confirm which lane corrects it (the EPIC-012 documentation chore that
  recorded D44–D46 is the natural owner) so the design authority and the
  shipped section agree. ENG-035's `files/files.md` carries the same residue
  ("zone/type structures"); ENG-036's hand-off 1 already refuses a contract
  that admits `type`, so that one is coordination, not a blocker.

## Parked (explicitly deferred)

none
