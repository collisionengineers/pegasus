# Photo capture app: static assets

The phone app behind `/c/{token}` and `/s/{slug}`. It is the Collision Engineers
photo app from the handover of 5 November 2026 (Base44 test build v10.34,
`public/capture-v10.html`), converted into static assets. The operator-approved
wording of 5 November 2026 is kept verbatim.

## Files

- `shell.html`: the markup the Razor page emits inside `<body>`, with the element
  ids `capture.js` needs and the placeholders the page fills.
- `capture.css`: the prototype's stylesheet unchanged, then one class for each
  former inline `style` attribute, then the outline of a photo the server refused.
- `capture.js`: the prototype's script as one strict-mode classic script (ES2020,
  no modules), still built round the `S` state and the `R` renderer per step.
- `img/`: the images the app shows.

## Conversion rules

- CSP: `default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' blob:;
  media-src 'self' blob:; connect-src 'self'`. No inline script, style
  attributes, inline handlers, `javascript:` or `data:` URLs, and no external
  fonts. CSSOM property writes such as `el.style.height` are allowed.
- Every user-typed or server-sent value put into markup goes through `esc()`.
- Images are resolved through the `#capture-assets` manifest with `asset(name)`.
  Nothing is hard-coded, and the prototype's `?v=` cache-busting is gone.
- The link context comes from `#capture-context` and `GET /capture/api/v1/context`.
  The credential travels only in the `X-Capture-Link` header (`c.<token>` or
  `s.<slug>`, taken from the path), with `credentials: 'omit'`.
- localStorage, keyed by the context's `linkKey`, holds only the step reached and
  the form in progress. It never holds the token.
- These prototype-only parts were removed: the hash switches, the "Test build"
  strips, the switch and skip links, "Start again (prototype)" and the simulated
  upload.

## Asset manifest

Keys are the path under `img/` without the extension. `asset()` also accepts keys that keep the extension (`d/fd-1-o.jpg`), which is what `PhotoCapture/CaptureAssets.cs` emits:

- `logo`, `guide`, `car-top`: `logo.webp` is the header logo, `guide.webp` the
  graphic of the eight overview angles and `car-top.webp` the top-down car.
- `1` to `8` (`1.jpg` to `8.jpg`): the overview-angle examples in the viewfinder.
- `d/<zone>-<view>-<stage>` (`d/*.jpg`): damage examples. The stage is `o`
  (overview), `m` (closer) or `c` (close-up), and each zone has the three views
  the app uses: front 8 1 2; fd 1 2 3; ds-f, ds-m and ds-r 2 3 4; rd 3 4 5;
  rear 4 5 6; rp 5 6 7; ps-r, ps-m and ps-f 8 7 6; fp 1 8 7. That gives 108
  files.
- `d/x-wfd`, `d/x-wfp`: the wheel photo on the Extras tile.

The logo, the overview graphic and the 24 damage crops come from the handover
pack. Everything else was downloaded from the public test build
(`https://ce-photo-capture-test-44e77bb8.base44.app/ov/`). Each file was
checked to be a real image, and the pack's crops match the test build byte for
byte.
