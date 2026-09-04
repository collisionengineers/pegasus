# Open questions — CASE-032

Both questions raised by the 2026-09-04 plan are resolved from existing
repository authority, not by a new operator decision. The ticket's own Approach
states the owners already exist in Core; the plan review located them and the
dispositioning agent verified each read-only at `80f0ca26`. No new business rule
is invented and no schema change follows.

- [x] **What reference must the Triage row display?** `InstructionDraft.ClaimNumber`
      (`src/Pegasus.Core/Intake/IntakeContracts.cs:382`), persisted per intake
      receipt on `InstructionDrafts` and reachable from
      `TriageEntity.OriginReceiptId`. `docs/operator-notes.md:221` defines it:
      "Claim Number — External reference number." It is the external reference
      Pegasus already carries; no Pegasus-allocated Triage identifier is
      invented, and FRD-01 keeps Case/PO allocation to Cases. Absent when the
      origin carries no instruction draft, in which case the row's `Join`
      renders the registration alone.

- [x] **What provider value must the Triage row display, and what when none is
      known?** `InstructionDraft.SuggestedPrincipalCode`
      (`src/Pegasus.Core/Intake/IntakeContracts.cs:380`).
      `docs/operator-notes.md:219` defines it: "Work Provider — Also referred to
      as the principal." `src/Pegasus.Core/Intake/IntakeAllocation.cs:263`
      already reads `receipt.InstructionDraft?.SuggestedPrincipalCode` as the
      principal code, so this reuses Core's existing owner rather than adding a
      Web display string. It is populated for mail-route and Provider API
      origins; manual classification may leave it null (FRD-03 invents no
      Principal identity there), and an absent provider renders nothing — the
      meta shows the assignee alone, per `docs/design/README.md`.
