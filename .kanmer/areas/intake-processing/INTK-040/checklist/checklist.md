# Checklist — INTK-040

- [x] Extend grouped intake and persistence with source channel and nullable parent receipt provenance, including migration and Worker grants.
- [x] Add and wire fresh-mail attachment submission, parent deferral, stable retry, and technical-failure visibility.
- [x] Update Worker composition, operator notes, and FRD-02.
- [x] Add focused behavioral, integration, custody, replay and exclusion tests.
- [x] Run locked restore, Release build, focused/relevant/full tests successfully.
- [x] Run and record the simplification pass over the branch diff.
- [x] Write the post-implementation report, push the branch, open PR #548 to dev, and move the ticket to Review.

## Progress notes

- 2026-08-25: Locked restore passed. Release build passes with 0 warnings/errors. Core passes 990/990 after the review fix; Architecture passes 99/99; the U35-shaped SQL-backed mailbox route passes and settles three direct JPEG children to Image-initiated Case AB12CDE-01 without a parent U-item or replay duplicate. The full non-corpus/non-browser Integration suite passed 910 tests with 2 expected skips before the final simplification; the affected integration scenario passed again after it.
- 2026-08-25: Independent simplification review findings were all applied, including correction of the partial-group terminal-failure path to one group-scoped technical U outcome.
- 2026-08-25: Commit `1cabc66e` and review fix `2440f1a6` pushed to PR #548 against `dev`. No deployment performed.

- 2026-08-25: CI identified missing Azure bootstrap accounting for the grant-carrying migration. Commit `af50a650` adds the exact Worker INSERT permissions to the bootstrap census; local deployment-plan validation and all 72 migration-grant checks pass.
