# Permanent and conditional boundaries

Primary contract: `P-BOUND`

Features: `ACC-12`; `ACC-13`; `ACC-14`; `MAIL-12`; `UI-12`; `DOC-09`; `DOC-10`; `DOC-11`; `DOC-12`; `DOC-13`; `DOC-14`; `DOC-15`; `OPS-12`; `OPS-15`; `OPS-16`; `OPS-17`; `OPS-18`; `OPS-19`; `OPS-21`; `BND-01`; `BND-02`; `BND-03`; `BND-04`; `BND-05`; `BND-06`; `BND-07`; `BND-08`; `BND-09`; `BND-10`; `BND-11` — **Never**; `EXT-16`; `EXT-17`; `EXT-19` — **Conditional / Unclear**.
Status: **Current non-implementation product boundary.** The [capability inventory](capabilities.md) owns allocation.

Never means no backlog, route, caller, schema, adapter, feature flag, account, resource, release gate, placeholder or dormant seam is created. Conditional means no current caller or implementation plan exists; a future direct decision is required before a focused activation plan can be written.

## Identity and external access boundaries

External/customer accounts, public registration and staff MFA are Never. Existing staff identity plans cannot use these rows to introduce public/external identity paths or MFA work. A rejected/unsupported request fails closed at its existing caller boundary; no account/role schema or provider integration is reserved.

## Communications boundaries

Composing, replying, forwarding and sending email in the app is Never. This does not change the separately allocated automation contracts for chasers/reports; they require their own approved callers and do not create a general sender or mailbox-composer surface.

## Operator interface boundaries

A responsive/mobile staff interface is Never. The approved operator UI route remains desktop-first; no responsive/mobile route, design system branch or test matrix is created.

## Document governance boundaries

Automated malware scanning, redaction, digital signatures, automated retention/deletion, legal hold, subject-access/correction/export/erasure workflows and dedicated DPIA/compliance workflow are Never. Existing custody/security work must not infer scanners, deletion pipelines, legal/compliance applications or data-subject portals from these rows.

## Azure release and resilience boundaries

GitHub Actions OIDC deployment, staging, slots/S1, private networking, zone redundancy, multi-region failover and quarterly restore exercises are Never. Existing release/recovery evidence remains scoped to accepted product architecture; it does not reserve infrastructure, identities, environments or operational schedules for these excluded capabilities.

## Product and environment boundaries

Predecessor import, predecessor retention after cutover, predecessor-code reuse, SMS, Teams, customer/claimant portal, independent Engineer accounts, external party accounts, separate QA/UAT/training environments are Never. The boundary does not delete or alter predecessor material; it simply authorises no v2 implementation path for it.

## Guided capture activation gates

Collision Engineers guided mobile capture and Tractable/Ravin integration are Conditional / Unclear. Neither creates a mobile feature, vendor evaluation, account, capture API, source adapter, schema, flag, data transfer nor caller now. A future direct decision must settle the business outcome, ownership, mobile/channel scope, vendor/data/privacy/licence/cost/security posture, stable source/case identities, failure/review behavior and intended caller before a focused activation plan is allowed.

## Custom domain activation gate

A Collision Engineers custom application domain is Conditional / Unclear. No DNS, certificate, hostname, Azure setting, identity redirect URI, deployment configuration or caller is created. A future direct decision must identify the exact domain/owner, identity/certificate/security, migration/rollback and hosting scope before any infrastructure plan or approval request.

## Evidence and recovery boundary

The proof here is planning-only: source-map the IDs to these sections and independently confirm their absence from active implementation routes. It does not prove a runtime denial, deployment state or permanent product outcome. If a later direct decision changes a Conditional item, update the maturity-map allocation and create one bounded activation plan; Never rows require an explicit authority change before any plan can exist. No external write, cloud action, data deletion or implementation is authorised by this contract.
