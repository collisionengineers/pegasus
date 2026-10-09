# Case record states

Temporary review artifact; see the [page README](../README.md).

The mockup's `state=` presets, each a frame the running host rendered on 9 October 2026:

| Preset | Case | How it was made |
| --- | --- | --- |
| `engineer` | QDOS31001 · With Engineer, read and edit | the visual host's seeded Case; a Repairable outcome and one repair spec line added on the walk |
| `review` | QDOS31001 · Review | `CaseWorkflows.State` set by SQL, Engineer cleared, restored after capture |
| `held` | QDOS31001 · Held, review on 20 May 2031 | SQL, restored after capture |
| `colleague` | QDOS31001 · sarah.patel holds the lease | SQL lease columns with a hashed token, expiry after the host's fixed 2031 clock, restored after capture |
| `notready` | QDOS31004 · Not ready standalone Audit | created on the Create page as an Inspection, `Cases.Type` flipped to `audit` by SQL |
| `views` | QDOS31003 · Inspection + Audit, Audit view, read and edit | created on the Create page; Engineer assigned by SQL; Create audit from the Actions menu |
| `viewsinsp` | QDOS31003 · Inspection view | `?view=inspection` |

Not captured: Query, Complete and closed states (no edit session; the ribbon offers Take over only), and a Triage Case (its own layout, out of scope).
