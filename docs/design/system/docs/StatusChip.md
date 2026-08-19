---
category: Status
---

Pill-shaped state chip: the state text plus a Lucide icon on a tinted ground. It is the single place a business or query state chooses its visual treatment — pass the settled state text and the chip picks the tone and icon (`toneForState`). Every chip carries its label, so no state is ever conveyed by colour or icon alone.

**Rules**

- Pass the exact operator label in its settled casing: `Not ready`, `Review`, `Held`, `Completed`, `Unidentified`, `Blocked`, `Stale`, `Refreshing`, `Denied`, `Lease held`… The chip never rewrites the words.
- Tones mean things: amber = incomplete/pending, navy = Review and other in-flight states, red = blocked/failed/denied, green = confirmed completion only, neutral = absent, loading, current or settled-terminal. Only override `tone` for a label the map does not know.
- Green never represents progress, availability or a generic positive; it is reserved for confirmed completion.
- On the dark `RecordHead` band the chip drops its border and takes the solid state tint automatically.

**Examples**

```tsx
<StatusChip state="Review" />
<StatusChip state="Not ready" />
<StatusChip state="Unidentified" count={3} />
<StatusChip state="Automation" tone="navy" icon="refresh-cw" />
```
