# Data boundaries

Collision Engineers has expressly authorised this project to use and share the current corpus and its
complete Box and Outlook archives for workstation and model development. The recorded scope is in
[data-authorisation.md](data-authorisation.md).

That permission resolves the project-level data-use question for these named sources. Purpose,
evidential authority, source role, licence metadata, client boundary, retention, and deletion still
need to be preserved so the data is used correctly and outputs remain auditable.

| Class | Examples | Git | External model/service | Training |
|---|---|---|---|---|
| Public/synthetic | Schemas, fake fixtures, public-domain examples | Allowed after review | Per approved provider policy | Only with recorded licence |
| Internal approved knowledge | CE-authored playbooks and approved templates | Usually private repo; minimise | Only approved deployment and purpose | Only if manifest permits |
| Authorised case/archive data | Instructions, email, images, reports, registrations, personal data | Allowed in this repository | Allowed within an approved technical/provider boundary | Authorised; build through a versioned dataset manifest |
| Licensed ephemeral | Per-job OEM/repair/valuation material | Never persist beyond terms | Only if licence and provider permit | Never by default |
| Secrets/credentials | Tokens, passwords, certificates, portal credentials | Never | Never | Never |

## Local layout

- `ml-ops/data/private/raw/` — authorised source snapshot; retain original bytes and provenance.
- `ml-ops/data/private/work/` — extraction and annotation workspace.
- `ml-ops/datasets/` — versioned recipes, schemas, manifests, cards, and synthetic fixtures only.
- `ml-ops/artifacts/` and `ml-ops/runs/` — run outputs pending the chosen registry/versioning policy.
- `models/` — model cards, configs, manifests, and promoted artifacts where practical.

## Promotion gate

A dataset manifest must identify purpose, owner, the recorded authorisation, sources and roles, licence
classes, permitted tasks, excluded material, minimisation, pseudonymisation, lineage, deduplication,
case/time split, retention, deletion propagation, access, location, review date, and approval. Training
code must fail closed when the manifest is missing. This is a reproducibility and control gate, not a
new request for permission to use the named archives.

## Logging and examples

Use opaque synthetic case IDs in tests and documentation. Logs store hashes, counts, classifications,
durations, and redacted error codes where possible—not message bodies, report text, or image content.
Screenshots and demos use designed synthetic cases.
