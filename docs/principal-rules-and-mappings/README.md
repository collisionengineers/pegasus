# Principal rules and mappings

The reviewed cross-provider evidence and criteria are held in the versioned
[principal-identification corpus](../../reference/workproviders-and-repairers/principal-identification-corpus.v1.json).
It contains one structured Received-and-Sent dossier for every operational
principal, historical-row dispositions, typed supporting identities, source
hashes, evidence groups, review states, and explicit gaps. It is review data,
not a runtime rule engine.

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

All other principals remain review-only in corpus version 1. Their absent or
unproved Received, Sent, route, classification, association, and extraction
criteria are recorded as gaps rather than inferred rules.
