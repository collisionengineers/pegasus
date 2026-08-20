# Proof — TICK-039 (INT-14)

Type: command-log. Delivered by the SIMPLI-013 parsers in **release 14** (`d91fd7d7…`, PR #449), production smoke passed 2026-08-20; promoted to `main` (`39bb118a`).

- Verification lane at the cut: `.doc` detection and dispatch to `ReadDoc` (`WordBinaryExtractor` over the bounded compound-file reader); fail-closed issue set (`unreadable-doc-file`, `encrypted-doc-file`, limits, partial-extraction), `doc-engine` evidence, embedded objects/macros never opened; end-to-end web-caller tests `DirectLegacyDocTextIsExtractedThroughWebCaller` and the unreadable-container fallback test.
- Live: production Upload accepts `.doc`; the deployed worker pipeline extracts it in-process. Capabilities register keeps the honest note that operator acceptance is separate evidence.
- Full transcript: DELIV-013 scratch.
