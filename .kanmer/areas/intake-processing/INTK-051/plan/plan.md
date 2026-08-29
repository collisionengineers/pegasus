# Plan — INTK-051

## Scope

One file of configuration and one documentation edit. No application code.

## Steps

1. **`infra/modules/platform.bicep`** — add thirteen `DocumentRequests__*`
   environment entries to the Web container's `env` array, immediately after the
   `AutomationMcp__RedirectUris` entry, following that array's existing
   `{ name: …, value: … }` convention.

   Reuses: nothing to reuse — this is configuration the file already has no
   equivalent of. The entries are read by
   `src/Pegasus.Web/Program.cs:243-278`, which already exists and already
   composes the upload-link services; nothing new is built.

   All thirteen land together because the composition is all-or-nothing:
   `LimitsVersion` must match `AcceptedLimitsVersion` byte for byte
   (`Program.cs:258-264`), `AllowedMediaTypes` must be a non-empty array
   (`:266-268`), and `RequestUploadLimits`' constructor rejects zero or negative
   bounds (`RequestUploadPolicy.cs:32-46`). A partial set throws at startup and
   crash-loops the container.

2. **`docs/open-decisions.md`** — rewrite the INT-31 entry as *partially*
   settled, keeping one-time-vs-reuse and the revocation/expiry error contract
   open, and record the interim values in a table with the basis for each.
   Keep INT-31 in the "explicitly NOT on the path" list, qualified — activating
   it does not make it a release gate.

3. Validate with `pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local`.

## Values and their provenance

Every value is anchored to something already recorded, not chosen freely:

- **Aggregate 10 485 760 bytes** — `docs/open-decisions.md` already names "the
  existing aggregate 10 MB intake limit" as INT-31's interim bound, and that is
  `IntakeContracts.MaximumContentLength` exactly.
- **Content types** — exactly the set
  `MimeKitPdfPigOpenXmlIntakeSourceReader` already handles, so an upload link
  admits no file type the estate could not already read.
- **168-hour lifetime** — the existing chase cadence (CASE-17/18, MAIL-18).
- **File count 10, rate 20 per 10 minutes** — proposed and accepted by the
  operator on 2026-08-29 as part of the interim set.

Explicitly rejected: the integration fixture's values
(`IntakeWebTestSupport.cs:128-141`, `integration-fixture-v1`, 1-hour lifetime,
1 MB per file). Those are test values; shipping them would make a test fixture
into production business policy.

## Verification

`pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` → exit 0.

Note the script shells out to `az bicep build`, which on this workstation needs
`AZURE_EXTENSION_DIR` pointed at an empty directory; one unreadable extension
metadata file otherwise breaks the entire CLI while it builds its command table.
Read the script's own exit code directly — piping it into `tail` reports the
pipe's status instead.

Post-deployment, `/Uploads/{token}` should compose and the Operations surface
should report the active limits.

## Simplification pass — 2026-08-29

**n/a — infrastructure configuration.** No code to reuse, simplify, make more
efficient, or re-level. The entries follow the adjacent convention in the same
array. No unapplied findings.
