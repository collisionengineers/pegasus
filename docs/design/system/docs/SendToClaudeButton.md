---
category: Actions
---

`.secondary-action.send-action` — the one recorded divergence from the Pegasus palette (docs/design/README.md, "Reviewed divergence"). The Engineer assessment surface's "Send to Claude" control carries the provider's own identity — terracotta gradient, 12px radius, Poppins-first type, sparkle glyph, blue focus ring — so it reads as Claude on sight and is never mistaken for a Collision Engineers action. Renders a `<button type="button">` with the sparkle and a label span; the default label is `Send to Claude`.

**Rules**

- Use only for the send-to-Claude action on the assessment surface; nowhere else may take the terracotta.
- Its values are local custom properties on the control — do not lift them into tokens or restyle other controls to match.
- Keep the accessible name a plain verb phrase; children replace the label but the sparkle stays.
- Reduced motion removes the lift and sparkle animation; the control keeps its 44px target in every mode.

**Examples**

```tsx
<SendToClaudeButton onClick={send} />

<SendToClaudeButton disabled aria-describedby="send-state">Send assessment to Claude</SendToClaudeButton>
```
