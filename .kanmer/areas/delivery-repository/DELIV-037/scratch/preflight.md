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

## Docs pass — measured staleness, 2026-08-29

The release skill's step 10 says the release "is unfinished until both
current-state documents match what was actually deployed". Measured against
`dev` before the deploy, they do not come close. Occurrence counts:

| Term | `current-architecture.md` | `operations.md` |
| --- | --- | --- |
| Integrated Operations Workspace | 0 | 0 |
| `AiJobs` | 0 | 0 |
| `NamedEstimates` | 0 | 0 |
| `CaseValuations` | 0 | 0 |
| `VehicleImages` | 1 (as a list page that no longer exists) | 0 |

So the four subsystems this release ships are documented nowhere in the
as-built snapshot, and the one stale mention describes a deleted page.

**Release-number prose is wrong in two places, not one.** `operations.md:296`
reads "the estate currently serves **release 35**" and `:385` says "served
release 35 unchanged throughout", while the release table at `:314` correctly
records **36**. Release 36's commit added its table row and missed the prose —
and release 35's evidently did too. Fix all three, and add release 37's row plus
its prose entry, rather than repeating the same miss a third time.

Also to correct in the same pass:

- `operations.md:121` — "Not implemented: no endpoint, client, credential, or
  caller" for the Provider API. Already stale against source; **actively false**
  once this release deploys the enabled gate. Rewrite it to say the endpoint and
  gate are live and that no credential has been issued, which is what actually
  closes the route.
- `open-decisions.md:57` — "Operations must not imply that `Features:SendToAi`
  or `Features:AutomationMcp` is production enabled." `Features__AutomationMcp`
  has been `true` in `infra/modules/platform.bicep` since ADR-0026, so half that
  sentence is now wrong. `Features:SendToAi` genuinely remains disabled and
  cannot be enabled (`SendToAi.cs:42` throws outside DevelopmentOffline), so the
  constraint survives for SendToAi alone. Split it rather than deleting it.

None of this is written before the deploy. Per the skill it is written **after**,
carrying the observed SHA, manifest hash, image digest, revision and migration
head, then delivered by reviewed PR to `dev` and put on `main` by a **second,
freshly authorised promotion-only pass**.
