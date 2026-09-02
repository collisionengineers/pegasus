# Files — UIIMP-012

*The files document. Not the research — this is the **surface area** of the change, not the findings behind it.*

Surveyed against `origin/dev` @ `9b8f78a36151313bc6d48625edee7f13a2173127` (read-only,
no worktree yet).

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Gains the one Triage-record panel label (`"Notes"`). The EPIC-011 rule is "one list per concept: labels live in `Presentation/OperatorLabels.cs`", and the shipped heading is a bare markup literal, so the string moves here rather than being edited in place in the page. Breaks nothing: additive nested class, no member reordered. |
| `src/Pegasus.Web/Pages/Triage/Details.cshtml` | Line 400 renders `<h2 id="triage-history-title">Permanent history</h2>`; it becomes the label reference. Lines 392–398 carry a Razor comment recording the divergence "UIIMP-012's to settle", which this change settles and so removes. Whole-file ownership for wave A, before INTK-054. Could break: the `aria-labelledby="triage-history-title"` pairing if the id is touched — it is not. |
| `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs` | Line 477 pins `Assert.Contains("Permanent history", finalHtml, …)` in `AcceptedTriageMatchEvidence…`'s final render check. The assertion is retargeted in the same diff, not deleted or weakened. Class carries `[Trait("Category","SqlServer")]`, so it runs in the non-Browser integration lane. |
| `docs/design/test-ui/pages/triage-details--default.html` | Committed generated artifact for the changed routed page. CI's Test UI lane re-captures and compares, so it is regenerated and committed with the page change. **Expected to come back byte-identical** — see Ripple effects. |

## Context files

| Path | What it tells the implementer |
|---|---|
| EPIC-011 `context.md` §1.5 and D25 (Kanmer group doc) | §1.5 names the panel **Notes** and says in terms "the shipped 'Permanent history' heading is renamed by UIIMP-012"; entry shape stays Date/Time/ID + text; D25 adds that INTK-054 merges append-only staff notes into the same panel afterwards. |
| EPIC-011 `context.md` D7 (line 110) | Already carries "**Narrowed 2026-09-01 (UIIMP-012):** the clause governs uncomposed integrations only; a control disabled by a state or permission gate with a real handler and a named condition (the merged `.gated` convention …) is legal." The Phase 0 controller made this edit; **no `context.md` work remains for this ticket.** |
| `docs/design/README.md` §Absent versus disabled (dev, lines 689–708) and rule 6 (line 1429) | Already distinguishes the two cases: an uncomposed capability is absent, "a control whose record does not yet satisfy a condition is present, disabled, and states the condition", and "the only disabled uncomposed capability is a named, ticketed integration seam (D7)". DELIV-040's open PR #643 rewrites this same section (+73/−26). Do not edit it here. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` nested classes (`Nav`, `Admin`, `AiJobs`, `EvaHandoffs`, `WorkflowConfiguration`, `MailSettings`, `StaffAccounts`, `AutomationAdmin`, `QueryResponseJobs`, `CaseWorkspace`, `Upload`) | The precedent: one nested `public static class <Surface>` per surface, `public const string` members, a `<summary>` naming the EPIC-011 section it serves, appended at the end without reordering. `CaseWorkspace` (line 1297) is the closest model. |
| `src/Pegasus.Web/Pages/Unidentified/Details.cshtml:96`, `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml:154` | The sibling panels both render a literal `<h2 …>History</h2>`. They are other lanes' files and keep their own headings; this ticket does not harmonise them. |
| `scripts/Update-TestUiSnapshots.ps1` | Capture lane = `dotnet test` on `Pegasus.IntegrationTests` filtered to `WebTests|Category=Browser|StaffSignInSecurityTests|TestUiFocusedRenderTests|QdosCustodialWebTests|AutomationConnectorAuthorizationTests|ImageViewingWebTests`, then `TestUiSnapshotTests` in `update` or `verify` mode. `-SkipCapture` reuses `artifacts/test-ui-capture`. |
| `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs:115–165` | Selection: candidates whose path matches the catalogue route pattern, then the ordinal-first normalized HTML. `QdosTriageIntegrationTests` is `Category=SqlServer` and therefore **outside** the capture filter, which is why the committed `/Triage/{id:guid}` artifact is the styled not-found page. |
| `AGENTS.md` §Commands (lines 153–174) and `docs/runbook.md` §Locked restore, build, and test | The canonical commands, the complement pair of integration filters, and the rule that `docs/design/test-ui/` is committed with the page change because CI runs the same verify. |

## Ripple effects

- **The committed Test UI artifact is expected not to change.**
  `docs/design/test-ui/pages/triage-details--default.html` on `dev` is the styled
  not-found page (`<title>We could not find that page · Pegasus</title>`, `<h1>` at
  line 81, 93 lines, no `<h2>`), because no test inside the capture filter renders a
  populated Triage detail. Renaming the heading therefore changes nothing that the
  capture selects. A regenerate-then-verify run that leaves the file untouched is the
  expected result, not a failure; a change to any *other* page's artifact is a stop.
- `docs/design/test-ui/index.html` is regenerated from `catalogue.json`, which is
  untouched, so it too is expected byte-identical.
- No CSS, JS or partial selects `#triage-history-title` (`git grep` finds it only in
  `Pages/Triage/Details.cshtml`); the id stays as it is.
- INTK-054 takes the same panel afterwards under the `triage_page` lock; it adds the
  append-only staff notes and owns any further structural change (D25).

## Out of scope

- `docs/design/README.md` and every other governing document: DELIV-040 (PR #643, open,
  targets `dev`) holds the `governing_docs` lock and already rewrites the same
  §Absent versus disabled text.
- EPIC-011 `context.md`: D7's narrowing and §1.5's Notes wording are already recorded.
- `tests/Pegasus.Core.Tests/ProviderApi/ProviderSubmissionTests.cs:432` — a code comment
  using "permanent history" as the business concept, not the heading. Correct as it
  stands; not touched.
- The `History` headings on the Unidentified and image-record pages.
- Adding staff notes, an Add note control, or the Files view (INTK-054, D25).
- Renaming `id="triage-history-title"` or the `notes-list` / `note-entry` classes.
- Any fix to why the Triage route's captured artifact is the not-found page: report it,
  do not fix it here.
