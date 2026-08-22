# Proof

**Shipped:** PR #505, commit `ad1ba223` · **Deployed:** Release 17, `71911734`,
carried forward to Release 20, `05fe7a7f`.

## Verified against a real production instruction

QDOS26010's retained assets, read from `IntakeAssets` on 2026-08-22 with the
dimensions the extraction path recorded. Applying
`InstructionEvidenceImages.Select` to these exact rows:

| Kind | Example | Bytes | W×H | Ratio | Selected? | Rule |
| --- | --- | ---: | --- | ---: | --- | --- |
| `inline_image` ×6 | `Outlook-3zhoua35.png`, `image001.png` | 839–7,703 | — | — | **no** | inline never qualifies |
| `embedded_image` ×5 | `page-1-image-1.png` | 2,395–29,026 | 306×120 … | 1.00–2.55 | **no** | under the 40 KB photograph floor |
| `embedded_image` | `page-1-image-1.png` | 110,783 | 1990×437 | **4.55** | **no** | over the floor, fails the 3.0 side-ratio test |
| `embedded_image` | `page-1-image-2.jpg` | 77,972 | 2214×248 | **8.93** | **no** | same |
| `embedded_image` ×9 | `page-3-image-1.jpg` … | 82,588–253,710 | 709×768 / 709×331 | 1.08–2.14 | **yes** | genuine damage photographs |
| `attachment` ×6 | `1_Mileage-V1.jpg`, `11_Vin-V1.jpg`, `3_CLVDamage1-V1.jpg` | 133 KB–335 KB | — | — | **yes** | deliberately attached images |

The two rejected banners are **the exact pair the fix was measured against**: the
1990×437 PNG at 110,783 bytes and the 2214×248 JPEG at 77,972 bytes named in
`InstructionEvidenceImages`' own comment as QDOS26008's false positives. Neither
a byte floor nor a format test would have caught them — one is a large PNG, the
other a JPEG, and both clear the 40 KB floor comfortably. The side-ratio test is
what excludes them, and it does so on live production data.

Six Outlook signature graphics are excluded before any measurement, by kind.

Nothing genuine is lost: every 709-wide photograph survives, including
`page-4-image-5.jpg` at ratio 2.14, which sits between the photographs and the
banners and is correctly kept — the 3.0 threshold has real clearance on both
sides (widest photograph 2.14, narrowest banner 3.30 across the corpus sample).

## Tests

Corpus-measured thresholds with the measurements recorded in the code, plus
extraction tests over real signature blocks and real damage photographs. CI green
on PR #505.

## Evidence tier

**Observed in production** for the selection rule — the inputs are a real
forwarded QDOS instruction and the recorded dimensions are the ones the deployed
extractor produced.

The one thing **not** observed is the rendered gallery, because that needs an
authenticated sign-in I must not perform. The gallery renders exactly this
selection, so what remains unchecked is the page, not the rule.
