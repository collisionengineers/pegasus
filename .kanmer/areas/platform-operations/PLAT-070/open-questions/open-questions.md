# Open questions — PLAT-070

Raised by the cross-family plan review (2026-09-03, Claude). See
`plan/plan.md` → "Plan review (2026-09-03, Claude)", finding 7.

- [x] **What does `/Administration/Configuration` show once the "Staff review
  requirements" panel is gone?** Resolved 2026-09-03 by the controller:
  option **(b)** — keep the page and its Administration nav entry (Workflow
  configuration is one of the eight designed areas and PLAT-062 / D23 refill
  it with the completeness items and the chase interval), remove the inert
  form: the page renders the current policy version read-only, and the
  Reason field, Save button and the page's call to
  `UpdateWorkflowConfiguration` are removed until a real setting returns
  (repository rule 21: a control that gates nothing is deleted; design
  authority: absent, not inert). The Core `UpdateWorkflowConfiguration`
  command and the `WorkflowConfigurations` store stay because PLAT-062 is
  the scheduled caller; if PLAT-062 is cancelled they are deleted then.
  `WorkflowConfigurationWebTests.cs` is updated to prove the absence of the
  form. Reason for not (a): an operator-visible Save that changes nothing is
  a defect. Reason for not (c): it deletes a designed surface and is beyond a
  fix-profile diff.

## Parked (explicitly deferred)
