# CollisionSpike v2 Project Discovery Questionnaire

Complete this document before the application architecture and Azure deployment plan are approved. Short answers are fine. Write `Unknown` where a decision has not yet been made.

## 1. Project ownership

**Project/product owner:**  Alex - Dev @ Collision Engineers

**Final decision-maker:**  Collision Engineers management

**Technical contacts:**  Me

**Target date for the first usable release:**  ASAP

**Target date for production:**  Undetermined

**What must the first release achieve to be considered successful?**  Alpha is full functionality for ONE provider - QDOS


## 2. Azure and GitHub ownership

**Which GitHub user or organisation will own the repository?**  collisionengineers

**Should this folder become the repository, or will an existing remote repository be used?**  yes this folder will

**Which Microsoft Entra tenant should own the application?**  collision engineers / whatever tenant is on my auth'd cli

**Which Azure subscription should be used?**  the azure sub under my email digital@collisionengineers.co.uk

**Is that subscription approved for production workloads and billing?**  Yes

**Who can approve Azure expenditure and budget alerts?**  Me

**Who can grant Azure RBAC roles and approve Entra application registrations?**  These should be granted but I can do this

**Required primary Azure region (for example, UK South):**  Unless it impacts performance, this is not a requirement.

**Must all application data remain in the UK?**  No. Anything related to Data is not a concern.


## 3. Users and organisations

**Who will use the system? Check all that apply.**

- [x] Collision Engineers employees
- [ ] Independent engineers
- [ ] Administrators
- [ ] Reviewers or quality-assurance staff
- [ ] Solicitors
- [ ] Insurers
- [ ] Repairers
- [ ] Vehicle owners or customers
- [ ] Other:

**Will all users belong to one Microsoft Entra tenant?**  The Microsoft Entra tenant owns and administers the Azure resources, but it is not the application sign-in system. CollisionSpike v2 manages its own internal staff accounts with usernames and passwords in the application database. Passwords must only be stored as secure non-reversible hashes, never as readable credentials.

**Will external organisations need their own users and segregated data?**  no

**Should external users be invited as Entra B2B guests, use dedicated customer accounts, or is this undecided?**  Not applicable. There are no external application users in the first MVP, and staff use self-managed application accounts rather than Entra sign-in.

**Should multi-factor authentication be mandatory?**  no

**Who creates, disables, and reviews user access?**  An Administrator may create, disable, and review staff accounts and assign their roles. Public registration is disabled.

### Proposed roles

Add or remove rows as needed.

| Role            | What this role may view     | What this role may create or change | What this role must never access |
| --------------- | --------------------------- | ----------------------------------- | -------------------------------- |
| Administrator   | All application data and settings | All case actions plus accounts, principals, and configuration | Nothing                          |
| Engineer        | Cases, inbox items, documents, and details | All case actions and review gates | Accounts, principals, and application settings |
| User            | Cases, inbox items, documents, and details | All case actions and review gates | Accounts, principals, and application settings |
| External client | Nothing. This isn't a role. | Nothing                             | Everything                       |
| Other           |                             |                                     |                                  |

## 4. The case lifecycle

**What event creates a case?**  Accepting definitive work instructions or usable image-led intake creates a case. An inbox item marked `Blocked intake` remains pre-case and consumes no reference until staff resolve and retry it.

**List the case stages in order, from initial instruction to final closure:**

Triage is an optional pre-case stage and does not itself create a case.

1. Receiving and accepting instructions and/or images (case created)
2. Chasing missing details, images, or documents when the case is incomplete
3. Ready to be passed or assigned to an Engineer after pre-assignment review
4. Inspection and report preparation
5. Post-report queries or disputes

A case reaches a terminal closed state through one of three outcomes:

1. Post-report work is complete
2. The provider cancels the case
3. Collision Engineers rejects or refuses the case

**Who is allowed to move a case between each stage?**  Administrator, Engineer, and User roles may perform every case transition and both review gates. Only Administrators manage accounts, principals, and application configuration. Automated transitions use the same rules, and every user or automated action records its actor, timestamp, prior/new state, and reason or context.

**Which stage changes require review or approval?**  Prior to being passed/assigned to an engineer (submission to EVA currently), and prior to the Engineer sending a report (as this goes to our work provider)

**Can a case be reopened after closure? If so, by whom and under what conditions?**  Yes. An authorised staff user may reopen any closed case. The reopening reason must be recorded in the permanent audit history.

**Can cases be merged, split, reassigned, cancelled, or deleted? Describe the rules.**  Instruction-initiated and image-initiated records may be merged automatically or manually when there is a definitive match. Administrator, Engineer, and User roles may reverse a mistaken merge, reassign a case before an Engineer report is sent, or cancel a case; every action and reason is audited.

For a principal correction before Collision Engineers sends its first report for the case, retain the same case, allocate the next reference for the corrected principal using the calendar year in which the correction occurs, retain the prior reference permanently as a searchable alias, and never reuse either sequence number. The application does not reconcile external records automatically. If a Box folder already uses the former reference, show its link and require a separate audited confirmation of the manual Box update. If EVA contains the former reference, require a separate audited confirmation of the manual EVA update. Block work only until every applicable confirmation is complete; never require confirmation for an artefact that does not exist. If the error is discovered after Collision Engineers sends any report for the case, keep the original principal/reference and add a permanent audit note only. Cases must never be permanently deleted and may only be archived.

**Which actions must be recorded in a permanent audit history?**  any user actions or automated actions

**What case, job, purchase-order, invoice, or report numbering rules must be preserved?**  Every work provider (also called a principal) has a principal code. The standard Case/PO format is `<principal-code><two-digit-current-year><three-digit-sequence>`, for example `QDOS26001`. The sequence is shared by all case types for that principal and year and increments once per case, so the next cases would be `QDOS26002`, `QDOS26003`, and so on.

For a standalone Audit, use the assessment in the original Engineer's report: prefix a repairable report with `a.`, for example `a.QDOS26004`, and a total-loss report with `ap.`, for example `ap.QDOS26004`. If that original report is missing or its assessment is ambiguous, do not create the case or allocate a reference; retain the source in the inbox with a blocking warning so staff can resolve and retry it.

An Inspection + Audit initially uses the standard Inspection reference, such as `QDOS26001`. After Collision Engineers' assigned Engineer completes the Inspection report, a second Audit reference is created with `a.` or `ap.` according to that Engineer's repairable or total-loss finding. The Audit is stored in a subfolder beneath the original Inspection folder in Box.

These references are entered into EVA and used as the corresponding Box folder names.

**Which parts of the current or previous workflow work well and must be retained?**  nothing

**Which parts must not be reproduced in v2?**  huge technical debt, spaghetti code, functions duplicated everywhere so there was so much drift.


## 5. Case information

**What information is required to create a case?**  A case may be initiated by receiving work instructions or a set of vehicle images. An authorised definitive instruction creates an incomplete case automatically and must retain the source email/instruction plus every available provider, claimant, claim, vehicle, accident, instruction-date, and inspection-address value. Missing ordinary details do not add a universal manual creation gate. Staff decide only whether non-definitive or known-blocked material can be resolved into definitive intake or should remain in the manual `Blocked intake` filter. The filter requires a reason, retains the source with a warning, and allocates no case/reference until staff resolve and retry it. Missing vehicle registration and a standalone Audit without a clear original-report assessment are examples of identity blockers. For an image-initiated case, a readable vehicle registration is guaranteed and is used as its identifier until the images are matched with the related instructions. A formal Case/PO is assigned once the principal is known.

**What people and organisations can be connected to a case?**  The work provider/principal, claimant, Collision Engineers staff and assigned Engineer, repairer/garage/bodyshop, third-party insurer, and relevant email or operational contacts.

**What vehicle information is required?**  Vehicle registration (VRM), make, model, mileage, accident circumstances, date of incident, damage images, and any vehicle details retrieved through DVLA/DVSA or MOT data when they are not present in the instructions.

**What appointment, inspection, repair, valuation, or engineering information is required?**  Collision Engineers performs desktop inspections only, so there is no physical inspection appointment. Record the physical vehicle/repairer address when required by the provider, otherwise record `Image Based Assessment`. The engineering record must support roadworthiness, repairable or total-loss outcome, estimated repair cost, the Engineer's report, relevant valuations, and post-report queries or disputes.

**What financial information is required?**  The architecture must accommodate repair estimate, vehicle valuation, and invoice amount. These values and their workflows are out of scope for the first MVP but must not require a redesign when introduced later.

**Which fields are mandatory, optional, calculated, or restricted?**  Cases may exist with incomplete information during intake and chasing. Instruction completeness and image completeness are separate staff judgements. The backend provides a configurable on/off gate that, when enabled, prevents Engineer assignment until staff have confirmed both `Instruction complete` and `Images complete`; the gate must be changeable without a code deployment. The application still shows missing and contradictory values, but the first MVP does not enforce a hard-coded universal or principal-specific field matrix. Principal identity before reference allocation and a clear original-report assessment for a standalone Audit remain separate identity rules. Instruction date defaults to the current date when absent; Case/PO and audit references are calculated; inspection address may validly be `Image Based Assessment`; DVLA/DVSA and MOT-derived values are enriched or calculated when available. Financial fields planned for later are not required in the first MVP.

**Do different case types require different fields or workflows?**  Yes. The active case types are Inspection, Audit, and Inspection + Audit. A standalone Audit includes the other firm's original Engineer report and derives its `a.` or `ap.` reference from that report's repairable/total-loss assessment. Inspection + Audit begins as an Inspection and creates its Audit reference and Box subfolder after Collision Engineers' own Engineer assessment. Diminution and Commercial must be represented in the architecture but are deferred beyond the first build.

**What searches and filters must users have?**  Search and filter by Case/PO, vehicle registration, claimant, claim number, provider/principal, case stage/status, assigned Engineer, received date, instruction date, and date range. Users must also be able to filter by intake origin: image-initiated or instruction-initiated. The original intake source must remain available after related records are matched or merged.

**What dashboards, reports, exports, or management information are required?**  The first MVP requires a case-intake dashboard modelled on the supplied mockup. It should provide operational tiles rather than a general analytics dashboard:

- Case queues: `Not ready`, `Review`, and `Held`
- Inbox queues: `Receiving work`, `Queries`, `Other`, `Needs sorting`, and the manual `Blocked intake` filter
- Today/this-week activity: `In today`, `Submitted today`, and `Cleared this week`
- A visible last-updated time and manual refresh action

`Not ready` contains incomplete cases being chased. `Review` contains complete cases awaiting a required pre-assignment or pre-report approval. `Held` is a manual case pause with a required reason; it blocks progression and recurring chasers while due dates remain visible. `Blocked intake` is pre-case and manually chosen by staff, creates no case/reference, and retains the source, reason, warning, and retry action. `Needs sorting` remains for uncertain classification or association rather than a known blocker.

Each tile or filter shows its current count and opens the corresponding work view. The first MVP must also export the case's structured JSON together with its stored images for transfer into EVA. This is an interim integration until EVA's API becomes usable.


## 6. Documents, photographs, and evidence

**What file types will be uploaded or generated?**  Inbound Outlook email and email-body content, WhatsApp content, PDF, DOC/DOCX, MSG, and vehicle images. The first MVP extracts EML, PDF, DOCX, JPEG, and PNG content. Legacy DOC and MSG sources are retained with provenance and sent to `Needs sorting`; automated extraction of those two containers is deferred. Generated or assembled outputs include structured case JSON for EVA, downloaded image bundles, Engineer reports, and related case correspondence. Instruction emails/documents, vehicle images, and final Engineer reports are stored in the case's Box folder.

**Expected typical and maximum file size:**  maybe 5-10mb maximum

**Expected number of files per case:**  anything from 2-3 to 20+

**Must photographs preserve original files and metadata?**  no

**Must files become immutable after submission, approval, or case closure?**  When a case is closed, its files become read-only at the application level. An authorised staff user must reopen the case before files can be changed, replaced, or removed. Reopening and subsequent file changes must be recorded in the permanent audit history. This is a reversible workflow lock rather than irreversible storage immutability.

**Should revised documents create a new version or replace the previous file?**  Revised documents create a new version and retain every previous version. Replacement must not destroy the earlier content, and each revision must be audited.

**Who may view, upload, download, replace, or delete evidence?**  Authenticated Administrator, Engineer, and User roles may view, upload, and download case evidence. They may revise or remove evidence only while the case is open or has been reopened. Revisions retain previous versions, removals are logical rather than permanent, and all actions are audited. External clients have no access.

**Are virus scanning, OCR, image processing, redaction, or AI classification required?**  Automated malware scanning is deferred beyond the first MVP. The first MVP uses deterministic local extraction first and OCR only for scan-like PDF pages that lack usable embedded text. Ordinary email image attachments, inline images, DOCX images, and discrete PDF images are retained as separate review candidates and are not sent to OCR. Automated vehicle-registration OCR/VLM, in-app AI, guided image capture, image/vision-AI assistance, and automated malware scanning are planned beyond the first MVP. Redaction is not required.

**Direct product decision — 2026-07-23:** automated vehicle-registration OCR/VLM is deferred beyond the first MVP. This resolves the older combined operator-note wording by separating two capabilities: OCR of scan-like PDF instruction pages remains required in the first MVP; reading a VRM automatically from ordinary vehicle images does not. Until the later capability is activated, staff may identify image-led work by a readable registration and all images remain reviewable evidence. Activation requires representative accuracy evidence, a selected adapter/service and licence/cost approval; this decision adds no dormant OCR/vision client, endpoint, queue, configuration or feature flag now.

**Are digital signatures or evidential chain-of-custody records required?**  No. The permanent action audit and retained file-version history are sufficient.


## 7. Communications and tasks

**Should the system send or receive email? Describe the mailboxes and flows.**  Yes. The first MVP automatically ingests the new shared `instructions@collisionengineers.co.uk` mailbox. The full product must ingest and classify all received email from `desk@collisionengineers.co.uk`, `engineers@collisionengineers.co.uk`, `info@collisionengineers.co.uk`, and `instructions@collisionengineers.co.uk`. Inbound email may contain PDF, DOC/DOCX, freehand instruction text, images, queries, or other correspondence. The system must support outbound chasers/file requests for missing information or images and, eventually, general case email management from within the application.

Mailbox categorisation is a major long-term architectural scope. Approved rules must be extensible and modifiable through one Core-owned policy rather than copied into Web, Worker, API, MCP, or individual mailbox adapters. The exact category predicates, rule-authoring authority, change mechanism, precedence, versioning, audit, correction, and rollback behavior remain decisions to settle before implementing automatic categorisation; this decision does not authorise a generic rule engine or dormant configuration model now.

**Must email and attachments be automatically associated with cases?**  Yes. Related emails and attachments must be identified, associated with the correct case automatically where a definitive match exists, shown in the case history, and stored in the corresponding Box case folder. Uncertain matches go to the `Needs sorting` queue for staff review.

**Are SMS, Teams, portal notifications, or other channels required?**  WhatsApp remains a manual staff channel in the first MVP. Staff may manually add received WhatsApp images or information to the relevant case. The architecture should preserve WhatsApp coexistence as a potential future automation and ingestion route. No direct SMS, Teams, customer-portal, or WhatsApp integration is required in the first MVP.

**What tasks, reminders, deadlines, escalations, or service-level timers are required?**  While a case is waiting for missing details, images, or documents, create a recurring reminder to chase every seven days. Stop future chase reminders when the required material is received or the case reaches a terminal state. Extract the inspection date, or equivalent deadline stated in the instructions, as the case's `Due by` date and use it to identify overdue work.

**Who may create, assign, complete, or cancel tasks?**  Authorised Administrator, Engineer, and User roles may create, assign, complete, or cancel case tasks. The system may create recurring chase reminders automatically. All task actions are included in the permanent audit history.

**Which communications need templates and approval?**  Automated sending is out of scope for the first MVP. For chasing missing information, images, or documents, the application generates a clickable message that staff can copy and paste into email or WhatsApp. Where useful, the application also creates or includes a Box file-request link. Because staff send the copied message manually, no separate in-app approval workflow is required in the first MVP.


## 8. Integrations

Complete the required integrations and identify the system that remains authoritative.

| System or service | Required for first release? | Information exchanged | Direction | Authoritative system | Contact/owner |
|---|---|---|---|---|---|
| Microsoft 365 / Outlook | Yes | Email, body content, attachments, sender/recipient data, timestamps | Inbound in first MVP; broader email management later | Microsoft 365 for mailbox content; application for case association | Collision Engineers / Alex |
| Box | Yes | Case folders, instruction emails/documents, vehicle images, Engineer reports, and file requests | Bidirectional | Box is the long-term file store; application owns case metadata and links | Collision Engineers / Alex |
| Accounting or invoicing | No; plan for later | Invoice amount and future accounting data | To be defined | Future accounting system to be selected | To be defined |
| DVLA / DVSA | Yes | Vehicle and MOT details, including mileage information when available | Inbound lookup | DVLA/DVSA source data; application stores the case snapshot | Collision Engineers / Alex |
| Mapping or location | No; plan for later | Inspection-address suggestions and location signals | Inbound lookup | To be defined | To be defined |
| OCR / document processing | Yes, scanned PDF pages only in the first MVP | PDF/email/DOCX content converted into reviewable case fields; OCR is limited to scan-like PDF pages without usable embedded text. Automated VRM reading is later scope | Inbound processing | Original source files remain authoritative; extracted fields and retained images are reviewable application data | Collision Engineers / Alex |
| AI services | No; plan for later | In-app assistance, vision/image assistance, and classification enhancements | To be defined | Application remains authoritative for approved case data | To be defined |
| EVA | Yes | Structured case JSON and image bundle for manual transfer; future API integration | Outbound in first MVP; bidirectional later if EVA API permits | EVA remains authoritative for Engineer assignment, estimating, valuation, and report generation until replaced | Collision Engineers / EVA vendor |
| WhatsApp | Manual only; automate later | Instructions, chaser messages, and vehicle images | Manual in first MVP | WhatsApp retains channel history; staff add relevant content to the application/Box | Collision Engineers |
| Tractable / Ravin | No; potential future route | Guided claimant image capture | Inbound later | To be defined | Collision Engineers / vendor |
| Audatex and valuation services | No; plan for later | Estimate and valuation data | To be defined | External service remains authoritative until replacement strategy is agreed | Collision Engineers / vendor |

**Are test accounts, API specifications, credentials, or vendor contacts available for these integrations?**  Working access is available for the required first-MVP integrations except EVA. EVA API documentation is available under `docs/reference/EVA/EVA_API_SCHEMA`, but its usable API access depends on the EVA vendor. Until that is resolved, the system exports JSON and images for manual transfer. Integration secrets must never be committed to the repository; use Infisical or Azure Key Vault for secret custody.

**What are the provider API and MCP boundaries?**  The provider HTTP API uses separately issued principal-scoped client IDs and opaque secrets. It accepts idempotent instruction and attachment submissions and lets a principal retrieve only its own submission receipt, processing status, and resulting Case/PO. It does not expose general case reads or workflow mutation in the first MVP.

MCP is a separate internal staff surface, primarily for Claude Desktop. Each staff member authorises the remote connector through CollisionSpike using OAuth, and every call uses that person's current application role and permanent audit identity. MCP may expose the full case, inbox, and document actions that the signed-in role can perform through the staff UI, but it does not expose account/role administration, principal configuration, credential management, cloud operations, or permanent deletion. Both surfaces call the same Core use cases and authorization policies as the staff Web application.


## 9. Existing data and migration

**Must any data from the previous application be migrated?**  No. CollisionSpike v2 starts fresh and does not import legacy application cases.

**Which cases, users, documents, emails, audit records, or reference data must be retained?**  No legacy application cases, users, audit records, or application state are migrated into v2. Existing historical documents and operational records remain in their existing Box, EVA, Outlook, spreadsheet, or network-drive locations. Required principal codes and other operational reference data are recreated cleanly for v2 rather than migrated wholesale.

**Where is the existing data currently stored?**  Box stores long-term case files; EVA stores current case-management, estimating, valuation, and report data; Outlook stores email; Excel acts as the current ready/not-ready holding pen; and unmatched WhatsApp images may temporarily be stored on the network drive.

**Approximately how much data is involved?**  No legacy dataset is being migrated, so a migration volume estimate is not required. New v2 storage capacity is addressed by the scale and growth questions.

**Can the old system be made read-only after cutover?**  No read-only legacy service is required. The previous CollisionSpike application will be shut down completely when v2 cuts over.

**How long must the old system remain available?**  Only until the agreed v2 cutover. It can be shut down after cutover validation.

**Who will validate that migrated data is complete and correct?**  Not applicable because no legacy data migration will occur. Collision Engineers will validate the fresh v2 workflow before cutover.


## 10. Data protection and governance

**What categories of personal, sensitive, financial, or legally privileged data will be stored?**  Operational case content can include claimant names and contact/address information, provider and insurer details, claim references, vehicle registrations and details, accident circumstances and dates, inspection locations, emails, documents, vehicle images, Engineer reports, and post-report correspondence. Repair estimates, valuations, and invoice amounts are planned for later. No special data-classification feature is required for the first MVP.

**What is the retention period for cases, documents, logs, backups, and audit records?**  No application-enforced retention or automatic deletion period is required for the first MVP. Box remains the long-term document store, and v2 case and audit records are retained unless Collision Engineers defines a later policy.

**When may data be deleted, and who can authorise deletion?**  Cases are never permanently deleted through the application and may only be archived. File removals are logical and retain version history. Closed cases must be reopened by authorised staff before changes can be made, and all such actions are audited.

**Are legal holds or litigation holds required?**  No legal-hold feature is required for development or the first MVP.

**Is a Data Protection Impact Assessment required or already available?**  No DPIA deliverable is in scope for application development or the first MVP.

**How should subject-access, correction, export, and erasure requests be handled?**  No dedicated in-application workflow is required for the first MVP. Any external legal or management process remains outside the development scope unless Collision Engineers later adds a requirement.

**Which activities require security or compliance alerts?**  Use standard operational security alerts for authentication or authorisation failures, privileged role/configuration changes, ingestion/integration failures, application availability, and unexpected Azure cost. If automated malware scanning is introduced later, detections must generate alerts. No additional data-compliance alert workflow is required for the first MVP.

**Are there contractual, insurer, solicitor, ISO, Cyber Essentials, or other compliance requirements?**  No additional compliance implementation requirement has been supplied for development or the first MVP. This development-scope decision does not replace any external legal or organisational obligations managed by Collision Engineers.


## 11. Scale, performance, and availability

**Expected number of users at launch:**  Approximately 8 Collision Engineers staff users.

**Expected concurrent users:**  Design for all 8 launch users to be active concurrently.

**Expected new cases per day or month:**  Approximately 2,000 new cases per month (about 24,000 per year).

**Expected annual data growth:**  Plan for approximately 24,000 new case records and roughly 48,000 to 480,000+ associated files per year based on the stated 2-3 to 20+ files per case. At the supplied 10 MB maximum per file, the conservative upper storage envelope is about 4.8 TB per year before allowing for versions. Box remains the long-term file store; Azure application storage should primarily hold structured metadata, processing state, audit records, and any necessary transient artifacts. Measure actual case-folder sizes after launch and adjust the forecast.

**Required operating hours:**  Automated mailbox ingestion and case processing operate continuously. Staff-facing use is expected primarily during Collision Engineers business hours, but the application should remain available outside those hours unless undergoing planned maintenance.

**Maximum acceptable planned downtime:**  A short planned interruption during a production release is acceptable for the first MVP. Validate the release in the shared development environment, deploy directly to the production B1 App Service outside office hours, wait for health checks, and run smoke tests. Keep the previous immutable artifact available for rollback and notify staff when an interruption will affect them. Upgrade to Standard S1 and deployment slots later only if release interruption becomes a genuine operational problem.

**Maximum acceptable unplanned downtime:**  Target restoration of service within four hours for the first MVP.

**Maximum acceptable data loss after a failure (recovery point objective):**  At most 15 minutes of recent application updates after a severe database failure requiring restore. Normal application restarts or deployments must not lose committed data. Source emails and files remain available in Outlook and Box for recovery or reconciliation.

**Maximum acceptable time to restore service (recovery time objective):**  Four hours for the first MVP.

**Are there seasonal or deadline-driven workload peaks?**  No predictable seasonal, calendar, weather-related, or deadline-driven intake peaks are expected.


## 12. Environments, networking, and access

**Required environments:**

- [x] Local development
- [x] Shared development
- [ ] Test or QA
- [ ] User acceptance testing
- [ ] Staging
- [x] Production
- [ ] Training or demonstration

Use one shared Azure development/test environment for unfinished cloud integration work. Its F1 App Service may sleep when idle and is subject to the Free-tier CPU quota. The first MVP has no separate staging environment or production deployment slot; approved releases deploy directly to production B1 outside office hours.

**Should production and non-production use separate Azure subscriptions?**  Not for the first MVP. Use the same approved Azure subscription with separate development and production resource groups, identities, configuration, data stores, budgets, and access boundaries.

**Will the application be public on the internet, restricted by organisation/network, or accessed through a private connection?**  The application is reachable over the public internet so Collision Engineers staff can use it from anywhere. Staff pages use self-managed CollisionSpike usernames and passwords; the provider API uses principal-scoped machine credentials; and MCP uses staff-authorised OAuth tokens. Only narrowly defined technical endpoints such as health checks are anonymous. Access is not limited to the office network.

**Is a custom domain already owned? If so, what is it?**  No custom application domain is planned for the first MVP. Use the stable Azure App Service hostname and have staff bookmark it. Preserve support for adding a Collision Engineers subdomain later without changing application behaviour or authentication.

**Are there fixed office IP addresses, VPNs, firewalls, or third-party allowlists to consider?**  No office-IP, VPN, or private-network restriction applies to staff access. The application is accessible from anywhere after application authentication. Any outbound allowlist requirements discovered for third-party APIs will be handled per integration.

**Who needs emergency operational access to production?**  Alex initially, plus any specifically designated Administrator or Azure operator added later. Emergency application and Azure actions must remain attributable in the relevant audit/activity logs.


## 13. Monitoring, support, and operations

**Who will support the application during business hours?**  Alex provides first-line application support.

**Who should receive security, availability, failure, and cost alerts?**  Alex initially. Additional recipients may be added later through monitoring configuration without code changes.

**What response times are expected for critical incidents?**  Critical incidents should be acknowledged immediately while Alex is in the staffed office. Outside staffed hours, respond as soon as reasonably possible. The first-MVP service-restoration target remains four hours.

**Which business operations must have dashboards or alerts?**  Dashboard the agreed case and inbox queues plus today/this-week throughput. Alert on mailbox ingestion failures, document/OCR processing failures, Box folder/file-request failures, unmatched or repeatedly failing case associations, overdue due-by dates, failed recurring-chase generation, EVA export failures, application health, authentication anomalies, and unexpected Azure cost.

**How often should backup restoration and disaster recovery be tested?**  Test database restore and the documented recovery procedure at least quarterly and after material infrastructure or persistence changes. Record the result and any remediation.

**Who may deploy to development, test, staging, and production?**  Alex controls releases initially. GitHub Actions performs automated deployments through its scoped Azure workload identity. Unfinished changes deploy only to shared development. There is no first-MVP staging environment; after approval, production releases deploy directly to B1 outside office hours and are health-checked and smoke-tested immediately.

**Who approves production releases?**  Collision Engineers management provides business approval; Alex performs or authorises the technical production release.


## 14. Budget and commercial constraints

**Target monthly Azure budget during development:**  No fixed monthly budget. Use the lowest practical Azure tiers that still support required development and integration testing, remove unnecessary resources, and configure budget alerts from the approved cost forecast.

**Target monthly Azure budget in production:**  No fixed monthly ceiling has been set. Size for the stated workload and reliability targets, favour managed services and simple architecture, document estimated recurring cost before deployment, and configure budget/forecast alerts.

**Is increased cost acceptable for zone redundancy, private networking, stronger backups, or disaster recovery?**  Defer multi-region failover, zone redundancy, and private networking beyond the first MVP. Functional delivery is the priority. Use a cost-conscious single-region design with secure authentication, least-privilege access, encryption, standard managed backups, health checks, monitoring, and a documented path to add the deferred reliability/networking features later.

**Are there licences or existing Microsoft agreements we should use?**  Reuse Collision Engineers' existing Microsoft 365, Azure, Box, EVA, Audatex, and other vendor accounts/licences where applicable. No additional enterprise agreement or licensing constraint has been supplied; confirm commercial/API entitlement before enabling each vendor integration.

**Are there fixed procurement or vendor restrictions?**  No fixed procurement or vendor restriction has been supplied. Prefer existing approved services and avoid introducing a new paid platform unless it provides necessary functionality and its recurring cost is documented.


## 15. First-release scope

### Must have

- Self-managed CollisionSpike staff usernames/passwords, account administration, and Administrator/Engineer/User roles
- QDOS principal configuration, provider-specific Case/PO sequencing, audit prefixes, Box naming, and Inspection + Audit secondary references
- Active support for Triage requests, Inspection, Audit, and Inspection + Audit; Diminution and Commercial remain deferred
- Automatic ingestion from the `instructions@collisionengineers.co.uk` shared Outlook mailbox
- Identification and categorisation of every ingested mailbox item into receiving work, queries, other, needs sorting, or the applicable business Triage flow
- Extraction of required case details from PDF, DOCX, and freehand EML instructions; retain legacy DOC and MSG sources in `Needs sorting` without allocating a reference
- OCR only for scan-like PDF pages without usable embedded text; automated vehicle-registration OCR/VLM is deferred
- Automatic case creation when new instructions are received
- Manual review and linking of image-led intake using the vehicle registration when staff can establish it; automated vehicle-registration reading is deferred
- Manual case creation and manual upload of instructions, correspondence, documents, and images
- Automatic linking of image-initiated and instruction-initiated records when there is a definitive match
- Manual linking, mistaken-merge reversal, principal reassignment before Collision Engineers' first report with correction-year references and retained aliases, cancellation, closure, reopening, and archive workflows with permanent audit history
- Automatic association of related emails and attachments with the correct case, with uncertain matches routed to `Needs sorting`
- Full staff case management and editing across intake, chasing, ready/review, inspection, post-report query/dispute, and terminal states
- Configurable backend enforcement of staff-confirmed `Instruction complete` and `Images complete` before Engineer assignment, without a hard-coded field matrix
- Review/approval gates before Engineer assignment and before an Engineer report is sent to the provider
- Inspection-address capture using either the physical vehicle/repairer address or `Image Based Assessment`
- Case due-by dates extracted from instructions and recurring seven-day chase reminders while information is missing
- Automatic Box case-folder creation and long-term storage of instruction emails/documents, images, correspondence, and Engineer reports
- Retained document versions, closed-case file locking, and reopen-before-edit behaviour
- Automatic Box file-request creation and copyable chaser messages for staff to send manually
- Case-intake dashboard with the agreed case queues, inbox queues, manual `Blocked intake` filter, today/this-week activity, counts, links, last-updated time, and refresh
- Search/filter by Case/PO, VRM, claimant, claim number, principal, stage/status, Engineer, dates, and image- versus instruction-initiated origin
- DVLA/DVSA lookup when vehicle details are absent
- Mileage estimation from MOT data when available
- Structured case JSON plus stored-image download for manual transfer into EVA until its API becomes usable
- In-app email management for the first-MVP mailbox scope
- Principal-scoped provider API for idempotent instruction/attachment submission and own-submission receipt, status, and resulting Case/PO retrieval
- OAuth-authorised internal staff MCP for Claude Desktop, exposing role-authorised case/inbox/document actions through the same Core use cases while excluding security, configuration, cloud, and permanent-delete operations
- Application and integration health monitoring, operational alerts, recovery controls, and the agreed audit trail

### Should have

- None. First-MVP functionality is either mandatory or explicitly deferred; there is no separate optional-but-desirable tier.

### Could have later

- Direct estimating and valuation service integrations, including Audatex and other valuation providers
- Direct EVA API integration and eventual replacement of EVA's assignment, estimating, valuation, and report functions
- Diminution and Commercial case workflows
- Collision Engineers' own guided mobile image-capture system
- In-app AI assistance and image/vision-AI features
- Inspection-address suggestions based on provider history, accident location, and image analysis
- Automated ingestion from the other Collision Engineers Outlook mailboxes
- Automated WhatsApp coexistence/ingestion
- Automated outbound email/chaser sending rather than copy-and-paste messages
- Tractable or Ravin guided-capture integration
- Accounting/invoicing integration and workflows for repair estimate, valuation, and invoice amount
- Automated malware scanning for inbound files
- A Collision Engineers custom subdomain
- Multi-region failover, zone redundancy, and private networking when justified by usage or business requirements

### Explicitly out of scope

- Migration of cases or application state from the previous CollisionSpike application
- Keeping the previous CollisionSpike application available after v2 cutover
- Reuse of the previous `cedocumentmapper` implementation or other poorly structured legacy application code
- External/customer application accounts in the first MVP
- Automated WhatsApp, SMS, Teams, or customer-portal integration in the first MVP
- Automated sending of chaser messages in the first MVP
- Estimating, valuation, accounting, invoicing, guided capture, in-app AI, and inspection-address suggestion features in the first MVP
- Automated malware scanning for inbound files in the first MVP
- Diminution and Commercial case processing in the first build
- A dedicated DPIA, legal-hold, retention, subject-request, or other data-governance workflow as part of first-MVP development
- Multi-region, zone-redundant, or private-network infrastructure in the first MVP

**What is the single most important end-to-end workflow the first release must prove?**  Full QDOS case handling: receive QDOS instructions and/or images; classify and extract EML, PDF, and DOCX content; retain legacy DOC/MSG and separate image evidence for review; OCR only scan-like PDF pages; create and definitively match the records; assign the correct QDOS Case/PO; create/store the Box case folder and files; chase missing information with due dates, reminders, and file-request links; enrich vehicle/MOT data; complete staff review and readiness gates; export structured JSON and images to EVA; track the case through Engineer/report and post-report activity; and close, reopen, or archive it with a complete audit history. Automated VRM OCR/VLM is a later enhancement, not a first-release dependency.

**Who will perform acceptance testing and approve that workflow?**  Alex performs technical and operational acceptance testing with relevant Collision Engineers staff. Collision Engineers management provides final business approval for production release.


## 16. Additional constraints and decisions

**Are there mandated technologies, suppliers, standards, or existing assets that must be used?**  Host the complete application on Microsoft Azure. Development and cloud operations run on Windows through PowerShell 7 using GitHub, Azure CLI, Azure Developer CLI, and Bicep-based infrastructure as code. Continue using Microsoft 365/Outlook for email, Box as the long-term case file store, DVLA/DVSA for vehicle/MOT enrichment, and EVA as the interim downstream case/engineering system. Store development/deployment secrets only in Infisical or Azure Key Vault. Treat `docs/operator-notes` as read-only, absolute business authority and use only genuine repository-provided example emails, documents, instructions, and images for test data. No application language/framework is mandated by the business requirements; the technical stack is finalised in the architecture/deployment plan.

**Are there technologies or approaches that must not be used?**  Do not reuse the previous `cedocumentmapper` implementation or reproduce the previous system's duplicated functions, spaghetti code, drift, or technical debt. Do not create synthetic emails, images, or instructions. Do not commit secrets or readable passwords. Do not expose internal Azure/function terminology or labels such as `dev copy` in the user interface, and do not make the application narrate obvious controls with unnecessary explanatory sentences. Use the reserved business terms `Audit` and `Triage` only for their actual Collision Engineers meanings. Do not edit `docs/operator-notes`.

**What concerns you most about rebuilding this system?**  Avoiding the previous application's huge technical debt, spaghetti code, duplicated functions, behavioural drift, and cumbersome structure while still delivering the full required functionality quickly.

**Anything else that should influence the product or architecture?**  Functionality and a complete QDOS workflow are the first-MVP priority. Use a simple modular architecture, logical names that reveal purpose, one authoritative implementation of each business rule, configurable backend policy where requirements may change, complete auditability, and explicit future extension points for deferred integrations and features. Cost should be controlled, but functional delivery takes priority over premium resilience/networking features in the first MVP.
