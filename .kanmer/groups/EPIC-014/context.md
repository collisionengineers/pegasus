# Pegasus v1 remediation context

The operator's 7 September 2026 request in this session is the current scope
and authorization. It supersedes earlier pack or group instructions to leave
PRs unmerged, require Linux only, retain a customer ownership hierarchy, or
exclude otherwise requested EPIC-011/012 work.

## Outcome and scope

An incoming or forwarded email can progress through principal identification,
classification and extraction to a Case, Triage, or Image-initiated case,
link to an existing Case automatically when unambiguous or manually when
needed, become ready by completeness, pass to an Engineer, use Glass's repair
estimating, generate report/fee note and send through staff action.

Reconcile every original Astra ticket and all Preparing-or-later work and
EPIC-011/012. Retire proven superseded tickets with evidence and links; retain
credential blocks explicitly. In-app AI assistant is excluded. Principal is
one customer identity: no operator-facing organisation owning principals.
Wipes must exclude old emails, including after delta reset or notification.

## Authority and implementation

Use existing Core commands, adapters and production routes. Plan and implement
bounded corrections, update affected callers and canonical documentation,
remove obsolete paths. No speculative architecture or new test framework.
The current repo integration branch is dev; release branch is main. Operator
grants full merge/deployment authority for this remediation and Azure Document
Intelligence provisioning/implementation. Target the existing Pegasus
production estate recorded in the release skill/runbook after live read-back.
Only test email recipient: digital@collisionengineers.co.uk; requested test
principal: pegasustest. Do not send test mail to any other address.

## Verification

One heavy verification owner per host: /root. Implementation lanes may request
compiler feedback; coordinate before building or testing. Use focused existing
tests for the changed failure modes, run the required final CI once per
reviewed head, and reuse exact-SHA evidence. No stress/soak/capacity tests.
A passing test is only evidence for its asserted behavior; SQL role and live
caller tests must prove the actual path, not duplicate permission lists.

## Baseline and preserved work

Remote dev/PR675 head: 3da60bd0c270111d5168dc17246dc831882108ea.
Remote main: 32f8679d3695e0dcab8f310a1c20f8b129d20190.
Shared checkout is stale at 3284f93f with operator edits; preserve it.
Historic EPIC-011/012 automation records are retained, not resumed or rewritten.
The current pack is local and ignored. Supplied originals are reference evidence;
current plans live under pegasus_pack/current and tracked pipeline docs in Kanmer.
