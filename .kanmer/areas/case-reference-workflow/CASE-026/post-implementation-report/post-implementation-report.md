## Review-fix pass (2026-08-28)

`origin/dev` merged into the branch (clean, no conflicts: Operations page,
`_StatusChip`, `OperatorLabels`, `OperationsWebTests`). Then the three real
shard-2 failures and the six Codex findings on PR #606; dispositions in the
plan under "Review findings — dispositions (2026-08-28)".

### Changed

- `Pages/Search/Index.cshtml` — header action to `/Upload` (the receipt-less
  `/Cases/Create` is a 404); `aria-label="Filter cases"` restores the
  accessible name the port dropped; the query-failure notice is hoisted so
  one failed load cannot render an image empty line, a "0 results" count or
  an empty pane; rows carry `data-copy-reference`; a `@section Scripts`
  keeps the copy source and the refresh form's `selected` on the row the
  shell selects.
- `Pages/Search/Index.cshtml.cs` — the exact-reference image lookup passes
  `State` and `ClosureReason` instead of taking the summary's
  `AwaitingInstruction` default.
- `CasesIndexWebTests.cs` — the two `·` assertions now pin the rendered
  bytes (`&#xB7;`, the convention MAIL-025 settled); new coverage for the
  image lifecycle state, for the image-failure notice, and for the
  per-row copy reference.
- `AdministrationSearchAccountWebTests.cs` — the 301 test now carries a
  whole thirteen-parameter bookmark and asserts both the redirect target
  byte for byte and that `/Search` renders every value back into its
  field.

### Verification (this pass, on the task branch after the dev merge)

- `dotnet build ./Pegasus.slnx --configuration Release` — PASS, 0 warnings,
  0 errors.
- `dotnet test ./Pegasus.slnx -c Release --no-build --filter
  "FullyQualifiedName~CasesIndexWebTests"` — PASS 5/5 (was 2 failed / 1
  passed before the fixes).
- `--filter "FullyQualifiedName~QdosCustodialWebTests"` — PASS 5/5.
- `--filter "FullyQualifiedName~AdministrationSearchAccountWebTests"` —
  PASS 6/6.
- `--filter "FullyQualifiedName~ImageIntakeWebTests|
  FullyQualifiedName~ShellAndStatusPageWebTests"` — PASS 7/7 (the image
  summary path and the shell's `/Search` links).
- Ticket verification item 1 (`/Cases?query=` 301 with values intact) is
  now proved by test, not by inspection.
- Ticket verification item 2 (no clipped text at 1580/1100/760) is NOT
  proved here: it needs a browser run, which this lane does not do. The
  pass added no fixed-width element — a standard `notice`, one attribute
  and a script — so the risk is unchanged from the reviewed layout, but
  the proof stays the orchestrator's walk.

### Still owned by someone else

- `_ShellDialogs.cshtml:64` and `wwwroot/js/site.js:1364` link Create Case
  and Ctrl N to the same receipt-less `/Cases/Create` 404 (PLAT-029).
- `TestUiSnapshotTests.cs:29` still matches the pre-port
  `<h2>Cases are unavailable</h2>`; snapshot generation will report
  `cases--unavailable (/Search)` missing until that constant follows the
  ported notice (UIIMP-005).
- The durable copy fix: delegate `[data-copy-target]` binding in site.js.
