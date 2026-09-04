# Research — INTK-059

## Question

Does current Triage creation capture a principal, and is that relationship
stored on the Triage record?

## Verified findings

- **No.** `TriageRecord` and `TriageSummary` have no `PrincipalId`, and
  `CreateTriageFromIntakeRequest` accepts only the origin, registration,
  accepted-match evidence, actor and operation key
  (`src/Pegasus.Core/Triage/TriageContracts.cs:34-41,79-84,271-278`).
- **No.** `TriageEntity` and the `Triage` table mapping have no principal
  column or foreign key. They persist origin receipt, source identity,
  evaluation revision, registration, state, assignee and linked Case only
  (`src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs:740-767,
  1236-1256`). `EfTriageStore` consequently cannot map a principal.
- The normal durable creation path reads a registration plus exactly one
  strong `AcceptedTriageMatch` evidence item, then passes neither a principal
  code nor identifier to `CreateTriageFromIntakeRequest`
  (`src/Pegasus.Core/Intake/DurableIntake.cs:1094-1130`).
- A mail route establishes only `EstablishedPrincipalContext.PrincipalCode`.
  The current registered mail-classification and extraction policies are QDOS
  implementations (`src/Pegasus.Core/Intake/ProcessIntake.cs:801-810`,
  `src/Pegasus.Infrastructure/DependencyInjection.cs:151-156`). The accepted
  match is derived from that route's classification decision, not from a
  separate Triage matcher (`src/Pegasus.Core/Intake/ProcessIntake.cs:651-696`).
- There is also a non-QDOS source: an authenticated Provider API declaration
  builds an instruction draft with `binding.PrincipalCode` and adds the same
  accepted-match evidence (`src/Pegasus.Core/Intake/ProcessIntake.cs:753-786`).
  In both paths the origin receipt can carry a suggested principal *code*, but
  that is not a principal relationship on Triage.
- The Triage page currently loads the Triage detail and its origin receipt for
  images, but has no principal projection (`src/Pegasus.Web/Pages/Triage/
  Details.cshtml.cs:288-331`).
- FRD-03 expressly permits manual Triage classification without inventing a
  Principal identity (`docs/frd/frd-03-triage.md:20-22`). Therefore a principal
  field on Triage must be optional, not a creation prerequisite.

## Implications

The original display-only scope was incorrect. INTK-059 must persist an
optional, authoritative principal relationship at Triage creation, propagate it
through the Core contracts and queries, and render it read-only. It must not
infer identity from QDOS text, introduce another classifier, or make manual
Triage invalid.

## Sources and limits

No project-declared external research sources apply to Intake & Processing
(`get_sources`, 2026-09-04). Findings above are read-only checks of the current
repository and the two governing FRDs.
