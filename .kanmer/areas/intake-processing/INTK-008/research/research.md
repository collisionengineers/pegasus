# Research — INTK-008

## Question

Can the existing ImageIntake route become the user-facing Image-initiated Case lifecycle without creating a second formal Case store, weakening Principal/Case/PO allocation, or losing grouped evidence, history, searchability, and custody?

## Findings

1. ImageIntake already is the image-first persistence route. ImageIntakeContracts.cs defines the immutable record, VRM reference, query/store ports; EfImageIntakeStore.cs allocates the next per-VRM sequence transactionally in ImageIntakeSequences, writes ImageIntakes, and changes the origin receipt to ImageIntakeRegistered. A readable VRM is registered even when zero or multiple formal Case candidates exist; association is a separate exact/unique decision.

2. There is no hidden principal-less formal Case path. CaseContracts.cs requires Principal, CaseType, and completeness in CaseAcceptanceRequest; EfCaseAcceptanceStore.cs allocates formal Case/PO identity only after those gates. INTK-008 must not add a Cases row, fake a Principal, or reuse Case/PO numbers.

3. Current lifecycle is implicit and receipt-derived. ImageIntakeDetail/Summary expose only association and registration time. ImageIntakeEntity is otherwise immutable. ImageIntakeCasePairing.cs automatically links a waiting ImageIntake when a newly accepted formal Case is the one exact eligible match. There is no persisted awaiting/merged/staff-closed state or explicit merge/closure history.

4. The existing UI is searchable and permissioned but uses the wrong vocabulary. ImageIntake/Index supports all/awaiting/associated filters and exact reference or VRM search. Cases/Index includes ImageIntake results and Cases/Details lists associated ImageIntake references. ImageIntake/Details is read-only and has no close action or lifecycle history. Existing Administrator/Engineer/User authorization is the required boundary.

5. Custody is formal-Case-shaped. CustodyContracts.cs exposes ICaseCustody keyed by formal CaseId/reference, and BoxCaseCustody.cs implements the approved-root/descendant adapter. FRD-05 distinguishes staging from long-term Box custody and prohibits local-alpha external mutation. The implementation must reuse that Box boundary while adding the smallest safe ImageIntake target keyed by the immutable VRM reference, without pretending it is a formal Case.

6. Group origin and filenames are already retained below ImageIntake. IntakeReceipt/assets preserve source identity, original filename, ordinal, content hash, storage key, and replay events. INTK-005/006 provide durable submission-group membership; INTK-008 must query it rather than copy assets.

7. Existing history seams are reusable. IIntakeMutationStore.AutoLinkAsync records reasoned association events and current receipt links. Formal Case details already load associated ImageIntake records. New lifecycle transitions need an append-only state/event record with operation-key idempotency and actor/reason.

8. Governing conflicts are real. CONTEXT.md, operator-notes.md, FRD-01/02/06/12, design README, capabilities, and ADR-0013 currently describe image-only work as pre-Case. ADR-0013 is accepted and immutable; a new ADR must supersede it. EPIC-007 context now establishes the two origins, conflicting-VRM Unidentified hand-off, and later merge/subsumption with both identities preserved.

## Implications

Model Image-initiated Case as a named lifecycle projection over ImageIntake, not CaseEntity. Keep its VRM reference separate from Case/PO, Audit, and U<n>. Add explicit state, merge target, staff-close reason, and append-only history with replay/CAS. Invoke merge from reverse pairing only after association succeeds. Reuse list/search/case-detail queries and authorization. Extend custody through the existing Box composition root; do not add a deployment unit or second Box client.
