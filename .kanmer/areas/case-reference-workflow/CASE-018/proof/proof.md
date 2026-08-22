# Proof — CASE-018

Release 23, source `b6d54ff6665253a4abe01c646bd64f238b15e24b`, image `sha256:7193802c…`, revision `pegasus-prod-web-252ow37gij--b6d54ff6-eva` serving 100% of traffic. Smoke passed 2026-08-22 and asserts the deployed source revision matches the manifest exactly.

## The alignment fix, verified on the deployed asset

Fetched from production, unauthenticated:

```
GET /css/site.css
.datarow { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1.4fr) 22px; … }
```

The reserved 22px third track is live. That figure is the whole fix: `.prov` is a fixed 22px, and the first attempt left the track `auto`, which each row sizes independently — so an iconed row still took 22px out of the flexible tracks and started its value 9px to the left.

## The alignment fix, measured before it shipped

Rendered locally under `DevelopmentOffline` against a database seeded to QDOS26011's exact field shape, and measured the value column's left edge in Chrome.

Before, in Case identity — the two rows carrying provenance icons out of line by 9px, exactly as reported:

```
Case/PO 645   Audit identity 645   Case type 645   Principal 645
Claimant 636* Claim number  636*   VAT status 645  Engineer  645     (* iconed)
```

After, every block column reporting **one** distinct left edge, including the columns that mix iconed and plain rows:

| Block column | Distinct left edges | Mixes iconed / plain |
| --- | ---: | --- |
| Case identity | 1 | yes — 2 of 8 iconed |
| Vehicle / Inspection | 1 | yes — 6 of 8 |
| Dates / Contact | 1 | yes — 2 of 7 |

## The removals, verified in rendered HTML

Two independent fixtures, both rendered through the real page:

| Claim | QDOS26011 fixture | `OperatorJourneyTests` fixture (QDOS31001) |
| --- | --- | --- |
| "Where this case stands" absent | 0 occurrences | 0 occurrences |
| "Engineer queries" absent | 0 occurrences | 0 occurrences |
| Registration rendered once | exactly 1 | exactly 1 |
| Mileage rendered once, classified | `121,823 Miles — Estimated` | `94,730 Miles — Supplied` |

Nothing was lost in the process: the seven fields that lived only in the deleted "Case detail" list — contact name, e-mail and phone, VAT status, inspection date, deadline, address and mode — all render in the new Inspection and Contact blocks, confirmed in both dumps.

## Regression coverage

The full browser suite passes: 44 local, and CI's `browser` job green in 8m37s on the merged commit, alongside three `sql-integration` shards and `unit`.

One test had to be corrected, and it is worth recording why. `OperatorJourneyTests.CustodyRecoveryAndEvaHandoff…` asserted the bare word `"confirmed"` after custody completes. The custody row has never carried it — it names the work state, and the page reads `Case evidence — Completed`. The assertion was matching the read-only Vehicle evidence panel's `Confirmed registration / make / model / mileage` labels, which are the third place the vehicle appeared and the exact thing this ticket removes. A step named "custody recovery" was asserting unrelated vehicle text and passing. It now asserts the custody row reads `Completed` and not `Failed`, which is strictly stronger: the old form passed even with a `Failed` row present.

## Not claimed

That an operator has viewed the deployed page. Production requires an interactive sign-in, which this agent does not perform. What is claimed is that the deployed stylesheet carries the fix, the deployed image is built from the exact reviewed commit, and the same markup was measured and photographed on two fixtures before release.

The dark header band still repeats registration, claimant, principal and case type. Left deliberately at the operator's direction (2026-08-22: *"keep the details in the header"*). Its separate defect — reading those from the intake draft rather than the case — is [[CASE-020]].
