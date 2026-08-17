# Checklist — DELIVE-001

- [x] Recheck the planned file inventory against active UI-revamp copies and record the non-overlap.
- [x] Add the repository root plan at `docs/temp-plans/harden-flaky-ci-tests.md`.
- [x] Align the Worker deployment-plan validator with the current replica envelope and expose complete subprocess failure evidence.
- [x] Add bounded SQL error 1205 retry around the deliberately parallel allocation-retry test only.
- [x] Move QDOS pressure from per-PR CI into a nightly/manual evidence-retaining workflow and update operating documentation.
- [x] Make already-requested document-extraction cancellation win before a resource-limit terminal outcome.
- [x] Run each formerly flaky focused contract 20 times or record the closest locally feasible equivalent.
- [x] Run workflow/document validation, locked restore, Release build, and affected test suites.
- [x] Write the post-implementation report, record commits and PR, and move the ticket to Review.

## Progress notes

- Worker rogue-setting contract: 20/20 passed.
- SQL parallel aggregate-retry contract: 20/20 passed against isolated LocalDB fixtures.
- Document-extraction cancellation plus uncancelled decoded-limit pair: 20/20 passed.
- QDOS pressure source was not edited or rerun as a PR gate; it was moved intact to recurring/manual scheduling.
- Locked restore and Release build passed with zero warnings/errors; architecture suite passed 87/87; document-extraction solution passed 972 with one opt-in cohort skip.
- Commit: `4b1cfed8be9530e367225a3deac4a651ae0da534`.
- PR: https://github.com/collisionengineers/pegasus/pull/378
