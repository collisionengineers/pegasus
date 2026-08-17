# Independent review — KANMER-001

## Changes

- Retargeted all 44 ticket-body links from the retired requirements document to 16 FRD file/anchor pairs.
- Retargeted the five substantive legacy-tracker tickets and the four EPIC-001 CI tickets without archiving them.
- Changed 77 exact boilerplate Backlog tickets from `archived: false` to `archived: true`, adding migration notes.
- Preserved the renderer cluster TICK-203–TICK-216 at its prior archive state and retargeted its legacy authority wording.
- Retargeted historical pipeline references in TICK-012 research/plan/proof, TICK-017 research, and TICK-194 research/files.
- Renamed TICK-196 from prescribing NOW.md validation to a clearly retired legacy-tracker title.
- Added KANMER-001 planning/reporting records and HZN-001 migration context.

## Comments

1. **Blocking — renderer consolidation is not discoverable from the renderer tickets.** The plan requires TICK-203–TICK-216 to be retargeted with linkage/context through SIMPLI-015. All 14 bodies now replace NOW.md with generic “canonical Kanmer board” / “retired pre-Kanmer tracker” wording and link only KANMER-001 in the migration note. None names or links SIMPLI-015, and `get_links SIMPLI-015` has no backlinks from the renderer tickets. HZN-001 mentions the decision, but the renderer tickets are not HZN-001 members. An agent opening one of these preserved tickets therefore cannot discover the consolidation owner. Add a `[[SIMPLI-015]]` relation or explicit body link to each renderer ticket, preserving the two pre-existing archived states (TICK-209/210).

2. **Non-blocking — archive classification passed.** The committed diff contains exactly 77 `archived: false → true` changes. Every one remains Backlog and the current records have neither substantive pipeline-doc directories nor group membership. TICK-194/195/197/200 remain unarchived with EPIC-001 membership. Renderer tickets retain their prior archive state: TICK-209/210 remain archived; the other 12 remain active.

3. **Non-blocking — FRD mapping passed.** The current board contains exactly 44 FRD link occurrences and 16 unique file/anchor pairs. Every file exists, every generated heading slug resolves, and no generic `[Requirements]` link label remains.

4. **Non-blocking — citation cleanup passed.** A recursive search of non-migration ticket bodies and pipeline documents finds zero NOW.md / requirements.md references. The six planned historical pipeline files are clean.

5. **Non-blocking — concurrency/integrity checks passed.** Current `kanmer-board` is clean and synced at `8fe88bbb`; the migration preserved concurrent substantive TICK-194/195/197/200 work. `git diff --check` passed for the implementation range, and no newly archived ticket carried a group or substantive document directory.

## Disposition

- Comment 1: **needs fix in board implementation before pass**. No implementation record was changed by this review.
- Comments 2–5: **accepted; no action required**.

## Verdict

**Needs changes.** The archive boundary, EPIC-001 preservation, FRD mappings/anchors, pipeline-document cleanup, and concurrency safety all pass. The sole blocker is the missing SIMPLI-015 linkage/context on TICK-203–TICK-216, which leaves the preserved renderer work disconnected from the consolidation owner required by the plan and recorded operator decision.

## Re-review — renderer consolidation link fix

- Confirmed TICK-203 through TICK-216 each now carries `SIMPLI-015` in its structured `links[]`; `get_links` resolves every relation to the renderer/document-extractor integration ticket.
- Confirmed archive states are unchanged: TICK-209 and TICK-210 remain archived; TICK-203–208 and TICK-211–216 remain active Backlog tickets.
- No remaining issue from the prior blocker.

**Verdict: PASS.** The renderer consolidation owner is now directly discoverable from every preserved renderer ticket, and the original review's other checks remain accepted.
