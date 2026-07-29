# Operator authority

> **Source labels:** `pre-consolidation operator source: README`; `pre-consolidation operator source: product-requirements — engineering-constraints`

This document is Collision Engineers’ single binding authority for business requirements, processes, operating knowledge, product requirements, and operator practices.

Repository maintainers are authorized to maintain and organize repository documentation, including this authority, provided that they:

- preserve every material business statement;
- keep changes reviewable in Git history; and
- stop for user resolution if authoritative statements materially conflict.

Code, references, plans, predecessor behaviour, and tool availability do not override this authority. Everything recorded here is authoritative operator truth.

## Evidence and delivery states

> **Source labels:** `pre-consolidation operator source: README`; `pre-consolidation operator source: systems-and-integrations — README`; `pre-consolidation operator source: systems-and-integrations — cedocumentmapper`

These states must remain distinct:

| State | Meaning |
| --- | --- |
| Intended | Required or desired by the operator. It does not establish that product work exists. |
| Implemented | Product code exists and is connected to a real caller. Do not claim this state from designs, schemas, predecessor code, isolated components, or operator intent alone. |
| Caller-proved | Evidence identifies a real caller using the implemented path. |
| Deployed | The caller-proved implementation is present in a named operating environment. |
| Accepted | The operator or designated authority has separately accepted the behaviour. Deployment is not acceptance. |

A current business workflow does not prove a corresponding Pegasus integration. A listed current system does not by itself authorize or require an integration in the active release. No external or cloud operation is authorized merely because its tool is available.

# Ordered business process

> **Source labels:** `pre-consolidation operator source: business-process — case-lifecycle`; `pre-consolidation operator source: business-process — intake-and-work-instructions`

## Stage 0 — Triage

Triage does not technically count as a case, but its emails must be stored. A work provider asks Collision Engineers to assess whether a vehicle is roadworthy or unroadworthy.

Triage may record these independently optional findings:

- Roadworthy or Unroadworthy
- Repairable or Total loss

Neither finding category is independently mandatory, but at least one must be populated when a finding is recorded.

Triage is:

- a distinct inbox classification or label;
- a separate pre-case reference record;
- optional and not guaranteed to progress into a full case; and
- potentially followed later by instructions for the same vehicle.

Triage is never:

- a case state;
- definitive or final; or
- a decision input for a subsequent case.

A case’s `has Triage` value is Boolean/reference-only. Triage findings have no bearing on the Case/PO/reference, workflow, final outcome, Engineer report, Audit suffix or allocation, or any other decision. The Engineer report remains definitive.

## Stage 1 — Receiving instructions or images

An intake may begin in either of two ways:

1. Collision Engineers receives Work Instructions sent by, or on behalf of, a work provider.
2. Collision Engineers receives vehicle images, often from a repairer, garage, bodyshop, or similar business. The associated work provider may initially be unclear or unknown.

Collision Engineers prepares sufficiently evidenced work to be passed to an Engineer.

An image-only arrival may be described operationally as an “image-initiated case” and may be logged in the holding process. Technically, it remains pre-case while the provider, instruction type, or case association is ambiguous. Images alone must not create a definitive association. They may be linked automatically only on a definitive match, or linked manually by staff.

A required image set should ideally show:

- the sustained vehicle damage; and
- a clear view of the vehicle registration.

## Stage 1.5 — Chasing missing information

If a case is incomplete, Collision Engineers chases the relevant party for the missing details, images, or documents. The case can proceed when the required material has been obtained.

## Stage 2 — Inspection

Collision Engineers does not physically inspect vehicles. An Engineer performs a desktop inspection and prepares a report containing:

- the vehicle’s roadworthiness determination;
- whether the vehicle is repairable or a total loss; and
- an estimated repair cost.

The Engineer report, not any earlier Triage finding, is definitive for roadworthiness and repairability or total-loss determinations.

## Stage 3 — Post-report

The Engineer sends the report to the provider. Queries or disputes may then be received, generally by email, from:

- the provider;
- a third-party insurer; or
- the claimant.

The Engineer must respond to those queries or disputes.

# Intake authority

> **Source label:** `pre-consolidation operator source: business-process — intake-and-work-instructions`

## Required instruction data

A Work Instruction contains details of a claimant involved in a road traffic accident. Capture:

| Field | Rule |
| --- | --- |
| Work Provider | Also referred to as the principal. |
| Claimant Name | Extract from the instruction. |
| Claim Number | External reference number. |
| Vehicle Registration | VRM. |
| Vehicle Make | Extract from the instruction or obtain through an authorized lookup capability when absent. |
| Vehicle Model | Extract from the instruction or obtain through an authorized lookup capability when absent. |
| Vehicle Mileage | Extract when supplied; estimation from MOT data is a required capability when available. |
| Accident Circumstances | Extract from the instruction. |
| Date of Incident | Extract from the instruction. |
| Instruction Date | Use the document value; if absent, default to the current date. |
| Inspection Address | Apply the inspection-address rules below. |

## Authoritative channels and formats

Email through Outlook supplies the vast majority of Work Instructions and is the primary intake-automation target.

| Channel | Accepted forms |
| --- | --- |
| Email | PDF attachment, DOC/DOCX attachment, or freehand email text |
| WhatsApp | PDF attachment, DOC/DOCX attachment, or text typed in WhatsApp |
| Provider API | Future intake channel into the Collision Engineers system |

## Provider and intermediary routing

The sender route and the underlying work provider are related but distinct facts.

1. If an email was forwarded by Collision Engineers staff from an `@collisionengineers.co.uk` address, use the original forwarded sender for route identification. Retain the staff forward as transport provenance.
2. Determine whether the effective sender belongs to an accepted direct-provider route or an intermediary route.
3. Extract attachments, email body, and subject before applying the identified route’s rules.
4. For a direct-provider route, use that provider’s rules to determine instruction type and any related case.
5. For an intermediary route, use the intermediary’s rules to determine the underlying provider, instruction type, and any related case.

A provider may send some work directly and other work through an intermediary. Those are separate routes to the same provider. An intermediary email must not be interpreted as though it were a direct provider email.

Case association must follow the identified route’s rules. Providers do not generally quote a Collision Engineers Case/PO, so Case/PO is never the universal first match. It may be used only as a lowest-priority fallback where the route’s evidence supports doing so.

Ambiguous provider, instruction-type, or case evidence remains pre-case for staff sorting.

# Inspection address

> **Source label:** `pre-consolidation operator source: business-process — inspection-address`

An inspection address is report data; it does not imply that Collision Engineers physically attended the vehicle. Every assessment is a desktop inspection.

The report has two permitted inspection-address treatments:

1. Record the physical location of the vehicle, such as the client’s address or the garage or repairer location.
2. Record the exact text **“Image Based Assessment”** instead of an address.

Some providers must always use **“Image Based Assessment”**. For many others, the vehicle’s physical location is important and must appear on the report even though the Engineer is inspecting remotely.

Current address determination is not handled ideally:

- some instruction documents identify the vehicle location;
- otherwise, Admin staff often rely on provider-specific knowledge;
- in practice, one knowledgeable person may infer the location from images or know the repairer commonly used by that provider.

The required inspection-address helper is intended to reduce this dependency by suggesting addresses from provider usage frequency, accident location when available, and image or vision AI. That helper is outside `0.1.0-alpha.1` and is not established here as implemented.

# Case references and types

> **Source label:** `pre-consolidation operator source: business-process — case-types-and-references`

## Case/PO number

A Case/PO number is Collision Engineers’ internal reference. It is a simple, uniform reference system across all providers.

Case type primarily affects how the Case/PO number is handled.

## Case types

| Case type | Binding meaning and boundary |
| --- | --- |
| Inspection | Standard case type. Collision Engineers receives instructions, prepares the case for an Engineer, and returns the Engineer’s report to the provider. |
| Audit | Another engineering firm has already inspected the vehicle. Collision Engineers receives instructions and the original report, and its Engineer audits or double-checks that firm’s work. |
| Audit + Inspection | Collision Engineers first completes its standard Inspection process and then carries out an Audit on that same inspection. |
| Diminution | Retained for provenance but deferred. Cases are not frequent enough to include in a first build. |
| Commercial | Retained for provenance but deferred for the same reason as Diminution. Cases are not frequent enough to include in a first build. |

# Reserved terms

> **Source label:** `pre-consolidation operator source: business-process — reserved-terms`

The following terms have specific Collision Engineers business meanings and must not name unrelated functions, code, or concepts:

- **Audit**
- **Triage**

For example, a generic inbox-sorting function must not be called “triage,” because Triage is a distinct kind of work received by Collision Engineers. The reserved list may be extended over time.

# Required product capabilities

> **Source label:** `pre-consolidation operator source: product-requirements — required-capabilities`

The IDs below are stable requirement identifiers and preserve the source order. They must not be renumbered merely because requirements are deferred or later retired.

This table records binding product needs, not implementation, caller, deployment, or acceptance status.

| Stable ID | Required capability | Boundary or dependency |
| --- | --- | --- |
| `CAP-001` | Automatically ingest emails from Outlook. | The full target covers all four Collision Engineers mailboxes and all received emails. |
| `CAP-002` | Extract required details from documents and emails. | Must respect route-specific provider and intermediary rules. |
| `CAP-003` | Automatically store case material on Box. | Box remains intended long-term storage; staging and custody are distinguished below. |
| `CAP-004` | Identify and categorize all emails automatically. | Business Triage terminology remains reserved and must not be reused for generic classification. |
| `CAP-005` | Provide API functionality for providers. | A future provider API is also an authoritative intake channel. |
| `CAP-006` | Provide MCP functionality. | No supplied source proves a caller, deployment, or acceptance. |
| `CAP-007` | Integrate with estimating and valuation services. | Not in `0.1.0-alpha.1`. Integration methods, particularly for Audatex, remain unclear. |
| `CAP-008` | Automatically create a case when new instructions are received. | Ambiguous provider, type, or case evidence remains pre-case. |
| `CAP-009` | Identify emails related to a case and attach them to that case automatically. | Association must use route-specific evidence; Case/PO is not a universal first match. |
| `CAP-010` | Extract JSON from the logged case and download it with stored images for drag-and-drop into EVA. | Intended to move to EVA API use. That API path is not currently functional and is waiting on EVA developers. |
| `CAP-011` | Allow staff to upload or add cases manually. | Manual creation remains necessary alongside automated intake. |
| `CAP-012` | Automatically link image-initiated and instruction-initiated work when there is a definitive match. | No automatic link is permitted on ambiguous evidence. |
| `CAP-013` | Allow staff to link image-initiated and instruction-initiated work manually. | Provides the resolution path where automation cannot establish a definitive match. |
| `CAP-014` | Provide an in-house guided-capture system. | Not in `0.1.0-alpha.1`. Tractable and Ravin remain evaluation evidence, not the in-house implementation. |
| `CAP-015` | Provide in-app AI features. | Not in `0.1.0-alpha.1`. |
| `CAP-016` | Give staff full case-management capability, including editing case details as necessary. | Intended eventual replacement scope includes EVA’s case-management functions. |
| `CAP-017` | Provide OCR for vehicle registrations and scanned PDFs. | Must support VRM recognition and non-embedded-text documents. |
| `CAP-018` | Provide an inspection-address helper. | Suggestions should use provider frequency, accident location when available, and image or vision AI. Not in `0.1.0-alpha.1`. |
| `CAP-019` | Look up vehicle details through DVLA and DVSA when instructions do not contain them. | Lookup authority does not itself authorize an external operation. |
| `CAP-020` | Estimate mileage from MOT data when available. | An estimate must remain distinguishable from supplied mileage. |
| `CAP-021` | Support email management from within the application. | Intended functionality; not proof of an Outlook caller or deployment. |
| `CAP-022` | Automatically create Box API file requests for use in chaser messages. | Box API access still requires separately authorized external operation. |

# Engineering and interface constraints

> **Source label:** `pre-consolidation operator source: product-requirements — engineering-constraints`

## Environment and tools

All work is carried out in a Windows environment using PowerShell.

Approved tools include:

- GitHub;
- PowerShell and necessary modules;
- Azure CLI and Azure Developer CLI;
- approved Azure skills and tools; and
- Box CLI where applicable.

Approval or availability of a tool does not authorize an external or cloud operation.

## Interface language

- Do not include “dev copy” or similar internal or unusual wording.
- Functions must be apparent from buttons and labels.
- Do not scatter explanatory sentences throughout the application.
- The application must not narrate its own functions.
- Do not expose internal Azure function names, concepts, or wording in the interface.

## Development data boundary

All supplied emails, PDFs, documents, images, and data are permissible for development use. PII, DPIA, retention, and related concerns are outside the development scope defined by this authority.

Do not create synthetic emails, images, or instructions as test data. Use only examples provided in the repository.

## Naming

Functions, code files, Azure services, and Azure resources must have logical names that identify their purpose at a glance. Reserved business terms must not be used for unrelated technical concepts.

# External systems

> **Source labels:** `pre-consolidation operator source: systems-and-integrations — README`; `pre-consolidation operator source: systems-and-integrations — outlook`; `pre-consolidation operator source: systems-and-integrations — whatsapp`; `pre-consolidation operator source: systems-and-integrations — eva`; `pre-consolidation operator source: systems-and-integrations — excel`; `pre-consolidation operator source: systems-and-integrations — tractable-and-ravin`; `pre-consolidation operator source: systems-and-integrations — audatex`; `pre-consolidation operator source: systems-and-integrations — box`; `pre-consolidation operator source: systems-and-integrations — cedocumentmapper`

“Current” records the operator’s present or supplied operational practice. “Target” records intent. “Evidence-only” identifies limitations that prevent the statement from proving a Pegasus implementation, caller, deployment, or acceptance.

| External system | Current | Target | Evidence-only or limitation |
| --- | --- | --- | --- |
| Outlook | Email is received through `desk@collisionengineers.co.uk`, `engineers@collisionengineers.co.uk`, `info@collisionengineers.co.uk`, and `instructions@collisionengineers.co.uk`. Most Work Instructions arrive through Outlook. | Automatically ingest all received emails from all four accounts. `instructions@collisionengineers.co.uk` is the new shared mailbox for the initial MVP, not the full-product boundary. | Current mailbox use does not prove an automated Outlook caller or deployed ingestion. |
| WhatsApp | Primarily used to chase garages for images. Collision Engineers frequently receives images through it. Unmatched images are staged on a network drive until associated with the relevant instructions. It can also carry PDF, DOC/DOCX, or typed-text instructions. | Remain an authoritative intake channel. Image-led work must support definitive automatic linking and staff-controlled manual linking. | The supplied sources do not prove automated WhatsApp ingestion or automatic transfer from network-drive staging. |
| EVA | Current case-management system. Once a case is ready, it is entered into EVA and assigned to an Engineer. EVA wraps estimating systems such as Audatex and Glass’s, contains valuation-service integrations, stores case valuations, and generates the final provider report. The supplied workflow records PDF-to-JSON extraction followed by JSON drag-and-drop into EVA. | Eventually replace all EVA functions and integrations while providing greater business automation. Interim JSON export is intended to move to API use. | EVA offers an API, and supplied details are routed according to its schema under the canonical [reference authority](/docs/reference/README.md). The required API path is not currently functional and is waiting on EVA developers; a schema does not prove a working caller. |
| Excel | Used as a holding pen for instruction-initiated and image-initiated work until ready for EVA. **Not ready** means something is missing, almost always images or instructions. **Ready** means ready to enter into EVA but not yet entered. | No standalone Excel integration target is stated. Product case management is intended to absorb the surrounding workflow over time. | Excel is a holding log, not the long-term document-custody system and not evidence that an image-only entry is technically a definitive case. |
| Box | Long-term storage for instruction emails, instruction documents, vehicle images, and produced Engineer Reports. Each case has its own Box subfolder. | Continue using Box for long-term storage, add automatic storage, and create Box API file requests for chaser messages. | Box custody does not mean every newly received item is immediately in Box. Unmatched WhatsApp images may remain in network-drive staging. No supplied source proves automatic staging-to-Box transfer or an API caller. |
| Audatex | Separate estimating system used by Collision Engineers. It is considered more prestigious and to have more functionality. EVA may wrap it. | Estimating-service integration is required eventually but excluded from `0.1.0-alpha.1`. | Audatex has API features, but the integration methods are currently unclear. |
| Tractable and Ravin | Mobile guided-capture services under evaluation as possible image-intake methods. Claimants use the apps and Collision Engineers receives the images directly. | Inform the future in-house guided-capture capability. | Evaluation does not establish adoption, integration, deployment, or acceptance. The in-house capability is excluded from `0.1.0-alpha.1`. |
| `cedocumentmapper` | The EVA workflow source records Collision Engineers’ predecessor process: a Python extractor with a Tkinter UI extracts PDF details to JSON, which is dragged into EVA. | Do not adopt or reuse this implementation. The operator source rejects it as very poorly designed and made. Pegasus designates PdfPig as the authoritative embedded-PDF extraction method. A bespoke extractor remains deferred until its hardening is separately accepted. | `cedocumentmapper` is predecessor evidence only and is not an implementation source. The PdfPig designation is a binding method rule, but the supplied operator sources do not identify a real Pegasus caller, deployment, or acceptance. |

## Storage and staging interpretation

> **Source labels:** `pre-consolidation operator source: systems-and-integrations — box`; `pre-consolidation operator source: systems-and-integrations — whatsapp`; `pre-consolidation operator source: systems-and-integrations — excel`

The storage statements describe different layers rather than competing custody rules:

- the network drive is temporary staging for unmatched WhatsApp images;
- Excel is a holding log for incomplete or EVA-ready work;
- Box is the intended long-term case-file repository, with one subfolder per case.

A staged image must not be treated as definitively associated merely because it has been received. The supplied sources do not establish that movement from staging into Box is automated.

# Source provenance

> **Source label:** `pre-consolidation operator source: README`

The repository workflow onboarding recorded on 2026-07-27 consolidated earlier fragments by concern. These labels preserve provenance only and are not navigation or competing authorities.

| Original source label | Concern preserved here |
| --- | --- |
| `collision-engineers-process/process-overview.md` | Ordered business process |
| `collision-engineers-process/initial-case-intake/*` | Intake authority |
| `collision-engineers-process/case-guide/*` | Case references and types |
| `collision-engineers-process/inspection-address/inspection-address-overview.md` | Inspection address |
| `reserved-terms.md` | Reserved terms |
| `development-notes/required-features-overview.md` | Required product capabilities |
| `development-notes/rules-to-follow.md` and `dev-tools.md` | Engineering and interface constraints |
| `systems-used/*` | External systems |
| Empty `development-notes/Untitled.md` | Removed because it contained no statement |