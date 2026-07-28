# Valuation, Vehicle Data and Uplift Evidence

## Objective

Gather vehicle and valuation evidence efficiently while keeping final valuation judgement under human/engineer control.

## Corrected concept

Rename the original “Valuation and Uplift Agent” to **Valuation Evidence Assistant**.

It should not automatically decide the final value. It should:

- enrich vehicle facts;
- retrieve approved valuation evidence;
- flag missing/uncertain mileage/specification data;
- calculate documented policy adjustments where approved;
- draft a valuation evidence note;
- require reviewer acceptance/modification/rejection.

## Data sources and roles

| Source | Suitable use | Caveat |
|---|---|---|
| DVLA Vehicle Enquiry Service | Make/model/year/fuel/colour/tax-style vehicle facts from VRM | Not a valuation source. Requires API key. |
| DVSA MOT History API | MOT dates, mileage readings, failures/advisories | Authorised access required; use as evidence, not proof of current mileage. |
| Percayso/current internal process | Current mileage estimate fallback mentioned in EVA process | Confirm licensing, API access and audit requirements. |
| Glass’s / Autovista | Valuation/specification evidence | Commercial access/licence required. |
| Cazana | Market-history/valuation evidence | Commercial access/licence required. |
| cap hpi | Vehicle data, DVLA/SMMT and valuations | Commercial access/licence required. |
| Autotrader/market listings | Market comparison evidence | Confirm data rights/API terms before scraping or automation. |

## Uplift logic

Do not build “recommended uplift” as black-box AI. Use a transparent rule set:

```yaml
uplift_policy:
  provider_code: example
  trigger: provider_requests_uplift
  calculation: percentage | fixed_amount | manual_review_only
  percentage: 10
  cap_amount: null
  evidence_required:
    - valuation_source
    - mileage_source
    - vehicle_spec_confirmed
  approval_required: true
```

## Output fields

```yaml
valuation_evidence:
  case_id: string
  vehicle_reg: string
  mileage_used: integer | null
  mileage_source: dashboard_photo | MOT | Percayso | estimate | instruction | unknown
  make_model_source: DVLA | instruction | estimate | other
  valuation_sources:
    - source_name: Glasss | Autovista | Cazana | cap_hpi | other
      lookup_at: datetime
      raw_result_id: string
      market_value: decimal | null
      retail_value: decimal | null
      trade_value: decimal | null
      notes: string
  proposed_adjustments:
    - type: mileage | condition | provider_uplift | salvage | other
      amount: decimal
      method: string
      evidence: string
  draft_summary: string
  reviewer_decision: accepted | modified | rejected | pending
  final_approved_by: user_id | null
```

## AI role

AI can:

- summarise valuation sources;
- explain discrepancies;
- draft a valuation evidence paragraph;
- identify missing spec/mileage inputs;
- compare valuation outputs.

AI cannot:

- determine final valuation;
- invent market comparables;
- override engineer judgement;
- scrape services where terms/API access do not permit it;
- apply a hidden “uplift” policy.

## Acceptance tests

- DVLA lookup fills factual vehicle fields but leaves valuation blank.
- MOT mileage history is marked as historical/estimated evidence, not verified current mileage.
- If no approved valuation API is configured, case is flagged for manual valuation rather than producing an unsupported figure.
- If provider uplift is requested but no policy exists, system asks for manual review.
- Reviewers can accept, edit or reject valuation evidence before it enters a report or EVA/Sentry payload.
