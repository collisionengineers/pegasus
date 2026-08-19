# Open questions — INTK-008

- [x] Is ImageIntake the image-initiated route? Yes. Reuse it; do not create a second Case store or formal Cases row.
- [x] Can an Image-initiated record receive a VRM reference? Yes, when the two-layer vision/VRM pipeline produces one usable registration; use the existing per-VRM Image Intake Reference sequence.
- [x] What happens when several valid VRMs are detected? INTK-007 owns one grouped Unidentified U<n> result with reason marker conflicting_vrms; this ticket must not register a fabricated VRM.
- [x] What happens when a formal Case later matches? The Image-initiated record becomes terminal merged/subsumed, links to the Instruction-initiated Case, and both histories show the relationship.
- [x] What happens when instructions never arrive? Staff may permanently close the Image-initiated record with an auditable reason; no generic close and no silent deletion.
- [x] Must Image-initiated records be searchable and permissioned? Yes. Reuse the existing Cases/search projection and Administrator/Engineer/User staff authorization.
- [x] Where is custody? Box under the immutable Image Intake Reference, via the existing approved Box root/adapter boundary; local alpha must use the existing non-mutating/fake custody profile.
- [x] Must formal Case/PO allocation change? No. Formal Instruction-initiated Cases retain the Principal and Case/PO allocator; Image-initiated references remain separate.
- [x] How is accepted ADR-0013 changed? It is not edited. Add the next monotonic accepted superseding ADR and update the index/frontmatter relationships.
- [x] Does this ticket own Unidentified or grouped recognition? No. INTK-006 and INTK-007 remain the owners; this ticket consumes their established outcomes.

## Parked (explicitly deferred)

- [x] A future operator workflow may add richer bulk Image-initiated actions; this ticket provides only the required one-record merge and staff-close actions.
