# Observed on a real instruction — QDOS26010

`CaseDataFields` read from production on 2026-08-22 for the case created by a
real forwarded QDOS audit instruction. Every report-sourced fact this ticket
introduced arrived, carrying its policy key and its provenance:

```
FieldName             Kind       Value                    SourceKind       PolicyKey
claimant_name         fact       Mr James Ainsworth       intake_evidence  qdos_instruction
claim_number          fact       LEB//47837/1             intake_evidence  qdos_instruction
incident_date         fact       2026-08-18               intake_evidence  qdos_instruction
vehicle_make          fact       RENAULT                  intake_evidence  qdos_instruction
vehicle_model         fact       TRAFIC SL27 SPORT DCI    intake_evidence  qdos_instruction
vehicle_registration  fact       LG64JAU                  intake_evidence  qdos_instruction
vehicle_mileage       fact       132389                   intake_evidence  qdos_instruction
vehicle_mileage_unit  fact       miles                    intake_evidence  qdos_instruction
instruction_date      fact       2026-08-22               intake_evidence  qdos_instruction
work_provider_code    fact       QDOS                     mail_route       qdos_mail_route
inspection_mode       confirmed  image_based_assessment   provider_setting provider-inspection-mode
inspection_address    confirmed  Image Based Assessment   provider_setting provider-inspection-mode
```

Three things this proves that a test could not:

- The facts are attributed to `PdfContent:uploaded E1492B…` — the **report**, not
  the covering email, so the report-named-fragment rule is selecting the right
  document in production.
- `qdos_instruction` is the policy key on every extracted fact, so the rules are
  running as **policy** rather than as hardcoded parsing.
- `vehicle_mileage 132389` with `vehicle_mileage_unit miles` beside it — the
  digit-guarded Speedo rule, which had no value-bearing corpus instance and was
  only synthetically tested, reads a real report correctly. That was the
  recorded methodology exception in the simplification pass, and it is now
  closed by live evidence.

Compare QDOS26009, created before this work reached the estate: ten fields and
**no mileage at all**.

## Evidence tier

**Observed in production**, on a real operator-forwarded instruction.

## Not covered here

Accident circumstances did not appear on this case, which is expected and
already mapped: the corpus survey recorded that audit letters carry no
circumstances prompt, while the four engineer letters do. This instruction is an
audit. The circumstances rule is pinned by corpus facts for the letters that
contain it.

Which engineering firm issued the report is not attributed — that is
[[INTK-031]]'s subject, not a gap in this ticket.
