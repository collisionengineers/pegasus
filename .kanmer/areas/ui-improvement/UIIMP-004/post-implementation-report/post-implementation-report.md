# Post-implementation report

## Delivered

Test UI is now generated from actual current Razor responses rather than manually approximated HTML. A test-only capture hook records responses from existing integration tests, focused tests supply the few otherwise-unreached states, and one manifest selects and documents every generated state.

The audit confirmed the previous 60-state catalogue contained older/reworked behavior. The coherent current catalogue is 57 states: three impossible current branches were removed and three outcome names were corrected.

## Boundaries

No Live Razor behavior, runtime composition, database schema, cloud resource or deployment unit changed. Test UI remains a Start-only static mode and is absent from application/release inputs. Deployment is n/a.

## Verification

- Fresh integration capture: 260 passed, 11 skipped for unavailable genuine corpus, 0 failed.
- Generated snapshot update and byte verify: pass for all 57 states.
- Catalogue validation: 52 routed sources, 57 prototypes, 0 broken local references.
- `dotnet build Pegasus.slnx --configuration Release --no-restore`: pass, 0 warnings, 0 errors.
- `scripts/Test-UiModes.ps1`: pass.
- `git diff --check`: pass.

## Independent-review corrections — 2026-08-26

The first review blocked merge and identified unsafe default fallback selection, missing browser parity, live root-relative URLs, and incomplete GUID normalization. Commit `35292cff` fixes all four:

- explicit current-render markers prevent Access Denied responses from satisfying four default states;
- all opaque GUIDs normalize consistently within each page;
- unmatched root-relative action/download/image/search URLs become inert local targets rather than broken live paths;
- Chromium now compares post-JavaScript DOM and full-page screenshots for every generated state at 1440 x 1000.

The corrected 57-state browser parity pass, catalogue validation and diff check pass.

## Chrome live/offline correction — 2026-08-26

Chrome was used against the real Development Razor host at `http://localhost:5233` and the generated catalogue served unchanged from loopback HTTP.

- Live and offline sign-in default states had identical accessibility DOM and exact computed main/form geometry.
- Chrome confirmed the prior evidence defect: generated `vehicle.png` had `naturalWidth: 0` with `src="#"`.
- The capture middleware now retains exact `image/*` response bytes. The generator embeds those bytes as data URLs before route/GUID rewriting.
- Chrome then confirmed `vehicle.png`, `overview.png`, and `close-up.png` load successfully from captured PNG bytes with no console warnings/errors.
- The tautological two-local-file comparison was removed. Durable verification now combines normalized Razor byte comparison with Chromium rendering of every offline page, non-empty screenshots, and positive dimensions for every visible image.

Commit: `44d16f46`.

## GitHub CI SDK correction — 2026-08-26

PR run 32991612398 failed the same mail-workspace route-preservation assertion twice on GitHub while the focused test and exact 306-test shard passed locally (305 passed, 1 intentional skip). The runner log showed the shared build action installed SDK 10.0.400 from `10.0.x`; the repository baseline is 10.0.302 and local validation used 10.0.303. Commit `f7c87173` makes the action's claimed pinned behavior real by installing 10.0.302 and restricting `global.json` roll-forward to the latest patch in that feature band. `dotnet --version`, the exact shard, actionlint, and `git diff --check` pass locally. Fresh GitHub CI is pending.

### Reviewer correction

Independent review blocked the initial 10.0.302 setup pin because local passing evidence used 10.0.303 and the repository-wide scope had not been added to the ticket plan. Commit `c7b47a29` pins both `global.json` and the shared action to the validated 10.0.303 baseline; the operator-requested CI resolution and its revert-if-unproven acceptance rule are now explicit in the plan. Fresh GitHub proof remains required.

### Confirmed mail-route fix

The clean 10.0.303 GitHub run failed identically, disproving the SDK hypothesis; commit `f5d072c5` restores the original SDK/action settings. Commit `77d2a04a` narrows the assertion to the exact matching case anchor and exposed that its generated URL omitted `mailbox` on the GitHub/Linux runner. Commit `f840d48a` replaces only that anchor's individual Tag Helper route attributes with `QueryHelpers.AddQueryString` over the same PageModel values. The focused end-to-end test passes locally and fresh GitHub proof plus independent re-review are pending.

### Final root cause

Run 33004368148 proved the explicit `QueryHelpers` candidate URL still lacked `mailbox`, so commit `e46845c2` removes that workaround and restores the original Tag Helper. The failure is the renamed `MailboxFilter` GET property arriving null on the GitHub runner despite the raw `mailbox` query. `OnGetAsync` now reads that canonical query key directly and applies the existing trim/null normalization. The exact-anchor focused regression passes locally; fresh GitHub proof and final re-review are pending.

### Combined GitHub fix

Run 33005929015 showed direct query population alone still failed at the exact candidate URL. The evidence matrix now establishes two required boundaries: explicit GET population and explicit candidate URL generation. Commit `d119bd39` combines the two already-focused corrections; the exact-anchor test passes locally. Fresh GitHub proof and final independent review remain pending.

### Exact candidate URL evidence

The custom-message run captured `/Inbox/...?...mailbox=<unrelated-guid>&pageNumber=2&section=case&caseQuery=MAIL31001&targetCaseId=...`. Commit `60d6ebea` changes only the candidate URL's mailbox source from the corrupted bound property to raw `Request.Query["mailbox"]`. The focused exact-anchor test passes locally; fresh GitHub proof and final review are pending.

### Authentication overwrite correction

The corrupt mailbox GUID is the integration administrator identity, proving post-handler overwrite rather than a malformed client URL. Commit `74371f98` captures the raw GET mailbox into a private non-bindable field before other work and uses it for the candidate URL, with the existing bound property retained for other/non-GET consumers. Focused regression passes; fresh CI and review are pending.

### Null-context review correction

Commit `5287ee81` adds an initialization flag so a GET with no mailbox remains intentionally null instead of falling back to the corrupted bound property. The existing no-mailbox candidate-search scenario now asserts the exact anchor contains no mailbox parameter. Both focused candidate tests pass locally.
