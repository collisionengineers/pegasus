# Post-implementation report

Branch `task/eng-016-collapse-handoff-into-export`, based on
`task/eng-015-eva-field-values`. Four commits. **Not merged** — the PR is open
against its base and is fourth in a stack of four.

## What changed

Export is the only act that produces the EVA package, and its first success on
a case records the once-per-case `First sent to Engineer` proxy. Everything the
gated hand-off was — a generate handler, a download page, an EVA panel, two
Automation MCP tools, four Core ports, a policy-authority capability, a command
policy and three database tables — is deleted. `EvaFirstHandoffProxies`,
`IEvaHandoffProxy`, `EvaBundleSchema`, `EvaHandoffPolicy.SelectEligibleImages`
and `BuildEvidence` survive.

Export became a `POST` with a named handler (`?handler=Bundle`, because the
unnamed POST on that page is already the selective document export). The GET
handler is gone entirely.

## What was deleted, and what proved it had no other consumer

| Deleted | How I proved nothing else needs it |
| --- | --- |
| `CaseEvaMapping.MapForProduction` + `ValidateAcceptedEvidence` | grep across `src/` and `tests/`: one caller, `EvaHandoffStore.MapAcceptedCase`, itself reached only from `GetPreparationAsync` and `GenerateAsync`. Both deleted. The only other hit was prose in a comment. |
| `EvaHandoffPolicyAuthority`, `IEvaHandoffPersistence`, `EvaHandoffCommandPolicy` | The authority is passed only to `IEvaHandoffPersistence`'s two methods and constructed only by `GenerateEvaHandoff`/`DownloadEvaHandoff`. All deleted. The export path already called Core policy statically. |
| `EvaHandoffPolicy.Evaluate` / `RenderedVersionConflict` / `DecideRevision` | Only the hand-off called them; the export never did. |
| `EvaEvidenceStatus.Corrected` | Every reader found: `IsAccepted` treated it as `Accepted`; four write sites in `EvaHandoffStore`; copied into `EvaFieldProvenance.Status`, which reaches no shipped file since ENG-014. Nothing could observe the difference. |
| `EvaBundleSchema.SchemaVersion` | Two callers, both deleted by this ticket. No doc, script or migration cites the literal. |
| Three tables | Full grep recorded in `research`. The only survivor needing rewiring was the proxy's required FK. |

## The consequential loss, stated plainly

`MapForProduction`'s fail-closed guard is **deleted, not merged**. One act means
one bar, and the operator chose the permissive one for Export ([[CASE-019]],
2026-08-22: *"A blank field does not block the download."*). A case with gaps is
now exportable **and recordable as sent to an engineer**.
`QdosBoundaryContractTests.AnIncompleteCaseWithAnUnacceptedAddressStillExports`
pins that so it stays visible rather than becoming folklore.

## Two facts in the brief that turned out to be wrong

Both checked with `git merge-base --is-ancestor`, both recorded rather than
assumed:

- **PLAT-042 (#531) is not in this branch's history.** So the unamended
  `docs/runbook.md:1140` applies, and the migration ships the recovery strategy
  that rule demands.
- **DOCS-013 (#526) is not in this branch's history either** — it is a sibling
  off the same base, not an ancestor. Its FRD-07 rewrite is not present here, so
  my FRD-07 and `capabilities.md` edits will conflict with it on merge.

## Verification

| Check | Result |
| --- | --- |
| `dotnet build --configuration Release` | Succeeded, 0 warnings (`TreatWarningsAsErrors` is on) |
| `Pegasus.Core.Tests` | **952 passed, 0 failed** |
| `Pegasus.ArchitectureTests` | **99 passed, 0 failed** |
| `Pegasus.IntegrationTests` | see the PR — final full-suite figure recorded there |
| Migration up → down → up | **Clean**, run against a scratch LocalDB. After up: the three tables are gone, `EvaFirstHandoffProxies` has lost `RevisionId` and `OperationKey`, and both `CK_EvaFirstHandoffProxies_*` constraints are present and correct. After down: all three tables are back. After up again: gone again, no pending migrations. |
| `Test-MigrationGrants.ps1` | Passes — 68 files checked. No edit needed; it only inspects `CreateTable(` inside `Up()`. |
| No historic `Designer.cs` edited | `git status` showed only the new migration pair and the snapshot |
| Byte audit | No CR bytes introduced; every edited file is LF in the working tree |

### Ticket verification list

- [x] One route produces the package
- [~] The hand-off routes 404 — **partly**. The deleted `Eva/Download` page
      answers **405**, not 404, because this app re-executes a 404 at
      `/status/{code}` and that page has only an `OnGet`.
      `Vehicle?handler=GenerateEvaHandoff` answers **200 with an empty body**,
      because Razor Pages runs no handler for an unrecognised handler name
      rather than refusing the request. Neither performs anything. My first
      assertions guessed 404 for both and were wrong; the tests now assert what
      actually happens.
- [x] Export is a POST with antiforgery, and a refresh does not double-record
- [x] First export records exactly one proxy row; the second records none
- [x] The dashboard "sent to engineer" count still works, fed by export
- [x] The proxy still cannot claim delivery or Engineer assignment
- [x] Package bytes unchanged from ENG-014/ENG-015
- [x] Dropped tables leave no orphaned FK, grant, or migration-guard failure

## Answer to F2

**`EvaEvidenceStatus.Corrected` is removed.** It had no observable consumer left:
`IsAccepted` treated it identically to `Accepted`, and with provenance unshipped
since ENG-014 nothing could tell the two apart. A status no consumer can observe
is a second name for `Accepted`, and the repo's "one list per concept" rail says
a state vocabulary lives in one place. The fact it recorded — that a staff
correction produced the value — is still authoritative upstream in
`CaseDataSourceKind.StaffCorrection`.

## Deliberately not done

- **No new ADR.** ADR-0021 clause 10 names the two MCP tools this ticket
  deletes; ADR bodies are immutable and the repair is a superseding ADR, which
  this ticket was not asked to write. Raised in the PR.
- **No rename** of `EvaHandoffStore` / `EvaHandoffEntities.cs` /
  `EvaHandoffModelConfiguration.cs`. Fourth branch in a four-deep stack; the
  rename would widen the conflict surface for no behavioural gain.
- **`ActivationGateReason` keeps its wording.** Operator-facing message text is
  a closed, operator-approved list.
- **`docs/operations.md` untouched.** It records the deployed estate; this
  ticket deploys nothing.
- **The Review gate on Export stays UI-only.** Pre-existing; `Details.cshtml`'s
  own comment claims it is a Core precondition and it is not. It matters more
  now that Export records a business event, but closing it means re-imposing
  part of the bar this ticket deletes.
- **F4 and F5** from ENG-014's review, and the orphan Core members the
  simplification pass found. All named in the plan and the PR.

---

## Corrections after the first CI run

Two things in the section above were written from expectation rather than
observation, and CI caught the second. Both are corrected here and in the code.

**The route behaviour, finally established by running it.** My earlier note said
the export route "answers 405 to a GET". It does not:

| Route | What actually happens |
| --- | --- |
| `POST /Cases/{id}/Eva/Download` | **405** — the page is deleted, the 404 is re-executed at `/status/{code}`, and that page has only an `OnGet`. |
| `POST /Cases/{id}/Vehicle?handler=GenerateEvaHandoff` | **200, no `Location` header** — Razor Pages runs no handler for an unrecognised handler name rather than refusing the request, so a stale form does nothing. |
| `GET /Cases/{id}/Documents/Export` | **302 to the case.** With no GET handler it returned a blank 200, which reads as a broken route — and a bookmark or history entry can still point there, because this route answered a GET with the archive until this ticket. An `OnGet` that redirects was added. It cannot export; producing the package is the POST. |

**I asserted an unverified guess twice.** The first (404 for both hand-off
routes) was caught by a local run; the second (an empty response body) was
caught by CI, because I re-ran only the three suites I had just edited and not
the one I had edited before them. The assertions now state observed behaviour,
and the body assertion — which was environment-dependent, empty locally and a
full HTML page in CI — is gone.

**Five integration failures the local suite found, three suites I had missed.**
`AutomationMcpIngressTests`, `AutomationAssessmentIngressTests` and
`Browser/OperatorJourneyTests` all reference the hand-off **by name string**
rather than by C# type, so they compiled cleanly and failed only when run. My
inventory grepped for identifiers. That is the lesson worth carrying: a deletion
of a named surface (an MCP tool, a route, a DOM id) needs a string grep as well
as a symbol grep. Fixing them also turned up a fact in prose: `35 typed tools`
in `current-architecture.md` had to become `33`.

### Verification, as it finally stands

| Check | Result |
| --- | --- |
| `dotnet build --configuration Release` | Succeeded, 0 warnings |
| `Pegasus.Core.Tests` | 952 passed, 0 failed |
| `Pegasus.ArchitectureTests` | 99 passed, 0 failed |
| `Pegasus.IntegrationTests`, full local suite | 940 passed / 5 failed / 14 skipped, then all five fixed |
| Re-run of every suite I changed after fixing | `AutomationMcpIngressTests` + `AutomationAssessmentIngressTests` + `OperatorJourneyTests` 15/15; `CaseDetailsWebTests` + `OperatorJourneyTests` 26/26 |
| Full suite re-run end to end after the fixes | **Not done** — CI is authoritative and covers it in three sharded jobs |
| Migration up → down → up | Clean, verified against a scratch LocalDB |
| CI, first push | `unit`, `browser`, `infrastructure`, `documentation`, `changes`, `reference-data`, `local-development-scripts`, `sql-integration (2)`, `(3)` and coverage all passed; `sql-integration (1)` failed on the body assertion above |
