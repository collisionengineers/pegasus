# Data Governance, Privacy, Confidentiality and Licensing

> **Pegasus custody boundary:** Management's recorded authorisation permits bounded development and
> evaluation of approved source material. It does not permit repository inclusion or bulk import of
> the corpus or complete Box/Outlook archives. Private inputs remain under ignored
> `corpus/ai-centre/`; tracked content is limited to schemas, manifests, synthetic fixtures, and
> generated results.

## Executive conclusion

The named corpus and archives are authorised for model development. They should still be treated as a
governed training corpus rather than an unstructured dump because source role, case separation,
licensing metadata, retention, deletion, and evidential provenance affect valid use and model quality.

Before promoting a dataset or model, Collision Engineers should document the privacy, contractual,
retention, source-role, and technical controls for its intended purpose. The project-level permission
to use and share the named sources is already recorded.

This report is an operational risk assessment, not legal advice.

## Personal-data profile

The sample contains or can contain:

- names and contact details;
- home, business and vehicle-location addresses;
- email addresses and telephone numbers;
- vehicle registrations and VINs;
- accident circumstances;
- claim, client and solicitor references;
- signatures and professional identities;
- images that may show people, premises or location clues;
- financial and valuation information;
- potentially health, injury, criminal-allegation or other special-category material in the wider inbox.

Pseudonymisation reduces risk but does not remove UK GDPR obligations. The ICO distinguishes pseudonymous personal data from genuinely anonymous information. [ICO pseudonymisation guidance](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/data-sharing/anonymisation/pseudonymisation/)

## Purpose and lawful basis

Separate these purposes:

1. data exploration and feasibility;
2. dataset creation;
3. model training and evaluation;
4. live decision support;
5. performance monitoring and retraining;
6. research beyond the original application.

The ICO advises organisations to define processing operations and lawful bases separately and to document them before processing. [ICO AI lawfulness guidance](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/artificial-intelligence/guidance-on-ai-and-data-protection/how-do-we-ensure-lawfulness-in-ai/)

Reusing case material for AI development may be a new purpose. Collision Engineers should perform a compatibility assessment against the purpose for which each class of data was collected and review:

- client agreements;
- privacy notices;
- data-processing agreements;
- controller/processor roles;
- retention commitments;
- data-subject expectations;
- rights to object, erase or correct;
- any professional or court-related duties.

[ICO purpose-limitation guidance](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/data-protection-principles/a-guide-to-the-data-protection-principles/purpose-limitation)

## DPIA

A Data Protection Impact Assessment should cover:

- data sources and flows;
- categories of people and information;
- purpose and necessity;
- model and deployment type;
- access and storage;
- cloud or cross-border processing;
- re-identification and memorisation risk;
- inaccurate or biased outputs;
- individual rights;
- human review;
- retention and deletion;
- incident response;
- residual risk and approval.

The DPIA should be updated when a pilot moves from anonymised research to live case support. [ICO DPIA guidance](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/accountability-and-governance/data-protection-impact-assessments-dpias/how-do-we-do-a-dpia/)

## Data minimisation

Build separate datasets by task.

Examples:

- A damage detector normally does not need names, email bodies, addresses or full registrations.
- A view-quality classifier does not need report totals.
- A report-style model does not need inbound third-party signatures.
- A case-router may need sender organisation but not claimant identity.
- A VRM OCR model needs plate regions but should be isolated from general vision training.

The ICO states that possible future usefulness is not enough to justify collecting or retaining unnecessary personal data. [ICO AI data-minimisation guidance](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/artificial-intelligence/guidance-on-ai-and-data-protection/how-should-we-assess-security-and-data-minimisation-in-ai/)

## Confidentiality and expert material

Case material may include:

- solicitor instructions;
- material prepared for litigation;
- third-party correspondence;
- confidential commercial pricing;
- unpublished professional opinions;
- court-facing expert reports.

The professional duty remains with the engineer. Civil Procedure Rule 35.3 states that an expert's duty to help the court overrides obligations to the instructing party. Practice Direction 35 requires clarity about material relied upon, qualifications and the expert's own opinion.

- [Civil Procedure Rules Part 35](https://www.justice.gov.uk/courts/procedure-rules/civil/rules/part35)
- [Practice Direction 35](https://www.justice.gov.uk/courts/procedure-rules/civil/rules/part35/pd_part35)

AI use should therefore preserve:

- the evidence and references used;
- changes made by the engineer;
- model version and configuration;
- generated-versus-human-authored boundaries;
- final approval;
- the ability to reproduce the draft.

## Copyright and database rights

Rights review is required for:

- photographs supplied by clients, repairers or other parties;
- manufacturer repair procedures;
- Audatex-derived reports or estimates;
- Glass's, CAP, Percayso and similar valuation data;
- ABP guides;
- Financial Ombudsman extracts;
- training materials and videos;
- templates and third-party correspondence;
- spreadsheets and price lists.

Possession, subscription access or permission to use a source for an assessment does not automatically confer the right to reproduce it in a training dataset. The UK government's 2026 Copyright and AI report notes that AI training involves making copies and that licensing can provide the necessary rights. [UK Copyright and AI report](https://www.gov.uk/government/publications/report-and-impact-assessment-on-copyright-and-artificial-intelligence/report-on-copyright-and-artificial-intelligence)

Recommended rights states:

- approved for training;
- approved for retrieval only;
- approved for operational reference only;
- approved only in aggregated analytics;
- pending review;
- prohibited.

## Vendor and cloud controls

Raw case data should not be uploaded to public dataset or model services.

Any external processor should be reviewed for:

- contractual role and data-processing terms;
- whether customer data trains provider models;
- storage region and transfers;
- encryption;
- sub-processors;
- logging and human access;
- retention and deletion;
- incident notification;
- ability to export models and artifacts;
- private networking and access control.

Temporary rented compute can be acceptable if the data and artifacts remain private, contractual controls are adequate, and the promoted model is exported into Collision Engineers-controlled storage.

## Retention and deletion

Maintain separate schedules for:

- raw case evidence;
- operational extracts;
- annotation datasets;
- model-training snapshots;
- evaluation sets;
- model checkpoints;
- inference logs;
- human feedback.

Deletion must propagate through future dataset builds. If a trained model presents a material memorisation risk, the governance plan must define whether retraining, unlearning or retirement is required.

## Governance roles

Recommended ownership:

- **Data owner:** approves purpose and access.
- **Privacy/legal owner:** lawful basis, DPIA, contracts and rights.
- **Domain owner:** engineering taxonomy and professional acceptability.
- **Dataset steward:** provenance, quality, splits and releases.
- **Model owner:** training, evaluation and limitations.
- **Deployment owner:** security, monitoring and rollback.
- **Independent approver:** production release decision.

## Minimum approval gate

No raw-data model training should begin until:

- the purpose is defined;
- lawful basis and compatibility are documented;
- the DPIA is approved;
- client/vendor contract implications are reviewed;
- each data family has a rights state;
- a minimised dataset specification exists;
- storage and access controls are implemented;
- deletion and incident procedures are tested.

## Conclusion

Governance does not make the opportunity impractical. It determines which high-value uses can be pursued safely. A properly separated approach—retrieval for licensed knowledge, pseudonymised case features for analytics, approved images for vision, and Collision Engineers-authored text for style—will usually be more useful as well as more defensible.

