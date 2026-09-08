# Research — PLAT-050: Principal contact identity

## Question

Retain the requested customer contact address through the existing flat
Principal flow, while accounting for all original Settings acceptance.
Read-only source pin: dev aefe4c32d078ad79c0368666b5666032e6865248.
No source, tests, build, live Principal or e-mail mutation performed.

## Original acceptance and disposition

| Original requirement | Current caller and integrated evidence | Disposition |
| --- | --- | --- |
| Settings, one customer, no owner hierarchy | Settings/Create Razor -> existing Core administration -> EfOrganizationAdministration. PLAT-028 PR680 merge 987988e0b984ad63c1de4be3ad189f3afeb2928c; proof 321095455570a06d: Core19 and Integration28 PASS, including actual settings/credentials and browser callers. | Preserve; no second page or administration contract. |
| Read-only route addresses | Settings.cshtml.cs:31 reads PrincipalMailRoutePolicy.AcceptedIdentities. TICK-035 merge 56566371a5b80ef59c4f98e377c8e8ff6469b5f7. | Preserve the sole activated domain/exact-mailbox catalog; contact is not route identity. |
| Two EVA toggles | Settings UpdateEva -> IUpdatePrincipalEvaSubmission -> persisted EvaManualSubmission; EfEvaSubmissionModeStore -> EvaSubmissionPolicy -> Cases/Eva/Send and Case Details. | ADR-0034 was explicitly superseded by accepted ADR-0038; automatic submission is retired, not missing. Preserve manual/default-inspection actions and ZIP behavior. |
| Generate/show-once/reset/revoke/pause/resume with reason | Settings handlers:242 onward -> existing credential Core commands -> EfPrincipalCredentialStore. TICK-061 merge 41a17163b31a76c6e28307c7767cdceff3602950 and PLAT-028 caller proof. | Preserve authentication/hash, exact replay, no-store/show-once, stale-version, actor and action-history assertions. |
| Delivered with Provider submission endpoint | ProviderApiEndpoints maps POST and GET. PR594 merge 0d985c9e0b3284f211f824d387e2f36460c0c826 is integrated; PR646 closed unmerged. | Endpoint presence is proved, not full historical acceptance. TICK-058/TICK-060 retain the attributed result-contract conflict and need root disposition; no reimplementation or closure here. |

All four named merge commits were checked reachable from the accepted dev
ancestry. PLAT-028 and TICK-061 proofs were read whole; their historical
failures remain recorded. No prior evidence is represented as a fresh run on
this source. PLAT-028's administration Core/store/credential/Web-test inputs
are unchanged at the current source; only the later Settings route-identity
projection changed under TICK-035.

## Contact capability and reuse

- FRD-04 explicitly defines one customer, created atomically through one
  name/code input; the backing Organizations row is the same identity, not
  a parent. OrganizationEntity (PegasusDbContext.cs:1074), PrincipalEntity,
  CreatePrincipalRequest (CaseContracts.cs:253), and
  PrincipalAdministrationDetails (OrganizationAdministration.cs:44) have no
  customer contact e-mail.
- Create.cshtml.cs is the only production ICreatePrincipal caller.
  OrganizationAdministrationPolicy.Normalize owns trusted validation.
  EfOrganizationAdministration:62 creates the customer, principal, lineage,
  operation receipt and one action history in one serializable transaction.
  Its request hash currently binds all supplied creation fields.
- List/GetPrincipal project the customer name via the existing same-row join
  (EfOrganizationAdministration:447/465). Code replacement retains
  OrganizationId, so contact belongs on that same row, not a copied principal
  generation or a new contact/directory record.
- CaseContact, ClaimSource.Email and repairer/location directory contacts
  are different roles; none is the Principal contact. Report delivery derives
  To from current CaseContact. The existing explicit mail composer supports
  a supplied To, but creation/display does not imply auto-fill or permission
  to send. No mail caller changes are needed.
- FRD-09 and PrincipalMailRoutePolicy keep Collision Engineers' domain as
  staff transport. Contact digital@collisionengineers.co.uk must not add an
  accepted route, impersonate QDOS, or change forwarding classification.

## Persistence and verification surface

Nullable nvarchar(320) ContactEmailAddress on Organizations uses the normal
EF migration stream. Existing table grants already permit Web
SELECT/INSERT/UPDATE and Worker SELECT; bootstrap reads that same matrix.
No new table, grant, permission vocabulary or bootstrap special case is
needed. Prove the real Core/EF create/replay/get under restricted Web role,
using AzureSqlRuntimeRoleMigrationTests' existing connected factory; keep
Worker creation denied.

Reserve proposed migration 20260908053000_PrincipalContactEmailAddress;
accepted source contains no such id. Up adds a nullable column with no
backfill; Down removes only this new contact data. Existing names, codes,
lineages, cases, receipts and historical records are not converted.

## Ownership and next step

INTK-064 map 07e40ec3beb8068c owns AzureSqlRuntimeRoleMigrationTests.cs;
its narrow DI and AcceptIntake fixture handoff does not release that test.
Await its merge/release or exact root-approved file handoff.
TICK-085 remains Verifying for genuine fifth-PDF OCR; its generated index
needs a narrow handoff after integrated local acceptance. No Case snapshot,
DI, estimate, Triage, CASE-031/FRD-07, or historic claim edits.

No declared project research sources exist (get_sources returned zero).
User/current EPIC-014 authorization resolves the contact scope; root's
whole-plan read and these ownership clearances still precede execution.
