# Plan — KANMER-001

## Chosen approach

Treat this as a live-board migration with explicit inventories and optimistic concurrency. Retarget dead canonical-owner links mechanically where the FRD mapping is proven, preserve substantive tickets, archive only mechanically imported queue stubs after a fresh body/doc check, and annotate historical evidence rather than rewriting its meaning.

This beats a regex-only mass rewrite because the board has drifted since the original research: four CI tickets and one additional pipeline-doc surface now contain real work.

## Governing docs

No PRD, FRD, or ADR is linked. This ticket changes repository workflow metadata, whose canonical owner is AGENTS.md. AGENTS.md declares the Kanmer board to be the work queue and forbids recording repository process in an ADR. Product FRDs are used only as the new targets for ticket links formerly pointing to docs/requirements.md.

## Ordered steps

1. Snapshot every affected ticket's current body, stage, archive state, groups, docs, and updated timestamp; stop on conflicts rather than overwriting concurrent edits.
2. Retarget the 44 docs/requirements.md canonical-owner links to the verified live FRD file and preserved heading anchor, including Done and archived records.
3. Retarget substantive NOW.md tickets TICK-001, TICK-118, TICK-120, TICK-199, and TICK-201 without archiving them.
4. Protect and retarget the evolved CI tickets TICK-194, TICK-195, TICK-197, and TICK-200; keep EPIC-001 membership and substantive pipeline work intact.
5. Preserve renderer TICK-203 through TICK-216 and replace stale NOW.md authority language with linkage/context through SIMPLI-015.
6. Re-evaluate each remaining boilerplate candidate against current body, docs, groups, and stage; archive only exact non-actionable imports, with an audit note in the body.
7. Retarget or historically annotate pipeline citations in TICK-012, TICK-017, and TICK-194.
8. Verify zero retired-file citations remain in active ticket bodies or active pipeline instructions; allow only clearly marked historical evidence in completed/archived records. Record counts and exceptions.
9. Write the post-implementation report, independently review the board diff/evidence, then verify the committed board state and close out the ticket.

## Proof

Capture before/after counts from repo-wide board searches; verify every replacement FRD path and anchor exists; list archived, preserved, conflicted, and historical-only records; confirm HZN-001 contains the ticket and KANMER-001 is released at Done.

## Risks and mitigations

- Concurrent board edits: expected_updated on body updates and fresh reads before writes.
- Over-archiving: require exact boilerplate plus no substantive docs/groups/new work.
- Historical distortion: annotate evidence citations instead of pretending the retired file never existed.
- Broken links: validate each mapped FRD file and anchor before mutation.
- Board/tool drift: keep Kanmer setup reconciliation out of this ticket; report it separately.
