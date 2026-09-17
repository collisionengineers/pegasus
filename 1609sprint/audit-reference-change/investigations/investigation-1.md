\## Overall assessment



\*\*The change is feasible, but it should be treated as a case-identity rule change—not a global `ap.` → `a.` replacement.\*\*



The prefix selection itself is centralised. However, Pegasus currently has \*\*several different ways of creating and storing audit identities\*\*, and those identities flow into case displays, Box custody, EVA submissions, reports, history, and retry handling. A formatter-only change would leave at least one important inconsistency: corrected-principal audit cases can still have an \*\*unprefixed primary reference\*\*.



My assessment of the scope is:



| Scope                                                                      | Assessment                                                                                                 |

| -------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |

| All \*\*newly created\*\* audit references use `a.`                            | A contained implementation change with a substantial regression-testing requirement.                       |

| Audit references also become allocatable \*\*before an assessment is known\*\* | A further intake/workflow change. It should not happen implicitly as part of changing the prefix.          |

| \*\*Existing\*\* `ap.` cases are also renamed                                  | A materially larger data and external-storage migration, with compatibility and historical-evidence risks. |



\*\*Recommended approach:\*\* introduce `a.` for new audit identities, preserve existing references initially, and handle any retrospective renumbering as a separately specified migration.



\*\*Review baseline:\*\* GitHub `main`, commit `8b9d358f71ac02363a3e9fa50549d4fef0fcdac8`. This is a source-level assessment; I have not executed tests or inspected production database rows or live Box contents.



\---



\## 1. Precisely what the new rule should mean



For Pegasus’s current linked-case design, I recommend the following contract:



| Case or operation                                    | Reference under the proposed rule                                                                       |

| ---------------------------------------------------- | ------------------------------------------------------------------------------------------------------- |

| Ordinary inspection                                  | `QDOS26001`                                                                                             |

| Standalone repairable audit                          | `a.QDOS26001`                                                                                           |

| Standalone total-loss audit                          | `a.QDOS26001`                                                                                           |

| Original Inspection + Audit case                     | Retains its ordinary inspection reference, such as `QDOS26001`                                          |

| Audit case created from that Inspection + Audit case | `a.QDOS26001`, regardless of assessment                                                                 |

| Later change to an audit’s engineering outcome       | Does \*\*not\*\* change its reference                                                                       |

| Corrected-principal replacement of an audit          | A newly allocated `a.{new principal/year/sequence}` reference; the original identity remains historical |



The Inspection + Audit distinction matters: Pegasus now creates a \*\*separate linked Audit case\*\*, rather than merely adding an audit label to the inspection. The two records share the original sequence but have separate case identities.



Therefore, “includes Inspection + Audit” naturally means that \*\*its linked Audit case also always receives `a.`\*\*. Prefixing the original inspection as well would be an additional design change: parent and child could then compete for the same reference, or naïve prefixing could produce `a.a.QDOS26001`. The current database design expects unique case references and only one linked audit per original.



\*\*The reference should identify the audit, not describe its outcome.\*\* Keep repairable/total-loss information in the assessment fields. Do not remove `AuditAssessment.TotalLoss`, original-report verdicts, or settlement outcomes merely because they no longer select the prefix.



\---



\## 2. Important implementation findings



\### A. There is one prefix helper, but multiple identity-creation paths



`src/Pegasus.Core/Cases/CaseContracts.cs` currently contains:



```csharp

AuditAssessment.Repairable => "a.",

AuditAssessment.TotalLoss => "ap.",

```



inside `AuditIdentity.Create(...)`. That is the central formatting change. However, its callers include standalone acceptance, linked-audit creation, corrected-principal replacement, Engineer Finding, and the web reference preview.



The cleaner eventual interface is conceptually:



```text

AuditIdentity.Create(baseCaseReference) → a.{baseCaseReference}

```



Assessment validation should remain where assessment evidence enters the system. The formatter should not need an engineering verdict to identify an audit.



\### B. Audit identity is currently stored in different shapes



This is the most important complication I found:



| Creation path                                     | Current primary `Reference`   | Current `AuditReference`          |

| ------------------------------------------------- | ----------------------------- | --------------------------------- |

| Standalone audit acceptance                       | Prefixed `a.…` or `ap.…`      | `null`                            |

| New linked Audit case                             | Prefixed `a.…` or `ap.…`      | Same prefixed value               |

| Corrected-principal audit replacement             | \*\*Unprefixed\*\* base reference | Prefixed audit reference          |

| Older Engineer Finding path on Inspection + Audit | Parent’s unprefixed reference | Newly assigned prefixed reference |



These are verified differences in the persistence implementations, not merely different UI labels.



\*\*Consequence:\*\* changing the helper to return `a.` everywhere does not, by itself, ensure that every Audit case’s primary Case/PO begins with `a.`.



The corrected-principal path deserves an actual correction, not just changed expectations. This also affects external output: `EvaSubmissionStore` passes `caseData.Identity.Reference` to the EVA payload mapping. An audit replacement that retains a plain primary reference can consequently still be submitted under that plain reference.



\### C. The older Engineer Finding path is still reachable



It is not simply an unused historical class:



\* `DependencyInjection.cs` still registers `IRecordEngineerFinding`.

\* `Workflow.cshtml.cs` exposes `OnPostRecordEngineerFindingAsync`.

\* Its persistence implementation still writes an audit identity onto the original case.



This should be reconciled with the newer “Create audit creates a separate case” model. Otherwise, the new prefix rule will be applied to \*\*two competing audit-identity mechanisms\*\*.



That inconsistency predates this proposal, but the reference change is a good point to resolve it deliberately.



\### D. Documentation and acceptance code already disagree



FRD-01 says missing or ambiguous original-report evidence withholds only a later Audit reference while allowing a normal Case/PO to be created.



The current acceptance implementation instead requires standalone-audit evidence before accepting an Audit, and puts the prefixed reference directly into the case’s primary `Reference`.



\*\*Do not carry this contradiction into the new documentation.\*\*



There are two legitimate policies:



| Policy                                                               | Consequence                                                                                                                   |

| -------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |

| Change the prefix only                                               | Keep existing original-report/evidence gates, but remove wording claiming those gates are needed to choose `a.` versus `ap.`. |

| Allocate identity once principal and Audit case type are established | Broader changes to intake, missing-evidence handling, evidence contracts, and readiness tests.                                |



For a referencing-only change, I would retain the existing gates initially. The requirement to have a report or assessment can remain a business requirement without being a prerequisite for calculating the prefix.



\---



\## 3. Relevant code files



\### Files requiring direct implementation attention



Paths below are relative to the repository root.



| File                                                                                                     | Required work or decision                                                                                                                                                                                                             |

| -------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |

| `src/Pegasus.Core/Cases/CaseContracts.cs`                                                                | Change `AuditIdentity`; separate identity formatting from assessment selection. Preserve assessment types and validation.                                                                                                             |

| `src/Pegasus.Core/Lifecycle/CreateAuditCase.cs`                                                          | Always generate `a.` for the linked Audit. Update comments, history examples, and the “derive the audit reference” refusal wording. Retain generated-report, permissions, edit-lease, and one-audit guards unless separately changed. |

| `src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs`                                        | Ensure every newly accepted standalone Audit has `a.` in its \*\*primary `Reference`\*\*. Update the outcome-dependent identity comments.                                                                                                 |

| `src/Pegasus.Infrastructure/Persistence/EfLinkedCaseReplacementStore.cs`                                 | Correct the primary-reference inconsistency for audit replacements. Preserve the original case’s identity and replacement relationship.                                                                                               |

| `src/Pegasus.Infrastructure/Persistence/EfRecordEngineerFinding.cs`                                      | Reconcile its identity-writing behaviour with separate linked-audit creation; do not overlook this path.                                                                                                                              |

| `src/Pegasus.Web/Pages/Cases/Workflow.cshtml.cs` and `src/Pegasus.Infrastructure/DependencyInjection.cs` | Update wiring/endpoint behaviour if the older identity-creation mechanism is retired or narrowed.                                                                                                                                     |

| `src/Pegasus.Web/Pages/Cases/Details.Frame.cs`                                                           | Keep `ProposedAuditReference` consistent with the actual creation rule; decide separately whether the preview should remain hidden until an outcome exists.                                                                           |



These changes follow directly from the inspected helper, callers, persistence implementations, and web endpoint.



\### Files requiring contract review or downstream regression coverage



These are \*\*not all necessarily implementation edits\*\*.



| Area and files                                                                                                                                                                                      | Impact                                                                                                                                                                                                              |

| --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |

| `src/Pegasus.Core/ProviderApi/ProviderInstruction.cs`                                                                                                                                               | The declared original-report verdict currently has comments explicitly linking it to prefix selection. Keep the verdict as evidence; changing reference format does not require removing or renaming the API field. |

| `src/Pegasus.Core/Intake/AcceptIntake.cs`, `ProcessIntake.cs`; `src/Pegasus.Infrastructure/Persistence/EfStandaloneAuditEvidenceStore.cs`                                                           | Review outcome/evidence gates. Behavioural changes here are needed only if allocation timing is also being changed.                                                                                                 |

| `src/Pegasus.Infrastructure/Persistence/EfCreateAuditCaseStore.cs`                                                                                                                                  | Verify saved identity fields, parent/child links, history, replay behaviour, and custody work. It consumes the supplied reference rather than choosing the prefix itself.                                           |

| `src/Pegasus.Core/Custody/CustodyContracts.cs`; `src/Pegasus.Infrastructure/Custody/BoxCaseCustody.cs`, `LocalCaseCustody.cs`; `src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs` | Test creation and recovery with new `a.` identities and retained `ap.` identities. Historical renaming would require additional storage reconciliation.                                                             |

| `src/Pegasus.Infrastructure/Persistence/EvaSubmissionStore.cs`                                                                                                                                      | Verify total-loss audits transmit the canonical `a.` reference and that replay does not trigger a second submission.                                                                                                |

| `src/Pegasus.Core/Reports/AssessmentReportProjection.cs`; `src/Pegasus.Infrastructure/Reports/AssessmentReportLayout.cs`, `QuestPdfAssessmentReportRenderer.cs`                                     | Test reference text, footers, assessment/fee-note filenames, and correct outcome presentation independently of the prefix.                                                                                          |

| `src/Pegasus.Infrastructure/Persistence/CaseMatchEntities.cs`                                                                                                                                       | Preserve the intentional exclusion of linked Audit cases from automatic intake matching. Do not collapse inspection and audit identities by stripping prefixes.                                                     |



The inspected report renderer derives filenames from `OurReference`; the layout also renders that reference in footers. The match-index projector deliberately excludes linked audits to avoid making matches ambiguous between parent and child.



\### What does not inherently need changing



For new identities only, this proposal does \*\*not inherently require\*\* a new principal/year sequence, different case GUIDs, larger reference columns, or removal of the existing audit-evidence tables. The current linked-audit schema already supports a separate child sharing the original sequence.



I would also avoid dropping `AuditReference` as part of this change. Its inconsistent use needs addressing, but removing a persisted and exposed field is a separate compatibility change.



\---



\## 4. Documentation implications



\### Active requirements and guidance



| Document                                              | Recommended update                                                                                                                                                       |

| ----------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |

| `CONTEXT.md`                                          | Replace the repairability-dependent identity explanation with the new rule and its effective scope.                                                                      |

| `docs/capabilities.md`                                | Update \*\*CASE-04\*\* and \*\*CASE-08\*\*. Keep \*\*CASE-09\*\*, reference immutability, unless retrospective renumbering is explicitly approved.                                   |

| `docs/frd/frd-01-case-identity-and-lifecycle.md`      | Make this the authoritative specification: all audit references use `a.`; explain standalone versus linked audit; settle allocation timing and historical compatibility. |

| `docs/frd/frd-02-intake-and-source-identity.md`       | Remove contradictory “later audit reference” wording or deliberately retain a clearly specified staged process.                                                          |

| `docs/frd/frd-09-provider-and-intermediary-routes.md` | Explain that the declared verdict is assessment evidence, not a prefix selector. Update API examples and acceptance expectations.                                        |

| `docs/adr/0002-dotnet-modular-monolith-on-azure.md`   | Add an explicit amendment/supersession note for the `a.`/`ap.` rule while retaining the shared-sequence rationale.                                                       |

| `docs/runbook.md`                                     | Add the cutover checks, legacy-reference handling, required test lanes, and any approved migration procedure.                                                            |



The capability inventory currently states “Repairable `a.` and total-loss `ap.` Audit references” explicitly. FRD-01 and the Provider API documentation also encode outcome-dependent reference selection. These are actual conflicting requirements after the proposed change, not merely stale example strings.



A suitable new requirement would be:



> Every newly allocated Audit case reference uses `a.` followed by its allocated base reference, irrespective of original-report verdict or subsequent assessment outcome. An Audit created from an Inspection + Audit case uses the original case’s base reference without consuming another sequence. Reference allocation and assessment recording are separate concerns.



Then explicitly state the policy for existing `ap.` records.



\### Historical documents and examples



\*\*Do not globally rewrite archived decisions or genuine reference material.\*\*



The repository includes old design plans, a v26 self-check, report-renderer samples, and EVA comparison output containing `ap.`. They need classification:



| Material                                              | Treatment                                                                       |

| ----------------------------------------------------- | ------------------------------------------------------------------------------- |

| Active requirements or executable acceptance examples | Update to the new rule.                                                         |

| Historical design decisions                           | Mark as superseded rather than pretending they always specified `a.`.           |

| Genuine source documents and historical exports       | Preserve unchanged.                                                             |

| Synthetic current-output fixtures                     | Regenerate deliberately, including associated filenames and checksum manifests. |

| Legacy compatibility fixtures                         | Retain `ap.` intentionally and label their purpose.                             |



Examples include `reference/rendererref1/sample\_job\_\*.json`, the archived Create audit design/self-check, and the `EVA-ap.QDOS26015` comparison-output directory.



A repository check that simply forbids every occurrence of `ap.` would therefore be the wrong guardrail.



\---



\## 5. Testing-file impacts



\### Definite changes to existing expectations



| Test file                                                      | Impact                                                                                                                                                                                                                                                                                  |

| -------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |

| `tests/Pegasus.Core.Tests/Lifecycle/CreateAuditCaseTests.cs`   | Change the total-loss creation expectation from `ap.QDOS26214` to `a.QDOS26214`. Rename `TheAuditReferenceIsDerivedFromTheRecordedOutcome` so it no longer describes the old policy. Keep assertions proving the assessment remains `TotalLoss`. Update new-event history expectations. |

| `tests/Pegasus.IntegrationTests/ProviderApiSubmissionTests.cs` | The current total-loss API test explicitly asserts `Assert.StartsWith("ap.", caseReference, ...)`. This must expect `a.`.                                                                                                                                                               |

| `tests/Pegasus.IntegrationTests/CaseRecordFrameV26WebTests.cs` | Add/update new-reference preview and creation coverage. \*\*Do not automatically replace every existing `ap.` fixture:\*\* some test that an already-existing linked audit is displayed correctly.                                                                                          |



These expectations are present in the inspected tests. Also note that `CaseRecordFrameV26WebTests.cs` contains the partial class \*\*`CaseDetailsWebTests`\*\*—important when selecting tests by fully qualified class name rather than filename.



\### Existing suites to extend or explicitly rerun



The following are relevant regression locations, rather than a claim that every file contains a hard-coded `ap.` expectation:



| Concern                                       | Relevant files                                                                                                                                                                       |

| --------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |

| Standalone acceptance and allocation recovery | `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`, `QdosAllocationRecoveryTests.cs`, `CaseCreateWebTests.cs`                                                     |

| Engineer Finding and replacement paths        | `tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs`, `CaseWorkflowWebTests.cs`                                                                                          |

| Box/local custody and queued work             | `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs`, `ImageCaseCustodyIntegrationTests.cs`                                                                             |

| Intake and original-report evidence           | `tests/Pegasus.Core.Tests/Intake/ProcessIntakeTests.cs`, `AllocateDefinitiveIntakeTests.cs`, `Qdos/QdosMailClassificationPolicyTests.cs`                                             |

| Provider contract validation                  | `tests/Pegasus.Core.Tests/ProviderApi/ProviderInstructionPolicyTests.cs`                                                                                                             |

| Report projection and rendering               | `tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs`; `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs`, `AssessmentReportDraftWebTests.cs` |



These suites were located through the audit-evidence, workflow, custody, and report-reference searches.



\### Minimum acceptance matrix I would require



| Scenario                                                            | Required result                                                                           |

| ------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |

| Standalone audit: repairable and total loss                         | Both produce canonical `a.` references; the assessment remains distinct.                  |

| Linked audit: total loss, repairable, cash in lieu, contract repair | All produce `a.`; parent reference and shared sequence remain unchanged.                  |

| Corrected-principal audit replacement                               | Primary `Reference` is prefixed, not only `AuditReference`.                               |

| Assessment subsequently changes                                     | Reference does not change.                                                                |

| Missing report/outcome                                              | Follows the explicitly chosen gate policy; no accidental relaxation.                      |

| Duplicate or concurrent creation                                    | One audit case, one intended custody destination, no additional sequence.                 |

| Old operation retried after cutover                                 | No replacement identity or duplicate external work is minted.                             |

| Existing `ap.` case                                                 | Still readable, searchable, linked, and exportable according to the compatibility policy. |

| Reports and EVA                                                     | New total-loss audits carry `a.` while still being represented as total loss.             |

| Different principals and sequence boundaries                        | No QDOS-only assumption, truncation, sequence reuse, or prefix duplication.               |



Add a focused `AuditIdentityTests.cs` if there is no dedicated formatter test, but avoid having expected results calculated by the same production helper being tested. Otherwise, implementation and expectation can change together without testing the actual contract.



\---



\## 6. Local testing and CI implications



\### Existing CI already covers much of the change



The current `.github/workflows/ci.yml` runs:



| Lane                        | Relevance                                                           |

| --------------------------- | ------------------------------------------------------------------- |

| `documentation`             | Markdown placement and documentation links.                         |

| `unit`                      | Full Core and Architecture test projects.                           |

| `sql-integration`           | Integration tests in \*\*three shards\*\*, excluding `Category=Corpus`. |

| `sql-integration-coverage`  | Verifies that the enumerated tests were covered by the shards.      |

| `local-development-scripts` | LocalDB lifecycle classifier checks.                                |

| `reference-data`            | Python provider-reference generator tests.                          |

| `infrastructure`            | Conditional infrastructure/migration checks.                        |



These are the current workflow selections, not hypothetical CI arrangements.



\*\*No new workflow is inherently required.\*\* Changes under `src/` and `tests/` already trigger the application build/test lanes.



However, `scripts/Get-CiChangeFlags.ps1` does \*\*not\*\* generally treat changes confined to `docs/` or `reference/` as build-relevant. A fixture-only follow-up could therefore miss the application tests that consume or validate the changed expectations. Add targeted path coverage, with corresponding `Test-CiChangeFlags.ps1` tests, where necessary.



Also, the `reference-data` job is not automatically a numbering-policy test. The inspected Python suite concerns provider-domain reference packages and their immutability, not audit Case/PO generation.



\### Local verification



The repository’s canonical commands are:



```powershell

dotnet restore ./Pegasus.slnx --locked-mode

dotnet build ./Pegasus.slnx --configuration Release --no-restore

dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"

```



Run the relevant corpus tests separately, with the authorised local evidence available:



```powershell

dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj `

&#x20; --configuration Release --no-build --filter "Category=Corpus"

```



The runbook supports LocalDB on Windows and a configured SQL Server container on Linux. \*\*Excluding the SQL lane would leave the most important persistence, uniqueness, and replay risks unverified.\*\* Corpus material must remain local and immutable; missing evidence should be reported as unavailable rather than represented as a pass.



Local UI verification should additionally exercise actual standalone and linked audit creation, reload the records, generate reports, and inspect the resulting custody destinations. A healthy development server alone is not evidence that these business paths work.



\---



\## 7. Existing `ap.` references: the highest-risk part



\### New cases only



This is the lower-risk route:



\* Existing case identities remain unchanged.

\* New audits receive `a.`.

\* Legacy reads continue to use the stored reference rather than recalculating it.

\* Previously generated reports and history retain the reference they actually used.



That approach is consistent with the existing documented immutability rule and avoids turning a formatting change into historical data rewriting.



\### Renaming existing cases



A retrospective change must account for more than `Cases.Reference`.



| Surface                                     | Migration concern                                                                                           |

| ------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |

| Primary and secondary identity fields       | Different creation paths populated them differently; both must be inventoried.                              |

| Parent/child relationships and uniqueness   | Verify proposed target references before updates. Do not assume every legacy row follows the newest model.  |

| Box and local custody                       | Folder names, stored identifiers, exact-name resolution, and recovery must remain consistent.               |

| Pending and completed operations            | Distinguish work not yet allocated from work already committed under an old identity.                       |

| Reports, fee notes, EVA bundles, and hashes | Preserve historical artifacts; decide how newly generated artifacts relate to the renamed case.             |

| Search and external correspondence          | Provide an explicit old-reference mapping where needed; do not blindly equate all `ap.X` and `a.X` records. |



Box custody includes exact-name matching and duplicate-name refusal. Report filenames derive from the reference, and linked audits retain document versions pointing to the same Box file/version as the original. These make a database-only rename insufficient as a migration design.



There is also a specific retry concern: \*\*`EfCreateAuditCaseStore.Fingerprint(...)` includes `command.AuditReference`.\*\* Reconstructing an old operation using the new formatter can produce a different fingerprint. Test the real use-case replay behaviour across cutover, not just the helper in isolation.



For retrospective renumbering, I would require a dry-run mapping, collision report, backup/recovery plan, external-storage reconciliation, and an explicit legacy-reference policy before applying changes. Do not edit an already-applied migration to conceal the transition.



\---



\## Recommended implementation order



\*\*First, settle the contract:\*\* new identities versus historical renumbering; linked Audit versus original inspection; whether assessment still gates creation.



\*\*Second, update the authoritative requirements and the four creation paths together.\*\* In particular, fix the corrected-principal primary-reference inconsistency and reconcile the still-wired Engineer Finding path.



\*\*Third, verify behaviour rather than just replacing expectations:\*\* canonical stored references, assessment independence, one-audit uniqueness, custody, API/report output, and cross-cutover retries.



\*\*Finally, deploy the writers consistently and check real new allocations.\*\* External acceptance of total-loss audits under `a.`—especially EVA behaviour—is a separate verification point; the inspected Pegasus code proves which reference it sends, not how the external system will interpret it.



\*\*Bottom line:\*\* I would support the `a.`-only rule. The central code change is small, but the safe implementation should include \*\*identity-path consistency, active documentation corrections, focused local/CI regressions, and an explicit historical-reference policy\*\*. The two mistakes to avoid are changing only `AuditIdentity` and assuming the job is finished, or globally replacing `ap.` across stored identities, historical evidence, and test fixtures.



