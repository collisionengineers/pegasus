# Principal rules and mappings

One document per work provider (principal), explaining in one place how that
provider's inbound email is identified, classified, typed, associated, and
mapped into Pegasus: the exact criteria in force, the policy versions that
implement them, and pointers to the exact files that own each rule.

These documents are **descriptive companions**, not behaviour owners. The
binding behaviour stays with the owning FRDs, ADRs, and Core policy code they
cite; if a document here disagrees with the cited owner, the owner wins and
the document is corrected. Update the provider's document in the same task
whenever a cited policy version or criterion changes.

Each document covers, for its provider:

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
