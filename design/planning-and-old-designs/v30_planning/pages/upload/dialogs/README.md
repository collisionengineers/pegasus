# Dialogs

The mockup offers two local confirmations: the exact selected Case before
adding files, and discard with one short retained-record consequence. The
dialog actions only change the mockup query state. The live version and roster
checks remain Core/Web responsibilities.

## New Upload designs: dialog coverage

- Review: exact Case/PO, registration, claimant, Principal, stage, file count and
  mixed-file warning. Cancel starts focused; confirmation names the Case.
- File preview: original filename, type/size, photograph, Previous / Next and an
  onward original-file destination. No preview is invented for unsupported media.
- Discard: exact scope, retained-source consequence and required acknowledgement.
- Onward destinations: explicitly labelled offline previews of existing screens.

Native dialogs use modal isolation, focus containment, Escape, outside-click
cancellation and focus return. [Behaviour decisions](../../../current/upload-design-proposals.md#decisions-for-this-round)
remain open; these are not new implemented application dialogs.
