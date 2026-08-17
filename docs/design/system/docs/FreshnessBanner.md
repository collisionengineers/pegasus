---
category: Shell
---

`.freshness-banner` — the full-width freshness strip used by older screens: the `.updated` group on the left ("Updated <time> London" plus a chip when not current) and a right-hand `action`, normally a hairline `Button` with the `refresh-cw` glyph. `status` colours the rail and ground on the state channel: `stale` amber, `loading` navy (`.is-refreshing` spins the icon), `failed` red, `current` plain hairline. New screens use the compact `Refresh` in the page heading instead.

**Rules**

- The chip carries the state as text; the coloured rail is reinforcement, never the only signal.
- Keep the last-good time in every state, including `failed`; a failure never blanks the strip.
- While loading, hold the button (`disabled`, label "Refreshing") so a double submit is impossible; the copy never says anything external succeeded.
- Do not stack it under a `PageHeading` that already carries a `Refresh`.

**Examples**

```tsx
<FreshnessBanner action={<Button icon="refresh-cw">Refresh</Button>}>
  Updated <time>14 Aug 09:32</time> London
</FreshnessBanner>

<FreshnessBanner status="stale" action={<Button icon="refresh-cw">Refresh</Button>}>
  Updated <time>14 Aug 08:05</time> London <StatusChip state="Stale" />
</FreshnessBanner>
```
