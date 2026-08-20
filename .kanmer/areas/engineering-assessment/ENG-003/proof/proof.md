# Proof — ENG-003

Type: visual. Released in **release 14** (`d91fd7d7…`, PR #440), production smoke passed 2026-08-20; promoted to `main` (`39bb118a`).

- Live on QDOS26002's assessment page: the readiness panel shows exactly one amber chip — "25 issues detected" — with the full titled list under the native disclosure (hover/click), and the "Not ready" card names the count and points back ("see Readiness above for the list") instead of repeating it. No warning flood.
- Verification lane at the cut: `CombinedReadiness` superset guarantee, single `.blocker-list`, `IssueSummaryText` sole pluralisation owner, browser test present.
- Full transcript: DELIV-013 scratch.
