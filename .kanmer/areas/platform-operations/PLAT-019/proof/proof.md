# Proof — PLAT-019 (verified on deployed release 16, 2026-08-21)

Type: visual. Deployment evidence bundle: [[DELIV-015]] proof.

The latent production defect this ticket fixed — the deployed CSP (`default-src 'self'`) silently discarding the reason-dialog's inline script, leaving the buttons dead — is verified fixed live: on the production message page (EREF6), clicking **Correct classification** opened the dialog under the deployed CSP (classification select, Other-category fields with the data-other-toggle behaviour, required reason, Cancel/Save). The binder now lives in `site.js` (served same-origin), the inline script is deleted, and the `_ReasonDialog` partial carries no mechanism/hint copy (design rails).
