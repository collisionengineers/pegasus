---
category: Status
---

`.blocker` — one unmet requirement in the readiness rail: an `<li>` with the requirement in `<strong>` (`title`) and its resolution in `<small>` (children). The rail tone comes from the state channel via `data-state`; unmet requirements are usually `not-ready` (amber), a hard stop is `blocked` (red). Always render inside `BlockerList`.

**Rules**

- Each blocker names its own field and what resolves it (`Vehicle registration` / `Enter the registration on the Vehicle tab.`) — never a bare "missing data".
- `state` is the only tone switch; do not colour by hand, and the text carries the meaning regardless of colour.
- Keep resolutions to one sentence pointing at where the operator acts (a tab, a field, a person).
- A blocker is not a validation error on submit (`ValidationSummary`) — it describes readiness before the action is attempted.

**Examples**

```tsx
<BlockerList>
  <Blocker title="Vehicle registration">Enter the registration on the Vehicle tab.</Blocker>
  <Blocker state="blocked" title="Principal identity">
    The sender does not match a known principal. Confirm the instructing insurer.
  </Blocker>
</BlockerList>
```
