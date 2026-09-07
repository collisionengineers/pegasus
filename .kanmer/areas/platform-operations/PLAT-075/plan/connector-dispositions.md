# Connector tool dispositions and test map

Read-only source checkpoint: Stream A `a243fd2090d2f3806e289b55f862016231bec42f`
and local combined host `e28522281847b3a83a1607166e965205b30f9b34`.
This replaces the earlier G9 source checkpoint; final acceptance remains pending.

The combined host registers all 44 tools below. Every tool resolves its caller
through `AutomationActorResolver.RequireAsync` with the stated OAuth scope. The
resolved automation subject is the authorization, grant, audit, lease and
idempotency actor; it is not replaced by the consenting human. There is still
no autonomous mail-send MCP tool.

The named test files below are source locations found by exact tool-name or
tool-discovery reference. Their presence is not execution evidence. The
PLAT-075 execution scratch records all 20 selected MCP tests passing at local
combined checkpoint `0ce9510cc` after the single-rate correction. That proof
predates both source heads above and is not final-host proof. The scratch does
not record a later successful execution of
`AutomationIntakeParityIngressTests`; its earlier Triage failure remains
historical evidence and must not be relabelled PASS. Exact final combined-head
build, test, activation and deployment evidence remain pending.

| Tool | Required scope | Current source disposition | Test-source references |
| --- | --- | --- | --- |
| `pegasus_ai_job_list` | `automation.jobs` | Registered; protected grant/filter/order cursor, default 50/max 100; final-host proof pending. | `AutomationAiJobIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_ai_job_create` | `automation.jobs` | Registered typed Core caller; bounded request and actor-scoped audit/replay path; final-host proof pending. | `AutomationAiJobIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_ai_job_take` | `automation.jobs` | Registered typed Core caller; actor/version/operation-key authority retained; final-host proof pending. | `AutomationAiJobIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_ai_job_progress` | `automation.jobs` | Registered typed Core caller; actor/version/operation-key authority retained; final-host proof pending. | `AutomationAiJobIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_ai_job_complete` | `automation.jobs` | Registered typed Core caller; produces draft-ready output only; final-host proof pending. | `AutomationAiJobIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_ai_job_complete_market_research` | `automation.jobs` | Registered typed Core caller; case edit lease is additionally required for the case mutation; final-host B parity pending. | `AutomationAiJobIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_ai_job_fail` | `automation.jobs` | Registered typed Core caller; actor/version/operation-key authority retained; final-host proof pending. | `AutomationAiJobIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_ai_job_release` | `automation.jobs` | Registered typed Core caller; actor/version/operation-key authority retained; final-host proof pending. | `AutomationAiJobIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_estimate_save` | `automation.assessment` | Registered against canonical estimate policy; requires active case edit lease and expected version; final-host B parity pending. | `AutomationAssessmentIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_estimate_list` | `automation.assessment` | Registered with the B bounded page port and protected continuation; old pending G9 host-patch note is obsolete; final-host proof pending. | `AutomationAssessmentIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_assessment_get` | `automation.assessment` | Registered typed read projection; final-host B parity pending. | `AutomationAssessmentIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_assessment_update` | `automation.assessment` | Registered restricted update; named B policy owners retain finding, signatory, valuation, estimate, rate, cost and VAT behavior; final-host proof pending. | `AutomationAssessmentIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_case_update_details` | `automation.cases` | Registered typed caller; requires active case edit lease and expected version; final-host B parity pending. | `AutomationAssessmentIngressTests.cs`; `AutomationMcpIngressTests.cs`; `InspectionAddressChoicesPersistenceTests.cs` |
| `pegasus_case_search` | `automation.cases` | Registered with B bounded query and protected keyset continuation; old pending G9 host-patch note is obsolete; final-host proof pending. | `AutomationMcpIngressTests.cs`; `AutomationConnectorAuthorizationTests.cs` |
| `pegasus_case_get` | `automation.cases` | Registered bounded case projection with independently paged documents/history; final-host aggregate paging proof pending. | `AutomationAdministrationWebTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_case_edit_begin` | `automation.cases` | Registered A01 lease caller under the automation actor; final-host proof pending. | `AutomationAssessmentIngressTests.cs`; `AutomationMcpTestSupport.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_case_edit_renew` | `automation.cases` | Registered A01 lease-renew caller; holder/token/version checks retained; final-host proof pending. | `AutomationMcpIngressTests.cs` |
| `pegasus_case_edit_end` | `automation.cases` | Registered A01 lease-release caller; holder/token checks retained; final-host proof pending. | `AutomationAssessmentIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_document_add` | `automation.documents` | Registered A04 custody caller; 10 MiB pre-decode limit and case lease/version checks retained; final-host proof pending. | `AutomationDocumentIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_document_download` | `automation.documents` | Registered metadata-first exact-version read with authenticated ETag/range endpoint; final-host proof pending. | `AutomationDocumentStreamingTests.cs`; `AutomationDocumentIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_document_export` | `automation.documents` | Registered; maximum 32 exact selections, bounded inline result or five-minute grant-bound sequential ZIP endpoint, no ranges; final-host proof pending. | `AutomationDocumentStreamingTests.cs`; `AutomationDocumentIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_intake_queue_list` | `automation.intake` | Registered with current C bounded page port and protected continuation; stale C08-pending note is obsolete; final-host proof pending. | `AutomationConnectorAuthorizationTests.cs`; `AutomationMcpIngressTests.cs`; `QdosAllocationRecoveryTests.cs` |
| `pegasus_intake_submit` | `automation.intake` | Registered bounded canonical intake command under the automation actor; final C caller/provenance proof pending. | `AutomationMcpIngressTests.cs` |
| `pegasus_mail_list` | `automation.mail` | Registered; protected grant/mailbox/filter/order cursor, default 50/max 100; final-host proof pending. | `AutomationMailIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_mail_get` | `automation.mail` | Registered retained-mail projection under the automation actor and mailbox grant; final-host proof pending. | `AutomationMailIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_mail_correct_classification` | `automation.mail` | Registered typed correction caller using current classification vocabulary; C04 behavior and final-host proof pending. | `AutomationMailIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_triage_list` | `automation.intake` | Registered with C typed actor and bounded page port; stale down-conversion/global-sequence pending note is obsolete in source, but no later exact parity PASS is recorded. | `AutomationIntakeParityIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_triage_get` | `automation.intake` | Registered typed Triage detail caller; final-host and later parity proof pending. | `AutomationMcpIngressTests.cs` |
| `pegasus_triage_source_download` | `automation.intake` | Registered metadata-first retained-source caller; final-host and later parity proof pending. | `AutomationIntakeParityIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_triage_await_information` | `automation.intake` | Registered shared typed mutation; actor, expected version and operation key pass to Core; final-host proof pending. | `AutomationMcpIngressTests.cs` |
| `pegasus_triage_record_finding` | `automation.intake` | Registered shared typed mutation; actor, expected version and operation key pass to Core; final-host proof pending. | `AutomationMcpIngressTests.cs` |
| `pegasus_triage_supersede_finding` | `automation.intake` | Registered shared typed mutation; actor, expected version and operation key pass to Core; final-host proof pending. | `AutomationMcpIngressTests.cs` |
| `pegasus_triage_response_link` | `automation.intake` | Registered shared typed mutation; actor, expected version and operation key pass to Core; final-host proof pending. | `AutomationMcpIngressTests.cs` |
| `pegasus_triage_response_unlink` | `automation.intake` | Registered shared typed mutation; actor, expected version and operation key pass to Core; final-host proof pending. | `AutomationMcpIngressTests.cs` |
| `pegasus_triage_complete` | `automation.intake` | Registered shared typed mutation; actor, expected version and operation key pass to Core; final-host proof pending. | `AutomationMcpIngressTests.cs` |
| `pegasus_triage_cancel` | `automation.intake` | Registered shared typed mutation; actor, expected version and operation key pass to Core; final-host and later parity proof pending. | `AutomationIntakeParityIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_triage_reopen` | `automation.intake` | Registered shared typed mutation; actor, expected version and operation key pass to Core; final-host proof pending. | `AutomationMcpIngressTests.cs` |
| `pegasus_triage_case_link` | `automation.intake` | Registered typed actor-aware case-link mutation; expected version and operation key pass to Core; final-host proof pending. | `AutomationMcpIngressTests.cs` |
| `pegasus_triage_case_unlink` | `automation.intake` | Registered typed actor-aware case-unlink mutation; expected version and operation key pass to Core; final-host proof pending. | `AutomationMcpIngressTests.cs` |
| `pegasus_unidentified_list` | `automation.intake` | Registered with current C typed projection and protected bounded continuation; stale C01/C07-pending note is obsolete; final-host proof pending. | `AutomationIntakeParityIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_unidentified_get` | `automation.intake` | Registered current-source/version typed detail projection; final-host proof pending. | `AutomationIntakeParityIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_unidentified_source_download` | `automation.intake` | Registered metadata-first exact retained-source caller; final-host proof pending. | `AutomationIntakeParityIngressTests.cs`; `AutomationMcpIngressTests.cs` |
| `pegasus_unidentified_resolve` | `automation.intake` | Registered typed mutation with actor/version/operation-key authority; final-host proof pending. | `AutomationMcpIngressTests.cs` |
| `pegasus_estimate_import` | `automation.assessment` | Registered canonical raw-import caller with B parser and current estimate projection; old registration-pending note is obsolete; final-host proof pending. | `AutomationAssessmentIngressTests.cs`; `AutomationMcpIngressTests.cs` |

## Evidence boundary

- Source registration and scope were inspected at the two heads named above.
- Test-source references establish coverage locations only.
- The recorded 20-test MCP PASS belongs to combined `0ce9510cc`, not the
  current combined head.
- The recorded Triage parity failure is not erased by later source changes;
  the execution scratch contains no matching later PASS for that test class.
- No live provider call, enabled final-host activation, deployment, or current
  exact-head full rail is claimed here.
