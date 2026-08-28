# Files — DOCS-015

## Changed files

| Path | Change | Risk |
| --- | --- | --- |
| `docs/json-extraction-parity/eva-api-docs.pdf` | Add the operator-supplied source unchanged | Binary source must remain byte-identical |
| `docs/json-extraction-parity/eva-api-docs.md` | Add complete normalized transcription | Table reconstruction or glyph normalization could lose data |

## Context files

| Path | Why |
| --- | --- |
| `docs/frd/frd-07-eva-and-external-engineering-handoff.md` | Governs the EVA integration boundary |
| `docs/json-extraction-parity/Final-Format-Example-02.json` | Establishes why exact JSON/API field parity matters |
| [[TICK-077]] | Active implementation ticket; its files and worktree are out of scope |

## Out of scope

No application code, credential, runtime configuration, request payload or EVA
endpoint call is changed.
