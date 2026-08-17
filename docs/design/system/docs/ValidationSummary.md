---
category: Status
---

`.validation-summary-errors` — the red-railed form error summary the tag helper emits, ported so hand-written forms match generated ones. Renders `role="alert"`, an optional heading with the alert-circle glyph (`.validation-summary__heading`), and a `<ul>` of errors (`.validation-summary__list`). Returns nothing when `errors` is empty.

**Rules**

- Place it at the top of the form, above the first field, so the operator sees every problem before scrolling.
- The real summary heading is `Please correct the following errors:` — keep that wording.
- Each error names the field and what to change (`Vehicle registration is required.`), one per bullet, in business language.
- It is for field validation on submit; a failed action's explanation is `FailureDetail` or `StatusCard variant="error"`.

**Examples**

```tsx
<ValidationSummary
  heading="Please correct the following errors:"
  errors={['Vehicle registration is required.', 'Accident date cannot be after today.']}
/>
```
