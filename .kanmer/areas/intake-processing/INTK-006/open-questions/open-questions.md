# Open questions

The two Case-origin types and the exact match condition are now confirmed, but the following contract decisions remain unresolved and must be settled before the Image-initiated persistence/UI branch is completed:

- [ ] **No usable or ambiguous VRM:** An Image-initiated Case requires a VRM-based reference. Should a group with no usable VRM, or conflicting accepted VRMs, enter [[INTK-007]] Unidentified as one grouped U<n> work item until staff resolves a VRM, rather than receive an Image-initiated reference?
- [ ] **Persistence identity:** Is an Image-initiated Case a first-class Case row/type, or should the existing ImageIntake aggregate be recast/promoted? The current Cases model requires PrincipalId; the current ImageIntake contract explicitly says pre-Case.
- [ ] **Association lifecycle:** When an Image-initiated Case later matches an Instruction-initiated Case, does the image-origin Case remain as a linked source record, become a closed/converted origin, or use another explicit state? In all options, are both references and all history permanent?
- [ ] **Operational treatment:** Which lifecycle states, queues, search results, Operations counts, custody rules, and permissions apply to Image-initiated Cases? Confirm they are distinct from Instruction Case/PO, Audit, and Unidentified references.
- [ ] **Reference allocation:** Confirm normalized VRM rules, per-VRM atomic sequence starting at 01, replay/concurrency behavior, and no-reuse after association or correction.

## Parked (explicitly deferred)

- None.
