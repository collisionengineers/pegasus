# Files — UIIMP-005

Refreshed 2026-08-30 after the merge with `dev` at `b9dcfec9`. The earlier
version predated that merge and listed neither the test files it touched nor
the catalogue entry it had to add.

## Tooling and the gate

| Path | Change |
| --- | --- |
| `.github/workflows/ci.yml` | New `test-ui` job — one capture run then a verify against the committed corpus. `timeout-minutes: 45` |
| `scripts/Update-TestUiSnapshots.ps1` | Hardened; `-Verify` is what CI runs |
| `scripts/Get-CiChangeFlags.ps1`, `scripts/Test-CiChangeFlags.ps1` | Change-flag plumbing for the new job |

## Determinism — the reason the first regenerated corpus was stale

| Path | Change |
| --- | --- |
| `tests/Pegasus.IntegrationTests/TestUiResponseCapture.cs` | `LayoutClockRegex` normalises the layout clock to `{{office-clock}}`, scoped to `<span>Current · HH:MM</span>` so the mail freshness banner (`<time datetime=…>`, a real last-sync value) is untouched |
| `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` | Three wall-clock sources replaced by the host `TimeProvider` — the receipt pair, and one `RegisterUnidentified` occurrence time the first sweep missed |

`_Layout.cshtml` renders both freshness clocks from render time, and one capture
host (`AutomationConnectorAuthorizationTests`) deliberately uses
`TimeProvider.System` and cannot be pinned — so every captured page carried the
minute it ran. Without this the new job is red on every run.

## Catalogue

| Path | Change |
| --- | --- |
| `docs/design/test-ui/catalogue.json` | Classified `Administration/Accounts/Confirm.cshtml`, which PLAT-027 added without an entry; kept `dev`'s `EvaSubmission` route over the branch's doubled one |
| `docs/design/test-ui/pages/**`, `index.html` | Corpus regenerated against `dev` |

## Tests reconciled with `dev`

Ten files, all merged rather than taken. No test method was lost — 260 `.cs`
files under `tests/` on both sides, zero added or deleted.

| Path | What the merge had to resolve |
| --- | --- |
| `CaseDetailsWebTests.cs` | Both sides added a property to `RecordingCaseDetailsStore` at the same spot; **both kept**. 17 branch + 21 dev → 22 merged. `dev`'s test pinning the claimant contact-number/address save fix survives |
| `OrganizationAdministrationWebTests.cs` | **Auto-merged but broken** — both sides added an EVA submission fetch to the same method, producing a duplicate local and a compile error. Kept `dev`'s full test, routed its GET through the shared driver |
| `ImageIntakeWebTests.cs`, `ImageViewingWebTests.cs` | Branch's shared `IntakeWebDriver.GetHtmlAsync` on `dev`'s `?section=case-files` URL |
| `TestUiSnapshotTests.cs` | Took `dev`'s two-part `eva-submission` marker over the branch's single one; five markers repointed at copy EPIC-011 replaced; removed a dead entry for a page `dev` deleted |
| `TestUiFocusedRenderTests.cs`, `IntakeWebTestSupport.cs`, `StaffAccountsAndRolesWebTests.cs` | Reconciled |

## Notably NOT changed

**Zero `src/` changes.** `git diff --stat origin/dev...HEAD -- src/` is empty —
this is tooling, snapshots and tests only.
