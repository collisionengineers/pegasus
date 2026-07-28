# Permanent and conditional boundaries

Primary contract: `P-BOUND`

Allocation is owned by the [capability inventory](capabilities.md). `Not
planned` means no backlog, route, caller, schema, adapter, feature flag,
account, resource, release gate, placeholder or dormant seam. Conditional
`Later` outcomes have no caller or implementation plan until a direct decision
promotes them.

## Identity and external access boundaries

External/customer accounts, public registration and staff MFA are `Not
planned`. `INT-31` is a narrow exception to external access, not identity: a
temporary, revocable, request-scoped link permits only the bounded upload and
immediate result. It exposes no case/request state and creates no account,
profile or public-registration path.

## Communications boundaries

General authenticated staff compose/reply/forward/send is `Later`/unallocated
under `MAIL-12`; it is no longer a permanent exclusion. It does not weaken the
separate `MAIL-17` targeted report-send transaction, whose destinations,
principal preferences, idempotency, Box filing, completion and recovery require
their own accepted caller.

## Operator interface boundaries

A responsive/mobile staff interface is `Not planned`. Desktop zoom and
constrained-width reflow do not create a mobile product.

## Document governance boundaries

Automated malware scanning, redaction, digital signatures, automated
retention/deletion, legal hold, subject-access/correction/export/erasure and a
dedicated DPIA/compliance workflow are `Not planned`. Existing custody and
security work must not infer those applications or pipelines.

## Azure release and resilience boundaries

GitHub Actions deployment, staging, slots/S1, private networking, zone
redundancy, multi-region failover and quarterly restore exercises are `Not
planned`. Validation CI does not deploy.

## Product and environment boundaries

Predecessor import/retention/code reuse, SMS, Teams, a persistent external
case/customer portal, independent Engineer accounts, external-party accounts
and separate QA/UAT/training environments are `Not planned`. `BND-06` excludes
the persistent portal only and explicitly permits the `INT-31` request-scoped
upload form.

## Conditional activation gates

Collision Engineers guided mobile capture, Tractable/Ravin integration and a
custom application domain remain conditional `Later`/unallocated. They create
no vendor evaluation, schema, resource, identity, transfer or caller until a
direct decision settles the exact outcome, ownership, security, cost, failure,
recovery and acceptance route.

## Evidence boundary

This document states intended product boundaries. It does not prove runtime
denial or deployment state. Changing a `Not planned` boundary requires explicit
product authority; promoting a conditional outcome requires one focused
activation change. Neither route authorizes an external write, cloud operation,
data deletion or implementation by itself.
