# Proof — verified in production

**Shipped:** PR #505, commit `1a86f5db` · **Deployed:** Release 17 (`71911734`), still live on Release 18.

## The mileage is now read from the report

Production `CaseDataFields` for QDOS26010, the first audit to arrive after the deploy:

```
vehicle_mileage       fact  132389
vehicle_mileage_unit  fact  miles
```

QDOS26009, which arrived **before** this fix reached production, has no `vehicle_mileage`
field at all. Same principal, same instruction shape, same kind of bodyshop report — the
difference is the de-anchored `Speedo:` rule.

That is the operator's original complaint closed: *"mileage neither extracted nor
calculated"*.

## The rest of the grammar came with it

The same receipt yielded, all as `fact`:

```
claimant_name  Mr James Ainsworth      vehicle_make          RENAULT
claim_number   LEB//47837/1            vehicle_model         TRAFIC SL27 SPORT DCI
incident_date  2026-08-18              vehicle_registration  LG64JAU
```

Registration parsed correctly, which was the second half of this fix — it had been followed
by `Registered:`/`Type:`/`Trans:` on its own line and never parsed either.

## No filename dependency

`IsReportFragment` was removed rather than widened, and this report is named
`1_Bodyshopreport295952-V1.pdf` — a different naming shape from QDOS26009's
`Bodyshopreport236502-V1.pdf`, arriving alongside seven other attachments. The grammar found
it by content.

## Not claimed

Whether the extracted mileage renders on the case overview with the report cited as its
source has not been seen; that needs an authenticated session. The extraction, its value,
its unit and its `fact` classification are proved from the persisted case data.
