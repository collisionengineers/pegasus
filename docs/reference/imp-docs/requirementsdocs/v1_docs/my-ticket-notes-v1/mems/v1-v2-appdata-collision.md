---
name: v1-v2-appdata-collision
description: V1 and V2.0 share the same Documents\CE Document Mapper\providers.json; running V2 migrates it to a schema that breaks V1
metadata: 
  node_type: memory
  type: project
  originSessionId: a68ab3dd-9966-4cea-9a9c-9266ffb22ae6
---

`cedocumentmapper` (V1, `app.py:56` `APP_TITLE="CE Document Mapper"`) and `cedocumentmapper_v2`
(`src/cedocumentmapper_v2/ui/paths.py:76`) both resolve `APP_DATA_DIR` to
`Documents\CE Document Mapper\` and read/write the **same** `providers.json` (and `app_settings.json`).

The schemas are incompatible:
- V1 (flat): `{providers:[{detect_phrases:[...], field_rules:{<field>:{method,config}}}]}`
- V2: `{schema_version:2, providers:[{id, detect:{required_phrases,...,minimum_confidence}, field_rules:{<field>:{kind,...}}}]}`

V2 migrates V1→V2 **in place** (`service.py:47`, `config/migration.py`); it is one-way. After V2 has run,
V1 still loads the file without crashing but its normaliser yields **empty `detect_phrases` and empty
field configs for every provider** → V1 detects/extracts nothing. V1's `detect_provider` skips
phrase-less providers (`app.py:1365`), so nothing matches.

The shipped V1 build (`dist\app.exe`) is byte-identical (SHA256) to `Documents\CE Document Mapper\CE
Document Mapper V64.exe`. Note: the v1 `app.spec` does NOT bundle `providers.json` (the documented
`--add-data` build command does), so a clean-install V1 exe seeds only the 1-provider hardcoded
`DEFAULT_CONFIG`, not the full set. `view_mode` in `app_settings.json` is valid for both versions.

**2026-06-23:** restored the V1-schema `providers.json` (26 fully-configured providers, copied from the
repo root `cedocumentmapper\providers.json`) to the live path; backed up the displaced V2 file to
`Documents\CE Document Mapper\providers.v2.bak.20260623-140358.json`. Verified V1 then detects AX/EVA and
extracts fields. The two byte-identical V1 backups are `cedocumentmapper\providers.json` and
`cedocumentmapper\docs\Settings Backup\providers.json` (SHA256 E5ABCCC5…, 44586 bytes).

**Why:** the user works on V1 and V2 separately; this shared path silently corrupts V1's config whenever
V2 is launched, which looks like "V1 stopped detecting anything."

**How to apply:** if V1 "stops working" (no provider detected), check whether
`Documents\CE Document Mapper\providers.json` has gained `schema_version`/`detect`/`kind` keys — if so V2
re-migrated it; restore a V1-schema copy. The durable fix is to give V2.0 its own app-data dir
(`cli.py:381` `--app-data-dir`, or `DocumentMapperService(app_data_dir=...)`) — a V2-only change, leaving
V1 untouched.
