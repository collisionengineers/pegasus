# Non-blocking findings inherited from ENG-014's review (2026-08-24)

Not fixed on `task/eng-014-drop-manifest-indent-json` because ENG-015 is stacked
on that branch — a commit there now would invert ENG-015's PR diff. Recorded
here rather than dropped. Each is in this ticket's subsystem, so fold in where
cheap.

**F1 — two comments assert a file that no longer exists.** Both became false when
`provenance.json` stopped being produced:
- `src/Pegasus.Core/Eva/CaseEvaMapping.cs:123-127` — "so provenance.json says
  where the value came from" is the entire stated purpose of `ExportDateSource`.
- `infra/modules/platform.bicep:433` — "it is written into every exported
  provenance.json". Comment-only; no infra change.

**F2 — `EvaEvidenceStatus.Corrected` is now unobservable.** With `provenance.json`
gone, `Corrected` and `Accepted` are indistinguishable to every remaining
consumer (only `IsAccepted` and the `Unrecorded` filter read `Status`), and
`ExportDateSource` is write-only. No test asserts `corrected` any more. Decide
whether the distinction still earns its place — this ticket merges the two
mapping functions, so it is the natural place to answer.

**F3 — `ValidateSource` builds dead output.** `EvaBundleSchema.cs:630-660`
constructs and normalises a 13-entry `EvaFieldProvenance[]` and returns it on the
source record, but `CreateOfflineReplay` no longer reads it. The validation
*throws* are load-bearing; the construction and the returned field are not.

**F4 — a test comment overclaims.** `EvaBundleContractTests.cs:141-146` says "a
field's own line breaks stay escaped inside its value rather than becoming
layout", but no fixture field contains a line break. ENG-015 changes that — the
6-line address and the labelled damage area both carry `\n` — so after ENG-015
the claim may hold; verify rather than assume.

**F5 — the strongest regression guard is missing.** A committed golden-file test
reading `reference/eva_information/Final Format Example 02.json` and
round-tripping its thirteen values through `CreateOfflineReplay` would pin
indent, newline, encoder and key order in one assertion, and keep proving parity
as field values change. `reference/` is explicitly permitted for testing. The
byte-parity proof was run as a throwaway probe and deleted.

**F6 — `docs/current-architecture.md:526` carries rationale, not as-built fact.**
It now argues *why* the companion files went ("neither was an operator
requirement, no importer or verifier ever read either…"). CLAUDE.md defines that
file as the as-built snapshot; the argument belongs in the ticket.

**Also worth carrying forward, from the same review:** the CRLF pin's recorded
reason is wrong. The PR says CI runs Windows and Linux — but `unit`,
`sql-integration` and `browser` are all `windows-latest`, and the two Ubuntu jobs
run no .NET tests. The real reason is stronger: production is Linux Container
Apps while every layout test runs on Windows, so unpinned the app would ship LF
to production with CI green forever. **CI does not guard that pin.** A future
reader deleting it on the belief that it does would break parity silently.
