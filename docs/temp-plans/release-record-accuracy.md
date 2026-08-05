# Release-record accuracy after the documentation-only merge

## Why

PR 353 merged `dev` into `main` carrying documentation only. It was the right
merge and it correctly required no release: nothing under `src/` changed, so
the built artifact the estate serves is unaffected.

But it left two records saying something that is no longer literally true.

`NOW.md` § Merged, not deployed said release 7 "carries everything currently in
`dev` and `main`". After PR 353 it does not — `dev` and `main` are four commits
ahead of `32feefa…`.

`docs/operations.md` § Production environment said "the estate currently serves
**release 7**" against a table whose newest source revision is `32feefa…`, with
nothing to explain a branch head ahead of it.

Both readings are individually defensible and jointly misleading. Someone
comparing `git log origin/main` against the release table finds an unexplained
gap, and the only two available conclusions are both wrong: either the table is
stale, or a release is owed.

## What this changes

Nothing about the estate, the branches, or any artifact. Two prose records
gain the rule they were already following.

- `NOW.md` § Merged, not deployed: "Nothing" becomes "Nothing that needs a
  release", the coverage claim narrows to "every source change in `dev` and
  `main`", and the documentation-only commits are named as riding the next
  functional release.
- `docs/operations.md` § Deployed evidence, release 7 note: states the rule
  directly — **a source revision ahead of the table is a release claim only
  when it changes something under `src/`.**

The rule belongs in `operations.md` because that file owns deployed evidence;
`NOW.md` states the current instance of it, which is what `NOW.md` is for.

`NOW.md` names `19d0abf` "at the time of writing" rather than as a fixed fact,
because the head moves and a pinned hash there would go stale within days. The
durable statement is the rule in `operations.md`, which needs no hash at all.

## Scope

Documentation only. No source change, no test change, no release, no
deployment. CI skips every build lane by design; `changes` and `documentation`
gate it.

## Verification

- `documentation` CI job (link and structure check across the docs tree).
- Read-back of both sections against `git log origin/main..` and the release
  table, confirming the two now agree.
