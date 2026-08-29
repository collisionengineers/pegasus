## 2026-08-29 — landed to PR #620

Merged `origin/dev` `b92cb9a7` into the existing branch (`4033e881`, no
conflicts), finished the ticket on top of the preserved `1594ff0e`, and opened
https://github.com/collisionengineers/pegasus/pull/620 against `dev`. Stage
moved to `review`. Not merged — merge authority is not this lane's.

Facts checked by reading rather than assumed, for whoever reviews:

- `QueuedIntakeStatus.CaseId` had no reader in `src/` or `tests/` on merged
  `dev`. Deleted with the second copy of the association precedence that was
  filling it.
- `docs/design/README.md` no longer carries the obsolete four-state /
  fixed-two-second Upload Status row the files document expected to replace;
  UIIMP-006's wave-0 rewrite removed it, and the rewritten Contracts table
  specifies no refresh cadence for any page. `frd-02` already states both
  required behaviours. No doc edit was made — see the checklist's Parked
  section.
- `TestUiSnapshotTests` executes only under `PEGASUS_TEST_UI_MODE`, so it did
  not run here. `upload-status--*` / `upload-group-status--*` snapshots will
  need regenerating on the merging branch; all four `StateMatch` needles still
  render.
- `OperatorLabels.cs` untouched.

Open for INTK-047: the hidden-tab behaviour has no executed test. It needs the
Browser category, which this lane may not run.
