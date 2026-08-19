# Open questions

The two Case-origin types and the exact match condition are now confirmed. The following contract decisions must be settled before the Image-initiated persistence/UI branch is completed:

- [x] **No usable or ambiguous VRM:** The intact submission group enters [[INTK-007]] Unidentified as one grouped work item with one shared immutable `U<n>` reference until staff resolves the VRM. Every member remains under that same reference. Do not allocate one U-reference per file and do not fabricate an Image-initiated VRM reference. — Operator decision, 2026-08-19.
- [x] **Persistence identity:** Evolve/promote the existing pre-instruction `ImageIntake` aggregate as the persistence owner for the user-facing Image-initiated Case. Do not insert a principal-less row into the formal `Cases` model or weaken its Principal/Case/PO invariants. The later Instruction-initiated Case remains a separate formal Case linked to this permanent origin. — Resolved from existing architecture and the confirmed two-origin model, 2026-08-19.
- [ ] **Association lifecycle:** When an Image-initiated Case later matches an Instruction-initiated Case, does the image-origin Case remain as a linked source record, become a closed/converted origin, or use another explicit state? In all options, are both references and all history permanent?
- [ ] **Operational treatment:** Which lifecycle states, queues, search results, Operations counts, custody rules, and permissions apply to Image-initiated Cases? Confirm they are distinct from Instruction Case/PO, Audit, and Unidentified references.
- [ ] **Reference allocation:** Confirm normalized VRM rules, per-VRM atomic sequence starting at 01, replay/concurrency behavior, and no-reuse after association or correction.

## Parked (explicitly deferred)

- None.
