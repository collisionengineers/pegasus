# Shared context — EPIC-007

## Binding outcome

This batch is one product workflow, not three independent UI fixes:

1. **Grouped submission:** files selected together retain a durable submission-group identity while each file keeps its own immutable source/receipt identity.
2. **Vehicle-image routing:** recognition evaluates all images in the group. If the group yields one confident, unambiguous VRM with exactly one eligible case match, every group image associates to that case. Otherwise Pegasus creates one Image-Only case containing the group.
3. **Unidentified work:** received documents, images, emails, attachments, or groups whose identity, meaning, ownership, or destination cannot be established enter **Unidentified**, receive the next immutable `U<n>` tracking reference, and retain a required reason and resolution history.

## Boundaries

- A U-reference is never a Case/PO, Audit reference, principal identity, or evidence that allocation gates passed.
- Triage, Blocked intake, Audit, and Image-Only cases keep their distinct meanings.
- A registration-free damage close-up follows a readable sibling in its submission group.
- Conflicting identification never attaches a group to an existing case.
- Preserve original files, filenames, custody, replay identities, and group membership.
- `INTK-005` supplies the durable group identity required by `INTK-006`.
- `INTK-007` is a wide governed replacement of the former broad `Needs sorting` meaning; it is not merely a label change.

## Documentation constraint

The Unidentified vocabulary and exhaustive vehicle-image outcome change settled behaviour and protected operator truth. Governing documentation must be reconciled before implementation; code or UI strings cannot silently establish the new policy.
