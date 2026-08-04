# csp-inline-style-hotfix

Found during the release-4 live checks (2026-08-04): the production CSP
(`default-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors
'none'`, no `style-src`, `Program.cs:656`) makes browsers discard every
inline `style` attribute. The Lucide sprite's `style="display:none;"` is
therefore ignored on the deployed site and the sprite renders as a ~1,900px
inline SVG at the top of `<body>`, pushing all page content below the fold on
every route. Local browser tests never see this because the Development
profiles do not send the production CSP header.

## Change

Replace every static inline `style` attribute under `src/Pegasus.Web/Pages`
with a site.css class (all nine are static):

- `_LucideSprite.cshtml` — the critical one: `class="sprite-sheet"` with
  `.sprite-sheet { display: none; }`.
- `_ErrorSummary.cshtml` (2), `_MetricCard.cshtml` (2),
  `_ProvenancePanel.cshtml` (2), `_ReasonDialog.cshtml` (2) — cosmetic
  equivalents.

Add a browser-lane regression test asserting the app shell starts within the
viewport (the sprite contributes no layout height) so a reintroduced visible
sprite fails CI.

## Explicitly out of scope

The inline `<script>` blocks (`_FreshnessBanner`, `_ReasonDialog`, the
unrouted Assessment artifacts) are equally dead under the production CSP but
are progressive enhancement only; choosing between external script files and
CSP hashes is a queued decision, not a hotfix.
