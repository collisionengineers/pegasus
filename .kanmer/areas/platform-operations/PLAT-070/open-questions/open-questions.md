# Open questions — PLAT-070

Raised by the cross-family plan review (2026-09-03, Claude). See
`plan/plan.md` → "Plan review (2026-09-03, Claude)", finding 7.

- [ ] **What does `/Administration/Configuration` show once the "Staff review
  requirements" panel is gone?** Verified on `dev`: that panel is the page's
  *entire* editable form. `Configuration.cshtml.cs` binds only
  `RequireStaffInstructionReviewBeforeEngineerAssignment`,
  `RequireStaffImageReviewBeforeEngineerAssignment`, `ExpectedVersion`,
  `OperationKey` and `Reason`; `Configuration.cshtml`'s only other content is
  the `Description` subtitle ("Staff review requirements"), the Reason field
  and the Save button. After D44 the page therefore ships a required Reason
  plus a Save button that records an audit entry and bumps `PolicyVersion`
  while configuring nothing — a control that gates nothing (repository
  conduct rule 21) and a surface the design authority would have absent
  rather than inert. `docs/design/README.md:1060` shows the *designed*
  Workflow configuration page eventually carrying Instruction/Image
  completeness rules (D23), Due work and Labour-rate cards, so the page is
  not permanently empty — but none of those is shipped or scheduled in
  EPIC-012 wave 1, and neither D44 nor the ticket body says what the page
  shows in the interim. Choose one:
  - **(a) Keep the page and the form as-is** (empty of settings until the
    D23/Due-work/labour-rate panels land). Smallest diff; PLAT-070 stays a
    panel deletion. An operator can still press Save and bump the policy
    version for nothing.
  - **(b) Keep the page, remove the form.** Render the current policy version
    read-only and drop the Reason/Save/`UpdateWorkflowConfiguration` path from
    the page until a real setting returns. No inert control ships; slightly
    larger diff (page model, `WorkflowConfigurationWebTests.cs`), and the
    Core `UpdateWorkflowConfiguration` command temporarily loses its only
    caller (repository rule 14 — "done means wired" — would then apply to it).
  - **(c) Retire the whole surface** — the route, its Administration nav entry,
    `GetWorkflowConfiguration`/`UpdateWorkflowConfiguration`, the
    `WorkflowConfigurations` store and the `ManageWorkflowConfiguration`
    right. Cleanest against rule 21, but it deletes a surface the design
    authority still lists and is well beyond a `fix`-profile diff; it would
    need its own ticket.

  This review's own recommendation is **(a)** for PLAT-070 with **(b)** or
  **(c)** raised as a follow-up ticket in a later wave, because it keeps this
  ticket to the deletion D44 actually names — but the choice is visible to
  operators, so it is the operator's, not the implementer's.

## Parked (explicitly deferred)
