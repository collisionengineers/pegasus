---
category: Status
---

A left-railed feedback card for a whole surface or section: `info` (navy) explains an in-flight condition, `attention` (amber) names something incomplete or pending, `error` (red) reports a failed action, and `done` (green tick, ink text) confirms an action completed on another page. Every state also carries text, so nothing is conveyed by colour alone.

**Rules**

- Copy is business language: name what happened and what the operator can do — never adapter, queue, lease or deployment mechanics.
- `done` is a one-line `<p role="status">` with the tick; the other variants take an optional `title` (`<h2>`) and one or more paragraphs.
- Give `error` cards `role="alert"`; the others default to no role (pass `role="status"` for live updates).
- Do not use a card as decoration: an empty result is an `EmptyState`, and a consequence beside a control is a `Notice`.

**Examples**

```tsx
<StatusCard variant="done">Case CE-2026-01432 was reopened.</StatusCard>

<StatusCard variant="error" title="Nothing was sent" role="alert">
  The case is unchanged. You can try again.
</StatusCard>

<StatusCard variant="attention">
  Every enabled staff member needs at least one role.
</StatusCard>
```
