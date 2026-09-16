# Pegasus deep audit report — repository state, delivery pipeline and Estimate-import findings

**Date:** 16 September 2026
**Auditor:** pi coding session (direct work, main checkout `/home/pguser/projects/pegasus`)
**Audited revisions:** local `dev` `5765a527a7729e606fe5683b3af3d47a17c708ff`; remote `dev` `9c4c09eb7` (local is behind by 1); production deployed source `e8efb19779baadc5ea46bd9c62e6c9c54740cac7` (Release 53, 15 September 2026, per `docs/operations.md:7-22`).
**Method:** static source audit of `src/` at `5765a527a`, cross-checked against the operator-authored sprint handover documents pushed in `9c4c09eb7` (`origin/dev:1609sprint/…`, `.codex/PLAN.md`), the deployment record in `docs/operations.md`, and the FRD owners in `docs/frd/`. The audit itself modified no application code and performed no writes beyond a read-only `git fetch origin`; committing this report to `1609sprint/deep-code-audit/` was a later, explicit operator instruction.

**Evidence labels used throughout:**

| Label | Meaning |
| --- | --- |
| **[S]** | Verified directly in source at `5765a527a` (this audit) |
| **[D]** | Reproduced offline against deployed bytes by the operator's 16 September investigation (`origin/dev:1609sprint/import-test-and-fix/draft-plan.md`) |
| **[P]** | Production observation retained in the performance plan (`origin/dev:.codex/PLAN.md`, §1 findings) |
| **[C]** | CI/workflow observation retained in the PR 764 progress report (`origin/dev:1609sprint/performance-pr-764/PROGRESS.md`) |

---

## 1. Executive summary

1. **The repository's core business invariants are sound.** Case/PO identity allocation, intake-allocation idempotency, image-intake pairing, authorization, and persistence integrity all use defensible, version-checked, unique-constraint-backed designs (§4). No defect was found in these areas during this audit.
2. **The Estimate-import journey is broken end-to-end — UI mechanics, operator feedback, and parser arithmetic — and this is the single most critical functional area in the product today** (§5). It is the operator's own reported failure of 16 September 2026 ("Import estimate button not responsive — doesn't close after choosing file"; "calc.pdf also wouldn't import but it should be set up"), it is fully diagnosed in the pushed sprint handover, and **no branch in the repository contains any part of the fix** (§5.7). Because `dev`'s runtime code is byte-identical to production for this journey (§2.3), every defect below is live in production right now.
3. **The delivery pipeline is separately blocked**: PR #764 (first-use performance) cannot obtain the green full CI the release procedure requires, because the three-shard SQL test allocation hits the 45-minute job ceiling. The operator-approved six-shard remediation exists as a written plan but has not been built, tested, committed or pushed (§3).
4. **One compliance flag** requires an early operator decision: commit `9c4c09eb7` pushed real-looking domain documents (Audatex reports, Glass's calculation sheets) into repository history under `1609sprint/` (§6).

---

## 2. Repository state and synchronization

### 2.1 The reported symptom and its cause

The checkout reported `Your branch is up to date with 'origin/dev'` while the remote had moved on. **[S]**

- `git status` compared local `dev` (`5765a527a`) only against the *cached* remote-tracking ref `refs/remotes/origin/dev`, which also pointed at `5765a527a`.
- A live `git ls-remote origin refs/heads/dev` returned `9c4c09eb7dc6545b4c74fc14dbade2f6f3b64396` ("added sprint 1609 plans and design mockups").
- Root cause: no recent fetch had updated the tracking refs. Repository configuration is healthy — single remote `origin` → `https://github.com/collisionengineers/pegasus.git`, standard refspec `+refs/heads/*:refs/remotes/origin/*`, `dev` correctly tracking `origin/dev`, not a shallow clone.
- A `git fetch origin` (executed during this audit) fast-forwarded all tracking refs without error; local `dev` now correctly reports `[behind 1]`. All other branches (`main`, `kanmer-board`, five new remote branches) were equally stale before the fetch.

**Lesson for operators:** "up to date" messages from `git status` or an IDE never contact the network; they reflect the last fetch. Fetch before trusting them.

### 2.2 Relevant commit map

| Commit | Meaning |
| --- | --- |
| `e8efb1977` | Deployed production source (Release 53, 15 Sept 2026) — `docs/operations.md:11-14` |
| `5765a527a` | Local `dev` head (docs/skills only since Release 53; see §2.3) |
| `9c4c09eb7` | Remote `dev` head — adds `1609sprint/` plans, `.codex/PLAN.md` rewrite, v27 mockups, and the committed domain PDFs of §6 |
| `ece490e8b` | Most recent change to `src/Pegasus.Web/wwwroot/js/site.js` on any branch — predates the operator report; **does not** fix any §5 defect |
| `aefe4c32d` | Creation of `GlassEstimatePdfParser.cs` ("Complete canonical Glass PDF estimate import (TICK-085)") — the parser has never been changed since; the §5.5 rounding defect shipped with it |

### 2.3 Runtime equivalence of `dev` and production

`git diff --stat e8efb1977..5765a527a -- src/` yields exactly one changed file: `src/Pegasus.Web/Pages/Shared/_StatusChip.cshtml`, one line — a documentation comment removing the retired term `Blocked intake`. **[S]** Therefore every runtime defect described in §5 exists identically in local `dev`, in remote `dev` (`9c4c09eb7` adds no `src/` change), and in production Release 53. The performance plan records the same observation (`origin/dev:.codex/PLAN.md` §1: "its only application-source change is a Razor comment in `_StatusChip.cshtml`").

---

## 3. Delivery-pipeline status (context for the functional work)

Not a code defect, but the reason completed functional work cannot ship; included because remediation ordering in §7 depends on it.

- **PR #764** (`perf/first-use-and-image-cache`, head `e4fc0ea05`, base `dev`) implements the first-use performance package: Identity-cookie renewal vs. `no-cache,no-store` interaction repair, same-request Case frame reuse, lazy report-service activation, and scaled thumbnail decoding with miss coordination. **Unmerged; latest full CI not green: two SQL jobs timed out.** **[C]**
- **Why CI times out:** the three-shard SQL allocation groups whole classes by enumerated test count; the longest runner reached the 45-minute job limit while another finished in 16 m 47 s; the 188-test `CaseDetailsWebTests` partial class alone occupied 31 m 07 s. **[C]** The shard wiring is `.github/workflows/ci.yml:153-176` (`shard: [1, 2, 3]`, `-ShardCount 3`). **[S]**
- **The approved fix is inert:** `origin/dev:1609sprint/ci-six-shards/PLAN.md` (operator-approved 16 September) specifies six SQL runners plus splitting the 16-file `CaseDetailsWebTests` partial into 15 concrete feature classes, keeping the 45-minute limit and four concurrent classes per runner, and updating all three shard-count owners in `ci.yml`. Its own progress record states the remediation "has not yet been built, tested, committed or pushed" (implementation worktree is a Windows path, `C:/Users/Alex/...`). **[C]**
- **Production pain the PR addresses** (retained findings, `origin/dev:.codex/PLAN.md` §1): Case response 7.333 s with only 178 ms of 47 recorded SQL calls (4.175 s main-handler span; remainder unattributed); Work Centre 3.373 s including one 1.801 s SQL dependency; thumbnail responses carry the intended ETag *and* `no-cache,no-store` with a renewed sign-in cookie, defeating browser reuse; first nine-image pass 12.26–16.41 s with one miss spending 6.164 s at the provider gate and 4.385 s rendering (3.440 s at decode); warm navigation already acceptable (Cases LCP 0.35–0.60 s). **[P]**

---

## 4. Audit of core invariants — sound areas

These areas were audited and found sound; documented here so the §5 conclusion rests on a surveyed baseline, not on absence of looking.

### 4.1 Case/PO identity allocation

**Files:** `src/Pegasus.Infrastructure/Persistence/CaseIdentityAllocator.cs` (whole file); `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs:525-540, 555-566`; migration `20260729150000_DocumentCustodyAndRequests.cs:414-427`. **[S]**

- Annual per-lineage sequences are stored in `CaseSequences` with composite primary key `(SequenceLineageId, Year)` and check constraints bounding `Year` (2000–9999) and `LastAllocatedSequence` (0–999).
- Exhaustion is refused before allocation (`CaseIdentityAllocator.cs:36-38` → `CaseIdentitySequenceExhaustedException`), and intake allocation classifies that failure as genuinely `Blocked` with the honest message "No case was created" (`src/Pegasus.Core/Intake/IntakeAllocation.cs`, `Classify`, INTK-044 comment).
- **Concurrency analysis:** two simultaneous allocations for the same lineage can both read `LastAllocatedSequence = n` and both compute `n+1`, but (a) `SaveChanges` executes the sequence update and the Case insert in one transaction, and (b) `Cases.Reference` carries a unique index (`PegasusDbContext.cs:561`), so the loser's insert fails and its sequence bump rolls back. The failure is classified recoverable (`ReloadThenRetry`) and retried idempotently. Worst case is an ugly recoverable failure, never a duplicate or lost Case/PO.
- The reference format `{PrincipalCode}{yy}{sequence:000}` and the "Audit shares its original's sequence" rule (`PegasusDbContext.cs:564-566`) preserve the CONTEXT.md contracts: *Case/PO is immutable; never delete or reuse* (see also the wrong-Principal replacement path, `EfLinkedCaseReplacementStore.cs:120-136`, which reuses the same allocator).

### 4.2 Intake allocation: idempotency, replay and failure classification

**File:** `src/Pegasus.Core/Intake/IntakeAllocation.cs`. **[S]**

- Every attempt is durable and deduplicated by an operation key plus a SHA-256 command hash over kind, command, actor, roles, key and reason (`CommandHash`, bottom of file). Concurrent staff retries converge on one Case identity; a replay with a *different* command under the same key throws `IntakeAllocationOperationConflictException` rather than silently forking.
- The concurrent-caller wait is bounded at ten seconds with an honest `Pending` report thereafter (CASE-005 note) — no indefinite blocking, no invented success.
- Failure classification is explicit and honest: Principal-unavailable → retry after correction; concurrency conflict → reload then retry; sequence exhaustion → blocked; anything else → recoverable with "No reference was allocated" (INTK-044 note: an unclassified fault must not become terminal, because staff cannot hand-create an Audit).
- The automatic route only runs for a receipt already decided `CaseCreated` on an accepted route; **manual upload is a proposal** and creates nothing until staff accept the extracted draft (`AttemptAutomaticAsync` early return) — matching FRD-02's acceptance boundary. Completeness is *observed* from retained photographs rather than a constant `true` (CASE-021 note), so Review and the EVA export agree by construction.

### 4.3 Image-intake pairing and merge

**File:** `src/Pegasus.Core/ImageIntake/ImageIntakeCasePairing.cs`. **[S]**

- Association target selection is strict: exact confirmed-registration match, exactly one candidate, group member-count rules, optional Principal pin (`SelectRegisteredTarget`). No near-miss completion of an immutable identity.
- Every member link re-reads current state and re-checks uniqueness; divergence throws `IntakeAssociationConflictException` ("A group member has a different association decision", "The current image destination is no longer unique") instead of writing.
- The automatic reconciliation sweep cannot touch manual-upload groups (each manual link is one explicit, reasoned staff decision); staff-decided groups are completed through the same owner with version-checked re-reads.
- Merge is idempotent per operation key (`image-intake-merge:{receiptId}`) and only publishes external work after the durable write.

### 4.4 Authorization

**Files:** `src/Pegasus.Web/Program.cs:676-681`; `src/Pegasus.Core/Intake/IntakeAllocation.cs` (`RequireStaffActor`); Core use cases generally. **[S]**

- A global fallback policy `RequireAuthenticatedUser` covers every endpoint including Razor Pages; the `Administrator` policy is role-based. No page or handler was found relying on default-anonymous access outside the intentional sign-in/denied surfaces.
- Business authorization is enforced in Core use cases (`StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework)` and equivalents), matching the repository principle that Core owns policy — the Web layer composes, it does not decide.
- `SystemWorker`/`Provider` actors are explicitly named and attributed (e.g. provider notes are written to the case timeline attributed to the instructing Principal, `WriteProviderNoteAsync`), consistent with the Automation Actor boundary in FRD-10.

### 4.5 Persistence integrity

**File:** `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` (model configuration passim); store implementations. **[S]**

- Unique indexes guard exactly the identities that must not duplicate: `Cases.Reference` and `Cases.AuditReference` (`:561-562`), `(CaseId, OperationKey)` pairs for idempotent commands, `(MailboxIdentity, ImmutableItemIdentity)` for immutable mail identity, report/valuation references, and others.
- `CaseEntity` carries both an EF `Version` concurrency token and an application-managed `ConcurrencyToken` (`:558-560`), and stores catch `DbUpdateConcurrencyException` and translate it into honest reload/retry outcomes (e.g. `EfCaseAcceptanceStore.cs:682`, `EfCaseDataStore.cs:162,279`).
- Check constraints encode invariants in the schema itself (`CK_Cases_Sequence`, `CK_CaseSequences_*`, `CK_Triage_Sequence` — the latter the subject of a deliberate earlier counter-first allocation fix, remote branch history `c-triage-allocator-hunks`, commit `65002169f`).

**Conclusion of §4:** the patterns that protect money-adjacent, identity-critical behavior (references, custody, association, idempotency) are consistently defensive. This is why §5 stands out: the Estimate journey's defects are not "more of the same" — they are a localized break in an otherwise disciplined system.

---

## 5. Critical functional finding — the Estimate-import journey

The operator report (retained verbatim in `origin/dev:1609sprint/import-test-and-fix/draft-plan.md`): *"Import estimate button not responsive — doesn't close after choosing file"*, with a production HAR (`artifacts/estimatehar.har`) and two screenshots; then *"C:\Users\Alex\Desktop\calc.pdf also wouldn't import but it should be set up"*.

The journey fails at five independent layers. F1–F3 are dialog mechanics affecting **every Case dialog**, not only imports; F4 is feedback; F5 is parser arithmetic.

### 5.1 F1 — Dialog opener never prevents default navigation

**Mechanism.** The Estimate section's Import control is a real anchor:

```html
<!-- src/Pegasus.Web/Pages/Cases/Shared/_CaseEstimate.cshtml:158-166 -->
<a class="btn btn--small" asp-page="/Cases/Details" asp-route-id="…"
   asp-route-section="estimate" asp-route-dialog="import-estimate"
   data-dialog-open="import-estimate-dialog" data-estimate-import>
```

Its target, `import-estimate-dialog`, is a **div-backdrop dialog** (`class="dialog-backdrop" data-dialog="import-estimate-dialog"`, `_CaseEstimate.cshtml:694-696`), not a native `<dialog>`. Two independent binder systems exist in `src/Pegasus.Web/wwwroot/js/site.js`:

- `bindNativeDialogs` (`:126-146`) binds `[data-dialog-open]` **only** when the target is a native `<dialog>` with `showModal` — it *does* call `preventDefault()`. For div-backdrop targets it returns without binding (`:128-129`). **[S]**
- `bindDialogOpeners` (`:1301-1315`), the div-backdrop path, binds:

```js
// src/Pegasus.Web/wwwroot/js/site.js:1312-1314
control.addEventListener('click', function () { open(control); });
```

**The handler ignores the event and never calls `preventDefault()`.** The anchor's default navigation to `…?section=estimate&dialog=import-estimate` therefore fires *in addition to* `open()` — a full page GET that discards the just-opened client dialog and re-renders the page server-side. **[S][D]** The same construction applies to the Send to AI (`_CaseEstimate.cshtml:220`), Compare (`:246`) and Discard (`:461`) controls, and per the operator's offline investigation also the Unidentified/EVA anchors. **[D]**

**Impact.** Every click on these controls costs a full page reload; the user ends on a server-rendered dialog state (F2); the URL retains `dialog=`, so refresh/re-login re-opens it. **[D]**

### 5.2 F2 — Server-opened dialog has no Escape, no inert, no focus trap

**Mechanism.** The server supports opening these dialogs via route: `Details.cshtml.cs:843-850` maps `"import-estimate"` → `Model.OpenDialog`, and the markup renders `hidden` absent for the selected dialog (`_CaseEstimate.cshtml:694-696`). But the client-side `open()` function — which installs the Escape handler, focus trap, focus return and `inertOutside` shell inactivity (`site.js:1158-1206`, `pegasusOpen` registration `:1234`, openers map `:1290`) — is only auto-invoked for dialogs marked `data-dialog-open-on-load="true"` (`site.js:1292-1297`). The Case dialogs carry no such attribute; only the Unidentified page dialogs do (`src/Pegasus.Web/Pages/Unidentified/Details.cshtml:278,331,358,381,404`). **[S]**

**Impact.** The dialog the operator actually lands on (after F1's navigation, or after any direct `?dialog=` link) is visible but inert-less and Escape-less: no way to close it with the keyboard, no focus containment, nothing behind it deactivated; the URL keeps `dialog=` so F5 re-opens it. The operator's literal words — "doesn't close after choosing file" — are this defect plus F4. **[D]**

### 5.3 F3 — In-place submit from a script-opened dialog can freeze the whole application shell

**Mechanism.** `inertOutside` (`site.js:1131-1144`) marks all siblings-inert while a dialog is open and returns a `release()` closure that runs on `close()` (`:1201-1206`). The Estimate import form submits in place (`data-estimate-import-form`, `_CaseEstimate.cshtml:704`; in-place swap machinery in `src/Pegasus.Web/wwwroot/js/case-workspace.js` ~`:735-806`). When a *script-opened* dialog inside a swapped root submits in place, the swap detaches the dialog element from the DOM **together with its release closure**, so the `inert` attributes set on `header.app-rail`, `section.utility-bar`, `nav.workspace-tabs`, the sticky ribbon and the toast region are never removed. **[D]** (Reproduced offline by the operator's investigation against deployed bytes; consistent with the closure structure verified at `site.js:1131-1144`. **[S]**)

**Impact.** From the operator's chair, the application is frozen: navigation, tabs, actions and toasts all dead until a manual full reload. This is the worst-class UI failure in the list and justifies the F1/F3 pair being ranked first in remediation (§7).

### 5.4 F4 — A 5.9-second import gives no visible feedback, and refusals are invisible

Four compounding feedback failures, all verified in source: **[S]** with production timings **[P][D]**:

1. **No busy state.** The import POST (`OnPostImportEstimateAsync`, `Details.cshtml.cs:2905`) took **5.9 s** in production (HAR). The form does set `aria-busy` during in-place submit (`case-workspace.js:735`, cleared `:758`), but no stylesheet rule exists for `form[aria-busy]` — `aria-busy` styling exists only for `.viewer-stage` (`wwwroot/css/site.css:774`) and `.case-viewer-stage` (`wwwroot/css/case-workspace.css:474`). The button therefore looks dead for six seconds.
2. **Silent click-drop.** `inplaceSubmitting` (`case-workspace.js:797-802`) swallows repeat clicks without any indication — the "not responsive" half of the operator report.
3. **Refusals render where the operator is not.** Parser refusals render into `[data-case-notices]` at the top of the page (`src/Pegasus.Web/Pages/Cases/Details.cshtml:99`); the in-place swap preserves the scroll anchor at the Estimate section, so the notice is never seen.
4. **Only successes are toasted.** The swap handler toasts only `[data-confirmation]` (success) notices (`case-workspace.js:700-704`); failures stay silently at page top.
5. **The "Complete import" retry repeats the invisibility:** re-running the same refusal, again unseen. **[D]**

### 5.5 F5 — Glass's Calculation PDFs are refused by a rounding-model mismatch

**Mechanism.** `src/Pegasus.Infrastructure/Assessment/GlassEstimatePdfParser.cs:308-323`, `Complete()`:

```csharp
|| own.Sum(item => item.Hours ?? 0) != sum.SummaryHours
|| own.Sum(item => item.Labour ?? 0) != sum.Labour || sum.Labour != sum.SummaryLabour
|| own.Sum(item => item.Material ?? 0) != sum.Material || sum.Material != sum.SummaryMaterial
|| sum.Labour + sum.Material != sum.Total
|| own.Any(item => item.Hours is { } hours
    && decimal.Round(hours * sum.SummaryRate.Value, 2, MidpointRounding.AwayFromZero) != (item.Labour ?? 0))
    throw Reject("The main rows disagree with their printed section totals or rate");
```

The parser demands that the **sum of per-row rounded amounts** equal the **printed section total** exactly. Glass's itself computes the section labour by **rounding once** from section hours × rate. The operator's `calc.pdf`: rows 66.62 + 133.25 + 141.58 + 24.98 + 24.98 = **391.41**, printed section labour **391.42** = round(4.70 h × £83.28). Refused. The five reference fixtures used during the parser's development happened to reconcile (rate 0.00, integer rate 80.00, or coincidental sums), so the defect escaped the original TICK-085 verification. **[D]** The parser is unchanged since its creation commit `aefe4c32d`. **[S]**

**Contract context.** FRD-06 (`docs/frd/frd-06-vehicle-and-engineering-evidence.md:340-355`) requires "Whole-row, section and document reconciliation" and that "complete arithmetic must agree before an import succeeds" — the printed section total *is* the source arithmetic; the parser's stricter sum-of-rows identity is the implementation choice that must be reconciled with Glass's single-rounding model. The operator has already decided the outcome: "it should be set up". The same FRD paragraph deliberately refuses unsupported formats, so the **Audatex Customer Estimate refusal is correct and stays** ("This file was not recognized as an Audatex estimate report…", `src/Pegasus.Infrastructure/Assessment/AudatexEstimatePdfParser.cs:180`; operator decision: keep refusing, keep the wording). **[D]**

### 5.6 Why the test suite did not catch F1–F4

`tests/Pegasus.IntegrationTests/AssessmentEstimateImportWebTests.cs` — 66 facts/theories — thoroughly covers the *server* contract, including the anchor's href (`?section=estimate&dialog=import-estimate`, asserted at `:463`) and the dialog's absence in read mode (`:459`). **[S]** The defects live entirely in client-side dialog mechanics (preventDefault, open-on-load wiring, inert release, busy styling, toast routing), for which no browser-level assertions exist. A browser test that opens Import, presses Escape, and submits would have caught F1, F2 and F3 at once.

### 5.7 Nothing on any branch fixes any of F1–F5

Checked across all remote branches: the most recent commit touching `site.js` anywhere is `ece490e8b` (navigation refactor; predates the 16 September report) except `perf/first-use-and-image-cache`'s unrelated `b8f7b1b55` (command-palette scroll). `GlassEstimatePdfParser.cs` has no commit after `aefe4c32d`. The diagnosis in `1609sprint/import-test-and-fix/draft-plan.md` is complete and operator-reviewed; the implementation is zero percent started. **[S]**

---

## 6. Compliance flag — domain documents committed in repository history

Commit `9c4c09eb7` pushed operator-supplied domain PDFs into the repository under `1609sprint/`: eight Audatex PDFs under `1609sprint/…/audatex/` (e.g. `1394136815897__YS71WKN Updated Audatex.pdf`, 10–133 KB each) and five Glass's calculation sheets under `…/glasss/` (e.g. `1710254173321__Calculation ML23 OXR.pdf`). **[S]**

- The repository's own rules make domain evidence strictly local: "corpus/ is local, ignored and immutable: never upload, commit, rename or modify it" (`AGENTS.md`, *Non-obvious constraints*), and the performance plan repeats "never upload raw cookies, case content, document URLs or immutable corpus/" (`origin/dev:.codex/PLAN.md` §1).
- The draft plan identifies these exact files as supplied evidence from the operator's workstation (`calc.pdf` from the Desktop; the `audatexanalysis` folder), referenced by SHA — their use as parser test inputs is legitimate; their *commit* into git history is the questionable act.
- If unintended, this is cheapest to fix now (relocate to `corpus/` or ignored `artifacts/`, then rewrite/redo the sprint-plans commit) — every subsequent push compounds the history problem. If the operator sanctioned committing them, record that decision. Either way, §7 does not proceed on this item without explicit operator direction.

---

## 7. Consolidated remediation plan

Ordered by operator impact × blast radius. Items 1–4 are the Estimate journey (§5); item 5 unblocks the pipeline (§3); item 6 is a decision, not work.

| # | Work | Why this order | Primary references |
| --- | --- | --- | --- |
| 1 | `preventDefault` in `bindDialogOpeners` + add `data-dialog-open-on-load` parity for Case dialogs | Smallest diff; kills the double navigation (F1), restores Escape/inert/focus for server-opened dialogs (F2), removes `?dialog=` URL persistence; benefits every Case dialog | `site.js:1301-1315`, `:1292-1297`; `_CaseEstimate.cshtml:694-696`; pattern already proven in `Unidentified/Details.cshtml:278` |
| 2 | Release `inert` on swap-detach (re-run release on detachment or swap-time cleanup) | Removes the app-freeze (F3) | `site.js:1131-1144`, `:1201-1206`; `case-workspace.js` swap path |
| 3 | Section-scoped refusal notices; toast failures; style `form[aria-busy]`; visible pending state on the submit button | Makes every outcome visible (F4); includes the 5.9 s POST case | `Details.cshtml:99`; `case-workspace.js:700-704, 735, 758, 797-802`; `site.css` / `case-workspace.css` |
| 4 | Glass's single-rounding reconciliation in `Complete()` (accept printed section totals; reconcile rows within one rounding step) + regression fixture from the operator's five PDFs | Unblocks the operator's explicit "should be set up" (F5) while preserving FRD-06's refusal of genuinely unsupported sources | `GlassEstimatePdfParser.cs:308-323`; `docs/frd/frd-06-…:340-355`; draft-plan item 6 |
| 5 | Six-shard CI + Case-class split (operator-approved plan), then PR #764 full CI, dev integration, promotion and live performance acceptance | Restores the delivery pipeline everything else queues behind | `origin/dev:1609sprint/ci-six-shards/PLAN.md`; `ci.yml:153-176`; `performance-pr-764/PROGRESS.md` |
| 6 | Operator decision on the committed domain PDFs (§6) | Irreversible-ish; cheap now, expensive later | `AGENTS.md`; commit `9c4c09eb7` |

Every item 1–4 change is subject to the repository's verification policy (affected browser/integration evidence, one named verifier for heavy checks) and, for item 5, to the release skill's authorized route.

---

## 8. Reference index

**Source (at `5765a527a` unless noted):**

- `src/Pegasus.Web/wwwroot/js/site.js` — `:126-146` native dialog binder; `:1131-1144` inertOutside/release; `:1158-1206` open/close; `:1234` pegasusOpen; `:1290` openers map; `:1292-1297` open-on-load; `:1301-1315` bindDialogOpeners (no preventDefault)
- `src/Pegasus.Web/wwwroot/js/case-workspace.js` — `:700-704` success-only toast; `:735/758` aria-busy; `:797-802` inplaceSubmitting
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseEstimate.cshtml` — `:158-166` Import anchor; `:220,246,461` sibling anchors; `:694-696` dialog markup; `:704` in-place form
- `src/Pegasus.Web/Pages/Cases/Details.cshtml` — `:99` notices container
- `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` — `:843-850` OpenDialog mapping; `:2905` OnPostImportEstimateAsync
- `src/Pegasus.Web/Pages/Unidentified/Details.cshtml` — `:278,331,358,381,404` open-on-load pattern (the fix template)
- `src/Pegasus.Web/wwwroot/css/site.css:774`; `case-workspace.css:474` — only viewer stages have aria-busy styles
- `src/Pegasus.Infrastructure/Assessment/GlassEstimatePdfParser.cs:308-323`; `AudatexEstimatePdfParser.cs:180`
- `src/Pegasus.Infrastructure/Persistence/CaseIdentityAllocator.cs` (whole); `PegasusDbContext.cs:525-540` (CaseSequences), `:555-566` (CaseEntity tokens/indexes); `EfLinkedCaseReplacementStore.cs:120-136`
- `src/Pegasus.Core/Intake/IntakeAllocation.cs` (whole; esp. AutomaticCompleteness/CASE-021, AwaitRecordedOutcomeAsync/CASE-005, Classify/INTK-044, CommandHash)
- `src/Pegasus.Core/ImageIntake/ImageIntakeCasePairing.cs` (whole)
- `src/Pegasus.Web/Program.cs:676-681` fallback authorization policy
- `tests/Pegasus.IntegrationTests/AssessmentEstimateImportWebTests.cs:459-470` (server-only coverage boundary)
- `.github/workflows/ci.yml:153-176` (three-shard wiring)

**Documents:**

- `docs/operations.md:7-22` — Release 53 record (deployed source `e8efb1977`, smoke evidence, skipped live checks)
- `docs/frd/frd-06-vehicle-and-engineering-evidence.md:340-355` — estimate import arithmetic/refusal contract
- `CONTEXT.md` — Case/PO immutability, Estimate terminology
- `AGENTS.md` — corpus constraint, verification policy, release discipline
- `origin/dev:1609sprint/import-test-and-fix/draft-plan.md` — operator report and offline HAR reproduction (items 1–6)
- `origin/dev:1609sprint/performance-pr-764/PROGRESS.md` — PR 764 status, CI timeouts, verification ledger
- `origin/dev:1609sprint/ci-six-shards/PLAN.md` — approved six-shard plan
- `origin/dev:.codex/PLAN.md` — performance baseline findings and acceptance targets

**Commits:** `e8efb1977` (deployed) · `5765a527a` (local dev) · `9c4c09eb7` (remote dev; sprint docs; committed PDFs) · `ece490e8b` (last site.js change) · `aefe4c32d` (Glass parser creation, TICK-085) · `b8f7b1b55` (perf branch site.js) · `65002169f` (Triage counter-first allocation fix) · PRs [#761](https://github.com/collisionengineers/pegasus/pull/761) (Release 53), [#764](https://github.com/collisionengineers/pegasus/pull/764) (performance, open).

---

*This report is sprint working material, committed under `1609sprint/deep-code-audit/` by explicit operator direction on 16 September 2026, alongside the other 16 September handovers. It makes no change to application code, documentation owners, or cloud resources, and asserts no verification results beyond those labeled above. Placement note: `1609sprint/` is outside the approved roots of `scripts/Test-MarkdownPlacement.ps1`, so the base..head placement gate fails for this commit exactly as it already fails for the operator's `9c4c09eb7` sprint commit; the eventual dev→main promotion needs an explicit disposition covering both.*
