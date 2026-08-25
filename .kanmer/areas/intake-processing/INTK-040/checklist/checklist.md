# Checklist — INTK-040

- [x] Extend grouped intake and persistence with source channel and nullable parent receipt provenance, including migration and Worker grants.
- [x] Add and wire fresh-mail attachment submission, parent deferral, stable retry, and technical-failure visibility.
- [x] Update Worker composition, operator notes, and FRD-02.
- [x] Add focused behavioral, integration, custody, replay and exclusion tests.
- [ ] Run locked restore, Release build, focused/relevant/full tests successfully.
- [ ] Run and record the simplification pass over the branch diff.
- [ ] Write the post-implementation report, push the branch, open the PR to dev, and move the ticket to Review.

## Progress notes

- 2026-08-25: Locked restore and Release build pass. Focused Core tests pass (43); Worker composition passes (4); committed-migration schema test passes; U35-shaped SQL-backed mailbox route passes and settles three JPEG children to Image-initiated Case AB12CDE-01 without a parent U-item or replay duplicate. Full validation remains.
