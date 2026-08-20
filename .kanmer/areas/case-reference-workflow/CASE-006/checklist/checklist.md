# CASE-006 checklist

- [ ] Core: `IntakeSourceDownload` carries stored media type; Source page behaviour unchanged
- [ ] Core+Ef: `ListImagesAsync` returns the intake's ordered image receipts
- [ ] `/Received/{id}/Image` endpoint: staff-only, image/* only, inline, nosniff, no-store
- [ ] Shared gallery partial + site.css styles (no inline styles)
- [ ] ImageIntake Details renders the gallery
- [ ] Case Details evidence tab renders per-intake galleries (both case kinds)
- [ ] Web tests: inline image/*, 404 non-image, anonymous redirect, roleless 403, galleries render
- [ ] Browser + accessibility suites green; octet-stream pin green
- [ ] Zero-warning build; simplification pass; PR (base INTK-014 branch, merge order noted); ticket → review
