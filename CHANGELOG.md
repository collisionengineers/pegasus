# Changelog

Factual, commit-by-commit history of `origin/main`, oldest first. Each entry is written after directly examining that individual commit and its changed content.

## 1. e65eb2e14dd4fd2d842f8baaa977e0240c5fb5ca — 2026-07-23 — establish CollisionSpike v2 baseline

This root commit created the initial CollisionSpike v2 working baseline. It brought in the project-discovery record, operational-process and architecture notes, the first two architecture decisions, the EVA API reference, an Infisical configuration placeholder, an Obsidian workspace, and a Microsoft Learn MCP plugin. It also installed a large local catalogue of agent skills and their reference material, chiefly for Azure planning, deployment, diagnostics, cost, compliance, AI, and Microsoft Foundry work. The discovery material describes a QDOS-first case-management product with internally managed staff accounts, case/PO reference rules, document and email intake, Box custody, OCR/extraction, and a manual EVA transfer path. This predates both `NOW.md` and `docs/capabilities.md`; neither file was changed or related capability recorded in this commit.

## 2. 27c5467f90bed297331cfc83c025225018d33a1e — 2026-07-23 — establish v2 repository foundation

This commit created the runnable .NET 10 application foundation: separate Core, Infrastructure, Razor Pages Web, and isolated Functions Worker projects; a solution file; a simple operational landing page; live and readiness health endpoints; and unit, integration, and architecture-test projects. The architecture tests enforce that business code has no host or infrastructure dependencies and that project references point in the intended direction. It also added Windows-oriented local checks, a repository-check CI workflow, agent guidance and delivery skills, initial UI concepts, and Bicep/`azd` deployment scaffolding for separate development and production resource groups with web, worker, SQL, storage, Key Vault, monitoring, managed identities, and an optional Document Intelligence resource. The scaffold explicitly does not claim a real intake workflow, trigger, deployment, or completed business feature. This predates both `NOW.md` and `docs/capabilities.md`; neither file was changed or related capability recorded in this commit.

## 3. 9159f8bef0fd15b98e016fc04889d28ea7cb9cd4 — 2026-07-23 — add local QDOS intake vertical slice

This commit added the first end-to-end local QDOS intake path. Staff could upload an `.eml` or PDF of up to 10 MB, explicitly confirm their authority to create a case, and review a persisted receipt. The application reads email/PDF content, identifies QDOS only from sufficiently strong instruction content, captures source and field evidence, extracts key instruction fields, defaults a missing instruction date from the receipt clock, and routes unsupported, technical-failure, insufficient-text, and uncertain inputs to distinct outcomes rather than guessing. Confirmed and authorised inputs allocate a QDOS reference; duplicate source bytes return the original receipt and do not consume a new number. It added database storage, migrations, queue/review pages, dependency wiring, and genuine-corpus integration tests covering forwarded email, OCR-required PDFs, duplicate uploads, unauthorised submissions, and concurrent reference allocation. ADR-0003 records PdfPig as the narrow embedded-text adapter after a corpus benchmark, while keeping scanned content as an explicit OCR-required outcome and prohibiting this development upload path from deployment. Neither `NOW.md` nor `docs/capabilities.md` existed in this revision, so neither was changed or related capability recorded.

## 4. 95646fd139efd97a1364dbca399551afe003ce5d — 2026-07-23 — record remaining requirements and handoff

This documentation-only commit described the limits of the new local QDOS proof and the work needed before a usable QDOS release. It added a handoff explaining the real Web-to-Core-to-Infrastructure caller path, how the development-only route is enabled, what genuine-corpus tests prove, and what remains deliberately unimplemented: authenticated staff operation, original-source custody, typed operator-confirmed data, mailbox automation, Box, vehicle-data integration, lifecycle work, and production evidence. It also made a detailed delivery-gap plan, updated the architecture index after selecting PdfPig, and changed the predecessor strategy to start v2 with fresh application data rather than migrate pre-release case, queue, or audit state. The Azure retirement guidance still requires exact-target approval and a separate decision for potentially shared or data-bearing assets. Neither `NOW.md` nor `docs/capabilities.md` existed in this revision, so neither was changed or related capability recorded.

## 5. 682e47be65b53f7a79de905f65b53a03c28edecf — 2026-07-23 — make corpus guard work in clean checkouts

This commit corrected the repository-structure check so it proves that the local `corpus/` boundary is ignored even on CI, where no corpus directory exists. Instead of asking Git about the absent directory itself, the check asks whether a deliberately nonexistent child path would be ignored. The guard still fails if corpus material is tracked or if the ignore rule is missing. This commit does not change `NOW.md` or `docs/capabilities.md` and does not relate to a recorded capability.

## 6. 9c39545cc0b69252f7cd116f2e33bdda36896732 — 2026-07-23 — refresh repository skills

This commit replaced and refreshed the repository’s local assistant tooling. It removed the checked-in Box skill and its installer record in favour of the Box plugin, enabled Microsoft Learn MCP plus guarded Box, GitHub, and Outlook app integrations, and updated the researcher guidance accordingly. It added reference packages for cloud solution architecture, delivery planning, frontend design review, Kusto queries, and building/evaluating MCP servers, including example tooling for MCP transport connections and question-and-answer evaluation. The skills lock file was updated to record the new sources and content hashes. No application behaviour, `NOW.md`, or `docs/capabilities.md` was changed.

## 7. 13df6cc6891decac1407ed3b866aa93bec23691b — 2026-07-23 — add guarded operator reference archive

This commit imported a large, explicitly protected archive of operator-provided reference material. The archive contains snapshot contracts, schemas and approved deltas; historical architecture decisions; product, operational, design, governance, review, and ticket records; repository inventories and reconciliations; and supporting fixtures/manifests. The repository instructions were extended to state that this tree is source evidence, not an editable implementation area, and the archive’s own instructions reinforce that boundary. The material provides context for later requirements and reconstruction work but does not itself make those archived designs or tickets live product behaviour. No application code, `NOW.md`, or `docs/capabilities.md` was changed.

## 8. 6779f0345d7b8991bab19562b41ecc1b9e23f591 — 2026-07-23 — deliver reviewable QDOS intake draft slice

This commit substantially reworked the QDOS proof from immediate case creation into a safer, reviewable intake draft. It added durable local artifact custody, source-channel identity and external receipt tokens, multi-format reading of email, PDF, DOC/DOCX and image material, per-asset outcomes and evidence, typed draft fields, and a review page that exposes the persisted result. The old automatic QDOS case/reference allocation was removed from this path: material is received and assessed first, leaving case acceptance for a separately authorised, reviewable step. The implementation also introduced database-migration adoption for local development, a database-aware readiness check, broader negative and persistence tests, and genuine-corpus coverage for the expanded formats. Documentation defined a remainder-delivery plan and new decisions for provider/MCP authentication and multi-format assets, clarified intake vocabulary and lifecycle rules, and updated the discovery requirements. Neither `NOW.md` nor `docs/capabilities.md` existed in this revision, so neither was changed or related capability recorded.

## 9. 59721a86bc82c476e53523cf198466fcd567b0a3 — 2026-07-24 — resolve remainder delivery review findings

This commit tightened the new intake-draft work after review. It clarified that authorised definitive instructions should ultimately create incomplete cases automatically, while non-definitive or identity-blocked material stays visible in `Blocked intake` until staff resolve it. It explicitly deferred vehicle-registration OCR from ordinary images, retained scan-like PDF OCR as first-MVP scope, and recorded that automatic mailbox categorisation needs one Core-owned policy rather than separate transport rules. The reader was strengthened to process whole PDFs within shared text, image, size and time budgets; when a limit is exceeded the source is retained but treated as incomplete, never accepted from a partial extraction. Plans also clarified Web versus Worker staging and outbox responsibilities, and tests added adversarial DOCX/PDF, artifact, validation, and migration cases. Neither `NOW.md` nor `docs/capabilities.md` existed in this revision, so neither was changed or related capability recorded.

## 10. 5e0aa7ad50281ba5942c03b5a59765bd245de7dd — 2026-07-24 — retain in-house extraction option

This documentation change kept open the possibility of replacing PdfPig with an in-house document extractor or another external engine later. It requires any replacement to use the existing engine-neutral Infrastructure contract, demonstrate contract parity and frozen-cohort/holdout evidence, meet security and licensing/maintenance checks, and have a real caller. It specifically rules out introducing a parallel reader, external checkout dependency, or dormant feature flag in advance. No application behaviour, `NOW.md`, or `docs/capabilities.md` was changed.

## 11. 6e8345b37d0023fc88688eff5da17a43589f1375 — 2026-07-24 — merge pull request #1: deliver reviewable QDOS intake draft slice

This merge integrated the completed reviewable QDOS draft work and its follow-up corrections into the main line. Relative to its first parent, it brought in the multi-format, artifact-backed intake draft path; typed review data; safety budgets for document processing; database readiness and development migration handling; extensive unit and integration coverage; and the delivery/architecture documentation that defines its limits and future sequencing. It also included the guarded option for a later in-house or external extraction-engine replacement. The merge does not independently introduce a new capability beyond those branch changes, and it does not change `NOW.md` or `docs/capabilities.md`.

## 12. fac89e8132efae10473af7e209e965e8eee6cdca — 2026-07-24 — record post-PR1 documentation review findings

This commit added a review record rather than changing runtime behaviour. It listed fifteen confirmed unresolved issues in the QDOS intake draft, including unbounded HTML stripping, attachment-byte/provenance handling, cancellation gaps, artifact-storage retry behaviour, untrusted-length values reaching SQL limits, document-expansion and image-memory limits, unsupported attachment retention, canonical conflict detection, nested-email depth, queue visibility and paging, receipt-token canonicalisation, and a weak genuine-DOCX assertion. It also recorded an unresolved meaning of “image occurrence” in the asset ADR. No `NOW.md` or `docs/capabilities.md` change occurred.

## 13. 515c5fe5f533c1e0800a2c9b297ec6cd685994bc — 2026-07-24 — merge main into the review branch

This merge synchronised the review branch with main. Its first-parent delta contains the reviewable QDOS intake draft implementation that had already been described in the earlier merged pull request: multi-format source handling, durable local artifacts, typed drafts, safer processing limits, readiness checks, migrations, tests, and the delivery-plan documentation. It adds no distinct product decision beyond integrating those existing branch changes. `NOW.md` and `docs/capabilities.md` were not changed.

## 14. 4cd1de317bd7fc17766a0d24000f3d6d162a74d0 — 2026-07-24 — consolidate legacy documentation

This large documentation reorganisation removed the previously imported `docs/reference/operator-provided` archive from its protected reference location and consolidated legacy material elsewhere in the repository. The moved material includes historical contracts, ADRs, architecture and operations notes, reviews, ticket/evidence records, inventories, screenshots, and EVA examples; an EVA JSON format example was added alongside that reorganised material. It is a repository-information move and cleanup, not a runtime change or a claim that the former documents became current requirements. `NOW.md` and `docs/capabilities.md` were not changed.

## 15. adb401e3856909a11fc2b3ba42d7cf1407235bdd — 2026-07-24 — remove skills and add future mailbox-classification reference

This commit removed development-wrapper guidance from the Collision Engineers design skill and removed the now superseded QDOS vertical-slice skill. It added a short reference tree describing future received, sent, and reply email categories, such as new work, ongoing-case correspondence, post-report queries, billing, and image requests, with a note to use the Outlook connector to inspect email. This is a future categorisation reference only; it neither implements mailbox processing nor settles the automatic categorisation policy. `NOW.md` and `docs/capabilities.md` were not changed.

## 16. 391cb9ffe5ce5bf256d68a9941d878f99a8ec895 — 2026-07-24 — make intake provider-neutral

This commit replaced the QDOS-named intake implementation with a general intake pipeline containing one deliberately bounded QDOS extraction policy. The upload route became `/Intake/Upload`; processing, source reading, artifact storage, receipt persistence, review/queue pages, test fixtures, and database tables were renamed and reshaped around neutral intake concepts. QDOS is now suggested only when positive readable content supports it, not from a sender or filename and never as the fallback for ambiguous material. Stable persisted codes and versioned JSON envelopes replaced direct enum-name storage, while a fresh provider-neutral initial migration and a SQLite baseline guard replaced the earlier adoption path. Documentation and ADR-0006 describe the boundary: it is not yet a second-provider implementation, mailbox categorisation, case creation, or deployment. `NOW.md` and `docs/capabilities.md` were not changed.

## 17. bbf32b3384385153bc42bbea4706e45d0f048ff6 — 2026-07-24 — add long-term local testing plan

This commit added a planning-only package for a reproducible Windows-native testing environment and honest evidence classification. It defines future local profiles for SQL Server LocalDB, Azurite/Functions, browsers, Graph simulation, observability, performance, security and approved live integration; it also specifies isolated per-run resources, safe cleanup, caller-backed tests, and the distinction between local verification, deployment, live verification and acceptance. The plan deliberately withholds implementation until each profile has a real product caller and rejects adding speculative services, containers or cloud operations. No product code, `NOW.md`, or `docs/capabilities.md` changed.

## 18. 0f3057449038aea87e8e60bcf24281d4901c72fe — 2026-07-24 — add deferred-capability architecture plan

This commit added an unapproved documentation plan for reconciling future/deferred capability considerations with the repository’s authority hierarchy. It distinguishes required-now unresolved decisions, named future considerations, rejected legacy concepts, potential vendor options, first-MVP exclusions, and technical alternatives that belong to specific ADRs. It records how later capability work must preserve Core ownership, identities, provenance, Box custody boundaries, explicit activation evidence and future migrations—without creating dormant code, configuration, infrastructure or a second roadmap. The plan’s stated output is separately approvable proposals, not an amendment to product requirements or accepted ADRs. No product code, `NOW.md`, or `docs/capabilities.md` changed.

## 19. df999cb810b8978f007da8f8ffbe181431f8df01 — 2026-07-24 — refine future email-tree terminology

This small documentation change renamed the future mailbox category from `new-work-received` to `new-instruction-received` and changed one example from website work to website enquiry. It only adjusts the future reference tree; it does not implement categorisation or alter product behaviour. `NOW.md` and `docs/capabilities.md` were not changed.

## 20. 50e4d5a9b074c2643dbfcfba75604e2d4ef76a15 — 2026-07-24 — add repository-plugin planning material

This commit added planning material for a repository-analysis plugin. It includes an Azure/Microsoft Learn tool catalogue, a general implementation-planning prompt, and a “grand architecture overview” skill with templates, schemas, workflow guidance and two read-only Node utilities. Those utilities discover likely projects in a directory and build a cross-project seam index from supplied profiles so later analysis can focus on shared entities, integrations, contracts and producer/consumer relationships. This is tooling/planning content only; it does not change Pegasus application behaviour, `NOW.md`, or `docs/capabilities.md`.

## 21. d68bed7501e3a30abd1f32835c0de96ba90801e5 — 2026-07-25 — add focused repository plugins and reorganise plans

This large commit established a local plugin marketplace and added focused plugins for planning, implementation, review, debugging, documentation, validation, task contracts, and UI work. It added ADRs defining the boundaries for repository-local Codex planning plugins and focused workflow plugins, plus supporting PowerShell task-artifact operations and validation. It also reorganised the UI material under `docs/plans/ui-ux`, added mailbox categorisation and Triage planning, revised many delivery and reference documents, and updated repository checks/skill validation to recognise the new plugin structure. In the application persistence model, receipt-local historical records were renamed from `IntakeAuditEvents` to `IntakeReceiptEvents` to avoid presenting pre-case receipt history as the permanent case audit trail; migrations and tests were updated consistently. `NOW.md` and `docs/capabilities.md` were not changed.

## 22. 1fcb3edcc0c21a39e5212f3188a97d7d395ec890 — 2026-07-26 — complete the CollisionSpike v3 planning suite

This documentation-heavy commit reorganised the repository around a versioned feature-planning suite. It added a 213-item feature worksheet and maturity map, a delivery roadmap, later-delivery plans, plan-index rules, explicit permanent/conditional boundaries, richer UI traceability/specification material, and new documentation area indexes. It introduced ADR-0009 for a direct authorised-terminal Azure release route, while explicitly recording that the committed deployment scaffold is not yet executable. It also added a comprehensive PowerShell documentation checker that validates links, feature-map parity, plan statuses/indexes, instruction discovery, required routes, and negative fixtures. The changes clarify planning ownership and evidence states; they do not deploy or implement the planned features. `NOW.md` and `docs/capabilities.md` were not changed.

## 23. 19a5231e6e899369683cad6da498f731894cf9eb — 2026-07-26 — plugin/workflow removal

This commit removed the repository-local Codex workflow system: its agent profiles, hooks, installed Azure and Microsoft documentation skill material, plugin marketplace, repository-plugin packages, and associated planning/review/task-contract utilities. It also removed the standalone Microsoft Docs plugin. Separately, it added a private Node development dependency on Azurite, with its lockfile, and configured Obsidian to display unsupported files. It does not change Pegasus application code or its product documentation; `NOW.md` and `docs/capabilities.md` were not changed.

## 24. 8c3919c81bf4117cbd8f4e4aa2e85ac29ce1f8ce — 2026-07-27 — Update config.toml

This commit removed the repository-local configuration that enabled the Azure MCP server and the Microsoft Learn MCP endpoint. The remaining configuration keeps third-party apps disabled, including destructive operations. It affects only local Codex tooling configuration; `NOW.md` and `docs/capabilities.md` were not changed.

## 25. 9af3733c05eb1cf8ef86b102d0437f299879597d — 2026-07-27 — design

This large onboarding commit introduced a governed product, engineering and design documentation structure. It added an architecture and operations baseline; issue forms and a pull-request template; a repository-wide design-system map; and a supplied Collision Engineers design reference package containing brand assets, fonts, previews and reusable website/document examples. It adopted the Azure Workflow documentation and GitHub-work standard while retaining the existing product authority and explicitly making no Azure change. It also created `docs/product/capabilities.md`, a 213-item durable capability inventory with owners, horizons and activation boundaries, and revised the feature map to point to it. `NOW.md` was not changed; the requested `docs/capabilities.md` path was not changed, but this commit added the successor-style inventory at `docs/product/capabilities.md`.

## 26. 92075658abc5536ba39c48490128527fafc7e49a — 2026-07-27 — chore: complete Azure Workflow onboarding

This commit completed that workflow onboarding by removing the large imported design-source bundle and retaining only the exact master logo plus a concise application-focused design authority. It made CI select documentation-only validation when a change contains only Markdown or issue-form files, otherwise retaining the full check. It replaced obsolete plugin/skill validation with a new validator for the documentation spine, capability inventory, issue forms, pull-request template, change records and decisions. The onboarding record was filled in with scope, evidence, exclusions and recovery details; it records no application, data, corpus, infrastructure or Azure behaviour change. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 27. 4ac1cf29004ba382049d6bf3c732ecaf003996f8 — 2026-07-27 — docs: consolidate operator authority

This commit reorganised the authoritative operator notes without intending to change their business meaning. It grouped the former fragments into business-process, product-requirements, and systems-and-integrations sections; added an index that maps each old location to its new canonical file; and removed the superseded folders and one empty note. The extracted material covers case lifecycle, intake/work instructions, case types/references, inspection addresses, reserved terms, engineering constraints and current external systems. The documentation validator was extended to enforce the consolidated-file count, provenance map, retained capability list and selected authoritative statements. It also clarified elsewhere that operator notes are authoritative but may be maintained structurally under standing user direction. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 28. 4bbe176994614f12c46cd591c49ad0d79e65d17a — 2026-07-27 — ci: refresh GitHub action runtimes

This commit updated the CI workflow to use the then-current major versions of the checkout and .NET setup actions, replacing their v4 pins after CI reported Node 20 deprecation warnings. The verification gate and its behaviour remained unchanged. The Azure Workflow onboarding record was updated with the action-version rationale and the earlier CI outcomes. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 29. c2c67aca43a446323b1ae5295527ef34df46c56c — 2026-07-27 — docs: convert plan tree to canonical owners

This commit replaced the active `docs/plans` tree with clearer canonical owners. Historical plans were preserved under `docs/history/plans` as evidence only; active product decisions, V1 gap and boundaries moved under `docs/product`; UI requirements and references moved to `design`; and local testing guidance moved to runbooks. It created five product-area pages to own the 213 capability outcomes, and updated every capability row to link to one of those areas or the boundary page instead of detailed planning files. Documentation tests now enforce the conversion, the archive size and the complete destination mapping. `NOW.md` and the requested `docs/capabilities.md` path were not changed; `docs/product/capabilities.md` was materially updated to use the new canonical area owners.

## 30. 5a3eae12644776926c746d9e5df9b699e6596000 — 2026-07-27 — docs: record onboarding verification outcome

This documentation-only commit marked the Azure Workflow onboarding record complete. It recorded that the published plan-conversion head passed the full CI gate, that an independent review of the 163 changed paths found no required findings, and that the draft pull request contained the completed onboarding work. It retained a final exact-head confirmation as a follow-up because this outcome-record commit came after the reviewed head. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 31. b6683f757938913cbd6aa1aeb0443791af41f31f — 2026-07-27 — docs: plan provider-aware QDOS alpha

This commit created the high-risk plan for the first live QDOS alpha at `0.1.0-alpha.1`. It separates a provider’s identity from direct-provider and intermediary email routes, requires evidence-backed versioned policies for each route, and keeps non-QDOS routes from creating cases until separately activated. The plan covers normalising the supplied provider and inspection-location reference data, proving forwarded-email senders, completing the QDOS case workflow, and later adding the approved Operations-first staff shell and the single `instructions@` Worker caller. It added Decision 0011 to make direct and intermediary policies independent, clarified the operator intake channels, and changed the `Now` capability rows to the alpha target while assigning their detailed scope to this release plan. `NOW.md` was not changed; `docs/product/capabilities.md` was materially updated.

## 32. ce0135ede23101af320846a135d97c1ee05c7146 — 2026-07-27 — docs: link QDOS alpha plan PR

This documentation-only commit linked the QDOS alpha planning record to newly created draft pull request #4. It updated the review state from not created to pending against that draft and listed the draft PR alongside its issue, Project fields and alpha milestone. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 33. 9a8ffe7cb992c024bb2ba1655368a2fdbe3db6fb — 2026-07-27 — docs: align selected UI authority

This documentation-only commit corrected the product index to describe the V1 operator-experience authority as selected rather than direction-neutral. It also recorded the review finding that prompted the correction and the corresponding remediation round in the QDOS alpha plan. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 34. adbd6fe092ab389ad7efd275313a11f83b568ead — 2026-07-27 — docs: correct plan publication state

This documentation-only commit corrected the QDOS alpha plan’s publication and review status. It recorded that the plan was already published as draft PR #4, that the relevant documentation CI runs had passed, and that a second review confirmed the UI-authority fix but found the inaccurate pending-publication wording. It clarified that the plan record cannot certify its own later GitHub CI/review results, and left implementation stopped pending the required evidence and approvals. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 35. dcfbb6c1f7103d83c192404f746514d0eef54150 — 2026-07-27 — Merge pull request #2: Adopt Azure Workflow repository standard

This merge commit brought pull request #2 into `main`. Relative to its first parent, it combines the Azure Workflow onboarding work already described in commits 24–29: removal of obsolete local Codex integrations, creation of issue/PR workflow files and CI checks, a consolidated authority/documentation structure, the retained application design authority, a 213-item product capability inventory, and archival of the former plans tree. It does not introduce a separate new product implementation beyond that merged work. `NOW.md` and the requested `docs/capabilities.md` path were not changed; the merged work includes `docs/product/capabilities.md`.

## 36. 2f8fcafcd0756c23cf0afba04411e85563f5e051 — 2026-07-27 — chore: revalidate plan against main

This is an empty commit: it changed no files. Its message indicates an administrative revalidation of the plan against `main`, but there is no repository content change to describe. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 37. b0d7a99f2198bbf1801872085e26b26f22ac64a0 — 2026-07-27 — docs: record disproportionate review loop

This commit added an append-only agent-incident record for an unnecessary second full review of PR #4. The record explains that only external PR and issue wording had changed, while the base, head, tracked diff and successful CI had not; it prescribes targeted readback for that kind of metadata-only correction instead of automatically repeating a complete review. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 38. b2f40a2b68b5b1a906ff2e736fa43653006dba61 — 2026-07-27 — Merge pull request #4: Plan provider-aware QDOS alpha

This merge commit brought the QDOS alpha planning pull request into `main`. Relative to its first parent, it combines the provider-aware QDOS alpha plan, Decision 0011, the selected Operations-first UI alignment, the `0.1.0-alpha.1` capability/roadmap allocation, and the documentation corrections and review-process record described in commits 31–37. It is planning and governance work, not the implementation of the QDOS workflow. `NOW.md` was not changed; the merge includes material changes to `docs/product/capabilities.md`.

## 39. d0965e1264dadc8d9942ac54fd68a4b45fd06f28 — 2026-07-27 — chore: checkpoint QDOS alpha delivery work

This large in-progress checkpoint began the QDOS alpha delivery work but explicitly left provider expansion and inspection-address seeding unfinished. It introduced a versioned, hash-validated provider-domain reference package generated from a checked-in workbook, Core validation and lookup rules, EF tables/migration, and a catalogue that returns found, unknown or ambiguous provider candidates from an email-domain suffix. The initial snapshot contains eleven providers and their observed domain suffixes. It also made the local Web profile explicit (`DevelopmentOffline`), moved database migration behind a `--migrate-development` command, added related unit/integration tests and reference-data authoring tools. Alongside that work it replaced the proportional documentation CI with restore/build/non-corpus tests, removed the previous repository-check scripts, removed local Codex configuration, added tool-neutral workflow guidance and an implementation plan for a wider offline-first alpha. `docs/product/capabilities.md` was updated; `NOW.md` was not changed.

## 40. 0681ff402c6b34025efb218b5ad95452f1dee963 — 2026-07-28 — docs: establish Pegasus orientation governance

This governance commit adopted Pegasus as the repository and product identity in the root instructions, while preserving historical naming where it remains evidence. It added a detailed orientation record that crosswalks the supplied Pegasus system plan into durable product owners and capability IDs, records EVA as the current manual Engineer handoff, and distinguishes intended, implemented, deployed and accepted states. Decision 0013 created a `workspaces/` boundary for separately buildable document-extraction, report-rendering and AI-centre source imports without allowing them into the Pegasus solution or runtime. The commit also added binary-asset handling, workspace-specific ignore rules and safety instructions. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 41. f69ea31dfdf0a59b8a2c176da90ae22a538fbc9c — 2026-07-28 — refactor: cut over runtime identity to Pegasus

This commit renamed the four application projects, three test projects, solution file, namespaces, database context and configuration keys from CollisionSpike to Pegasus, while preserving the existing modular-monolith structure and intake behaviour. It updated Azure deployment metadata, Bicep names, database and connection-string names to Pegasus terminology, without performing a deployment. Architecture tests were expanded to verify the renamed solution’s dependency direction and to keep `workspaces/` out of application projects. It also removed a generated Python bytecode file. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 42. 1897267a830fa716c688cfd0af7f77860b8a50e0 — 2026-07-28 — feat: import document extraction workspace

This commit imported `workspaces/document-extraction`, an independently buildable .NET 10 source workspace for deterministic, headless extraction of PDF, DOC, DOCX, MSG and EML files. It contains managed parsers, a command-line tool, format-specific models, resource limits, security and regression tests, source/compatibility documentation, and locked dependencies. Its public extraction outcome is limited to ordered text and discrete images with provenance and failure evidence; it does not render, OCR, edit or emit arbitrary attachments. CI gained a separate locked restore/build/test job for this workspace, while the Pegasus solution remains unreferenced by it. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 43. f5b0049da5fb5d8c734d0a21bb776328e60017eb — 2026-07-28 — feat: import secure report renderer workspace

This commit imported `workspaces/report-renderer`, an independently buildable report-rendering source workspace. It includes a core renderer with template catalogues, validation, brand assets and HTML/PDF composition; API, CLI, desktop GUI and MCP entry points; document templates for expert reports, fee notes and valuation evidence; a headless Chromium/Playwright PDF path; and extensive unit and contract tests. The workspace’s own documentation records design, security, template and deployment decisions, but it remains a source workspace rather than a Pegasus runtime caller. CI gained a separate report-renderer build/test job. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 44. b53447d036cabed7d2ef7671f0c1c3416a7e9574 — 2026-07-28 — feat: import hardened AI Centre workspace

This commit imported `workspaces/ai-centre`, a non-caller AI research and experimentation workspace. It includes data/training/evaluation strategy material and an implemented TypeScript “Collision Brain” retrieval service with HTTP, stdio and MCP interfaces, pluggable authentication, object-storage, repository and embedding adapters, a PostgreSQL/pgvector option, upload staging and a document lifecycle. Its MCP tools retrieve cited passages, queue text or staged-file ingestion, list document metadata and remove a document only with explicit confirmation. The workspace documentation says Pegasus Core remains the authority for case policy and that the desktop, connectors and model pipelines are not implemented Pegasus features. CI gained a Node 24 install/typecheck/build/test job for Collision Brain. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 45. 362e08541f1643317addfeffaae2d8a364c14949 — 2026-07-28 — feat: import Agent Skills and workspace manifests

This commit added a large set of Collision Engineers skill packages under the AI Centre workspace, together with their reference material, validation scripts, test fixtures and brand assets. The packages cover house style, design, vehicle assessment, total-loss and salvage categorisation, diminution work, roadworthiness, manufacturer methods and vehicle history. It updated the workspace manifests and repository README to describe their provenance and added CI validation for the skills. These are source/workspace guidance assets, not Pegasus application policies or callers. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 46. df62e9260c267e2eeb39102ddbbd588d239c6f22 — 2026-07-28 — docs: preserve Pegasus history and EVA evidence

This documentation-only commit reorganised historical planning and discovery material under `docs/history`, while making the current QDOS alpha terminology and active documentation owners clearer. It preserved a 213-feature allocation worksheet as historical evidence, updated archived plans to use the renamed release horizons, and moved the original discovery questionnaire out of the repository root. It also consolidated EVA reference material: it added an example case-data JSON file and Engineer-workflow screenshot observations, and recorded that structured JSON plus stored images is the current manual EVA handoff while direct API use remains a future, unproven option. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 47. 63204db75fd3a224e4ab6f2f478386e81c1f83b6 — 2026-07-28 — docs: normalize imported reference terminology

This commit imported a substantial historical reference collection under `docs/reference/imp-docs/requirementsdocs`, including original emails, instruction documents, reports, spreadsheets, images, handover material, work notes and related exported assets. Alongside those originals, it added extracted text companions, a source manifest, parser fixture ledgers, provider coverage information and a regression report describing which document readers and fields had been found in the imported instruction set. The added index explicitly treats these materials as evidence and generated context rather than active product scope. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 48. c95bb712f2a87aee0600a4100970902375129b51 — 2026-07-28 — docs: orient canonical Pegasus documentation

This documentation change established `docs/product` as the living product authority and moved the questionnaire and feature worksheet into historical-evidence roles. It renamed the current release-gap document to `qdos-alpha-gap.md`, updated architecture, operations, runbooks, design references and source-of-truth guidance for the Pegasus name and independent source workspaces, and expanded the roadmap around QDOS intake, request-scoped uploads, pairing, Engineer workbench, reports, correspondence and AI proposals. It substantially revised `docs/product/capabilities.md`: the inventory rose from 213 to 229 stable capability IDs, with refreshed ownership, release horizons and explicit boundaries. `NOW.md` was not changed.

## 49. 0d38551e46908c48452735ea553618f140a642b1 — 2026-07-28 — ci: enforce Pegasus repository integration

This commit added a CI policy check to keep the repository aligned with the Pegasus documentation orientation. The new PowerShell validation verifies the 229 capability rows, IDs and horizon totals against the design traceability matrix; rejects obsolete project paths and inappropriate old naming; and checks that imported workspaces remain source-only, without application references or generated/private material. CI runs this validation before the existing .NET and workspace checks. It also moved the repository-orientation change record to review status. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 50. a550c237d6dec76fd8b0ce15c440a72644bcb844 — 2026-07-28 — docs: link Pegasus orientation PR series

This small documentation change linked the Pegasus repository-orientation change record to pull request 17 and added a chronological table of the ten pull requests that delivered the orientation programme. The table covers governance, renaming, the three workspace imports, skill import, historical and reference preservation, documentation orientation and CI policy enforcement. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 51. 4351ec0eeaa9febd32b96812143071ab72b7b8e8 — 2026-07-28 — refactor: cut over runtime identity to Pegasus (#9)

This commit renamed the working .NET application from CollisionSpike to Pegasus throughout the solution, projects, namespaces, database context, web pages, worker, tests, local commands, CI and Azure deployment configuration. It replaced `CollisionSpike.slnx` with `Pegasus.slnx`, updated the Azure resource naming and application metadata, and removed an accidentally tracked Python bytecode file. The functional local intake route, persistence and architecture boundaries were retained, while architecture tests were strengthened to verify the renamed project dependency direction and that source workspaces stay outside the application solution. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 52. ba29cb043a26459a5aeeec904f426e4a92b8bb11 — 2026-07-28 — ci: separate workspace validation from runtime diff

This CI-only commit split the document-extraction workspace validation into its own `source-workspaces` job, separate from the Pegasus application validation job. In doing so, it removed the report-renderer, AI Centre and skill-package checks that had been grouped in the previous workspace job, leaving the application job focused on the Pegasus solution and the workspace job focused on document extraction. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 53. 674339e264abfc34f4bce1f0e58292643493d328 — 2026-07-28 — Merge pull request #10 from collisionengineers/split/03-document-extraction

This merge imported the independent CollisionDocNet document-extraction workspace and added a dedicated CI job for it. The workspace contains a managed .NET library and command-line tool for deterministic, headless extraction of ordered text and discrete images from PDF, legacy Word, DOCX, MSG and EML files, together with bounded readers, format-specific components, extensive tests, format contracts, source/provenance records and packaging guidance. Its documented boundary excludes Office automation, rendering, OCR, conversion, hosted extraction services and arbitrary attachment output; it remains a source workspace rather than a Pegasus application caller. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 54. 65aed4af9929b8d68dcb05249acde263e06ce62c — 2026-07-28 — Update AGENTS.md

This governance-only change tightened the repository Git-safety instruction: pull-request merges are allowed only when the operator explicitly includes the phrase `MERGE AUTH GRANTED` in their prompt. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 55. dab73e29d09256a7ca7e056e2cb0ff1d2be89f09 — 2026-07-28 — fix(renderer): align imported integration boundary

This commit aligned the imported report-renderer workspace with Pegasus’s source-only and design-ownership boundaries. It moved report templates, stylesheet, signatures and temporary Windows GUI package assets from the workspace into the top-level `design` system, then changed the renderer projects to link and embed those assets at build time. It updated the renderer’s container build context and SDK, its documentation and the workspace manifest to state that any future Pegasus integration would retain only headless rendering behind an accepted contract and caller, while retiring the separate desktop GUI and renderer-specific MCP host. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 56. dd76344e4ff0649fd38f9c231c28828f3dfc4b3d — 2026-07-28 — ci: align workspace validation with retarget base

This CI change moved the report-renderer validation out of the main workflow and into its own `report-renderer-check` workflow. The new workflow restores, builds, installs the renderer’s browser dependency and tests its solution; the main workflow continues to validate Pegasus and the document-extraction workspace. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 57. 270214ad675939e914db0984d3e87de4f1a2c161 — 2026-07-28 — Merge pull request #11 from collisionengineers/split/04-report-renderer

This merge imported the independent Collision Renderer workspace and its dedicated CI workflow. The workspace provides a shared .NET rendering engine, command-line tool, desktop application, API and MCP host for producing branded valuation evidence, advert packs, fee notes and expert-report variants as PDFs using Scriban templates, shared CSS and headless Chromium. It validates input, encodes text, supports controlled uploaded attachments and optional token authentication for the standalone API, and includes test suites and visual-regression tooling. The import remains source-only for Pegasus: it does not add a Pegasus caller, deployment or business-policy authority. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 58. 84878266c770908e9b0a4094cbf882f07c72db30 — 2026-07-28 — docs(ai-centre): align corpus and desktop direction

This documentation change clarified the AI Centre workspace’s long-term desktop direction and local data boundary. It described a future engineer workstation as a composition of accepted Pegasus policy, APIs, design, renderer and caller-backed retrieval rather than a parallel application, and listed the approvals and technical contracts required before work could start. It also moved the approved local AI development and ML-operations input location to the ignored immutable `corpus/ai-centre/` subtree, documented its former source mappings and prohibited automation from fetching or changing that corpus. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 59. 94dbe97454412e117342e1e66fbc1da3f0f47644 — 2026-07-28 — ci: isolate AI Centre validation

This CI change gave the AI Centre workspace its own `ai-centre-check` workflow. It installs Node 24 with a cache keyed to Collision Brain’s lock file, then installs dependencies, type-checks, builds and tests Collision Brain. The main workflow was correspondingly reduced to Pegasus and document-extraction validation. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 60. 4ad22c7e8bc507d880b390cabe19186697f2f8fc — 2026-07-28 — Merge pull request #12 from collisionengineers/split/05-ai-centre

This merge imported the AI Centre workspace, its research and governance material, and a dedicated CI workflow. Its implemented Collision Brain service is a provider-neutral retrieval system with document ingestion, storage/repository adapters, a local hash embedding prototype, asynchronous indexing, hybrid search with stable citations, HTTP and MCP interfaces, role checks and controlled upload tokens; it deliberately returns source passages rather than AI-generated answers. The workspace also added ML-operations planning for data, training, vision, assessment, workflow and governance, while retaining the broader desktop direction as unimplemented and non-caller. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 61. cb9dce4cba93b27bf42f047391cf45f6c527cfaf — 2026-07-28 — Merge updated AI Centre branch into Agent Skills branch

This merge brought the updated AI Centre branch into the Agent Skills branch and resolved the shared CI workflow conflict. It carried forward the dedicated AI Centre validation workflow, the clarified long-term desktop composition, and the `corpus/ai-centre/` local development and ML-operations data boundary. It did not itself introduce a Pegasus application caller or alter active product capability allocation. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 62. e526d30b341a5de7e76ef2e8804afa35c0097c16 — 2026-07-28 — feat: restore reviewed skill source material

This commit restored reviewed source material for the AI Centre skill collection. It unpacked the repair-cost-defence skill into a deterministic branded Word-report generator with source-reading and court-response guidance, and restored development-reference material and fixtures for house style, design, diminution, manufacturer methods, roadworthiness, salvage, total loss, vehicle assessment and vehicle history. The workspace manifest was updated to describe the extracted archive and consolidated development-reference layout; these remain non-caller source packs, not Pegasus runtime code. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 63. d6d48a8b4af2004c40a8571c6c5dacbd849a00c4 — 2026-07-28 — Merge PR 8 branch into Agent Skills branch

This merge brought the report-renderer integration-boundary changes into the Agent Skills branch and resolved the workspace-manifest conflict. It transferred report templates, stylesheet, signatures and temporary GUI assets to the top-level design system, updated the renderer to embed linked design assets, and included its dedicated validation workflow. The renderer remained an independently maintained source workspace with no Pegasus caller. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 64. 9521d87e845cf191136c3f991fffeb3f06454bc7 — 2026-07-28 — Merge latest Agent Skills update

This merge incorporated the latest reviewed Agent Skills update into the combined branch. It brought in the extracted cost-defence report skill and the restored development-reference material, fixtures and checks for the other Collision Engineers specialist skill packs. Those packs remained source-only workspace content and did not activate any Pegasus runtime behaviour. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 65. 0f5f23d01da77e8c9b4e14dba6b9e83b31d0989d — 2026-07-28 — Merge pull request #13 from collisionengineers/split/06-agent-skills

This merge completed the Agent Skills import and documented the reviewed workspace manifests. It added the usable specialist packs for house style, design, diminution reports and rebuttals, manufacturer-method evidence, roadworthiness, salvage categorisation, total-loss assessment, vehicle assessment and vehicle-history checks, with their reference data, payload validators, report generators and packer tests. These packs provide evidence-led, engineer-reviewed guidance and explicitly retain competence, source and privacy boundaries. The application CI gained skill-package validation, but the packs remain independent source material and are not Pegasus callers. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 66. f83fbd83fd345c522d0bca7eda90450cb8b250d0 — 2026-07-28 — Restore elided questionnaire evidence

This documentation repair restored a section that had been replaced by an ellipsis in the historical project-discovery questionnaire. The restored evidence records the intended internal staff MCP boundary, no-migration cutover from the predecessor, data-handling decisions, operational scale and recovery targets, environment and access assumptions, and initial monitoring/support arrangements. It preserves historical source evidence rather than changing the living product rules. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 67. 15d5c5461ba2d7d1e13d83b7190ed954654d7769 — 2026-07-28 — Migrate retired product authority callers

This documentation maintenance commit redirected active documentation away from retired root-level product authorities and toward canonical product areas, while retaining the questionnaire and feature worksheet as historical reconciliation evidence. It updated source-of-truth guidance, product index, gap and open-decision documents, test runbooks and the Azure onboarding record; it also corrected test-runbook paths to the renamed Pegasus projects. `docs/product/capabilities.md` was changed only to point its retained worksheet link to `docs/history/product/feature-versioning-worksheet.md`. `NOW.md` was not changed.

## 68. 4f1fc53abdfbc7d6b53342c0f9ebcda3595411ce — 2026-07-28 — Merge pull request #14 from collisionengineers/split/07-pegasus-history-evidence

This merge combined the historical-documentation and EVA-evidence work with the authority-link migrations. It moved the questionnaire, feature worksheet and scaffold plans into `docs/history`; aligned historical planning terminology with the QDOS alpha horizon; and added EVA example data plus reviewed Engineer-workflow observations. It also carried the stated manual JSON-and-image EVA handoff boundary and updated canonical documentation to use the historical records only as evidence. `docs/product/capabilities.md` was changed for the worksheet relocation; `NOW.md` was not changed.

## 69. d94e688ed9fe54d57b36a1d803b776553eda025f — 2026-07-28 — Repair imported reference navigation

This documentation-only repair corrected navigation links inside the imported predecessor reference tree so its category index and documentation links point to the right relative locations. It also restored historical wording where the global terminology rewrite had changed technical version text or a filename-classification description. The large category-index edits were path corrections within retained reference material, not changes to Pegasus requirements or behaviour. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 70. 67cfd233e98c32a8c16574bf3951e4ce5adb2031 — 2026-07-28 — docs: clarify extraction and activation boundaries

This documentation change clarified that Pegasus uses PdfPig for embedded-PDF extraction and does not use the predecessor’s `cedocumentmapper`; any bespoke extractor remains deferred until separately accepted. It also removed a data-handling activation paragraph from the documents-and-integrations product area. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 71. 2ea6a4bd611c1bbeafe7f8e0914ab7771847261c — 2026-07-28 — Merge pull request #15 from collisionengineers/split/08-pegasus-imported-reference

This merge imported the large historical reference collection beneath `docs/reference/imp-docs/requirementsdocs`. It includes original instruction and case documents, emails, reports, images and work notes, plus normalized text companions, manifests, parser fixtures, provider coverage and regression evidence. The imported material is explicitly retained as reference and generated context rather than an active Pegasus requirement or caller. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 72. d8438a8ebc794feac31f0d475cc78cc7e2d19c7a — 2026-07-28 — Merge PR 8 branch into canonical docs branch

This conflict-resolution merge combined the canonical Pegasus documentation with the imported renderer, AI Centre and Agent Skills work. It moved renderer branding and templates into the root design assets, added isolated workflows for the two workspaces, and retained their code and skills as non-caller development material. It also added the long-term, deferred desktop direction: any future desktop surface must reuse Pegasus policy, design and accepted APIs instead of becoming a separate system. `docs/product/capabilities.md` was changed: it records the 229-capability allocation, including later desktop, renderer and AI outcomes and their activation boundaries. `NOW.md` was not changed.

## 73. b705e4e615de4edb47284b170a4f5bb6c954de1d — 2026-07-28 — Resolve canonical documentation review findings

This documentation review pass corrected the implementation handoff so it distinguishes the original CollisionSpike provider-data resource name from its Pegasus rename, and it refined the local checks required for each isolated workspace. It clarified that supported valuation vendors provide observations while an Engineer still owns the resulting decision, and it recorded additional principal and route candidates from the supplied mapped-principals spreadsheet as evidence requiring validation before activation. It also aligned Azure and QDOS planning, testing and product references with the canonical documentation structure. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 74. 5b78cb1bf5f3ea046422f92600bfff931272c9a9 — 2026-07-28 — Merge canonical documentation updates into repository integration

This merge integrated the canonical-documentation corrections with the workspace and design changes. It brought the separate AI Centre and report-renderer validation workflows into the repository and resolved the main CI workflow so the core application and source workspaces are checked independently; the resulting file contains two `source-workspaces` jobs with that same name. It also incorporated the clarified provider-data, valuation and principal-route evidence, the deferred desktop direction, and the relocated renderer assets and templates. `docs/product/capabilities.md` was changed as part of the merged 229-capability inventory; `NOW.md` was not changed.

## 75. b02bb28fdc07dda386205098ac7400a2c16b6afd — 2026-07-28 — Harden repository policy checks

This change strengthened the repository policy check so it validates each capability’s horizon and release allocation, recognises only the defined traceability-matrix labels, and allows retained predecessor names only in specifically documented contexts. It also prevents tracked corpus material, workspace Git links, generated/private workspace files and application build references to workspaces. The check now recalculates committed-file manifests directly from the Git index for each imported workspace and compares them with the recorded file count, byte count and hash; the workspace manifest table was updated for the moved renderer assets and added AI Centre material. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 76. ce30d9eb9f8d2cbff7b9ffa2c8a48675fa270251 — 2026-07-28 — Remove duplicate repository check job

This CI correction removed the first of two duplicate `source-workspaces` jobs introduced by the preceding merge. The retained job continues to validate document extraction, report rendering, AI Centre and skill packaging, so the workflow no longer repeats the smaller document-extraction and skill-only set. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 77. c9c0063925b1807175f8376dcb89cce2f6e3ef5f — 2026-07-28 — Merge pull request #16 from collisionengineers/split/09-pegasus-canonical-docs

This merge established the reviewed canonical Pegasus documentation across product, architecture, operations, design and change records. It defined the repository as a development proof of a clean-room case-management application, set the QDOS-alpha path and the manual EVA handoff boundary, and clearly separated actual callers from future integrations and independently buildable workspace imports. It also revised the capability traceability material and renamed the current gap record to `qdos-alpha-gap.md`. `docs/product/capabilities.md` was changed: it retains the 229 stable capability IDs, their horizons and activation boundaries. `NOW.md` was not changed.

## 78. a477be9e11d638f930436166ad2980e18b7f3b19 — 2026-07-28 — Merge pull request #17 from collisionengineers/split/10-pegasus-integration

This merge delivered the repository-integration checks with the canonical documentation. It installed the policy check behind the CI’s repository-language step, restored one comprehensive source-workspaces job, and added validation for the imported report-renderer and AI Centre workspaces alongside document extraction and skill packaging. The integration record also documented the reviewed remediation boundaries, including source-workspace manifests, renderer hardening and retained external-service safeguards, while leaving independent exact-head review outstanding. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 79. 669389984ca82716a5433fc112f7108fe0822409 — 2026-07-28 — Update .gitignore

This housekeeping change ignored local Claudian and Obsidian settings, an imported review-bundle directory, and generated output from the Collision Brain and skill-packaging workspaces. It also added a standalone `l` ignore pattern. No product code or requirements changed. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 80. 32f971b86b5a7457650d0f54876dc1ce686cc866 — 2026-07-28 — Update .gitignore

This follow-up ignore-file change added the repository’s `.obsidian` directory as a whole, replacing the earlier approach of ignoring selected Obsidian files. No product code or requirements changed. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 81. 17cb7846a6724884d13c354084dca7a8c5614382 — 2026-07-28 — Merge remote-tracking branch `origin/split/01-pegasus-governance` into `merge/pr8-authorized`

This large merge brought the Pegasus governance baseline into the authorized integration branch. It adopted the Pegasus identity and an explicit source-workspace boundary: document extraction, report rendering and AI Centre/skills remain independently validated source imports, while the four production projects and Core-owned business policy stay the runtime boundary. The merge also added the related workspace ADR, root getting-started guidance, CI/policy controls, product and design documentation, reference evidence and the imported workspace source. `docs/product/capabilities.md` was changed as part of the integrated capability inventory; `NOW.md` was not changed.

## 82. 3e5e06a49b15cf36fbc39c9af190d1fe0375b19d — 2026-07-28 — Update .gitignore

This housekeeping change ignored the jQuery validation licence file under the old `CollisionSpike.Web` static-library path. No product code or requirements changed. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 83. 716b4736efa35f4de40243a071a92c7259760b1f — 2026-07-28 — Add shared agent definitions

This change added shared role definitions for Azure implementation, design, documentation, planning, research, review, scouting and general delegated tasks. The definitions set each role’s expected scope, evidence standards, safety boundaries and output format, including read-only constraints for research and review roles. No Pegasus product code or requirements changed. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 84. 16f3e4331a8839925728ae90e4fc8debb0ed4ca6 — 2026-07-28 — Merge branch `main` of `https://github.com/collisionengineers/pegasus`

This merge brought the shared agent definitions into the current branch without adding further changes beyond those role-definition files. It therefore made the Azure, design, documentation, planning, research, review, scouting and task-agent guidance available on both branch lines. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 85. f0129504084ddaabf6bae3eb66dd86a79dde6e86 — 2026-07-28 — Update ui-spec.md

This user-interface specification correction clarified that a definitive authorised instruction creates one case through the ordinary fail-closed acceptance route: complete material starts in Review, while incomplete material starts Not ready. It also separated Triage from case decisions by defining optional roadworthiness and assessment findings, requiring at least one before completion, and stating that Triage findings do not alter a case reference, outcome, workflow or report. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 86. c355e49d6fc4eab4e36407a9cfae2765d38bae41 — 2026-07-28 — triage wording corrected

This product-documentation correction made Triage both a distinct inbox label and a separate pre-case reference record, never a case state. It changed the planned finding model to independently optional roadworthiness and repairability/total-loss findings, with one required for a recorded or completed Triage; those findings and links are reference-only and cannot drive case identifiers, workflow, outcomes, reports or Audit allocation. It also clarified that definitive authorised intake can create a complete Review case or an incomplete Not ready case through the shared acceptance path. `docs/product/capabilities.md` was changed for the revised `TRI-04` outcome; `NOW.md` was not changed.

## 87. 28f37b8c41586f2fcc599f49675671a32fb9e155 — 2026-07-28 — triage wording corrected

This matching design-documentation correction applied the updated intake and Triage rules to the user-facing requirements, interface specification and traceability matrix. It states that complete definitive intake may begin in Review, preserves Not ready for incomplete material, and presents Triage as a distinct inbox label plus reference-only pre-case record with the two optional finding types. No product code changed. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 88. cb7d19c9fbfff7d5d37defcb80a09a4264087248 — 2026-07-28 — collisionspike reference removed

This workspace-documentation correction replaced the former CollisionSpike integration description with the actual Pegasus development-only intake caller and its `DevelopmentOffline`/feature gate. It explicitly states that document extraction remains independently buildable source only, has no Pegasus adapter or production consumer, and may be integrated only through a later accepted contract while Core retains the decision policy. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 89. d5acada2ef9efc647504055aa295baa16bf5a74f — 2026-07-28 — moved ADRs

This documentation reorganisation moved ADRs 0001–0009 from `docs/architecture/decisions/` into the canonical `docs/decisions/` directory, expanded the decision index to cover both ADRs and repository decisions, and repaired the links throughout product, architecture, runbook and historical material. It also updated the Triage capability wording to include the inbox label and separate pre-case record. At this commit, the `TRI-04` capability row contains unremoved Git merge-conflict markers and two alternative descriptions, so the inventory is not cleanly resolved. `NOW.md` was not changed.

## 90. 467284f23b268e199d7fbe77dbb2163b50f00e23 — 2026-07-28 — Update motion.md

This design guidance change allowed a basic, non-essential refresh or loading animation while continuing to exclude a general product motion system and marketing-style animations. It requires the animation to have a static equivalent when reduced-motion preferences are active, so feedback does not rely on movement. `NOW.md` and `docs/product/capabilities.md` were not changed.

## 91. 296bc470a66e8bccff64ab7a70b0fd77e482b8d1 — 2026-07-29 — docs: centralize repository authority

This large documentation consolidation replaced the distributed product-area, history, runbook and workspace-document trees with a smaller set of root authorities. It introduced canonical `docs/requirements.md`, `docs/capabilities.md`, `docs/open-decisions.md`, `docs/engineering.md` and `docs/operator-notes.md`; moved the 229-ID capability inventory to the new path; and made the documentation index define ownership, evidence states, retention and drift rules. It also condensed design, architecture, operations, workspace, reference and AI Centre documentation and removed many derived reports, planning pages and duplicate guidance. `docs/capabilities.md` was created and is the capability inventory; the former `docs/product/capabilities.md` was deleted. `NOW.md` was not changed.

## 92. 1ea88ad9d6e29f086b3ea215ddb548fce82c5acb — 2026-07-29 — docs: record consolidation review identity

This change updated the Pegasus repository-orientation record to identify pull request 18 as the documentation-centralization delivery, its baseline and initial implementation commit. It recorded that local census, claim, call-site, link, anchor, reachability, language, source-boundary and workspace-manifest checks had passed, while retaining the distinction between that local evidence and the still-required frozen-head checks and independent review. `NOW.md` and `docs/capabilities.md` were not changed.

## 93. 48559b8f9e7204e14d5ee053c91c12c0300ea03b — 2026-07-29 — fix: hash workspace manifests from Git blobs

This policy-check correction changed workspace manifest generation from reading the working tree to reading each staged Git blob through `git cat-file`. It now hashes the recorded relative path and exact committed blob payload, making the verification independent of local uncommitted files; the four workspace manifest counts, byte totals and hashes were updated accordingly. The orientation evidence record was refreshed with its resulting material-claim inventory hash. `NOW.md` and `docs/capabilities.md` were not changed.

## 94. 54273b132ad6d28facf3c0808213ccb7c12d4572 — 2026-07-29 — fix: bind documentation proof to exact head

This change hardened the documentation-consolidation verifier so it requires the exact expected commit and a clean allowed index, materialises the checked documentation from that commit’s Git blobs, and pins the identity and hashes of all proof inputs. It added exhaustive identity and coverage checks for the material-claim and call-site inventories, plus safe repository-relative path handling. It also corrected source-workspace guidance: the house-style and design packages became evidence only, the total-loss package became an accepted-payload renderer rather than an assessor, the ABI comparison became optional dated evidence, and the DOCX matrix was labelled as workspace rather than Pegasus integration material. `NOW.md` and `docs/capabilities.md` were not changed.

## 95. bddb66cb37973e1474f56ca26d5f27284a48f690 — 2026-07-29 — docs: close source workspace authority gaps

This follow-up corrected remaining source-workspace authority claims. It changed the root instruction to name `docs/operator-notes.md`, labelled Collision Brain as an independently runnable service with no Pegasus integration, and rewrote the vehicle-history package as local evidence and a proposed connector contract that cannot make live external calls. The cost-defence and other skill references now describe their content as historical or package-local evidence requiring authorised review, while the document-extraction architecture treats CollisionSpike only as predecessor identity and any Pegasus adapter as future, separately accepted work. `NOW.md` and `docs/capabilities.md` were not changed.

## 96. 70c3b581d0ed31a76274b263cfd62d9c1653ac9b — 2026-07-29 — docs: close source workspace authority gaps

This final authority-gap correction changed the duplicated labour-rate references from claimed current runtime defaults into dated, package-local candidate evidence that needs Core, operator and authorised-human approval before use. It refreshed the consolidation material-claim proof hash and recomputed the document-extraction, AI Centre and skills workspace manifest values after the preceding edits. `NOW.md` and `docs/capabilities.md` were not changed.

## 97. 7f9f088150ff04d8336a38a27e25804dac412d8a — 2026-07-29 — docs: record consolidation review outcome

This documentation record marked the exact-head independent review as complete and recorded that review of commit `70c3b581d0ed31a76274b263cfd62d9c1653ac9b` found no blocker, required or advisory correctness finding. It described that documentation head as safe to precede capability-allocation work. `NOW.md` and `docs/capabilities.md` were not changed.

## 98. 19b69aaec49c9cef357d8d6f435ca34f2c258fb3 — 2026-07-29 — docs: allocate planned capabilities

This planning change assigned every one of the 200 planned capability IDs an exact first-introduction release from `0.1.0-alpha.1` through `1.4.0`, while retaining the 29 permanent boundaries as `Not planned / unallocated`. It documented the ordered release sequence, release-specific dependency conditions and evidence gates, updated the design traceability documents, and added policy checks for the exact release counts, target validity and unresolved merge markers. It also defined the one-way, fail-closed plan for mirroring the capability IDs into GitHub milestones and Project 3 without treating planning cards as activation or implementation. `docs/capabilities.md` was changed with the full release mapping; `NOW.md` was not changed.

## 99. c484c883cfac01b3844c179580e78ca3ad03eead — 2026-07-29 — docs: record capability allocation evidence

This change recorded the allocation run’s evidence: repository policy and language checks, restore, Release build and 184 non-corpus tests, plus an authenticated GitHub readback of 12 milestones and 229 keyed Project draft items. It states that Project field/card synchronization was completed but that saved-view layout and visual confirmation were intentionally stopped in favour of alpha delivery, so they are not claimed as evidence. It also records a compatible fallback release field retained after discovering an incomplete preferred field, rather than deleting it. `NOW.md` and `docs/capabilities.md` were not changed.

## 100. 8bd432828ad1bff9d84842168236ff17ef25cafd — 2026-07-29 — docs: link capability allocation review

This change moved the capability-allocation record from in progress to in review and linked it to pull request 20. It retained the statement that repository allocation is complete while GitHub planning state does not prove implementation, deployment, live verification, operator acceptance or release. `NOW.md` and `docs/capabilities.md` were not changed.

## 101. 1f6e9ae34c69779e9623c8cba31873758380298d — 2026-07-29 — docs: remediate allocation review

This review-remediation change removed obsolete `Next/Later / unallocated` wording from current design, architecture, operations and Azure owners, directing readers to the canonical capability inventory for exact deferred targets. It clarified that the three `1.4.0` outcomes are conditionally allocated rather than unassigned, replaced the deleted roadmap reference in the allocation record, and added a policy check to prevent stale allocation labels in those authority pages. `docs/capabilities.md` was changed for the conditional-allocation wording; `NOW.md` was not changed.

## 102. 83b1337fa6eff086b5287fac0204a1e2deb82fb1 — 2026-07-29 — docs: close allocation authority drift

This further review correction removed the remaining provider-API statement that described it as unallocated or deferred, leaving the capability inventory as the sole owner of its exact target. It expanded the repository guard to scan all mutable allocation-bearing design and documentation owners and reject `unallocated` wording unless it concerns a permanent boundary. The capability-file edit was a line-wrap-only formatting change. `docs/capabilities.md` was changed; `NOW.md` was not changed.

## 103. 77beead590b6212c2ecd48d84bd3bbe5bf52aabc — 2026-07-29 — Restore documentation evidence and canonical rules

This large corrective change restored historical plans, discovery records, reviewed reports, design mockups, AI Centre research and protected skill source material that had been removed or reduced by consolidation. It revised the authority rules so unique historical and reference evidence is retained unless it is a proven duplicate or has a verified destination, while keeping it subordinate to current requirements. It strengthened canonical requirements and capability links for identity, intake, Triage, mailbox taxonomy, image/VRM work and other detailed rules, and added an instruction that the imported AI skill packages cannot be modified without exact user authorization. `docs/capabilities.md` was changed to link individual capabilities to the restored precise requirements clauses; `NOW.md` was not changed.

## 104. 7bd94f23da5553e62d0955fcd4b3984b90a63822 — 2026-07-29 — temp plans added

This change added three large temporary planning documents and preserved copies of their originals: a documentation-centralization proposal, a full QDOS-alpha delivery plan, and a plan for versioning every planned capability. The plans define proposed scope, authority, verification, release sequencing, GitHub-project synchronization and implementation boundaries; the centralization plan explicitly describes itself as intended rather than accepted evidence. No product implementation changed. `NOW.md` and `docs/capabilities.md` were not changed.

## 105. bb8e87fb659463eb95059f5674a5184af448e4e8 — 2026-07-29 — eml importer

This change added a PowerShell importer for unique `.eml` files. When run, it scans a source directory while excluding the Pegasus checkout and reparse-point directories, fingerprints source and existing Pegasus files by length and SHA-256, confirms matching bytes before deduplicating, and copies only unique representatives into `corpus/import` through a temporary file and byte comparison. It writes source, Pegasus and error CSV reports under `artifacts/intake/eml-corpus-import` and reports the selected/copied/error counts. `NOW.md` and `docs/capabilities.md` were not changed.

## 106. e2bf316e61bbc9765f52facd93ee84cf15c8c46b — 2026-07-29 — docs: remediate documentation authority review

This documentation-review remediation separated the Development/local email evaluator from QDOS-alpha delivery: its reviewed genuine-email evidence may be a prerequisite, but its route, workspace workflow, command, report campaign and UI are not QDOS commitments. It corrected and expanded requirements for blocked-intake actions, closed-file custody, EVA package keys and image selection, recovery proof, editing and navigation, and it repaired cross-document authority links and retained-decision navigation. It also makes `Test-RepositoryPolicy.ps1` deliberately exit as a deferred no-op until after the alpha, so neither it nor its language wrapper can be reported as passing policy evidence. `docs/capabilities.md` was changed to record the `DOC-CON-052` allocation conflict and link affected capability rows to their precise owners; `NOW.md` was not changed.

## 107. 990e078f3e3535fc55fc8e6959c5ec40162f6958 — 2026-07-29 — docs: allocate planned capabilities

This planning change gave each of the 200 planned capability IDs a first intended release target, from the existing alpha through `1.4.0`, while retaining 29 permanent product boundaries as `Not planned / unallocated`. It added an ordered release narrative and dependency constraints, described the intended GitHub milestone/project synchronization, and clarified that a target neither activates a feature nor proves implementation, a caller, deployment or acceptance. It also updated open-decision wording for the newly sequenced provider, EVA and AI work, and expanded the deferred policy checker’s consistency rules. `docs/capabilities.md` was materially changed as the authoritative release-allocation ledger; `NOW.md` was not changed.

## 108. b7d22af7fcc0380a31493d42aa27daa99ede3ede — 2026-07-29 — docs: record capability allocation evidence

This follow-up recorded the claimed GitHub planning work for the release allocation: twelve milestones, two Project views, and 229 keyed draft capability items with their stated count and field readback. It also records that presentation of the Project views was stopped before their grouping, filtering, displayed fields or visual samples were configured or accepted, so repository allocation and alpha delivery must not depend on them. The change updated operations guidance to make the same distinction and lists local-check and review evidence claimed at that point. `NOW.md` and `docs/capabilities.md` were not changed.

## 109. 95289bc2922e2b122ac08306adc2f978b5350a12 — 2026-07-29 — docs: link capability allocation review

This small documentation update marked the capability-allocation change as in review and linked it to pull request 20. It replaced the pending-review wording with the PR-specific status while retaining the distinction that allocation and GitHub planning state are not proof of implementation, deployment, live verification or acceptance. `NOW.md` and `docs/capabilities.md` were not changed.

## 110. ffd7275c11259be8a4a45473753a2695e7be958d — 2026-07-29 — docs: remediate allocation review

This review correction removed stale descriptions of future work as `unallocated` from design, architecture, operations and Azure-retirement guidance, directing readers to the capability inventory’s exact release targets instead. It replaced the long future-ID listing with clear lists of interfaces and integrations absent from the alpha, clarified the three `1.4.0` outcomes as conditional allocations, and added a checker rule to catch the stale label in the affected authority pages. `docs/capabilities.md` was changed to clarify those conditional outcomes and the release-change rule; `NOW.md` was not changed.

## 111. 4ea0566d93c958cd179cd84125831c31bb2adde0 — 2026-07-29 — docs: close allocation authority drift

This follow-up corrected the remaining requirements sentence so the capability inventory, rather than the requirements, owns exact release targets for additional-provider routes and the provider API. It expanded the repository checker from four pages to all listed mutable allocation-authority documents and made it reject any `unallocated` label that is not an approved permanent-boundary use. The allocation change record was updated to document the finding; the capability-file change was formatting only. `docs/capabilities.md` was changed; `NOW.md` was not changed.

## 112. 81a091977fb221a09e1bcc65c1cda34f92e4cdc8 — 2026-07-29 — temp plans added

This commit added three extensive temporary plans, with preserved original versions: a proposed documentation consolidation, a complete QDOS-alpha delivery programme, and a capability-versioning/GitHub-project plan. The consolidation proposal specifies an exact artefact census, authority spine and evidence-preservation process; the alpha plan sets implementation and released-alpha finish lines, gated decisions and caller-proof requirements; and the allocation plan sets target-release, milestone and Project-card rules. These are planning documents that repeatedly label their outcomes as intended or gated rather than implementation or acceptance proof. `NOW.md` and `docs/capabilities.md` were not changed.

## 113. f77e1492b25abdd5a14725f4c15129333482b743 — 2026-07-29 — docs: remove temporary planning artifacts

This commit removed the six temporary planning files added immediately beforehand, including the three preserved originals. It therefore withdrew the temporary documentation-consolidation, QDOS-alpha-delivery and capability-versioning plans from the repository without changing application code or canonical product documentation. `NOW.md` and `docs/capabilities.md` were not changed.

## 114. 536f5fc470a541281f86ebc711564d49432ed73f — 2026-07-29 — Merge pull request #18: Centralize repository documentation authority

This large merge centralized the repository’s documentation into a new root index and named owners for requirements, capability allocation, open decisions, architecture, operations, engineering workflow and operator truth. It consolidated many product, design, runbook, evaluation, workspace and reference pages into those owners or workspace-level technical documents; moved the ADR set into `docs/decisions`; and removed extensive duplicate, superseded or derived documentation and selected reference artefacts. It added detailed engineering workflow and operator-authority records, revised the QDOS orientation/change evidence, expanded the repository-policy checker, and preserved explicit boundaries that imported workspaces are not Pegasus callers or deployments. `docs/capabilities.md` was created as the canonical 229-ID allocation inventory; `NOW.md` was not changed.

## 115. 46b0328b149d7da887fa899c8aa39e01fcf159dc — 2026-07-29 — Merge pull request #20: Allocate planned capabilities to exact releases

This merge brought the capability-allocation work into `main`. It assigns all 200 planned capabilities to one of twelve first-introduction releases while retaining 29 permanent boundaries as unallocated, adds the release sequence and dependency narrative, and updates design, operations, architecture, Azure and decision guidance to refer to the allocation owner. It also records the GitHub planning synchronization/review evidence and broadens allocation-drift checks in the repository policy script. `docs/capabilities.md` was materially changed as the allocation ledger; `NOW.md` was not changed.

## 116. d2442ca33b6ba05297916f434a07c02c2476437b — 2026-07-29 — docs: update QDOS delivery plan

This commit added a temporary, hardened delivery plan for the 128-capability QDOS alpha and a short guide explaining the temporary-plan directory. The plan distinguishes an implementation-candidate pull request from a later released-alpha outcome, sets many proposed contract reconciliations and evidence gates, defines intended Core/Web/Worker responsibilities, and specifies staged work for identity, intake, custody, Triage, MCP, Azure and operational proof. It is explicitly a gated plan: external operations still need exact approval and its proposed decisions are not silently treated as settled. `NOW.md` and `docs/capabilities.md` were not changed.

## 117. 429c9704b26e8b4bc7f288c226fff8f993406c85 — 2026-07-29 — feat: add local email evaluation workbench

This change added an Intake page for locally uploading one `.eml` file, reading its content and transport evidence through the existing source reader, and presenting the QDOS instruction-extraction result. The page validates file type, emptiness and a 10 MB limit; it keeps the upload in memory and does not persist a receipt, source file or generated artefact. Integration tests exercise a valid synthetic email, malformed input and validation failures, confirm HTML encoding and assert that no receipts or artefact files are created; the upload page links to the workbench. It also added a Python Git-file manifest helper and a tracked-file list for the document-extraction workspace. `NOW.md` and `docs/capabilities.md` were not changed.

## 118. c4d2aadcfa972032f1acfbfd3fdee7b403e20a31 — 2026-07-29 — Merge pull request #21: Carry local evaluator and QDOS execution plan

This merge integrated both the temporary QDOS-alpha execution plan and the local email-evaluation workbench into `main`. The plan records proposed/gated delivery work, while the new page and tests provide a transient local `.eml` reader and policy-evaluation interface that does not persist intake data. It also included the related manifest helper and document-extraction tracked-file list. `NOW.md` and `docs/capabilities.md` were not changed.

## 119. f6c769ae80799b3d3ec07a38d935fcf5f04ce34e — 2026-07-29 — docs: adopt single-context engineering layout

This documentation-layout change added a root `CONTEXT.md` glossary defining Pegasus terms such as Case, Principal, Triage, Needs sorting and Blocked intake. It moved the root ADR collection from `docs/decisions` to `docs/adr`, added an ADR accepting that single-context arrangement, removed the former repository-plugin and Azure-workflow decision records, and updated repository links accordingly. It also added agent guidance for GitHub issue tracking, triage labels and use of the glossary/ADRs, while retaining the existing requirements, operator, architecture and workspace owners. `NOW.md` and `docs/capabilities.md` were not changed.

## 120. 91382e1dfb5cd652f8c03be37011b0bb603bf16b — 2026-07-29 — Create PegasusSystemPlan.md

This commit added a draft management-facing Pegasus system plan under `New folder/`. It describes a proposed end-to-end replacement of EVA and surrounding spreadsheets through intake, pairing, case setup, engineer decisions, deterministic reports, sending, correspondence, management information and Box filing; it also lists existing assets, external dependencies, risks, team roles and a phased build order. The document calls for engineer approval of AI output and identifies its status as a draft for discussion, rather than implementation or acceptance evidence. `NOW.md` and `docs/capabilities.md` were not changed.

## 121. f7a22586e9221031dcd099122ba7d5e49ca2e517 — 2026-07-29 — chore: exclude local source draft

This commit removed the draft `PegasusSystemPlan.md` from the tracked `New folder/` path, reversing the previous addition. It made no application or canonical-documentation change. `NOW.md` and `docs/capabilities.md` were not changed.

## 122. 14d2a0ef28d29271ee16bdc79056b232dbd6fcbb — 2026-07-29 — Create PegasusSystemPlan.md

This commit re-added the same 26 July 2026 draft Pegasus system plan under `grillref/` rather than `New folder/`. It again describes the proposed EVA-replacement workflow, AI-assisted but Engineer-approved work, report generation, external dependencies and phased delivery; it remains a draft discussion document, not implementation or acceptance proof. `NOW.md` and `docs/capabilities.md` were not changed.

## 123. ae9400d687f2e823f197cc1648ab90f262d6f259 — 2026-07-29 — docs: activate QDOS alpha execution contract

This change added ADR-0014, accepting checkpoint one’s clause-specific QDOS implementation contract. It defines the intended Razor, Worker and staff-MCP caller boundaries, retains Core as the business-policy owner, narrows which earlier ADR clauses it supersedes, and records the evidence that must still exist before mail, vehicle, Outlook, Box, Azure and release steps can activate. It expressly keeps the local evaluator as separately owned evidence work and the repository-policy check as deferred, and updates requirements, the QDOS record and open decisions to link that boundary. `NOW.md` and `docs/capabilities.md` were not changed.

## 124. 918ff0afa1686c6ad26a77947dcd1ff2f0204d7a — 2026-07-29 — feat: establish QDOS functional foundations

This substantial foundation change introduced Core contracts for cases, custody, documents, request-scoped uploads, Triage and durable intake. It implemented a staged-intake flow that hashes and stores source bytes, records idempotent work, dispatches ID-only queue messages, leases/retries processing, records evaluations and marks poison or expired work; the matching EF migration adds the corresponding persistence tables and constraints. It added Azure Blob and Queue adapters plus Worker timer, queue and poison triggers, enhanced QDOS extraction to require an accepted direct sender route before producing an applicable draft, and exposed product version/source-SHA diagnostics from Web. Request-upload policy now creates hashed opaque tokens and evaluates expiry, revocation, rate, file and size limits without exposing tokens through `ToString`. `NOW.md` and `docs/capabilities.md` were not changed.

## 125. 8f145821a45757b16a5724cb6b6d55c136b2152b — 2026-07-29 — feat: add QDOS domain and offline runtime

This large offline-runtime change added Core actor and staff-role authorization contracts, mailbox-polling and vehicle-lookup work models, and persistence/custody implementations for case acceptance, documents, requests and queued custody. It adds a wide EF migration and local custody that confines case files to a validated root, retains accepted source bytes by hash, and verifies existing content for idempotency. It also adds local-development tooling: an SDK tool manifest, prerequisite doctor, database initializer, Azure-database bootstrap script, and an ownership-manifest launcher that creates isolated LocalDB, Azurite, Web and Worker runs and can smoke, stop or reset only its own run. Web startup and readiness behavior, Worker vehicle functions, package locks and integration tests were updated to support that offline profile. `NOW.md` and `docs/capabilities.md` were not changed.

## 126. 09a5190c75661ac8f5e2392dc01b1007a217a73d — 2026-07-29 — feat: deliver QDOS workflow and staff surfaces

This broad feature slice added staff sign-in, sign-out, password, access-denied and Administrator account/access pages; Triage list/detail pages and lifecycle/evidence contracts; request-scoped upload and case-document pages; and intake-review support for EVA handoff. It introduced deterministic offline EVA packages with the fixed ordered 13-field JSON, selected custody-confirmed images and SHA-256 manifest, while deliberately leaving production source mapping blocked pending its acceptance gate. It also added inspection-address resolution, email-evidence/chaser and staff-audit persistence, new Worker triggers for vehicle lookup, external work, EVA export and email evidence, plus a substantial database migration. Release-artifact, local-acceptance and Azure-plan scripts, CI, Bicep, readiness/performance/failure tests and operations/deployment documentation were updated; the repository-policy script was reduced to its deferred behavior. `NOW.md` and `docs/capabilities.md` were not changed.

## 127. a8d59919778ff1a3b2e972b14f77946b36d9cffa — 2026-07-29 — feat: expose QDOS operations MCP and acceptance gates

This change added the staff Operations MCP endpoint and tool families for intake, Triage, documents, inspection addresses and offline EVA handoff. The tools require authenticated, enabled staff with the appropriate OAuth scope, route to existing Core boundaries, validate identifiers and payload sizes, and use idempotency keys; the EVA tools return readiness or hashes and do not call EVA. It also added case workflow, lifecycle, assignment, report-approval/report-sent, close/reopen, edit-lease and manual-chase policies, plus role administration and operator-interface changes. The commit expanded acceptance, browser/accessibility, recovery, negative-matrix and MCP security tests; added an acceptance-evidence script with fail-closed offline-candidate prerequisites; and adjusted Azure platform/database-access infrastructure and deployment documentation. `NOW.md` and `docs/capabilities.md` were not changed.

## 128. 1f92643a33296871ad1b61911c077093f9516f64 — 2026-07-29 — fix: validate offline release with Azure CLI Bicep

This release-validation fix made the Bicep outputs explicitly dereference conditional resources only when activation is allowed, satisfying the Bicep type checker for the guarded deployment mode. It also changed the offline deployment-plan test so it can compile Bicep through either the standalone Bicep CLI or Azure CLI’s Bicep command, and checks the compiled activation guard and source-level approval-only condition more precisely. `NOW.md` and `docs/capabilities.md` were not changed.

## 129. f07fd9d4240428011e0782eded75e35118ca6beb — 2026-07-29 — fix: preserve canonical deferred policy gates

This commit restored the large repository-policy verifier implementation behind its existing immediate “deferred until post-alpha” exit. The preserved code covers documentation census/proof inputs, forbidden-prefix handling, links and anchors, allocation consistency and other repository rules for a later reviewed re-enable, while the current invocation remains a successful no-op rather than passing evidence. Its language-wrapper script was simplified to invoke the policy script without removed activation parameters and return its exit code. `NOW.md` and `docs/capabilities.md` were not changed.

## 130. ec465e31b53a085507dbbb402f048690336ad5bb — 2026-07-29 — added agent skills

This commit imported a large set of repository-local agent skills under `.agents/skills`, covering idea grilling, specifications, tickets, implementation, TDD, code review, architecture, bug diagnosis, research, triage, handoff, prototypes and wayfinding. The included instructions define the intended engineering flows, issue-tracker/triage conventions, domain-glossary and ADR formats, plus per-skill agent metadata. `skills-lock.json` records each imported skill’s GitHub source path and computed SHA-256 hash. `NOW.md` and `docs/capabilities.md` were not changed.

## 131. 6cf766581d385ce53cef3299183dc984ee9de826 — 2026-07-29 — triage skill changed to vetting

This commit renamed the imported repository-development `triage` skill and its labels to `vetting`, so it is not confused with Pegasus’s separate business Triage workflow. The new vetting guidance classifies incoming work as bugs or enhancements and moves it through needs-vetting, needs-info, ready-for-agent, ready-for-human or wontfix, with an AI disclaimer for tracker comments. It removes `docs/engineering.md`, changes the root agent/documentation routes to make installed skills the repository-development workflow authority, and adds architecture invariants about one Core policy owner, capability-oriented structure and explicit classifier/error precedence. It also restructures the workspace overview into an integration-status register separate from provenance. `NOW.md` and `docs/capabilities.md` were not changed.

## 132. 2d6f4a7c227ba5e5168ba4297af3dca2f34c36d0 — 2026-07-29 — fix: close QDOS alpha review blockers

This large corrective slice made the QDOS alpha workflow more durable and fail-closed. Mailbox intake now retains approved immutable source files before processing, records and validates detailed mail-route decisions separately from instruction extraction, and polls a local approved inbox through the Worker; accepted case creation now detects conflicting retries by storing the exact command material and fingerprint. It added persisted case-workflow state, editing leases, actions, history and a staff case-detail page, alongside durable external-work dispatch and poison handling that records custody failure rather than silently retrying exhausted work. Local development was rebuilt around isolated, owned runs with Azurite, LocalDB, Web and Worker processes, a development-only passwordless administrator and PKCE MCP client; release artifacts now require a clean checkout, bind the full executed revision and verify the Web build’s source-SHA diagnostic. The commit also added migrations, integration/negative/recovery tests, deployment-plan checks and operations documentation for those safeguards. `NOW.md` and `docs/capabilities.md` were not changed.

## 133. 05762c4445067b9d8f115af8008ba413229c76c8 — 2026-07-29 — GRILL SESSION

This documentation and product-definition review clarified the alpha’s intended behaviour. It defines new terms including Inspection + Audit, Automation Actor, field provenance, image-readiness assessment and Not ready; requires provenance for case data, global vehicle/history/value checks or recorded exceptions before engineer-queue eligibility, and makes ordinary incomplete cases Not ready rather than preventing safe identity allocation. It makes accepted provider-route categorisation the gate into Triage, makes Box the required day-one accepted-case custody target with explicit failure handling, and describes a constrained future AI image-readiness advisory. ADR 0011 restricts MCP to the named Claude Automation Actor rather than ordinary staff, with an ordinary operational Core-action inventory and no management powers. The commit updates UI/traceability and requirements material, records related unresolved decisions, removes the superseded grill reference plan, and updates `docs/capabilities.md` to reflect these revised allocation and boundary statements. `NOW.md` was not changed.

## 134. 37c4ac6caf2e6f9b7f19e14882499405c874426b — 2026-07-30 — grill session 2

This second product-definition review introduces Image Cases: image-only intake with a usable normalised registration becomes a Not ready case with an immutable Image Intake Reference, can later be consolidated into an eligible instructed case, and retains both identities and history; material without a usable registration stays Needs sorting. It also clarifies Audit-reference rules, held cancellation handling, third-party-vehicle image evidence, human-owned report-image selection, individual readiness blockers, and the definition of “New cases today.” The UI requirements add accessible full-row search results, clearer intake rows, consistent semantic icons, image and email-preview contracts, and constrained desktop identity layout. `docs/capabilities.md` changes UI-04 from “In today” to “New cases today” and records the updated alpha UI implications; `NOW.md` was not changed. The added change note records the review’s decisions and explicitly says it is not implementation or acceptance proof.

## 135. 0e2dd5bfc20e7ad20d01330fc36bb739f8327269 — 2026-07-30 — FURTHER GRILL

This follow-up documentation decision clarifies that a Needs sorting item must show the predicates that are missing, ambiguous or contradictory. It adds two non-actionable General mail categories: a multi-case `general-chase` remains one unlinked source occurrence, while a `case-summary` creates no intake, Triage or Case work. It also says a successfully generated focused manual EVA bundle is immediately downloadable with its JSON, chosen images and manifest, but downloading it neither proves EVA receipt nor report delivery and does not change case state. The accompanying review note records the same decisions. `NOW.md` and `docs/capabilities.md` were not changed.

## 136. 40bcb67b544c927cf62cacf20188896bb8220196 — 2026-07-30 — grill session

This source-document review defines Repairers as reusable organisations, clarifies Review and vehicle-enrichment terms, and adds safeguards against associating material merely because it arrived at a similar time. ADR 0012 adopts a conservative, reviewable MOT-mileage estimate that preserves raw observations, abstains or supplies a qualified range when evidence is inadequate, and does not activate a DVSA/DVLA caller. It also sets requirements for chaser history, repairer/address handling, a future manual-EVA bundle container review, a future Claude Desktop Automation Actor submission route, and deferred mailbox viewing/search of approved Deleted Items. `docs/capabilities.md` expands MAIL-11 to include read-only Deleted Items search and UI-10 to include an evaluated read-only View in Outlook action; `NOW.md` was not changed. The new source-review record distinguishes these documentation decisions from implementation and activation evidence.

## 137. e402fca91ec9b98fdcab0d0115d7cbaa18ab175f — 2026-07-30 — collisionbrain dotnet rewrite

This commit replaced the independent Collision Brain TypeScript/Node prototype with a .NET 10 solution, while keeping it outside Pegasus’s application and policy boundary. The new package implements the four existing MCP tools for retrieval, queued writing, document listing and confirmed removal; a stateless HTTP endpoint, stdio proxy, worker, upload staging, authentication modes, deterministic local embeddings, memory/PostgreSQL repositories and memory/filesystem/S3 object stores. It retains supported text, HTML, text-PDF and DOCX extraction, document lifecycle/tombstones, import/export and benchmark commands, and package-local tests, Docker orchestration and CI; the old TypeScript source, tests and npm files were removed. It also adds an ADR and a change record that describe the clean runtime cutover and explicitly leave Pegasus integration, production providers, corpus work, OCR, answer generation and deployment deferred. `NOW.md` and `docs/capabilities.md` were not changed.

## 138. 1ca5f69118c6c65bc3cde39bc3fb06a50c4c3e2c — 2026-07-30 — doc realign and qdos adr

This broad documentation realignment added the repository engineering workflow and ADR 0013, which settles the QDOS alpha contract: image-led work stays pre-Case, vehicle and readiness checks are mandatory, cancellation and Box recovery are staff-led, references stop after 9999, and the manual EVA bundle includes all eligible case-vehicle images without Pegasus selection controls. It renames the MCP decision to use a vendor-neutral Automation Actor rather than Claude-specific staff access, and revises the QDOS delivery plan’s caller, evidence and checkpoint wording accordingly. It also adjusts agent-skill guidance, domain/design/operations/open-decision material and redirects workspace ADRs. `docs/capabilities.md` moves one capability from Next to Later and updates CASE-13–15, EXT-03 and MCP-01–04 to match these contracts; `NOW.md` was not changed.

## 139. 29e80c2ce059fd9a45701a000710f968fda24e5a — 2026-07-30 — renderer reference files

This commit adds a reference pack for a Collision Engineers assessment-report renderer: four sample PDFs, logo and engineer-signature images, a design specification, a JSON Schema and four example jobs. The specification defines report layouts and the total-loss, repairable, cash-in-lieu and contract-repair outcomes; it requires raw job components to be validated and derived consistently by the renderer, while retaining specific free-text inputs and known wording placeholders. The schema formalises case references, vehicle and incident facts, assessment values, repair costs, narratives, engineer, fee and photo inputs, and the sample data provides concrete example report jobs. These are reference materials rather than a renderer implementation. `NOW.md` and `docs/capabilities.md` were not changed.

## 140. 1df63ef29494cc24f47f2e61cdc6bd0c78223809 — 2026-07-30 — feat: preserve staff case and triage workspace for consolidation

This feature adds a first persisted staff workspace for Cases and Triage. It introduces Core contracts for staff actors, case/Triage queries and commands, permanent action history, case editing leases, workflow state, queues and linked replacements; EF stores and a migration persist the corresponding records. The Web application gains cookie/Identity authentication, first-administrator bootstrap, sign-in/out/password-change pages, protected navigation, an operational dashboard, Case and Triage lists/detail pages, shared status/provenance/error components and a revised visual shell. The pages call the new workflow stores for lifecycle, chaser, lease, Triage finding, assignment and case-link actions, while Triage completion intentionally reports that exact reply evidence is unavailable. The commit adds integration tests for the visual shell/workspace and adapts intake tests to authentication and the new database baseline. `NOW.md` and `docs/capabilities.md` were not changed.

## 141. 28f2e7e9c194bfca4816a373042cb203a6dce43f — 2026-07-30 — feat: preserve qdos alpha continuation for PR23 consolidation

This very large consolidation expands the offline QDOS-alpha implementation across Core, Infrastructure, Web, Worker, SQL migrations, release tooling and tests. It standardises on LocalDB for local evidence and Azure SQL for deployed persistence, adds application/bootstrap, staff, role, principal, organisation, mailbox, workflow-configuration and MCP-client administration, and broadens durable intake, source download, case data, case lifecycle/leases, tasks/chasing, Triage replay, custody/recovery, vehicle replay/work, EVA handoff and sent-report-evidence handling. The Web surface is reorganised into Intake, Case, Triage, Operations, Search, Administration and public request-upload routes; the Worker gains profile-specific Azure/local composition and queue/timer processing; and an explicit Automation Actor MCP manifest plus scope/rate-limit/tool adapters is added. It replaces the SQLite baseline with SQL Server migrations, extends release/deployment and local/acceptance scripts, and adds wide architectural, Core and integration coverage for the new behaviours. Architecture, operations and the QDOS source-corpora record were updated to describe the callers and SQL Server-only boundary. `NOW.md` and `docs/capabilities.md` were not changed.

## 142. 3b62de69de3333e3002ffd1bd47ea5a3b1e35a87 — 2026-07-30 — feat: add standalone local email evaluator

This commit replaces the local Web email-evaluation page with a separate Windows WinForms tool outside the Pegasus solution. The evaluator deterministically reads top-level local `.eml` files, displays decoded inert message content, obtains an advisory from the shared intake reader/policy, and lets a reviewer copy a file into the retained Received/Sent taxonomy or a validated Other category with a required reason. It creates ignored local output only: a copied file and an append-only JSONL log, with failures for bad logs, categories, collisions and copy/log problems leaving source material unchanged. It adds the standalone project, UI, workflow, taxonomy/workspace handling and focused tests, plus a safe local email display reader; the old Razor page and its Web tests are removed. The ADR and change record explicitly exclude mailbox, Box, cloud, database, automatic filing and Pegasus case-policy effects. `NOW.md` and `docs/capabilities.md` were not changed.

## 143. 011c7abf02134312197ab0795761d31fa3841e24 — 2026-07-30 — docs: complete collision brain provider evaluation guidance

This documentation change replaces a broad provider catalogue with a concise, dated experimental comparison for the independent Collision Brain service. It describes the current .NET 10 and fixed 384-dimension vector boundary, then orders five possible embedding experiments: Cloudflare’s hosted BGE model, self-hosted BGE and E5, Google Gemini Embedding 2, OpenAI’s embedding model, and the existing deterministic local baseline. It records pricing/allowance and data-use caveats, including why none amount to provider selection or approval. The document also specifies a controlled benchmark with frozen inputs, separate re-indexing, quality/latency/recovery/cost measurements and pre-registered pass/stop rules; the README now links to it. `NOW.md` and `docs/capabilities.md` were not changed.

## 144. 8314a332ef3e6fe606630e95a070710978e11cfd — 2026-07-30 — docs: add PR23 consolidation and remediation runbook

This commit adds an extensive, step-by-step runbook for consolidating PR24, staff workspace work, the standalone evaluator, Collision Brain and renderer material into PR23 (`pegasus-realign`). It fixes ownership and safety decisions, identifies the exact source worktrees/heads, preserves source branches, prohibits external writes and destructive Git operations, and defines staged merge resolution rules that keep PR23’s product authority and one Core policy owner. The runbook specifies how to remove obsolete SQLite/staff-MCP/evaluator/ADR paths, reconcile migrations and documentation, implement required QDOS corrections, verify real callers and run the complete local test/review ladder before an approval-gated push. It is a plan, not evidence that the consolidation was performed. `NOW.md` and `docs/capabilities.md` were not changed.

## 145. f3cfd878fc105f1a47fc590b326e56571569ea03 — 2026-07-30 — merge: consolidate QDOS alpha implementation into PR23

This merge folds the QDOS alpha implementation branch into the PR23 consolidation branch. It brings in the broad Core, Infrastructure, Web and Worker implementation for identity and administration, durable intake and custody, case/Triage workflow, vehicle and EVA processing, report-sent evidence, operations, public uploads and the Automation Actor MCP surface; it also adds SQL Server migrations, LocalDB/offline tooling, release/deployment scripts and a large test suite. The merge updates the current architecture, operations, requirements and QDOS evidence documents, replaces obsolete local routes and SQLite baseline coverage, and incorporates the initial migration/runtime composition needed by the merged solution. It is a consolidation merge rather than independent proof of a deployed or accepted product. `NOW.md` and `docs/capabilities.md` were not changed.

## 146. 38202df68afb155206f97a91a97084c26a6f9636 — 2026-07-30 — merge: fold staff case and triage workspace into PR23

This merge brings the earlier staff Case/Triage workspace into the PR23 consolidation. It adds the transferred staff/action-history contracts, an alternative workflow persistence implementation and migration, authenticated actor accessor, Case and Triage detail pages, shared UI components for status, freshness, provenance, errors and reason prompts, plus visual assets and focused workspace tests. It also updates the design authority and intake test support. The merge preserves these as consolidation inputs alongside the existing QDOS implementation; it does not itself establish a final single policy/persistence implementation or product acceptance. `NOW.md` and `docs/capabilities.md` were not changed.

## 147. d56a98cedf7218d30d54b6cecf0beec7b3a2a0f2 — 2026-07-30 — merge: fold standalone email evaluator into PR23

This merge folds the standalone local email evaluator into PR23 and routes its decision as ADR-0014 under the canonical ADR directory. It adds the independently runnable Windows evaluator, its taxonomy, safe local display, deterministic queueing, copy-and-JSONL filing workflow and focused tests, while ignoring evaluator output. It removes the legacy Web evaluator integration tests and retains the shared MIME reader adjustment. The imported change record and ADR keep the evaluator separate from the application, mailbox and cloud boundaries. `NOW.md` and `docs/capabilities.md` were not changed.

## 148. 9270a6a8df869d78d9e1b6774e54a0d117940392 — 2026-07-30 — merge: fold renderer reference evidence into PR23

This merge transfers the report-renderer reference pack into PR23 unchanged: four sample assessment PDFs, logo and engineer-signature images, the locked report design specification, data schema and worked JSON jobs. The material documents expected assessment-report formats and renderer inputs, but remains reference evidence rather than an application renderer or integration. `NOW.md` and `docs/capabilities.md` were not changed.

## 149. 1dcce452bbc8e84d870b8067e330bcc5292f0ab5 — 2026-07-30 — merge: fold Collision Brain workspace evidence into PR23

This merge folds the Collision Brain workspace rewrite and provider-evaluation documentation into PR23. It replaces the workspace’s Node/TypeScript service and tests with the .NET 10 package, its HTTP/stdio/worker runtime, MCP tools, storage/repository/authentication implementations and C# tests; package-local Docker/CI/configuration documentation was updated accordingly. It also adds the rewrite change record and the dated provider-experiment comparison, while maintaining the workspace as an independently buildable, non-caller source import. The merge updates top-level ignores and CI/workspace references but does not make Collision Brain part of the Pegasus application. `NOW.md` and `docs/capabilities.md` were not changed.

## 150. 9e27ce214af970557c779c0cf5344880c067555d — 2026-07-30 — fix: consolidate duplicate workflow ownership

This cleanup removes the duplicate staff Case/Triage workflow implementation that had arrived with the staff-workspace merge. It deletes the redundant Core contracts, EF entities/stores, migration, Web actor/detail pages and their workspace tests, and removes their dependency-injection registrations. The remaining QDOS implementation is left as the single registered workflow and persistence path. `NOW.md` and `docs/capabilities.md` were not changed.

## 151. f7a89ae5914d9ed01e621af052ec9fdecb78074a — 2026-07-30 — fix: register intake callers and test administrator

This fix restores the dependency-injection registrations for intake listing, detail/source download, mutation, resolution, re-evaluation and linking use cases that were removed with the duplicate-workflow cleanup. Integration-test authentication now uses the fixed DevelopmentOffline administrator identifier rather than a separate hard-coded identifier, so the test actor matches the initialized local account. `NOW.md` and `docs/capabilities.md` were not changed.

## 152. cbf481f3050bc306d873baaa3de1c243f5fd1a25 — 2026-07-30 — fix: make EVA handoff image order deterministic

This change removes the case-page controls that let staff choose the EVA overview, main-damage and ordered image set. Handoff generation now loads the eligible custody-confirmed images itself, requires at least two, uses the first two as the required leading images, and passes the entire ordered set to the Core command. The page explains that Pegasus includes every eligible image deterministically and EVA owns later selection and ordering. `NOW.md` and `docs/capabilities.md` were not changed.

## 153. 4c7e0ac991814b4c6e1793482c3fe173d4c28dc7 — 2026-07-30 — refactor: harden QDOS alpha delivery boundary

This large refactor removes substantial dormant or out-of-scope material to narrow QDOS alpha to its supported local boundary. It deletes the tracked reference corpus and its derived fixtures, the Bootstrap project, Azure/release build and database-bootstrap scripts, Collision Brain CI workflow, Web development evaluator, staff MCP/OpenIddict endpoints and clients, and related tests, administration pages and package dependencies. It also updates architecture, operations, operator notes and the QDOS change record to say that Worker callers exist, Automation MCP remains separately gated, and the evaluator is a separate desktop tool. The remaining code adds migrations for third-party vehicle evidence and removing dormant OpenIddict storage, changes custody/EVA handling so staff-confirmed third-party vehicle images are excluded, and adjusts local initialization, persistence and tests around the reduced scope. `NOW.md` and `docs/capabilities.md` were not changed.

## 154. fd67b03236567ff34e7329e3d83c2a2db9b2f8dd — 2026-07-30 — ci: bound QDOS pressure job

This CI adjustment gives the Windows QDOS pressure job a 15-minute timeout. It allows normal setup variance but causes a deadlocked caller path to fail instead of using GitHub Actions’ default six-hour job window. `NOW.md` and `docs/capabilities.md` were not changed.

## 155. cc08c846fa9f1adf8638bf0330354d2c41c9748a — 2026-07-31 — .codex folder setup

This commit adds a repository-local Codex team configuration. It defines fifteen specialist agent roles with bounded responsibilities, grants each only its assigned local skill packages, makes Microsoft documentation available to the team, and leaves the Azure MCP disabled. It also imports the 37 referenced skill packages and their supporting guidance and scripts for architecture, Azure, build, performance, testing, workflow and MCP work. `NOW.md` and `docs/capabilities.md` were not changed.

## 156. f2b437016009e327e5b30fbe02bcbce097fa976c — 2026-07-31 — docs

This commit adds a prepared, explicitly non-authorizing plan for retiring the CollisionSpike development estate and implementing then deploying Pegasus to a separate Azure development group. It records the exact predecessor archive and deletion safeguards, proposed Graph, Box, vehicle-evidence and EVA boundaries, release tooling, maintenance-mode, infrastructure, identity, storage, verification and approval gates; the Azure documentation index now links to it. It also commits a local smoke SQLite database and an unintegrated UTF-16 C# acceptance-store draft, despite the plan itself describing those paths as work to preserve. `NOW.md` and `docs/capabilities.md` were not changed.

## 157. 371b86a935c6b68bb7c14fb733b5c1446bde9569 — 2026-07-31 — small dir alter

This documentation reorganisation moves the comprehensive QDOS alpha delivery plan and its readme from `temp-plan/` to `research-and-planning/`. That plan remains a gated proposal for implementing the allocated QDOS alpha scope, with detailed evidence states, product decisions, caller requirements and delivery checkpoints. The commit also removes the older `realign-plan/plan.md` PR23 consolidation and remediation runbook. `NOW.md` and `docs/capabilities.md` were not changed.

## 158. 99c6f1ae7574e7998d060218b7fc739b9493d36b — 2026-07-31 — merge: adopt single-context engineering documentation layout

This PR23 merge brings a large QDOS-alpha implementation and documentation consolidation onto `main`. It adds Core use cases and policies for staff identity, case creation and lifecycle, intake, custody, Triage, vehicle evidence, EVA handoff, mail evidence, tasks and operations; EF persistence and a long migration sequence; local adapters; Worker queue, mailbox and staging callers; and an authenticated Web interface for administration, intake, cases, Triage, operations, search and upload requests. It also imports extensive unit, integration, browser, architecture and performance tests, updates Azure/deployment and CI configuration, removes the obsolete in-Web email evaluator, and deletes the formerly tracked reference corpus and legacy decision layout. Documentation is reorganised around `docs/adr/`, `CONTEXT.md`, installed agent workflows and new change records, while the capability inventory is revised with clarified/deferred mail, MCP and AI allocations. `NOW.md` was not changed; `docs/capabilities.md` was changed.

## 159. c946952a2b54e63d3a9521c8afa3aecd904b9ce1 — 2026-07-31 — merge: merge dev into main

This merge advances `main` to the already-merged PR23 tree. Its resulting tree exactly matches its `dev` parent, so it introduces no additional files or content beyond the QDOS-alpha implementation and documentation consolidation recorded in the preceding merge entry. `NOW.md` and `docs/capabilities.md` have no changes unique to this merge.

## 160. 20d36666c694365e34e351b41262f6a85dafd6e7 — 2026-07-31 — docs update

This documentation change accepts ADR-0014, which replaces the assumed Azure development/integration environment with a local-development-to-production-only delivery model. It revises the target Bicep plan, architecture, Azure guidance, requirements and operations capabilities to describe one production deployment; adds a detailed but non-authorizing production replacement and predecessor-retirement plan; and records the planned UI-10 email-management workspace decisions, including in-app viewing rather than a `View in Outlook` action. The infrastructure, integration, release, archive, approval and recovery steps remain planned and separately gated. `NOW.md` was not changed; `docs/capabilities.md` was changed.

## 161. 690e97d16cc1d0c53fe54e715b036cfdba703aae — 2026-07-31 — feat: implement Azure production replacement route

This change implements the source-side production replacement route without performing any Azure, provider or retirement operation. It makes Bicep production-only and fail-closed on an explicit live deployment mode; provisions separate transport and custody storage boundaries, dedicated Web and Worker identities, SQL, Key Vault, telemetry, alerting and a production budget; and keeps Worker triggers disabled. It adds Graph mail-source, Box custody and DVLA/DVSA production adapters behind existing Core ports, production startup validation and composition wiring, plus tests for those adapters and Worker configuration. It also adds scripts to validate the deployment plan, build hashed release artifacts, bootstrap the database and first administrator, smoke-test production, archive the predecessor, and retire it, accompanied by a change record and updated production/operational documentation. The record explicitly marks deployment, live verification, acceptance and predecessor retirement as not performed. `NOW.md` and `docs/capabilities.md` were not changed.

## 162. 99b74d5e84a8775a58f5f2ec2a76bbfc1b99d520 — 2026-07-31 — fix: restore release runtime assets

This release-packaging fix restores each deployable runtime before publishing it. The artifact script now performs locked `linux-x64` restores separately for the Web and Worker projects, then uses those restored assets for the existing no-restore publishes. This ensures their runtime-specific dependencies are present in the packaged output. `NOW.md` and `docs/capabilities.md` were not changed.

## 163. 682a2180541ee466e3670d25ee675bb942601a48 — 2026-07-31 — fix: lock release runtime targets

This follow-up makes the release runtimes part of the project contracts and locked dependency data. Core, Infrastructure, Web and Worker now declare `linux-x64` and `win-x64`, and their lock files gain the resolved runtime assets. The release script consequently uses ordinary locked project restores before publishing Linux packages, relying on the declared runtime identifiers instead of a restore-time runtime argument. `NOW.md` and `docs/capabilities.md` were not changed.

## 164. 504a23ab0263852f27ef6518c5da86a61778a457 — 2026-07-31 — docs: record local production release proof

This evidence update records that the local production-route checks passed: restore, a zero-warning Release build, 539 non-corpus tests, Bicep compilation, deployment-plan validation, and the three-run QDOS pressure profile. It also records successful build and revalidation of the immutable Web, Worker and migration artifacts with their SHA-256 manifest. The documentation explicitly limits that evidence to local proof: Azure preview/provisioning, live integrations, deployment, acceptance, retirement and recovery were still not performed. `NOW.md` and `docs/capabilities.md` were not changed.

## 165. 7627e3c77ee17939f7d2d3b2edca34fb52c69b0d — 2026-08-01 — docs(azure): record predecessor archive preflight

This change records an approved read-only predecessor preflight and local OCI archive: inventories and non-secret metadata were captured, and sixteen unique ACR images were preserved and verified by the recorded archive manifest. It documents that no secret values, excluded predecessor data, resource changes or deletions occurred. The archive script is hardened to include inherited role assignments, use and verify OCI-layout copies of unique digests, keep archive targets inside the approved root, and handle alternate Azure usage-record identifiers. It also records that SQL and generic quota validation remained blocked because the relevant Azure providers were unregistered, requiring separate approval for registration and preview. `NOW.md` and `docs/capabilities.md` were not changed.

## 166. 5aa98739444b5458424b28452fb3f4af17e31172 — 2026-08-01 — fix(azure): use durable Box JWT authentication

This production-integrations correction replaces the planned static Box access token with Box JWT authentication. The Worker now reads a retained Box JWT configuration and client secret through Key Vault references, validates the configuration, obtains short-lived SDK authorization headers and attaches them to custody requests; infrastructure parameters and runtime settings were changed accordingly, with tests updated. The records also state that SQL provider registration was the only Azure mutation, while the Azure preview stopped before resource evaluation because the immutable Graph mailbox/folder IDs and DVSA token endpoint were still unavailable. `NOW.md` and `docs/capabilities.md` were not changed.

## 167. 07e55f56ade4e03c6f9a427777c2c4e556ebeab0 — 2026-08-01 — fix(azure): separate azd and deployment environment names

This correction avoids an Azure Developer CLI reserved-variable collision by passing Bicep’s required `prod` value through `PEGASUS_ENVIRONMENT_NAME` rather than `AZURE_ENV_NAME`, which azd expanded to its local environment name. The documentation records that the required integration metadata was subsequently obtained without message or attachment reads, that a temporary exploratory Graph permission was removed, and that the corrected preview reached ARM preflight. It then stopped because UK South App Service quota allowed zero VMs while the deployment requires one; no resource was created or changed, and provisioning remains unrun. `NOW.md` and `docs/capabilities.md` were not changed.

## 168. 66345b101ecd1d19c4b2cca852054eebf0f9709a — 2026-08-01 — fix(azure): use quota-backed production web plan

This deployment-plan correction changes the production Web plan from unavailable Linux B1 to Linux P0v4, the smallest UK South tier with the subscription-specific quota available after the B1 quota request failed. It documents the resulting monthly cost increase and records that the P0v4 ARM preview exposed a separate regional virtual-machine aggregate limit of zero. The repository records unsuccessful self-service/support routes for that aggregate limit and makes a quota of at least one plus a clean preview the next gate; no workload resource was created, provisioned or deployed. `NOW.md` and `docs/capabilities.md` were not changed.

## 169. 253e65f2bb5f81df0a28ef3e0664b9b6de2d9058 — 2026-08-01 — docs(azure): use CLI aggregate quota route

This documentation correction replaces the proposed support-ticket route for the UK South aggregate App Service quota with an Azure CLI REST request that addresses the literal wildcard quota resource using `%2A`. It records the earlier one-hour quota-write throttle and adds the exact update-and-poll sequence to raise the aggregate limit to one before retrying the unchanged P0v4 preview. It does not execute the request or provision a workload resource. `NOW.md` and `docs/capabilities.md` were not changed.

## 170. e71fd867b8af944760fdecbcc5eb64899825db9d — 2026-08-01 — feat(azure): host production web on container apps

This change accepts ADR-0015 and replaces the blocked App Service Web route with Azure Container Apps Consumption. The intended production topology now adds a Basic private registry, deploys a digest-pinned Linux/AMD64 Web image with managed-identity pull, permits only one active revision and scale-to-zero/one, and keeps Web activation conditional until base infrastructure, image verification, database migration and administrator bootstrap are complete. Release packaging and validation are extended to a schema-2 manifest containing a locally built OCI archive, while the old Web ZIP remains bootstrap-only. It also substantially hardens predecessor retirement: a new manifest generator binds the verified archive, fresh resource/role inventories, retained vaults, child-group ownership, stop targets and deletion batches, and the retirement script validates that manifest before acting. Documentation records local compilation/OCI and test evidence but explicitly leaves clean packaging, the Container Apps preview, provisioning, deployment, live verification and retirement incomplete. `NOW.md` and `docs/capabilities.md` were not changed.

## 171. db472f338a6a51fafc66a15b5175c1f92d252e76 — 2026-08-01 — fix(azure): align worker with flex consumption settings

This Bicep correction uses Azure’s standard `AllowAzureServices` name for the SQL firewall rule and removes the obsolete `FUNCTIONS_WORKER_RUNTIME` application setting from the Flex Consumption Worker. The Worker remains configured as a .NET isolated Flex application through its deployment/runtime properties. `NOW.md` and `docs/capabilities.md` were not changed.

## 172. 5c5ec45fa7c1621b81fa7ef52154b309fa0f2560 — 2026-08-01 — fix(azure): validate multiline azd environment

This validation-script fix joins the multiline output of `azd env get-values` before checking the required pre-migration values. It allows the existing environment validation to search the complete command result instead of treating each output line as a separate value. `NOW.md` and `docs/capabilities.md` were not changed.

## 173. dd389cc29f56c262c53c3dfcb48e9f3336b1ea10 — 2026-08-01 — fix(azure): bootstrap sql with azure cli token

This database-bootstrap fix replaces the external `sqlcmd` dependency with the SqlServer PowerShell module and an access token obtained from the authenticated Azure CLI identity. It runs bootstrap and permission-verification queries through `Invoke-Sqlcmd`, uses named result columns for reliable matrix parsing, and clears the token variable after use. `NOW.md` and `docs/capabilities.md` were not changed.

## 174. 5a8155c0f44346a3b071f0c8ec1e1fbd6707e77f — 2026-08-01 — fix(azure): keep runtime sql grants role-only

This least-privilege bootstrap change revokes direct `CONNECT` permission from the Web and Worker runtime users after ensuring their membership in the respective runtime roles. Database access is therefore intended to flow through the controlled role grants rather than direct user permissions. `NOW.md` and `docs/capabilities.md` were not changed.

## 175. 2854ab4548d4a12bf5001c273d46baa491dacd80 — 2026-08-01 — fix(azure): allow standard sql system metadata grants

This bootstrap-verification adjustment accepts SQL Server’s standard `public`-role `SELECT` permissions on system metadata as well as its normal `CONNECT` grant. It continues to reject other unexpected public-role permissions while avoiding a false failure for built-in system metadata access. `NOW.md` and `docs/capabilities.md` were not changed.

## 176. 7bae137d1601fd0476435aeddf6df11af96a82c6 — 2026-08-01 — fix(azure): permit required runtime sql connect

This follow-up corrects the runtime users’ database access by granting, rather than revoking, their direct `CONNECT` permission. The verification rule is narrowed to allow only that direct grant and still rejects any other direct runtime-user permission, while data permissions remain role-controlled. `NOW.md` and `docs/capabilities.md` were not changed.

## 177. f793d3dccb2852be4503a8b1700ab8e046140574 — 2026-08-01 — fix(azure): normalize sql permission collation

This bootstrap-script fix makes permission-matrix queries robust when SQL metadata columns use different collations. It builds result rows with `CONCAT` and explicitly applies the database-default collation to principal names, permission names and table names before comparing the expected and actual runtime grants. `NOW.md` and `docs/capabilities.md` were not changed.

## 178. 379fa714b58c4a20546055d2655dfebc5ff6e33a — 2026-08-01 — fix(azure): ignore retired tables in sql verification

This verification fix excludes tables removed by the final third-party-vehicle and dormant-OpenIddict migrations from the expected runtime permission matrix. The bootstrap script now derives those dropped tables from the migrations and omits their historical table/grant entries before comparing permissions. `NOW.md` and `docs/capabilities.md` were not changed.

## 179. c452da7b98a6d47e4e07a3231fc68b9621db21ad — 2026-08-01 — fix(release): pin web assembly source identity

This release-packaging check prevents the Web assembly from carrying an unexpected source identity. The build disables automatic informational-version source revision addition, then runs the published assembly’s build-diagnostics command and fails packaging unless its schema, release version and source SHA exactly match the requested release values. `NOW.md` and `docs/capabilities.md` were not changed.

## 180. 9ee8037e2d0a9ed998717cee9780b652a304da4b — 2026-08-01 — fix(release): invoke the supported identity probe

This small packaging correction calls the Web application’s supported `--diagnostics-version` identity probe instead of the unsupported `--build-diagnostics` argument when validating the published assembly’s release version and source SHA. `NOW.md` and `docs/capabilities.md` were not changed.

## 181. 727797219fe0baab7f3ccd799e2c9ff0dede6964 — 2026-08-01 — fix(azure): allow web schema readiness check

This change grants the Web runtime role read access to EF Core’s migration-history table so the application can perform its schema-readiness check. It adds the corresponding migration, includes that one grant in the bootstrap script’s expected permission matrix, and tests that the Worker receives no equivalent migration-history access. `NOW.md` and `docs/capabilities.md` were not changed.

## 182. 7c447bb02250ded626b5227b5a6f999f5cb63fca — 2026-08-01 — fix(production): complete web and worker composition

This composition fix moves the Azure Blob intake-artifact store from the Worker project into Infrastructure, allowing both the production Web and Worker to resolve the same store behind the existing intake port. The Web production startup now registers the transient-intake container client and store, while the Worker preserves its conditional local container creation. The Function App explicitly uses its assigned identity for Key Vault references. It also refreshes shared production UI styling and the error page, and adds tests for profile-specific artifact-store selection, Web operations composition and the Worker Key Vault identity wiring. `NOW.md` and `docs/capabilities.md` were not changed.

## 183. 4837ff6cdbb5852a4de969e8d02708e584d0e2f2 — 2026-08-01 — fix(azure): permit transient intake tag lifecycle

This infrastructure fix grants the Web and Worker identities container-scoped Blob Data Owner on transient intake rather than Blob Data Contributor. The stronger, still container-bounded role permits the blob-tag lifecycle used for staged intake artifacts; the change adds an architecture test that asserts both scope and role. `NOW.md` and `docs/capabilities.md` were not changed.

## 184. 71d44c0848afcc7e5cc3fd9bfd634ace256d2bf0 — 2026-08-01 — fix(web): expose the production operator shell

This Web-shell adjustment removes the prominent local-only wording from the production Operations page and hides the local acceptance notice outside Development. It also makes the Intake navigation visible in Production while retaining the existing development feature flag for local intake. `NOW.md` and `docs/capabilities.md` were not changed.

## 185. 8f1bb1cd76aa483f2772e90684951a2ee2289b68 — 2026-08-01 — fix fail-open containment guard in Import-UniqueEmlCorpus

This corpus-import safety fix replaces platform-dependent string-prefix containment checks with relative-path containment checks that respect Windows and non-Windows case rules. It prevents the importer from scanning the Pegasus tree, including its protected `corpus/` directory, and avoids a false match for sibling directories with a similar name. The source root is now mandatory rather than defaulting to a user directory, and generated/import paths use platform-neutral separators. `NOW.md` and `docs/capabilities.md` were not changed.

## 186. 497360f6977f86d7dd122e0c2d5b0ac0d043ca14 — 2026-08-01 — add platform abstraction and make repository scripts path-portable

This change adds a shared, dot-sourced PowerShell platform helper for Windows and Linux. It centralises platform detection, filesystem-case-aware comparisons, executable naming, local database commands, process inspection and safe owned-process termination; on Linux it uses a dedicated process group to prevent descendants from being leaked after their parent exits. The affected scripts now use portable paths, the release builder can create a migration bundle for a chosen runtime and records its actual name in the manifest, and the validator reads that name rather than assuming Windows. Document-extraction scripts can locate their JSON contracts on Linux, while the Windows-specific report-renderer bundle explicitly refuses an unsupported Linux default. `NOW.md` and `docs/capabilities.md` were not changed.

## 187. 15b8de0ea4709cc3ac16cbb8a97e05edd547dae2 — 2026-08-01 — make Invoke-Doctor run on Windows and Linux

This change makes the environment-checking script support both approved workstation platforms. Windows retains its build requirement, while Linux requires a reachable Docker daemon and locally available database image; it also chooses local database, Python and Azurite checks by platform. The doctor now enumerates installed .NET SDKs so a missing pinned SDK is reported clearly, treats Linux development-certificate trust as a real advisory rather than a false pass, captures Playwright dry-run output at the process level, centralises repair guidance, and accepts PowerShell 7.6.3 or later below version 8. `NOW.md` and `docs/capabilities.md` were not changed.

## 188. cb85aa975e00cb448ab6b4c04e25dd56cf59c1b3 — 2026-08-02 — fix(worker): compose quarantine retention in production

This Worker composition fix makes the Azure Blob intake store also serve the quarantine-retention port. It adds streaming storage with length and SHA-256 integrity checks plus verification of an existing retained artifact, then registers the same production store instance for normal intake and quarantine retention. The worker composition test confirms the two ports resolve to that single shared store. `NOW.md` and `docs/capabilities.md` were not changed.

## 189. 1bfaee11ea1d519167827a629f3f710442ac2972 — 2026-08-01 — make local development lifecycle run on Windows and Linux

This change makes the local-development setup and lifecycle work on both supported workstation platforms. Windows continues to use LocalDB, while Linux creates one isolated SQL Server container per run with a generated password held only in an owner-only run file; the scripts start, check, stop and reset those resources through the shared platform helper. It also fixes empty-port allocation, single-run manifest lookup, manifest timestamp comparison and Linux process-identity tolerance, detaches started Linux processes from the terminal, removes unused database environment settings, and lets the existing integration-test fixture use an explicitly configured SQL Server data source instead of LocalDB. `NOW.md` and `docs/capabilities.md` were not changed.

## 190. 1028be9614d33eaf6021256f52c8a0b853d68d19 — 2026-08-01 — document Windows and Linux development

This documentation change records Windows and Linux as supported PowerShell 7 development platforms, with one platform used per workstation. It documents LocalDB on Windows and a loopback, per-run SQL Server container on Linux, including its credential handling and Linux test configuration; it also states that hosted CI remains Windows-only, so Linux results are developer evidence. The release route remains Windows-only because its migration bundle is `win-x64`, while the architecture, requirements, operator notes, ADR index and referenced workspace instructions are aligned with that distinction. `NOW.md` and `docs/capabilities.md` were not changed.

## 191. 50a3f0952fcc6526b33bb2836f00b3344ab9048a — 2026-08-02 — fix(graph): accept canonical delta cursors

This Graph mailbox-ingestion fix accepts Microsoft Graph’s canonical delta-link path forms for the already approved mailbox folder, including forms with the mailbox or folder identifier in parentheses. It keeps the safety boundary intact by still requiring HTTPS, the configured Graph host and an exact approved-folder delta path, and adds an integration test for the canonical OData cursor. `NOW.md` and `docs/capabilities.md` were not changed.

## 192. 096c9967f4fc87b76e36fed60fab0d80944e2e8e — 2026-08-02 — fix(graph): normalize delta cursor encoding

This follow-up makes approved Graph delta-cursor validation compare decoded path components instead of raw escaped paths. That allows folder identifiers containing encoded padding characters to match the approved folder while retaining the exact-path, configured-host and HTTPS restrictions; the integration test now exercises that encoded identifier case. `NOW.md` and `docs/capabilities.md` were not changed.

## 193. 94997dd036a48cde23fce0f960b159a2b4a921c0 — 2026-08-02 — fix(graph): preserve canonical folder casing

This Graph cursor compatibility change permits the canonical `mailFolders` casing as well as the existing `mailfolders` form when checking delta links for the approved folder. It adds the corresponding canonical-casing test while continuing to reject links outside the approved mailbox-folder boundary. `NOW.md` and `docs/capabilities.md` were not changed.

## 194. 8c61cf096acda9bf2b31de6c8b60ad61114eef56 — 2026-08-02 — fix(azure): bind Function Apps managed child parent

This predecessor-retirement safeguard makes the generated retirement manifest prove the ownership relationship for the managed child resource group more precisely. It now requires the exact Function Apps platform owner and a single same-named Function App parent for the group’s sole Container App, records that parent resource ID, and uses it for later inventory and deletion gates. The production-replacement runbook is updated so approval explicitly covers both ownership values. `NOW.md` and `docs/capabilities.md` were not changed.

## 195. bf1839c6a647dabb943a47fd560a13a0c0598755 — 2026-08-02 — fix(azure): stop Function Apps through parent

This predecessor-retirement correction stops excluding the managed child Container App from the generated stop list. Because the platform-managed child group has a deny assignment, the runbook now explains that the exact same-named Function App parent must be stopped and later deleted instead, with Azure managing the child’s lifecycle. `NOW.md` and `docs/capabilities.md` were not changed.

## 196. 1795b968985ba58f6f899f978c01d5cb7513d23e — 2026-08-02 — fix(azure): resume verified OCI retirement archives

This archive-resilience change adds an explicit option to resume an interrupted predecessor OCI-image archive only before its final archive manifest exists. On resume, the script verifies an existing image layout against its expected digest and keeps it if valid; otherwise it replaces the invalid partial layout and downloads it again. The retirement runbook documents this tightly scoped recovery path. `NOW.md` and `docs/capabilities.md` were not changed.

## 197. 3c1d2d73610a7c546faf1f052594f0f7252ffcca — 2026-08-02 — fix(azure): replace interrupted OCI layouts

This small archive recovery fix treats a failure to inspect a partial OCI layout as an invalid layout rather than aborting the whole resume. The archive script now catches that inspection failure, removes the unusable partial layout and recreates it from the expected image digest. `NOW.md` and `docs/capabilities.md` were not changed.

## 198. 4db61b32e8118d4091806ec53e1f34a53409709a — 2026-08-02 — fix(azure): retain every role disposition candidate

This retirement-manifest validation fix ensures every discovered role-assignment candidate remains in the list used to check the approved disposition file. It changes the deduplication to use each candidate’s ID explicitly and improves the failure message to report expected, supplied and unique counts when classifications are incomplete or duplicated. `NOW.md` and `docs/capabilities.md` were not changed.

## 199. 0e153dd9902ce7e083474bceeec949e7554e8492 — 2026-08-02 — docs(azure): record production replacement and retirement

This documentation update records that Pegasus was deployed to its sole production resource group and that the predecessor estate was retired through the bound archive and retirement manifests. It captures the deployed source revision and image digest, health and operator-route checks, Worker and Graph processing evidence, retained Key Vault reference status, the retained predecessor resources and role assignments, and the historical evidence hashes. It also makes clear that these results do not prove recovery or all external-provider business outcomes, and that an isolated recovery exercise remains mandatory before a second production release. `NOW.md` and `docs/capabilities.md` were not changed.

## 200. f183d0989ac7c7c89bb4ced3945c161edfd5601a — 2026-08-02 — docs

This change refines the planned email-management workspace behaviour: operators refresh manually rather than being interrupted by automatic refresh, and an opened message that refresh removes from the active view remains visible with an explicit state and return action. It aligns the UI specification, UI-10 change record and requirements, and assigns user-secrets IDs to the Web and Worker projects for local secret storage. This concerns the planned email-workspace capability, but `docs/capabilities.md` and `NOW.md` were not changed.

## 201. 198e8971a3f142a73a9f0e14b1f14cffe245ed9d — 2026-08-02 — add NOW.md work tracking; retire issue templates and migrate decisions

This tracking-process change makes the new `NOW.md` file the single current-work queue, with capped Doing, Next, Waiting and Path sections, an expiry rule and links to the roadmap and decisions. It removes GitHub issue templates after retiring issue/milestone tracking, simplifies the pull-request template to summary and verification, and moves the unresolved first-production-journey, release-evidence and critical-path questions into `docs/open-decisions.md`. This directly introduces `NOW.md`; `docs/capabilities.md` is linked as the roadmap but was not changed.

## 202. a7956f1d09541bad58b560f8551a63d072c460f1 — 2026-08-02 — simplify CI: dedupe workflows, drop dead policy gate, add timeouts

This CI simplification removes a duplicate report-renderer workflow and a disabled repository-policy check that could only report success. It moves frozen workspace validation into its own timed workflow that runs only when `workspaces/**` changes or when manually requested, adds timeouts to the main and workspace jobs, and removes unreferenced root files and the retired policy scripts. `NOW.md` and `docs/capabilities.md` were not changed.

## 203. 4e084ca2c8cca32df279a0f69b2f4d8fd9545b32 — 2026-08-02 — delete superseded planning material; migrate surviving content first

This documentation consolidation removes retired issue-governance material, old change records, historical delivery plans, planning questionnaires, a predecessor tree dump and obsolete mock-ups, leaving Git history as the archive. Before deletion, it moves ten still-open QDOS activation questions to `docs/open-decisions.md` and five operator statements to `docs/operator-notes.md`; it also replaces a detailed reference-file hash table with a shorter evidence guide. The commit changes neither `NOW.md` nor `docs/capabilities.md`, but it removes superseded planning material formerly related to the roadmap.

## 204. a8aeb5e9c23ef9588d749b1418faaa8fdaa2243a — 2026-08-02 — rewrite canonical docs around one authority rule; fix every broken link

This documentation restructuring reduces the index to a one-file-per-question map and a single authority order, moves repository-work guidance into `docs/engineering.md`, and puts evidence-tier rules in operations. It updates repository instructions to use `NOW.md`, the roadmap, open decisions and ADRs for planning, renames a misnumbered desktop-evaluator decision to ADR-0016, and updates documentation links and references after the earlier archive removals. `docs/capabilities.md` is changed to identify itself as the roadmap and point current work to `NOW.md`; `NOW.md` itself was not changed.

## 205. 8a70b5d8bd994300ad78929ab65ccd00fecdd95f — 2026-08-02 — add documentation link check to CI; delete completed corpus scripts

This CI change adds a PowerShell check that scans tracked Markdown outside imported workspaces and fails on broken relative file links, then runs it before the main build. It removes the completed corpus inventory and unique-email import scripts while retaining the separate provider-reference-data authoring script, and updates `NOW.md` to mark the link-check work complete and make the production post-deploy sweep the next item. `docs/capabilities.md` was not changed.

## 206. 05a4f444e7d101649e46ecb014b7b72c8a0f0fb9 — 2026-08-02 — merge feat/azure-production-replacement (production deploy fixes and runbook updates)

This merge brings the production-replacement branch into the current line. The merged work records production deployment and manifest-bound predecessor retirement, strengthens the retirement archive and managed-child safeguards, accepts canonical Graph delta-cursor forms, composes Azure Blob quarantine retention in the Worker, and aligns the email-workspace refresh rule and local secret project settings. `NOW.md` and `docs/capabilities.md` were not changed by the merge.

## 207. 13067d97125efebe8611887d7887050ef5e03677 — 2026-08-02 — post-deploy sweep: retire the executed runbook and predecessor tooling

This post-deployment cleanup moves the durable production environment, deployment evidence and second-release recovery gate into `docs/operations.md`, and removes the executed Azure runbook, Azure transition documents and one-off predecessor archive/retirement scripts. Architecture and deployment documentation are adjusted to stop describing production as pending. `NOW.md` advances the roadmap sequencing session to Doing and records recovery and platform vault purge as waiting items; `docs/capabilities.md` was not changed.

## 208. a8f7f0fa1f59a1554e515e07ebc45b1ad3a02259 — 2026-08-02 — raise validate timeout to 75 minutes

This CI adjustment increases the main validation job timeout from 30 to 75 minutes because historical successful Windows runs take 32–43 minutes. The comment makes clear that the timeout remains a hung-run safeguard rather than a normal-test limit. `NOW.md` and `docs/capabilities.md` were not changed.

## 209. 43efecd49109710be812ca45b503aa225c5b5c06 — 2026-08-02 — record the first production journey: full QDOS cutover to EVA handoff

This planning decision records the first live journey as a full QDOS cutover: a genuine instruction email must pass through Pegasus intake, review, Case/PO allocation and Box custody to an EVA handoff bundle, while EVA retains engineering and report work. `NOW.md` gains the ten-step critical path, required acceptance and recovery gates, and explicitly non-blocking capability groups; `docs/open-decisions.md` records the decision and preserves the outstanding Box-custody and extraction-threshold blockers. It directly maps roadmap capability IDs from `docs/capabilities.md`, but does not change that file.

## 210. 2ce6f6b2c732af361e110c23880826c298a13521 — 2026-08-02 — fix integration-test drift from the production release

This test-maintenance change adds the newly committed Web migration-history permission migration to the expected schema migration list and updates the operator-journey browser assertion to the current Development-only acceptance-boundary wording. It also advances `NOW.md` from making `main` green to the Box custody decision. `docs/capabilities.md` was not changed.

## 211. d349e65577ab1b260fc0ed62d00ac069860200cf — 2026-08-02 — record deployment reality and the decided Box custody root under one owner

This documentation reconciliation makes `docs/operations.md` the sole current owner of production state, while the Azure deployment plan becomes an immutable execution record and architecture links to operations instead of repeating live claims. It records Box folder `392761581105` as the decided production custody root for all case folders, closes that open decision, and clarifies deployed Worker/Graph, migration, authentication, telemetry and recovery evidence limits. `NOW.md` removes the resolved Box decision, advances extraction thresholds to Doing and records removal of the superseded Box File Request UI; `docs/capabilities.md` was not changed.

## 212. 41d9528c35a53910ef7ef7a3c4a39f8bbab40ac9 — 2026-08-02 — align alpha acceptance scope and evaluator semantics to the decided path

This roadmap alignment makes `docs/capabilities.md` the explicit owner of what blocks `0.1.0-alpha.1` acceptance. It marks selected MCP actions, automatic registration reading, upload links and automatic report-sent detection as allocated but non-blocking, and records recovery proof as post-acceptance work that gates a second release. It also corrects evaluator capability rows and design/operations references to the ADR-0016 desktop tool, which copies reviewed EML into a local tree and writes a JSONL log rather than moving source files; `NOW.md` was not changed.

## 213. c33357df9f3a85c118a7f860def53fa91468aa8c — 2026-08-02 — sweep mechanical drift: routes, CI truth, ADR index, and design records

This consistency sweep corrects documentation to current routes, CI workflow, checkout-path length, Playwright setup and locked restore behaviour. It repairs the Azure deployment-plan verifier after the runbook and retirement scripts were removed, updates the ADR index’s titles and supersession notes, and makes design records match the embedded logo, unused Lucide sprite, implemented styling and actual intake decision labels. It also moves capability outcomes out of the design traceability matrix, adds glossary distinctions for the first EVA-send proxy event, and removes obsolete GitHub work-tracking guidance. `docs/capabilities.md` is changed only to clarify the UI-04 metric wording; `NOW.md` was not changed.

## 214. 752f1301c9de9aeeb9a77078b979870d15fca857 — 2026-08-02 — make the documented Linux test filter true; merge DOC-06 into INT-31

This test-labelling fix adds the `SqlServer` trait to the integration-test classes that share the LocalDB fixture, so the documented Linux filter can genuinely exclude database-dependent tests. It also merges the duplicate `DOC-06` request-scoped upload-link outcome into `INT-31`, retires the old ID permanently and updates all roadmap, requirement, index and traceability totals from 229 to 228 capabilities. `docs/capabilities.md` is directly changed; `NOW.md` was not changed.

## 215. ef377c6d47d56a547030e77a948087a565bd9b36 — 2026-08-02 — fix workspace docs against directory truth; extend the link check to them

This documentation repair extends the Markdown link check to imported workspace documentation while continuing to exclude protected skill-package source. It updates workspace maps, paths, dates, route descriptions, format support statements and historical-link references to match the tracked directories and current root documentation, and records an unresolved imported skill-package reference without altering protected `SKILL.md` files. It also clarifies that workspace manifests describe their import snapshot rather than current file counts. `NOW.md` and `docs/capabilities.md` were not changed.

## 216. 6877d72110048dd28119647639d0529e7018ec11 — 2026-08-02 — remove overtaken reference reports and qualify the vendor EVA notes

This evidence cleanup removes six retained reports whose unqualified present-tense claims had been overtaken by current documentation, preserving their history in Git and recording the removal in the reference index. It adds a clear banner to the raw EVA vendor notes: the accepted integration remains the manual 13-key JSON/image handoff, while direct EVA API use is only an optional later allocation. It also corrects a tracked email-address reference extract that incorrectly called itself uncommitted. `docs/capabilities.md` is referenced for the EVA allocation but not changed; `NOW.md` was not changed.

## 217. c6597d621ced8e49ababc9501152f889affa4ce5 — 2026-08-02 — further repo simplification

This Worker composition fix registers the source reader and `ProcessIntake` in production so queued intake processing and all nine Function types can be created by dependency injection; the composition test now proves that. It also makes genuine-corpus tests skip unless the exact pinned file hashes they assert are present, caches multi-format corpus candidates by extension, and reports missing frozen inputs explicitly instead of selecting a different local file. `NOW.md` and `docs/capabilities.md` were not changed.

## 218. 56e8f92b5acdd596b9388982a80453d0bafe9642 — 2026-08-02 — correct the decided Box custody root to the pegasus folder 405543781910

This correction changes the decided production Box custody root to folder `405543781910` (“pegasus”), while retaining `392761581105` solely as the disposable integration-test target. The source guard, production Bicep setting and custody tests now require the new root; operations and open decisions explicitly record that the deployed application still uses the old root until a separately approved deployment applies the change. `NOW.md` adds that pending deployment work; `docs/capabilities.md` was not changed.

## 219. dc7e6506455f267d1165eb4e3d1324e566eb3bbc — 2026-08-02 — remove the remaining overtaken reference reports and the Project mirror text

This further evidence cleanup deletes six additional reports whose present-tense claims were contradicted by current documentation or deployed behaviour, leaving only the observational reports and retaining history in Git. It revises the EVA notes to describe the previously reviewed but absent Sentry guide as non-authoritative vendor context, and removes the remaining GitHub Project synchronisation/mirror text so `NOW.md` and the capability inventory remain the work-tracking sources. `NOW.md` and `docs/capabilities.md` were not changed.

## 220. 76712cf27b7d75e47134e30dde6c43c558b78739 — 2026-08-02 — settle the review findings as clean current-state statements

This policy and documentation alignment defines an always-image-based Principal as automatically receiving `Image Based Assessment` at Case creation, even if instructions contain a physical location, while allowing authorised staff to override it. It also expands `Needs sorting` to cover held Triage material, corrects design and operations statements about authentication, request-scoped uploads, telemetry and recovery, and records related ADR supersessions. `docs/capabilities.md` is updated to remove repeated `NOW.md` references from non-blocking entries and clarify related qualifications; `NOW.md` itself was not changed.

## 221. 2ed96896237d62f89c4ce2f51e9a1bdf54146d58 — 2026-08-03 — remove the RPO/RTO release gate and point NOW at the composition fix

This decision makes OPS-09 recovery proof deferred and non-blocking for every release, while retaining the recovery procedure as the accepted method for a future exercise. It removes the former second-release gate from architecture, operations, requirements, capabilities, open decisions and the ADR index. `NOW.md` shifts the active work to composing the production staff surface—document content storage, authenticated Intake, a real Triage matcher and an activation test—and makes the resulting deployment, threshold cohort and Linux test/PR work the next steps. `docs/capabilities.md` is directly changed to record OPS-09 as non-blocking.

## 222. c697e43685f51c261e64c3a16f465961e5634e0c — 2026-08-03 — drop the last recovery-gate clause from the delivery order

This small requirements correction removes the final statement that recovery proof gates a second release from the alpha delivery order. The order now consistently treats both Automation MCP and OPS-09 recovery proof as allocated but non-blocking for acceptance. `NOW.md` and `docs/capabilities.md` were not changed.

## 223. 25f9d9ef87ec99b4d0dfc47a2fecd3397e36baf2 — 2026-08-03 — add vault consolidation to the release-2 deployment scope

This planning update expands Release 2 to consolidate the Box, DVLA and DVSA secrets from the two adopted predecessor vaults into the Pegasus Key Vault. It requires repointing Worker references and proving their resolution before retiring the adopted vaults and `rg-collisionspike-dev`; it makes no cloud change. `NOW.md` is updated with this Release 2 scope, while `docs/capabilities.md` is not changed.

## 224. 836db05c94a0084a6b782e6034ffbd11128964ae — 2026-08-03 — merge pull request #313 from collisionengineers/dev

This merge integrates the documentation-truth, custody-root and test-lane changes from the development branch. The resulting tree corrects canonical production and planning documentation, retires overtaken reference reports, changes the intended Box custody root to `405543781910` while preserving the test target, strengthens Worker composition and genuine-corpus test controls, and brings workspace documentation under link checking. It directly includes `NOW.md` and `docs/capabilities.md` updates from the merged branch.

## 225. 067c28f776bef199b697a1a4aaa61efd72a3fd7c — 2026-08-03 — record release 2: Box custody root live on the pegasus folder

This release record states that Release 2 deployed the intended Box root `405543781910` to production through the authorised-terminal route, with a digest-verified Web upload, no infrastructure creates or deletes, a Worker package redeploy and a passing production smoke. The database stage was not run because the migration identity was unchanged. Operations and open decisions now treat the new root as deployed, while `NOW.md` leaves the forwarded-header correction and vault consolidation for the composition-fix release. `docs/capabilities.md` was not changed.

## 226. 044a21f37f5caf5f4a7b360da55aaca5a6b8c2de — 2026-08-03 — record provider-determined inspection mode decision in ADR 0017 (CASE-29, EXT-18)

This accepted decision changes inspection mode from a document-derived rule to a persisted Principal setting. QDOS is seeded as image-based, so new QDOS Cases automatically receive the exact `Image Based Assessment` value with provider-setting provenance; authorised staff can make a reasoned per-Case override, while physical-address Principals retain the evidence-first flow. ADR-0017 also defines schema, replay and history consequences and keeps the address reference-data pipeline separate. `docs/capabilities.md` directly updates CASE-29 and EXT-18; `NOW.md` was not changed.

## 227. 482657d786aa6de5a2b0520a881c9eff49dd9b82 — 2026-08-03 — add per-principal inspection-mode setting with QDOS seed (CASE-29)

This implementation adds the Principal inspection-mode field to Core, persistence and the administrator create/list screens, with physical address as the default and validation for the two permitted values. The migration adds the SQL constraint, permits `provider_setting` provenance, and seeds QDOS as `image_based_assessment`; replacement Principals inherit the predecessor’s setting and the Development fixture is seeded the same way. An integration migration test verifies QDOS’s seed, the default for other Principals and both database constraints. `NOW.md` and `docs/capabilities.md` were not changed.

## 228. e13e3b9c124bccd2f8ac14ffadd7ed7b5293455f — 2026-08-03 — autofill Image Based Assessment at case creation for image-based providers (CASE-29, EXT-18)

This acceptance-path implementation reads the active Principal’s inspection mode before creating a Case. For image-based providers it replaces any confirmed intake address with `Image Based Assessment`, preserves a physical extraction as a suggestion, records provider-setting provenance and a case-history event, and permits later reasoned Case overrides. It adds a persistence store and dependency registrations, puts the resolved mode into schema-version-4 acceptance command material, and rejects acceptance or replay if the Principal’s setting changes mid-flow. New tests cover autofill, override and restoration, provenance, and replay conflict. `NOW.md` and `docs/capabilities.md` were not changed.

## 229. 1f19ec26d642049c865fed6c630dfd7f367948e4 — 2026-08-03 — relax intake acceptance gate for image-based providers (EXT-18)

This Intake-page change permits acceptance without an inspection-address resolution only when the selected Principal is image-based. The detail page tells staff that Case creation will automatically record `Image Based Assessment`, while retaining later reasoned Case correction; physical-address providers still require an accepted or corrected address. The page model looks up the Principal mode both when displaying and validating the form, so a posted Principal cannot bypass the physical-address gate. `NOW.md` and `docs/capabilities.md` were not changed.

## 230. 31fc73072fd3e7a8fce7b67dbcaca65dffb11c3b — 2026-08-03 — standardize Image Based Assessment labels and case override helper text (CASE-29)

This UI and test change standardises the Case workflow option to the exact staff-facing phrase `Image Based Assessment` and explains that selecting it requires the inspection-address value to match exactly. It adds Core unit tests proving address and mode must be saved together, image-based mode rejects any other address value, and the phrase cannot be used as a physical address. `NOW.md` and `docs/capabilities.md` were not changed.

## 231. 00d05817e59be8aef524d4cbf3741ebbc496882f — 2026-08-03 — update operations notes and NOW for provider inspection mode (CASE-29)

This operations update documents the persisted per-Principal inspection-mode setting, its QDOS seed and successor inheritance, and supplies the authorised production-database procedure for changing an existing Principal. It clarifies that a mode change affects only later acceptances and causes in-flight or replayed acceptance across the change to fail safely. It also updates the operational policy so `Image Based Assessment` is autofilled only from the accepted Principal setting with recorded provenance, while physical-address Principals still need confirmed evidence. `NOW.md` is directly updated to show the inspection-mode pull request awaiting review and merge authority; `docs/capabilities.md` was not changed.

## 232. 4b43b0d7f0badb97e307630fcbfb149d1f2d8d22 — 2026-08-03 — record the inspection-mode migration in the committed schema inventory (CASE-29)

This test-only update adds the provider-inspection-mode migration to the integration test's expected applied-migration inventory, so the committed schema check confirms that a newly initialised database includes it and has no pending migrations. `NOW.md` and `docs/capabilities.md` were not changed.

## 233. 485226276d4eceda316ce0a3704a9b6b0a67b632 — 2026-08-03 — compose the production staff surface on Box-backed custody

This production-composition change makes the Web and Worker use one explicit storage profile: Azure Blob storage for intake artefacts and the approved, root-fenced Box location for case custody and managed document content. It introduces a Core document-content boundary, adds a Box implementation that verifies content hashes and sizes, and refactors the shared Box client so custody and document work apply the same descendant, duplicate and trashed-item safeguards. It makes the production Intake review pages and document/EVA services available while keeping manual local upload, upload links without accepted limits, and automatic Triage matching inactive; it also requires Box settings at production startup, configures the Web container app to reference the Box secrets through its managed identity, handles forwarded HTTPS headers before redirects, and fixes a Linux project-reference parsing test. New composition and access tests prove registrations and fail-closed conditions without contacting Box, Graph or Azure. `NOW.md` is directly updated to move this work to PR/merge and deployment follow-up; `docs/capabilities.md` was not changed.

## 234. 7144fb8e88e1c16a637f97bd350b53db45ec2d86 — 2026-08-03 — adopt the multi-agent task workflow: claims on dev, task worktrees, temporary plans

This workflow change adopts a formal parallel-work process: a task is claimed by a `NOW.md`-only push to `dev`, implemented in its own `task/<slug>` worktree, planned in a temporary plan file, independently reviewed against that plan, and merged into `dev` after green or path-skipped CI; only `dev` to `main` still requires explicit operator merge authority. It records the decision in ADR-0017, updates repository and documentation authority guidance, creates the temporary-plan contract and bootstrap plan, adds stale-claim handling, clarifies allowed Git operations that protect other agents' work, and ignores `.claude/`. CI now detects Markdown-only changes, still checks links, and skips build/test and pressure work for those changes; ancillary formatting and link-check rules are adjusted accordingly. `NOW.md` is directly restructured into the multi-agent queue; `docs/capabilities.md` was not changed.

## 235. d938610a09380d1a6fa75b0dc641943b229b96a1 — 2026-08-03 — address plan review: three-dot docs-only diff, queue the matrix follow-up

This follow-up corrects the Markdown-only CI classifier to compare a pull request from its merge base (`base...head`), so the result reflects changes introduced by the branch rather than unrelated changes on its target branch. It also adds a queued task to consolidate the product traceability matrix into the design README and use that Markdown-only pull request to demonstrate the CI path skip. `NOW.md` is directly updated; `docs/capabilities.md` was not changed.

## 236. a117b985e4092ec00cb5a00beea87f3b93f6b985 — 2026-08-03 — gate CI build lanes on a build-relevant allowlist; fix claim retry and abandonment

This review correction changes CI from a Markdown-only exception to a build-relevant path allowlist: application source, tests, solution and project/configuration files, package locks, and the pressure-test script run the Windows build/test and pressure lanes, while every other change still receives the documentation link check. It documents that the Linux `changes` job only inspects paths and is not Linux application evidence. The task workflow now tells an agent whose claim push is rejected, or who abandons a task, to reset its own task worktree to fresh `origin/dev` before making the required claim-line change, avoiding a stale-tip push; the temporary plan is left on the abandoned task branch to disappear with it. `NOW.md` and `docs/capabilities.md` were not changed.

## 237. e10a481193e26013096a007e1e8da122ceb347d5 — 2026-08-03 — merge pull request #317: adopt the multi-agent task workflow

This merge brings the reviewed multi-agent workflow branch into its target, including the `NOW.md` task-claim queue, temporary task plans, ADR-0017, the task-to-`dev`-to-`main` delivery rules, and the CI path classifier. The resulting workflow uses the corrected build-relevant allowlist and fresh-`origin/dev` claim retry and abandonment steps, while retaining link checking for every change set. `NOW.md` is directly included by the merge; `docs/capabilities.md` was not changed.

## 238. f49301c4ee1ae9776d3fdec75ea2e0cb071a45a8 — 2026-08-03 — maintenance: delete merged task plan agent-workflow-guidance

This maintenance commit deletes the now-obsolete temporary plan for the merged multi-agent workflow task, following the workflow's rule that task plans are transient and removed after merge. `NOW.md` and `docs/capabilities.md` were not changed.

## 239. a4fa41d4ce6f79ccf04e66a2a6d1744c8db93eac — 2026-08-03 — claim: delete traceability matrix

This claim-only commit moves the task to remove the duplicated product traceability matrix into `NOW.md`'s active queue on branch `task/delete-traceability-matrix`. The stated work is to delete the matrix and redirect its inbound links because the capabilities and design documentation already own the rules it repeats, and to use the Markdown-only pull request to exercise the documentation-only CI path. `NOW.md` is directly updated; `docs/capabilities.md` was not changed.

## 240. 595dccd4d6b8bd170ef437e98ed88207768af0d6 — 2026-08-03 — delete the traceability matrix; capability inventory is the sole allocation owner

This documentation cleanup deletes the 228-row product traceability matrix rather than replacing it. It redirects the matrix's design, UI-specification and product-requirements links to their actual owners: the capability inventory for per-capability allocation and activation boundaries, and the design authority for UI principles and the recorded Operations-first choice. It releases the active task claim and adds the task's temporary plan, which defines the deletion and link-check/CI evidence expected for the pull request. `NOW.md` is directly updated; `docs/capabilities.md` is explicitly identified as the sole allocation owner but its contents were not changed.

## 241. 77f37321cf4140d0817dd723807f13abab464dc8 — 2026-08-03 — address review: name the rejected-alternatives section precisely

This wording correction makes the product requirements link identify both relevant parts of the design authority: the selected product direction and the retained rejected alternatives evidence. It does not change the chosen design or the preserved mockup evidence. `NOW.md` and `docs/capabilities.md` were not changed.

## 242. 98f6e57582574ccaaaeaf3ad29d8de6753720c72 — 2026-08-03 — merge pull request #318: delete the traceability matrix

This merge applies the traceability-matrix removal branch, deleting the redundant design matrix, repointing its links to the capability inventory and design authority, releasing the task claim, and carrying the temporary task plan into the merged tree. `NOW.md` is directly included by the merge; `docs/capabilities.md` remains the stated allocation owner but was not changed.

## 243. 9a76ec10a04cade0f6cbdd59e627061213e481d4 — 2026-08-03 — maintenance: delete merged task plan delete-traceability-matrix

This maintenance commit removes the temporary deletion-task plan after its branch was merged, keeping temporary plans out of the durable documentation set. `NOW.md` and `docs/capabilities.md` were not changed.

## 244. 51fa8f0280997d0af30ff36b631fad59ca3723d1 — 2026-08-03 — merge origin/dev into the release-2-record branch

This branch-maintenance merge brings the multi-agent workflow and traceability-matrix cleanup from `origin/dev` into the release-record branch, resolving the concurrent `NOW.md` change. The resulting branch gains the task workflow and CI path rules, temporary-plan framework, updated repository guidance, and removal of the redundant matrix with its links redirected. `NOW.md` is directly included by the merge; `docs/capabilities.md` was not changed.

## 245. f2f807951b7ef049791b3b6a8662d6710e8ae546 — 2026-08-03 — merge pull request #314: record release 2, Box custody root live on the pegasus folder

This merge applies the Release 2 record to the development line. The resulting operational documentation treats Box folder `405543781910` as both the decided and deployed production custody root, records the Web image/revision, redeployed Worker, healthy revision and passing production smoke, and retains the earlier Release 1 Graph verification as historical evidence. `NOW.md` and `docs/capabilities.md` were not changed.

## 246. 6be0d1edb30f9da499119862e3fca70fcb70d225 — 2026-08-03 — merge origin/dev into the provider-inspection-mode branch

This branch-maintenance merge incorporates the development line into the provider-inspection-mode feature and resolves numbering collisions by retaining the new task-workflow decision as ADR-0017 while renumbering the inspection-mode decision and its references to ADR-0018. It carries in the multi-agent workflow, traceability-matrix cleanup and Release 2 record, while preserving the provider setting's rule that it selects inspection mode rather than inferring an address. `NOW.md` is directly included by the merge, and `docs/capabilities.md` is directly changed to update CASE-29, EXT-18 and DATA-02 to ADR-0018.

## 247. 0f6a431de5b73ced46a28f19b7043b0fca43029e — 2026-08-03 — cover the same-transaction inspection-mode guard with a store-level test (CASE-29)

This integration test adds direct coverage for the acceptance transaction's protection against a Principal inspection-mode change during intake acceptance. It supplies an acceptance resolved as physical-address for QDOS, whose stored setting is image-based, and verifies that the persistence store rejects the stale request with the inspection-mode-change error. `NOW.md` and `docs/capabilities.md` were not changed.

## 248. d3e8f3bd334ea763c2031c0ec3c633831cedc395 — 2026-08-03 — address review: page Box child lookups, prove the deployed composition and content store

This correction makes Box child searches fetch subsequent pages rather than stopping at Box's 1,000-item limit, preventing a growing flat case folder from making later children appear absent. It adds in-memory Box tests for the managed document layout, hash and length validation, replay handling, idempotent deletion and second-page lookup, and adds a real Web-host composition test proving the production profile resolves Box custody plus document and EVA services. These tests make no external Box, Graph or Azure call. `NOW.md` and `docs/capabilities.md` were not changed.

## 249. e9a74adc7b9ae6b3ec03ec61ce0ce8a8600848b1 — 2026-08-03 — claim: image-led intake (INT-13/27/29/30, UI-07, open decision 1)

This claim-only commit starts an image-led intake task on `task/image-led-intake`. The task covers a pre-Case image intake record with manual registration entry, Case linking and relinking, origin preservation and reference search, plus evidence research for the unresolved VRM-recognition decision; it explicitly excludes vendor selection, credentials and automatic matching without operator approval. `NOW.md` is directly updated; `docs/capabilities.md` was not changed.

## 250. 5a6832f48dbbc70ea38bb7faac72976f1bc9da47 — 2026-08-03 — plan: image-led intake domain and open-decision-1 research

This temporary implementation plan defines a separate Image Intake domain for retained image evidence: manual VRM confirmation, non-reused `VRM-sequence` references, persisted origin and version history, and reasoned manual Case link, unlink and relink before report delivery. It plans Core, EF, migration, dependency-injection and staff-page changes, with lifecycle, allocation, concurrency and Web verification. Separately, it scopes evidence-backed research into VRM-recognition options for open decision 1, explicitly leaving vendor selection, credentials, automated recognition and automated matching for later operator-approved work. `NOW.md` and `docs/capabilities.md` were not changed.

## 251. c52ee67fc02fce3558c15c33a0d6d2d697bf8762 — 2026-08-03 — add CLAUDE.md as a symlink to AGENTS.md

This repository-configuration change adds `CLAUDE.md` as a symbolic link to `AGENTS.md`, so Claude-oriented tooling reads the same repository instructions rather than a duplicated copy. `NOW.md` and `docs/capabilities.md` were not changed.

## 252. 8bc8bb6089cf4469d39a6897abdd7300621bd313 — 2026-08-03 — queue the repository-check speed task

This planning update adds a queued task to reduce repository-check duration by splitting validation into parallel unit, SQL-integration and browser lanes, reusing a migrated LocalDB template per run, and caching NuGet packages and the pinned Playwright browser. `NOW.md` is directly updated; `docs/capabilities.md` was not changed.

## 253. daea8c053dfa6e7b33fc1a37e260d8551c220d18 — 2026-08-03 — claim: QDOS email identification and classification (MAIL-21/22)

This claim-only commit starts a shared Core email-classification task on `task/qdos-email-classification`. It scopes the received/sent families and subtypes, mirrored reply context, validated `Other` details, versioned policy and decision evidence, an explicit ambiguity outcome and acceptance cohort, while explicitly excluding queue/Triage/Outlook routing, mailbox mutation, AI classification, evaluator work and invented precedence or confidence thresholds. `NOW.md` is directly updated; `docs/capabilities.md` was not changed.

## 254. 4c4993ac4785482ca0fc2d240efe9a65a959b879 — 2026-08-03 — plan MAIL-21/22 QDOS email identification and classification

This temporary plan specifies the shared Core classification foundation for the settled eight received and four sent families, including mirrored Reply context, reasoned `Other`, versioned policy evidence and append-only corrections. It requires ambiguity to be recorded and fail closed without inventing precedence or confidence thresholds, keeps classification distinct from mail routing, queues, Triage and Outlook destinations, and defines taxonomy, ambiguity, separation, correction and cohort checks. It also records an unrelated evaluator defect—the deleted catalog path—as out of scope for this task. `NOW.md` and `docs/capabilities.md` were not changed.

## 255. e84097dae58dc8c80edbb9c4a5e41e834f7e074e — 2026-08-03 — record INT-17 recognition-engine evidence in open decision 1

This research update adds evidence for the still-unselected vehicle-registration recognition route. It compares a static, in-process ONNX option; United States-hosted ANPR services; Azure AI Vision Read in UK South; and an on-premises licensed container, recording their licensing, processing, retention, authentication, cost and deployment implications. It records that the local immutable corpus can support a genuine cohort and holdout but still needs reviewed plate-legibility truth, and keeps selection and activation gated on operator decision, measured cohort results and any required boundary changes. `NOW.md` and `docs/capabilities.md` were not changed.

## 256. 9a1a59c7c3a5849cd5dc12dfabf4cabf658da110 — 2026-08-03 — claim: cut repository-check wall clock

This claim-only commit moves the repository-check performance task into the active queue on `task/repository-check-speed`. Its scope is parallel validation lanes, a per-run migrated LocalDB template in place of per-test migration, and caches for NuGet and Playwright Chromium. `NOW.md` is directly updated; `docs/capabilities.md` was not changed.

## 257. b165860de52f9a913ea263320d0024f4f4ea2e39 — 2026-08-03 — accept ADR-0018: in-process ONNX VRM recognition engine (INT-17)

This accepted decision selects hash-pinned, vendored fast-alpr plate-detection and fast-plate-ocr recognition ONNX model bytes running through `Microsoft.ML.OnnxRuntime`, with no Python service, container, runtime download, image egress or new deployment unit. The future engine returns source-bound candidate registrations or abstains; staff must confirm every suggestion, and it cannot accept records or match them automatically. The remaining open question is the accuracy and abstention threshold from a reviewed genuine cohort and untouched holdout, with wrong suggestions prioritised over abstentions. `docs/capabilities.md` is directly updated to record the selected engine for INT-17; `NOW.md` was not changed.

## 258. 4ed2c6757cf7cc463fdafd11758bf0869b3823c0 — 2026-08-03 — add Core Image Intake contracts and lifecycle (INT-13, INT-27, INT-29, INT-30)

This Core change introduces a durable, pre-Case Image Intake record with its retained-source origin, normalised VRM, permanent expanding `VRM-sequence` reference, optional current Case link and version. It defines staff-authorised registration, queries, linking and unlinking ports, replay and version-conflict semantics, and append-only history. The lifecycle validates source identity and hash, requires reasons and an active Case edit lease, permits only one current Case association, and limits associations to eligible pre-report Cases without report-sent evidence. `NOW.md` and `docs/capabilities.md` were not changed.

## 259. a84196184d1f7d732969a4fc445645c7557da9eb — 2026-08-03 — merge origin/dev into the production-composition-fix branch

This branch-maintenance merge brings the development line into the production-composition worktree, resolving concurrent `NOW.md` and operations-document changes. It incorporates the multi-agent task workflow, CI rules, shared Claude instructions, traceability-matrix removal and Release 2 record, while preserving the production-composition branch's Box managed-document-layout question and the expected Web-and-Worker custody composition after deployment. `NOW.md` is directly updated by the merge; `docs/capabilities.md` was not changed.

## 260. 0c0709fc4b8647c7ea3555a70f0d2624b86e5c5b — 2026-08-03 — plan the repository-check wall-clock cut

This detailed temporary plan targets the long integration-test setup cost with a once-per-run migrated LocalDB template restored through server-side backup/restore, while retaining an explicit fallback and tests that prove the template is structurally and migration-equivalent to a fresh database. It defines separate Windows documentation, unit, SQL-integration and browser CI lanes whose filters must sum to the existing suite, plus locked NuGet and Playwright caching. It preserves the canonical local command, records measurements and CI evidence required to prove no tests were lost, and sets stop conditions for template drift, cache safety and unsafe parallelisation. `NOW.md` and `docs/capabilities.md` were not changed.

## 261. 9351e2bf6888b261df0539fe753b679b9ef278cf — 2026-08-03 — merge pull request #315: provider-determined inspection mode with Image Based Assessment autofill (CASE-29, EXT-18)

This merge brings the provider-inspection-mode feature into the development line: a Principal setting and QDOS seed, a schema migration and persistence checks, case-creation autofill with provider-setting provenance, reasoned per-Case correction, and an Intake rule that lets image-based providers proceed without a physical address. It includes administrator presentation, fail-closed replay and mid-flight-change protection, staff wording and Core/integration coverage, alongside ADR-0018 and the associated operations and product clarification. `docs/capabilities.md` is directly updated for CASE-29, EXT-18 and DATA-02; `NOW.md` was not changed.

## 262. 2d727509a3d907244fd611af73df06904943484b — 2026-08-03 — record QDOS sender-domain inventory and accepted additions

This supporting research document records that the provider source groups `qdosassist.co.uk`, `qdoslaw.co.uk` and `qdosassists.co.uk` under QDOS, and that the operator accepted the latter two as additions to the QDOS route. It specifies exact whole-domain matching, a mail-route policy-version increment and tests that reject subdomains and non-QDOS domains; it also inventories other provider domains without activating their routes. The classification plan is linked to this evidence and notes the required open-decision correction. `NOW.md` and `docs/capabilities.md` were not changed.

## 263. a25725eba1607652454aaa2b2e99a475f57233a1 — 2026-08-03 — record provider-domains-v1 liveness and PCH intermediary constraint

This evidence update confirms that all three accepted QDOS domains were already present in the shipped, migration-seeded provider-domain package, but that no production caller consumes that catalog and the actual route policy still hardcodes only one domain. It therefore keeps Core as the explicit, versioned authority for the accepted QDOS set while requiring a test to prevent drift from the reference package. It also documents that the flat package cannot distinguish PCH direct and intermediary routes, so it cannot become route authority and that distinction remains future INT-04/ADR-0008 work. `NOW.md` and `docs/capabilities.md` were not changed.

## 264. 6a74aa4cc12f779707173a66ab9c17b8ea9ad4cb — 2026-08-03 — claim: documentation accuracy pass

This claim-only commit starts a documentation-correction task on `task/docs-accuracy-pass` after the Release 2, inspection-mode and composition merges. The stated scope is to correct stale architecture claims about production Intake access and Worker-only adapters, premature operations wording about Key Vault references, and the CI allowlist wording in engineering guidance. `NOW.md` is directly updated; `docs/capabilities.md` was not changed.

## 265. 28311b99f2b12be25f489ad456db6cdfbac25048 — 2026-08-03 — replace task plan with the full grounded implementation plan

This revised Image Intake plan incorporates the accepted ADR-0018 mechanism and expands scope from research to implementing the in-process ONNX VRM-recognition engine, while keeping threshold acceptance open. It provides a concrete sequence for extra Core queries, transactional EF persistence and migration, Intake, Image Intake and Case pages, reasoned manual association, source-bound persisted suggestions, embedded hash-verified models, and a local-only genuine-corpus evaluation harness. It explicitly preserves no automatic association, no image egress, no Case allocation for Image Intakes, no pipeline decision change and no corpus data in version control, with defined unit, integration, Web and CI evidence. `NOW.md` and `docs/capabilities.md` were not changed.

## 266. aed3677b40716f9ab28af62a62bc46071fac0cba — 2026-08-03 — correct stale architecture and engineering claims after the composition merge

This documentation correction states that staff Intake pages are served wherever Intake is composed, including production, while only the manual upload handler remains DevelopmentOffline-only. It corrects adapter placement to both Web and Worker composition roots, records Web Box-backed custody and document/EVA composition as merged implementation rather than deployment, and removes stale Development-only labels. It also clarifies that the CI build-path allowlist names the one acceptance script and deliberately excludes the documentation-link script because that always runs. The temporary plan records the audited findings and no code or CI logic changes. `NOW.md` and `docs/capabilities.md` were not changed.

## 267. 437a9f419dc6797013c6a5edd7e3a63c38d12fdd — 2026-08-03 — plan the future split of the QDOS policy into generic and QDOS parts

This unclaimed future-task proposal identifies that the current QDOS extraction policy both identifies a provider and extracts generally useful instruction fields. It recommends separating route selection from generic parsing while retaining a small QDOS-specific policy, stable historical policy keys and versions, and existing behaviour tests. The planned refactor is deliberately separate from email classification and must precede second-provider activation without inventing PCH rules or building a general rule engine. `NOW.md` and `docs/capabilities.md` were not changed.

## 268. 03f77ce1d3c0c5f4e4e37bc56e9c59a54bfdfa2e — 2026-08-03 — merge pull request #316: compose the production staff surface on Box-backed custody

This merge applies the production-composition feature to the development line, including the shared Azure Blob and root-fenced Box storage profile, managed document-content port and Box implementation, production staff Intake/document/EVA composition, forwarded-header handling and Web Box secret references. It retains inactive automatic Triage matching and upload links without accepted limits, and brings the Box paging plus host- and content-store test coverage. `NOW.md` is directly updated to make release, vault and layout work the follow-up; `docs/capabilities.md` was not changed.

## 269. 0b19245ea5e0c98f4c572de4423699fdbc55d96f — 2026-08-03 — merge origin/dev into the documentation-accuracy task branch

This branch-maintenance merge brings the production-composition feature into the documentation-accuracy task branch. It incorporates the Box-backed staff surface, required production secret references, associated tests, managed-document-layout question and inactive Triage-matcher decision, and updates the branch's `NOW.md` baseline to the post-composition follow-up queue. `NOW.md` is directly included by the merge; `docs/capabilities.md` was not changed.

## 270. 63a9ab38f98ce75d6bb1ebcc8a9309db26e54566 — 2026-08-03 — reword Key Vault reference claims to the composition-fix release; release claim

This documentation correction removes the completed accuracy-pass claim and clarifies that the Web container app's Box Key Vault secret references belong to the future composition-fix deployment, rather than already being live. The release queue now asks for the Web identity grant for the two secrets referenced by that release, alongside vault consolidation. `NOW.md` is directly updated; `docs/capabilities.md` was not changed.

## 271. 587f36b12da51d14265e143a03f1c7b4a5d259a8 — 2026-08-03 — review follow-up: fix the same stale 404 claim in the extraction workspace document

This follow-up aligns the document-extraction workspace architecture note with the production-composition behaviour: only the manual local `ReceiveIntake` handler is unavailable without the DevelopmentOffline and local-intake gates, while staff Intake routes are served wherever Intake is composed. It also reformats the queued release line without changing its meaning. `NOW.md` is directly updated; `docs/capabilities.md` was not changed.

## 272. 45a93eae41f8c9e6609d8f724d39e8b898134c77 — 2026-08-03 — merge pull request #319: documentation accuracy pass after the Release 2, inspection-mode and composition merges

This merge applies the accuracy-pass corrections: documentation distinguishes staff Intake pages from the Development-only manual upload handler, places production adapters at both Web and Worker roots, and classifies the merged Web storage composition as implemented rather than deployed. It also corrects the secret-reference timing, makes the CI allowlist wording exact, aligns the extraction workspace note, removes the completed claim and retains the task's temporary plan. `NOW.md` is directly updated; `docs/capabilities.md` was not changed.

## 273. 5e269169d45b4dd7d9b5cb78f1d653a1a47b8246 — 2026-08-03 — maintenance: delete merged task plan docs-accuracy-pass

This maintenance commit deletes the temporary accuracy-pass plan after its task merged, as required by the transient-plan workflow. `NOW.md` and `docs/capabilities.md` were not changed.

## 274. 035dd6416b5fe893993f19da2bc433b71fc98785 — 2026-08-03 — record QDOS email tells found in 329 genuine emails

This local, read-only corpus analysis records that QDOS subjects commonly contain a durable claim reference, a templated `EREF` code, incident details and reply/forward context. It identifies the claim reference as the reliable new-versus-seen-work key while separating that fact from Pegasus Case association, and records evidence that EREF describes message type rather than case stage. The meanings of EREF codes, including any Audit identification, remain unconfirmed for operator review; sender and attachment patterns are only supporting signals. `NOW.md` and `docs/capabilities.md` were not changed.

## 275. a0ac901a4c84f2244c7ceb6163c33fc8608394d1 — 2026-08-03 — flesh out the plan with the grilled operator decisions

This revised Image Intake plan records new operator decisions that substantially change the intended implementation: Image Intakes are only for image-only receipts, registration becomes a real Intake decision, and Case association uses the existing receipt association rather than a competing Image Intake link. It plans automatic in-pipeline scanning, four explicit recognition outcomes, automatic registration at a provisional confidence bar and automatic association only for one eligible uncontradicted matching Case; all other results retain staff registration, review and reversal paths. It also plans the required documentation changes, including bringing INT-28 and INT-32 into current scope, while threshold acceptance remains open. `NOW.md` and `docs/capabilities.md` were not changed by this planning commit.

## 276. 0af53cd99028abf34aa71105ccea141f2e095145 — 2026-08-03 — cut repository-check wall clock with sharded lanes, a template database, and caches

This CI and test-infrastructure change replaces the single validation job with always-on documentation checking plus parallel unit, three-way SQL-integration, coverage-verification and browser lanes, while retaining the pressure lane. A shared composite action performs locked restore and Release build with NuGet caching; the browser lane caches but always verifies the Playwright installation. A fail-closed sharding script enumerates tests, assigns whole classes, checks executed counts and proves the shards cover the selected tests exactly once. Integration tests now build one migrated LocalDB template per process and restore disposable databases from its server-side backup, with a migration fallback that is exposed by structural-equivalence and isolation tests; unmigrated-database tests remain untouched. The unit projects gain lock files so locked restore covers the whole solution, and operations/engineering documentation records the lane model, evidence boundaries and measured local improvement. `NOW.md` is directly updated to release the speed task; `docs/capabilities.md` was not changed.

## 277. 7ed895a003ac636350347977a6acfe81d76aa42d — 2026-08-03 — record the Audit tell: instruction-letter attachment names

This corpus-evidence update identifies two disjoint generated attachment-name patterns that distinguish standalone Audit instructions from Inspection instructions across the analysed QDOS emails. It corrects the earlier assumption that EREF codes make that distinction, records that body mentions of “audit” are mainly existing-case chasers rather than new instructions, and elevates attachment names behind claim references in the proposed evidence order. Filename stability, Inspection-plus-Audit timing and the EREF mapping remain operator questions; no mail rule is activated. `NOW.md` and `docs/capabilities.md` were not changed.

## 278. 4343db77a732eaa2b7a7ee4b13c319c320e9e739 — 2026-08-03 — merge origin/dev into the repository-check-speed task branch

This branch-maintenance merge incorporates the development line into the repository-check-speed branch, resolving the CI workflow and engineering-guide conflicts so the new sharded-lane configuration retains the exact build-path explanation. It brings in the provider-inspection-mode feature, production Box composition, their documentation corrections and associated tests, while preserving the speed branch's CI implementation. `NOW.md` is directly included by the merge; `docs/capabilities.md` is directly included through the provider-inspection-mode roadmap updates.

## 279. 2db8a5bb6e0fe3b0455764cb57b92fe8e47e9d56 — 2026-08-03 — record instruction-letter contents and corpus ground truth

This read-only corpus evidence adds direct inspection of the instruction-letter documents behind the filename patterns. It finds that all examined letters have an explicit title agreeing with the filename: standalone Audit or combined Inspection plus Audit; no plain-Inspection letter appears in the sample. It also records that Audit letters are PDFs while combined instructions are legacy DOC files, exposing a possible dependency on the deferred legacy-DOC extraction capability. Finally, it documents that existing human-filed corpus folder references provide useful—but not email-level—case-type ground truth for a future acceptance cohort, with a worked audit-chaser counterexample. `NOW.md` and `docs/capabilities.md` were not changed.

## 280. 240292bee7f64a5a61a479159de4e7103c162eab — 2026-08-03 — record the post-merge lane counts in the plan

This plan correction distinguishes the original speed measurements from the later merged-code baseline and records the updated split: 309 SQL-lane tests plus 14 browser tests equals the 323 non-corpus integration tests after merging `origin/dev`. It does not change the CI implementation or test selection. `NOW.md` and `docs/capabilities.md` were not changed.

## 281. ae6f0c2d5076592eb4f703c653b47c89ad1ec58b — 2026-08-03 — build the automatic image-intake core, persistence, and ONNX engine

This adds the automatic Image Intake path for image-only receipts. It records a recognition outcome for each retained image, including unreadable, technical-failure, and unavailable outcomes; automatically registers a unique high-confidence vehicle registration; and only auto-links the receipt when exactly one eligible case matches, while recoverable recognition or association failures leave the receipt for sorting rather than stopping intake. The registration is immutable and derives its case association solely from the originating receipt, with replay protection, non-reused per-registration reference allocation, persisted suggestion dispositions, and eligibility checks for later manual or automatic linking. It also adds an in-process plate-recognition engine that decodes supplied image bytes locally, verifies embedded ONNX models against pinned SHA-256 values before use, and abstains instead of guessing when it cannot initialise or read a plate; the required model resources and ONNX/Skia dependencies are embedded and registered in application composition. `NOW.md` and `docs/capabilities.md` were not changed.

## 282. 1f78790037f786d3c9914e786259943df98b2790 — 2026-08-03 — add the image-intake migration and operator pages

This makes the Image Intake work usable through the database and staff web interface. It adds the migration and runtime-role grants for immutable intake registrations, per-registration sequences and per-image recognition suggestions, while denying deletion. Staff can now browse and search Image Intakes by their own reference or vehicle registration, see their stored recognition results and eligible case candidates, register a qualifying image-only receipt using a suggested or entered registration, and dismiss a suggestion with a reason. The existing intake and case pages now show the related Image Intake and its live association status, and the case search adds an Images filter that presents Image Intake results alongside—or instead of—ordinary case searches without treating an Image Intake reference as a Case reference. `NOW.md` and `docs/capabilities.md` were not changed.

## 283. ff0ccfc7588130f88683579eecf7ad3abda72926 — 2026-08-03 — record CollisionSpike email-logic findings as predecessor evidence

This adds a read-only evidence note about the predecessor application's email identification and QDOS classification behaviour; it does not reuse that application’s code or make it a Pegasus specification. The note identifies concrete failure modes and design lessons: a flat taxonomy that needed special-case rules, unused confidence scores, unexploited subject and filename signals, instruction letters that cannot distinguish repairable from total-loss work, an incorrectly minted case from an email quoting the firm’s own report, forwarded-sender handling, legacy `.doc` parse ordering, and known vehicle-registration false positives. It maps those findings to Pegasus’s existing fail-closed requirements and open decisions, preserving them as potential test cases and questions rather than closing any decision. This relates to `NOW.md`’s MAIL-21 and MAIL-22 work; `NOW.md` itself and `docs/capabilities.md` were not changed.

## 284. ef987ac49cb4819cddcc605bc25c93a5d2c6f196 — 2026-08-03 — merge release 3: inspection mode, production composition, and task workflow

This merge brings the Release 3 work from `dev` into `main`. It establishes the multi-agent claim-by-push workflow and updates `NOW.md` to track concurrent claimed tasks, then adds a persisted provider inspection-mode setting: image-based providers such as QDOS automatically receive the exact `Image Based Assessment` value when a case is created, while physical-address providers retain the evidence-first flow and staff can make reasoned case-specific overrides. The release also composes the production staff surface with Box-backed document custody, protects the still-deferred upload-link paths, restores production access to intake review actions while keeping manual upload development-only, and honours forwarded headers so production HTTPS redirects and sign-in callbacks use the original scheme. It includes the related migration, custody/content-store work, case data and acceptance changes, CI/workflow and documentation updates, and focused tests. This changes `NOW.md`; it also updates `docs/capabilities.md` for CASE-29, EXT-18, and DATA-02 to describe the provider inspection-mode decision and its boundary from address-reference data.

## 285. 52dbef3cf7ccbbc603073d79750e2a7a3b68b46e — 2026-08-03 — add image-intake tests and run automation after evaluation persistence

This moves automatic Image Intake processing from the initial intake command into the queued worker, after the evaluation revision has been committed, so a registration can reliably retain that committed revision as its origin. The automation remains advisory: recoverable failures do not undo a completed receipt. It adds focused unit, persistence, web-journey, and engine-contract tests covering reference formatting and allocation, registration replay and eligibility rules, high-confidence automatic registration and unambiguous auto-linking, abstention for low or conflicting reads, known unavailable/corrupt/plate-free outcomes, staff registration and suggestion handling, and the places where a registered Image Intake is shown. The engine tests deliberately verify loading, hash pinning and abstention rather than recognition accuracy because the repository contains no genuine plate-bearing fixture. `NOW.md` and `docs/capabilities.md` were not changed.

## 286. 0c4cb30ad90fed7fb199c13a2d262a8680784210 — 2026-08-03 — record the first local VRM corpus evaluation and operator-directed amendments

This adds a local-only corpus evaluation test for the ONNX registration-reading engine. It derives labels from existing case-export filenames, makes a deterministic 80/20 cohort/holdout split, can bound a run explicitly, writes a local report under `artifacts/`, and keeps the holdout untouched unless separately requested. The first bounded run over 400 cohort images is recorded in the open-decision evidence: at the provisional 0.80 confidence bar it suggested 37 registrations, of which three did not match the case-level label, while abstaining for most images; the note also preserves the limitation that a third-party plate in a multi-vehicle photo can be counted as wrong. Following operator direction, the requirements and capability roadmap now allow a confident unambiguous read at the provisional bar to automatically register and associate an Image Intake under the stated safeguards, while acceptance of a final threshold remains open. This updates `docs/capabilities.md` for INT-17, INT-28, and INT-32; `NOW.md` was not changed.

## 287. a93b970f026f43679a0d6da148a17283cd181429 — 2026-08-03 — merge the updated dev branch into the image-intake task branch

This merge refreshes the Image Intake task branch with the then-current `origin/dev` changes. It incorporates the Release 3 provider inspection-mode, production-composition, Box custody, migration, documentation, CI, and test work so the Image Intake work can continue on top of that base; it does not introduce a separate Image Intake feature implementation. The imported `NOW.md` changes add the QDOS-classification and repository-check-speed claims and update the release queue. It also imports the `docs/capabilities.md` changes for CASE-29, EXT-18, and DATA-02 concerning provider-determined inspection mode.

## 288. b65cc77039885201cfe96238ddff19a2c696ab06 — 2026-08-03 — remove the completed image-intake task claim

This removes the Image Intake task’s active claim from `NOW.md` after its work was considered complete on that task branch. It changes no application code, tests, or roadmap capabilities. This changes `NOW.md`; `docs/capabilities.md` was not changed.

## 289. 3f4a35ba68042676fef00497e72a221ff8388c05 — 2026-08-03 — claim the Release 3 record task

This adds a `NOW.md` claim for the Release 3 record task. The claimed scope is to update production deployment evidence, correct the anonymous-denial check in the production smoke script, remove a stale second-deployment recovery gate, and reduce the remaining release item. It changes `NOW.md`; `docs/capabilities.md` was not changed.

## 290. fb89f3ca1a0205d4d9c3a23e1f7c913aa407712c — 2026-08-03 — record Release 3, correct production smoke checking, and remove a stale recovery gate

This records Release 3 as deployed in the operations evidence, including the active Web revision and image digest, migration-before-activation, healthy Key Vault secret resolution, nine Worker functions, and a live-verified HTTPS sign-in redirect for anonymous case access. The production smoke script now disables automatic redirects so it checks the actual anonymous denial response and explicitly rejects a redirect that downgrades to HTTP. It also removes the obsolete requirement to prove RPO/RTO recovery before a second production deployment, leaving the recovery procedure in place but deferring its proof without making it a release gate. `NOW.md` is reduced to the remaining vault-consolidation work; `docs/capabilities.md` was not changed.

## 291. 095ec06975f7a1fb317a39aac90e36fe23551ff1 — 2026-08-03 — mark Web production composition as deployed

This corrects the architecture document to say that the Web production composition—Box-backed custody and managed document content, staff document/EVA surfaces, and Azure Blob intake artifacts—is deployed in Release 3 rather than merely merged. It points readers to the operations document for the current production state and removes an extra blank line from `NOW.md`. This changes `NOW.md`; `docs/capabilities.md` was not changed.

## 292. 65d20d1226f76252b90ed641b55a4b4a32fc229a — 2026-08-03 — merge the Release 3 record task

This merge brings the Release 3 record task into `main`. It incorporates the updated deployment evidence, the raw-redirect and HTTPS assertion in the production smoke check, the removal of the obsolete recovery gate, the correction that Web composition is deployed, and the completed-task/remaining-vault-consolidation update in `NOW.md`. `docs/capabilities.md` was not changed.

## 293. 6e36f278424793efed22147371b43d146f1520ea — 2026-08-03 — remove the merged Release 3 record plan

This maintenance commit deletes the transient `release-3-record` task plan after its work merged. It changes no application behaviour, `NOW.md`, or roadmap capabilities. `NOW.md` and `docs/capabilities.md` were not changed.

## 294. f030798c6cf5ee6eb7310e4eb998017463bb2b38 — 2026-08-03 — record repository-check CI evidence and its change-detection fragility

This updates the repository-check-speed task plan with measured CI evidence. It records successful cold- and warm-cache runs, their lane timings, complete test-set coverage, and the improvement from the former 28-minute validation job; it also records a cancelled run caused by the `changes` job timing out while fetching history. The plan identifies that this pre-existing job now gates more lanes, and notes unbalanced test shards and the unproven Linux SQL-container path as remaining work. `NOW.md` and `docs/capabilities.md` were not changed.

## 295. 6ef26993b57078fe72189147328bb6c792ac96a8 — 2026-08-03 — pin the Image Intake migration in schema evidence

This extends the intake persistence integration test’s expected migration list to include `ImageIntakeRegistration` and verifies that its three tables—Image Intakes, their registration sequences, and per-image VRM suggestions—exist after migration. It makes no production-code or documentation change. `NOW.md` and `docs/capabilities.md` were not changed.

## 296. 9831e51de5ef17b2ec52c5cdd79b23bebb40c817 — 2026-08-03 — record that audit-report titles carry the repairability verdict

This expands QDOS corpus evidence to show that a standalone Audit’s attached third-party engineer report, rather than the instruction letter, carries the repairable-versus-total-loss verdict in its title. In the examined sample, 23 of 27 Audit emails had an extractable verdict, including one total-loss report; the note identifies non-report attachments and warns against false positives from vehicle-history or parts-list wording. It corrects the predecessor analysis accordingly and records the design implication that document content must be available during classification, otherwise determinable attachment-based facts are incorrectly treated as unknowable. This relates to `NOW.md`’s MAIL-21 and MAIL-22 work; `NOW.md` and `docs/capabilities.md` were not changed.

## 297. 4d08570d01e1e0be13c519bd63c324fdf6ca8224 — 2026-08-03 — raise the provisional automatic VRM threshold to 0.90

This replaces the initial bounded evaluation evidence with a full 2,818-image cohort result and raises the automatic registration threshold from 0.80 to 0.90. The recorded evidence shows that 0.80 produced 315 suggestions with 57 case-label mismatches, while 0.90 produced 64 suggestions with five mismatches and substantially more abstention; because an automatic read creates a permanent, never-reused Image Intake reference, the implementation now favours abstention. The decision remains open pending operator review and the separately protected holdout run. This relates to `NOW.md`’s Image Intake work; `NOW.md` and `docs/capabilities.md` were not changed.

## 298. 21bda2af4b65452a23bb9d0f3fc9db55a9947361 — 2026-08-03 — separate near-miss and different-vehicle VRM suggestions in corpus results

This refines the local corpus-evaluation report so mismatches against a case-level registration are split by edit distance. Differences of one or two characters are recorded as near misses, while larger differences are treated as likely correctly read registrations from another vehicle in a multi-vehicle image; the report keeps the local-only label/suggestion pairs for operator review while publishing only aggregate counts elsewhere. It adds the edit-distance calculation and the separate rates and counts for both categories. `NOW.md` and `docs/capabilities.md` were not changed.

## 299. 1e0f747523ddbbdfca0a6b10238a8b8f8b63cc5d — 2026-08-03 — claim the UI alpha design pass

This adds a `NOW.md` claim for a fixture-only UI alpha design pass covering the Operations dashboard, work queues, activity/freshness states, intake workbench, case workspace, administration surfaces, and accessibility. The claim explicitly excludes Core wiring, real case/reference changes, unresolved business rules, the already-owned Image Intake surface, and deferred or unplanned UI capabilities. This changes `NOW.md`; `docs/capabilities.md` was not changed.

## 300. 6ec51bfa5f25377bd47c2555f8be919bbaa03cd0 — 2026-08-03 — record near-miss versus third-party results from the full VRM cohort

This replaces the open-decision evidence with a rerun that classifies case-label mismatches by edit distance. At the 0.80 bar, 14 of 315 suggestions were near misses and 43 were likely correctly read third-party registrations; at 0.90, four of 64 were near misses and one was a different vehicle. It records that the observed genuine errors were truncations or single-character confusions, keeps the implemented bar at the conservative 0.90, and frames final acceptance as an operator trade between coverage and true-misread risk, with local pair-level evidence available for review. This relates to `NOW.md`’s Image Intake work; `NOW.md` and `docs/capabilities.md` were not changed.

## 301. 3e712021390be451481dfb38c0868ce75672eb6b — 2026-08-03 — plan the fixture-only UI alpha design pass

This adds the implementation plan for a fixture-backed, design-only alpha UI shell. It specifies the shared navigation and accessibility baseline, Operations metrics and queue drill-downs, intake review workbench, case workspace, and administration workspace, with explicit state coverage and a no-real-mutation boundary. It also records excluded capabilities and sub-surfaces, including existing Image Intake search, the deferred email workspace, mobile design, report-image selection, AI readiness advice, mailbox mechanisms, and exact upload-limit invention; its verification requires builds/tests, layout and accessibility checks, and evidence that no fixture route calls Core or external services. This relates to `docs/capabilities.md` UI-01, UI-02, UI-03, UI-04, UI-05, UI-06, UI-08, UI-09, UI-11, and UI-13; `NOW.md` was not changed.

## 302. f1b5daeb10d38534b881735572b84951b25a3ced — 2026-08-03 — claim vault consolidation

This moves the vault-consolidation task from `NOW.md`’s ordered queue into the active claims list. Its stated scope is copying Box, DVLA, and DVSA secrets to the Pegasus Key Vault, repointing Worker and Web references, proving resolution, then retiring the adopted vaults and predecessor resource group. It changes `NOW.md`; `docs/capabilities.md` was not changed.

## 303. 8ba6abc24b90c551120cdceb7205fca832ff98c0 — 2026-08-03 — record operator-confirmed QDOS work-type rules

This records and corpus-checks the operator’s exact generated-text guarantees for QDOS work classification: a body-only `Triage Only Request`, and attachment-only uppercase notification titles for standalone Audit and Inspection plus Audit. It defines the resulting priority: triage first, then the attachment title, then a standalone Audit’s attached third-party report title for repairable or total-loss, with plain Inspection recognised only by its exact unseen title and all other forms failing closed. It also defines the rare standalone-Audit case without an engineer report as a staff-visible “report missing” failure, not an assumed repairable case. This relates to `NOW.md`’s MAIL-21 and MAIL-22 work; `NOW.md` and `docs/capabilities.md` were not changed.

## 304. 16bf13c1b066c4590648a2098ae55522de9dd148 — 2026-08-03 — correct the Triage evidence and document its separate convention

This corrects the QDOS evidence note: Triage emails are genuine instructions with the ordinary field set, but they use the Title Case `Triage Only Request` convention rather than an uppercase notification title and sometimes a third, triage-specific letter filename. None of the examined Triage emails carries an Audit or Inspection instruction letter, so omitting the phrase would cause them to fail the work-type rule rather than be mistaken for case work. Because most are images-only, the realistic risk is that they are absorbed into ordinary image handling; the note records that distinction and the attachment distribution. This relates to `NOW.md`’s MAIL-21 and MAIL-22 work; `NOW.md` and `docs/capabilities.md` were not changed.

## 305. 73218fae9453e9e7a0ce19f979f29608ee651328 — 2026-08-03 — allow a one-character-truncated VRM read to use a confirmed case registration

This implements the operator-directed rule that a confident registration read missing exactly one character can match a case’s confirmed registration. Candidate selection now happens before Image Intake registration: an exact match wins, or one uniquely matching confirmed registration completes a truncated read and becomes the permanent registered identity; substitutions, longer differences, or more than one consistent candidate remain non-matches or ambiguity. The same matching rule is used for persistence queries, suggestion confirmation, automatic association, and corpus scoring, with unit and automation tests for the permitted and rejected cases. `NOW.md` and `docs/capabilities.md` were not changed.

## 306. 3739cb6b7ab89e4c9a9635c7cfb3b11f0f7e763c — 2026-08-03 — sweep abandoned disposable test databases as well as backups

This extends the test-database template cleanup to drop disposable databases that a killed test run left attached, addressing accumulated LocalDB disk use. The sweep runs server-side, only considers names with the exact test-database prefix plus a GUID, only drops databases older than one day, escapes underscore wildcards in the SQL filter, and swallows cleanup failures so cleanup cannot fail a test run. The exact name rule is shared with the existing create/restore/drop guard, with tests proving live databases and invalid names are not eligible; operations documentation adds a read-only query for inspecting attached disposable databases. `NOW.md` and `docs/capabilities.md` were not changed.

## 307. 589792cd564a3eb4e9dc8e158667de56b27f2d30 — 2026-08-03 — update the VRM cohort evidence for the one-character match rule

This reruns and updates open-decision evidence using the new exact-or-one-missing-character registration match rule. At the 0.80 bar, crediting truncated reads reduces case-vehicle near misses from 14 to 11 of 315 suggestions while leaving 43 likely third-party readings; the 0.90 figures remain four near misses from 64 suggestions. The note records the remaining error shapes, retains the conservative 0.90 provisional threshold, and leaves the coverage-versus-misread decision and holdout confirmation for operator acceptance. This relates to `NOW.md`’s Image Intake work; `NOW.md` and `docs/capabilities.md` were not changed.

## 308. 0d41e6db2e6bcda1e3d0e3321e6c40f9305a3a44 — 2026-08-03 — correct the QDOS instruction-letter inventory

This corrects a major corpus inventory undercount caused by QDOS truncating generated PDF filenames to 15 characters. Matching all `Ltrto` filenames and reading every document found 198 letters across Audit, Inspection plus Audit, Triage, and one post-report valuation dispute, with the filename and in-document title agreeing throughout. It establishes that all 158 non-Audit instruction letters in this corpus are report-plus-audit letters, making the absence of plain Inspection a stronger unresolved gap, and confirms that titles—not truncated filenames—must be the authoritative classifier evidence. This relates to `NOW.md`’s MAIL-21 and MAIL-22 work; `NOW.md` and `docs/capabilities.md` were not changed.

## 309. 9b942e4c8a868ae318165e208953648f564081fe — 2026-08-03 — recognise a common fifth-position inserted `1` in VRM reads

This extends the registration match rule for a specific plate-furniture error: an eight-character read with `1` in the fifth position is retried after removing that character and may then match a confirmed seven-character registration, including the existing one-character-truncation rule. Other insertions, substitutions, and non-standard lengths remain rejected. The rule is documented as operator-directed, applied by the corpus evaluation, and covered by unit and automation tests. `NOW.md` and `docs/capabilities.md` were not changed.

## 310. 5a590850dc4827d3ad22f6009bce815f1f681b2c — 2026-08-03 — update the VRM cohort evidence for the inserted-`1` rule

This updates open-decision evidence after scoring the full cohort with both operator-directed registration-match rules. At the 0.80 threshold, the remaining genuine near-miss count becomes 10 of 315 suggestions while 43 reads remain likely third-party registrations; at 0.90 it becomes three near misses from 64 suggestions. The evidence identifies the remaining risks as two-character truncations and substitutions, retains the provisional 0.90 threshold, and leaves final selection and holdout confirmation to operator acceptance. This relates to `NOW.md`’s Image Intake work; `NOW.md` and `docs/capabilities.md` were not changed.

## 311. f7d99b18d594550bbf6d9def8a7af4c8497150d8 — 2026-08-03 — accept the 0.80 VRM bar, add reverse Image Intake pairing, and renumber the engine ADR

This closes the VRM-threshold decision with operator acceptance of the 0.80 automatic-action bar and the agreed matching rules, updating the engine, requirements, capabilities, and decision register accordingly. It adds the reverse pairing path for the common “images first” sequence: after accepting a case, Pegasus scans unassociated Image Intakes and automatically links each only when the newly accepted case is its single eligible unambiguous match; failures do not undo case acceptance. It also prevents policy re-evaluation from reverting a permanently registered Image Intake receipt to Needs sorting, adds pairing tests, and renumbers the ONNX engine decision from ADR-0018 to ADR-0019 to resolve the conflicting Provider Inspection Mode ADR number. `docs/capabilities.md` changes INT-17 and INT-28 to show the accepted threshold and bidirectional pairing; `NOW.md` was not changed.

## 312. f191dd7211b8a47b1187fe7d12b54b7184c02090 — 2026-08-03 — parallelise integration test classes and remove the SQL shard matrix

This removes the single xUnit collection that had serialised all SQL integration-test classes, caps integration concurrency at four threads through a copied runner configuration, and limits browser tests to two threads because each starts Chromium, a web host, and a database. It makes parallel hosts use ephemeral data-protection keys, aligns database lifecycle timeouts with restore/backup work, and retains safety around shared LocalDB resources. The CI workflow consequently replaces the three-runner SQL shard matrix and its coverage job with one parallel SQL lane, removes the shard script, and updates the engineering and operations guidance. The recorded measurements show materially faster, repeatable runs with the full expected test counts and concurrent LocalDB validation. `NOW.md` and `docs/capabilities.md` were not changed.

## 313. ba65c1ed8b4b7e25b8e0c07b1a43feac9c0e7829 — 2026-08-03 — record holdout confirmation for the accepted VRM threshold

This updates the ADR index with the one-time, untouched 705-image holdout result for the accepted 0.80 registration-recognition bar. It records 88 suggestions, two genuine near misses, 14 correctly read third-party registrations, and no technical failures, confirming consistency with the cohort result while retaining the stated boundary that this is evaluation evidence rather than proof of a live caller. `NOW.md` and `docs/capabilities.md` were not changed.

## 314. 948f9fbab423e277aa9ddf302511f8956d325f9e — 2026-08-03 — claim MCP Automation Actor ingress work

This adds a `NOW.md` claim to build a management- and development-controlled MCP ingress for one named vendor-neutral Automation Actor that invokes existing Case, intake-queue, and document Core use cases through its own identity. The claim explicitly excludes staff MCP access, administrator/configuration/credential/cloud/release/deletion authority, AI proposal transport, and the later broader email-workspace actions. This changes `NOW.md`; `docs/capabilities.md` was not changed.

## 315. 69d05ee84f3e15459b80daee6936d1a8dbb7a530 — 2026-08-03 — draft the MCP Automation Actor ingress plan

This adds a scaffolding-only plan for the MCP Automation Actor work and explicitly pauses implementation pending direction on four security and scope decisions: authentication and actor identity, the exact allow-listed tools, the actor/authorization model, and initial real-caller evidence. It documents the rejected per-staff OAuth MCP surface as prior art only, identifies the existing Core actor/use-case/history patterns to reuse, prohibits external client, vendor, credential, and privileged authority work, and defines the future verification requirement as an exercised MCP caller with both successful and denied actions evidenced in history. This relates to `NOW.md`’s MCP-01 through MCP-04 claim; `NOW.md` and `docs/capabilities.md` were not changed.

## 316. 2877af74d6e4803cd1b0144979a92321e923334b — 2026-08-03 — merge current dev into the repository-check-speed task branch

This merge refreshes the repository-check-speed task branch with the Release 3 deployment-record changes from `origin/dev`. It brings in the updated production evidence, raw HTTPS redirect smoke check, removal of the obsolete recovery gate, and the correction that Web composition is deployed. The `NOW.md` conflict is resolved by retaining the UI alpha, vault-consolidation, and MCP ingress claims alongside the existing speed and classification claims. `docs/capabilities.md` was not changed.

## 317. 63de13180565e8e5a4213a8ef7781fefbdf1be66 — 2026-08-03 — record the first measured Azure cost evidence

This adds the first operator-commanded, read-only Azure cost evidence to the open decisions. It records the initial production spend by service, explains that the previous 30-day total was almost entirely the already removed development resource group, projects a £30–35 monthly alpha steady state within the £75 alert, and notes that in-process VRM recognition needs no separate Azure service. It also identifies the worker’s near-idle Flex baseline and legacy-vault Box-secret references as watch items for the queued vault-consolidation work. `NOW.md` and `docs/capabilities.md` were not changed.

## 318. 2791c6208eed1991bb6c26c407e8946448c1e032 — 2026-08-03 — complete MCP ingress design and research AI assistant and Claude hand-off options

This expands the MCP task plan into three unimplemented proposals. For the Automation Actor ingress, it recommends a configuration-gated streamable-HTTP MCP endpoint in Pegasus.Web, an OpenIddict client-credentials identity, a new fail-closed Automation actor, scoped one-action tools over existing Core use cases, rate limiting, action-history evidence, and Claude Code or a scripted client for local caller proof; operator approval remains required for authentication, client registration, tool inventory, and initial client choice. It separately researches a future in-app staff AI assistant, recommending an in-process Core tool loop rather than making staff MCP callers, and a future “Send to Claude” hand-off, recommending a management/development Claude Code channel as research only while retaining the durable AI-09 work-request design as the end state. The plan makes no implementation, activation, or capability claim. This relates to `NOW.md`’s MCP-01 through MCP-04 work and discusses deferred AI-01 and AI-09; `NOW.md` and `docs/capabilities.md` were not changed.

## 319. d2d012d90ebd7b2d869efac1fc7cfc0051e4da73 — 2026-08-03 — make the proposed Claude hand-off return path explicit

This refines the MCP/Claude hand-off research so Claude’s work can return to Pegasus only through approved Automation Actor write tools, with normal attribution, idempotency, leases, and action history. The channel’s reply path is limited to operator chat and permission relay, preventing it from becoming a second business-data ingress or policy path. The plan also states that proposal-shaped AI output remains blocked behind the deferred AI-09 contract, and leaves any admission of AI-drafted documents to the operator-approved tool inventory. This relates to `NOW.md`’s MCP-01 through MCP-04 work and deferred AI-09; `NOW.md` and `docs/capabilities.md` were not changed.
