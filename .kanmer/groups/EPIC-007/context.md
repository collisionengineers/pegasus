# Shared context — EPIC-007

## Binding outcome

This batch is one product workflow:

1. **Grouped submission:** files selected together retain a durable submission-group identity while each file keeps its own immutable source/receipt identity.
2. **Vehicle-image routing:** recognition evaluates all images in the group. If the group yields one confident, unambiguous VRM with exactly one eligible Instruction-initiated Case match, every group image associates to that formal Case. If it yields one usable VRM but no unique formal match, the existing ImageIntake route owns one Image-initiated Case reference for the complete group. If it yields no usable VRM, or conflicting valid VRMs, the complete group enters Unidentified.
3. **Unidentified work:** received documents, images, emails, attachments, or groups whose identity, meaning, ownership, or destination cannot be established enter **Unidentified**, receive the next immutable `U<n>` tracking reference, and retain a required reason and resolution history.

## Case-origin model

- **Instruction-initiated Case:** the main/formal type, created from official instructions; it uses Principal and Case/PO identity rules and may initially have no images.
- **Image-initiated Case:** the secondary/pre-instruction type, represented by the existing ImageIntake aggregate and VRM-sequenced reference such as `AB12ABC-01`; it has no formal Case/PO and remains searchable until merged/subsumed into a matching Instruction-initiated Case or staff-closed with a reason.
- A later exact, non-overlapping VRM match closes the Image-initiated Case into the Instruction-initiated Case, preserving merge history on both sides.

## Boundaries

- A U-reference is never a Case/PO, Audit reference, principal identity, or evidence that allocation gates passed.
- Triage, Blocked intake, Audit, Image-initiated Cases, and Unidentified keep their distinct meanings.
- A registration-free damage close-up follows a readable sibling in its submission group.
- Conflicting identification never attaches a group to an existing Case and uses the explicit `conflicting_vrms` Unidentified reason.
- Preserve original files, filenames, custody, replay identities, and group membership.
- `INTK-005` supplies durable group identity; `INTK-006` supplies grouped recognition and existing-Case association.
- `INTK-007` owns Unidentified references/reasons; `INTK-008` owns Image-initiated lifecycle, search/history, custody presentation, merge, and staff closure.

## Documentation constraint

The Image-initiated and Unidentified vocabulary is settled product behaviour. Governing documentation must be reconciled before implementation; code or UI strings cannot silently establish the new policy.
