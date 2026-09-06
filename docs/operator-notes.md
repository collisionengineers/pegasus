# Operator authority

> **Source labels:** `pre-consolidation operator source: README`; `pre-consolidation operator source: product-requirements — engineering-constraints`

This document is Collision Engineers’ single binding authority for business requirements, processes, operating knowledge, product requirements, and operator practices.

Repository maintainers are authorized to maintain and organize repository documentation, including this authority, provided that they:

- preserve every material business statement;
- keep changes reviewable in Git history; and
- stop for user resolution if authoritative statements materially conflict.

Code, references, plans, predecessor behaviour, and tool availability do not override this authority. Everything recorded here is authoritative operator truth.

## Current v1 decisions — 6 September 2026

These later direct operator decisions govern conflicting earlier descriptions
below. Earlier observations remain historical evidence, not current delivery
claims. The implementation instruction activates the approved three-stream
plan; the planning package's former planning-only boundary is not a product
restriction.

- The v3 interface is the target, subject to the task and later answers.
  Engineering and final reports are completed in Pegasus. EVA is optional.
- Integrate Glass's repair estimates, not its valuation service. Credentials
  are per Engineer in Administration. Initial alex configuration and all live
  Glass's acceptance belong to the operator; no secret is seeded.
- Triage is separate from normal Case/PO allocation. Its global increasing
  `T-00001` sequence has no yearly or Principal reset or reuse. Formal
  instructions allocate a normal Case and link the Triage.
- Administrators onboard existing authorized Microsoft 365 mailboxes through
  Pegasus. Mailbox creation and one-time infrastructure setup are separate.
- Staff initiate every report and chaser send. Sent evidence must be truthful;
  there are no unattended scheduled chasers or autonomous connector sends.
- Unknown repairer VAT requires explicit status or VAT-category selection
  before estimate totals can be accepted.
- Include address suggestions and configurable Principal defaults for the
  `Image Based Assessment` location. Preserve provider-controlled report
  addresses; a physical address does not imply CE attendance.
- HDUK-branded instructions in the supplied YML samples belong to the
  confirmed YML route. Preserve document issuer separately from Principal.
- Existing application email/Case data is disposable development data. Build
  one target schema without obsolete-data conversion. This is no permission
  to delete source references or perform an unreviewed cloud reset.
- Three scoped owner tickets deliver exactly three replacement PRs to `dev`,
  left open and unmerged. Required work from PRs 639, 646, 670 and 671 is
  preserved and verified before those PRs close as superseded. Preserve their
  original branches and evidence; existing tickets get reasoned dispositions.
- Defer the one-off customer workflow and additional spreadsheet-driven
  recipient/package/chase and garage-procedure automation. Include the named
  location defaults, address suggestions and top-15 extraction.
- Remove periodic account reviews. Include disable/delete access, password
  reset, force logout and targeted lease clearance. Historical actor and
  printed signatory records remain stable.
- Box is durable file storage. Azure holds processing bytes and a 24-hour
  idle cache. SQL retains minimal arrival, idempotency and provenance records.
- Preserve the four-project architecture and one Core business-policy owner.
  No prerelease compatibility layers, replacement platforms, generic workflow
  engine or speculative abstractions are required.

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

> **Source labels:** `pre-consolidation operator source: business-process — case-lifecycle`; `pre-consolidation operator source: business-process — intake-and-work-instructions`; direct-decision evidence in the historical project-discovery questionnaire §§3–6

## Stage 0 — Triage

Triage does not technically count as a case, but its emails must be stored. A work provider asks Collision Engineers to assess whether a vehicle is roadworthy or unroadworthy.

The normal workflow is:

1. retain the provider request as separate pre-case Triage work;
2. keep it as **Unidentified** (formerly `Needs sorting`; see [Unidentified received material](#unidentified-received-material)) until a vehicle registration is known, then open the Triage;
3. obtain any missing information and record at least one accepted finding;
4. send the response on the original reply chain; and
5. complete the Triage only when the exact approved-mailbox reply-chain Sent item is confirmed.

An acknowledgement, information request, Draft, or other correspondence is not a finding or completion. `Cancelled` is the only end without a finding and reply. The canonical product transitions and evidence boundary are centralized in [Triage normal workflow and completion evidence](frd/frd-03-triage.md#normal-workflow-and-completion-evidence).

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

`Completed` and `Cancelled` close only the separate Triage workflow. They do not make a Triage finding definitive or final for a later Case, and a reasoned reopen returns that Triage to `Open`.

## Unidentified received material

When Pegasus can safely retain a document, image, message, attachment, or inseparable
submission group but cannot establish a usable identity, meaning, ownership, or
destination, it must place the material in the **Unidentified** queue. Unidentified
replaces the old broad `Needs sorting` destination for this meaning; it does not
rename or collapse Triage, Blocked intake, incomplete Audit evidence, or Image Intake.

Each item or inseparable group receives the next immutable internal tracking reference
`U1`, `U2`, `U3`, and so on, with no fixed-width ceiling. The reference is not a
Case/PO, Audit reference, Image Intake reference, Principal identity, or evidence that
case allocation gates passed. A grouped submission receives one U-reference while
each member retains its original filename, receipt identity, custody, and source
history. Every item records one required reason and safe explanatory detail.

The six reasons are: unreadable or corrupt content; unsupported content; no usable
identification; conflicting identification; ambiguous ownership or destination; and
terminal technical processing failure after custody succeeds. A conflicting vehicle
registration group is explicitly recorded as conflicting identification. Retryable
processing remains in processing and does not allocate a U-reference.

Authorised staff may resolve an Unidentified item into a supported destination, but its
U-reference and origin never change or become reusable. Resolution records the actor,
time, reason, target, and immutable history. Open Unidentified work is searchable and
visible in queue/count/detail surfaces; resolved work remains searchable and visible
in history.

## Stage 1 — Receiving instructions or images

An intake may begin in either of two ways:

1. Collision Engineers receives Work Instructions sent by, or on behalf of, a work provider.
2. Collision Engineers receives vehicle images, often from a repairer, garage, bodyshop, or similar business. The associated work provider may initially be unclear or unknown.

Collision Engineers prepares sufficiently evidenced work to be passed to an Engineer.

An image-only arrival is an Image-initiated Case projection and may be logged in the holding process. Its immutable source occurrence, evidence, and VRM reference remain distinct from any formal Instruction-initiated Case while instructions or case association are pending. Images alone do not create a formal Case/PO association. They may be linked automatically only on a definitive match, or linked manually by staff. A mistaken link or merge is reasonedly reversible while both original intake origins and every prior association remain attributable.

## Image-initiated Case clarification — 2026-08-19

The image-first record is a secondary Image-initiated Case projection, not a
formal Instruction-initiated Case. When the vision/VRM system identifies one
usable registration it receives the immutable VRM-sequenced Image Intake
Reference (for example AB12ABC-01), is searchable, and is retained under that
reference. It has no Case/PO. A later unique non-overlapping match merges it
into the Instruction-initiated Case and records history on both records; staff
may permanently close it with a reason when instructions never arrive.

Conflicting valid VRMs are not a readable Image-initiated Case outcome. The
whole group enters Unidentified with the explicit conflicting_vrms marker.

### Two branches for a readable registration — operator ruling, 2026-08-19

A readable vehicle registration on received images settles into exactly one of
two outcomes, never a third:

- If the registration **matches an existing Case** (by VRM), the images are
  attached to that Case as evidence — they do not create a separate
  Image-initiated Case.
- If the registration **matches no existing Case**, that creates an
  Image-initiated Case under its own VRM-sequenced reference, as described
  above.

Operator, verbatim: “It could be either an image initiated case, OR it could
be images being received for an existing case. ie if we get images, with a
registration that doesnt match any existing case, then that creates an image
initiated case. If they match an existing case (by VRM), then get get
attached as evidence to that case.” This is the same fork already described
above (register, then either await, merge, or staff-close) restated as the
two settled outcomes for a readable registration; it does not change the
existing sentence that automatic linking requires a definitive match and
manual linking remains a staff action.

A required image set should ideally show:

- the sustained vehicle damage; and
- a clear view of the vehicle registration.

## Stage 1.5 — Chasing missing information

If a case is incomplete, Collision Engineers chases the relevant party for the missing details, images, or documents. The case can proceed when the required material has been obtained.

The working view must keep the missing-material reason, `Due by`, next chase,
most recent recorded channel/outcome, optional note, and next permitted action
together. Preparing or copying a chaser is not evidence that it was sent,
delivered, or answered. The product owner for these fields and their schedule is
[due work, chasing, and action
history](frd/frd-01-case-identity-and-lifecycle.md#due-work-chasing-and-action-history).

## Stage 2 — Inspection

Collision Engineers does not physically inspect vehicles. An Engineer performs a desktop inspection and prepares a report containing:

- the vehicle’s roadworthiness determination;
- whether the vehicle is repairable or a total loss; and
- an estimated repair cost.

### Repair estimates — operator statement, 2026-08-19

Repair cost figures are not typed into Pegasus by hand. The operator, verbatim:
"these are imported through other means generally ie external estimating
systems: auxatex, glasses etc. Or an AI performs an estimate and sends via MCP
connector. We also need to be able to drag+drop an estimate in." So the three
intended routes are external estimating systems (Audatex, Glass's), an
AI-produced estimate delivered through the MCP connector, and staff
drag-and-drop of an estimate file. None of the three is built yet (tracked as
ENG-002); until one is, report generation lists repair-cost figures as
outstanding rather than inventing them.

The Engineer report, not any earlier Triage finding, is definitive for roadworthiness and repairability or total-loss determinations.

Roadworthiness and Assessment are independent professional findings. Correcting
one does not erase or implicitly change the other: retain the accepted earlier
version, correction reason, superseding finding, actor, and chronology. A closed
case must be reasonedly reopened before revision, and a finding correction does
not itself change a fee or invoice. The canonical product contract is
[professional engineering findings and
correction](frd/frd-06-vehicle-and-engineering-evidence.md#professional-engineering-findings-and-correction).

## Stage 3 — Post-report

The Engineer sends the report to the provider. Queries or disputes may then be received, generally by email, from:

- the provider;
- a third-party insurer; or
- the claimant.

The Engineer must respond to those queries or disputes.

A retained acknowledgement, source receipt, outbound message record, or `Report sent` event is not post-report completion. Report sent enters post-report work; the separately named, reasoned closure outcome ends it. The canonical distinction is [lifecycle closure and correspondence](frd/frd-01-case-identity-and-lifecycle.md#lifecycle-closure-and-correspondence).

The exact state machine for later query/dispute handling, including reply
evidence and final resolution, remains an [open external/report
decision](open-decisions.md#external-data-submission-and-report-contracts);
receipt alone must not invent it.

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

## Vehicle-source and classification distinctions

> **Source labels:** `pre-consolidation operator source: business-process — intake-and-work-instructions`; `accepted finding: suggestion-first image analysis and VRM recognition`; `direct-decision evidence: project-discovery questionnaire`

These are distinct evidence classes, not interchangeable labels:

| Observation | Operator interpretation |
|---|---|
| Registration written in instructions | Supplied VRM evidence; preserve its source. |
| Registration read from an ordinary vehicle image | Source-image-bound suggestion pending staff confirmation; no synthetic instruction or automatic final value. |
| DVLA/DVSA vehicle observation | Separately sourced make, model, manufacture year, engine capacity, or fuel-type evidence where the approved lookup supplies it. It does not silently overwrite a confirmed instruction value. |
| MOT observation | Separately sourced test chronology/status and recorded mileage/value/unit evidence where supplied by the approved lookup. |
| Mileage in instructions | Supplied fact. |
| Mileage calculated from accepted MOT observations | Derived estimate with its observations and method; never relabel as supplied mileage. |
| Missing, no-result, stale, partial, unavailable, or failed lookup | Explicit status; never a zero, blank confirmed value, invented vehicle fact, or permission to call an unapproved service. |

The durable behavior and refresh/reconciliation rules are centralized in
[ordinary-image VRM and image
analysis](frd/frd-06-vehicle-and-engineering-evidence.md#ordinary-image-vrm-and-image-analysis) and [vehicle
data and MOT enrichment](frd/frd-06-vehicle-and-engineering-evidence.md#vehicle-data-and-mot-enrichment). This
note preserves operator provenance rather than defining an adapter or lookup
contract.

## Authoritative channels and formats

Email through Outlook supplies the vast majority of Work Instructions and is the primary intake-automation target.

| Channel | Accepted forms |
| --- | --- |
| Email | PDF attachment, DOC/DOCX attachment, or freehand email text |
| WhatsApp | PDF attachment, DOC/DOCX attachment, or text typed in WhatsApp |
| Provider API | Future intake channel into the Collision Engineers system |

## Provider and intermediary routing

The sender route and the underlying work provider are related but distinct facts.

1. If an email was forwarded by Collision Engineers staff from an
   `@collisionengineers.co.uk` address, retain that staff sender as transport
   provenance. Use one proved original sender for route identification: either
   an attached original email or the one `From:`, `Sent:`, `To:`, `Subject:`
   header quartet in a normal Outlook forward. A partial, conflicting, or
   malformed header remains Unidentified (formerly `Needs sorting`).
2. Determine whether the effective sender belongs to an accepted direct-provider route or an intermediary route.
3. Extract attachments, email body, and subject before applying the identified route’s rules.
4. For a direct-provider route, use that provider’s rules to determine instruction type and any related case.
5. For an intermediary route, use the intermediary’s rules to determine the underlying provider, instruction type, and any related case.

If a newly received email cannot otherwise be identified or routed, but carries
directly attached vehicle photographs, those photographs enter Image Intake as
one group through the same route as a grouped manual upload. The image-group
outcome replaces the parent email's Unidentified outcome: one usable
registration may associate the group to one eligible Case or create one
Image-initiated Case, while no usable registration, conflicting registrations,
or a terminal processing failure produces the group's one Unidentified outcome.
The original EML, inline signature/logo images, and images derived from another
attachment are not members of that group. This rule applies to newly processed
mail and does not replay or alter existing Unidentified item U35.

A provider may send some work directly and other work through an intermediary. Those are separate routes to the same provider. An intermediary email must not be interpreted as though it were a direct provider email.

Case association must follow the identified route’s rules. Providers do not generally quote a Collision Engineers Case/PO, so Case/PO is never the universal first match. It may be used only as a lowest-priority fallback where the route’s evidence supports doing so.

Ambiguous provider, instruction-type, or case evidence remains pre-case for staff sorting.

## Confirmed mailbox categorisation

> **Source labels:** user-confirmed taxonomy from the retained current-tree
> evidence; current user direction on
> correction, reversal, and audit; retained mailbox decision
> dossier

Alex directly confirmed the Received/Sent taxonomy, its subtypes, and the
mirrored Reply rule. The exact categories and behavior are centralized in the
[settled mailbox taxonomy and correction
clause](frd/frd-08-email-mailbox-and-background-processing.md#settled-mailbox-taxonomy-and-correction), including
`Other`, the separation of classification from queues, Triage, and Outlook
folders, and the reasoned, append-only correction/reversal audit contract. This
operator note records the confirmation and provenance; it is not a second
product-policy owner.

`new-instruction-received` is a Received family and has no confirmed Sent
counterpart. Alex has not confirmed how to choose between multiple rules that
match the same message, so exact multi-rule precedence remains an [open
decision](open-decisions.md#mailbox-rule-activation-automatic-matching-and-confidence-display).

The retained dossier preserves the phased maturity sequence, dependencies,
option comparison, and unresolved predicate, activation, holdout, and Graph
scope research as subordinate historical evidence. It neither reopens the
settled taxonomy and correction/audit behavior nor proves an implementation,
caller, deployment, or acceptance.

# Inspection address

> **Source label:** `pre-consolidation operator source: business-process — inspection-address`

An inspection address is report data; it does not imply that Collision Engineers physically attended the vehicle. Every assessment is a desktop inspection.

The report has two permitted inspection-address treatments:

1. Record the physical location of the vehicle, such as the client’s address or the garage or repairer location.
2. Record the exact text **“Image Based Assessment”** instead of an address.

Which treatment applies depends on the provider, not on the instruction text: instruction documents do not contain the literal “Image Based Assessment”. Some providers — QDOS among them — always use **“Image Based Assessment”**: it is autofilled when the Case is made from the provider’s recorded setting, even when a repairer/location appears in the instructions, and an authorised staff member may override it on that Case with a recorded reason (and switch it back the same way). For many others, the vehicle’s physical location is important and must appear on the report even though the Engineer is inspecting remotely. (Provider-driven determination confirmed by the product owner on 2026-08-03.)

Physical-address determination is not handled ideally:

- some instruction documents identify the vehicle location;
- otherwise, Admin staff often rely on provider-specific knowledge;
- in practice, one knowledgeable person may infer the location from images or know the repairer commonly used by that provider.

The required inspection-address helper is intended to reduce this dependency by suggesting addresses from provider usage frequency, accident location when available, and image or vision AI. That helper is outside `0.1.0-alpha.1` and is not established here as implemented.

# Principal, repairer, and historical case parties

> **Source labels:** accepted operator finding dated 2026-07-23 in `reference/reports/repairer-identity-and-case-party-roles.md`; direct-decision evidence in the historical project-discovery questionnaire §5; `pre-consolidation operator source: business-process — intake-and-work-instructions`

The operator decisions distinguish reusable organisation identity from the function that organisation or person performs on one case:

| Case-party function | Operator meaning |
| --- | --- |
| Principal | The work provider that instructs and pays. |
| Intermediary | Routes the work without thereby becoming Principal. |
| Repairer | Commonly holds the vehicle and may supply images. A repairer, garage, or bodyshop is a reusable organisation identity connected deliberately to a case, not merely free text inside the inspection address. |
| Image Source | The actual supplier of images: Principal, Intermediary, Repairer, or an individual. |

One organisation or individual may hold more than one function on the same case. An ambiguous sender does not establish Principal merely because it transmitted an email or images, and operator-facing labels must name the known function rather than substitute an ambiguous `client` label.

Each case retains the inspection address, organisation identities, and case-party functions accepted for that case. A later correction to reusable repairer or organisation directory data must not rewrite that historical case evidence. The canonical product contract is [principal, reference, organisation, and case-party identity](frd/frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity); this note preserves the accepted operator provenance rather than defining a second implementation policy.

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
| Inspection + Audit | Collision Engineers first completes its standard Inspection process and then carries out an Audit on that same inspection. |
| Diminution | Retained for provenance but deferred. Cases are not frequent enough to include in a first build. |
| Commercial | Retained for provenance but deferred for the same reason as Diminution. Cases are not frequent enough to include in a first build. |

# Reserved terms

> **Source label:** `pre-consolidation operator source: business-process — reserved-terms`

The following terms have specific Collision Engineers business meanings and must not name unrelated functions, code, or concepts:

- **Audit**
- **Triage**

For example, a generic inbox-sorting function must not be called “triage,” because Triage is a distinct kind of work received by Collision Engineers. The reserved list may be extended over time.

# Staff roles and access authority

> **Source labels:** direct-decision evidence in the historical project-discovery questionnaire §§3–4 and §6; pre-consolidation QDOS-alpha identity/access requirements

The current staff roles are:

| Role | Operator-authorised application work | Restricted work |
| --- | --- | --- |
| Administrator | All ordinary Intake, Triage, Case, document, evidence, task, transition, and pre-Engineer-assignment review work; account creation/disable/access review/role assignment; principals; workflow configuration; approved Outlook mailbox allowlist | No permanent deletion; credential, cloud, and release operations are not staff-application administration |
| Engineer | Cases, inbox items, documents, evidence, all authorised case actions, and the pre-Engineer-assignment review gate | No account, role, access-review, principal, configuration, or approved-mailbox administration |
| User | Cases, inbox items, documents, evidence, all authorised case actions, and the pre-Engineer-assignment review gate | No account, role, access-review, principal, configuration, or approved-mailbox administration |

Andrew and Alex are the initial Administrator assignments held as application data/configuration, never hard-coded authorization. External customers have no application account or access. The exact fail-closed product matrix and automated-actor boundary are centralized in the [staff role access matrix](frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix); this section records the operator decision and provenance.

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
| `CAP-017` | Provide OCR for vehicle registrations and scanned PDFs. | Scan-like OCR is `INT-16` (`Next / 0.2.0`); ordinary-image VRM reading is `INT-17` (alpha, non-blocking). Must support VRM recognition and non-embedded-text documents. |
| `CAP-018` | Provide an inspection-address helper. | Suggestions should use provider frequency, accident location when available, and image or vision AI. Not in `0.1.0-alpha.1`. |
| `CAP-019` | Look up vehicle details through DVLA and DVSA when instructions do not contain them. | Lookup authority does not itself authorize an external operation. |
| `CAP-020` | Estimate mileage from MOT data when available. | An estimate must remain distinguishable from supplied mileage. |
| `CAP-021` | Support email management from within the application. | Intended functionality; not proof of an Outlook caller or deployment. |
| `CAP-022` | Allow authenticated staff to create temporary, revocable, request-scoped in-house upload links for chaser messages. | The isolated unauthenticated page may accept files and return that request’s result only; it must not expose case/request history or other material. Box File Request behavior is superseded. |

# Engineering and interface constraints

> **Source label:** `pre-consolidation operator source: product-requirements — engineering-constraints`

## Environment and tools

All work is carried out using PowerShell 7, on Windows or on Linux.

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

Recorded from the operator's interface review and answers, 2026-08-04. These
are additions; no statement above changes meaning.

- The word “intake” is an internal development term and must not appear anywhere in the interface. Interface surfaces use business language: e-mail activity, Inbox, Upload, received items, vehicle images.
- “Blocked intake” is renamed to “Blocked” in the interface, without explanatory copy.
- File sizes are never shown in bytes. Where a size is relevant — an e-mail attachment, an upload limit — it is shown in megabytes; otherwise it is not shown.
- A count of zero is shown as 0. Placeholder states such as “Unavailable” must not stand in for numbers, and a metric whose query does not exist must not be shown at all.
- The interface never displays raw internal identifiers: GUIDs, hashes, storage paths, database or enum value names, event codes, or version integers.
- “Unidentified” (formerly “Needs sorting”) refers to e-mail that cannot be matched; it is not a case stage.
- Screens are screens in an application, not pages on a website. They are compact, and scrolling is minimised: the identity, the state, the available actions and the main content of a screen are visible without scrolling.
- A screen about one record shows that record inside one container, with its actions as a bar at the top and its sections as tabs — not as separate panels stacked down the page.
- A case's material is called **Evidence**, and covers files, images and e-mail.
- Where a value or a document came from is shown as an icon with a one-word explanation on hover: Staff, Extracted, AI, E-mail, Lookup, Principal, Automatic.
- An action that this record will offer once a condition is met stays visible and disabled, with the condition named on it. Exporting a case is available when the case is in Review.

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
| EVA | Current case-management system. Once a case is ready, it is entered into EVA and assigned to an Engineer. EVA wraps estimating systems such as Audatex and Glass’s, contains valuation-service integrations, stores case valuations, and generates the final provider report. The supplied workflow records PDF-to-JSON extraction followed by JSON drag-and-drop into EVA. | Eventually replace all EVA functions and integrations while providing greater business automation. Interim JSON export is intended to move to API use. | EVA offers an API, and supplied details are routed according to its schema under the canonical [reference authority](../reference/README.md). The required API path is not currently functional and is waiting on EVA developers; a schema does not prove a working caller. |
| Excel | Used as a holding pen for instruction-initiated and image-initiated work until ready for EVA. **Not ready** means something is missing, almost always images or instructions. **Ready** means ready to enter into EVA but not yet entered. | No standalone Excel integration target is stated. Product case management is intended to absorb the surrounding workflow over time. | Excel is a holding log, not the long-term document-custody system and not evidence that an image-only entry is technically a definitive case. |
| Box | Long-term storage for instruction emails, instruction documents, vehicle images, and produced Engineer Reports. Each case has its own Box subfolder. | Continue using Box for long-term custody and automatic storage. Chaser messages use the separately bounded in-house request-scoped upload link; Box API file requests are superseded. | Box custody does not mean every newly received item is immediately in Box. Unmatched WhatsApp images may remain in network-drive staging. No supplied source proves automatic staging-to-Box transfer or an API caller. |
| Audatex | Separate estimating system used by Collision Engineers. It is considered more prestigious and to have more functionality. EVA may wrap it. | Estimating-service integration is required eventually but excluded from `0.1.0-alpha.1`. | Audatex has API features, but the integration methods are currently unclear. |
| Tractable and Ravin | Mobile guided-capture services under evaluation as possible image-intake methods. Claimants use the apps and Collision Engineers receives the images directly. | Inform the future in-house guided-capture capability. | Evaluation does not establish adoption, integration, deployment, or acceptance. The in-house capability is excluded from `0.1.0-alpha.1`. |
| `cedocumentmapper` | The EVA workflow source records Collision Engineers’ predecessor process: a Python extractor with a Tkinter UI extracts PDF details to JSON, which is dragged into EVA. | Do not adopt or reuse this implementation. The operator source rejects it as very poorly designed and made. Pegasus designates PdfPig as the authoritative embedded-PDF extraction method. A bespoke extractor remains deferred until its hardening is separately accepted. | `cedocumentmapper` is predecessor evidence only and is not an implementation source. The PdfPig designation is a binding method rule, but the supplied operator sources do not identify a real Pegasus caller, deployment, or acceptance. |

## External-workflow distinctions

> **Source labels:** `pre-consolidation operator source: systems-and-integrations — eva`; `pre-consolidation operator source: systems-and-integrations — box`; `direct-decision evidence: project-discovery questionnaire`; `accepted finding: EVA API preference and focused QDOS-alpha JSON handoff`

The current and intended workflows use several different evidence transfers:

- an in-house request-scoped link receives images/documents into Pegasus intake;
  upload success is not case creation, Case/PO allocation, Box custody, EVA
  handoff, or external delivery;
- the focused alpha EVA export is the current send-to-Engineer route: a
  reviewed JSON/image download for manual import into EVA. `Review` is its one
  readiness decision; suggested values do not block export, VAT and mileage
  are optional, and Case/Audit custody are not duplicate export gates. Its
  first successful generation records Pegasus's once-per-case `First sent to
  Engineer` proxy and every export records action history; this proves neither
  EVA receipt nor an external EVA assignment. Pegasus may separately record an
  optional direct assignment to one of its Engineer accounts;
- EVA currently generates the final provider report, while Box stores produced
  Engineer Reports; a PDF's existence or custody does not prove that the report
  was sent or received; and
- the planned successors are the EVA API when EVA fixes it, then direct
  estimating-system integrations that replace EVA; AI-generated estimates may
  instead remain in Pegasus for Engineer review and report generation; and
- a provider submission API and report delivery remain separate contracts and
  authorizations.

**Evidence-only limitation:** the supplied EVA schema does not establish a
usable Pegasus caller or an accepted proxy fetch, create-with-children,
picture-upload, report-with-PDF, response, or case/reference-correlation
contract. These details remain unresolved, and no external operation is
authorized from schema availability.

## Storage and staging interpretation

> **Source labels:** `pre-consolidation operator source: systems-and-integrations — box`; `pre-consolidation operator source: systems-and-integrations — whatsapp`; `pre-consolidation operator source: systems-and-integrations — excel`

The storage statements describe different layers rather than competing custody rules:

- the network drive is temporary staging for unmatched WhatsApp images;
- Excel is a holding log for incomplete or EVA-ready work;
- Box is the intended long-term case-file repository, with one subfolder per case.

A staged image must not be treated as definitively associated merely because it has been received. The supplied sources do not establish that movement from staging into Box is automated.

## Report readiness after Review

> **Source label:** `direct operator statement, 2026-08-25`

"Report readiness does not need to recalculate requirements that were needed
for a case to reach review. By virtue of it being there, these requirements are
met. Hence this is duplication."

The Assessment workspace is unavailable while a case is `Not ready`. It opens
only after a successful EVA export in the current Review cycle. Engineer
assignment is not a progression requirement: a case may remain unassigned, be
assigned directly where required, or be assigned by an Engineer taking the work.

# Additional recorded operator statements

> **Source label:** `pre-deletion migration from docs/history/product/project-discovery-questionnaire.md, 2026-08-02`

These statements were made in the discovery questionnaire and are preserved here
verbatim because no other canonical document records them.

- **Box filing of the secondary Audit:** "The Audit is stored in a subfolder
  beneath the original Inspection folder in Box." (The Audit's Box folder nests
  under the parent Inspection case folder, not beside it.)
- **Operating hours:** "Automated mailbox ingestion and case processing operate
  continuously. Staff-facing use is expected primarily during Collision
  Engineers business hours, but the application should remain available outside
  those hours unless undergoing planned maintenance."
- **Support and incident response:** "Alex provides first-line application
  support." Alex initially receives security, availability, failure, and cost
  alerts; additional recipients may be added later through monitoring
  configuration without code changes. "Critical incidents should be acknowledged
  immediately while Alex is in the staffed office. Outside staffed hours,
  respond as soon as reasonably possible." Emergency production access: "Alex
  initially, plus any specifically designated Administrator or Azure operator
  added later."
- **Commercial and licensing constraints:** "No fixed monthly budget. Use the
  lowest practical Azure tiers that still support required development and
  integration testing." No fixed monthly ceiling has been set. "Reuse Collision
  Engineers' existing Microsoft 365, Azure, Box, EVA, Audatex, and other vendor
  accounts/licences where applicable" and "confirm commercial/API entitlement
  before enabling each vendor integration." No fixed procurement or vendor
  restriction has been supplied.
- **Data residency and region:** A primary Azure region is not a requirement
  "unless it impacts performance", and application data is not required to
  remain in the UK ("Anything related to Data is not a concern"). UK South is a
  chosen default (ADR-0015), not an operator constraint.

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
