# Renderer parity gate (Wave 2)

Cross-engine go/no-go check for the renderer convergence: renders the **same**
valuation case through both engines and compares the outputs **structurally**
(never by sha256 — different engines never byte-match).

- **OLD** — the canonical Python `vehicle-valuation` skill (Jinja2 → WeasyPrint):
  `active/collision-agent-skills/vehicle-valuation/scripts/render_report.py` + `render_evidence_pack.py`
- **NEW** — the .NET `CollisionRenderer.Mcp` host (Scriban → Chromium):
  `render_valuation_outputs` driven over stdio MCP

## What it asserts

1. **Validation parity** — both engines accept a valid payload.
2. **No dropped fields** — every subject/advert value the Python report shows also
   appears in the .NET report (the core convergence risk). It distinguishes a real
   drop from "both engines omit this field" (parity-consistent).
3. **Page-count parity** — report and pack page counts within tolerance.
4. **Capture-append parity** — every captured-advert marker appears in both packs,
   and both packs grow by one appended page per capture.

## Prerequisites

- Python deps (already present in this repo's dev env): `weasyprint`, `jinja2`, `pypdf`, `reportlab`.
- The Python validator's contracts package on `PYTHONPATH` — the harness wires
  `active/connectors/valuation-adverts-connector/contracts/generated/python` automatically.
- The built .NET host exe (default `src/CollisionRenderer.Mcp/bin/Release/net8.0/collisionrenderer-mcp.exe`):
  `dotnet build src/CollisionRenderer.Mcp -c Release`
- Playwright Chromium installed (`collisionrenderer-mcp.exe` will fetch it on first render, or run `install_browser`).

## Run

```pwsh
python tests/parity/parity_check.py            # all cases under cases/
python tests/parity/parity_check.py --keep     # keep temp work dirs for inspection
python tests/parity/parity_check.py --exe <path-to-host-exe>
```

Exit code `0` = all cases pass, `1` = any failure. Add cases by dropping a
snake_case valuation payload JSON into `cases/`.

> Not part of `dotnet test`: this is a cross-language harness (Python + the built
> .NET exe + real Chromium/WeasyPrint), run manually or in CI before the Wave-4 cutover.
