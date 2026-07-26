# CollisionSpike v2 Project Discovery Questionnaire

Status: **Active settled product authority**

This document owns settled product behavior. [The feature maturity map](docs/plans/feature-maturity-map.md) owns allocation: V0 is local pre-alpha, V1 is the live QDOS alpha release gate, additional providers arrive during V1.x before V2, V2 is the email-workspace/provider-API beta, and V3/V3+ contains the allocated later release work. An allocation is not evidence that a capability is implemented, called, deployed, or accepted. `Never`, conditional, and `Unclear` remain distinct.

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

Triage is an optional, separate pre-case business record and does not itself create a case. An active Triage requires a vehicle registration; without one, retain the source in `Needs sorting`. Administrator, Engineer, and User roles may record the binary finding `Roadworthy` or `Unroadworthy`. Its states are `Open` or `Awaiting information`, then `Finding recorded`, then `Completed`. `Cancelled` is the only terminal Triage state without a finding. It has an optional assignee, no due date, and no chasers.

Completing a Triage requires the exact reply-chain Outlook message found in Sent Items; subject, vehicle-registration, and manual-selection fallbacks are not sufficient. Evidence must come from an Administrator-maintained allowlist of approved shared or individual staff mailboxes. Before sending, staff may replace a finding with a required reason. After sending, a changed finding supersedes the earlier finding, requires a new response, and retains the full history. Reopening a completed or cancelled Triage always returns it to `Open`.

A later case association is automatic only for a definitive shared match; otherwise staff confirm it. The Triage remains a separate linked record. Each Triage may link to at most one case, while a case may link to multiple Triage records. Any staff role may unlink or relink with a required reason. Every Triage mutation is recorded in permanent action history.

1. Receiving and accepting instructions and/or images (case created)
2. Chasing missing details, images, or documents when the case is incomplete
3. Ready to be passed or assigned to an Engineer after pre-assignment review
4. Inspection and report preparation
5. Post-report queries or disputes

A case reaches a terminal closed state through one of four outcomes:

1. Post-report work is complete
2. The provider cancels the case
3. Collision Engineers rejects or refuses the case
4. The case was `Created in error` because its reference was allocated under the wrong principal

**Who is allowed to move a case between each stage?**  Administrator, Engineer, and User roles may perform every case transition and the pre-Engineer-assignment review gate. Only Administrators manage accounts, principals, application configuration, and the approved Outlook mailbox allowlist. Automated transitions use the same rules. Business actions are recorded under the permanent action-history boundary below.

**Which stage changes require review or approval?**  The configurable completeness and staff review gates apply before a case is passed or assigned to an Engineer. There is no pre-send report review gate. In V1, Engineers continue to send reports through the existing Outlook/EVA process and CollisionSpike only detects exact sent evidence. Automatic report sending is separately allocated to V3+ and requires its own authority, approval, idempotency, recovery, and acceptance contract before it can replace that current boundary.

**Can a case be reopened after closure? If so, by whom and under what conditions?**  Yes. An authorised staff user must provide a reason and choose any otherwise-valid nonterminal workflow state; every normal gate for the chosen destination is enforced. `Held` is excluded because entering it is a separate reasoned action. A case closed as `Created in error` can never reopen. The action and reason are retained in permanent action history.

**Can cases be merged, split, reassigned, cancelled, or deleted? Describe the rules.**  Instruction-initiated and image-initiated records may be merged automatically or manually when there is a definitive match. Administrator, Engineer, and User roles may reverse a mistaken merge or cancel a case. Each reversal and cancellation requires a reason in permanent action history. Cases must never be permanently deleted and may only be archived.

Case principal and reference are immutable immediately upon reference allocation. If the wrong principal was used, close the erroneous original as `Created in error`, require a reason and a link to its replacement, and create the replacement as a new case under the corrected principal. Neither reference is reused, and the original can never reopen.

**Which actions must be recorded in permanent action history?**  Include business mutations; downloads and exports; material denied or failed business actions; automated business results; and external information actually accepted, linked, or used. Account creation, role changes, disabling, and credential administration are permanent actions; sign-ins use the separate security log. Routine views, searches, refreshes, polling, retries, leases, heartbeats, and adapter mechanics go to content-safe telemetry rather than permanent action history.

Each permanent action records structured before/after field values, actor, time, entered reason when required, and outcome. It excludes secrets and file or message bodies. An entered reason is mandatory for hold/release, cancellation, rejection, reopening, corrections, reversals or unlinks, principal/reference replacement changes, logical removal, overrides, and account or configuration changes.

**What case, job, purchase-order, invoice, or report numbering rules must be preserved?**  Every work provider (also called a principal) has a principal code. The standard Case/PO format is `<principal-code><two-digit-current-year><three-digit-sequence>`, for example `QDOS26001`. The sequence is shared by all case types for that principal and year and increments once per case, so the next cases would be `QDOS26002`, `QDOS26003`, and so on. A principal code becomes immutable on first use.

A legitimate code replacement creates a new linked principal and atomically deactivates the predecessor. In the cutover year, the replacement principal continues from the predecessor's next sequence number. In later years, its sequence starts at `001`. For example, if the predecessor's last 2026 reference ended in `004`, the replacement's first 2026 reference ends in `005`; its first 2027 reference ends in `001`.

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

**Which fields are mandatory, optional, calculated, or restricted?**  Cases may exist with incomplete information during intake and chasing. Instruction completeness and image completeness are separate staff judgements. The backend provides a configurable on/off gate that, when enabled, prevents Engineer assignment until staff have confirmed both `Instruction complete` and `Images complete`; the gate must be changeable without a code deployment. The application still shows missing and contradictory values, but the first MVP does not enforce a hard-coded universal or principal-specific field matrix. Principal identity before reference allocation and a clear original-report assessment for a standalone Audit remain separate identity rules. Instruction date defaults to the current date when absent; Case/PO and Audit references are calculated; inspection address may validly be `Image Based Assessment`; DVLA/DVSA and MOT-derived values are enriched or calculated when available. Financial fields planned for later are not required in the first MVP.

**Do different case types require different fields or workflows?**  Yes. The active case types are Inspection, Audit, and Inspection + Audit. A standalone Audit includes the other firm's original Engineer report and derives its `a.` or `ap.` reference from that report's repairable/total-loss assessment. Inspection + Audit begins as an Inspection and creates its Audit reference and Box subfolder after Collision Engineers' own Engineer assessment. Diminution and Commercial must be represented in the architecture but are deferred beyond the first build.

**What searches and filters must users have?**  Search and filter by Case/PO, vehicle registration, claimant, claim number, provider/principal, case stage/status, assigned Engineer, received date, instruction date, and date range. Users must also be able to filter by intake origin: image-initiated or instruction-initiated. The original intake source must remain available after related records are matched or merged.

**What dashboards, reports, exports, or management information are required?**  The first MVP requires a case-intake dashboard modelled on the supplied mockup. It should provide operational tiles rather than a general analytics dashboard:

- Case queues: `Not ready`, `Review`, and `Held`
- Inbox queues: `Receiving work`, `Queries`, `Other`, `Needs sorting`, and the manual `Blocked intake` filter
- Calendar activity: `In today`, paired `Sent to Engineer` today/week totals, and paired `Reports sent` today/week totals
- A visible last-updated time and manual refresh action

`Not ready` contains incomplete cases being chased. `Review` contains complete cases awaiting the required pre-assignment approval. `Held` is a manual case pause with a required reason; it blocks progression and recurring chasers while due dates remain visible. `Blocked intake` is pre-case and manually chosen by staff, creates no case/reference, and retains the source, reason, warning, and retry action. `Needs sorting` remains for uncertain classification or association rather than a known blocker.

Dashboard calendar days run from Europe/London midnight to midnight; weeks run Monday midnight to the following Monday midnight. `In today` counts cases created in the current London calendar day. `Sent to Engineer` counts each case once. In the first MVP, its evidence is the first successful generation of the EVA JSON and image export: this is an explicit proxy and does not prove EVA receipt. A future EVA replacement records actual Engineer assignment instead. `Reports sent` counts every successfully sent report, so one case may contribute more than once. Evidence unlink/relink recomputes these counts.

Each tile or filter shows its current count and opens the corresponding work view. The first MVP must also export the case's structured JSON together with its stored images for transfer into EVA. This is an interim integration until EVA's API becomes usable.


## 6. Documents, photographs, and evidence

**What file types will be uploaded or generated?**  Inbound Outlook email and email-body content, WhatsApp content, PDF, DOC/DOCX, MSG, and vehicle images. The first MVP extracts EML, PDF, DOCX, JPEG, and PNG content. Legacy DOC and MSG sources are retained with provenance and sent to `Needs sorting`; automated extraction of those two containers is deferred. Generated or assembled outputs include structured case JSON for EVA, downloaded image bundles, Engineer reports, and related case correspondence. Instruction emails/documents, vehicle images, and final Engineer reports are stored in the case's Box folder.

**Expected typical and maximum file size:**  maybe 5-10mb maximum

**Expected number of files per case:**  anything from 2-3 to 20+

**Must photographs preserve original files and metadata?**  no

**Must files become immutable after submission, approval, or case closure?**  When a case is closed, its files become read-only at the application level. An authorised staff user must reopen the case before files can be changed, replaced, or removed. Reopening and subsequent file changes must be recorded in permanent action history. This is a reversible workflow lock rather than irreversible storage immutability.

**Should revised documents create a new version or replace the previous file?**  Revised documents create a new version and retain every previous version. Replacement must not destroy the earlier content, and each revision is recorded in permanent action history.

**Who may view, upload, download, replace, or delete evidence?**  Authenticated Administrator, Engineer, and User roles may view, upload, and download case evidence. They may revise or remove evidence only while the case is open or has been reopened. Revisions retain previous versions, removals are logical rather than permanent, and all actions are recorded in permanent action history. External clients have no access.

**Are virus scanning, OCR, image processing, redaction, or AI classification required?**  V1 automatically reads a vehicle registration from ordinary vehicle images while keeping each original image and any suggestion reviewable; the implementation mechanism is not inferred here. Scan-like PDF OCR and broader vehicle-image/damage AI or vision assistance are V2. In-app AI is V3, and AI-assisted email/document/address behavior is V3 only if rule-based behavior is insufficient. Guided capture remains `Unclear`. Automated malware scanning, redaction workflow, and the other `DOC-09..15` governance workflows are `Never`, not deferred backlog.

Automatic VRM reading and broader AI/vision assistance are separate capabilities. Any selected recogniser still requires representative accuracy and false-positive evidence, provenance, operator review, licence/cost/security approval, and a real caller. This decision adds no dormant client, endpoint, queue, configuration, or feature flag.

**Are digital signatures or evidential chain-of-custody records required?**  No. Permanent action history and retained file-version history are sufficient.


## 7. Communications and tasks

**Should the system send or receive email? Describe the mailboxes and flows.**  Yes. V0 runs the real provider-specific instruction-identification rules against ignored working-copy `.eml` files in the local evaluator. V1 automatically ingests staff-forwarded work from `instructions@collisionengineers.co.uk`; forwarding means the transport sender may be a staff member, so source provenance and strong instruction content must not be replaced by sender-only classification. V2 extends the same Core-owned policy across `desk@collisionengineers.co.uk`, `engineers@collisionengineers.co.uk`, `info@collisionengineers.co.uk`, and `instructions@collisionengineers.co.uk`, adds the full email workspace and suggested actions, and lets staff confirm recommended Outlook-folder moves. General compose/reply/forward/send in the app is `Never`; automatic chasers are V3 and automatic reports are V3+ as separate capabilities.

The detailed Received taxonomy and its confirmed examples/subtypes are settled:

| Received family | Confirmed examples or subtypes |
| --- | --- |
| `General` | autoreply, undeliverable, and acknowledgements such as “thank you” |
| `billing` | payment notifications, remittances, invoice requests, billing query, and general billing |
| `new-instruction-received` | initial work instructions; Audit, Diminution, Inspection, new client, and website enquiry |
| `non-client-related` | internal/company messages from tools, services, software, and similar sources |
| `in-progress-cases` | cancellation, case update, client chasing for update, and other ongoing-case correspondence |
| `post-report-emails` | queries, disputes, amendment requests, and similar post-report correspondence |
| `pre-instruction-emails` | Triage requests, handling requests before formal instruction, and images received before instructions |
| `internal-cc` | internal copied correspondence |

Sent families are `Report sent`, `case-rejected`, `query-sent`, and `additional-image-request`. Reply is not a standalone recorded type: Collision Engineers' replies mirror the underlying Received category with reply context, and incoming replies to Sent messages mirror the underlying Sent category with reply context. The V0 evaluator also offers `Other`, requiring a new category name and reasoning. These are the confirmed taxonomy claims derived from the directly selected reference evidence in `docs/reference/CollisionSPikeCurrenttree.txt`; the source remains unchanged and does not become authority for unrelated legacy behavior.

Detailed classification, operational queues, Triage routing, and Outlook folder destinations are distinct facts. Mailbox categorisation and all automatic email matching remain one combined open research decision routed through `docs/plans/mailbox-categorisation-and-email-matching/README.md`. The research must now settle the V0 instruction predicates and governance needed by the real evaluator, then the V1 exact matching dependencies and V2 expansion. Approved rules remain one Core-owned policy rather than copies in Web, Worker, API, MCP, or mailbox adapters. Do not invent predicates or add a generic rule engine or dormant configuration model before the applicable decision is accepted.

**Must email and attachments be automatically associated with cases?**  Yes, but all automatic email matching remains inside the combined open research decision. Once accepted, related emails and attachments may be associated automatically only where the approved policy proves a definitive match. Uncertain matches go to `Needs sorting` for staff review. This does not change the separately settled Triage-to-case linking rule.

**What proves that Collision Engineers sent a report?**  One exact Outlook Sent item from a mailbox on the Administrator-maintained allowlist of approved shared and individual staff mailboxes. V1 detects and links this evidence but does not send the report. Automatic exact-item matching is a V1 gate owned by the combined research; when evidence is absent or ambiguous, an authorised staff user may link the exact item with a required reason. Subject-only evidence is insufficient. Outlook `sentDateTime` is authoritative; discovery/link times remain separate. Any staff role may unlink/relink with a reason and recompute dependent events/counts, while confirmed evidence remains final if Outlook later moves or deletes it. Automatic report sending is V3+ and cannot begin without a separately accepted sending contract.

**Are SMS, Teams, portal notifications, or other channels required?**  V1 keeps WhatsApp manual and permits staff to add relevant received material to the case. Automated WhatsApp ingestion/coexistence is V3. SMS, Teams, customer/claimant portals, and external-role application accounts are `Never`.

**What tasks, reminders, deadlines, escalations, or service-level timers are required?**  Entering `Not ready` schedules the first chase at the same Europe/London local clock time exactly seven calendar days later; subsequent outstanding chasers follow the same seven-calendar-day cadence. Entering `Held` preserves the prior state and any remaining local-clock chase interval. Release offers the prior state or `Review`; returning to `Not ready` resumes the preserved remainder, while choosing `Review` ends the missing-information chase. Receiving the required material or entering any terminal case state also stops future chasers. Extract the inspection date, or equivalent deadline stated in the instructions, as the case's `Due by` date and keep it visible while held.

**Who may create, assign, complete, or cancel tasks?**  Authorised Administrator, Engineer, and User roles may create, assign, complete, or cancel case tasks. The system may create recurring chase reminders automatically. All task actions are included in permanent action history.

**Which communications need templates and approval?**  In V1 the application generates a clickable message that staff copy into email or WhatsApp and may include a Box file-request link. No separate approval is required because staff send it manually. Automatic chasers are V3 and automatic reports V3+; each later sender requires a separate authority, confirmation, idempotency, recovery, and acceptance contract.


## 8. Integrations

Complete the required integrations and identify the system that remains authoritative.

| System or service | Required for first release? | Information exchanged | Direction | Authoritative system | Contact/owner |
|---|---|---|---|---|---|
| Microsoft 365 / Outlook | Yes | Email, body content, attachments, sender/recipient data, timestamps | V1 staff-forwarded `instructions@` intake and exact Sent evidence; four-mailbox management in V2 | Microsoft 365 for mailbox content; application for classification and case association | Collision Engineers / Alex |
| Box | Yes | Case folders, instruction emails/documents, vehicle images, Engineer reports, and file requests | Bidirectional | Box is the long-term file store; application owns case metadata and links | Collision Engineers / Alex |
| Accounting or invoicing | No; plan for later | Invoice amount and future accounting data | To be defined | Future accounting system to be selected | To be defined |
| DVLA / DVSA | Yes | Vehicle and MOT details, including mileage information when available | Inbound lookup | DVLA/DVSA source data; application stores the case snapshot | Collision Engineers / Alex |
| Mapping or location | V1 | Inspection-address mapping/prediction; AI-assisted suggestions are separately conditional V3 | Inbound lookup or rule-based suggestion | Application stores only operator-accepted case values and provenance | Collision Engineers / Alex |
| OCR / document processing | V2 for scan-like PDF OCR; V1 for automatic VRM reading | Reviewable extraction, retained images, scan-like PDF OCR, and registration suggestions | Inbound processing | Original sources remain authoritative; accepted fields remain application data | Collision Engineers / Alex |
| AI services | V2/V3/V3+ only as individually allocated | Image/damage assistance, conditional classification/extraction/address suggestions, staff assistant, and AI Assessor | To be defined per capability | Original sources and staff-approved application data remain authoritative | To be defined |
| EVA | V1 export; V3+ API/replacement | Structured case JSON and image bundle; later assignment/engineering replacement | V1 manual handoff; later API only if usable and approved | EVA remains authoritative for named assignment and downstream engineering until replaced | Collision Engineers / EVA vendor |
| WhatsApp | Manual V1; automated V3 | Instructions, chaser messages, and vehicle images | Manual then separately approved automated intake | WhatsApp retains channel history; accepted material is added to the application/Box | Collision Engineers |
| Tractable / Ravin | No; potential future route | Guided claimant image capture | Inbound later | To be defined | Collision Engineers / vendor |
| Audatex and valuation services | No; plan for later | Estimate and valuation data | To be defined | External service remains authoritative until replacement strategy is agreed | Collision Engineers / vendor |

**Are test accounts, API specifications, credentials, or vendor contacts available for these integrations?**  Working access is available for the required first-MVP integrations except EVA. EVA API documentation is available under `docs/reference/EVA/EVA_API_SCHEMA`, but its usable API access depends on the EVA vendor. Until that is resolved, the system exports JSON and images for manual transfer. Integration secrets must never be committed to the repository; use Infisical or Azure Key Vault for secret custody.

**What are the provider API and MCP boundaries?**  The V2 provider HTTP API uses separately issued principal-scoped client IDs and opaque secrets. It accepts idempotent instruction/attachment submissions and lets a principal retrieve only its own receipt, processing status, and resulting Case/PO. It never creates external application accounts or exposes general case reads/workflow mutation.

MCP is a separate V1 internal staff surface, primarily for Claude Desktop. Each staff member authorises it through CollisionSpike using OAuth, and every call uses that person's current role and permanent action-history identity. V1 MCP covers role-authorised case, document, and intake-queue actions through the same Core use cases as the staff UI; broader classified-email actions are V2. It never exposes account/role administration, principal configuration, credential management, cloud operations, or permanent deletion.


## 9. Existing data and migration

**Must any data from the previous application be migrated?**  No. CollisionSpike v2 starts fresh and does not import legacy application cases.

**Which cases, users, documents, emails, action-history records, or reference data must be retained?**  No legacy application cases, users, action-history records, or application state are migrated into v2. Existing historical documents and operational records remain in their existing Box, EVA, Outlook, spreadsheet, or network-drive locations. Required principal codes and other operational reference data are recreated cleanly for v2 rather than migrated wholesale.

**Where is the existing data currently stored?**  Box stores long-term case files; EVA stores current case-management, estimating, valuation, and report data; Outlook stores email; Excel acts as the current ready/not-ready holding pen; and unmatched WhatsApp images may temporarily be stored on the network drive.

**Approximately how much data is involved?**  No legacy dataset is being migrated, so a migration volume estimate is not required. New v2 storage capacity is addressed by the scale and growth questions.

**Can the old system be made read-only after cutover?**  No read-only legacy service is required. The previous CollisionSpike application will be shut down completely when v2 cuts over.

**How long must the old system remain available?**  Only until the agreed v2 cutover. It can be shut down after cutover validation.

**Who will validate that migrated data is complete and correct?**  Not applicable because no legacy data migration will occur. Collision Engineers will validate the fresh v2 workflow before cutover.


## 10. Data protection and governance

**What categories of personal, sensitive, financial, or legally privileged data will be stored?**  Operational case content can include claimant names and contact/address information, provider and insurer details, claim references, vehicle registrations and details, accident circumstances and dates, inspection locations, emails, documents, vehicle images, Engineer reports, and post-report correspondence. Repair estimates, valuations, and invoice amounts are planned for later. No special data-classification feature is required for the first MVP.

**What is the retention period for cases, documents, logs, backups, and action-history records?**  No application-enforced retention or automatic deletion period is required for the first MVP. Box remains the long-term document store, and v2 case and action-history records are retained unless Collision Engineers defines a later policy.

**When may data be deleted, and who can authorise deletion?**  Cases are never permanently deleted through the application and may only be archived. File removals are logical and retain version history. Closed cases must be reopened by authorised staff before changes can be made, and all such actions are recorded in permanent action history.

**Are legal holds or litigation holds required?**  No legal-hold feature is required for development or the first MVP.

**Is a Data Protection Impact Assessment required or already available?**  No DPIA deliverable is in scope for application development or the first MVP.

**How should subject-access, correction, export, and erasure requests be handled?**  No dedicated in-application workflow is required for the first MVP. Any external legal or management process remains outside the development scope unless Collision Engineers later adds a requirement.

**Which activities require security or compliance alerts?**  Use standard operational security alerts for authentication or authorisation failures, privileged role/configuration changes, ingestion/integration failures, application availability, and unexpected Azure cost. Automated malware scanning is `Never`, so no scanner-specific detection or alert workflow is planned. No additional data-compliance alert workflow is required for V1.

**Are there contractual, insurer, solicitor, ISO, Cyber Essentials, or other compliance requirements?**  No additional compliance implementation requirement has been supplied for development or the first MVP. This development-scope decision does not replace any external legal or organisational obligations managed by Collision Engineers.


## 11. Scale, performance, and availability

**Expected number of users at launch:**  Approximately 8 Collision Engineers staff users.

**Expected concurrent users:**  Design for all 8 launch users to be active concurrently.

**Expected new cases per day or month:**  Approximately 2,000 new cases per month (about 24,000 per year).

**Expected annual data growth:**  Plan for approximately 24,000 new case records and roughly 48,000 to 480,000+ associated files per year based on the stated 2-3 to 20+ files per case. At the supplied 10 MB maximum per file, the conservative upper storage envelope is about 4.8 TB per year before allowing for versions. Box remains the long-term file store; Azure application storage should primarily hold structured metadata, processing state, action-history records, and any necessary transient artifacts. Measure actual case-folder sizes after launch and adjust the forecast.

**Required operating hours:**  Automated mailbox ingestion and case processing operate continuously. Staff-facing use is expected primarily during Collision Engineers business hours, but the application should remain available outside those hours unless undergoing planned maintenance.

**Maximum acceptable planned downtime:**  A short planned interruption during a production release is acceptable for V1. Validate in the shared development/integration environment, deploy directly to production B1 outside office hours, wait for health checks, and run smoke tests. Keep the previous immutable artifact for rollback and notify staff. Standard S1 and deployment slots are `Never` under the current product boundary.

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

**Who needs emergency operational access to production?**  Alex initially, plus any specifically designated Administrator or Azure operator added later. Emergency application and Azure actions must remain attributable in the relevant permanent action history, security logs, or Azure activity logs.


## 13. Monitoring, support, and operations

**Who will support the application during business hours?**  Alex provides first-line application support.

**Who should receive security, availability, failure, and cost alerts?**  Alex initially. Additional recipients may be added later through monitoring configuration without code changes.

**What response times are expected for critical incidents?**  Critical incidents should be acknowledged immediately while Alex is in the staffed office. Outside staffed hours, respond as soon as reasonably possible. The first-MVP service-restoration target remains four hours.

**Which business operations must have dashboards or alerts?**  Dashboard the agreed case and inbox queues plus today/this-week throughput. Alert on mailbox ingestion failures, document/OCR processing failures, Box folder/file-request failures, unmatched or repeatedly failing case associations, overdue due-by dates, failed recurring-chase generation, EVA export failures, application health, authentication anomalies, and unexpected Azure cost.

**How often should backup restoration and disaster recovery be tested?**  Prove database restore, the 15-minute RPO, and the documented four-hour recovery path before V1 acceptance and after a material persistence/release change where the owning change requires it. A recurring quarterly restore exercise is `Never`.

**Who may deploy to development, integration, and production?**  Alex controls releases initially. An authorised operator deploys committed Bicep through Azure Developer CLI from an authorised terminal. Unfinished cloud integration work uses the shared development/integration environment. There is no staging environment. After approval, production deploys directly to B1 outside office hours and is health-checked and smoke-tested immediately. GitHub Actions/OIDC deployment is `Never`.

**Who approves production releases?**  Collision Engineers management provides business approval; Alex performs or authorises the technical production release.


## 14. Budget and commercial constraints

**Target monthly Azure budget during development:**  No fixed monthly budget. Use the lowest practical Azure tiers that still support required development and integration testing, remove unnecessary resources, and configure budget alerts from the approved cost forecast.

**Target monthly Azure budget in production:**  No fixed monthly ceiling has been set. Size for the stated workload and reliability targets, favour managed services and simple architecture, document estimated recurring cost before deployment, and configure budget/forecast alerts.

**Is increased cost acceptable for zone redundancy, private networking, stronger backups, or disaster recovery?**  Multi-region failover, zone redundancy, and private networking are `Never` under the current product boundary. Use a cost-conscious single-region design with secure authentication, least-privilege access, encryption, managed backups, health checks, monitoring, and the V1 recovery proof.

**Are there licences or existing Microsoft agreements we should use?**  Reuse Collision Engineers' existing Microsoft 365, Azure, Box, EVA, Audatex, and other vendor accounts/licences where applicable. No additional enterprise agreement or licensing constraint has been supplied; confirm commercial/API entitlement before enabling each vendor integration.

**Are there fixed procurement or vendor restrictions?**  No fixed procurement or vendor restriction has been supplied. Prefer existing approved services and avoid introducing a new paid platform unless it provides necessary functionality and its recurring cost is documented.


## 15. First-release scope

### Must have

- Self-managed CollisionSpike staff usernames/passwords, account administration, and Administrator/Engineer/User roles
- QDOS principal configuration, provider-specific Case/PO sequencing, Audit prefixes, Box naming, and Inspection + Audit secondary references
- Active V1 support for Triage, Inspection, Audit, and Inspection + Audit
- Automatic ingestion of staff-forwarded work from the `instructions@collisionengineers.co.uk` shared mailbox with stable source identity and bounded visible recovery
- The V0-proved Core classification owner identifying authorised instruction email for the V1 intake path; non-definitive or unsupported material remains visible in `Needs sorting`
- Extraction of required case details from PDF, DOCX, and freehand EML instructions; legacy DOC and MSG remain retained in `Needs sorting` without a reference until their V2 automation
- Automatic vehicle-registration reading for ordinary vehicle images, with original evidence and reviewable provenance retained; scan-like PDF OCR is V2
- Automatic case creation from definitive authorised instructions through the shared case-acceptance rules
- Manual review and linking of image-led intake using vehicle registration; automatic image/instruction matching is V2
- Manual case creation and manual upload of instructions, correspondence, documents, and images
- Manual linking, mistaken-merge reversal, wrong-principal `Created in error` closure with a linked replacement case, cancellation, reasoned reopening to valid nonterminal states, and archive workflows with permanent action history
- Full staff case management and editing across intake, chasing, ready/review, the tracked inspection/report stage, and all four V1 terminal outcomes; post-report query/dispute work is V2
- Configurable backend enforcement of staff-confirmed `Instruction complete` and `Images complete` before Engineer assignment, without a hard-coded field matrix
- A review/approval gate before Engineer assignment; no pre-send report review gate
- Inspection-address capture using either the physical vehicle/repairer address or `Image Based Assessment`
- Case due-by dates extracted from instructions and recurring seven-day chase reminders while information is missing
- Automatic Box case-folder creation and long-term storage of instruction emails/documents, images, correspondence, and Engineer reports
- Retained document versions, closed-case file locking, and reopen-before-edit behaviour
- Automatic Box file-request creation and copyable chaser messages for staff to send manually
- Case-intake dashboard with `Not ready`, `Review`, `Held`, `Needs sorting`, manual `Blocked intake`, a separate Triage route, and London-calendar `In today`, `Sent to Engineer`, and `Reports sent` counts; categorised `Receiving work`, `Queries`, and `Other` queues are V2
- Search/filter by Case/PO, VRM, claimant, claim number, principal, stage/status, Engineer, dates, and image- versus instruction-initiated origin
- DVLA/DVSA lookup when vehicle details are absent
- Mileage estimation from MOT data when available
- Successful operator-approved structured case JSON plus stored-image export for every active QDOS case type; this records CollisionSpike's once-per-case `Sent to Engineer` handoff/proxy but not EVA receipt or named assignment
- Exact Outlook Sent-item report evidence and exact reply-chain Triage evidence, including the V1 automatic matchers after the combined research is accepted and the settled manual report fallback
- OAuth-authorised internal staff MCP for Claude Desktop, exposing V1 role-authorised case, document, and intake-queue actions through the same Core use cases while excluding security, configuration, cloud, and permanent-delete operations
- Application and integration health monitoring, operational alerts, recovery controls, permanent action history, security logs, and content-safe telemetry
- Direct authorised-terminal production deployment using committed Bicep through `azd`, with explicit migration, health, smoke, and prior-artifact rollback boundaries

### Should have

- None. First-MVP functionality is either mandatory or explicitly deferred; there is no separate optional-but-desirable tier.

### Could have later

- V1.x activation of additional providers through the same bounded intake/case workflow
- V2 provider API, four-mailbox classification/email workspace, suggested folder moves, mail mutations, automatic email/image associations, DOC/MSG extraction, scan-like PDF OCR, post-report query/dispute work, and image/damage AI or vision assistance
- V3 Diminution and Commercial cases, automated WhatsApp ingestion, automatic chasers, and the in-app assistant
- V3 AI-assisted email, document, or address suggestions only if rule-based behavior is insufficient
- Conditional V3+ direct EVA API use, followed by EVA assignment/estimating/valuation/report replacement, finance workflows, integrated services, automatic reports, and the staff-selected AI Assessor option
- Collision Engineers guided capture, Tractable/Ravin integration, and a custom domain remain `Unclear`

### Explicitly out of scope

- `Never`: predecessor case/application-state import, predecessor operation after cutover, and predecessor-code reuse
- `Never`: SMS, Teams, customer/claimant portal, external-role accounts, and public registration apart from the principal-scoped provider API
- `Never`: staff mobile UI, malware scanning, redaction, digital signatures, automated retention/deletion, legal hold, subject-request workflow, and dedicated DPIA/compliance workflow
- `Never`: GitHub Actions/OIDC deployment, separate QA/UAT/staging/training/demo environments, deployment slots/S1 hosting, private networking, zone redundancy, multi-region failover, and recurring quarterly recovery exercises
- Current V1 excludes V2/V3/V3+ and `Unclear` capabilities without converting them into permanent exclusions

**What is the single most important end-to-end workflow the first release must prove?**  Every active QDOS case type—Inspection, standalone Audit, and Inspection + Audit—must travel through the real V1 path: staff-forwarded `instructions@` or approved manual/image intake; instruction identification; bounded EML/PDF/DOCX extraction with legacy DOC/MSG retained visibly; automatic VRM reading with reviewable evidence; definitive or staff-resolved acceptance; correct shared-sequence Case/PO and Audit identity; Box custody; seven-day missing-information chasing; vehicle/MOT enrichment; completeness and staff review gates; successful structured JSON/image export to EVA as CollisionSpike's `Sent to Engineer` proxy; tracked inspection/report progress; exact report evidence; and terminal closure, reopening, or archive with permanent action history. V2 post-report dispute work, automatic image matching, scan-like PDF OCR, and broader email management are not V1 dependencies.

**Who will perform acceptance testing and approve that workflow?**  Alex performs technical and operational acceptance testing with relevant Collision Engineers staff. Collision Engineers management provides final business approval for production release.


## 16. Additional constraints and decisions

**Are there mandated technologies, suppliers, standards, or existing assets that must be used?**  Host the complete application on Microsoft Azure. Development and cloud operations run on Windows through PowerShell 7 using GitHub, Azure CLI, Azure Developer CLI, and Bicep-based infrastructure as code. Continue using Microsoft 365/Outlook for email, Box as the long-term case file store, DVLA/DVSA for vehicle/MOT enrichment, and EVA as the interim downstream case/engineering system. Store development/deployment secrets only in Infisical or Azure Key Vault. Treat `docs/operator-notes` as read-only, absolute business authority and use only genuine repository-provided example emails, documents, instructions, and images for test data. No application language/framework is mandated by the business requirements; the technical stack is finalised in the architecture/deployment plan.

**Are there technologies or approaches that must not be used?**  Do not reuse the previous `cedocumentmapper` implementation or reproduce the previous system's duplicated functions, spaghetti code, drift, or technical debt. Do not create synthetic emails, images, or instructions. Do not commit secrets or readable passwords. Do not expose internal Azure/function terminology or labels such as `dev copy` in the user interface, and do not make the application narrate obvious controls with unnecessary explanatory sentences. Use the reserved business terms `Audit` and `Triage` only for their actual Collision Engineers meanings. Do not edit `docs/operator-notes`.

**What concerns you most about rebuilding this system?**  Avoiding the previous application's huge technical debt, spaghetti code, duplicated functions, behavioural drift, and cumbersome structure while still delivering the full required functionality quickly.

**Anything else that should influence the product or architecture?**  Functionality and a complete QDOS workflow are the first-MVP priority. Use a simple modular architecture, logical names that reveal purpose, one authoritative implementation of each business rule, configurable backend policy where requirements may change, complete accountability through permanent action history and security logs, and explicit future extension points for deferred integrations and features. Cost should be controlled, but functional delivery takes priority over premium resilience/networking features in the first MVP.
