# Checklist

- [ ] Triage page loads its origin receipt, degrading silently without the right
- [ ] Photographs selected through `InstructionEvidenceImages.Select`, not a second rule
- [ ] Gallery partial consumed unchanged — no edits to `_ImageGallery.cshtml`
- [ ] Section absent when there are no images; no empty-state panel
- [ ] No new operator-facing sentence
- [ ] Web test: photographs render on a Triage whose receipt carries them
- [ ] Web test: no section at all when the receipt carries none
- [ ] Release build green
- [ ] Core tests green
- [ ] Integration tests green
- [ ] Simplification pass recorded in the plan
- [ ] PR open against `dev`, CI green, independent review passed

## Progress notes
