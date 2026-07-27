---
name: `0.1.0-alpha.1`-`Next`/`unallocated`-appdata-collision
description: `0.1.0-alpha.1` and `Next`/`unallocated`.0 share the same Documents\CE Document Mapper\providers.json; running `Next`/`unallocated` migrates it to a schema that breaks `0.1.0-alpha.1`
metadata: 
  node_type: memory
  type: project
  originSessionId: a68ab3dd-9966-4cea-9a9c-9266ffb22ae6
---

`cedocumentmapper` (`0.1.0-alpha.1`, `app.py:56` `APP_TITLE="CE Document Mapper"`) and `cedocumentmapper_v2.0`
(`src/cedocumentmapper_v2/ui/paths.py:76`) both resolve `APP_DATA_DIR` to
`Documents\CE Document Mapper\` and read/write the **same** `providers.json` (and `app_settings.json`).

The schemas are incompatible:
- `0.1.0-alpha.1` (flat): `{providers:[{detect_phrases:[...], field_rules:{<field>:{method,config}}}]}`
- `Next`/`unallocated`: `{schema_version:2, providers:[{id, detect:{required_phrases,...,minimum_confidence}, field_rules:{<field>:{kind,...}}}]}`

`Next`/`unallocated` migrates `0.1.0-alpha.1`→`Next`/`unallocated` **in place** (`service.py:47`, `config/migration.py`); it is one-way. After `Next`/`unallocated` has run,
`0.1.0-alpha.1` still loads the file without crashing but its normaliser yields **empty `detect_phrases` and empty
field configs for every provider** → `0.1.0-alpha.1` detects/extracts nothing. `0.1.0-alpha.1`'s `detect_provider` skips
phrase-less providers (`app.py:1365`), so nothing matches.

The shipped `0.1.0-alpha.1` build (`dist\app.exe`) is byte-identical (SHA256) to `Documents\CE Document Mapper\CE
Document Mapper V64.exe`. Note: the v1 `app.spec` does NOT bundle `providers.json` (the documented
`--add-data` build command does), so a clean-install `0.1.0-alpha.1` exe seeds only the 1-provider hardcoded
`DEFAULT_CONFIG`, not the full set. `view_mode` in `app_settings.json` is valid for both versions.

**2026-06-23:** restored the `0.1.0-alpha.1`-schema `providers.json` (26 fully-configured providers, copied from the
repo root `cedocumentmapper\providers.json`) to the live path; backed up the displaced `Next`/`unallocated` file to
`Documents\CE Document Mapper\providers.v2.bak.20260623-140358.json`. Verified `0.1.0-alpha.1` then detects AX/EVA and
extracts fields. The two byte-identical `0.1.0-alpha.1` backups are `cedocumentmapper\providers.json` and
`cedocumentmapper\docs\Settings Backup\providers.json` (SHA256 E5ABCCC5…, 44586 bytes).

**Why:** the user works on `0.1.0-alpha.1` and `Next`/`unallocated` separately; this shared path silently corrupts `0.1.0-alpha.1`'s config whenever
`Next`/`unallocated` is launched, which looks like "`0.1.0-alpha.1` stopped detecting anything."

**How to apply:** if `0.1.0-alpha.1` "stops working" (no provider detected), check whether
`Documents\CE Document Mapper\providers.json` has gained `schema_version`/`detect`/`kind` keys — if so `Next`/`unallocated`
re-migrated it; restore a `0.1.0-alpha.1`-schema copy. The durable fix is to give `Next`/`unallocated`.0 its own app-data dir
(`cli.py:381` `--app-data-dir`, or `DocumentMapperService(app_data_dir=...)`) — a `Next`/`unallocated`-only change, leaving
`0.1.0-alpha.1` untouched.
