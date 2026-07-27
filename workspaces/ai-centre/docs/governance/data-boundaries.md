# Data boundaries

Management's historical authorisation permits bounded workstation evaluation of approved source
material. The root repository boundary still controls custody: private corpus material remains
external under ignored `corpus/`, and complete Box or Outlook archives are not imported. See
[data-authorisation.md](data-authorisation.md).

Purpose, evidential authority, source role, licence metadata, client boundary, retention, and deletion
must be preserved so each bounded evaluation is auditable.

| Class | Examples | Git | External model/service | Training |
|---|---|---|---|---|
| Public/synthetic | Schemas, fake fixtures, public-domain examples | Allowed after review | Per approved provider policy | Only with recorded licence |
| Internal approved knowledge | CE-authored playbooks and approved templates | Usually private repo; minimise | Only approved deployment and purpose | Only if manifest permits |
| Authorised case/archive data | Instructions, email, images, reports, registrations, personal data | Never; keep externally under `corpus/` custody | Only within a separately approved technical/provider boundary | Bounded extracts only through a versioned dataset manifest |
| Licensed ephemeral | Per-job OEM/repair/valuation material | Never persist beyond terms | Only if licence and provider permit | Never by default |
| Secrets/credentials | Tokens, passwords, certificates, portal credentials | Never | Never | Never |

## Repository and external layout

- external ignored `corpus/` — immutable approved source inputs and bounded evaluation extracts;
  never copied into this workspace;
- `ml-ops/datasets/` — versioned recipes, schemas, manifests, cards, and synthetic fixtures only;
- root `artifacts/` — generated run and evaluation outputs;
- `models/` — model cards, configs, manifests, and artifact references; no private training corpus.

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
