# Checklist — MAIL-036

- [ ] Record a wipe cutoff atomically with SQL deletion and require a stopped Worker.
- [ ] Preserve the cutoff through poll refresh and reject old notified MIME.
- [ ] Prove existing/missing state and old/new Graph reset paths; update workflow docs.
