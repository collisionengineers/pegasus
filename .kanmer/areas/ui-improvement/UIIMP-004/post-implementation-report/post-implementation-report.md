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
