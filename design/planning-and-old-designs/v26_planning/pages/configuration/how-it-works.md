# Configuration — how it works (workflow settings)

Read from the live source on 13 September 2026. Only the workflow settings are covered here so far; the rest of the page (labour-rate cards, mailboxes, categories) is still to be written up.

- Page: `src/Pegasus.Web/Pages/Administration/Configuration.cshtml` (+ `.cs`). Workflow configuration is a versioned record edited under an edit lease; the read view lists the values, the edit form posts them with the expected version.
- Core: `CaseWorkflowConfiguration` in `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs`; administration in `src/Pegasus.Core/Workflow/WorkflowConfigurationAdministration.cs`.
- Persistence: `src/Pegasus.Infrastructure/Persistence/AdministrationPolicyEntities.cs` (default 7, check constraint 1 to 365).

Values today:

| Setting | Type | Range | Default | Used by |
| --- | --- | --- | --- | --- |
| Chase interval (days) | whole calendar days | 1 to 365 | 7 | the missing-material chase schedule: first chase = entered Not ready + interval at the same local time; each run adds the interval again (`CaseChaseSchedule`) |
| Instruction and image completeness rules | policy | — | — | `CaseCompletenessPolicy`, which decides readiness for engineer assignment |

Governing documentation: [FRD-12 § Administration](../../../../../docs/frd/frd-12-operator-experience.md#administration) ("workflow configuration holds the versioned instruction- and image-completeness policy and the chase interval as one global whole-calendar-day value, 1 to 365, default 7"); [FRD-01](../../../../../docs/frd/frd-01-case-identity-and-lifecycle.md) for `Due by` and what stops a chase.
