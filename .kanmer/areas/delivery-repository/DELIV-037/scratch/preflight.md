## 2026-08-29 — two corrections to the ticket body, both verified

**1. The migration head has moved, and there are ten pending, not nine.**
The plan recorded `20260828185508_ProviderDeclaredInstruction` as the head. ENG-027
(#621, Case valuations) has since merged and added
`20260829095336_CaseValuations`, which is now the head. Release 36 applied up to
and including `20260827143200_GrantEvaSubmissions` (`docs/operations.md:314`), so
the pending set for release 37 is exactly:

| # | Migration | Grants? |
| --- | --- | --- |
| 1 | `20260828084601_AiJobs` | |
| 2 | `20260828084644_GrantAiJobs` | yes |
| 3 | `20260828104130_PrincipalApiCredentials` | |
| 4 | `20260828104139_GrantPrincipalApiCredentials` | yes |
| 5 | `20260828110108_CaseEditLeaseHolderKind` | |
| 6 | `20260828111707_ProviderSubmissions` | |
| 7 | `20260828111732_GrantProviderSubmissions` | yes |
| 8 | `20260828112103_NamedEstimates` | |
| 9 | `20260828185508_ProviderDeclaredInstruction` | |
| 10 | `20260829095336_CaseValuations` | |

Three grant-carrying, not four. AUTO-013 and AUTO-012 may add more before the
release SHA is fixed — re-derive this table against the promoted SHA rather than
trusting it.

**2. The 2026-08-14 Worker grant hotfix is NOT a risk. It was codified.**
The ticket body lists it as the highest-risk item, on the strength of a
session note saying the live `GRANT` was never captured in a migration and would
regress on the next bootstrap. **That note was stale.** The hotfix was
subsequently written up as
`src/Pegasus.Infrastructure/Persistence/Migrations/20260814092852_AddWorkerCaseCreationGrants.cs`,
which grants `pegasus_worker_runtime_role` the eighteen tables
`EfCaseAcceptanceStore.AcceptOnceAsync` writes in its single batch — `Cases`,
`CaseSequences`, `CaseMatchIndex`, `CaseIntakeLinks`, `CaseHistory`,
`CaseWorkflows`, `CaseDataSnapshots`, `CaseDataFields`, `CaseDueWork`,
`ExternalWorkItems`, `IntakeMutationHistory`, `StandaloneAuditEvidence`,
`Principals`, `PrincipalSequenceLineages`, `Organizations`, `OrganizationRoles`,
`VehicleConfirmations`, `WorkflowConfigurations`.

The migration's own comment records why local testing never caught the original
gap: "Local/LocalDB tests run full-privilege and never exercised the
least-privilege role, so this only ever failed against the deployed estate."
Its grants are additive to the reconciliation baseline, `DELETE` stays denied
everywhere for the Worker, and its `Down` revokes only what it added.

Dated 2026-08-14, it is far below release 36's applied head, so it is already in
production and `Invoke-AzureDatabaseBootstrap.ps1` will not revoke it. **Struck
from the risk list.** The grant census read-back stays in the verification list
as evidence, not as mitigation for a live threat.

## Live state read read-only before any write, 2026-08-29

- `az` on this workstation needs `AZURE_EXTENSION_DIR` pointed at an empty
  directory. One unreadable extension metadata file
  (`.azure/cliextensions/account/azext_account/azext_metadata.json`) breaks the
  whole CLI while it builds the command table — even `az account show`. With an
  empty extension dir everything the release needs works, because
  `containerapp`, `functionapp`, `sql`, `acr` and `rest` are all core commands.
- Web active revision `pegasus-prod-web-252ow37gij--84132d01ccb0`, digest
  `sha256:5ba65f61ad754639185764ed2c7795fc06938e6e397a3a9d5c7f7fe5c01bb032`,
  created 2026-08-28T02:54:27Z, 1 replica — matches the release 36 row exactly.
- Worker census is exactly the expected seven functions.
- Web carries one `Features__*` setting: `Features__AutomationMcp=true`.
  `Features__ProviderApi` is absent and therefore closed.
- `azd`, `oras` and PowerShell 7.6.5 are all present on PATH.
