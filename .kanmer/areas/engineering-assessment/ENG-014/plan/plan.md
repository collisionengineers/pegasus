# Plan

One packaging, one writer, no flag. Every step is a deletion except the
indent flip and the new pinning test.

## 1. `EvaBundleSchema.cs` — delete the two files and indent the JSON

Reuses: nothing new. `WriteEntry`, `Hash`, `SafeFileComponent`,
`ValidateAndNameImages` and `ValidateSource` all stay exactly as they are.

- Delete `ProvenanceFileName`/`ManifestFileName` (`:557-558`), `WriteProvenance`
  (`:773`), `WriteManifest` (`:835`).
- `WriteArchive` loses its `provenance`/`manifest` parameters and the two
  trailing `WriteEntry` calls; `CreateOfflineReplay` loses the three locals and
  the three constructor arguments.
- `EvaBundle` loses `ProvenanceContent`, `ProvenanceSha256`, `ManifestContent`
  (`:50-52`). Positional record — every construction site is a compile error,
  which is the checking mechanism.
- `WriteOrderedJson` (`:756`): `Indented = false` → `true`.
- The `using System.Text;`/`StringBuilder` need in that file drops with
  `WriteManifest`; check whether `Encoding` is still used (it is, by
  `WriteArchive`'s `ZipArchive` ctor) before removing the using.

### Newline: set it explicitly, do not take the default

Verified by running it (net10.0, SDK 10.0.302), not assumed:
`JsonWriterOptions.NewLine` defaults to `Environment.NewLine`. With `Indented
= true` and the default left alone, the archive bytes — and therefore
`EvaBundle.Sha256`, and therefore `InputFingerprint` — would differ between a
Windows and a Linux run. This file's contract is "replay-identical … hashes
are explicit", and CI runs both `windows-latest` and `ubuntu-latest`, so that
is a live defect, not a theoretical one.

So `WriteOrderedJson` sets `NewLine = "\r\n"` alongside `Indented = true`.
`"\r\n"` rather than `"\n"` because all three known-good samples use CRLF —
checked at byte level: `AX_SP58WVO.json`, `Final Format Example 02.json` and
`old-extraction-working/QDOS_NX14AXY.json` all open `7B 0D 0A 20 20 22`. A
probe of `Utf8JsonWriter` with `Indented = true` on this SDK emits exactly
that prefix. This is a fixed constant in one writer, not a configuration knob.

## 2. Persistence

Reuses: the existing `EvaHandoffRevisionEntity` shape and
`EvaHandoffModelConfiguration`; no new entity, no new store method.

- `EvaHandoffEntities.cs:16-18` — delete the three properties.
- `EvaHandoffModelConfiguration.cs:24` — delete the `ProvenanceSha256` line.
- `EvaHandoffStore.cs` — delete the three assignments in `NewRevision` and the
  three arguments in `Bundle(revision)`.

**Correction to the ticket:** it says "the four `EvaHandoffRevisions`
columns". There are **three**: `ProvenanceContent`, `ProvenanceSha256`,
`ManifestContent`. `JsonContent`/`JsonSha256`/`BundleContent`/`BundleSha256`
all stay.

## 3. Migration

`dotnet ef migrations add DropEvaHandoffProvenanceAndManifest` — scaffolded,
not hand-written, so the snapshot regenerates correctly. Expect three
`DropColumn` in `Up()` and three `AddColumn` in `Down()`. No `CreateTable`, so
`scripts/Test-MigrationGrants.ps1` has nothing to check; run it anyway.
Verify `Up` then `Down` then `Up` against LocalDB.

## 4. Tests

Reuses the existing fixtures throughout — `Source()`, `Images()`, `Image()` in
`EvaBundleContractTests`; `SeedCaseAsync`/`SeedImageAsync`/`Factory` in
`EvaHandoffPersistenceTests`. No new fixture, no new helper.

- `EvaBundleContractTests`: strip the two names from the three entry lists,
  drop the provenance/manifest assertions, delete
  `BusinessReadableEntryNamesAndManifestGrammarAreExact` whole (its subject is
  gone; the entry-name half it also carried moves into the new test below).
- **New** `ExportedJsonIsTwoSpaceIndentedAndTheArchiveCarriesOnlyJsonAndImages`:
  asserts the entry list exactly, and asserts the JSON *bytes* — starts
  `{\r\n  "Work Provider": `, every line after the first is `  "` or `}`, 13
  keys in order. This is the ticket's regression guard: nothing pins the
  layout today except transitively through SHA equality.
- `CaseOperatorExportTests`: add the exact entry-list assertion to the export
  path so "both paths, one packaging" is pinned on both.
- `CustodyOutboxIntegrationTests:1276-1318`: replace the two `Assert.Contains`
  and the manifest-hash loop with one exact entry-list assertion.
- `EvaHandoffPersistenceTests`: delete the three seed lines; re-point `:255` at
  the archive's `Images/` entries (the excluded occurrence's file must not
  appear) and `:349` at the exported JSON's `VRM` value (the staff-corrected
  registration is what ships). Rename `:308` accordingly — it can no longer
  claim to observe the *status*, only the corrected value.

## 5. Docs

`docs/current-architecture.md:526` — drop "provenance and manifest" from the
reuse list and state the shipped shape (indented JSON + `Images/`).

## Consequence, stated not fixed

The archive bytes change, so `InputFingerprint` changes. Existing
`EvaHandoffRevisions` rows stop deduping against a regenerated bundle: a
regeneration makes Revision 2 rather than replaying Revision 1. Nothing
breaks; replay identity across this boundary is lost. Recorded in the PR.

## Verification

- `dotnet build --configuration Release`
- `dotnet test` — Core/EVA filters first, then the integration suites in
  chunks (~28 min full).
- Migration up/down/up on LocalDB; `scripts/Test-MigrationGrants.ps1`.
- Byte-diff the produced JSON against `reference/eva_information/AX_SP58WVO.json`
  for layout.

---

## Simplification pass — 2026-08-24

Run over this branch's own diff (reuse, simplification, efficiency, altitude).
The change is overwhelmingly deletion, so most of the pass had nothing to bite
on; four findings, three applied.

| # | Lens | Finding | Disposition |
| --- | --- | --- | --- |
| 1 | Altitude | `CustodyOutboxIntegrationTests` asserted `bundle.Sha256 == SHA256(bundle.Content)` — a `Pegasus.Core` invariant being paid for inside a LocalDB integration test that takes minutes. | **Applied.** Moved to `EvaBundleContractTests`, and extended to `JsonSha256` while it was there. The integration test keeps only what is genuinely integration-level: the entry list and the indented JSON. |
| 2 | Convention | `System.Text.Encoding.UTF8` written fully-qualified in `CaseOperatorExportTests`, where every other type comes from a using. | **Applied.** Added `using System.Text;`. |
| 3 | Convention | The explanatory comment on the renamed persistence test sat *between* `[Fact]` and the signature; the codebase puts it above the attribute. | **Applied.** Moved above `[Fact]`. |
| 4 | Simplification | `CustodyOutboxIntegrationTests` now makes three assertions (JSON present, two `Images/`, three entries total) where one exact entry-list equality would read better. | **Not applied.** The image entry names carry a seeded ordinal prefix, so an exact list would couple the test to seeding order for no extra coverage. The three counted assertions pin the same fact without that brittleness. |

No abstraction, flag, parameter or enum was added anywhere — with the manifest
gone from both paths there is one packaging, so `WriteArchive` lost two
parameters rather than gaining a switch.

## Findings from implementation, recorded not fixed

1. **The ticket says "four columns"; there are three.** `ProvenanceContent`,
   `ProvenanceSha256`, `ManifestContent`. Confirmed against the entity, the
   model snapshot and the live schema after applying the migration.

2. **`JsonWriterOptions.NewLine` defaults to `Environment.NewLine`.** Verified
   by running it, not assumed. Left at the default, `Indented = true` would
   have made the archive bytes — and therefore `InputFingerprint` and the
   download `Content-Digest` — differ between a Windows and a Linux run, and CI
   runs both. Pinned to `"\r\n"`, which is also what all three known-good
   samples use at byte level. This is the one decision in the change that goes
   beyond the ticket's literal text; it is a fixed constant, not a knob.

3. **This migration is not additive**, and the runbook's release rule
   (`docs/runbook.md`, "schema is roll-forward only … releases keep migrations
   additive so the previous application runs against the newer schema") assumes
   additive ones. An application built before ENG-014 lists these three columns
   in its `EvaHandoffRevisions` insert, so rolling the app back behind this
   migration fails EVA hand-off *generation* until it is rolled forward again.
   Nothing is lost and nothing else degrades — existing revisions keep their
   bundle, JSON and hashes, and download still serves `BundleContent`. Called
   out in the migration's own comment and in the PR for the reviewer to accept
   or to convert into a two-step deprecate-then-drop.

4. **A byte difference against the reference sample that is not layout.**
   `Utf8JsonWriter` escapes non-ASCII (the sample's `’` becomes `’`), and
   `CaseEvaMapping` strips the sample's trailing whitespace in
   `Inspection Address`. Both are pre-existing and JSON-semantically identical
   — `json.loads` of both files gives equal strings for every key — so neither
   is in ENG-014's scope. Noted for [[ENG-015]] if byte-parity is ever wanted.

---

## Addition — non-ASCII escaping (coordinator, 2026-08-24)

Folded into the same `JsonWriterOptions`, so `WriteOrderedJson` now makes three
explicit parity choices rather than two.

The predecessor extractor dumps with `json.dumps(ordered, ensure_ascii=False,
indent=2)`. `ensure_ascii=False` means non-ASCII is written as literal UTF-8;
both retained samples carry raw non-ASCII bytes (a curly apostrophe and an
en-dash in `Accident Circumstances`) and zero `\u` escapes. `Utf8JsonWriter`'s
default `JavaScriptEncoder` escapes all non-ASCII plus `& < > + '`. So the
writer diverged, invisibly, because `ap.QDOS26015`'s thirteen values are pure
ASCII.

`Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, with the justification
recorded in the code comment and in the PR: the output is a file written to
disk and dragged into a desktop application, never interpolated into HTML or
JavaScript, so there is no markup context for the default encoder to protect.

**Guard added:** `NonAsciiTravelsAsLiteralUtf8RatherThanEscapes` puts an
accented name, a curly apostrophe, an en-dash and `& < > + '` through a real
bundle and asserts the emitted bytes contain the literal UTF-8 and no `\u`
sequence at all, and that it still parses back to the same string.

### This upgraded the parity evidence

Re-running the throwaway probe against both retained samples after the encoder
change:

| Sample | Result |
| --- | --- |
| `Final Format Example 02.json` | **Byte-for-byte identical** — 656 bytes, all 15 lines equal |
| `AX_SP58WVO.json` | Identical but for one line — `CaseEvaMapping` strips that sample's trailing whitespace in `Inspection Address` |

That retires finding 4 above: the escaping half is now fixed, and only the
mapping-normalisation half remains, which is [[ENG-015]]'s territory rather
than a packaging question.

### Also in the same commit

`IntakePersistenceIntegrationTests.CommittedMigrationCreatesTheSqlServerSchema`
asserts the committed migration list exactly; the new migration had to be added
to it. CI caught this — it was the only real failure in the first run.
