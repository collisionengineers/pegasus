---
category: Auth
---

`.auth-card__reference` — the support reference: the request id in `<code>` (single line, ellipsised) beside a compact `Copy` `.btn`. It is the content of an `AuthCard` `foot` after the words `Support reference`, so the operator can quote the id to an administrator without it taking hero prominence on the card.

**Rules**

- Always prefixed by `Support reference` in the foot; the code alone is meaningless to the operator.
- The id is the request identifier only — never a stack trace, exception text or environment name.
- Wire `onCopy` to the clipboard; the button keeps its position so the foot does not reflow.

**Examples**

```tsx
<AuthCard title="We could not complete that request" fault
  foot={<>Support reference <SupportReference reference="0HN5K2Q9V3R7L:00000012" onCopy={copy} /></>}>
  …
</AuthCard>
```
