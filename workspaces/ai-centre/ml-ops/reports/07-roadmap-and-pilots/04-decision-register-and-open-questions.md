# Decision Register and Open Questions

## Executive conclusion

The sample is sufficient to conclude that the wider corpus can support useful AI systems. It is not sufficient to authorise training or to determine production performance. The programme needs explicit decisions about rights, task boundaries, labels, infrastructure and professional accountability.

This register separates decisions supported by the current analysis from questions requiring evidence or organisational authority.

## Decisions supported now

| ID | Decision | Rationale |
|---|---|---|
| D-001 | Treat the case, not a single image, as the primary learning unit. | Instructions, evidence, reports, amendments and correspondence are interdependent. |
| D-002 | Define the service as remote, image/document-based assessment. | Collision Engineers does not conduct physical inspections; limitations must be explicit. |
| D-003 | Do not train a general language or vision foundation model from scratch. | Thousands of cases are valuable for domain adaptation but far too small for competitive general pretraining. |
| D-004 | Use pretrained models and task-specific fine-tuning. | This is the most data- and cost-efficient route for OCR, classification, detection and constrained drafting. |
| D-005 | Use RAG for changing domain knowledge. | Methods, values, rules and guidance require dates, provenance and updates. |
| D-006 | Keep calculations deterministic. | VAT, totals, thresholds and current values must be reproducible. |
| D-007 | Require evidence grounding and abstention. | Remote evidence is incomplete; unsupported confidence is a primary risk. |
| D-008 | Keep final professional conclusions and issuance under engineer control. | Models assist but do not become the independent expert. |
| D-009 | Separate source roles. | Client, repairer and opposing-party text must not be learned as CE ground truth. |
| D-010 | Split datasets by case and time. | Image/message splits would leak duplicates and later evidence. |
| D-011 | Build QA and case assembly before ambitious multimodal assessment. | They provide immediate value and create cleaner labels. |
| D-012 | Retain locally controlled, portable artifacts. | Checkpoints/adapters, manifests, hashes, evaluation and offline smoke tests protect reproducibility and independence. |

## Questions requiring organisational decisions

### Purpose and product

- Is the first objective internal productivity, quality assurance, a client-facing product or model/data licensing?
- Which report types and instructing contexts are in initial scope?
- Which outputs are expressly prohibited?
- Does the product serve only Collision Engineers staff or external assessors?
- Is guided capture part of the service even though CE itself performs no physical inspection?

### Data rights and privacy

- Who owns or licenses the images, reports, instructions and estimates?
- What do client agreements say about secondary use and model training?
- Which reference documents permit indexing, transformation or training?
- What is the lawful basis for each purpose?
- Which correspondence is privileged, unrelated or out of scope?
- What retention/deletion obligations must propagate to derivatives and datasets?
- Are there client-specific segregation or residency requirements?
- What transparency is required for affected individuals and clients?

### Corpus reality

- How many unique cases, images, reports and messages exist after deduplication?
- What date range and vehicle/source distribution do they cover?
- How many cases have reliable final approval and version history?
- How often is later evidence available?
- How many include estimates, PAV, salvage and outcome?
- Are repairable/total-loss and rare conditions sufficiently represented?
- Can images be traced reliably to the report finding and evidence cutoff?

### Label quality

- Is there an agreed component, damage, operation and view taxonomy?
- Can engineers adjudicate ambiguous labels?
- Are reasons for amendments recoverable?
- Which report version is the approved target?
- Can hidden damage be separated from visible-at-cutoff evidence?
- How much annotation capacity is available?
- What inter-engineer agreement is acceptable?

### External data

- Which vehicle, valuation, repair-method, parts and salvage providers are authorised?
- Do the licences permit caching, model features and derived analytics?
- How are effective dates and supersession recorded?
- What happens when a current source is unavailable?
- Which external facts must be independently verified?

### Professional and legal governance

- Which reports may become expert evidence?
- What review and declaration wording is required?
- Who is authorised to approve each high-consequence output?
- How will AI assistance be recorded or disclosed where applicable?
- What constitutes a material amendment?
- What client pressure or conflict signals require escalation?

### Infrastructure and security

- Which providers and deployment locations are permitted?
- Is local inference required for images, text or both?
- What latency and throughput are needed?
- How are mailbox and case-system permissions enforced in retrieval?
- What is the acceptable recovery point and recovery time?
- Who can export datasets or model artifacts?
- How will prompt injection and untrusted attachments be contained?

### Success and economics

- What is the current time and defect baseline?
- Which quality metric is non-negotiable?
- What false-ready or unsupported-finding rates are acceptable?
- What annotation and engineer-review budget is available?
- What infrastructure cost per case is acceptable?
- What evidence is required to move from shadow to production?

## Decisions to defer until measurement

Do not choose these prematurely:

- a single cloud or model vendor;
- an end-to-end VLM versus pipeline architecture;
- the exact vision architecture;
- whether style fine-tuning is necessary;
- production thresholds;
- precise dataset-size commitments;
- a central estimate for project cost or duration.

Run baselines and pilots first. The correct choices depend on measured corpus quality, task frequency, provider constraints and risk thresholds.

## Proposed decision process

For each material decision, record:

```yaml
decision:
  id:
  question:
  owner:
  options:
  evidence_required:
  privacy_security_impact:
  professional_impact:
  cost_operational_impact:
  decision:
  rationale:
  approved_by:
  date:
  review_trigger:
```

Maintain the register with the dataset, model and source versions. Revisit a decision when its trigger occurs rather than silently changing practice.

## Immediate evidence-gathering actions

1. Inventory a statistically useful sample of the wider archive.
2. Review contracts and data-source licences.
3. Reconstruct 100–200 complete case timelines.
4. Measure duplicates, parse failures and source-role accuracy.
5. Establish current workflow/quality baselines.
6. Ask engineers to adjudicate a small taxonomy and challenge set.
7. Select one repairable and one total-loss report type for detailed mapping.
8. Confirm the permitted deployment and provider boundary.
9. Approve Pilot A and Pilot B stop gates.

## Go/no-go criteria for training

Training may proceed for a task only when:

- purpose and use are approved;
- data rights and privacy controls are documented;
- inputs and targets are defined;
- source roles and approval states are known;
- case/time splits are leakage-tested;
- labels meet an agreed quality threshold;
- evaluation and stop criteria exist;
- the human owner and deployed boundary are named;
- artifacts can be reproduced and exported.

Failure on one task does not invalidate other opportunities. For example, weak damage masks would not prevent QA, retrieval or report drafting.

## Conclusion

The strategic answer is already clear: the data is valuable for AI, fine-tuning is realistic, and general foundation training from scratch is not. The remaining uncertainty is operational—what rights exist, how clean the wider corpus is, which task delivers the first measurable value and what risk boundary the organisation will approve.
