# EV-2026-07-24: text/image output boundary

Evidence label: **Locally verified** for the stated checks only. This is not format conformance, differential verification, genuine-corpus acceptance or CollisionSpike caller acceptance.

## Boundary exercised

- Port units: `EXT-API-001`, `EXT-API-003`, `EXT-CLI-002` and `EXT-NEST-001`.
- Specification/configuration identity: `EXT-API-001/2026-07-23`, default CollisionSpike 10 MB policy.
- Host: Windows, .NET SDK 10.0.302, PowerShell 7.6.4.
- Public API: `DocumentExtractor`; format handler entry points are internal.
- Output: ordered text, signature-recognised image assets and control evidence only.

## Automated verification

Command:

```powershell
pwsh -NoLogo -NoProfile -File scripts/Invoke-RepoCheck.ps1
```

Exit: `0`. Locked restore, formatting, Release build, MTP tests, JSON parsing and local Markdown-link checks passed. A final post-documentation rerun used `-SkipRestore` against the unchanged lock state and also exited `0`. Build result was zero warnings and zero errors. Final test result was 534 total, 533 passed, one explicitly opt-in local EML cohort skipped, zero failed.

An earlier run correctly exposed one stale security assertion that still expected a hostile non-image attachment to be returned as an asset. The assertion was migrated to require zero assets, one bounded `nonPayload.binary` descriptor and `NON_IMAGE_ASSET_NOT_EMITTED`; the focused 21-test security project and final repository check then passed.

The added boundary cases prove:

- mixed MIME image/non-image inputs emit only the recognised image bytes;
- unsupported binary bytes retain a stable hash descriptor without becoming an asset or changing `Complete` solely because they are non-payload;
- a claimed image with no recognised signature is omitted and forces `Partial`;
- supported nested content is parsed before its non-image parent source bytes are removed;
- unsupported nested bytes are not emitted and do not double-charge the root input budget;
- CLI bundles create only stable image files and create no `assets/` directory for a non-image-only input;
- the result schema pins `kind` to `image` and restricts the current media-type set;
- all five format-handler entry types are non-public, leaving `DocumentExtractor` as the supported extraction entry point; and
- passive DOCX hyperlinks remain informational while external relationships that may hide required text/images remain incomplete.

## Caller-selected local samples

Inputs were selected explicitly and were not scanned recursively. Names and contents are not recorded here.

| Input class | SHA-256 | Bytes | Exit/outcome | Text segments | Image files | Issues |
|---|---|---:|---|---:|---:|---|
| DOCX | `9873bdd8f79bc76534a4108fac70c708fee7d5f07ab28500831727f22213e673` | 217,648 | `0` / `Complete` | 193 | 7 | 18 informational: dependency inventory, drawing passive, external hyperlink |
| DOC | `30ba3639d8b2804010f077e125f287c0ffe9e763aee1224b44f5596a2cd447f6` | 114,688 | `10` / `Partial` | 130 | 0 | 37: explicit unimplemented structural/semantic DOC branches plus three non-image descriptors |

Ignored outputs are under `artifacts/evaluation/20260724-scope-pass-matty/` and `artifacts/evaluation/20260724-scope-pass-doc/`.

## Limitations and next gates

- Current image admission recognises PNG, JPEG, GIF, TIFF, BMP, WebP, ICO, WMF and EMF signatures; complete structural validation and safe dimension/pixel accounting for every codec remain required.
- SVG is deliberately not emitted because active/external-content handling and sanitisation have not been designed.
- The DOC sample remains honestly `Partial`; this change does not close the DOC structural semantics listed in its result or compatibility matrix.
- Formal format conformance, fuzz/property breadth, pinned differential tools, Linux evidence, genuine-data cohorts/holdouts, performance budgets, independent review and real caller acceptance remain outstanding.
