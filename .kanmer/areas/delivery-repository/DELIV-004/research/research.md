# Research — DELIV-004: gated-off feature policy

## Question

Do the existing repository rules already prohibit shipping features behind a
closed composition or feature gate, and what must DELIV-004 change?

## Findings

- `AGENTS.md` requires claims to prove the actual caller and distinguishes a
  registration, green build, and deployed feature. It also makes the
  `docs/engineering.md` simplicity rules binding on every task.
- `docs/engineering.md` § “Abstractions and deferred capabilities” explicitly
  requires deferred capability work to live in `docs/capabilities.md` or
  `docs/open-decisions.md`, never as dormant registration, an unused endpoint,
  a disabled flag, or dark destructive code.
  - It further requires anything built but unwired for two weeks to gain a real
    caller or be deleted.
- The existing wording therefore already rejects dark, disabled implementation
  as the way to defer work. It does not, however, state the user's categorical
  release rule in `AGENTS.md`: a closed gate means the feature is not shipped
  and cannot be claimed as implemented.
- `AGENTS.md` places repository rules in itself and currently references the
  Engineering rules rather than repeating this release claim. The clarification
  belongs in `AGENTS.md`, with `docs/engineering.md` retained as the detailed
  anti-dormancy authority.

## Implications

DELIV-004 should add one concise, unambiguous repository rule to `AGENTS.md`
that closed-gate behaviour is not shipped, delivered, or claimable; a gate is
not a deferral mechanism. It should align with—not duplicate or weaken—the
existing Engineering prohibition on dormant registrations, unused endpoints,
disabled flags, and dark destructive code.

## Open questions

None. The user has set the intended rule: we do not ship features gated off.
