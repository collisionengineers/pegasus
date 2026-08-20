# Checklist — TICK-047

- [x] Extend `RetainedMailDetail` and `GetRetainedMail` with the read-only MAIL-23 policy/binding-derived exact-folder recommendation and fail-closed unavailable result.
- [x] Render recommendation/provenance or an accessible unavailable state on authenticated exact-message detail, with no input or mutation control.
- [x] Add focused Core tests for configured, fail-closed, re-derived, and valid No action outcomes using existing fakes.
- [x] Add focused Web caller evidence and reconcile `docs/design/README.md` plus `docs/capabilities.md` to the local evidence tier.
- [x] Run proportional restore/build/tests and the four-lens simplification pass; apply findings and record dispositions.
- [ ] Commit and push the ticket branch, write the post-implementation report, open a PR to `dev`, and move TICK-047 to Review.

## Progress notes

Implementation starts from merged `origin/dev` `fb42ce15802d6bfa35ada3d26b006ba164c595f1`; no Outlook, Graph, Azure, or other external write is authorized or required.

Focused evidence: locked restore passed; Release solution build passed with 0 warnings/errors; `RetainedMailTests` passed 26/26; `MailWorkspaceWebTests` passed 16/16 against LocalDB.

Four-lens finding applied: removed opaque folder identity and classification/binding versions from the page projection. The exact binding remains an internal availability check, and MAIL-07 must re-read current state. Reuse: existing MAIL-23 policy/store and `GetRetainedMail`; efficiency: no store call for absent/ambiguous outcomes; altitude: no persistence, adapter, transaction, command or MCP surface.

Canonical non-corpus solution selection passed: Core 834/834, Architecture 98/98, Integration 800/800 (1,732 total). After adding the explicit re-derivation unit case, the final Release build again passed with 0 warnings/errors and focused retained-mail tests passed 27/27; the earlier final-markup Web class passed 16/16.
