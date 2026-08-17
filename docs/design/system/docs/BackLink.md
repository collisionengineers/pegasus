---
category: Shell
---

`.back-link` — a muted return link with the arrow glyph rotated to point back ("Back to Cases"). Use it at the top of a subordinate screen that has one obvious origin and no breadcrumb trail (a form reached from a list, an account page); records use `Crumb` instead.

**Rules**

- Name the destination, not the gesture: "Back to Not ready", never "Back" or "Go back".
- One per screen, first in the content area, before the page heading or record.
- It is navigation, not an action: never use it to cancel a form (that is a `SecondaryAction`).

**Examples**

```tsx
<BackLink href="/Cases">Back to Cases</BackLink>
<BackLink href="/Triage?stage=not-ready">Back to Not ready</BackLink>
```
