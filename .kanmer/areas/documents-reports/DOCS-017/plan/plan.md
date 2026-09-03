# Plan — DOCS-017 (2026-09-02, gpt-5.6-terra high; corrected 2026-09-03 after plan review)

Estimated diff: six production files (two Core, three Infrastructure, one
Scriban template), six test files, one Test UI snapshot, and the existing
FRD-11 signatory paragraphs. No migration, routed Razor page, label, Web UI,
or assessment-vocabulary change.

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

**Accepted risk (plan review finding 2).** Between the DOCS-017 merge and the
CASE-040 + PLAT-068 merges, no report draft can be generated on `dev` — a
deliberate regression of a working capability, not a disabled flag. It is
accepted because DOCS-017 `blocks` both dependencies (it must merge first),
EPIC-012 ships one production release after every PR, and the alternative —
retaining the D18 fixed tuple as a fallback — is forbidden by conduct rule 6.
No released environment sees the interim state.

## Contract for PLAT-068 and CASE-040

DOCS-017 defines one smallest seam: add an optional `ReportSignatory` field to
`AssessmentReportProjectionInput`; do not add a second Core port or DI
registration.

`ReportSignatory` replaces `ReportEngineer` in
`AssessmentReportRendering.cs` and has this report-snapshot shape:

```csharp
ReportSignatory(
    string PrintedName,
    string? Qualifications,
    byte[] SignatureContent,
    string SignatureContentType)
```

`PrintedName` is D31's **printed signatory name** held on the account, not an
account display name or username derived from it.

Core owns validation at the immutable report-snapshot boundary:

- Printed name and signature content must be present; the signature content
  type must be one of the report-image media types already accepted by
  `ReportImageEvidence.Validate` (`image/jpeg`, `image/png`, `image/webp`) —
  the same list, not a second copy — so the renderer can construct its data
  URI.
- Qualifications are optional; blank qualifications are retained as absent and
  render no separator or blank line.
- Core contains no signatory dictionary, signature key, account-name list, or
  eligibility policy.
- The projection copies the supplied tuple into the versioned snapshot before
  rendering, so the rendered draft remains deterministic for its snapshot and
  payload version.

`AssessmentReportProjection.Prepare` must add the named `Sign-off Engineer`
readiness item when the input has no complete tuple. `Project` must not read
`assessment.engineer.name`, `assessment.engineer.qualifications`, or
`assessment.engineer.signature`.

`Prepare` is also called directly by the routed Assessment page at
`src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs:459`, whose comment
requires the control's condition to agree with what generating would decide.
The new signatory parameter is therefore added to `Prepare` as an optional
trailing `ReportSignatory?`, so that call site keeps compiling and keeps
agreeing (it, like the projection source, supplies nothing and so reports the
same outstanding requirement). Making that page pass the Case's signatory is
[[CASE-040]]'s work, listed below; DOCS-017 does not edit that page.

[[CASE-040]] must persist and expose the Case's `SignOffEngineerId`, enforce
selection only from eligible signatory accounts, apply D31's default — the
assigned Engineer when that account is flagged, otherwise the one account
carrying the Administrator-set "Default sign-off Engineer" designation — and
pass the resulting signatory to `Prepare` from
`Pages/Cases/Assessment/Index.cshtml.cs`. [[PLAT-068]] must provide the
selected account profile by that ID: eligibility as **enabled + Engineer role
+ Sign-off Engineer flag + a signature image on file** (qualifications
optional), the printed signatory name, optional qualifications, signature
bytes, and signature media type. Their integration must make the existing
production projection source populate
`AssessmentReportProjectionInput.Signatory`; until then it deliberately passes
no tuple and Core reports the readiness item. Neither ticket should recreate
renderer policy, a Core allowlist, or an embedded resource.

Cross-lane note (not DOCS-017's to fix): FRD-01 § Sign-off Engineer says the
default is "the assigned Engineer when that account is flagged, otherwise
A Patterson", which names the person rather than D31's Administrator-set
designation. FRD-01 is [[CASE-040]]'s governing document; that lane reconciles
the sentence with D31.

Removing the three `AssessmentVocabulary.Engineer*` definitions from
`src/Pegasus.Core/Assessment/AssessmentContracts.cs` is explicitly outside
DOCS-017. Create or assign the follow-up **assessment signatory vocabulary
retirement** after consumers of those assessment fields are inventoried; this
ticket only stops report projection from reading them.

### Proof allocation for the ticket's two verification bullets

- "A report for a Case whose sign-off is Ed renders Ed's tuple; missing
  qualifications print the name alone" — DOCS-017 proves this at the snapshot
  and renderer level with a supplied tuple. The Case-sourced half is proven by
  the [[CASE-040]] + [[PLAT-068]] integration.
- "An unflagged Engineer cannot be chosen as sign-off" — entirely
  [[CASE-040]] / [[PLAT-068]] behaviour; DOCS-017 owns no selection code and
  must not claim it. Its `proof.md` states this allocation rather than
  asserting the criterion.

### Co-ownership with ENG-035 (plan review finding 3)

ENG-035 (same wave) owns `src/Pegasus.Core/Assessment/AssessmentContracts.cs`,
`src/Pegasus.Core/Reports/AssessmentReportProjection.cs`,
`src/Pegasus.Infrastructure/Reports/**` (report template) and the vocabulary
and projection tests. Three of those are also touched here, so the carve-up is
by symbol, not by file:

| Shared file | DOCS-017 edits | ENG-035 edits |
| --- | --- | --- |
| `AssessmentReportProjection.cs` | `AssessmentReportProjectionInput.Signatory`, the `Prepare` signatory readiness item, the `Project` signatory block | the assessment-vocabulary reads and their projected fields |
| `PlaywrightAssessmentReportRenderer.cs` / `assessment_report.scriban` | the `sig-block` signature, name and qualifications only | the report template's assessment sections |
| `AssessmentReportProjectionTests.cs` | signatory cases | vocabulary cases |
| `AssessmentReportContract.TemplateVersion` | one bump to `rendererref1-v2` (below) | reuses that bump; does not bump again |

`AssessmentContracts.cs` stays untouched here. Merge order: DOCS-017 merges
first (it `blocks` the dependent lanes); ENG-035 refreshes with
`git merge --no-edit origin/dev` and rebuilds before touching these files.
The implementer refreshes from `origin/dev` immediately before editing each
shared file.

## Design rules

The change stays inside the owned paths and the supporting files named in
`files/files.md`. It adds no package, migration, account model, Case field,
Web control, label, explanatory copy, disabled control, compatibility path, or
hard-coded signatory list.

`Pegasus.Core` is the sole owner of report-tuple completeness. Eligibility and
the D31 default remain in the Case/account owners. Reuse
`AssessmentReportProjectionInput`, `IAssessmentReportProjectionSource`,
`AssessmentReportSnapshot.Validate()`, `ReportImageEvidence`'s accepted
media-type list, and existing report test fakes. No new abstraction is
justified; the one extraction is the shared byte-to-data-URI helper in step 3.

No routed Razor page file changes, but the Assessment page's rendered
readiness sentence does change, so the Test UI snapshot commands **do** apply
(step 6). No migration exists, so `Test-MigrationGrants.ps1` does not apply.
Exact state labels remain untouched; excluded capability remains absent rather
than disabled.

## Implementation steps

1. Replace the D18 signatory contract in
   `src/Pegasus.Core/Reports/AssessmentReportRendering.cs` and
   `src/Pegasus.Core/Reports/AssessmentReportProjection.cs`.

   Reuse `AssessmentReportSnapshot.Validate()`, the existing projection input,
   its named readiness-item mechanism, and `ReportImageEvidence`'s accepted
   media-type list. Replace `ReportEngineer` with `ReportSignatory`; remove
   `AcceptedEngineers`, `TryResolveAcceptedEngineer`, `SignatureKey`, and all
   key/tuple matching. Add `ReportSignatory? Signatory` to
   `AssessmentReportProjectionInput` and an optional trailing
   `ReportSignatory?` parameter to `Prepare`; have `Prepare` name
   `Sign-off Engineer` when it is absent or incomplete, and have `Project`
   copy only this supplied tuple into the snapshot.

   Bump `AssessmentReportContract.TemplateVersion` to `rendererref1-v2`: the
   snapshot contract and the rendered signature block both change materially,
   and the version is validated at line 233 and emitted on every
   `RenderedReportArtifact`, so two different contracts must not share it.

   Keep the requirement string as the literal `Sign-off Engineer` on the
   readiness item. `Pages/Cases/Assessment/Index.cshtml:98` already renders
   every Core `Requirement` verbatim ("Current estimate required", "Accepted
   engineer signature"); routing one item through `OperatorLabels` would be a
   second list for the same concept and would touch a shared-lock path for no
   operator-visible gain.

   The existing `engineer.*` assessment fields must no longer be read by the
   projection. Do not modify `AssessmentContracts.cs`; record the named
   follow-up above.

   Acceptance: an Ed tuple with a signature image reaches a valid snapshot;
   a Neil tuple with no qualifications reaches a valid snapshot; printed-name,
   image or unsupported-media-type absence produces the named readiness or
   rejection outcome; `rendererref1-v1` is rejected as unsupported; no Core
   dictionary, key, or assessment-field signatory read remains.

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
   extract the inline byte-to-data-URI expression currently written once in
   `Photos` (line 248) into one private `ImageDataUri(byte[], string)` helper
   and use it for both the photos and the signatory bytes. `ResourceDataUri`
   (line 297) is an embedded-resource loader and is not reusable here — this
   is a small extraction, not an existing helper. Populate the existing
   Scriban context from `snapshot.Signatory`; do not resolve a resource from a
   key.

   In
   `docs/design/assets/report-renderer/templates/assessment_report.scriban`,
   conditionally emit ` — qualifications` only when qualifications are present.
   In `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj`, remove only
   the Andy signature embedded-resource entry. Do not move or rename governed
   brand assets.

   Acceptance: generated PDF text contains `Ed Mawdsley — ATA VDA AQP`; Neil
   renders `Neil O'Reilly` without a dangling separator; signature content is
   a supplied data URI rather than an embedded resource lookup.

   Fixture provenance: `Ed Mawdsley` / `ATA VDA AQP` and `Neil O'Reilly` with
   empty qualifications are the mockup's own values
   (`Pegasus_UI_v2_src/src/04-fixtures.js:33-35`), permitted as fixture data
   by D43 (`docs/engineering.md` § Case Workspace v2 fixture values). They are
   not invented; the retired `reference/rendererref1/DESIGN_SPEC.md` "to be
   confirmed" note predates D31 and D43.

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
   Add boundary tests for missing printed name, missing signature content, an
   unsupported signature media type, the rejected `rendererref1-v1` payload
   version, the projection's `Sign-off Engineer` readiness result, and the
   production source's interim not-ready result. Update all input
   constructors, including the target-typed browser fixture, rather than
   retaining a compatibility overload.

   `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs`
   proves the **interim** contract: the production source builds an input with
   no signatory and the projection returns `Sign-off Engineer`. It does not
   prove persisted-signatory consumption — that arrives with [[CASE-040]] and
   [[PLAT-068]] (the earlier `files/files.md` row for that file is superseded
   by this step).

   Acceptance: tests prove the exact Core contract, Ed rendering, Neil
   name-only rendering, the fail-closed interim production caller, and the
   absence of D18 assumptions.

5. Reconcile FRD-11's D18-era signatory sentences.

   Edit
   `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`, after
   refreshing from `origin/dev` to preserve DELIV-041:

   - Lines approximately 70–95: remove the exact-matching-tuple,
     signature-key, `andy_patterson` and hard-coded supplied-tuple language,
     and the "signature resource" wording that assumes an embedded asset.
     State that the report snapshot receives the Case sign-off account tuple;
     printed name and signature image are required, qualifications are
     optional, and missing or unsupported required content fails closed.
     Reference FRD-01 and FRD-04 for selection and account data rather than
     duplicating the staff list.
   - Line approximately 138 (§ Readiness): "the issuing Engineer's identity"
     is D18-era and contradicts D31 — replace it with the Case's sign-off
     tuple. This is the same signatory rule and stays inside the ticket's
     owned FRD-11 scope.

   Leave lines approximately 338–340 unchanged unless a narrow wording fix is
   necessary: they already correctly identify signatures as provenance-
   sensitive renderer assets and the Case Sign-off Engineer as signatory.
   Preserve the deterministic, versioned, retained, review-gated, and
   distinct-event paragraphs.

   `docs/design/README.md` line ~620 ("Andy Patterson's approved exact tuple
   is embedded by Infrastructure") also goes stale when step 3 removes the
   embedded resource, but the design authority is outside DOCS-017's owned
   doc path. Record it as a named follow-up for the EPIC-012 docs lane rather
   than editing it here.

   Acceptance: FRD-11 contains no D18-era exact tuple/key/embedded-resource
   policy and no hard-coded signatory data, while FRD-04 remains the single
   owner of account setting data.

6. Refresh the one affected Test UI snapshot.

   Adding the `Sign-off Engineer` readiness item changes the Assessment page's
   rendered readiness sentence, which
   `docs/design/test-ui/pages/case-assessment--default.html:183,188` records
   verbatim and CI verifies. Run
   `./scripts/Update-TestUiSnapshots.ps1`, then
   `./scripts/Update-TestUiSnapshots.ps1 -Verify` and
   `./scripts/Test-UiCatalogue.ps1`, and commit only the snapshot files the
   readiness change actually alters. `docs/design/test-ui/**` is a shared-lock
   path with capacity one: claim it for this change, keep the diff to the
   affected snapshot, and refresh from `origin/dev` immediately before
   regenerating.

   Acceptance: the verify script and the catalogue check both pass, and the
   snapshot diff contains only the readiness sentence.

## Verification

Run the following from the task worktree, recording exit codes:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
./scripts/Update-TestUiSnapshots.ps1 -Verify
./scripts/Test-UiCatalogue.ps1
```

The canonical `Category!=Corpus` filter is used, not the narrower
`Category!=Corpus&Category!=Browser`: `AssessmentReportRendererTests` carries
`[Trait("Category", "Browser")]`, so step 3's PDF-text acceptance and the
browser readiness fixture are only proven when Browser tests run. A run that
excludes them is INCONCLUSIVE for those claims, not a PASS.

No migration is planned, so do not run `./scripts/Test-MigrationGrants.ps1`.

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

## Plan review (2026-09-03, gpt-5.6-sol xhigh; dispositions Claude Opus)

Verdict read: REQUEST CHANGES (nine findings). Each was verified independently
in the read-only `.worktrees/research` checkout at `897db953`; the checkout was
clean afterwards.

| # | Severity | Step | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | Contract, 1, 4 | Contract says "display name" and "assigned-Engineer-or-A-Patterson default", leaves "eligible" undefined, and does not allocate the "unflagged Engineer" acceptance criterion. | **Fixed.** The contract now states D31's printed signatory name, the Administrator-set "Default sign-off Engineer" designation, and eligibility as enabled + Engineer + flag + signature on file. A new "Proof allocation" subsection assigns each ticket verification bullet to the lane that can prove it. FRD-01's "otherwise A Patterson" wording is recorded as a CASE-040 cross-lane note; DOCS-017 does not own FRD-01. |
| 2 | blocker | 1, 2, 4 | The successful path is not wired: the sole production source passes no tuple, so no report can be generated; this contradicts "Done means wired", and `files/files.md` promises persisted-signatory consumption that step 2 refuses. | **Partly fixed; risk accepted and recorded.** The production caller does reach the new Core code and fails closed by design; the alternative (keeping the D18 tuple as a fallback) is forbidden by conduct rule 6, and DOCS-017 `blocks` both dependencies so it must merge first. The interim regression is now stated explicitly as an accepted risk, and step 4 supersedes the `files/files.md` persistence row. **Rejected** the suggested "add an integration phase after CASE-040/PLAT-068 land": that absorbs another ticket's scope (conduct rule 2). |
| 3 | blocker | 1, 3, 4 | Not disjoint from ENG-035: `AssessmentReportProjection.cs`, `Infrastructure/Reports/**` and the projection tests are shared files; the plan also reaches outside the three stated roots. | **Fixed.** A new "Co-ownership with ENG-035" subsection carves the shared files up by symbol, fixes merge order (DOCS-017 first) and requires an `origin/dev` refresh before editing each shared file. The supporting files outside the three approximate roots were already enumerated and justified in `files/files.md`. |
| 4 | blocker | 3, 4 | `Ed Mawdsley — ATA VDA AQP` is fabricated domain data; `reference/rendererref1/DESIGN_SPEC.md:145` says those qualifications are unconfirmed and FRD-04 writes `E Mawdsley`. | **Rejected**, with the citation added. The values are the mockup's own fixtures (`04-fixtures.js:33-35`), which D43 (`docs/engineering.md` § Case Workspace v2 fixture values, operator sign-off 2026-09-03) explicitly permits in tests and snapshots. `DESIGN_SPEC.md` is retired reference material predating D31 and D43. Step 3 now records that provenance so the values do not read as invention. |
| 5 | blocker | 1, Verification | `Prepare` changes the routed Assessment page's rendered readiness output (`Index.cshtml.cs:459`; snapshot `case-assessment--default.html:183`) yet snapshot work is excluded; and the test filter excludes `Category=Browser` while steps 3 and 4 depend on Browser-tagged tests. | **Fixed, both halves.** New step 6 regenerates and verifies the Test UI snapshot under shared-lock coordination, and the contract section records how the page's `Prepare` call site stays consistent (optional trailing parameter now; CASE-040 supplies the tuple later). Verification now uses the canonical `Category!=Corpus` filter and states that excluding Browser is INCONCLUSIVE for the PDF claims. |
| 6 | should-fix | 5 | FRD-11 outside lines 75–95 still says the readiness input is "the issuing Engineer's identity"; `docs/design/README.md:620` still says Andy's tuple is embedded. | **Fixed for FRD-11** — step 5 now names line ~138 as part of the same signatory rule. **Deferred for the design README**: it is outside DOCS-017's owned doc path; recorded in step 5 as a named follow-up for the EPIC-012 docs lane. |
| 7 | should-fix | 1, 3 | The payload version is retained although both the snapshot contract and the rendered template change. | **Fixed.** Step 1 bumps `AssessmentReportContract.TemplateVersion` to `rendererref1-v2`, with tests that the new version is emitted and that `rendererref1-v1` still fails closed; the ENG-035 carve-up table records that ENG-035 reuses this bump rather than adding a second. |
| 8 | should-fix | 1 | The literal `Sign-off Engineer` is operator-facing (`Index.cshtml:98` renders `Requirement` verbatim), so it is a label added in Core. | **Rejected**, with the reason recorded in step 1. Every existing Core readiness requirement is already rendered verbatim ("Current estimate required", "Accepted engineer signature"); routing one item through `OperatorLabels` would create a second list for the same concept and touch a shared-lock path for no operator-visible gain. The existing convention wins. |
| 9 | should-fix | 3 | The claimed reusable byte-to-data-URI helper does not exist (photos convert inline at line 248; `ResourceDataUri` is a resource loader), and validating only that a media type is present admits non-image content. | **Fixed.** Step 3 names this honestly as one small extraction, `ImageDataUri(byte[], string)`, shared by photos and the signature; the contract now validates the signature media type against `ReportImageEvidence`'s existing accepted list, with a rejection test in step 4. |

Confirmed clean and not raised: no step assumes a staff review flag, checkbox
or dialog (D44) or a damage type (D45); D46 is untouched; no new package,
migration, or speculative abstraction is introduced; `Pegasus.Core` remains the
sole owner of report-tuple policy.
