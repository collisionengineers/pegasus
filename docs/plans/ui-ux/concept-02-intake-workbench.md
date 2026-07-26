# Historical/unapproved concept 2: intake workbench

Status: Retained historical concept and superseded as an active candidate by the direction-neutral [UI specification](ui-spec.md). It does not select a direction, authorise image generation, or set requirements.

![Intake workbench](mockups/concept-02-intake-workbench.png)

## Intent

Put incoming transport evidence, document/image evidence, and operator confirmation in one view. This is the strongest candidate for the first QDOS workflow because it exposes why the application proposes a value.

## Keep

- All / Instructions / Images filters.
- Attachment and document preview next to evidence found.
- Suggested values are visually distinct from confirmed values.
- Separate instruction and image completeness decisions.
- Block intake, Create incomplete, and Create for review are explicit actions. Block intake requires a reason and leaves the source in the inbox without a case/reference. Create incomplete accepts a case into `Not ready`; Create for review accepts a case into `Review` after the operator separately judges instructions and images complete. The configurable completeness gate is applied later at Engineer assignment, not at case creation.

## Change before implementation

- The generated Australian providers, addresses, dates, registration, and document content are visual filler.
- Principal must be a work provider, not a claimant/insured name.
- Show evidence source for each suggestion and contradictions across email, PDF, filename, and image.
- Make unsupported/corrupt files and transient extraction failures recoverable.
- Make allocation explicit at case acceptance. Once allocated, principal/reference are immutable; a later wrong-principal discovery uses the separate reasoned `Created in error` replacement flow rather than editing this screen.

## Deferred-capability impact

The [UI planning impact register](README.md#deferred-capability-impact) applies. This concept preserves source occurrence, evidence provenance, confirmed-versus-suggested values and explicit unsupported outcomes for later mailbox, document, guided-capture and AI/vision work. It does not define the V1 exact-match predicates or automatic-VRM mechanism, and it does not authorise a runtime rules editor, WhatsApp intake, later case-type form, or generic provider workflow; each needs its owning contract, caller, and accepted evidence.
