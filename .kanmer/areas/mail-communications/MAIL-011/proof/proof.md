# Proof — production, 2026-08-23

Tier: **production**. Release 26 (`7d6a948a`), revision
`pegasus-prod-web-252ow37gij--7d6a948a2f34`, against a database wiped clean by
[[PLAT-040]] — so every row below was written by the new code.

The operator forwarded three QDOS messages at 14:57Z. One of them is the same
message that became U34 the day before: *"Fw: Engineer Triage - Our Claim
Reference 47939/1, Vehicle registration GD65TVY"*, forwarded by
`desk@collisionengineers.co.uk`, with a `Cc:` line in its forwarded header.

Read from `RetainedMailboxMessages` joined to `IntakeMailRouteDecisions`:

| Transport sender | Cc line | Route | Effective sender |
| --- | --- | --- | --- |
| `desk@collisionengineers.co.uk` | no Cc | accepted | `jfleming@qdosassist.co.uk` |
| `desk@collisionengineers.co.uk` | no Cc | accepted | `lbirchenough@qdosassist.co.uk` |
| `desk@collisionengineers.co.uk` | **has Cc** | **accepted** | **`randerson@qdosassist.co.uk`** |

The third row is the fix. Yesterday that exact message produced
`originalIdentities.Length == 0`, disposition `NeedsSorting`, and unidentified
item U34; the inbox rendered it as from "Desk" and its preview showed our own
signature. Today it routes **accepted**, names the real original sender, and no
Unidentified item exists at all (`UnidentifiedItems` is empty).

All four symptoms in the ticket body are gone:

1. It was identified rather than becoming an Unidentified item.
2. The effective sender is the original, not the forwarding desk.
3. The forwarded header is found, so the preview boundary resolves.
4. Two further Cc-less forwards routed correctly in the same batch, so the
   widening did not cost the ordinary case anything.

## What this proof does not cover

Being routed is not being *acted on*. The same message then failed automatic
allocation with `case_type_unavailable` and produced no Triage — a separate
defect filed as [[INTK-031]]. This ticket's scope was reading the forwarded
header, and that is proved.
