# @pegasus/design-system

React bindings for the Pegasus operator interface, built so the
[Claude Design](https://claude.ai/design) agent designs with Pegasus's real
parts. Every component renders the exact markup and class names that
`src/Pegasus.Web/wwwroot/css/site.css` styles; the stylesheet itself is copied
byte-for-byte to `dist/styles.css` at build time and is never authored here.

This package is **design-tool output only**: it is not part of `Pegasus.slnx`,
is not referenced by the Web runtime, and is not deployed. `docs/design/README.md`
remains the design authority; this package follows it.

## Build

```sh
cd docs/design/system
npm ci
npm run build      # dist/index.js (ESM), dist/*.d.ts, dist/styles.css
```

## Sync to Claude Design

From the repository root, run the `/design-sync` skill. Config lives in
`.design-sync/config.json`; per-component preview stories in
`.design-sync/previews/`; the design agent's conventions header in
`.design-sync/conventions.md`; per-component usage docs (which also set the
component group) in `docs/`.

## Layout

- `src/components/*.tsx` — grouped components (Shell, Actions, Status,
  Metrics, Record, Tables, Forms, Overlay, Auth, Layout, Icon, StatusChip).
- `src/logo.png` — downscaled brand logo (checksum recorded in
  `docs/design/README.md`), inlined as a data URI.
- `docs/<Name>.md` — usage doc per component; `category:` frontmatter = group.
- `scripts/build.mjs` — esbuild + stylesheet copy.
