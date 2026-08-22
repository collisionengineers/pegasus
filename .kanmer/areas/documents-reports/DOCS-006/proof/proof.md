# Observed on a real instruction — QDOS26010

Read from `IntakeAssets` in production on 2026-08-22, for the receipt that
created QDOS26010.

## Extraction and retention work

Twenty `embedded_image` rows were extracted from the instruction PDF and
retained across four pages, each with its recorded dimensions — the shape this
ticket introduced:

```
page-1  5 images     page-3  4 images (709×768, 85 KB–158 KB)
page-2  1 image      page-4  5 images (709×768 and 709×331, 82 KB–253 KB)
```

Nine of those are genuine damage photographs at 709×768 (ratio 1.08) plus one at
709×331. They sit alongside six deliberately attached photographs
(`1_Mileage-V1.jpg`, `11_Vin-V1.jpg`, `3_CLVDamage1-V1.jpg` and three more) and
the retained `message/rfc822` source at 14.7 MB.

Six Outlook inline graphics were retained as `inline_image` and are correctly
outside the evidence selection, as are the two letterhead banners — the
selection half is proved in detail under [[INTK-030]].

## What this does not yet show

The Box side of "and Box files". Custody for this case failed on a cause
unrelated to this ticket — the Worker had no grant on the case-document tables,
fixed and verified under [[DOCS-008]] in release 20 — so the photographs are
extracted and retained but their Box registration has not been observed for a
case created since that fix.

That is a dependency, not a defect in this work: the promotion code has been
exercised by the corpus end-to-end custody fact (EREF9) and by the audit-root
integration test, and the live gap closes on the first case created after the
grant, or on an operator pressing **Retry custody**.

## Evidence tier

Extraction and retention: **observed in production**. Box registration: covered
by integration tests, not yet live-observed.
