# Security, Deployment and Local Model Ownership

## Executive conclusion

The case corpus contains vehicle identifiers, personal data, correspondence, legal material, commercial terms and potentially sensitive claims information. AI deployment should therefore use privacy-by-design, least privilege, strict tenant/case isolation and auditable processing.

Collision Engineers should retain usable ownership and portability of every task-specific model and dataset artifact it funds. A hosted training or inference provider may be convenient, but it must not become the only place from which the model can run, be evaluated or be recovered.

## Data zones

### Raw restricted zone

Contains original emails, files, images and metadata. Access is limited to authorised ingestion, case workers and specifically approved investigation.

### Curated case zone

Contains canonical case records, source-role labels, hashes, text extraction and redacted/minimised derivatives. This is the normal application boundary.

### Training zone

Contains only approved task-specific examples, with documented purpose, rights status, split membership and transformation lineage. It should not be a copy of the entire inbox.

### Evaluation zone

Contains frozen holdouts and challenge sets inaccessible to routine training jobs.

### Model registry

Contains signed artifact bundles, evaluation results, approvals and deployment status.

Separating zones reduces the blast radius of mistakes and makes purpose limitation enforceable.

## Access and identity

- single sign-on or centrally controlled identities;
- role-based least privilege;
- case/client segregation where required;
- separate service identities for ingestion, training and inference;
- short-lived credentials;
- protected administrator actions;
- periodic access reviews;
- immediate offboarding;
- audit logs for raw-data access, exports and model promotion.

Do not place mailbox credentials, API keys or source-system passwords in prompts, training files or code repositories.

## Encryption and network controls

- encrypt data in transit and at rest;
- manage keys separately from stored data;
- restrict training and inference egress;
- use private endpoints or controlled gateways where appropriate;
- malware-scan and sandbox untrusted attachments;
- restrict downloadable raw artifacts;
- prevent model services from calling arbitrary tools or URLs;
- log authorised external lookups without leaking full case content.

The exact on-premises, private-cloud or hybrid design should follow a threat model and operational needs rather than a blanket assumption that one location is inherently safe.

## Provider due diligence

Before sending live case material to any hosted model or training service, confirm:

- data-use and training terms;
- retention and deletion behaviour;
- processing locations and subprocessors;
- contractual confidentiality;
- incident notification;
- encryption and access controls;
- logging options;
- ability to disable provider-side data improvement;
- export formats;
- service discontinuation and migration path;
- client-specific restrictions.

Minimise the prompt or image content sent to the provider. A provider approval does not remove the need for case-level access control.

## Threats specific to this system

- cross-case or cross-client retrieval;
- prompt injection hidden in emails or documents;
- malicious or malformed attachments;
- model extraction or checkpoint theft;
- training-data poisoning;
- memorisation and regurgitation of identifiers;
- membership inference;
- unauthorised model or dataset export;
- compromised repair/valuation reference source;
- incorrect tool execution by an agent;
- shadow AI use outside the approved platform.

Retrieved email and document text should be treated as untrusted content, not as system instructions. Tools need allow-lists, bounded parameters and human approval for external effects.

## Local-owned artifact bundle

Every promoted model should have an exportable bundle containing:

```text
model/
  training checkpoint or adapter
  portable inference export, such as ONNX where applicable
  tokenizer, processor and label map
  model card
  dependency and base-model lock
  configuration
data/
  dataset manifest and lineage
  split hashes
  taxonomy version
evaluation/
  full metrics and challenge-set results
  calibration thresholds
  offline smoke-test inputs and expected outputs
governance/
  approvals
  licence and rights record
  known limitations
  rollback instructions
checksums.txt
```

Where licensing prevents redistribution of a base model, record the immutable source identifier and retain the firm's adapter, configuration and reproducible build process.

## Deployment pattern

A defensible initial architecture is:

1. private ingestion and canonical case store;
2. deterministic extraction and QA services;
3. permission-filtered retrieval;
4. task-specific local or privately hosted models;
5. an application API enforcing case scope;
6. an engineer review interface;
7. append-only audit events;
8. monitored export/report service.

Models should not have direct unrestricted access to the mailbox, file share or final-send capability.

## Release, rollback and incident handling

For each release:

- freeze and identify the dataset;
- reproduce training;
- run offline and security tests;
- approve documented thresholds;
- deploy to shadow or canary traffic;
- monitor errors and drift;
- retain the previous working version;
- support immediate rollback.

Incident plans should cover wrong-case disclosure, harmful professional output, compromised credentials, poisoned data and provider outage. Preserve relevant evidence, contain access and inform the designated privacy/security owner.

## Evaluation and assurance

- cross-case leakage red-team tests;
- prompt-injection tests;
- identifier regurgitation tests;
- role/access-control tests;
- dependency and artifact integrity;
- offline reproducibility;
- backup restore;
- provider outage/failover;
- latency and capacity;
- deletion propagation;
- audit-log completeness.

Security review is required after major changes to tools, retrieval sources, model providers or data scope.

## Conclusion

The data can be used securely, but only through a deliberately bounded platform. Portable artifact bundles, isolated data zones and reproducible evaluation protect both confidentiality and the firm's ability to change infrastructure without losing its model investment.
