---
id: DOCS-019
type: ticket
title: Design README still asserts the embedded Andy signature resource (D18-era)
status: backlog
area: documents-reports
assignee: ''
profile: chore
labels:
  - sign-off
  - case-workspace-v2
  - docs
groups:
  - EPIC-012
links:
  - DOCS-017
refs:
  - docs/design/README.md
archived: false
created: '2026-09-03T19:35:41.384Z'
updated: '2026-09-03T19:35:41.384Z'
---

## What

`docs/design/README.md:620` still reads:

> | Supplied engineer signatures | Andy Patterson's approved exact tuple is
> embedded by Infrastructure; other supplied assets remain governed; never Web
> decorative imagery. The signatory policy is D31 (the Case's Sign-off
> Engineer tuple), delivered by `DOCS-017`. |

The first clause is now false. [[DOCS-017]] (PR #651) removed the
`Pegasus.Infrastructure.Reports.Assets.brand.signatures.andy_patterson.png`
embedded-resource registration from
`src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj`; the renderer now
builds the signature data URI from the supplied `ReportSignatory`
`SignatureContent` / `SignatureContentType` bytes, and
`AssessmentReportRendererTests.NoSignatoryResourceIsEmbedded` asserts that no
`brand.signatures` manifest resource exists at all.

## Why

Raised by the DOCS-017 PR review (PR #651, gpt-5.6-terra xhigh + reviewer,
2026-09-03). DOCS-017's plan named this as a deliberate follow-up because
`docs/design/README.md` is the design authority and is outside DOCS-017's
owned doc paths — but the follow-up existed only inside DOCS-017's own plan
and post-implementation report, with no board record. This ticket is that
record, so the stale statement is not lost when DOCS-017 closes.

## Approach

Rewrite that one row so it states the D31 rule only: the report snapshot
carries the Case sign-off account's supplied signature image (printed name,
optional qualifications, image bytes and media type); no signature asset is
embedded in the application; other supplied assets remain governed; never Web
decorative imagery. Change nothing else in the table.

`docs/design/README.md` is a shared-lock path with capacity one — claim it and
refresh from `origin/dev` before editing.

## Verification

- [ ] `docs/design/README.md` contains no claim that a signature is embedded
      by Infrastructure.
- [ ] `grep -rn "brand.signatures" src/` returns nothing, matching the doc.
- [ ] `./scripts/Test-DocumentationLinks.ps1` and `./scripts/Test-UiCatalogue.ps1`
      still pass.
