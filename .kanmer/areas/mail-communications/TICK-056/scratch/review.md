# Independent review — 2026-08-21 — PR #492 at `b78705d5b48d4f689e9981ce93ca34a6ba978c8a`

## Changes

- `docs/capabilities.md` records exact local UI-10 caller/test evidence without claiming deployment or mailbox mutation.
- `Pages/Mail/Index.cshtml.cs` adds one authenticated GET preview handler that delegates to the existing Core-owned `GetRetainedMail` and returns only the seven FRD-08 display facts.
- `Pages/Mail/Index.cshtml` preserves the semantic table and its mailbox/folder/queue/search/page-aware full-message link, adding selected-row attributes and one adjacent evidence-only `aside`.
- Existing `site.js` adds abortable pointer/focus selection, safe text-only rendering, cached exact reads, failure state, and focus/pointer dismissal. Existing `site.css` adds selected state, desktop table/preview panes, constrained ordered stacking, and forced-colour treatment.
- Focused Web and Browser tests prove authentication, exact/not-found scoping, no state mutation or preview actions, keyboard/pointer selection, focus-away dismissal, no-JavaScript full-detail navigation, axe, desktop adjacency, constrained/200%-equivalent stacking, and no document overflow.
- No Core, Infrastructure, EF, schema, migration, query policy, message action, new framework, or bitmap asset changed.

## Comments

- No blocking findings.
- No non-blocking findings.
- The image-generated concept is correctly treated as a transient, non-normative layout constraint: table primary, selected row, adjacent desktop preview, ordered constrained fallback. FRD-08 and `docs/design/README.md` remain authoritative; no generated bitmap or invented action entered the diff.
- The reported shared-browser `net::ERR_NO_BUFFER_SPACE` failure was on unchanged `/Administration/Mailboxes`; its isolated branch rerun passed, and the complete GitHub browser job subsequently passed.

## Disposition

- No review change was required and no PR Review ticket was filed.
- PIR inventory matches all seven changed paths and honestly records the implementation, design-system build, four simplicity lenses, external-write boundary, and local browser anomaly.
- Open questions contain no unresolved item. The linked FRD is met without modification or an ADR.
- Simplicity passes: one existing Core read owner, one thin Web handler, one existing asset bundle, no second projection/store/policy, no action registry, no speculative controls, and no unconsumed abstraction.

## Verdict

**Pass.** Independent review checked the full ticket and both epic contexts, governing FRD/design rules, plan/checklist/PIR against the complete diff, exact PR head and mergeability, `git diff --check`, authenticated interaction/accessibility behavior, and all CI.

Independent local evidence at the exact head:
- focused preview Web + Browser: 3/3 passed;
- full `MailWorkspaceWebTests`: 39/39 passed.

GitHub run `32430416370` is fully green: changes, documentation, local scripts, reference data, unit, browser, all three SQL shards, and SQL coverage passed; infrastructure correctly skipped.
