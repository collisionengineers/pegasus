## Independent review — PR #455 (orchestrator, 2026-08-20)

Verdict: **pass**, merge on green CI.

- The wrong-money-is-worse-than-no-money rule is structural: `EstimateParseRejectedException` on ANY pairing ambiguity, unreadable amount, missing identity, or section-checksum mismatch (labour/paint work-unit sums, parts sub-total, extras total verified against the document's own printed totals); no partial import can exist. Coordinate-baseline pairing was evidence-driven (corpus probing showed flat text extraction mis-associates values) — the hazard was proven before the design, not assumed.
- Custody home correctly argued: staff action inside an existing case → case-document path (`IAddCaseDocument`/StaffUpload), original retained before any draft lands; parse-first means a rejected parse retains nothing.
- Provenance lands on the TICK-093 model exactly (route/source-version/SHA-256 via `StartDraftAsync`); draft-until-Engineer-acceptance follows the MCP-06 unconfirmed precedent; the MCP route was cited as already-covered, not rebuilt.
- EXT-09's authority respected: the estimate→report cost derivation is explicitly NOT settled (real sample's printed labour money differs from hours×rate by rounding — the exact reason formula authority exists); "Repair cost figures" readiness still fires and the report says so.
- Parked (correctly, operator-blocked): a real Glass's export sample; re-import-replaces-draft semantics. capabilities.md EXT-12 row updated honestly.
- The simplification pass surfaced and fixed a GUID leak in a duplicate-submit message — good catch.
