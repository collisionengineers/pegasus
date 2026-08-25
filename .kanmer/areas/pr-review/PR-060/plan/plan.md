# Plan — PR-060: Correct ENG-016 migration commentary

## Approach

Edit only the explanatory comment block in `20260824123336_DropEvaHandoffTables.cs`. State the implemented split plainly: Export has an operation key recorded in `ActionHistory`; the surviving proxy is once per case and therefore no longer owns replay metadata; recovery after this permitted pre-cutover removal is roll-forward under ADR-0030. Leave every migration operation, designer, snapshot, runtime class, and test unchanged.

## Governing docs

- **Meets — `docs/adr/0030-non-additive-schema-changes-before-cutover.md`:** the comment will describe the accepted pre-cutover recovery rule as “roll forward, never back.” It will not advise clearing proxy rows, promise usable production rollback, or introduce compatibility machinery.
- **No modification:** ADR-0030 already owns the correct decision and does not need amendment. The ticket remains `docs_todo` only because the board's governing-doc validator currently resolves against the main checkout where ADR-0030 is not present; the implementation branch must retain the accepted file from `origin/dev`.

## Steps

1. Replace the migration's inaccurate leading commentary with three facts: Export carries an operation key and stores per-export replay/history in `ActionHistory`; `EvaFirstHandoffProxies` is only the once-per-case first-send fact; `Down()` is development migration scaffolding while supported recovery under ADR-0030 is roll-forward.
2. Compare the final wording with ADR-0030, the ENG-016 post-implementation report, and PR #539 description. Change those evidence surfaces only if an actual contradiction remains; do not broaden their content.
3. Verify that this ticket's diff changes comments only and passes whitespace checks, then record the exact disposition in the post-implementation report.

## Verification

- `git diff --check`
- Inspect `git diff -- src/Pegasus.Infrastructure/Persistence/Migrations/20260824123336_DropEvaHandoffTables.cs` and confirm `Up()`, `Down()`, generated metadata, and schema operations are byte-for-byte unchanged.
- Search the ENG-016 branch for the false claims that Export has no operation key or that proxy rows should be cleared for rollback.
- Re-read ADR-0030, PR #539's description, and ENG-016's post-implementation report for one consistent roll-forward statement.

No build or test run is required for a comment-only diff; ENG-016's normal branch verification remains responsible for the unchanged compiled migration.

## Risks / open questions

- **Accidental scope growth:** touching migration operations or recovery machinery would contradict the ticket. Mitigation: constrain the diff to the existing comment block and verify it directly.
- **Overstating `Down()`:** EF's generated reversal shape is not the supported recovery procedure. Mitigation: describe it only as development scaffolding and point recovery to ADR-0030.
- No unresolved questions.
