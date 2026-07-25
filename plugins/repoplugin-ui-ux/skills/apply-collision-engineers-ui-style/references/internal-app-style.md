# Collision Engineers internal-app style

Use system UI text and these semantic tokens. Adjust values only where an approved UI specification requires a contrast or interaction correction.

```css
:root {
  --ce-surface: #ffffff;
  --ce-surface-muted: #f5f4f2;
  --ce-text: #16191d;
  --ce-text-muted: #5f6368;
  --ce-border: #e6e4e1;
  --ce-accent: #db0816;
  --ce-accent-strong: #8f1422;
  --ce-accent-subtle: rgba(219, 8, 22, 0.07);
  --ce-danger: #b42318;
  --ce-success: #16833b;
  --ce-focus: 0 0 0 3px rgba(219, 8, 22, 0.38);
  --ce-radius: 2px;
  --ce-space-1: 4px;
  --ce-space-2: 8px;
  --ce-space-3: 12px;
  --ce-space-4: 16px;
  --ce-space-6: 24px;
}
```

- Use red as emphasis, primary action, or a confirmed status accent; pair it with text or an icon.
- Use charcoal/near-black for important structure and text, muted neutrals for supporting detail, and borders before shadows.
- Use a system UI stack for normal text; Futura is reserved for short display/section labels.
- Keep state, validation, and destructive actions explicit in text and control labels. Do not depend on colour, motion, or an image to convey meaning.
- Keep motion minimal; respect `prefers-reduced-motion`.
