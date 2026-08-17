---
category: Data
---

`.plain-list` — a simple bulleted `<ul>` with the 8px item rhythm and standard left indent. Use it for short prose lists inside a panel or record body: what happened, what is still needed, the contents of an instruction. Facts with terms belong in `Facts`; evidence items in the evidence list.

**Rules**

- Children are plain `<li>` elements with sentence-case business text; no nested controls.
- Three to six items — a longer list is a table or a set of `DataRow`s.
- Do not use it as a navigation menu or as an action row (`ActionList`).

**Examples**

```tsx
<PlainList>
  <li>Instruction received from AXA on 12 Aug 09:14</li>
  <li>Registration LM19 KXR confirmed against the vehicle record</li>
  <li>Engineer not yet assigned</li>
</PlainList>
```
