# Synthetic inspection imagery

Temporary, offline design fixture generated on 25 September 2026 using the
built-in image generation tool (generate mode). It is not actual accident
evidence and does not depict the fixture claimant or a real Case.

Saved project asset: [synthetic-inspection-sheet.png](synthetic-inspection-sheet.png).
The original six-view contact sheet is retained unchanged. The mockups use CSS
to display individual cells; the same six views are reused across eleven long
file-name fixtures to exercise layout density. Case/PO, registration, Principal
and claimant values in the interface are separate synthetic fixtures.

## Final prompt

Create a single photorealistic contact sheet for a fictional automotive insurance inspection, landscape 3:2 aspect ratio. EXACTLY 6 equally sized rectangular photographs arranged in a perfectly regular 3 columns by 2 rows grid, edge to edge with NO gutters, NO borders, NO captions, NO text, NO watermarks. All six photos show the SAME unbranded silver compact five-door hatchback parked on a dry plain grey industrial yard under bright overcast daylight. Believable smartphone inspection photographs, factual neutral composition, natural subdued colours, sharp detail, no cinematic effects. The car has a dented and scraped front-left wing and cracked left corner of front bumper, otherwise intact. First row: left photo front-left three-quarter whole-car view; middle photo straight-on front view; right photo close-up of front-left wing damage and wheel. Second row: left photo rear-left three-quarter whole-car view; middle photo left side profile whole-car view; right photo close-up of the scratched front bumper and headlight. No people, no company branding or vehicle badges, number plates plain blank, no identifiable premises. This is synthetic sample evidence for an offline UI mockup, not actual accident documentation. Keep each photo fully inside its own equal-sized cell; no objects spanning cells.

## Consumption

The [Upload builder](../../build-upload-designs.mjs) embeds this asset once in
each offline HTML file. No remote image service, font service or application
endpoint is called when a mockup is opened.
