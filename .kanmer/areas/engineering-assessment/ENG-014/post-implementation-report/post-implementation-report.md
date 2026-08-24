# Post-implementation report

PR **#527** — https://github.com/collisionengineers/pegasus/pull/527
Branch `task/eng-014-drop-manifest-indent-json`, four commits, base `dev`.

## What shipped

`manifest.sha256` and `provenance.json` are no longer produced, persisted or
reconstructed anywhere. The archive is the ordered thirteen-key JSON and
`Images/`, on both the hand-off and the operator-export path, and the JSON is
two-space indented.

| Commit | Change |
| --- | --- |
| `e65956a3` | `EvaBundleSchema`: `WriteProvenance`, `WriteManifest`, the two filename constants, the two `WriteEntry` calls and three `EvaBundle` members deleted; `WriteOrderedJson` indents. |
| `3c274c1f` | Three `EvaHandoffRevisions` columns dropped, with a reversible migration. |
| `dc19f867` | Tests: the layout/entry-list regression guard added, manifest-grammar test deleted, provenance readers re-pointed. |
| `bb3d79c3` | `docs/current-architecture.md` records the shape that now ships. |

`WriteArchive` lost two parameters. No enum, flag, parameter or wrapper was
added — with the manifest gone from both paths there is one packaging left and
nothing to configure.

**Kept, as the ticket directed:** the whole-archive `Sha256`, `JsonSha256`, and
the in-memory `EvaFieldProvenance[]` with `ValidateSource`'s use of it.

## Verification

| Check | Result |
| --- | --- |
| `dotnet build --configuration Release` | Succeeded, 0 warnings |
| `Pegasus.Core.Tests` (full) | **937 passed, 0 failed** |
| `Pegasus.ArchitectureTests` (full) | **99 passed, 0 failed** (1m24s) |
| `EvaHandoffPersistenceTests` | **8 passed, 0 failed** (2m35s) |
| `CustodyOutboxIntegrationTests` | Running locally at the time of writing; CI is the binding result |
| Migration up → down → up on LocalDB | Clean. Columns confirmed dropped (13 remain), restored by `Down()`, dropped again. |
| `scripts/Test-MigrationGrants.ps1` | Passes — 67 migration files checked. The migration creates no table, so it has nothing to grant. |
| Layout vs `reference/eva_information/AX_SP58WVO.json` | Head and tail bytes **identical**; per-line skeleton identical; keys identical and in order. |

The reference-sample comparison was done with a throwaway probe that fed the
sample's own thirteen values through `CreateOfflineReplay` and compared bytes.
The probe was deleted before the first commit; it is not in the diff.

### One flake, disclosed

The first `EvaHandoffPersistenceTests` run reported **2 failed of 8**, both
inside `SeedCaseAsync` — database setup, not an assertion about this change. It
ran immediately after I dropped a LocalDB database, so LocalDB was still busy.
The clean re-run (8/8) is the result recorded above. Worth knowing rather than
quietly re-running.

## Findings recorded, not silently absorbed

1. **The ticket says "four columns"; there are three** — `ProvenanceContent`,
   `ProvenanceSha256`, `ManifestContent`. Confirmed against the entity, the
   model snapshot, and the live schema after applying the migration.

2. **`JsonWriterOptions.NewLine` defaults to `Environment.NewLine`.** Verified
   by running it. Left alone, `Indented = true` would have made the archive
   bytes — and so `InputFingerprint` and the download `Content-Digest` — differ
   between a Windows and a Linux run, and CI runs both. Pinned to `"\r\n"`,
   which is also what all three known-good samples use at byte level. This is
   the one decision that goes beyond the ticket's literal text.

3. **The migration is not additive**, against the runbook's roll-forward rule.
   An application built before this change lists the three columns in its
   insert, so rolling the app back behind the migration fails EVA hand-off
   *generation* until it is rolled forward. Nothing is lost. Flagged in the
   migration comment and raised for the reviewer in the PR as an accept-or-
   convert-to-two-step decision.

4. **A 7-byte difference against the reference sample that is not layout** —
   non-ASCII escaping (`’` → `’`) and `CaseEvaMapping` stripping the
   sample's trailing whitespace in `Inspection Address`. Both pre-date this
   change and are JSON-semantically identical. Out of scope; noted for
   [[ENG-015]].

## Consequence accepted, not fixed

Archive bytes change, so `InputFingerprint` changes. Existing
`EvaHandoffRevisions` rows stop deduping against a regenerated bundle — a
regeneration makes Revision 2 rather than replaying Revision 1. Nothing breaks;
replay identity across this boundary is lost. Stated in the PR.

## Not done

- No Azure or cloud operation of any kind, read or write.
- `docs/frd/frd-07-*`, `docs/operator-notes.md`, `docs/capabilities.md`,
  `docs/design/README.md`, `docs/open-decisions.md`, `docs/runbook.md` untouched
  — DOCS-013 (#526) owns them.
- No historic `Migrations/*.Designer.cs` snapshot edited.
- Not merged. PR is open against `dev` for independent review.

---

## Final state (updated after the coordinator's encoder addition)

PR **#527**, five commits, **CI fully green**
(run `32715786200`: `unit`, `browser`, `sql-integration (1|2|3)`,
`sql-integration-coverage`, `changes`, `documentation`,
`local-development-scripts`, `reference-data` all pass; `infrastructure`
skipping as normal). Ticket left in **review** — not merged.

### Fifth commit — `c1beaf3a`

`Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping` on the same
`JsonWriterOptions`, so non-ASCII is written as literal UTF-8 the way the
predecessor extractor's `ensure_ascii=False` does. Guarded by
`NonAsciiTravelsAsLiteralUtf8RatherThanEscapes`. Also adds the new migration to
the exact list `IntakePersistenceIntegrationTests` asserts.

### Corrected verification numbers

| Check | Result |
| --- | --- |
| `dotnet build --configuration Release` | Succeeded, 0 warnings |
| `Pegasus.Core.Tests` | **938 passed, 0 failed** |
| `Pegasus.ArchitectureTests` | **99 passed, 0 failed** |
| `EvaHandoffPersistenceTests` (local) | 8 passed, 0 failed |
| `CustodyOutboxIntegrationTests` (local) | 22 passed, 1 LocalDB connection timeout |
| CI, all shards | **Green** |
| `Final Format Example 02.json` | **Byte-for-byte identical**, 656 bytes |
| `AX_SP58WVO.json` | One line differs — trailing-whitespace normalisation only |

### The two CI/local failures, both explained

1. **`sql-integration (3)`, first CI run — real, and mine.**
   `CommittedMigrationCreatesTheSqlServerSchema` asserts the committed
   migration list exactly and I had not added the new migration to it. Fixed in
   `c1beaf3a`; that shard is green.

2. **`ExportingACaseProducesTheEvaFormatArchive`, local — not real.** A
   `SqlException: Connection Timeout Expired ... post-login phase` against
   LocalDB while I had builds running concurrently. The same test ran green on
   CI's `sql-integration (2)` in both runs. Recorded rather than quietly
   re-run.

### Findings 1–3 stand; finding 4 is retired

The escaping half of finding 4 is now fixed by the encoder change. What remains
is `CaseEvaMapping` stripping trailing whitespace in `Inspection Address` — a
mapping-normalisation question for [[ENG-015]], not a packaging one.

Findings 1 (three columns, not four), 2 (pinned newline) and 3 (non-additive
migration) are unchanged and are all raised in the PR body for the reviewer.
