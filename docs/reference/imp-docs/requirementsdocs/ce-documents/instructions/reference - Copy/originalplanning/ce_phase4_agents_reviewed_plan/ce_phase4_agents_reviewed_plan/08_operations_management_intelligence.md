# Operations and Management Intelligence

## Objective

Replace the “Continuous Learning Agent” idea with measurable operations intelligence, dashboards and reviewed process recommendations.

## Correct framing

This should begin as analytics, not autonomous learning. The system should not modify workflows, prompts or provider rules on its own. It should surface patterns and require management/admin approval for process changes.

## Metrics to track

### Intake

- new instructions per day/week;
- source mailbox/provider;
- attachment mix;
- unknown/unclassified document rate;
- duplicate/forward rate.

### Evidence completeness

- cases awaiting instruction;
- cases awaiting images;
- cases awaiting estimate;
- cases awaiting mileage/location/valuation;
- average time in each state;
- first-chase and escalation volumes.

### Provider performance

- missing-image frequency;
- missing-instruction frequency;
- time to respond to chasers;
- percentage needing manual review;
- common data-quality issues;
- report return/fee-note rules by provider.

### Engineering throughput

- ready-for-engineer queue;
- engineer allocation/time-to-review;
- overdue reports;
- incomplete packs;
- repeat query rates.

### Automation quality

- extraction confidence by provider/template;
- AI draft edit distance;
- false-positive matches;
- false-negative matches;
- failed API submissions;
- manual override rate.

## AI role

AI can:

- produce a weekly narrative summary;
- explain unusual changes in metrics;
- suggest process changes;
- summarise provider-specific issues;
- generate management briefing drafts.

AI cannot:

- automatically change provider rules;
- reduce review thresholds without approval;
- make staffing decisions;
- use sensitive case data for model training without governance;
- produce client-facing performance claims without validation.

## Dashboard views

- Today’s actions.
- Awaiting images/instruction/estimate.
- Ready for EVA.
- Ready for engineer.
- Manual escalation.
- Provider data-quality league table.
- Extraction/AI quality monitor.
- Overdue reports and bottlenecks.

## Acceptance tests

- Dashboard reproduces current holding-pen visibility before replacing it.
- Every management metric is traceable to case records/events.
- AI summary includes source metrics and does not invent causes.
- Suggested process changes go into a decision queue, not directly into production rules.
