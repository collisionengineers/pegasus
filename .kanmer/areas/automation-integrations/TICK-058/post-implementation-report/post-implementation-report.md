# Post-implementation report — TICK-058 (API-01)

## What changed, and why the contract was rewritten

The contract accepted on 2026-08-28 took `multipart/form-data` files plus a
`providerReference` and nothing else, then relied on the Principal's instruction
extraction policy to read the business values back out of the submitted
documents. That policy recognises QDOS only. QDOS arrives by e-mail, so the
route had no caller; and for any other Principal its own accepted text conceded
the outcome — "retained for sorting rather than allocated" — which is to say
API-01 could not create a case for the providers it exists for.

The operator replaced it the same day: **one endpoint, taking a declared JSON
instruction with its files inline**. A provider integrating over HTTP already
holds the fields and states them; nothing is read back out of a document.

## Shape

`POST /api/provider/v1/submissions`, `application/json`. Credential, feature
gate, rate limit, idempotency header and status codes are unchanged from the
ingress already built; only the request shape and what happens to it changed.

**One submission is one intake receipt.** The retained source is the request
exactly as it arrived and the submitted files are that receipt's attachments —
the shape an e-mail instruction already has. This was the design decision the
plan did not anticipate and the operator settled: retaining each file as its own
receipt would scatter one instruction across many, and a declared Audit could
not then find its original report among its own evidence.

**One substitution, not a second pipeline.** `ProcessIntake.AssessAsync` returns
a declared assessment for the `provider_api` channel and never routes,
classifies or extracts. Allocation, Triage creation, custody, action history and
the durable Worker path are untouched and carry a declared instruction exactly
as they carry an extracted one.

## Decisions recorded

| Decision | Where it lives |
| --- | --- |
| `auditreport` = Inspection + Audit, and carries no incoming report or verdict — only a standalone `audit` does | FRD-01 § Case types; `ProviderInstructionKinds` |
| A declared Audit verdict derives the `a.`/`ap.` reference | FRD-01 amended; `DeclaredAuditReportAsync` |
| `triage` opens a Triage and allocates no Case/PO | FRD-03 amended |
| The body's `principal` is a cross-check, never a selector — mismatch is 403 | FRD-09; `DeclaredPrincipalMatches` |
| `YourRef` is `claimNumber`, one field | FRD-09 |
| Every actor that may act on a case may write a note | `AddCaseNote` |
| Envelope 30 MiB decoded / 42 MiB body | `IntakeEnvelopeLimits` — **still wants operator confirmation** |

## Corrections made against the approved plan

- **No acceptance actor-guard change.** `AttemptAutomaticAsync` already
  allocates as the system worker, so `AcceptIntake` and `EfCaseAcceptanceStore`
  needed no widening. Provider attribution lives on the submission's own action
  history, which is where FRD-09 puts it.
- **`inspection.deadline` dropped from the wire schema.** The snapshot asserts
  the accepted deadline equals the draft's inspection date; the deadline is the
  inspection date, as it is for the mail route.
- **`auditreport` carries no verdict** — the plan had both Audit kinds
  requiring one.

## Defects found and fixed on the way

1. **`IntakeEvidenceSource` had two persisted code maps, already drifted**
   (`EfIntakeReceiptStore`, `InspectionAddressResolutionStore`). Receipts wrote
   the new member and the address-resolution snapshot then refused to read it
   back, failing allocation with an unclassified fault whose safe reason says
   only "The case could not be created." One owner now
   (`IntakeEvidenceSourceCodes`). Latent on `dev` before this ticket.
2. **The scaffolded migration re-added `CaseWorkflows.EditLeaseHolderKind`** —
   a migration merged from `dev` carries an earlier timestamp than this branch's
   own, so the branch's last Designer snapshot predates it and the diff saw the
   column as missing. Removed by hand, reason recorded in the migration.
   Follow-up raised as **DELIV-032**; it will recur on any branch that merges
   `dev` and then scaffolds.

## Evidence

- `dotnet restore --locked-mode`, `dotnet build -c Release`: **succeeded**.
- `Pegasus.Core.Tests`: **1110 passed, 0 failed**.
- `ProviderApiSubmissionTests` (SQL, real HTTP through the composed host):
  **8 passed, 0 failed** — a declared instruction creates a real Case/PO under
  the authenticated Principal; a declared `total-loss` Audit takes the `ap.`
  prefix; a declared `triage` opens a Triage and allocates no case; a body
  naming another Principal is 403 with a recorded security event; replay is 200,
  a changed body under the same key is 409; the surface is 404 with the gate off.
- `Test-MigrationGrants.ps1`: passed (84 files). No new table, so no grant
  sibling; the change adds columns to already-granted tables and recreates two
  check constraints.
- `Test-MarkdownPlacement.ps1`: passed.

## What this is not

**No provider has called this**, in any environment. The feature gate is off,
no credential has been issued, and TICK-058 carries `requires-live-approval`:
issuing a live credential needs exact-target approval first. The evidence above
is a green build and an exercised in-process caller — not a deployed feature.
