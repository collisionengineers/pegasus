# Plan — Retire NOW.md (Stage B, with [[SIMPLI-002]])

1. **Relocate durable facts first** (gate). For each row of the root-plan
   fact table, write the fact into `operations.md` / `open-decisions.md`.
   The Worker-state update is a meaning change to a live-state doc — **confirm
   with user before editing operations.md**.
2. **Update `open-decisions.md:25`** to own the `## Path` sequence instead of
   delegating to NOW.md.
3. **Rewrite AGENTS.md claim model** (done jointly in [[SIMPLI-002]]): the
   claimable unit becomes a Kanmer ticket taken via `take_ticket` (records
   branch/worktree/date/agent); drop "commit a NOW.md claim line / bump the
   NOW.md date".
4. **Retarget/remove** the remaining canonical NOW.md references and the two
   code comments.
5. **Delete `NOW.md`** once no tracked, checked Markdown links to it and every
   fact has a home.

## Acceptance
`NOW.md` gone; no live operational fact lost; no dangling `NOW.md` link.

## Verify
`git grep -n 'NOW.md' -- ':!CHANGELOG.md' ':!docs/temp-plans'` → no live hits;
`pwsh ./scripts/Test-DocumentationLinks.ps1` green.

**Held for user review.** Open decision: transitional NOW.md claim line wanted
during the rewrite? (recommend no.)
