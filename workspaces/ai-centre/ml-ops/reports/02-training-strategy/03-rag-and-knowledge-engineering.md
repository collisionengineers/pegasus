# Retrieval-Augmented Generation and Knowledge Engineering

## Executive conclusion

The `Documents` library is primarily a retrieval and knowledge-management asset, not a corpus that should be indiscriminately embedded into model weights.

Manufacturer methods, valuation guidance, salvage rules, SOPs, templates and dispute responses have different owners, authority and effective dates. A permission-aware retrieval-augmented generation system can surface the right material with citations while allowing documents to be corrected, superseded or withdrawn without retraining a model.

## Target capabilities

A Collision Engineers knowledge assistant should answer questions such as:

- Which approved guidance supports a proposed wheel or tyre operation?
- What manufacturer method applies to this model and component?
- What is the current salvage-category definition?
- Which approved response addresses a PAV dispute?
- What does a client-specific SOP require when sending a report?
- Is a source still current?
- Which source and page support a paragraph in a draft?

It should not answer from an undifferentiated semantic search over every file.

## Knowledge domains

Recommended top-level collections:

1. manufacturer repair methods;
2. salvage and total-loss guidance;
3. valuation/PAV guidance;
4. repair-pricing and ABP material;
5. diminution;
6. expert-report requirements;
7. client-specific SOPs;
8. approved query responses;
9. internal training;
10. superseded/archive material.

Collections should have separate permissions and ranking policies.

## Source metadata

Every source should record:

```yaml
knowledge_source:
  source_id:
  title:
  owner_or_publisher:
  document_type:
  domain:
  vehicle_make_or_model_scope:
  jurisdiction:
  effective_from:
  effective_to:
  retrieved_at:
  authority_tier:
  rights_state:
  client_scope:
  confidentiality_class:
  supersedes:
  superseded_by:
  review_owner:
  next_review_date:
```

Missing effective dates should reduce ranking confidence and trigger review.

## Authority tiers

One possible hierarchy:

1. current legislation, Civil Procedure Rules or official regulator guidance;
2. current manufacturer repair method;
3. current contractual/licensed professional guide;
4. approved Collision Engineers policy or SOP;
5. approved precedent response;
6. internal training note;
7. unverified email or third-party opinion;
8. superseded/archive material.

Authority is contextual. A client SOP may govern routing but cannot override an expert's duty or a manufacturer repair requirement.

## Document processing

### Parse structure

Preserve:

- title and headings;
- page number;
- table boundaries;
- lists and steps;
- figure captions;
- vehicle applicability;
- source links;
- workbook sheet and cell references;
- document version.

### Chunk semantically

Chunks should represent complete propositions or procedures, not arbitrary token windows. Examples:

- one repair-method step;
- one salvage category;
- one valuation principle;
- one approved rebuttal point;
- one SOP route.

Use limited overlap and keep parent-document links.

### Generate searchable descriptors

For each chunk, derive reviewed tags:

- component;
- make/model;
- damage/operation;
- question type;
- effective date;
- authority;
- client;
- risk level.

Embedding search should be filtered by these fields before semantic ranking.

## Retrieval flow

```text
User/case question
    → classify domain and intent
    → apply permission/client/date filters
    → keyword + semantic retrieval
    → authority and applicability reranking
    → answer from selected passages
    → cite source, page/section and effective date
    → expose uncertainty or conflicts
```

For high-consequence questions, require more than one supporting source or explicit engineer acknowledgement.

## Handling conflicts

The system must not blend contradictory sources into a smooth answer.

When sources conflict:

- show each source and date;
- identify authority tier;
- explain applicability differences;
- flag the conflict;
- require domain-owner resolution;
- record the resolution as a governed decision.

## Approved response library

The Box Notes and response documents contain useful phrasing for:

- wheel alignment;
- paint PPE/consumables;
- blends;
- salvage;
- PAV;
- repairable versus total-loss disputes;
- diminution.

These should become reviewed response components with:

- approved text;
- prerequisites;
- prohibited uses;
- supporting authority;
- effective date;
- owner;
- example inputs;
- status.

Retrieval should supply the component; a language model may adapt it to case facts without changing the underlying position.

## Rights-aware retrieval

Some documents may be usable operationally but not for model training. Retrieval allows stricter controls:

- store protected documents in a restricted index;
- return short necessary passages;
- enforce client/user permissions;
- log every retrieval;
- prevent bulk export;
- honour licence restrictions;
- withdraw a source immediately.

The rights review remains necessary; retrieval is not an automatic copyright exemption.

## Evaluation

Build a question set from real engineer and admin tasks. Measure:

- retrieval recall at K;
- top-result authority and applicability;
- citation correctness;
- outdated-source rate;
- unsupported-answer rate;
- conflict detection;
- answer usefulness rated by engineers;
- time to find source compared with current practice.

Evaluation questions should include near-matches where the wrong make, year, client or superseded document appears semantically relevant.

## Recommended first release

Limit the first knowledge base to:

- approved SOPs;
- current salvage guidance;
- current PAV guidance;
- a reviewed set of manufacturer methods;
- approved query responses.

Archive or exclude material whose rights, date or authority are unclear until reviewed.

## Conclusion

RAG is the fastest route to operational value from the reference library. Its success depends more on metadata, authority and lifecycle management than on choosing a fashionable embedding model. The system should behave like a cited professional library, not a chatbot trained to remember a folder.

