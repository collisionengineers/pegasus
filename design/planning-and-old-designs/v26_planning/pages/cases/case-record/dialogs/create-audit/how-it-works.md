# Create audit — how it works today

Read from the live source and the governing documents on 13 September 2026. There is no Create audit action today.

- Case types are Inspection, Audit and Inspection and Audit (`CaseType` in `src/Pegasus.Core/Cases/CaseContracts.cs`; operator label "Inspection and audit" in `src/Pegasus.Web/Presentation/OperatorLabels.cs`).
- A **standalone Audit** carries its `a.` or `ap.` reference as a second reference on the same Case identity (`CaseIdentity.AuditReference`). It is derived at creation: on the retained-e-mail route from the literal outcome read in the original report, on the Provider API route from the Principal's declared verdict. Missing, conflicting or ambiguous evidence withholds only that reference; the normal Case/PO is still allocated. The Box folder for it comes from the custody port's `CreateAuditReferenceFolder`. ([FRD-01 § Principal, reference, organisation, and case-party identity](../../../../../../../../docs/frd/frd-01-case-identity-and-lifecycle.md))
- An **Inspection + Audit** Case begins as a normal Inspection Case with the normal reference. Nothing in Pegasus creates the Audit. After the Engineer produces the Audit report through EVA, the Engineer creates the `a.{Case/PO}` or `ap.{Case/PO}` Box subfolder by hand under the Case's folder; FRD-01 defers any Pegasus-made folder to the EVA replacement.
- CONTEXT.md defines Inspection + Audit as one Case in which the Audit "retains its own identity, evidence, and acceptance boundary".
- The v2 prototype in the private pack had an "Original report verdict" dialog that wrote `auditRef` onto the same Case and swapped its displayed reference; the v26 mockup did not carry that over.

Governing documentation: [FRD-01](../../../../../../../../docs/frd/frd-01-case-identity-and-lifecycle.md) (reference rules; Audit; Inspection + Audit), [FRD-11](../../../../../../../../docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md) (the Audit report), [CONTEXT.md](../../../../../../../../CONTEXT.md) (Audit, Inspection + Audit).
