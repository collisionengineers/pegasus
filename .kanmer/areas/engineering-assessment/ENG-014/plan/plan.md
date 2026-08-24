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
