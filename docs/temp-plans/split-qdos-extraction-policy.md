# Proposed task: split the QDOS policy into generic and QDOS parts

**Delivered on `task/qdos-email-classification`** (operator decision
2026-08-03 folded this into that branch): the generic field engine is
`InstructionFieldEngine`, route evaluation is `QdosMailRoutePolicy`,
`ProcessIntake` takes `IMailRoutePolicy` at the composition root (the
cast-and-throw is gone), and stored policy keys kept their meaning
(`qdos_instruction` v1; `qdos_mail_route` bumped to v3 only for the accepted
three-domain behaviour change). The sections below are the original proposal,
kept for its reasoning until this file's normal post-merge deletion.

## The problem in one paragraph

`QdosInstructionExtractionPolicy` is one class doing two jobs. It works out
which provider sent an email, and it pulls the instruction fields out of that
email. Most of what is inside it is not QDOS-specific at all — it is ordinary
field reading that any provider would need. Only a handful of constants make it
QDOS. Today that is fine, because QDOS is the only provider switched on. It
stops being fine the moment a second provider is added.

## What is actually QDOS-specific

| Part | QDOS-specific? |
| --- | --- |
| Accepted sender domains | Yes |
| Principal code `QDOS` stamped on the draft | Yes |
| The `QDOS` word marker used to confirm content | Yes |
| Policy keys `qdos_instruction`, `qdos_mail_route` | Yes |
| The eleven instruction fields and their labels | No — generic |
| Staff-forward unwrapping via the Collision Engineers domain | No — company-wide |
| Date, mileage, and registration parsing | No — generic |

Four constants and a regular expression sit on top of a provider-neutral
engine.

## Recommendation

Split it, but as its own task, and before the first additional provider is
switched on. Not as part of the MAIL-21/22 classification work.

Reasons to keep it separate:

- One task, one worktree, one PR is the repository rule; this is a distinct
  change with its own risk.
- The classification work does not need the split. The mailbox taxonomy needs a
  new Core owner either way, because [ADR-0006](../adr/0006-provider-neutral-intake-with-contained-qdos-policy.md)
  says this policy must not categorise a mailbox item.
- The split touches `ProcessIntake` and dependency registration, which the
  in-flight `task/image-led-intake` also touches. Doing both at once invites a
  conflict.

Reason not to leave it too long: PCH is next in the provider order, PCH uses an
intermediary as well as sending directly, and there is nowhere to put a second
policy today.

## What the split needs

1. **Separate the two jobs.** Route identification and field extraction become
   two things rather than one class implementing both interfaces. Right now
   `Extract` calls `Evaluate` internally, so extraction cannot run without
   route evaluation.
2. **Add a way to choose a policy.** There is no selection mechanism at all
   today. One policy is registered and `ProcessIntake` casts it to the route
   interface, throwing if the cast fails. Choosing between two providers has no
   home.
3. **Move the generic parts out.** The field definitions, label matching,
   staff-forward unwrapping, and value parsing become shared, provider-neutral
   code.
4. **Leave a thin QDOS policy behind.** Accepted domains, principal code,
   content marker, policy keys.
5. **Keep policy keys and versions stable.** Stored decisions reference
   `qdos_instruction` v1 and `qdos_mail_route` v2. Existing records must keep
   meaning what they meant. A behaviour change needs a version bump, not a
   silent redefinition.
6. **Prove behaviour is unchanged.** The existing intake tests should pass
   without being rewritten to fit the new shape. If a test has to change
   meaning, that is a behaviour change and needs saying out loud.

## What could go wrong

- Turning one class into several can quietly change what counts as a match.
  The mitigation is that this is a refactor with no behaviour change, proven by
  keeping the current tests as they are.
- Guessing at the second provider's rules while splitting. The split must not
  invent PCH behaviour. It only makes room for it.
- Building a general rule engine. [Open decisions](../open-decisions.md)
  explicitly forbids that. Two policies and a way to pick between them is the
  target, not a configurable framework.

## Sequencing

Do this after the MAIL-21/22 classification foundation lands, and before INT-04
activates a second provider. It needs no operator decision of its own, because
it changes structure and not behaviour — but activating any second provider
does.
