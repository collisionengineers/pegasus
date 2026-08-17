# Checklist — BUG-001

- [ ] Create the dedicated BUG-001 branch/worktree from fresh `origin/dev`, take the ticket into Implementing, and record the exact head.
- [ ] Add a Core regression test for one fragment containing exact `OfQDOS` plus at least two recognised instruction labels.
- [ ] Add negative embedded-string cases and retain the existing cross-fragment proof refusal.
- [ ] Implement bounded standalone-`QDOS` / exact-`OfQDOS` recognition without relaxing the two-label or same-fragment gates.
- [ ] Apply the repository's extraction-policy versioning convention and update version-bound fixtures.
- [ ] Prove the corrected Audit shape produces exactly one allocation, Case/PO, link, and custody work item.
- [ ] Prove replay is idempotent and a non-confirming lookalike produces no allocation.
- [ ] Run the QDOS policy, intake processing, allocation/recovery, custody-outbox, and Worker-composition tests to conclusive results.
- [ ] Run the Release build and `git diff --check`; record exact evidence in the post-implementation report.
- [ ] Obtain independent review and green CI before merge to `dev`.
- [ ] Obtain explicit exact-target approval before Web/Worker deployment and deploy only the approved immutable revision.
- [ ] Read back deployed Web/Worker identity, health, trigger configuration, telemetry, and required migrations; update current-state docs.
- [ ] Obtain separate explicit approval to re-evaluate production receipt `9a91fe16-d62f-4477-a11e-830fd96f672a`.
- [ ] Re-evaluate through the existing reasoned command and confirm prior decision history is preserved.
- [ ] Confirm exactly one Case/PO, link, custody work item, Box folder, retained source, and custody confirmation, with no replay duplicates.
- [ ] Write `proof.md` with local, deployed, live, approval, and replay evidence; close only if every tier passes.
