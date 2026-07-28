# Analytics, Benchmarking and Product Insights

## Executive conclusion

The unified case graph can produce valuable operational, quality and product intelligence even without training a predictive model. It can show where evidence is incomplete, where reports are amended, which queries recur and which workflow stages create delay.

Analytics must be designed to improve evidence quality and consistency, not to pressure engineers toward a preferred outcome or create misleading individual performance rankings.

## Core operational dashboard

Measure:

- new instructions by date and type;
- evidence-complete versus waiting;
- age by workflow stage;
- contractual deadlines at risk;
- engineer and audit queue capacity;
- report turnaround;
- causes of delay;
- amendments and reopened cases;
- query volume and resolution time.

Separate external waiting time from internal handling time so teams are not judged for evidence they do not control.

## Remote-evidence dashboard

Measure:

- images per case;
- expected view coverage;
- blur, darkness, glare and obstruction rates;
- evidence requests per case;
- time to sufficient evidence;
- source/channel quality;
- duplicate rate;
- wrong-vehicle or identity-conflict events;
- correlation between evidence quality and amendment/supplement.

These findings can inform sender guidance and capture workflows. They should not be used to claim that a source or individual is unreliable without contextual review.

## Engineering and report-quality dashboard

Measure:

- supported finding and citation rates;
- estimate QA findings;
- valuation source freshness;
- report audit changes by reason;
- original-to-amended changes;
- recurring omitted sections;
- accepted model suggestions and rejections;
- material defect escape rate;
- inter-engineer disagreement on adjudicated samples.

Raw amendment counts are not a quality score. Distinguish new evidence, changed instruction, market update, administrative correction and professional correction.

## Commercial and product insight

Aggregated data can reveal:

- case types with high administrative effort;
- common technical query categories;
- clients or sources needing better evidence instructions;
- vehicle segments requiring new domain material;
- opportunities for self-service evidence upload;
- value of early total-loss triage;
- demand for valuation, QA or correspondence services;
- potential anonymised industry benchmarking products.

Any external benchmarking product requires a separate legal, contractual and re-identification review. Small cohorts or distinctive cases can remain identifiable even after obvious fields are removed.

## Model-performance dashboard

For every deployed model, show:

- version and deployment date;
- task and allowed use;
- volume and coverage;
- abstention;
- confidence/calibration;
- performance on recent audited samples;
- error slices;
- engineer acceptance/edit/rejection;
- latency and cost;
- drift alerts;
- open incidents;
- rollback status.

Do not combine unlike tasks into a single “AI accuracy” score.

## Independence and fairness analytics

Monitor whether engineering outcomes or model errors vary unexpectedly by:

- instructing source;
- evidence source;
- engineer;
- vehicle segment;
- value band;
- geography where legitimately held;
- report type.

The analysis should ask whether differences are explained by evidence, vehicle mix or process. Client identity must not become a hidden outcome feature. Restrict access to sensitive breakdowns and use minimum cohort sizes.

## Benchmarking design

Use three comparison levels:

1. **Internal baseline:** current process before the tool.
2. **Controlled pilot:** matched or randomised workflow where practical.
3. **Longitudinal monitoring:** performance after rollout, segmented by model version and calendar period.

Define metrics and exclusions before viewing the result. Include quality and safety alongside time and cost.

Example balanced scorecard:

| Dimension | Example measures |
|---|---|
| Evidence | completeness, false-ready rate, request cycles |
| Quality | material defects, unsupported findings, amendments by reason |
| Service | turnaround, overdue rate, query resolution |
| Human factors | review time, override rate, trust and usability |
| Model | calibration, abstention, segment performance |
| Governance | leakage, access incidents, stale sources, audit completion |
| Economics | cost per case, avoided rework, infrastructure cost |

## Causal caution

Dashboard correlations do not establish causation. For example:

- high amendments may reflect more later evidence;
- longer cases may be more complex;
- high rejection may indicate healthy review rather than poor performance;
- fast approval may indicate either excellent output or automation bias.

Product decisions should combine quantitative data, sampled case review and engineer interviews.

## Data quality and reproducibility

- Define each metric and owner.
- Version transformations and filters.
- Preserve source lineage.
- Exclude test/training leakage from model comparisons.
- Record late-arriving events.
- Make report periods reproducible.
- Display missing data and cohort size.
- Limit access to personal or commercially sensitive views.

## Recommended first analyses

Before predictive deployment:

1. build case/evidence completeness statistics;
2. quantify duplicate and unparseable artifacts;
3. map report amendment reasons;
4. measure time by workflow stage;
5. identify the most frequent query classes;
6. establish current QA error and report-preparation baselines;
7. compare evidence source/channel quality.

These analyses directly inform which pilot has the strongest value and whether the data is ready.

## Conclusion

The corpus can become a management and product-intelligence asset as well as training material. The right analytics make process constraints and evidence quality visible while protecting independent professional judgement and avoiding simplistic staff or client rankings.
