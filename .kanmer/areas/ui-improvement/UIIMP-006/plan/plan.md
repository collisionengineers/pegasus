# Plan — UIIMP-006 rewrite the design authority

Diff estimate: one file, `docs/design/README.md`, ~1,340 lines replaced by
~950 lines. Docs-only; no code, no assets, no checksums (bytes land with
PLAT-029, which records them).

## Steps

1. Transcribe group `context.md` §1 and D1–D13 into the README: shell,
   tokens (verified against the prototype's `html[data-design="integrated"]`
   block), typography (vendored Inter, D13), shape/spacing/breakpoints,
   assets (logo mapping fix; four unplaced marks not in the tree), icons
   (Lucide v0.344.0 ids for the prototype set plus the five undefined
   glyphs), component/class vocabulary incl. CSP utility classes, routes and
   301 stubs, keyboard/dialog contract, amended "Absent versus disabled"
   (D7), removed surfaces, reviewed divergences (§1.15 + retired
   `Send to Claude` flourish).
2. Keep verbatim: Evidence discipline (framing), Test UI, Voice, No
   explanatory copy and page economy, Accessibility, Change and verification
   rule; keep the enforced presentation rules 1–7 (rule 6 amended per D7).
   Preserve headings other documents anchor to: `The Pegasus marks`,
   `Contracts`, `Deferred integration and intake surfaces`,
   `No explanatory copy and page economy`, `UI specification`.
3. Replace the `0.1.0-alpha.1` inventory in Operator experience requirements
   and UI specification with the §1 contract, worded as planned / delivered
   by PLAT-029 (wave 1) and EPIC-011 waves 2–5.
4. Run `pwsh ./scripts/Test-DocumentationLinks.ps1`; open the PR to `dev`.

Reuse: existing README structure and headings; no new file.
