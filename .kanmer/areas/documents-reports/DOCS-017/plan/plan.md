# Plan — DOCS-017 (2026-09-02, gpt-5.6-terra high)

Estimated diff: six production files (two Core, three Infrastructure, one
Scriban template), six test files, and the existing FRD-11
signatory paragraph. No migration, routed Razor page, label, Web UI, or
assessment-vocabulary change.

## Verified basis and boundary

`897db953` is checked out cleanly. `AssessmentReportProjectionInput` is already
the production seam from `EfAssessmentReportProjectionSource` to
`AssessmentReportProjection`; that source is registered and reached by
`GenerateCaseAssessmentReportDraft` from the Assessment page.

The current Core allowlist, `ReportEngineer.SignatureKey`, assessment
`engineer.*` reads, and Andy embedded resource are D18-era behaviour. D31
requires a Case/account-supplied signatory. The Case field and staff-account
data do not yet exist, so this ticket cannot render a live Case signatory until
[[CASE-040]] and [[PLAT-068]] land.

The selected interim behaviour is fail-closed. On `dev` after DOCS-017 merges
and before both dependencies merge, an Engineer can still use the existing
report-draft route, but it returns the existing not-ready outcome with the
named outstanding requirement `Sign-off Engineer`; no PDF is generated. This
is a real production behaviour through the existing caller, not a stub,
fallback tuple, or embedded-brand policy.

## Contract for PLAT-068 and CASE-040

DOCS-017 defines one smallest seam: add an optional `ReportSignatory` field to
`AssessmentReportProjectionInput`; do not add a second Core port or DI
registration.

`ReportSignatory` replaces `ReportEngineer` in
`AssessmentReportRendering.cs` and has this report-snapshot shape:

```csharp
ReportSignatory(
    string Name,
    string? Qualifications,
    byte[] SignatureContent,
    string SignatureContentType)
```

Core owns validation at the immutable report-snapshot boundary:

- Name and signature content must be present; signature content type must be
  present so the renderer can construct its data URI.
- Qualifications are optional; blank qualifications are retained as absent and
  render no separator or blank line.
- Core contains no signatory dictionary, signature key, account-name list, or
  eligibility policy.
- The projection copies the supplied tuple into the versioned snapshot before
  rendering, so the rendered draft remains deterministic for its snapshot and
  existing payload version.

`AssessmentReportProjection.Prepare` must add the named `Sign-off Engineer`
readiness item when the input has no complete tuple. `Project` must not read
`assessment.engineer.name`, `assessment.engineer.qualifications`, or
`assessment.engineer.signature`.

[[CASE-040]] must persist and expose the Case's `SignOffEngineerId`, enforce
selection only from eligible signatory accounts, and apply D31's assigned-
Engineer-or-A-Patterson default. [[PLAT-068]] must provide the selected account
profile by that ID: enabled Engineer eligibility plus flag, display name,
optional qualifications, signature bytes, and signature media type. Their
integration must make the existing production projection source populate
`AssessmentReportProjectionInput.Signatory`; until then it deliberately passes
no tuple and Core reports the readiness item. Neither ticket should recreate
renderer policy, a Core allowlist, or an embedded resource.

Removing the three `AssessmentVocabulary.Engineer*` definitions from
`src/Pegasus.Core/Assessment/AssessmentContracts.cs` is explicitly outside
DOCS-017. Create or assign the follow-up **assessment signatory vocabulary
retirement** after consumers of those assessment fields are inventoried; this
ticket only stops report projection from reading them.

The ticket verification “An unflagged Engineer cannot be chosen as sign-off” is
[[CASE-040]] and [[PLAT-068]] behaviour. DOCS-017 can prove only that a report
snapshot requires a complete supplied tuple, then renders Ed and qualification-
less Neil correctly.

## Design rules

The change remains in the owned paths only. It adds no package, migration,
account model, Case field, Web control, label, explanatory copy, disabled
control, compatibility path, or hard-coded signatory list.

`Pegasus.Core` is the sole owner of report-tuple completeness. Eligibility and
the D31 default remain in the Case/account owners. Reuse
`AssessmentReportProjectionInput`, `IAssessmentReportProjectionSource`,
`AssessmentReportSnapshot.Validate()`, the renderer's existing photo data-URI
pattern, and existing report test fakes. No new abstraction is justified.

No routed Razor page changes, so Test UI snapshot commands do not apply. No
migration exists, so `Test-MigrationGrants.ps1` does not apply. Exact state
labels remain untouched; excluded capability remains absent rather than
disabled.

## Implementation steps

1. Replace the D18 signatory contract in
   `src/Pegasus.Core/Reports/AssessmentReportRendering.cs` and
   `src/Pegasus.Core/Reports/AssessmentReportProjection.cs`.

   Reuse `AssessmentReportSnapshot.Validate()`, the existing projection input,
   and its named readiness-item mechanism. Replace `ReportEngineer` with
   `ReportSignatory`; remove `AcceptedEngineers`,
   `TryResolveAcceptedEngineer`, `SignatureKey`, and all key/tuple matching.
   Add `ReportSignatory? Signatory` to
   `AssessmentReportProjectionInput`, have `Prepare` name `Sign-off Engineer`
   when it is absent or incomplete, and have `Project` copy only this supplied
   tuple into the snapshot.

   The existing `engineer.*` assessment fields must no longer be read by the
   projection. Do not modify `AssessmentContracts.cs`; record the named
   follow-up above.

   Acceptance: an Ed tuple with a signature image reaches a valid snapshot;
   a Neil tuple with no qualifications reaches a valid snapshot; name or image
   absence produces the named readiness outcome; no Core dictionary, key, or
   assessment-field signatory read remains.

2. Wire the deliberate interim production behaviour in
   `src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs`.

   Reuse the registered `IAssessmentReportProjectionSource` implementation and
   its existing `AssessmentReportProjectionInput` construction. Until the Case
   FK and staff profile exist, pass no signatory tuple. Leave
   `DependencyInjection.cs` unchanged: the existing registration and
   `GenerateCaseAssessmentReportDraft` caller remain the production route.

   Do not add an Andy resource fallback, temporary account lookup, or
   placeholder adapter. The source must cause the standard not-ready result
   with `Sign-off Engineer`, rather than throw or render a fixed identity.

   Acceptance: the existing Assessment draft route remains callable and
   reports the named outstanding signatory requirement rather than producing a
   report with D18-era data.

3. Render supplied signature content and remove the obsolete embedded asset.

   In `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`,
   reuse the existing `ReportImageEvidence` byte-to-data-URI construction for
   the signatory bytes and supplied media type. Populate the existing Scriban
   context from `snapshot.Signatory`; do not resolve a resource from a key.

   In
   `docs/design/assets/report-renderer/templates/assessment_report.scriban`,
   conditionally emit ` — qualifications` only when qualifications are present.
   In `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj`, remove only
   the Andy signature embedded-resource entry. Do not move or rename governed
   brand assets.

   Acceptance: generated PDF text contains `Ed Mawdsley — ATA VDA AQP`; Neil
   renders `Neil O'Reilly` without a dangling separator; signature content is
   a supplied data URI rather than an embedded resource lookup.

4. Update only the owned report tests and existing projection-source fakes.

   Reuse `ReadyInput`, `FakeProjectionSource`, report snapshot builders, and
   the persistence/report-renderer test harnesses in:

   - `tests/Pegasus.Core.Tests/Reports/AssessmentReportRenderingTests.cs`
   - `tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs`
   - `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs`
   - `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs`
   - `tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs`
   - `tests/Pegasus.IntegrationTests/Browser/AssessmentReadinessSummaryBrowserTests.cs`

   Replace obsolete fixed-Andy assertions with supplied Ed and Neil fixtures.
   Add boundary tests for missing name and signature content, the
   projection's `Sign-off Engineer` readiness result, and the production
   source's interim not-ready result. Update all input constructors, including
   the target-typed browser fixture, rather than retaining a compatibility
   overload.

   Acceptance: tests prove the exact Core contract, Ed rendering, Neil
   name-only rendering, the fail-closed interim production caller, and the
   absence of D18 assumptions.

5. Reconcile only FRD-11's D18-era signatory paragraph.

   Edit
   `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` lines
   approximately 75–95, after refreshing from `origin/dev` to preserve
   DELIV-041. Remove the exact-matching tuple, signature-key,
   `andy_patterson`, and hard-coded supplied-tuple language. State that the
   report snapshot receives the Case sign-off account tuple; name and signature
   image are required, qualifications are optional, and missing required
   content fails closed. Reference FRD-01 and FRD-04 for selection and account
   data rather than duplicating the staff list.

   Leave lines approximately 338–340 unchanged unless a narrow wording fix is
   necessary: they already correctly identify signatures as provenance-
   sensitive renderer assets and the Case Sign-off Engineer as signatory.
   Preserve the deterministic, versioned, retained, review-gated, and
   distinct-event paragraphs.

   Acceptance: FRD-11 contains no D18-era exact tuple/key policy and no
   hard-coded signatory data, while FRD-04 remains the single owner of account
   setting data.

## Verification

Run the following from the task worktree, recording exit codes:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
```

No routed Razor page changes are planned, so do not run
`./scripts/Update-TestUiSnapshots.ps1`,
`./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture`, or
`./scripts/Test-UiCatalogue.ps1`. No migration is planned, so do not run
`./scripts/Test-MigrationGrants.ps1`.

## Simplification pass (2026-09-02)

to be recorded by the implementer before the PR opens

## Stop condition

Open the DOCS-017 PR targeting `dev`, attach the implementation report and
verification evidence, and move the ticket to Review. Do not merge it.

## Wrapper check (Claude, 2026-09-02)

- Codex ran read-only in the detached `.worktrees/research` checkout at
  `897db953` (= `origin/dev`); `git status --porcelain` was clean afterwards.
- Every planned file is inside the owned paths refined in `files/files.md`;
  `DependencyInjection.cs`, `AssessmentContracts.cs`, `OperatorLabels.cs`,
  migrations and every CASE-040 / PLAT-068 path stay untouched. The
  `files/files.md` row for `EfAssessmentReportProjectionSource.cs` ("compose
  the tuple once the dependencies land") is narrowed by step 2: this ticket
  passes no tuple and the dependencies populate `Signatory`.
- DELIV-041 has landed (PR #647, `897db953`): FRD-11 lines 75-95 and 338-340
  and FRD-04 77-88 already carry D31, so step 5 is a reconciliation of the
  remaining D18-era sentences only, not the first D31 write.
- Interim behaviour is a deliberate fail-closed readiness item, so on `dev`
  between this merge and the CASE-040 + PLAT-068 merges no report draft can
  be generated; EPIC-012 ships one production release after all PRs, so no
  released environment sees that state. CASE-040 and PLAT-068 planners must
  read "Contract for PLAT-068 and CASE-040" above.
