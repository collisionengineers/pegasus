# Accepted findings: repairer identity and case party roles

**Operator decision:** Accepted on 2026-07-23.

**Legacy sources dealt with:** ADR-0001 (`../dealt-with/accepted/0001-repairer-first-class-entity.md`), ADR-0011 (`../dealt-with/accepted/0011-work-provider-intermediary-garage-roles.md`), and the examined [repairer spreadsheet](../workproviders-and-repairers/contacts/REPAIRER.xls).

This report records accepted findings from those sources. It does not make the predecessor architecture authoritative or approve an automatic import of its data.

## Finding 1: repairer identity is missing from the current case design

### Accepted finding

A repairer, garage, or bodyshop is a reusable organisation identity that can be connected to a case. The accepted-case design must represent that organisation deliberately rather than reducing it to the free-text inspection address.

The case must retain the historical inspection-address and role facts used for that case. A later correction to reusable directory information must not silently rewrite historical case evidence.

### Current v2 authority and evidence

- The [questionnaire](../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md#5-case-information) says a repairer/garage/bodyshop can be connected to a case and requires the physical vehicle/repairer address where applicable.
- The [operator inspection-address note](../../operator-notes/business-process/inspection-address.md) says staff may need the garage/repairer location and often know which repairer a principal generally uses.
- [Remaining requirements](../../product/v1-gap.md#4-case-model-and-lifecycle) require the full case record and allow a real vehicle/repairer address or `Image Based Assessment`.
- The examined spreadsheet contains recurring repairer records with distinct codes, names, and address components. Its evidence limits are recorded below.

### Difference from current implementation and plan

- The only current real caller is the Development-only `/Intake/Upload` path. Its [typed pre-case draft](../../../src/CollisionSpike.Core/Intake/IntakeContracts.cs) stores `InspectionAddress` as a nullable string.
- [Current EF persistence](../../../src/CollisionSpike.Infrastructure/Persistence/CollisionSpikeDbContext.cs) likewise persists only that draft string. There is no accepted Case, Repairer identity, case-to-repairer association, or historical address policy.
- The [intake and case-acceptance plan](../../history/plans/remainder-delivery/casework/intake-and-case-acceptance.md) mentions typed fields and associations but does not identify a repairer policy owner, persisted identity, migration, failure behaviour, or caller test.

### Not accepted from the legacy design

The finding does not yet settle:

- a many-to-many principal-to-repairer directory relationship;
- contact fields or a `figures status` field;
- the exact database tables, type names, or generic party abstraction;
- whether the case stores a repairer reference plus an immutable address snapshot or another history-preserving representation; or
- automatic inspection-address suggestions or migration of legacy rows.

Those choices need current design evidence and, where they affect product behaviour, a further operator decision.

## Finding 2: case party roles must remain distinct

### Accepted finding

Principal, Intermediary, Repairer, and Image Source are distinct functions on a case. One organisation or individual may hold more than one role on the same case, so an organisation's reusable identity and its case-specific roles are separate concepts.

- The Principal is the work provider that instructs and pays.
- An Intermediary routes work without thereby becoming the Principal.
- A Repairer commonly holds the vehicle and may supply images.
- Image Source records who actually supplied the images and may be the Principal, an Intermediary, a Repairer, or an individual.
- An ambiguous sender must not be treated as the Principal merely because it transmitted the email or images. Strong, unambiguous instruction content takes precedence over a staff-forwarded sender.
- Operator-facing labels must name the actual role. The ambiguous label `client` must not substitute for Principal, claimant/insured person, Repairer, or another known role.

### Current v2 authority and evidence

- The [operator intake note](../../operator-notes/business-process/intake-and-work-instructions.md) distinguishes instructions sent by or on behalf of a work provider from images supplied by a repairer where the related work provider may be unknown.
- The [questionnaire](../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md#5-case-information) already lists the Principal, claimant, Repairer, insurer, and operational contacts as case-connected people and organisations.
- [Remaining requirements](../../product/v1-gap.md#what-is-already-proved-locally) say strong QDOS instruction content outranks the sender of a staff-forwarded email.
- The real Web caller has a [caller-level integration test](../../../tests/CollisionSpike.IntegrationTests/QdosIntakeWebTests.cs) for that forwarded-sender precedence rule.

### Difference from current implementation and plan

The current QDOS path records sender evidence and applies one provider-resolution rule, but it creates no accepted case and persists no party identity or case-specific party roles. Intermediary and Image Source are not represented as named case roles. The current plan therefore proves only a narrow intake-classification rule, not the accepted case model or later chasing behaviour.

## Spreadsheet examination

The original `REPAIRER.xls` was examined locally as untrusted historical evidence. No row values are reproduced here.

| Property | Examined result |
| --- | --- |
| SHA-256 | `3f78697510b4558fb1546b11dd54849803bb5b29982104fd077ccb67e24a2d0e` |
| Workbook shape | One visible sheet, 72 physical rows and 10 columns |
| Data shape | One blank row, one header row, and 70 data rows; columns 8–10 are empty |
| Named columns | `Code`, `Name`, `Group`, `Address`, `City`, `County`; the populated seventh column has no header |
| Identity evidence | 70 populated, distinct codes; 70 populated names with 67 distinct normalised names and three duplicate-name groups |
| Category evidence | All 70 data rows have the single group value `REPAIRER` |
| Address evidence | 68 addresses, 56 cities, 46 counties, and 68 values in the unlabelled seventh column; 66 of those 68 values have a UK-postcode shape |
| Contact evidence | No email-like or telephone-like cells were found |
| Read limitation | The workbook has no code-page record; the reader fell back to ISO-8859-1, so text fidelity still needs operator review |

The workbook supports the historical existence of a reusable, coded repairer directory and shows incomplete and potentially duplicate business data. It does not prove that any row remains current, that duplicate names identify the same organisation, that the unlabelled column is always a postcode, or that the codes should become v2 identifiers. It contains no evidence for principal relationships, contacts, case roles, or figures policy.

The workbook is now an examined repository evidence input. Before any import or production directory is designed, staff must review its column meaning, duplicate-name groups, current/retired records, address quality, and code semantics. No import, upload, data correction, or external write is authorised by this finding.

## Delivery consequences

### Policy owner and real callers

The future Core accepted-case use case, currently planned as `AcceptCaseDraft`, must own confirmed case-party association alongside case creation. `ProcessIntake` remains the single current pre-case intake owner; it must not grow a second case model.

The first real mutation caller is the authorised staff review/acceptance flow. Later Worker, provider API, MCP, mailbox, and manual-case callers must invoke the same Core policy rather than assigning roles independently.

### Persistence and migration boundary

Future accepted-case persistence must preserve:

- a stable identity for a reusable repairer or other case-connected party;
- one or more named roles held by that party on a particular case;
- the case's historical inspection-address decision and relevant source evidence; and
- role assignment and correction recorded in permanent action history.

Exact tables, keys, snapshots, contact fields, import rules, and principal-to-repairer relationships remain design work. The spreadsheet is evidence for review, not a seed migration.

### Failure behaviour and observability

- Unknown or conflicting Principal identity must remain reviewable and must not allocate a reference.
- Unknown Image Source or Intermediary identity must be shown as unresolved rather than guessed.
- Assigning, removing, or correcting a case role must record actor, timestamp, prior value, new value, reason, and source/correlation where available.
- Chasers must target an operator-confirmed relevant contact/source; the legacy spreadsheet supplies no such contact evidence.

### Evidence required before implementation is accepted

- A real staff-caller test where the sender/intermediary differs from the Principal and no reference is allocated to the wrong Principal.
- A real staff-caller test where one organisation holds both Repairer and Image Source roles without duplicated organisation records.
- Persistence tests proving multiple roles per case and the same reusable organisation across cases.
- A history test proving that editing directory information does not rewrite the inspection-address evidence already used by an existing case.
- Adversarial tests for ambiguous sender, duplicate repairer name, unknown role, and the forbidden ambiguous `client` label on operator surfaces.
- A separate, operator-reviewed workbook assessment before any row is migrated.

## Deferred-capability impact

- Future mailbox coverage, WhatsApp ingestion, guided capture, and AI/vision assistance need stable source identity and case-role association. This finding preserves that seam but builds none of those adapters now.
- Inspection-address suggestions may later use accepted repairer/principal history, but prediction, mapping, and automatic address selection remain deferred and must never overwrite the staff decision.
- Provider API, staff MCP, EVA API/replacement, and later case types must call the same Core party-role policy. No parallel external role model is approved.
- Repair estimates, valuations, invoices, accounting, and direct estimating services remain deferred. The spreadsheet contains no evidence for `figures status`, so no financial field or workflow is added.
- External/customer accounts remain deferred. A reusable organisation identity is not an application login or authorisation boundary.
- Diminution, Commercial, automated malware scanning, custom-domain work, and later infrastructure options are not constrained by this finding and receive no dormant schema, service, flag, or release gate.
