# Principal rules and mappings

The reviewed cross-provider evidence and criteria are held in the versioned
[principal-identification corpus](../../reference/workproviders-and-repairers/principal-identification-corpus.v1.json).
It contains one structured Received-and-Sent dossier for every operational
principal, historical-row dispositions, typed supporting identities, source
hashes, evidence groups, review states, and explicit gaps. It is review data,
not a runtime rule engine.

The v1 corpus retains the historical QDOS review baseline, including its v5
evaluation, while its source snapshots identify the current Core policy bytes
and references. Refreshing those current-source links neither reclassifies the
historical evaluation nor activates a runtime policy.

These documents are **descriptive companions**, not behaviour owners. The
binding behaviour stays with the owning FRDs, ADRs, and Core policy code they
cite; if a document here disagrees with the cited owner, the owner wins and
the document is corrected. A companion document is added only when a
principal has an operator-accepted runtime policy; the structured corpus avoids
48 speculative Markdown dossiers. Update an existing companion in the same
task whenever a cited policy version or accepted criterion changes.

Each runtime-policy companion covers, for its provider:

- **Route identification** — how an email is proved to belong to the provider
  (accepted domains, staff-forward unwrapping, effective sender).
- **Message-type classification** — the exact tells that type a message, and
  the fail-closed behaviour when tells conflict or are absent.
- **Case type** — how the classification maps to a case type and what happens
  when no type is available.
- **Case association** — the accepted predicates that link a message to an
  existing case.
- **Field extraction** — the label grammar and rules that populate an
  instruction draft from the provider's documents.
- **Presentation** — display labels and body-cleaning rules specific to the
  provider's mail shapes.
- **Evidence** — which attached/embedded material becomes case evidence.

## Documents

| Provider | Document |
| --- | --- |
| QDOS (Qdos Assist / Qdos Law) | [qdos.md](qdos.md) |

FRD-09 owns the accepted principal route/profile set. This directory is a
selective set of descriptive companions, not an activation inventory. An absent
companion does not make an accepted route review-only, and corpus membership
alone never activates one. Record actual evidence gaps without inventing rules.

## Operator SOP guides

`sop-guides/` holds the operator-supplied standard operating procedure
documents as received. They are business evidence for review, not behaviour
owners; the owning FRD, ADR, or Core policy still wins on any disagreement.

| Guide | File |
| --- | --- |
| QDOS SOP | `sop-guides/qdos_sop_guide.docx` |
| PCH SOP | `sop-guides/pch_sop_guide.docx` |
| SBL SOP | `sop-guides/sbl_sop_guide.docx` |
| Report sending SOP | `sop-guides/report_sending_sop_guide.docx` |
| EVA setup guide | `sop-guides/eva_setup_guide.docx` |
| AX salvage and salvage matrix | `sop-guides/ax_salvage.xlsx`, `sop-guides/ax_salvage_matrix.xlsx` |
| Unidentified and re-work reasons | `sop-guides/un_rw_reasons_sop.xlsx` |
